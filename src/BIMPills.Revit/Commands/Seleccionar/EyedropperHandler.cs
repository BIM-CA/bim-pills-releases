using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Seleccionar;
using System;
using System.Linq;

namespace BIMPills.Revit.Commands.Seleccionar
{
    /// <summary>
    /// Lee el primer elemento seleccionado y devuelve su categoría + valores de parámetros.
    /// Usado por el botón 🔬 (cuentagotas) de FindSelectModal.
    /// </summary>
    public sealed class EyedropperHandler : IExternalEventHandler
    {
        /// <summary>Llamado con los datos del elemento tras la lectura. Dispatched al hilo UI.</summary>
        public Action<EyedropperData>? OnData { get; set; }

        public void Execute(UIApplication app)
        {
            var onData = OnData;
            OnData = null; // consumir callback de forma segura

            var uiDoc = app.ActiveUIDocument;
            var doc   = uiDoc.Document;

            // Solicitar al usuario que haga clic en un elemento
            Autodesk.Revit.DB.Reference? pickedRef = null;
            try
            {
                pickedRef = uiDoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    "Cuentagotas: haz clic en el elemento de referencia");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                onData?.Invoke(new EyedropperData()); // usuario canceló con ESC
                return;
            }

            var elem = doc.GetElement(pickedRef);
            if (elem?.Category == null)
            {
                onData?.Invoke(new EyedropperData());
                return;
            }

            var data = new EyedropperData { CategoryName = elem.Category.Name };

            foreach (Parameter p in elem.Parameters)
            {
                if (p.Definition?.Name == null) continue;
                try
                {
                    var value = p.StorageType switch
                    {
                        StorageType.String    => p.AsString() ?? string.Empty,
                        StorageType.Integer   => p.AsInteger().ToString(),
                        StorageType.Double    => p.AsValueString() ?? p.AsDouble().ToString("G"),
                        StorageType.ElementId => p.AsValueString() ?? string.Empty,
                        _                    => string.Empty
                    };
                    data.ParamValues[p.Definition.Name] = value;
                }
                catch { /* parámetro no accesible */ }
            }

            // Incluir también parámetros de tipo
            var typeId = elem.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var typeElem = doc.GetElement(typeId);
                if (typeElem != null)
                    foreach (Parameter p in typeElem.Parameters)
                    {
                        if (p.Definition?.Name == null || data.ParamValues.ContainsKey(p.Definition.Name)) continue;
                        try
                        {
                            var value = p.StorageType switch
                            {
                                StorageType.String    => p.AsString() ?? string.Empty,
                                StorageType.Integer   => p.AsInteger().ToString(),
                                StorageType.Double    => p.AsValueString() ?? p.AsDouble().ToString("G"),
                                StorageType.ElementId => p.AsValueString() ?? string.Empty,
                                _                    => string.Empty
                            };
                            data.ParamValues[p.Definition.Name] = value;
                        }
                        catch { }
                    }
            }

            onData?.Invoke(data);
        }

        public string GetName() => "BIMPills: EyedropperHandler";
    }
}
