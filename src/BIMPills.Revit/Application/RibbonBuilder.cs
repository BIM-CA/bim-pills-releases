using Autodesk.Revit.UI;
using BIMPills.Core.Modules;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Resources;
using System;

namespace BIMPills.Revit.Application
{
    internal sealed class RibbonBuilder : IRibbonBuilder
    {
        private readonly UIControlledApplication _app;

        public RibbonBuilder(UIControlledApplication app)
        {
            _app = app;
        }

        public void EnsureTab(string tabName)
        {
            try { _app.CreateRibbonTab(tabName); }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab already exists
            }
        }

        public void EnsurePanel(string tabName, string panelName)
        {
            foreach (var existing in _app.GetRibbonPanels(tabName))
                if (existing.Name == panelName) return;

            _app.CreateRibbonPanel(tabName, panelName);
        }

        public void AddPushButton(
            string tabName,
            string panelName,
            string buttonName,
            string tooltip,
            string commandTypeFullName,
            string assemblyPath,
            string? iconKey = null)
        {
            RibbonPanel? panel = null;
            foreach (var existing in _app.GetRibbonPanels(tabName))
                if (existing.Name == panelName) { panel = existing; break; }

            if (panel == null)
                throw new InvalidOperationException(
                    $"Panel '{panelName}' no encontrado. Llama a EnsurePanel primero.");

            var data = new PushButtonData(buttonName, buttonName, assemblyPath, commandTypeFullName)
            {
                ToolTip = tooltip
            };

            if (!string.IsNullOrEmpty(iconKey))
            {
                try
                {
                    data.LargeImage = BimPillsIcons.Large(iconKey);
                    data.Image      = BimPillsIcons.Small(iconKey);
                }
                catch (Exception ex)
                {
                    if (ServiceLocator.IsRegistered<ILogger>())
                        ServiceLocator.Get<ILogger>().Error(
                            $"[Ribbon] Falló ícono '{iconKey}' (IconRoot='{BimPillsIcons.IconRoot}')", ex);
                }
            }

            panel.AddItem(data);
        }
    }
}
