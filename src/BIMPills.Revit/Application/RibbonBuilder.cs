using Autodesk.Revit.UI;
using BIMPills.Core.Modules;
using BIMPills.Revit.Resources;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BIMPills.Revit.Application
{
    internal sealed class RibbonBuilder : IRibbonBuilder
    {
        private readonly UIControlledApplication _app;

        private static readonly Dictionary<string, Func<BitmapSource>> _iconFactories
            = new Dictionary<string, Func<BitmapSource>>
            {
                ["audit"]         = RibbonIconFactory.CreateAuditIcon,
                ["about"]         = RibbonIconFactory.CreateAboutIcon,
                ["export"]        = RibbonIconFactory.CreateExportIcon,
                ["documentacion"] = RibbonIconFactory.CreateDocumentacionIcon,
                ["gestion"]       = RibbonIconFactory.CreateGestionIcon,
                ["dimension"]     = RibbonIconFactory.CreateDimensionIcon,
                ["connect"]       = RibbonIconFactory.CreateConnectIcon,
                ["ordering"]      = RibbonIconFactory.CreateOrderingIcon,
                ["datamanager"]   = RibbonIconFactory.CreateDataManagerIcon,
            };

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

            // Load icon from factory
            if (!string.IsNullOrEmpty(iconKey) && _iconFactories.TryGetValue(iconKey, out var factory))
            {
                try
                {
                    data.LargeImage = factory();
                }
                catch
                {
                    // Icon failure should not block plugin startup
                }
            }

            panel.AddItem(data);
        }
    }
}
