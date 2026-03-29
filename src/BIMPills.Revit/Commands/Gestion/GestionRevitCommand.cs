using Autodesk.Revit.DB;
using BIMPills.Commands.Gestion;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.UI.Gestion;
using System;

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

            if (doc != null)
            {
                createCallback = (name) =>
                {
                    logger?.Info($"[Gestion] Creando subproyecto (workset): '{name}'");
                    try
                    {
                        using (var tx = new Transaction(doc, "BIMPills: Crear subproyecto"))
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
                        using (var tx = new Transaction(doc, "BIMPills: Renombrar subproyecto"))
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
            }

            new GestionWindow(
                GestionCommand.LastResult,
                createCallback,
                renameCallback
            ).ShowDialog();
        }
    }
}
