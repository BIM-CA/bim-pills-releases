using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Commands.Ordering;
using BIMPills.Core.Commands;
using BIMPills.Core.Models;
using BIMPills.Revit.Commands;
using BIMPills.UI.Ordering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.Ordering
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class OrdenarRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new OrderingCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            var uiApp  = CommandData!.Application;
            var doc    = uiApp.ActiveUIDocument.Document;
            var viewId = doc.ActiveView.Id;

            // ── Categorías filtradas por vista activa ────────────────────────────
            // Sólo aparecen las categorías que tienen elementos presentes en la vista.
            Func<string, IReadOnlyList<string>> getCategoriesByType = type =>
            {
                var catType = type == "Modelo" ? CategoryType.Model : CategoryType.Annotation;
                try
                {
                    var cats = new FilteredElementCollector(doc, viewId)
                        .WhereElementIsNotElementType()
                        .Cast<Element>()
                        .Where(e => e.Category?.CategoryType == catType)
                        .Select(e => e.Category!.Name)
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList();

                    return cats;
                }
                catch
                {
                    // Fallback si la vista no admite colector (ej. tabla de planificación)
                    return doc.Settings.Categories
                        .Cast<Category>()
                        .Where(c => c.CategoryType == catType &&
                                    (catType == CategoryType.Annotation || c.AllowsBoundParameters))
                        .Select(c => c.Name)
                        .OrderBy(n => n)
                        .ToList();
                }
            };

            // ── Parámetros editables — instancia + tipo, unión de todas las familias ─
            // Se recorren TODOS los elementos de la categoría en la vista para reunir
            // los parámetros de cada familia distinta; así no se pierde ningún campo.
            Func<string, IReadOnlyList<string>> getParams = categoryName =>
            {
                try
                {
                    // Elementos de la categoría en la vista activa
                    var elemsInView = new FilteredElementCollector(doc, viewId)
                        .WhereElementIsNotElementType()
                        .Cast<Element>()
                        .Where(e => e.Category?.Name.Equals(categoryName,
                            StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();

                    // Fallback al documento completo si no hay nada en la vista
                    if (elemsInView.Count == 0)
                    {
                        var cat = doc.Settings.Categories
                            .Cast<Category>()
                            .FirstOrDefault(c => c.Name.Equals(categoryName,
                                StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                        {
                            var fallbackElem = new FilteredElementCollector(doc)
                                .OfCategoryId(cat.Id)
                                .WhereElementIsNotElementType()
                                .FirstElement();
                            if (fallbackElem != null)
                                elemsInView = new List<Element> { fallbackElem };
                        }
                    }

                    if (elemsInView.Count == 0) return new List<string>();

                    // Acumular parámetros de instancia y tipo de cada familia distinta.
                    // Se usa el TypeId como clave para no procesar la misma familia dos veces.
                    var seen     = new HashSet<ElementId>();
                    var allNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var elem in elemsInView)
                    {
                        // Parámetros de instancia (siempre, para cada elemento único)
                        foreach (var p in CollectParameters(elem))
                            allNames.Add(p);

                        // Parámetros de tipo — uno por TypeId único
                        var typeId = elem.GetTypeId();
                        if (typeId != ElementId.InvalidElementId && seen.Add(typeId))
                        {
                            var typeElem = doc.GetElement(typeId);
                            if (typeElem != null)
                                foreach (var p in CollectParameters(typeElem))
                                    allNames.Add(p);
                        }
                    }

                    return allNames.OrderBy(n => n).ToList();
                }
                catch { return new List<string>(); }
            };

            // Ventana de configuración (wizard de 4 pasos)
            var configWindow = new OrdenarWindow(getCategoriesByType, getParams);
            if (configWindow.ShowDialog() != true) return;

            var config  = configWindow.Config;
            var session = new OrderingSessionState
            {
                Config       = config,
                CurrentValue = config.StartValue,
                IsActive     = true
            };

            var pickHandler = new OrderingPickHandler(session);
            var undoHandler = new OrderingUndoHandler(session);
            var pickEvent   = ExternalEvent.Create(pickHandler);
            var undoEvent   = ExternalEvent.Create(undoHandler);

            var sessionWindow = new OrdenarSessionWindow(
                session,
                raisePick:       () => pickEvent.Raise(),
                raiseUndo:       () => undoEvent.Raise(),
                onPickDone:      cb => { pickHandler.OnValueAssigned  = cb; },
                onUndoDone:      cb => { undoHandler.OnUndone         = cb; },
                onPickCancelled: cb => { pickHandler.OnPickCancelled  = cb; });

            sessionWindow.Show();
        }

        private static List<string> CollectParameters(Element elem)
        {
            return elem.Parameters
                .Cast<Parameter>()
                .Where(p => p.Definition?.Name != null && !p.IsReadOnly)
                .Select(p => p.Definition.Name)
                .Distinct()
                .ToList();
        }
    }
}
