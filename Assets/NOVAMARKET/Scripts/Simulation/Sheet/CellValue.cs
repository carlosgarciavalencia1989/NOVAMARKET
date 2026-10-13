using System;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>Errors a cell can show, with the texts of Excel in Spanish.</summary>
    public enum SheetError
    {
        DivisionByZero,
        Value,
        Reference,
        Name,
        NotAvailable,
        Number,
        Circular,
        Syntax
    }

    public enum CellValueKind
    {
        Empty,
        Number,
        Text,
        Boolean,
        Error
    }

    /// <summary>The computed value of a cell: empty, number, text, boolean (VERDADERO/FALSO) or error.</summary>
    public readonly struct CellValue : IEquatable<CellValue>
    {
        private CellValue(CellValueKind kind, double number, string text, SheetError error)
        {
            Kind = kind;
            Number = number;
            Text = text;
            Error = error;
        }

        public static readonly CellValue Empty = default;
        public static CellValue Of(double number) =>
            double.IsNaN(number) || double.IsInfinity(number) ? Fail(SheetError.Number) : new CellValue(CellValueKind.Number, number, null, default);
        public static CellValue Of(string text) => new CellValue(CellValueKind.Text, 0, text ?? string.Empty, default);
        public static CellValue Of(bool value) => new CellValue(CellValueKind.Boolean, value ? 1 : 0, null, default);
        public static CellValue Fail(SheetError error) => new CellValue(CellValueKind.Error, 0, null, error);

        public CellValueKind Kind { get; }

        /// <summary>The number (1/0 for booleans, 0 otherwise).</summary>
        public double Number { get; }

        public string Text { get; }
        public SheetError Error { get; }

        public bool IsEmpty => Kind == CellValueKind.Empty;
        public bool IsNumber => Kind == CellValueKind.Number;
        public bool IsText => Kind == CellValueKind.Text;
        public bool IsBoolean => Kind == CellValueKind.Boolean;
        public bool IsError => Kind == CellValueKind.Error;

        /// <summary>"#¡DIV/0!" style text of an error.</summary>
        public static string ErrorText(SheetError error) => error switch
        {
            SheetError.DivisionByZero => "#¡DIV/0!",
            SheetError.Value => "#¡VALOR!",
            SheetError.Reference => "#¡REF!",
            SheetError.Name => "#¿NOMBRE?",
            SheetError.NotAvailable => "#N/A",
            SheetError.Number => "#¡NUM!",
            SheetError.Circular => "#¡CIRCULAR!",
            _ => "#¡SINTAXIS!"
        };

        public bool Equals(CellValue other) =>
            Kind == other.Kind && Number.Equals(other.Number) && Text == other.Text && Error == other.Error;

        public override bool Equals(object obj) => obj is CellValue other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, Number, Text, Error);

        public override string ToString() => Kind switch
        {
            CellValueKind.Empty => string.Empty,
            CellValueKind.Number => SheetNumber.Format(Number, CellFormat.General),
            CellValueKind.Text => Text,
            CellValueKind.Boolean => Number != 0 ? "VERDADERO" : "FALSO",
            _ => ErrorText(Error)
        };
    }
}
