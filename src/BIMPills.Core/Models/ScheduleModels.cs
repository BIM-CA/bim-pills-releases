using System.Collections.Generic;

namespace BIMPills.Core.Models
{
    /// <summary>Summary info for a Revit ViewSchedule.</summary>
    public class ScheduleInfo
    {
        public long   Id           { get; set; }
        public string Name         { get; set; }
        public string CategoryName { get; set; }
        public int    RowCount     { get; set; }
        public int    ColumnCount  { get; set; }
        /// <summary>UI selection state for multi-export.</summary>
        public bool   IsSelected   { get; set; }
    }

    /// <summary>Info about a single field/column in a schedule.</summary>
    public class ScheduleColumnInfo
    {
        public string Name          { get; set; }
        public string ParameterName { get; set; }
        public bool   IsReadOnly    { get; set; }
        public string StorageType   { get; set; }  // "String", "Integer", "Double"
    }

    /// <summary>Full data snapshot of a schedule — schedule info + columns + rows.</summary>
    public class ScheduleData
    {
        public ScheduleInfo             Schedule   { get; set; }
        public List<ScheduleColumnInfo> Columns    { get; set; } = new List<ScheduleColumnInfo>();
        /// <summary>Element IDs — parallel array to Rows.</summary>
        public List<long>               ElementIds { get; set; } = new List<long>();
        /// <summary>Row data: [rowIndex][columnIndex].</summary>
        public List<List<string>>       Rows       { get; set; } = new List<List<string>>();
    }

    /// <summary>Request to set a parameter value on a specific element.</summary>
    public class ParameterUpdateRequest
    {
        public long   ElementId     { get; set; }
        public string ParameterName { get; set; }
        public string NewValue      { get; set; }
    }

    /// <summary>Result of a batch parameter update operation.</summary>
    public class ParameterUpdateResult
    {
        public int          Updated { get; set; }
        public int          Skipped { get; set; }
        public List<string> Errors  { get; set; } = new List<string>();
    }
}
