using Autodesk.Revit.DB;
using BIMPills.Commands.ExportFamilies;
using BIMPills.Core.Commands;
using BIMPills.Revit.Commands;
using BIMPills.UI.ExportFamilies;
using System;

namespace BIMPills.Revit.Commands.ExportFamilies
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class ExportFamiliesRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new ExportFamiliesCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            if (ExportFamiliesCommand.LastResult == null) return;

            var doc = CommandData?.Application.ActiveUIDocument.Document;

            Func<long, string, bool>? exportCallback = null;
            if (doc != null)
            {
                exportCallback = (familyId, destinationPath) =>
                {
                    try
                    {
                        var elementId = new ElementId(familyId);
                        var family = doc.GetElement(elementId) as Family;
                        if (family == null) return false;

                        var familyDoc = doc.EditFamily(family);
                        if (familyDoc == null) return false;

                        try
                        {
                            // Ensure directory exists
                            var dir = System.IO.Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(dir))
                                System.IO.Directory.CreateDirectory(dir);

                            var saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                            familyDoc.SaveAs(destinationPath, saveOptions);
                            return true;
                        }
                        finally
                        {
                            familyDoc.Close(false); // Close without saving changes to original
                        }
                    }
                    catch
                    {
                        return false;
                    }
                };
            }

            var result = ExportFamiliesCommand.LastResult;
            int revitVersion = doc?.Application.VersionNumber != null
                ? int.TryParse(doc.Application.VersionNumber, out var v) ? v : 0
                : 0;

            new ExportFamiliesWindow(
                result.Families,
                exportCallback,
                result.DocumentTitle,
                revitVersion).ShowDialog();
        }
    }
}
