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
            var uiApp = CommandData!.Application;
            var doc   = uiApp.ActiveUIDocument.Document;

            // Categorías por tipo (Modelo / Anotación) — paso 1 del wizard
            Func<string, IReadOnlyList<string>> getCategoriesByType = type =>
            {
                var catType = type == "Modelo" ? CategoryType.Model : CategoryType.Annotation;
                var cats = doc.Settings.Categories
                    .Cast<Category>()
                    .Where(c => c.AllowsBoundParameters && c.CategoryType == catType)
                    .Select(c => c.Name)
                    .OrderBy(n => n)
                    .Distinct()
                    .ToList();

                // Asegurar que Ejes (Grids) aparezca en Anotación aunque no esté en Settings
                if (catType == CategoryType.Annotation &&
                    !cats.Any(n => n.Equals("Ejes", StringComparison.OrdinalIgnoreCase) ||
                                   n.Equals("Grids", StringComparison.OrdinalIgnoreCase)))
                {
                    cats.Insert(0, "Ejes");
                }

                return cats;
            };

            // Parámetros por categoría — instancia + tipo, sin duplicados
            Func<string, IReadOnlyList<string>> getParams = categoryName =>
            {
                try
                {
                    var cat = doc.Settings.Categories
                        .Cast<Category>()
                        .FirstOrDefault(c => c.Name.Equals(categoryName,
                            StringComparison.OrdinalIgnoreCase));

                    // Caso especial: Ejes / Grids
                    if (cat == null &&
                        (categoryName.Equals("Ejes", StringComparison.OrdinalIgnoreCase) ||
                         categoryName.Equals("Grids", StringComparison.OrdinalIgnoreCase)))
                    {
                        var grid = new FilteredElementCollector(doc)
                            .OfClass(typeof(Grid))
                            .FirstElement();
                        if (grid == null) return new List<string>();
                        return CollectParameters(grid);
                    }

                    if (cat == null) return new List<string>();

                    var elem = new FilteredElementCollector(doc)
                        .OfCategoryId(cat.Id)
                        .WhereElementIsNotElementType()
                        .FirstElement();
                    if (elem == null) return new List<string>();

                    // Parámetros de instancia
                    var paramNames = CollectParameters(elem);

                    // Parámetros de tipo
                    var typeElem = doc.GetElement(elem.GetTypeId());
                    if (typeElem != null)
                    {
                        foreach (var p in CollectParameters(typeElem))
                            if (!paramNames.Contains(p))
                                paramNames.Add(p);
                    }

                    return paramNames.OrderBy(n => n).ToList();
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
