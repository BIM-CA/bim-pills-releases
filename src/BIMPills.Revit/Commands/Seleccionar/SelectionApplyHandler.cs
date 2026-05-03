using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Seleccionar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.Seleccionar
{
    /// <summary>
    /// Aplica un SelectionFilterConfig al documento activo y selecciona los elementos
    /// que coinciden. Se ejecuta en el hilo de Revit vía ExternalEvent.
    /// </summary>
    public sealed class SelectionApplyHandler : IExternalEventHandler
    {
        public SelectionFilterConfig? Filter { get; set; }
        public Action<int>? OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            if (Filter == null) return;

            var uiDoc = app.ActiveUIDocument;
            var doc   = uiDoc.Document;

            try
            {
                var effectiveCats = Filter.EffectiveCategoryNames;
                var baseCollector = new FilteredElementCollector(doc, doc.ActiveView.Id)
                                        .WhereElementIsNotElementType();

                IEnumerable<Element> collector;
                if (effectiveCats.Count == 0)
                {
                    collector = baseCollector.Cast<Element>();
                }
                else
                {
                    var catSet = new HashSet<string>(effectiveCats, StringComparer.OrdinalIgnoreCase);
                    collector = baseCollector.Cast<Element>()
                        .Where(e => e.Category?.Name != null && catSet.Contains(e.Category.Name));
                }

                var matching = new HashSet<ElementId>();

                foreach (var element in collector)
                {
                    try
                    {
                        var parameters = ReadParameters(element, doc);
                        if (Filter.Evaluate(parameters))
                            matching.Add(element.Id);
                    }
                    catch { /* elemento no accesible — continuar con el resto */ }
                }

                ICollection<ElementId> finalIds;
                switch (Filter.Action)
                {
                    case SelectionAction.Add:
                        var current = new HashSet<ElementId>(uiDoc.Selection.GetElementIds());
                        current.UnionWith(matching);
                        finalIds = current;
                        break;

                    case SelectionAction.Remove:
                        var existing = new HashSet<ElementId>(uiDoc.Selection.GetElementIds());
                        existing.ExceptWith(matching);
                        finalIds = existing;
                        break;

                    default: // Replace
                        finalIds = matching;
                        break;
                }

                uiDoc.Selection.SetElementIds(finalIds.ToList());
                OnCompleted?.Invoke(finalIds.Count);
            }
            catch
            {
                // silenciar — el usuario puede haber cerrado la vista.
                // Notificar igualmente con 0 para que la UI actualice el contador.
                try { OnCompleted?.Invoke(0); } catch { }
            }
        }

        public string GetName() => "BIMPills: SelectionApplyHandler";

        private static IReadOnlyDictionary<string, string> ReadParameters(Element element, Document doc)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Parámetros de instancia
            ReadIntoDict(element.Parameters, dict, overwrite: false);

            // Parámetros de tipo — no sobreescriben instancia si coinciden en nombre
            var typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var typeElem = doc.GetElement(typeId);
                if (typeElem != null)
                    ReadIntoDict(typeElem.Parameters, dict, overwrite: false);
            }

            return dict;
        }

        private static void ReadIntoDict(ParameterSet parameters, Dictionary<string, string> dict, bool overwrite)
        {
            foreach (Parameter p in parameters)
            {
                if (p.Definition?.Name == null) continue;
                if (!overwrite && dict.ContainsKey(p.Definition.Name)) continue;
                try
                {
                    var value = p.StorageType switch
                    {
                        StorageType.String    => p.AsString() ?? string.Empty,
                        StorageType.Integer   => p.AsInteger().ToString(),
                        StorageType.Double    => p.AsValueString() ?? p.AsDouble().ToString("G"),
                        StorageType.ElementId => p.AsValueString() ?? string.Empty,
                        _                     => string.Empty
                    };
                    dict[p.Definition.Name] = value;
                }
                catch { /* parámetro no accesible — ignorar */ }
            }
        }
    }
}
