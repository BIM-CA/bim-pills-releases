using System.Collections.Generic;

namespace BIMPills.Core.Models
{
    /// <summary>Summary info for a view template from a Revit document.</summary>
    public class ViewTemplateInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string ViewType { get; set; } = "";
        public int FilterCount { get; set; }
        public string SourceDocumentTitle { get; set; } = "";
        public bool IsSelected { get; set; }
    }

    /// <summary>Detailed info for a view template — used in the preview panel.</summary>
    public class ViewTemplateDetail
    {
        public string Name { get; set; } = "";
        public string ViewType { get; set; } = "";
        /// <summary>Number of views in the project that use this template.</summary>
        public int AssignedViewCount { get; set; }
        /// <summary>All template parameters with their values and include flags.</summary>
        public List<ViewTemplateParameter> Parameters { get; set; } = new List<ViewTemplateParameter>();
    }

    /// <summary>
    /// One parameter row in the view template properties table.
    /// Matches Revit's native "View Template Properties" dialog columns:
    /// Parámetro | Valor | Incluir.
    /// </summary>
    public class ViewTemplateParameter
    {
        public string Name { get; set; } = "";
        /// <summary>Display value (e.g. "1 : 100", "Alto", "Coordinación").</summary>
        public string Value { get; set; } = "";
        /// <summary>True for parameters with nested dialogs ("Editar...").</summary>
        public bool IsComplex { get; set; }
        /// <summary>Whether this parameter is checked for inclusion in the transfer.</summary>
        public bool Include { get; set; } = true;
    }

    /// <summary>Info about a view filter applied to a view template.</summary>
    public class ViewFilterInfo
    {
        public string Name { get; set; } = "";
        public bool IsVisible { get; set; } = true;
        public bool HasOverrides { get; set; }
    }

    /// <summary>Info about an open Revit document.</summary>
    public class OpenDocumentInfo
    {
        public string Title { get; set; } = "";
        public string PathName { get; set; } = "";
        public bool IsCurrent { get; set; }
    }

    /// <summary>How to handle duplicate template names during transfer.</summary>
    public enum ConflictResolution
    {
        Replace,
        Skip,
        Rename
    }

    /// <summary>Result of a view template transfer operation.</summary>
    public class TransferResult
    {
        public int Transferred { get; set; }
        public int Skipped { get; set; }
        public int Conflicts { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
