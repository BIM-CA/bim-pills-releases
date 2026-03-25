using Autodesk.Revit.UI;
using BIMPills.Core.Modules;
using System;
using System.Windows.Media.Imaging;

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
                // Tab already exists — safe to ignore
            }
        }

        public void EnsurePanel(string tabName, string panelName)
        {
            // CreateRibbonPanel throws if the panel already exists, so check first
            foreach (var existing in _app.GetRibbonPanels(tabName))
                if (existing.Name == panelName) return;

            _app.CreateRibbonPanel(tabName, panelName);
        }

        public void AddPushButton(
            string panelName,
            string buttonName,
            string tooltip,
            string commandTypeFullName,
            string assemblyPath,
            string? largeImagePath = null)
        {
            RibbonPanel? panel = null;
            foreach (var existing in _app.GetRibbonPanels())
                if (existing.Name == panelName) { panel = existing; break; }

            if (panel == null)
                throw new InvalidOperationException(
                    $"Panel '{panelName}' not found. Call EnsurePanel first.");

            var data = new PushButtonData(buttonName, buttonName, assemblyPath, commandTypeFullName)
            {
                ToolTip = tooltip
            };

            if (!string.IsNullOrEmpty(largeImagePath))
            {
                try
                {
                    data.LargeImage = new BitmapImage(new Uri(largeImagePath, UriKind.RelativeOrAbsolute));
                }
                catch
                {
                    // Image load failure should not block plugin startup
                }
            }

            panel.AddItem(data);
        }
    }
}
