using Autodesk.Revit.DB;
using BIMPills.Core.LegendFromExcel;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.LegendFromExcel
{
    internal static class RevitProjectStylesService
    {
        public static IReadOnlyList<RevitStyleInfo> GetTextStyles(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .Select(t => new RevitStyleInfo { Id = GetId(t.Id), Name = t.Name })
                .OrderBy(s => s.Name)
                .ToList();
        }

        public static IReadOnlyList<RevitStyleInfo> GetLineStyles(Document doc)
        {
            var result = new List<RevitStyleInfo>();
            try
            {
                var linesCategory = doc.Settings.Categories
                    .get_Item(BuiltInCategory.OST_Lines);
                if (linesCategory?.SubCategories == null) return result;

                foreach (Category sub in linesCategory.SubCategories)
                    result.Add(new RevitStyleInfo { Id = GetId(sub.Id), Name = sub.Name });
            }
            catch { }
            return result.OrderBy(s => s.Name).ToList();
        }

        public static IReadOnlyList<RevitStyleInfo> GetFillRegionTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .Select(t => new RevitStyleInfo { Id = GetId(t.Id), Name = t.Name })
                .OrderBy(s => s.Name)
                .ToList();
        }

        private static long GetId(ElementId id)
        {
#if REVIT2024
#pragma warning disable CS0618
            return (long)id.IntegerValue;
#pragma warning restore CS0618
#else
            return id.Value;
#endif
        }
    }
}
