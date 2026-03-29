using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMPills.Core.Models;
using System;
using System.Collections.Generic;

namespace BIMPills.Revit.Commands.Ordering
{
    /// <summary>
    /// IExternalEventHandler that picks one element and assigns the next
    /// incremental value to the configured parameter.
    /// </summary>
    public class OrderingPickHandler : IExternalEventHandler
    {
        private readonly OrderingSessionState _session;

        /// <summary>Called on the WPF thread after each successful assignment.</summary>
        public Action<int>? OnValueAssigned { get; set; }

        /// <summary>Called when the user presses Esc during PickObject.</summary>
        public Action? OnPickCancelled { get; set; }

        public OrderingPickHandler(OrderingSessionState session)
        {
            _session = session;
        }

        public void Execute(UIApplication app)
        {
            if (!_session.IsActive) return;

            var uiDoc = app.ActiveUIDocument;
            var doc   = uiDoc.Document;

            try
            {
                var filter = new CategoryNameSelectionFilter(
                    doc, _session.Config.CategoryName);

                var reference = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    filter,
                    $"Selecciona elemento — Valor: {_session.Config.FormatValue(_session.CurrentValue)}  |  Esc para cancelar");

                var element = doc.GetElement(reference);
                var param   = element?.LookupParameter(_session.Config.ParameterName);

                if (param == null || param.IsReadOnly)
                {
                    // Silently skip — not a valid target
                    OnValueAssigned?.Invoke(_session.CurrentValue);
                    return;
                }

                string prevValue = "";
                try { prevValue = param.AsString() ?? param.AsValueString() ?? ""; } catch { }

                var newValue = _session.Config.FormatValue(_session.CurrentValue);

                using var tx = new Transaction(doc, $"BIMPills: Ordenar — {newValue}");
                tx.Start();
                param.Set(newValue);
                tx.Commit();

                _session.History.Add(new OrderingHistoryEntry
                {
                    ElementId     = GetElementIdValue(element.Id),
                    PreviousValue = prevValue,
                    AssignedValue = newValue
                });

                _session.CurrentValue += _session.Config.Step;
                OnValueAssigned?.Invoke(_session.CurrentValue);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc pressed — notify floating window but keep session active
                OnPickCancelled?.Invoke();
            }
        }

        public string GetName() => "BIMPills: OrderingPickHandler";

        private static long GetElementIdValue(ElementId id)
        {
#if REVIT2024
            return id.IntegerValue;
#else
            return id.Value;
#endif
        }
    }

    /// <summary>
    /// Selection filter that accepts only elements of a specific category name.
    /// </summary>
    internal class CategoryNameSelectionFilter : ISelectionFilter
    {
        private readonly Document _doc;
        private readonly string   _categoryName;

        public CategoryNameSelectionFilter(Document doc, string categoryName)
        {
            _doc          = doc;
            _categoryName = categoryName ?? "";
        }

        public bool AllowElement(Element elem)
        {
            if (string.IsNullOrEmpty(_categoryName)) return true;
            return elem.Category?.Name
                       .IndexOf(_categoryName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
