using Autodesk.Revit.DB;
using BIMPills.Commands.Gestion;
using BIMPills.Core.Commands;
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

            Func<string, bool>? createCallback = null;
            Func<long, string, bool>? renameCallback = null;

            if (doc != null)
            {
                createCallback = (name) =>
                {
                    try
                    {
                        using (var tx = new Transaction(doc, "BIMPills: Crear subproyecto"))
                        {
                            tx.Start();
                            Workset.Create(doc, name);
                            tx.Commit();
                            return true;
                        }
                    }
                    catch { return false; }
                };

                renameCallback = (worksetId, newName) =>
                {
                    try
                    {
                        using (var tx = new Transaction(doc, "BIMPills: Renombrar subproyecto"))
                        {
                            tx.Start();
                            var wsId = new WorksetId((int)worksetId);
                            WorksetTable.RenameWorkset(doc, wsId, newName);
                            tx.Commit();
                            return true;
                        }
                    }
                    catch { return false; }
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
