using Autodesk.Revit.DB;
using BIMPills.Commands.Gestion;
using BIMPills.Core.Commands;
using BIMPills.Core.Gestion;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.UI.Gestion;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using CoreViewDetailLevel = BIMPills.Core.Gestion.ViewDetailLevel;

namespace BIMPills.Revit.Commands.Gestion
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class GestionRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new GestionCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            if (GestionCommand.LastResult == null) return;

            var doc = CommandData?.Application.ActiveUIDocument.Document;

            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;

            Func<string, bool>? createCallback = null;
            Func<long, string, bool>? renameCallback = null;
            Func<View3DCreationConfig, View3DCreationResult>? createViewsCallback = null;

            if (doc != null)
            {
                createCallback = (name) =>
                {
                    logger?.Info($"[Gestion] Creando subproyecto (workset): '{name}'");
                    try
                    {
                        using (var tx = new Transaction(doc, "BIM Pills: Crear subproyecto"))
                        {
                            tx.Start();
                            Workset.Create(doc, name);
                            tx.Commit();
                            logger?.Info($"[Gestion] Subproyecto '{name}' creado correctamente.");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Error($"[Gestion] Error al crear subproyecto '{name}'", ex);
                        return false;
                    }
                };

                renameCallback = (worksetId, newName) =>
                {
                    logger?.Info($"[Gestion] Renombrando workset Id={worksetId} → '{newName}'");
                    try
                    {
                        using (var tx = new Transaction(doc, "BIM Pills: Renombrar subproyecto"))
                        {
                            tx.Start();
                            var wsId = new WorksetId((int)worksetId);
                            WorksetTable.RenameWorkset(doc, wsId, newName);
                            tx.Commit();
                            logger?.Info($"[Gestion] Workset Id={worksetId} renombrado a '{newName}' correctamente.");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Error($"[Gestion] Error al renombrar workset Id={worksetId}", ex);
                        return false;
                    }
                };

                createViewsCallback = (config) => CreateViews3D(doc, config, logger);
            }

            new GestionWindow(
                GestionCommand.LastResult,
                createCallback,
                renameCallback,
                createViewsCallback
            ).ShowDialogOverRevit();
        }

        // ── 3D View creation ──────────────────────────────────────────────────

        private static View3DCreationResult CreateViews3D(
            Document doc, View3DCreationConfig config, ILogger? logger)
        {
            var result = new View3DCreationResult();
            try
            {
                // Find default 3D ViewFamilyType
                var viewFamilyTypeId = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional)
                    ?.Id;

                if (viewFamilyTypeId == null)
                {
                    result.Errors.Add("No se encontró un tipo de vista 3D en el modelo.");
                    return result;
                }

                // Collect all user worksets (for visibility control)
                var allWorksets = doc.IsWorkshared
                    ? new FilteredWorksetCollector(doc)
                        .OfKind(WorksetKind.UserWorkset)
                        .ToList()
                    : new List<Workset>();

                using var tx = new Transaction(doc, "BIM Pills: Crear Vistas 3D");
                tx.Start();

                for (int i = 0; i < config.WorksetIds.Count; i++)
                {
                    var targetWsIntId = (int)config.WorksetIds[i];
                    var wsName  = config.WorksetNames[i];
                    var viewName = System.Text.RegularExpressions.Regex.Replace(
                        config.ViewNameTemplate, @"\{nombre\}", wsName,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    // Find the actual Workset object by NAME — WorksetId.IntegerValue
                    // returns 0 for all worksets in Revit 2024, so ID matching is broken.
                    var targetWorkset = allWorksets.FirstOrDefault(w => w.Name == wsName);

                    try
                    {
                        // Check for existing view
                        var existing = new FilteredElementCollector(doc)
                            .OfClass(typeof(View3D))
                            .Cast<View3D>()
                            .FirstOrDefault(v => !v.IsTemplate &&
                                string.Equals(v.Name, viewName, StringComparison.OrdinalIgnoreCase));

                        if (existing != null)
                        {
                            if (config.ConflictResolution == ViewConflictResolution.Skip)
                            {
                                result.Skipped++;
                                logger?.Info($"[Gestion] Vista '{viewName}' ya existe — omitida.");
                                continue;
                            }
                            // Overwrite: delete existing
                            doc.Delete(existing.Id);
                        }

                        // Create isometric 3D view
                        var view3D = View3D.CreateIsometric(doc, viewFamilyTypeId);
                        view3D.Name = viewName;

                        // Detail level
                        view3D.DetailLevel = config.DetailLevel switch
                        {
                            CoreViewDetailLevel.Coarse => Autodesk.Revit.DB.ViewDetailLevel.Coarse,
                            CoreViewDetailLevel.Fine   => Autodesk.Revit.DB.ViewDetailLevel.Fine,
                            _                          => Autodesk.Revit.DB.ViewDetailLevel.Medium
                        };

                        // Workset visibility: hide all, then show only target
                        if (doc.IsWorkshared && targetWorkset != null)
                        {
                            logger?.Info($"[Gestion] Vista '{viewName}': target={wsName} id={targetWorkset.Id.IntegerValue}");

                            // Hide ALL worksets
                            foreach (var ws in allWorksets)
                                view3D.SetWorksetVisibility(ws.Id, WorksetVisibility.Hidden);

                            // Show only target workset
                            view3D.SetWorksetVisibility(targetWorkset.Id, WorksetVisibility.Visible);

                            logger?.Info($"[Gestion] Verify: {targetWorkset.Name} = {view3D.GetWorksetVisibility(targetWorkset.Id)}");
                        }

                        // Discipline: Coordination
                        if (config.SetCoordinationDiscipline)
                            view3D.Discipline = ViewDiscipline.Coordination;

                        // Hide annotation categories
                        if (config.HideAnnotationCategories)
                        {
                            foreach (Category cat in doc.Settings.Categories)
                            {
                                if (cat.CategoryType == CategoryType.Annotation &&
                                    cat.get_AllowsVisibilityControl(view3D))
                                {
                                    try { view3D.SetCategoryHidden(cat.Id, true); } catch { }
                                }
                            }
                        }

                        result.Created++;
                        logger?.Info($"[Gestion] Vista 3D creada: '{viewName}'");
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        result.Errors.Add($"{wsName}: {ex.Message}");
                        logger?.Error($"[Gestion] Error creando vista '{viewName}'", ex);
                    }
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                logger?.Error("[Gestion] Error en CreateViews3D", ex);
                result.Errors.Add(ex.Message);
            }
            return result;
        }
    }
}
