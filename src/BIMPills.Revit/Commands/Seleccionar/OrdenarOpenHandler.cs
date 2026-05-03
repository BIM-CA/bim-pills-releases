using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Models;
using BIMPills.Revit.Commands.Ordering;
using BIMPills.UI.Ordering;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.Seleccionar
{
    /// <summary>
    /// Abre OrdenarWindow + OrdenarSessionWindow desde la galería Organizar.
    /// Se ejecuta en el hilo de Revit vía ExternalEvent para poder
    /// acceder a la API (FilteredElementCollector, etc.).
    /// </summary>
    public sealed class OrdenarOpenHandler : IExternalEventHandler
    {
        /// <summary>Owner WPF window (the Organizar gallery). Set before raising the event.</summary>
        public System.Windows.Window? OwnerWindow { get; set; }

        public void Execute(UIApplication app)
        {
            var doc    = app.ActiveUIDocument.Document;
            var viewId = doc.ActiveView.Id;

            // ── Categorías filtradas por vista activa ─────────────────────────
            Func<string, IReadOnlyList<string>> getCategoriesByType = type =>
            {
                var catType = type == "Modelo" ? CategoryType.Model : CategoryType.Annotation;
                try
                {
                    return new FilteredElementCollector(doc, viewId)
                        .WhereElementIsNotElementType()
                        .Cast<Element>()
                        .Where(e => e.Category?.CategoryType == catType)
                        .Select(e => e.Category!.Name)
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList();
                }
                catch
                {
                    return doc.Settings.Categories
                        .Cast<Category>()
                        .Where(c => c.CategoryType == catType &&
                                    (catType == CategoryType.Annotation || c.AllowsBoundParameters))
                        .Select(c => c.Name)
                        .OrderBy(n => n)
                        .ToList();
                }
            };

            // ── Parámetros editables de la categoría ──────────────────────────
            Func<string, IReadOnlyList<string>> getParams = categoryName =>
            {
                try
                {
                    var elemsInView = new FilteredElementCollector(doc, viewId)
                        .WhereElementIsNotElementType()
                        .Cast<Element>()
                        .Where(e => e.Category?.Name.Equals(categoryName,
                            StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();

                    if (elemsInView.Count == 0)
                    {
                        var cat = doc.Settings.Categories
                            .Cast<Category>()
                            .FirstOrDefault(c => c.Name.Equals(categoryName,
                                StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                        {
                            var fallback = new FilteredElementCollector(doc)
                                .OfCategoryId(cat.Id)
                                .WhereElementIsNotElementType()
                                .FirstElement();
                            if (fallback != null)
                                elemsInView = new List<Element> { fallback };
                        }
                    }

                    if (elemsInView.Count == 0) return new List<string>();

                    var seen     = new HashSet<ElementId>();
                    var allNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var elem in elemsInView)
                    {
                        foreach (var p in CollectParameters(elem))
                            allNames.Add(p);

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

            // ── Wizard de configuración ───────────────────────────────────────
            var configWindow = new OrdenarWindow(getCategoriesByType, getParams);
            if (OwnerWindow != null)
                configWindow.Owner = OwnerWindow;
            if (configWindow.ShowDialogOverRevit() != true) return;

            // ── Sesión de numeración ──────────────────────────────────────────
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
                onPickDone:      cb => { pickHandler.OnValueAssigned = cb; },
                onUndoDone:      cb => { undoHandler.OnUndone        = cb; },
                onPickCancelled: cb => { pickHandler.OnPickCancelled = cb; });

            sessionWindow.ShowOverRevit();
        }

        public string GetName() => "BIMPills: OrdenarOpenHandler";

        private static List<string> CollectParameters(Element elem) =>
            elem.Parameters
                .Cast<Parameter>()
                .Where(p => p.Definition?.Name != null && !p.IsReadOnly)
                .Select(p => p.Definition.Name)
                .Distinct()
                .ToList();
    }
}
