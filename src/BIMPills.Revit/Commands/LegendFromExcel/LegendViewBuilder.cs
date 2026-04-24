using Autodesk.Revit.DB;
using System.Linq;

namespace BIMPills.Revit.Commands.LegendFromExcel
{
    internal static class LegendViewBuilder
    {
        /// <summary>
        /// Devuelve una vista Dibujo con el nombre dado, creándola si no existe.
        /// Usa ViewDrafting (vista de dibujo) que admite DetailLine, TextNote y
        /// FilledRegion sin requerir la API de Legend (no disponible en Revit 2024).
        /// Debe llamarse dentro de una Transaction activa.
        /// </summary>
        public static View CreateOrGet(Document doc, string viewName)
        {
            // Reusar vista existente con ese nombre (Drafting o Legend)
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .FirstOrDefault(v => v.Name == viewName);

            if (existing != null) return existing;

            var draftingFamilyType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting);

            if (draftingFamilyType == null)
                throw new System.InvalidOperationException(
                    "No se encontró un tipo de vista de Dibujo en el modelo.");

            var newView = ViewDrafting.Create(doc, draftingFamilyType.Id);

            try { newView.Name = viewName; } catch { }

            return newView;
        }
    }
}
