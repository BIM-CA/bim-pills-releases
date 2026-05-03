using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.Seleccionar
{
    /// <summary>
    /// Lee las categorías de los elementos actualmente seleccionados en Revit.
    /// Usado por el botón □ (selección activa) de FindSelectModal.
    /// </summary>
    public sealed class RectSelectHandler : IExternalEventHandler
    {
        /// <summary>Llamado con la lista de categorías únicas. Dispatched al hilo UI.</summary>
        public Action<IReadOnlyList<string>>? OnData { get; set; }

        public void Execute(UIApplication app)
        {
            var onData = OnData;
            OnData = null;

            var uiDoc = app.ActiveUIDocument;
            var doc   = uiDoc.Document;

            // Lee la selección activa en Revit — sin interacción adicional.
            // El usuario selecciona elementos normalmente en Revit y luego pulsa ⬚.
            var ids = uiDoc.Selection.GetElementIds();
            var categories = ids
                .Select(id => doc.GetElement(id))
                .Where(e => e?.Category != null)
                .Select(e => e!.Category!.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();

            onData?.Invoke(categories);
        }

        public string GetName() => "BIMPills: RectSelectHandler";
    }
}
