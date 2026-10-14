using System;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>
    /// Reference picking while a formula is typed, as in Excel: after "=", "(", ";", ":" or an operator,
    /// a click on a cell writes its reference where the cursor is, and the text around it is kept.
    /// </summary>
    public static class FormulaEditing
    {
        private const string ReferenceStarters = "=(;+-*/^:<>&";

        /// <summary>
        /// True when a click should insert a reference at <paramref name="caret"/>: the text is a formula
        /// and the character before the cursor (spaces aside) opens an argument or follows an operator.
        /// </summary>
        public static bool CanInsertReference(string text, int caret)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '=') return false;
            int i = Math.Min(Math.Max(caret, 0), text.Length) - 1;
            while (i > 0 && text[i] == ' ') i--;
            return i >= 0 && ReferenceStarters.IndexOf(text[i]) >= 0;
        }

        /// <summary>
        /// Writes <paramref name="reference"/> in place of the characters from <paramref name="start"/> to
        /// <paramref name="end"/> (the reference picked before, or nothing) and returns the new text, with
        /// the cursor right after the reference.
        /// </summary>
        public static string Insert(string text, int start, int end, string reference, out int caret)
        {
            text ??= string.Empty;
            start = Math.Min(Math.Max(start, 0), text.Length);
            end = Math.Min(Math.Max(end, start), text.Length);
            caret = start + reference.Length;
            return text.Substring(0, start) + reference + text.Substring(end);
        }
    }
}
