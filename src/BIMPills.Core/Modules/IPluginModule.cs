namespace BIMPills.Core.Modules
{
    /// <summary>
    /// A self-contained feature unit. Each module registers its services
    /// and describes what ribbon buttons to create.
    /// </summary>
    public interface IPluginModule
    {
        string TabName { get; }
        string PanelName { get; }

        void BuildRibbon(IRibbonBuilder builder);
    }

    /// <summary>
    /// Minimal abstraction over Revit's UIControlledApplication ribbon API.
    /// </summary>
    public interface IRibbonBuilder
    {
        void EnsureTab(string tabName);
        void EnsurePanel(string tabName, string panelName);

        void AddPushButton(
            string panelName,
            string buttonName,
            string tooltip,
            string commandTypeFullName,
            string assemblyPath,
            string? largeImagePath = null);
    }
}
