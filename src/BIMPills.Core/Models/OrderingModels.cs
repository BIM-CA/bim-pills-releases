using System.Collections.Generic;

namespace BIMPills.Core.Models
{
    /// <summary>Determines whether the sequence uses integers or letters.</summary>
    public enum SequenceType { Numeric, Alphabetic }

    /// <summary>
    /// Configuration for an incremental ordering/numbering session.
    /// </summary>
    public class OrderingConfig
    {
        public string       CategoryName  { get; set; } = "";
        public string       ParameterName { get; set; } = "";
        public string       Prefix        { get; set; } = "";
        /// <summary>
        /// For Numeric: the integer start value.
        /// For Alphabetic: the 0-based letter index (0=A, 1=B, …, 25=Z, 26=AA…).
        /// </summary>
        public int          StartValue    { get; set; } = 1;
        public int          Step          { get; set; } = 1;
        public string       Suffix        { get; set; } = "";
        public SequenceType SequenceType  { get; set; } = SequenceType.Numeric;

        public string FormatValue(int n)
        {
            string core = SequenceType == SequenceType.Alphabetic
                ? IndexToLetters(n)
                : n.ToString();
            return $"{Prefix}{core}{Suffix}";
        }

        /// <summary>Converts a 0-based index to a letter string (0→A, 25→Z, 26→AA…).</summary>
        public static string IndexToLetters(int index)
        {
            if (index < 0) index = 0;
            string result = "";
            do
            {
                result = (char)('A' + index % 26) + result;
                index  = index / 26 - 1;
            }
            while (index >= 0);
            return result;
        }

        /// <summary>Converts a letter string back to a 0-based index (A→0, B→1, Z→25, AA→26…).</summary>
        public static int LettersToIndex(string letters)
        {
            if (string.IsNullOrWhiteSpace(letters)) return 0;
            letters = letters.Trim().ToUpperInvariant();
            int result = 0;
            foreach (char c in letters)
                result = result * 26 + (c - 'A' + 1);
            return result - 1;
        }
    }

    /// <summary>
    /// Live state of an active ordering session.
    /// Shared between the pick handler and the floating UI window.
    /// </summary>
    public class OrderingSessionState
    {
        public OrderingConfig Config       { get; set; } = new OrderingConfig();
        public int            CurrentValue { get; set; }
        public bool           IsActive     { get; set; } = true;

        public List<OrderingHistoryEntry> History { get; } = new List<OrderingHistoryEntry>();
    }

    public class OrderingHistoryEntry
    {
        public long   ElementId     { get; set; }
        public string PreviousValue { get; set; } = "";
        public string AssignedValue { get; set; } = "";
    }
}
