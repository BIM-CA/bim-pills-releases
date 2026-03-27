namespace BIMPills.Core.Gestion
{
    public sealed class WorksetInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsOpen { get; set; }
        public bool IsDefault { get; set; }
        public bool IsEditable { get; set; }
        public string Owner { get; set; } = string.Empty;
        public int ElementCount { get; set; }
    }
}
