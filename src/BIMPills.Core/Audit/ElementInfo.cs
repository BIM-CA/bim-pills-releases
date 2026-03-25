namespace BIMPills.Core.Audit
{
    public sealed class ElementInfo
    {
        public int Id { get; }
        public string Name { get; }
        public string? Category { get; }

        public ElementInfo(int id, string name, string? category)
        {
            Id = id;
            Name = name;
            Category = category;
        }
    }
}
