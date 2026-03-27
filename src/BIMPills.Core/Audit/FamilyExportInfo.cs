namespace BIMPills.Core.Audit
{
    public sealed class FamilyExportInfo
    {
        public long Id { get; }
        public string Name { get; }
        public string Category { get; }
        public bool IsSelected { get; set; }

        public FamilyExportInfo(long id, string name, string category)
        {
            Id = id;
            Name = name;
            Category = category;
            IsSelected = true; // selected by default
        }
    }
}
