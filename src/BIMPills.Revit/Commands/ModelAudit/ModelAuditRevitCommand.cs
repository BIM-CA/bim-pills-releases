using Autodesk.Revit.DB;
using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.UI.ModelAudit;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;

namespace BIMPills.Revit.Commands.ModelAudit
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class ModelAuditRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new ModelAuditCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            if (ModelAuditCommand.LastResult == null) return;

            var doc = CommandData?.Application.ActiveUIDocument.Document;

            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;

            Func<IReadOnlyList<long>, PurgeCallbackResult>? purgeCallback = null;
            if (doc != null)
            {
                purgeCallback = ids =>
                {
                    logger?.Info($"[ModelAudit] Iniciando purga de {ids.Count} elementos...");
                    var deletedIds = new List<long>();
                    var failedItems = new List<(long Id, string Name, string Reason)>();

                    foreach (var id in ids)
                    {
                        Transaction? trans = null;
                        var preprocessor = new SilentFailuresPreprocessor(logger, id);
                        try
                        {
                            var elementId = new ElementId(id);
                            var elem = doc.GetElement(elementId);
                            if (elem == null) continue;

                            var elemName = elem.Name ?? $"Id {id}";

                            trans = new Transaction(doc, "BIMPills - Purgar elemento");

                            var failOpts = trans.GetFailureHandlingOptions();
                            failOpts.SetFailuresPreprocessor(preprocessor);
                            failOpts.SetClearAfterRollback(true);
                            trans.SetFailureHandlingOptions(failOpts);

                            var startStatus = trans.Start();
                            if (startStatus != TransactionStatus.Started)
                            {
                                logger?.Warning($"[ModelAudit] No se pudo iniciar transacción para Id={id}");
                                failedItems.Add((id, elemName, "No se pudo iniciar la transacción"));
                                continue;
                            }

                            if (elem.Pinned)
                                elem.Pinned = false;

                            doc.Delete(elementId);

                            var commitStatus = trans.Commit();
                            if (commitStatus == TransactionStatus.Committed)
                                deletedIds.Add(id);
                            else
                            {
                                var reason = preprocessor.FailureReason ?? $"Commit: {commitStatus}";
                                logger?.Warning($"[ModelAudit] Commit rechazado para Id={id}: {reason}");
                                failedItems.Add((id, elemName, reason));
                            }
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                if (trans?.GetStatus() == TransactionStatus.Started)
                                    trans.RollBack();
                            }
                            catch { }
                            var elemName = $"Id {id}";
                            logger?.Warning($"[ModelAudit] No se pudo eliminar Id={id}: {ex.Message}");
                            failedItems.Add((id, elemName, ex.Message));
                        }
                        finally
                        {
                            trans?.Dispose();
                        }
                    }
                    logger?.Info($"[ModelAudit] Purga: {deletedIds.Count} eliminados, {failedItems.Count} omitidos.");
                    return new PurgeCallbackResult(deletedIds, failedItems);
                };
            }

            new ModelAuditWindow(ModelAuditCommand.LastResult, purgeCallback).ShowDialogOverRevit();
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
}
