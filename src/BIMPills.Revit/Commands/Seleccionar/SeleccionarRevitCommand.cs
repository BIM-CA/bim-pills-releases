using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Commands.Seleccionar;
using BIMPills.Core.Commands;
using BIMPills.Core.Gestion;
using BIMPills.Core.Seleccionar;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Persistence;
using BIMPills.Revit.Commands;
using BIMPills.UI.Seleccionar;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.Seleccionar
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class SeleccionarRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new SeleccionarCommand();

        private static List<ParamInfo> CollectEditableParamInfos(Element elem, bool isTypeElem = false)
        {
            var result = new List<ParamInfo>();
            foreach (Parameter p in elem.Parameters)
            {
                if (p.Definition?.Name == null
                    || string.IsNullOrWhiteSpace(p.Definition.Name)
                    || p.IsReadOnly) continue;
                result.Add(new ParamInfo
                {
                    Name        = p.Definition.Name,
                    Group       = TryGetParamGroup(p),
                    IsTypeParam = isTypeElem
                });
            }
            return result;
        }

        /// <summary>
        /// Recoge los valores actuales de un conjunto de parámetros en el diccionario de valores.
        /// Limita a <paramref name="maxPerParam"/> valores distintos por parámetro.
        /// </summary>
        private static void CollectParamValues(ParameterSet parameters,
            Dictionary<string, HashSet<string>> paramValues, int maxPerParam)
        {
            foreach (Parameter p in parameters)
            {
                if (p.Definition?.Name == null || string.IsNullOrWhiteSpace(p.Definition.Name)) continue;
                var key = p.Definition.Name;
                if (!paramValues.TryGetValue(key, out var set))
                    paramValues[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (set.Count >= maxPerParam) continue;
                try
                {
                    // StorageType.ElementId → AsValueString() devuelve el nombre legible (ej. nombre de fase)
                    var sv = p.StorageType switch
                    {
                        StorageType.String    => p.AsString() ?? string.Empty,
                        StorageType.Integer   => p.AsInteger().ToString(),
                        StorageType.Double    => p.AsValueString() ?? p.AsDouble().ToString("G"),
                        StorageType.ElementId => p.AsValueString() ?? string.Empty,
                        _                     => string.Empty
                    };
                    if (!string.IsNullOrWhiteSpace(sv)) set.Add(sv);
                }
                catch { /* parámetro no accesible */ }
            }
        }

        private static string TryGetParamGroup(Parameter p)
        {
#if REVIT2024
            try
            {
                if (p.Definition is not InternalDefinition inDef) return "Otros";
#pragma warning disable CS0618
                var grp   = inDef.ParameterGroup;
                var label = LabelUtils.GetLabelFor(grp);
#pragma warning restore CS0618
                return string.IsNullOrWhiteSpace(label) ? "Otros" : label;
            }
            catch { return "Otros"; }
#else
            return "Otros";
#endif
        }

        protected override void OnSuccess(IPluginCommand command)
        {
            var uiApp = CommandData!.Application;
            var doc   = uiApp.ActiveUIDocument.Document;

            // ── Presets ──────────────────────────────────────────────────────
            var presetRepo = new JsonFilterPresetRepository();

            // ── Categorías disponibles en todo el modelo ──────────────────────
            var viewId = doc.ActiveView.Id;
            var categories = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e => e.Category != null)
                .Select(e => e.Category!.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            // ── Parámetros por categoría (vista activa) ───────────────────────
            // Usado para filtrar parámetros disponibles cuando el usuario elige categorías.
            // category → paramName → ParamInfo (primer grupo encontrado gana)
            var paramsByCat = new Dictionary<string, Dictionary<string, ParamInfo>>(StringComparer.OrdinalIgnoreCase);
            var seenTypeIds = new HashSet<ElementId>();

            // Valores distintos por nombre de parámetro (máx. 50 por param).
            // Permite poblar el combo "Valor" de cada fila de parámetro.
            const int MaxDistinctValues = 50;
            var paramValues = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var elem in new FilteredElementCollector(doc, viewId)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e => e.Category != null))
            {
                var catName = elem.Category!.Name;
                if (!paramsByCat.ContainsKey(catName))
                    paramsByCat[catName] = new Dictionary<string, ParamInfo>(StringComparer.OrdinalIgnoreCase);

                var catDict = paramsByCat[catName];
                foreach (var pi in CollectEditableParamInfos(elem, isTypeElem: false))
                    if (!catDict.ContainsKey(pi.Name)) catDict[pi.Name] = pi;

                // Recoger valores de parámetros de instancia
                CollectParamValues(elem.Parameters, paramValues, MaxDistinctValues);

                var tid = elem.GetTypeId();
                if (tid != ElementId.InvalidElementId && seenTypeIds.Add(tid))
                {
                    var te = doc.GetElement(tid);
                    if (te != null)
                    {
                        foreach (var pi in CollectEditableParamInfos(te, isTypeElem: true))
                            if (!catDict.ContainsKey(pi.Name)) catDict[pi.Name] = pi;

                        // Recoger valores de parámetros de tipo (solo primera vez por tipo)
                        CollectParamValues(te.Parameters, paramValues, MaxDistinctValues);
                    }
                }
            }

            // Todos los parámetros (unión de todas las categorías), ordenados por Group+Name
            var allParamsByName = new Dictionary<string, ParamInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var catDict in paramsByCat.Values)
                foreach (var kvp in catDict)
                    if (!allParamsByName.ContainsKey(kvp.Key))
                        allParamsByName[kvp.Key] = kvp.Value;

            // Asignar los valores recogidos a cada ParamInfo
            foreach (var pi in allParamsByName.Values)
                if (paramValues.TryGetValue(pi.Name, out var vals) && vals.Count > 0)
                    pi.AllowedValues = vals.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();

            var allParamInfos = allParamsByName.Values
                .OrderBy(p => p.Group)
                .ThenBy(p => p.Name)
                .ToList<ParamInfo>();

            // Convertir a IReadOnlyList<ParamInfo> por categoría
            var paramsByCategory = paramsByCat.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<ParamInfo>)kvp.Value.Values
                    .OrderBy(p => p.Group).ThenBy(p => p.Name).ToList(),
                StringComparer.OrdinalIgnoreCase) as IReadOnlyDictionary<string, IReadOnlyList<ParamInfo>>;

            // ── Worksets disponibles ───────────────────────────────────────────
            var worksets = WorksetReader.GetUserWorksets(doc)
                .Select(w => new WorksetInfo { Id = w.Id, Name = w.Name })
                .ToList();

            // ── ExternalEventHandlers ─────────────────────────────────────────
            var applyHandler       = new SelectionApplyHandler();
            var assignHandler      = new SubprojectAssignHandler();
            var openAssignHandler  = new AssignValuesOpenHandler { Worksets = worksets };
            var ordenarHandler     = new OrdenarOpenHandler();
            var eyedropperHandler  = new EyedropperHandler();
            var rectSelectHandler  = new RectSelectHandler();
            var applyEvent         = ExternalEvent.Create(applyHandler);
            var assignEvent        = ExternalEvent.Create(assignHandler);
            var openAssignEvent    = ExternalEvent.Create(openAssignHandler);
            var refreshEvent       = ExternalEvent.Create(openAssignHandler);
            var ordenarEvent       = ExternalEvent.Create(ordenarHandler);
            var eyedropperEvent    = ExternalEvent.Create(eyedropperHandler);
            var rectSelectEvent    = ExternalEvent.Create(rectSelectHandler);

            // Conectar el handler de apertura con el de escritura
            openAssignHandler.OnAssign = request =>
            {
                assignHandler.Request = request;
                assignEvent.Raise();
            };

            // Refrescar parámetros cuando la selección cambia y el modal está abierto
            void OnSelectionChanged(object? s, Autodesk.Revit.UI.Events.SelectionChangedEventArgs e)
            {
                if (openAssignHandler.OpenModal != null)
                {
                    openAssignHandler.RefreshMode = true;
                    refreshEvent.Raise();
                }
            }
            uiApp.SelectionChanged += OnSelectionChanged;

            // ── Parámetros comunes y resumen de selección ─────────────────────
            var selection        = uiApp.ActiveUIDocument.Selection.GetElementIds();
            var selectionSummary = new List<BIMPills.Core.Seleccionar.CategoryElementSummary>();

            foreach (var id in selection)
            {
                var elem = doc.GetElement(id);
                if (elem?.Category == null) continue;

                var catName = elem.Category.Name;
                var found   = false;
                foreach (var s in selectionSummary)
                    if (s.CategoryName == catName) { s.TotalCount++; s.EditableCount++; found = true; break; }
                if (!found)
                    selectionSummary.Add(new BIMPills.Core.Seleccionar.CategoryElementSummary(catName, 1, 1));
            }

            // ── Galería ───────────────────────────────────────────────────────
            SeleccionarGalleryWindow? gallery = null;
            gallery = new SeleccionarGalleryWindow(
                categories:       categories,
                allParamInfos:    allParamInfos,
                paramsByCategory: paramsByCategory!,
                selectionSummary: selectionSummary,
                presetRepo:       presetRepo,
                raiseApply:    filter =>
                {
                    applyHandler.Filter = filter;
                    applyEvent.Raise();
                },
                raiseOpenAssign: () =>
                {
                    // Cuando el AssignValuesModal se abra, suscribirse a su Close para restaurar galería
                    openAssignHandler.OnModalOpened = modal =>
                        modal.Dispatcher.Invoke(() =>
                            modal.Closed += (_, __) =>
                            {
                                try
                                {
                                    gallery?.Dispatcher.Invoke(() =>
                                    {
                                        if (gallery.WindowState == System.Windows.WindowState.Minimized)
                                        {
                                            gallery.WindowState = System.Windows.WindowState.Normal;
                                            gallery.Activate();
                                        }
                                    });
                                }
                                catch { }
                            });
                    openAssignEvent.Raise();
                },
                raiseOrdenar:  () =>
                {
                    ordenarHandler.OwnerWindow = gallery;
                    ordenarEvent.Raise();
                },
                onApplyDone:   cb => { applyHandler.OnCompleted  = cb; },
                onAssignDone:  cb => { assignHandler.OnCompleted = cb; },
                onEyedropperReady: onData =>
                {
                    eyedropperHandler.OnData = onData;
                    eyedropperEvent.Raise();
                },
                onRectSelectReady: onData =>
                {
                    rectSelectHandler.OnData = onData;
                    rectSelectEvent.Raise();
                });

            gallery.Closed += (_, __) =>
            {
                // Unsubscribe del evento de Revit (protegido por try/catch: puede lanzar
                // fuera de API context si el cierre viene de un contexto WPF sin API activa)
                try { uiApp.SelectionChanged -= OnSelectionChanged; } catch { }

                // Cerrar AssignValuesModal si quedó abierto al cerrar la galería
                try { openAssignHandler.OpenModal?.Close(); } catch { }
            };

            gallery.ShowOverRevit();
        }
    }
}
