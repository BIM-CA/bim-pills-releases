using Autodesk.Revit.DB;
using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Commands;
using BIMPills.Revit.Commands;
using BIMPills.UI.ModelAudit;
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

            System.Action<IReadOnlyList<long>>? purgeCallback = null;
            if (doc != null)
            {
                purgeCallback = ids =>
                {
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
                            }
                            catch { /* Elemento no eliminable — continuar */ }
                        }
                        trans.Commit();
                    }
                };
            }

            new ModelAuditWindow(ModelAuditCommand.LastResult, purgeCallback).ShowDialog();
        }
    }
}
