using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Context;
using BIMPills.UI.ModelAudit;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using System.Windows.Threading;

namespace BIMPills.Revit.Commands.ModelAudit
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class ModelAuditRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new ModelAuditCommand();

        // Progress window — kept alive during analysis, closed in OnSuccess / on error
        private AuditProgressWindow? _progressWindow;

        protected override ICommandContext CreateContext(ExternalCommandData commandData)
        {
            _progressWindow = new AuditProgressWindow();
            _progressWindow.Show();
            // Force initial render before heavy analysis begins
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));

            return new RevitCommandContext(commandData, (current, total, phase) =>
            {
                _progressWindow?.SetProgress(current, total, phase);
                // Pump the WPF dispatcher so the window repaints during the Revit main-thread analysis
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            });
        }

        protected override void OnSuccess(IPluginCommand command)
        {
            _progressWindow?.Close();
            _progressWindow = null;

            if (ModelAuditCommand.LastResult == null) return;

            var doc = CommandData?.Application.ActiveUIDocument.Document;

            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;

            Func<IReadOnlyList<long>, PurgeCallbackResult>? purgeCallback = null;
            if (doc != null)
            {
                purgeCallback = ids =>
                {
                    logger?.Info($"[ModelAudit] Iniciando purga de {ids.Count} elementos...");

                    // ── Fast path: delete everything in one batch transaction.
                    // One transaction vs N transactions = dramatically less overhead.
                    var batchResult = TryBatchDelete(doc, ids, logger);
                    if (batchResult != null)
                    {
                        logger?.Info($"[ModelAudit] Purga batch: {batchResult.DeletedIds.Count} eliminados.");
                        return batchResult;
                    }

                    // ── Fallback: batch failed — use binary-split strategy.
                    // Split the list in half, retry each half as a batch.
                    // Recursively isolates blocked elements in O(log N) batches
                    // instead of N individual transactions.
                    logger?.Info("[ModelAudit] Batch rechazado — binary-split fallback...");
                    var deletedIds = new List<long>();
                    var failedItems = new List<(long Id, string Name, string Reason)>();
                    BinarySplitDelete(doc, ids, deletedIds, failedItems, logger);

                    logger?.Info($"[ModelAudit] Purga: {deletedIds.Count} eliminados, {failedItems.Count} omitidos.");
                    return new PurgeCallbackResult(deletedIds, failedItems);
                };
            }

            new ModelAuditWindow(ModelAuditCommand.LastResult, purgeCallback).ShowDialogOverRevit();
        }

        // Recursively splits the list and attempts batch deletes on each half.
        // When a sub-list reaches size 1 and still fails, records it as a failed item.
        // This achieves O(log N) transactions in the best case vs O(N) one-by-one.
        private static void BinarySplitDelete(
            Document doc,
            IReadOnlyList<long> ids,
            List<long> deletedIds,
            List<(long Id, string Name, string Reason)> failedItems,
            ILogger? logger)
        {
            if (ids.Count == 0) return;

            // Single element — must go individual to capture failure reason
            if (ids.Count == 1)
            {
                var id = ids[0];
                var elementId = new ElementId(id);
                var elem = doc.GetElement(elementId);
                if (elem == null) return;

                var elemName = elem.Name ?? $"Id {id}";
                var preprocessor = new SilentFailuresPreprocessor(logger, id);
                Transaction? trans = null;
                try
                {
                    trans = new Transaction(doc, "BIMPills - Purgar elemento");
                    var fo = trans.GetFailureHandlingOptions();
                    fo.SetFailuresPreprocessor(preprocessor);
                    fo.SetClearAfterRollback(true);
                    trans.SetFailureHandlingOptions(fo);

                    if (trans.Start() != TransactionStatus.Started)
                    {
                        failedItems.Add((id, elemName, "No se pudo iniciar la transacción"));
                        return;
                    }
                    if (elem.Pinned) elem.Pinned = false;
                    doc.Delete(elementId);

                    if (trans.Commit() == TransactionStatus.Committed)
                        deletedIds.Add(id);
                    else
                        failedItems.Add((id, elemName, preprocessor.FailureReason ?? "Commit rechazado"));
                }
                catch (Exception ex)
                {
                    try { if (trans?.GetStatus() == TransactionStatus.Started) trans.RollBack(); } catch { }
                    failedItems.Add((id, elemName, ex.Message));
                }
                finally { trans?.Dispose(); }
                return;
            }

            // Try the whole sub-list as a batch first
            var batchResult = TryBatchDelete(doc, ids, logger);
            if (batchResult != null)
            {
                deletedIds.AddRange(batchResult.DeletedIds);
                return;
            }

            // Batch failed — split and recurse
            int mid = ids.Count / 2;
            var left  = ids.Take(mid).ToList();
            var right = ids.Skip(mid).ToList();
            BinarySplitDelete(doc, left,  deletedIds, failedItems, logger);
            BinarySplitDelete(doc, right, deletedIds, failedItems, logger);
        }

        // Attempts to delete all elements in a single transaction.
        // Returns a successful PurgeCallbackResult on commit, or null if the batch rolled back.
        private static PurgeCallbackResult? TryBatchDelete(Document doc, IReadOnlyList<long> ids, ILogger? logger)
        {
            var preprocessor = new SilentFailuresPreprocessor(logger, -1);
            using var trans = new Transaction(doc, "BIMPills - Purgar (lote)");
            try
            {
                var fo = trans.GetFailureHandlingOptions();
                fo.SetFailuresPreprocessor(preprocessor);
                fo.SetClearAfterRollback(true);
                trans.SetFailureHandlingOptions(fo);

                if (trans.Start() != TransactionStatus.Started) return null;

                var toDelete = new List<ElementId>(ids.Count);
                foreach (var id in ids)
                {
                    var elementId = new ElementId(id);
                    var elem = doc.GetElement(elementId);
                    if (elem == null) continue;
                    if (elem.Pinned) elem.Pinned = false;
                    toDelete.Add(elementId);
                }

                doc.Delete(toDelete);

                if (trans.Commit() == TransactionStatus.Committed)
                    return new PurgeCallbackResult(ids, Array.Empty<(long, string, string)>());

                return null;
            }
            catch
            {
                try { if (trans.GetStatus() == TransactionStatus.Started) trans.RollBack(); }
                catch { }
                return null;
            }
        }

        private sealed class SilentFailuresPreprocessor : IFailuresPreprocessor
        {
            private readonly ILogger? _logger;
            private readonly long _elementId;

            public string? FailureReason { get; private set; }

            public SilentFailuresPreprocessor(ILogger? logger, long elementId)
            {
                _logger = logger;
                _elementId = elementId;
            }

            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                var failures = accessor.GetFailureMessages();
                if (failures.Count == 0) return FailureProcessingResult.Continue;

                foreach (var msg in failures)
                {
                    var severity = msg.GetSeverity();
                    var description = msg.GetDescriptionText();
                    _logger?.Warning($"[ModelAudit] Id={_elementId} — {severity}: {description}");

                    if (severity != FailureSeverity.Warning)
                    {
                        FailureReason ??= description;
                        return FailureProcessingResult.ProceedWithRollBack;
                    }

                    accessor.DeleteWarning(msg);
                }
                return FailureProcessingResult.Continue;
            }
        }
    }

    /// <summary>
    /// Ventana de progreso mostrada durante el análisis del modelo.
    /// Se actualiza desde el hilo principal de Revit vía Dispatcher.Invoke.
    /// </summary>
    internal sealed class AuditProgressWindow : Window
    {
        private readonly ProgressBar _bar;
        private readonly TextBlock   _phaseText;
        private readonly TextBlock   _percentText;

        public AuditProgressWindow()
        {
            Title                  = "BIM Pills";
            Width                  = 380;
            SizeToContent          = SizeToContent.Height;
            ResizeMode             = ResizeMode.NoResize;
            WindowStartupLocation  = WindowStartupLocation.CenterOwner;
            WindowStyle            = WindowStyle.None;
            AllowsTransparency     = true;
            Background             = Brushes.Transparent;
            ShowInTaskbar          = false;
            Topmost                = true;

            var outerBorder = new Border
            {
                Background    = Brushes.White,
                CornerRadius  = new CornerRadius(12),
                BorderBrush   = new SolidColorBrush(WpfColor.FromRgb(229, 229, 234)),
                BorderThickness = new Thickness(1),
                Margin        = new Thickness(12)
            };
            outerBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black, Opacity = 0.16, BlurRadius = 20, ShadowDepth = 3, Direction = 270
            };

            var stack = new StackPanel { Margin = new Thickness(24, 20, 24, 24) };

            // Header row: icon + title
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            headerRow.Children.Add(new TextBlock
            {
                Text              = "⚙",
                FontSize          = 20,
                Foreground        = new SolidColorBrush(WpfColor.FromRgb(21, 101, 192)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            });
            headerRow.Children.Add(new TextBlock
            {
                Text              = "Analizando modelo",
                FontSize          = 14,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = new SolidColorBrush(WpfColor.FromRgb(28, 28, 30)),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(headerRow);

            // Phase label
            _phaseText = new TextBlock
            {
                Text       = "Iniciando...",
                FontSize   = 12,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(110, 110, 115)),
                Margin     = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(_phaseText);

            // Progress bar
            _bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value   = 0,
                Height  = 6,
                Margin  = new Thickness(0, 0, 0, 6),
                Foreground = new SolidColorBrush(WpfColor.FromRgb(21, 101, 192)),
                Background = new SolidColorBrush(WpfColor.FromRgb(229, 229, 234))
            };
            stack.Children.Add(_bar);

            // Percent label (right-aligned)
            _percentText = new TextBlock
            {
                Text              = "0 %",
                FontSize          = 11,
                Foreground        = new SolidColorBrush(WpfColor.FromRgb(110, 110, 115)),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            stack.Children.Add(_percentText);

            outerBorder.Child = stack;
            Content           = outerBorder;

            // Drag to move
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
        }

        public void SetProgress(int current, int total, string phase)
        {
            var pct = total > 0 ? (int)((current / (double)total) * 100) : 0;
            _bar.Value      = pct;
            _phaseText.Text = phase;
            _percentText.Text = $"{pct} %";
        }
    }
}
