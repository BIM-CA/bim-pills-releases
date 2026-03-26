namespace BIMPills.Core.Modules
{
    public interface IPluginModule
    {
        string TabName { get; }
        string PanelName { get; }

        void BuildRibbon(IRibbonBuilder builder);
    }

    public interface IRibbonBuilder
    {
        void EnsureTab(string tabName);
        void EnsurePanel(string tabName, string panelName);

        void AddPushButton(
            string tabName,
            string panelName,
            string buttonName,
            string tooltip,
            string commandTypeFullName,
            string assemblyPath,
            string? iconKey = null);
    }
}
