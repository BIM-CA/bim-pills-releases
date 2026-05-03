using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Gestion;
using BIMPills.Core.Seleccionar;
using BIMPills.UI.Seleccionar;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.Seleccionar
{
    /// <summary>
    /// Lee la selección activa en el momento del click, calcula parámetros comunes
    /// (con su grupo de Revit), y abre AssignValuesModal con datos frescos.
    /// Ejecutado en el hilo de Revit via ExternalEvent — la ventana WPF
    /// se crea directamente aquí, sin Dispatcher.Invoke.
    /// </summary>
    public sealed class AssignValuesOpenHandler : IExternalEventHandler
    {
        /// <summary>Worksets disponibles en el documento.</summary>
        public IReadOnlyList<WorksetInfo> Worksets { get; set; } = Array.Empty<WorksetInfo>();

        /// <summary>Se llama cuando el usuario presiona Aplicar en el modal.</summary>
        public Action<SubprojectAssignRequest>? OnAssign { get; set; }

        /// <summary>Modal actualmente abierto. Null cuando está cerrado.</summary>
        public AssignValuesModal? OpenModal { get; private set; }

        /// <summary>Callback invocado justo después de abrir el modal.</summary>
        public Action<AssignValuesModal>? OnModalOpened { get; set; }

        /// <summary>
        /// Cuando true, Execute() re-lee selección y parámetros y llama
        /// OpenModal.UpdateParams en lugar de abrir un nuevo modal.
        /// </summary>
        public bool RefreshMode { get; set; } = false;

        public void Execute(UIApplication app)
        {
            var uiDoc = app.ActiveUIDocument;
            var doc   = uiDoc.Document;

            // ── Re-leer worksets (recoge cambios hechos tras abrir la app) ──
            var currentWorksets = WorksetReader.GetUserWorksets(doc)
                .Select(w => new WorksetInfo { Id = w.Id, Name = w.Name })
                .ToList();
            Worksets = currentWorksets;

            // ── Leer selección actual ────────────────────────────────────
            var (compatibleParams, selectionSummary) = ReadSelectionData(uiDoc, doc);

            if (RefreshMode)
            {
                RefreshMode = false;
                var target = OpenModal;
                if (target != null)
                    target.Dispatcher.Invoke(() =>
                        target.UpdateParams(compatibleParams, selectionSummary, currentWorksets));
                return;
            }

            // ── Abrir modal directamente (ExternalEvent.Execute corre en el hilo UI de Revit) ──
            var modal = new AssignValuesModal(Worksets, compatibleParams, selectionSummary);
            modal.OnApply += request => OnAssign?.Invoke(request);
            modal.Closed  += (_, __) => OpenModal = null;
            OpenModal = modal;
            OnModalOpened?.Invoke(modal);
            OnModalOpened = null;
            modal.ShowOverRevit();
        }

        /// <summary>
        /// Lee la selección activa y calcula parámetros compatibles + resumen.
        /// </summary>
        private static (IReadOnlyList<ParamInfo> compatibleParams,
                        IReadOnlyList<CategoryElementSummary> selectionSummary)
            ReadSelectionData(Autodesk.Revit.UI.UIDocument uiDoc, Document doc)
        {
            var selection        = uiDoc.Selection.GetElementIds();
            var selectionSummary = new List<CategoryElementSummary>();
            var paramInfoByName  = new Dictionary<string, ParamInfo>(StringComparer.OrdinalIgnoreCase);
            HashSet<string>? sharedNames = null;

            // Recopilar fases disponibles una sola vez
            var phaseNames = new FilteredElementCollector(doc)
                .OfClass(typeof(Phase))
                .Cast<Element>()
                .Select(e => e.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n)
                .ToList();

            foreach (var id in selection)
            {
                var elem = doc.GetElement(id);
                if (elem?.Category == null) continue;

                // Resumen por categoría
                var catName = elem.Category.Name;
                var found   = false;
                foreach (var s in selectionSummary)
                    if (s.CategoryName == catName) { s.TotalCount++; s.EditableCount++; found = true; break; }
                if (!found)
                    selectionSummary.Add(new CategoryElementSummary(catName, 1, 1));

                // Parámetros editables (instancia + tipo) con su grupo y valores permitidos
                var infos = CollectEditableParamInfos(elem, doc, phaseNames, isTypeElem: false);
                var typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    var typeElem = doc.GetElement(typeId);
                    if (typeElem != null)
                        foreach (var pi in CollectEditableParamInfos(typeElem, doc, phaseNames, isTypeElem: true))
                            infos.Add(pi);
                }

                // Registrar grupos y valores (primer hallazgo para cada nombre)
                foreach (var pi in infos)
                    if (!paramInfoByName.ContainsKey(pi.Name))
                        paramInfoByName[pi.Name] = pi;

                // Intersección de nombres
                var namesSet = new HashSet<string>(infos.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                if (sharedNames == null)
                    sharedNames = namesSet;
                else
                    sharedNames.IntersectWith(namesSet);
            }

            IReadOnlyList<ParamInfo> compatibleParams;
            if (sharedNames != null && sharedNames.Count > 0)
            {
                compatibleParams = sharedNames
                    .Select(n => paramInfoByName.TryGetValue(n, out var pi) ? pi : new ParamInfo { Name = n })
                    .OrderBy(p => p.Group)
                    .ThenBy(p => p.Name)
                    .ToList();
            }
            else
            {
                compatibleParams = Array.Empty<ParamInfo>();
            }

            return (compatibleParams, selectionSummary);
        }

        public string GetName() => "BIMPills: AssignValuesOpenHandler";

        // ── Helpers ──────────────────────────────────────────────────────

        private static List<ParamInfo> CollectEditableParamInfos(
            Element elem, Document doc, IReadOnlyList<string> phaseNames, bool isTypeElem = false)
        {
            var result = new List<ParamInfo>();
            foreach (Parameter p in elem.Parameters)
            {
                if (p.Definition?.Name == null
                    || string.IsNullOrWhiteSpace(p.Definition.Name)
                    || p.IsReadOnly) continue;

                // Filtrar parámetros de identidad de familia/tipo — no son asignables via string
                try
                {
                    if (p.Definition is InternalDefinition inDef)
                    {
#pragma warning disable CS0618
                        var bip = inDef.BuiltInParameter;
#pragma warning restore CS0618
                        if (bip == BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM
                            || bip == BuiltInParameter.ELEM_FAMILY_PARAM
                            || bip == BuiltInParameter.ELEM_TYPE_PARAM)
                            continue;
                    }
                }
                catch { }

                var pi = new ParamInfo
                {
                    Name        = p.Definition.Name,
                    Group       = TryGetParamGroup(p),
                    IsTypeParam = isTypeElem
                };

                // Detectar parámetros de fase para mostrar dropdown
                if (phaseNames.Count > 0 && p.StorageType == StorageType.ElementId)
                {
                    bool isPhaseParam = false;

                    // Estrategia 1: verificar tipo del elemento referenciado
                    var refId = p.AsElementId();
                    if (refId != null && refId != ElementId.InvalidElementId)
                    {
                        try { isPhaseParam = doc.GetElement(refId) is Phase; } catch { }
                    }

                    // Estrategia 2: detectar por BuiltInParameter (cubre params sin valor)
                    if (!isPhaseParam)
                    {
                        try
                        {
                            if (p.Definition is InternalDefinition inDef2)
                            {
#pragma warning disable CS0618
                                var bip2 = inDef2.BuiltInParameter;
#pragma warning restore CS0618
                                isPhaseParam = bip2 == BuiltInParameter.PHASE_CREATED
                                           || bip2 == BuiltInParameter.PHASE_DEMOLISHED;
                            }
                        }
                        catch { }
                    }

                    if (isPhaseParam)
                        pi.AllowedValues = phaseNames;
                }

                result.Add(pi);
            }
            return result;
        }

        private static string TryGetParamGroup(Parameter p)
        {
#if REVIT2024
            // Revit 2024: ParameterGroup property still available on InternalDefinition
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
            // Revit 2025+: GetGroupTypeId() API changed; group labels not directly accessible
            // without deprecated ParameterGroup property — fall back to flat list.
            return "Otros";
#endif
        }
    }
}
