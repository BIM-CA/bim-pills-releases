namespace BIMPills.Core.Audit
{
    public sealed class ViewInfo
    {
        public string Name { get; }
        public string ViewType { get; }
        public bool IsOnSheet { get; }

        public ViewInfo(string name, string viewType, bool isOnSheet)
        {
            Name = name;
            ViewType = viewType;
            IsOnSheet = isOnSheet;
        }
    }
}
