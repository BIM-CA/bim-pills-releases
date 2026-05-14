using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Seleccionar;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BIMPills.Revit.Commands.Seleccionar
{
    /// <summary>
    /// Asigna un workset y/o parámetros a los elementos indicados.
    /// Se ejecuta en el hilo de Revit vía ExternalEvent.
    /// </summary>
    public sealed class SubprojectAssignHandler : IExternalEventHandler
    {
        public SubprojectAssignRequest? Request { get; set; }
        public Action<SubprojectAssignResult>? OnCompleted { get; set; }

        // ── Logger de diagnóstico ─────────────────────────────────────────────
        // Escribe a %LOCALAPPDATA%\BIMPills\assign_diag_YYYYMMDD.log
        private static string DiagLogPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIMPills",
                $"assign_diag_{DateTime.Now:yyyyMMdd}.log");

        private static void DiagLog(StringBuilder sb, string msg)
        {
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
        }

        private static void FlushDiag(StringBuilder sb)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DiagLogPath)!);
                File.AppendAllText(DiagLogPath, sb.ToString(), Encoding.UTF8);
            }
            catch { /* no bloquear la ejecución por fallos de log */ }
        }

        // ── Execute ───────────────────────────────────────────────────────────

        public void Execute(UIApplication app)
        {
            if (Request == null) return;

            var diag   = new StringBuilder();
            var uiDoc  = app.ActiveUIDocument;
            var doc    = uiDoc.Document;
            var result = new SubprojectAssignResult();

            var elementIds = Request.UseCurrentSelection
                ? uiDoc.Selection.GetElementIds().Select(id =>
#if REVIT2024
#pragma warning disable CS0618
                    (long)id.IntegerValue
#pragma warning restore CS0618
#else
                    id.Value
#endif
                  ).ToList()
                : Request.ElementIds;

            DiagLog(diag, $"=== SubprojectAssignHandler.Execute ===");
            DiagLog(diag, $"  ElementIds: {elementIds.Count}  |  Assignments: {Request.ParameterAssignments.Count}  |  AssignWorkset: {Request.AssignWorkset}");
            foreach (var a in Request.ParameterAssignments)
                DiagLog(diag, $"  Assignment → Param='{a.ParameterName}'  Value='{a.NewValue}'");

            if (elementIds.Count == 0)
            {
                DiagLog(diag, "  → No elements, early return.");
                FlushDiag(diag);
                OnCompleted?.Invoke(result);
                return;
            }

            // Separate instance vs type assignments up-front
            var instanceAssignments = Request.ParameterAssignments.Where(a => !a.IsTypeParam).ToList();
            var typeAssignments     = Request.ParameterAssignments.Where(a =>  a.IsTypeParam).ToList();

            try
            {
                using var tx = new Transaction(doc, "BIM Pills: Asignar subproyecto");
                tx.Start();

                // ── Type parameters: apply once per unique type element ────────
                if (typeAssignments.Count > 0)
                {
                    var seenTypeIds = new HashSet<ElementId>();
                    foreach (var id in elementIds)
                    {
                        var elemId  = new ElementId(id);
                        var element = doc.GetElement(elemId);
                        if (element == null) continue;

                        var typeId = element.GetTypeId();
                        if (typeId == ElementId.InvalidElementId || !seenTypeIds.Add(typeId)) continue;

                        var typeElem = doc.GetElement(typeId);
                        if (typeElem == null) continue;

                        DiagLog(diag, $"  TypeElem {typeId} for instance {id}:");
                        foreach (var assignment in typeAssignments)
                        {
                            var ok = ApplyParamAssignment(typeElem, assignment, doc, diag);
                            if (ok) result.ElementsAssigned++;
                        }
                    }
                }

                // ── Instance parameters + workset ──────────────────────────────
                foreach (var id in elementIds)
                {
                    try
                    {
                        var elemId  = new ElementId(id);
                        var element = doc.GetElement(elemId);
                        if (element == null) { DiagLog(diag, $"  Elem {id}: NULL — skip"); continue; }

                        var catName = element.Category?.Name ?? "?";
                        DiagLog(diag, $"  Elem {id} ({catName}):");

                        var changed = false;

                        // ── Workset ────────────────────────────────────────────────────
                        if (Request.AssignWorkset)
                        {
                            var worksetParam = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                            if (worksetParam == null)
                            {
                                DiagLog(diag, $"    Workset: param NULL");
                            }
                            else if (worksetParam.IsReadOnly)
                            {
                                DiagLog(diag, $"    Workset: IsReadOnly=true — skip");
                            }
                            else
                            {
                                var ok = worksetParam.Set((int)Request.WorksetId);
                                DiagLog(diag, $"    Workset={Request.WorksetId}: Set={ok}");
                                if (ok) changed = true;
                            }
                        }

                        // ── Instance parameters ────────────────────────────────────────
                        foreach (var assignment in instanceAssignments)
                        {
                            var ok = ApplyParamAssignment(element, assignment, doc, diag);
                            if (ok) changed = true;
                        }

                        if (changed) result.ElementsAssigned++;
                        DiagLog(diag, $"    → changed={changed}  ElementsAssigned={result.ElementsAssigned}");
                    }
                    catch (Exception ex)
                    {
                        var msg = $"ElementId {id}: {ex.Message}";
                        result.Errors.Add(msg);
                        DiagLog(diag, $"  Elem {id} EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                tx.Commit();
                DiagLog(diag, $"  Transaction committed.");
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
                DiagLog(diag, $"  Transaction EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            }

            DiagLog(diag, $"=== Result: Assigned={result.ElementsAssigned}  Errors={result.Errors.Count} ===");
            FlushDiag(diag);
            OnCompleted?.Invoke(result);
        }

        /// <summary>
        /// Aplica una asignación de parámetro a un elemento. Retorna true si el parámetro
        /// fue modificado.
        /// </summary>
        private static bool ApplyParamAssignment(
            Element element, ParameterAssignment assignment, Document doc, StringBuilder diag)
        {
            var param = element.LookupParameter(assignment.ParameterName);

            if (param == null)
            {
                DiagLog(diag, $"    '{assignment.ParameterName}': NOT FOUND");
                return false;
            }

            DiagLog(diag, $"    '{assignment.ParameterName}': StorageType={param.StorageType}  IsReadOnly={param.IsReadOnly}");

            if (param.IsReadOnly)
            {
                DiagLog(diag, $"      → IsReadOnly=true — skip");
                return false;
            }

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        var r1 = param.Set(assignment.NewValue);
                        DiagLog(diag, $"      → Set(string '{assignment.NewValue}') = {r1}");
                        return r1;

                    case StorageType.Integer:
                        if (int.TryParse(assignment.NewValue, out var iv))
                        {
                            var r2 = param.Set(iv);
                            DiagLog(diag, $"      → Set(int {iv}) = {r2}");
                            return r2;
                        }
                        else
                        {
                            var r3 = param.SetValueString(assignment.NewValue);
                            DiagLog(diag, $"      → SetValueString('{assignment.NewValue}') = {r3}");
                            return r3;
                        }

                    case StorageType.Double:
                        var r4 = param.SetValueString(assignment.NewValue);
                        DiagLog(diag, $"      → SetValueString('{assignment.NewValue}') = {r4}");
                        return r4;

                    case StorageType.ElementId:
                        return TrySetElementIdParam(param, assignment.NewValue, doc, diag);

                    default:
                        DiagLog(diag, $"      → StorageType desconocido — skip");
                        return false;
                }
            }
            catch (Exception ex)
            {
                DiagLog(diag, $"      → EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Intenta asignar un parámetro de tipo ElementId localizando el elemento
        /// referenciado por su nombre (AsValueString). Prueba en orden:
        ///   1. SetValueString (puede funcionar para algunos tipos como Tipos de familia)
        ///   2. Búsqueda por nombre entre fases, niveles, materiales, etc.
        /// </summary>
        private static bool TrySetElementIdParam(
            Parameter param, string displayValue, Document doc, StringBuilder diag)
        {
            // Intento 1: SetValueString (funciona para Tipos, niveles en algunos contextos)
            try
            {
                var r1 = param.SetValueString(displayValue);
                DiagLog(diag, $"      → SetValueString('{displayValue}') = {r1}");
                if (r1) return true;
            }
            catch (Exception ex)
            {
                DiagLog(diag, $"      → SetValueString EXCEPTION: {ex.Message}");
            }

            // Intento 2: buscar ElementId por nombre en fases
            var phase = TryFindByName<Phase>(doc, displayValue);
            if (phase != null)
            {
                try
                {
                    var r2 = param.Set(phase.Id);
#if REVIT2024
#pragma warning disable CS0618
                    DiagLog(diag, $"      → Phase.Set({phase.Id.IntegerValue}) = {r2}");
#pragma warning restore CS0618
#else
                    DiagLog(diag, $"      → Phase.Set({phase.Id.Value}) = {r2}");
#endif
                    if (r2) return true;
                }
                catch (Exception ex) { DiagLog(diag, $"      → Phase.Set EXCEPTION: {ex.Message}"); }
            }

            // Intento 3: buscar en niveles
            var level = TryFindByName<Level>(doc, displayValue);
            if (level != null)
            {
                try
                {
                    var r3 = param.Set(level.Id);
#if REVIT2024
#pragma warning disable CS0618
                    DiagLog(diag, $"      → Level.Set({level.Id.IntegerValue}) = {r3}");
#pragma warning restore CS0618
#else
                    DiagLog(diag, $"      → Level.Set({level.Id.Value}) = {r3}");
#endif
                    if (r3) return true;
                }
                catch (Exception ex) { DiagLog(diag, $"      → Level.Set EXCEPTION: {ex.Message}"); }
            }

            DiagLog(diag, $"      → ElementId param: all attempts failed for '{displayValue}'");
            return false;
        }

        private static T? TryFindByName<T>(Document doc, string name) where T : Element
        {
            try
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(T))
                    .Cast<T>()
                    .FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }

        public string GetName() => "BIMPills: SubprojectAssignHandler";
    }

    /// <summary>
    /// Lee los worksets disponibles del documento. Util para poblar el selector en la UI.
    /// </summary>
    public static class WorksetReader
    {
        public static IReadOnlyList<(long Id, string Name)> GetUserWorksets(Document doc)
        {
            if (!doc.IsWorkshared) return Array.Empty<(long, string)>();

            return new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Cast<Workset>()
                .Select(w =>
                {
#pragma warning disable CS0618
                    long id = w.Id.IntegerValue;
#pragma warning restore CS0618
                    return (id, w.Name);
                })
                .OrderBy(w => w.Name)
                .ToList();
        }
    }
}
