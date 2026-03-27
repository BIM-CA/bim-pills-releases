namespace BIMPills.Core.Audit
{
    public sealed class FamilyInfo
    {
        public string Name { get; }
        public string Category { get; }
        public int InstanceCount { get; }
        public long SizeBytes { get; }

        public FamilyInfo(string name, string category, int instanceCount, long sizeBytes)
        {
            Name = name;
            Category = category;
            InstanceCount = instanceCount;
            SizeBytes = sizeBytes;
        }

        public double SizeMB => SizeBytes / 1_048_576.0;

        public string SizeLabel =>
            SizeBytes >= 1_048_576 ? $"{SizeMB:F1} MB" :
            SizeBytes >= 1_024    ? $"{SizeBytes / 1_024.0:F1} KB" :
                                    $"{SizeBytes} B";
    }
}
