namespace BIMPills.Core.Audit
{
    public sealed class ModelWarningInfo
    {
        public string Description { get; }
        public string Severity { get; }
        public int ElementCount { get; }

        public ModelWarningInfo(string description, string severity, int elementCount)
        {
            Description = description;
            Severity = severity;
            ElementCount = elementCount;
        }
    }
}
