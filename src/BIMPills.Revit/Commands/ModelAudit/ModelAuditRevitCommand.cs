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

            System.Action<IReadOnlyList<long>>? purgeCallback = null;
            if (doc != null)
            {
                purgeCallback = ids =>
                {
                    logger?.Info($"[ModelAudit] Iniciando purga de {ids.Count} elementos...");
                    int purged = 0;
                    int failed = 0;
                    using (var trans = new Transaction(doc, "BIMPills - Purgar elementos"))
                    {
                        trans.Start();
                        foreach (var id in ids)
                        {
                            try
                            {
                                var elementId = new ElementId(id);
                                var elem = doc.GetElement(elementId);
                                if (elem == null) continue;

                                // Revit 2025+ bug: pinned elements throw on Delete
                                if (elem.Pinned)
                                    elem.Pinned = false;

                                doc.Delete(elementId);
                                purged++;
                            }
                            catch (Exception ex)
                            {
                                logger?.Warning($"[ModelAudit] No se pudo eliminar elemento Id={id}: {ex.Message}");
                                failed++;
                            }
                        }
                        trans.Commit();
                    }
                    logger?.Info($"[ModelAudit] Purga completada: {purged} eliminados, {failed} omitidos.");
                };
            }

            new ModelAuditWindow(ModelAuditCommand.LastResult, purgeCallback).ShowDialogOverRevit();
        }
    }
}
