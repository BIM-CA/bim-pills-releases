using Autodesk.Revit.DB;
using BIMPills.Commands.ParameterExtractor;
using BIMPills.Core.Commands;
using BIMPills.Core.ParameterExtractor;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Persistence;
using BIMPills.Core.Services;
using BIMPills.Revit.Commands;
using BIMPills.UI.Export;
using BIMPills.UI.Shared;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.ParameterExtractor
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class ExtractorRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new ExtractorCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>()
                ? ServiceLocator.Get<ILogger>() : null;

            var uidoc = CommandData!.Application.ActiveUIDocument;
            var doc   = uidoc.Document;

            var currentSelection = uidoc.Selection.GetElementIds().ToList();

            // Scan full model so all categories appear (e.g. Líneas de Propiedad)
            var (categories, paramsByCategory, hasCurveByCategory, familyTypesByCategory) = ExtractorCategoryResolver.ResolveFromModel(doc);

            var window = new ExportarWindow();
            window.SetDocumentName(doc.Title ?? "Modelo");

            window.InitializeExtractor(
                selectedElementCount: currentSelection.Count,
                applyCallback: config =>
                {
                    try
                    {
                        // Resolve target elements based on scope
                        IList<ElementId> targets;
                        switch (config.Scope)
                        {
                            case ExtractionScope.WholeModel:
                                targets = new FilteredElementCollector(doc)
                                    .WhereElementIsNotElementType()
                                    .ToElementIds()
                                    .ToList();
                                break;

                            case ExtractionScope.ActiveView:
                                var view = uidoc.ActiveView;
                                targets = new FilteredElementCollector(doc, view.Id)
                                    .WhereElementIsNotElementType()
                                    .ToElementIds()
                                    .ToList();
                                break;

                            default: // CurrentSelection
                                if (currentSelection.Count == 0)
                                {
                                    BimPillsDialog.Warning(
                                        "Extractor de Parámetros",
                                        "No hay elementos seleccionados.",
                                        detail: "Selecciona elementos en el modelo antes de aplicar con alcance 'Selección actual'.",
                                        owner: window);
                                    return false;
                                }
                                targets = currentSelection;
                                break;
                        }

                        logger?.Info($"[Extractor] Alcance={config.Scope}, Elementos={targets.Count}, Reglas={config.Rules.Count}");
                        var result = ExtractorApplier.Apply(doc, targets, config);
                        logger?.Info($"[Extractor] Escritos={result.ParametersWritten}, Creados={result.ParametersCreated}, Errores={result.Errors.Count}");

                        ShowResultDialog(result, window);
                        return result.Errors.Count == 0;
                    }
                    catch (System.Exception ex)
                    {
                        logger?.Error("[Extractor] Error inesperado al aplicar extracción", ex);
                        BimPillsDialog.Error(
                            "Extractor de Parámetros",
                            "Ocurrió un error al aplicar la extracción.",
                            detail: ex.Message,
                            owner: window);
                        return false;
                    }
                },
                presetRepository: new JsonExtractionPresetRepository(),
                availableCategories: categories,
                paramsByCategory: paramsByCategory,
                hasCurveByCategory: hasCurveByCategory,
                familyTypesByCategory: familyTypesByCategory);

            window.ShowExtractorTab();
            window.ShowDialogOverRevit();
        }

        private static void ShowResultDialog(ExtractionResult result, System.Windows.Window? owner = null)
        {
            var summary =
                $"Elementos procesados: {result.ElementsProcessed}\n" +
                $"Parámetros escritos:  {result.ParametersWritten}\n" +
                $"Parámetros creados:   {result.ParametersCreated}";

            if (result.Errors.Count == 0)
            {
                BimPillsDialog.Info(
                    "Extractor de Parámetros",
                    "Extracción completada.",
                    detail: summary,
                    owner: owner);
            }
            else
            {
                var sample = string.Join("\n", result.Errors.Take(5));
                var more   = result.Errors.Count > 5 ? $"\n(+{result.Errors.Count - 5} más)" : "";
                BimPillsDialog.Warning(
                    "Extractor de Parámetros",
                    $"Extracción completada con {result.Errors.Count} errores.",
                    detail: summary + "\n\nPrimeros errores:\n" + sample + more,
                    owner: owner);
            }
        }
    }
}
