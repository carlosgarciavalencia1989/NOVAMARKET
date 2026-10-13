using System;
using System.Collections.Generic;
using System.Globalization;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>One SKU of a real sales history file: code, description, family and monthly units.</summary>
    public sealed class HistoryRecord
    {
        public HistoryRecord(string code, string description, string family, int[] units)
        {
            Code = code;
            Description = description;
            Family = family;
            Units = units ?? throw new ArgumentNullException(nameof(units));
        }

        public string Code { get; }
        public string Description { get; }
        public string Family { get; }

        /// <summary>Units sold per month, index 0 = January 2023.</summary>
        public int[] Units { get; }
    }

    /// <summary>
    /// Reads the real history file of a sector (Data/Sectors/&lt;Sector&gt;/Historia_*.csv): separator
    /// ";" (descriptions carry decimal commas), header "codigo;descripcion;familia;2023-01;…".
    /// </summary>
    public static class SalesHistoryCsv
    {
        public const char Separator = ';';

        public static IReadOnlyList<HistoryRecord> Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length == 0 || lines[0].Trim().Length == 0) throw new FormatException("The history file is empty.");

            string[] header = lines[0].Trim().TrimStart('﻿').Split(Separator);
            if (header.Length < 4 || header[0] != "codigo" || header[1] != "descripcion" || header[2] != "familia")
                throw new FormatException("Header must start with codigo;descripcion;familia.");
            for (int column = 3; column < header.Length; column++)
                if (SalesCalendar.IndexOfKey(header[column]) != column - 3)
                    throw new FormatException($"Column {column + 1} must be month {SalesCalendar.Key(column - 3)}, found '{header[column]}'.");

            var records = new List<HistoryRecord>();
            for (int row = 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (line.Length == 0) continue;
                string[] cells = line.Split(Separator);
                if (cells.Length != header.Length)
                    throw new FormatException($"Line {row + 1} has {cells.Length} columns; expected {header.Length}.");
                var units = new int[header.Length - 3];
                for (int month = 0; month < units.Length; month++)
                {
                    if (!int.TryParse(cells[month + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out units[month])
                        || units[month] < 0)
                        throw new FormatException($"Line {row + 1}, {header[month + 3]}: '{cells[month + 3]}' is not a quantity.");
                }
                records.Add(new HistoryRecord(cells[0], cells[1], cells[2], units));
            }
            return records;
        }
    }
}
