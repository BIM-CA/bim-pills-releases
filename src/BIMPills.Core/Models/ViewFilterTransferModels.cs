using System.Collections.Generic;

namespace BIMPills.Core.Models
{
    /// <summary>A view filter available to transfer between documents.</summary>
    public class TransferableFilterInfo
    {
        public long   Id            { get; set; }
        public string Name          { get; set; } = "";
        public string FilterType    { get; set; } = "";  // "Parámetro" | "Selección"
        public int    CategoryCount { get; set; }
        public int    RuleCount     { get; set; }
        public bool   IsSelected    { get; set; }
    }

    /// <summary>Detailed information about a single filter for the preview panel.</summary>
    public class FilterDetail
    {
        public string       Name        { get; set; } = "";
        public string       FilterType  { get; set; } = "";
        public List<string> Categories  { get; set; } = new List<string>();
        public List<string> Rules       { get; set; } = new List<string>();
    }
}
