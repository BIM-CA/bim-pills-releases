using BIMPills.Commands.CustomDimensionSchemes;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.UI.CustomDimensionSchemes;

namespace BIMPills.Revit.Commands.CustomDimensionSchemes
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class CustomDimensionSchemesRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new CustomDimensionSchemesCommand(ServiceLocator.Get<IDimensionSchemeService>());

        protected override void OnSuccess(IPluginCommand command)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;

            try
            {
                var window = new CustomDimensionSchemesWindow();
                window.ShowDialog();
            }
            catch (System.Exception ex)
            {
                logger?.Error("Error al abrir CustomDimensionSchemesWindow", ex);
                throw;
            }
        }
    }
}
