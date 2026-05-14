using System;
using System.Collections.Generic;

namespace BIMPills.Core.Seleccionar
{
    public enum FilterOperator
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        IsEmpty,
        IsNotEmpty
    }

    public enum FilterLogic
    {
        And,
        Or
    }

    public enum SelectionAction
    {
        Replace,
        Add,
        Remove
    }

    /// <summary>
    /// Condición individual: un parámetro + operador + valor esperado.
    /// </summary>
    public class FilterCondition
    {
        public string ParameterName { get; set; } = string.Empty;
        public FilterOperator Operator { get; set; } = FilterOperator.Equals;
        public string Value { get; set; } = string.Empty;
        /// <summary>
        /// Indica si la condición fue añadida como parámetro de tipo (true), instancia (false)
        /// o sin distinción (null). Se persiste en preset para reconstruir la fila correctamente.
        /// </summary>
        public bool? IsTypeParam { get; set; } = null;

        /// <summary>
        /// Evalúa la condición contra un valor de parámetro leído del elemento.
        /// <para><c>null</c> significa que el parámetro no existe en el elemento —
        /// solo <see cref="FilterOperator.IsEmpty"/> coincide en ese caso.</para>
        /// </summary>
        public bool Evaluate(string? paramValue)
        {
            // null = el parámetro no existe → solo IsEmpty lo considera match
            if (paramValue == null)
                return Operator == FilterOperator.IsEmpty;

            if (Operator == FilterOperator.IsEmpty)
                return string.IsNullOrWhiteSpace(paramValue);
            if (Operator == FilterOperator.IsNotEmpty)
                return !string.IsNullOrWhiteSpace(paramValue);

            var v = paramValue;

            return Operator switch
            {
                FilterOperator.Equals       => string.Equals(v, Value, StringComparison.OrdinalIgnoreCase),
                FilterOperator.NotEquals    => !string.Equals(v, Value, StringComparison.OrdinalIgnoreCase),
                FilterOperator.Contains     => v.IndexOf(Value, StringComparison.OrdinalIgnoreCase) >= 0,
                FilterOperator.NotContains  => v.IndexOf(Value, StringComparison.OrdinalIgnoreCase) < 0,
                FilterOperator.StartsWith   => v.StartsWith(Value, StringComparison.OrdinalIgnoreCase),
                FilterOperator.EndsWith     => v.EndsWith(Value, StringComparison.OrdinalIgnoreCase),
                FilterOperator.GreaterThan  => TryCompareNumeric(v, Value) > 0,
                FilterOperator.LessThan     => TryCompareNumeric(v, Value) < 0,
                _                           => false
            };
        }

        private static int TryCompareNumeric(string a, string b)
        {
            if (double.TryParse(a, out var da) && double.TryParse(b, out var db))
                return da.CompareTo(db);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Información de parámetro con su grupo para mostrar agrupado en la UI.
    /// </summary>
    public class ParamInfo
    {
        public string Name  { get; set; } = string.Empty;
        public string Group { get; set; } = "Otros";
        /// <summary>
        /// Valores posibles para asignación (e.g. fases del proyecto).
        /// Null = campo de texto libre. No se persiste en presets.
        /// </summary>
        public IReadOnlyList<string>? AllowedValues { get; set; }
        /// <summary>True = parámetro de tipo; False = parámetro de instancia.</summary>
        public bool IsTypeParam { get; set; } = false;
    }

    /// <summary>
    /// Filtro de selección: categoría/s + lista de condiciones sobre parámetros.
    /// </summary>
    public class SelectionFilterConfig
    {
        /// <summary>Nombre de categoría (legacy, una sola). Vacío = todas las categorías.</summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>Lista de categorías a filtrar (nuevo, múltiples). Tiene prioridad sobre CategoryName.</summary>
        public List<string> CategoryNames { get; set; } = new();

        /// <summary>
        /// Categorías efectivas: usa CategoryNames si hay alguna, si no CategoryName como fallback legacy.
        /// Vacío = todas las categorías.
        /// </summary>
        public IReadOnlyList<string> EffectiveCategoryNames
        {
            get
            {
                if (CategoryNames.Count > 0) return CategoryNames;
                if (!string.IsNullOrEmpty(CategoryName)) return new[] { CategoryName };
                return Array.Empty<string>();
            }
        }

        /// <summary>Lista de condiciones evaluadas con la lógica especificada.</summary>
        public List<FilterCondition> Conditions { get; set; } = new();

        /// <summary>Cómo combinar las condiciones: todas deben cumplirse (And) o al menos una (Or).</summary>
        public FilterLogic Logic { get; set; } = FilterLogic.And;

        /// <summary>Cómo aplicar el resultado: reemplazar selección, añadir o quitar.</summary>
        public SelectionAction Action { get; set; } = SelectionAction.Replace;

        /// <summary>
        /// Evalúa el filtro contra un diccionario de parámetros {nombre → valor} de un elemento.
        /// El caller (capa Revit) es responsable de extraer ese diccionario del elemento.
        /// </summary>
        public bool Evaluate(IReadOnlyDictionary<string, string> parameters)
        {
            if (Conditions.Count == 0) return true;

            foreach (var condition in Conditions)
            {
                parameters.TryGetValue(condition.ParameterName, out var value);
                bool conditionMet = condition.Evaluate(value);

                if (Logic == FilterLogic.Or && conditionMet)  return true;
                if (Logic == FilterLogic.And && !conditionMet) return false;
            }

            return Logic == FilterLogic.And;
        }
    }

    /// <summary>
    /// Preset guardado: un filtro con nombre, persistido a JSON.
    /// </summary>
    public class FilterPreset
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public SelectionFilterConfig Filter { get; set; } = new();

        public override string ToString() => Name;
    }

    /// <summary>
    /// Resultado de aplicar un filtro al modelo.
    /// </summary>
    public class SelectionFilterResult
    {
        public int TotalEvaluated { get; set; }
        public List<long> MatchingElementIds { get; set; } = new();
        public int MatchCount => MatchingElementIds.Count;
    }

    /// <summary>
    /// Valor a asignar en lote a un parámetro.
    /// </summary>
    public class ParameterAssignment
    {
        public string ParameterName { get; set; } = string.Empty;
        public string NewValue      { get; set; } = string.Empty;
        public bool   IsTypeParam   { get; set; } = false;
    }

    /// <summary>
    /// Resumen de elementos seleccionados por categoría, con conteo editable.
    /// </summary>
    public class CategoryElementSummary
    {
        public string CategoryName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int EditableCount { get; set; }

        public CategoryElementSummary() { }
        public CategoryElementSummary(string name, int total, int editable)
        {
            CategoryName  = name;
            TotalCount    = total;
            EditableCount = editable;
        }
    }

    /// <summary>
    /// Solicitud de asignación de valores en lote a un conjunto de elementos.
    /// </summary>
    public class SubprojectAssignRequest
    {
        public List<long> ElementIds { get; set; } = new();
        public long WorksetId { get; set; }
        public bool AssignWorkset { get; set; }
        public bool UseCurrentSelection { get; set; } = true;
        public FilterPreset? FilterPreset { get; set; }
        public List<ParameterAssignment> ParameterAssignments { get; set; } = new();
    }

    public class SubprojectAssignResult
    {
        public int ElementsAssigned { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Datos leídos de un elemento de Revit por el cuentagotas.
    /// Contiene la categoría y los valores actuales de todos sus parámetros.
    /// </summary>
    public class EyedropperData
    {
        public string CategoryName { get; set; } = string.Empty;
        /// <summary>Nombre de parámetro → valor como string.</summary>
        public Dictionary<string, string> ParamValues { get; set; } = new();
    }
}
