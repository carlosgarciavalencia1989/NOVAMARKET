using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>A cell position, 0-based (A1 = column 0, row 0). Text form "AB12".</summary>
    public readonly struct CellAddress : IEquatable<CellAddress>
    {
        /// <summary>Largest column (ZZ) and row the sheet accepts.</summary>
        public const int MaxColumns = 702;
        public const int MaxRows = 100_000;

        public CellAddress(int column, int row)
        {
            if (column < 0 || column >= MaxColumns) throw new ArgumentOutOfRangeException(nameof(column));
            if (row < 0 || row >= MaxRows) throw new ArgumentOutOfRangeException(nameof(row));
            Column = column;
            Row = row;
        }

        public int Column { get; }
        public int Row { get; }

        public static bool IsValid(int column, int row) => column >= 0 && column < MaxColumns && row >= 0 && row < MaxRows;

        /// <summary>Column letters: 0 → "A", 25 → "Z", 26 → "AA".</summary>
        public static string ColumnName(int column)
        {
            if (column < 0 || column >= MaxColumns) throw new ArgumentOutOfRangeException(nameof(column));
            string name = string.Empty;
            for (int n = column + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
            return name;
        }

        /// <summary>Column index of letters ("AT" → 45), or -1.</summary>
        public static int ColumnIndex(string letters)
        {
            if (string.IsNullOrEmpty(letters) || letters.Length > 2) return -1;
            int index = 0;
            foreach (char c in letters)
            {
                char upper = char.ToUpperInvariant(c);
                if (upper < 'A' || upper > 'Z') return -1;
                index = index * 26 + (upper - 'A' + 1);
            }
            return index - 1 < MaxColumns ? index - 1 : -1;
        }

        public static bool TryParse(string text, out CellAddress address)
        {
            address = default;
            if (string.IsNullOrEmpty(text)) return false;
            int split = 0;
            while (split < text.Length && char.IsLetter(text[split])) split++;
            int column = ColumnIndex(text.Substring(0, split));
            if (column < 0 || split == text.Length) return false;
            for (int i = split; i < text.Length; i++) if (!char.IsDigit(text[i])) return false;
            if (!int.TryParse(text.Substring(split), out int row) || !IsValid(column, row - 1)) return false;
            address = new CellAddress(column, row - 1);
            return true;
        }

        public static CellAddress Parse(string text) =>
            TryParse(text, out CellAddress address) ? address : throw new FormatException($"'{text}' is not a cell address.");

        public CellAddress Offset(int columns, int rows) => new CellAddress(Column + columns, Row + rows);

        public bool Equals(CellAddress other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object obj) => obj is CellAddress other && Equals(other);
        public override int GetHashCode() => Column * 397 ^ Row;
        public static bool operator ==(CellAddress a, CellAddress b) => a.Equals(b);
        public static bool operator !=(CellAddress a, CellAddress b) => !a.Equals(b);

        public override string ToString() => ColumnName(Column) + (Row + 1);
    }

    /// <summary>A rectangle of cells ("B2:B9"), always stored with Start at the top left.</summary>
    public readonly struct CellRange : IEquatable<CellRange>
    {
        public CellRange(CellAddress a, CellAddress b)
        {
            Start = new CellAddress(Math.Min(a.Column, b.Column), Math.Min(a.Row, b.Row));
            End = new CellAddress(Math.Max(a.Column, b.Column), Math.Max(a.Row, b.Row));
        }

        public CellAddress Start { get; }
        public CellAddress End { get; }
        public int Width => End.Column - Start.Column + 1;
        public int Height => End.Row - Start.Row + 1;
        public int Count => Width * Height;

        public static CellRange Parse(string text)
        {
            string[] parts = text.Split(':');
            if (parts.Length == 1) return new CellRange(CellAddress.Parse(parts[0]), CellAddress.Parse(parts[0]));
            if (parts.Length != 2) throw new FormatException($"'{text}' is not a range.");
            return new CellRange(CellAddress.Parse(parts[0]), CellAddress.Parse(parts[1]));
        }

        public bool Contains(CellAddress cell) =>
            cell.Column >= Start.Column && cell.Column <= End.Column && cell.Row >= Start.Row && cell.Row <= End.Row;

        /// <summary>Cells row by row, left to right.</summary>
        public IEnumerable<CellAddress> Cells()
        {
            for (int row = Start.Row; row <= End.Row; row++)
                for (int column = Start.Column; column <= End.Column; column++)
                    yield return new CellAddress(column, row);
        }

        public bool Equals(CellRange other) => Start == other.Start && End == other.End;
        public override bool Equals(object obj) => obj is CellRange other && Equals(other);
        public override int GetHashCode() => Start.GetHashCode() * 31 + End.GetHashCode();

        public override string ToString() => Count == 1 ? Start.ToString() : $"{Start}:{End}";
    }
}
