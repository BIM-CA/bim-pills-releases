using System.Collections.Generic;

namespace BIMPills.Core.Models
{
    public enum ExportFormat { Pdf, Dwg }

    /// <summary>Single export operation in the non-blocking queue.</summary>
    public sealed class ExportQueueItem
    {
        public long ViewId { get; set; }
        public string Folder { get; set; } = "";
        public string FileName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public ExportFormat Format { get; set; }
        public PdfExportSettings? PdfSettings { get; set; }
        public DwgExportConfig? DwgConfig { get; set; }
    }

    /// <summary>Type of exportable item (sheet or specific view type).</summary>
    public enum ExportableItemType
    {
        Sheet,
        FloorPlan,
        CeilingPlan,
        Elevation,
        Section,
        ThreeDView,
        Legend,
        DraftingView,
        AreaPlan,
        Other
    }

    /// <summary>
    /// Unified model for an exportable item — either a sheet (ViewSheet) or an individual view.
    /// Replaces SheetExportInfo for the "Planos y Vistas" panel.
    /// </summary>
    public sealed class ExportableViewInfo
    {
        public long Id { get; }
        public string UniqueId { get; }
        public string Name { get; }
        public ExportableItemType ItemType { get; }
        /// <summary>Sheet number — only populated for sheets.</summary>
        public string SheetNumber { get; }
        public string Revision { get; }
        public string Discipline { get; }
        public bool IsSelected { get; set; }

        /// <summary>Revit parameter values (name → value) for naming tokens.</summary>
        public Dictionary<string, string> ParameterValues { get; }

        public ExportableViewInfo(long id, string uniqueId, string name,
                                  ExportableItemType itemType,
                                  string sheetNumber = "", string revision = "",
                                  string discipline = "",
                                  Dictionary<string, string>? parameterValues = null)
        {
            Id = id;
            UniqueId = uniqueId ?? "";
            Name = name;
            ItemType = itemType;
            SheetNumber = sheetNumber ?? "";
            Revision = revision ?? "";
            Discipline = discipline ?? "";
            IsSelected = true;
            ParameterValues = parameterValues ?? new Dictionary<string, string>();
        }

        /// <summary>Display name: sheet number + name for sheets, just name for views.</summary>
        public string DisplayName => ItemType == ExportableItemType.Sheet && !string.IsNullOrEmpty(SheetNumber)
            ? $"{SheetNumber} - {Name}"
            : Name;

        /// <summary>Localized type label for UI display.</summary>
        public string TypeLabel => ItemType switch
        {
            ExportableItemType.Sheet => "Plano",
            ExportableItemType.FloorPlan => "Plano de planta",
            ExportableItemType.CeilingPlan => "Plano de techo",
            ExportableItemType.Elevation => "Alzado",
            ExportableItemType.Section => "Sección",
            ExportableItemType.ThreeDView => "Vista 3D",
            ExportableItemType.Legend => "Leyenda",
            ExportableItemType.DraftingView => "Vista de diseño",
            ExportableItemType.AreaPlan => "Plano de área",
            _ => "Vista"
        };
    }
}
