using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Sheet;

namespace NovaMarket.Simulation.Erp
{
    /// <summary>
    /// The result of an ERP query: a title, column headers and rows of values, plus footnotes.
    /// "Descargar a hoja" copies it to a new tab of the planner sheet with the Atlas layout:
    /// row 1 the title (the player writes the periods there), row 2 the headers, data from row 3,
    /// so with a billing query the months land in columns C…AT.
    /// </summary>
    public sealed class ErpTable
    {
        public const int TitleRow = 0;
        public const int HeaderRow = 1;
        public const int FirstDataRow = 2;

        private readonly List<CellValue[]> rows = new List<CellValue[]>();
        private readonly List<string> notes = new List<string>();

        public ErpTable(string screenId, string title, IReadOnlyList<string> headers, IReadOnlyList<CellFormat> formats = null)
        {
            ScreenId = screenId;
            Title = title;
            Headers = headers ?? throw new ArgumentNullException(nameof(headers));
            Formats = formats;
        }

        public string ScreenId { get; }
        public string Title { get; }
        public IReadOnlyList<string> Headers { get; }

        /// <summary>Number format per column (null = general).</summary>
        public IReadOnlyList<CellFormat> Formats { get; }

        public IReadOnlyList<CellValue[]> Rows => rows;
        public IReadOnlyList<string> Notes => notes;

        /// <summary>Default name of the tab it downloads to.</summary>
        public string SheetName { get; set; }

        public void AddRow(params CellValue[] values)
        {
            if (values.Length != Headers.Count) throw new ArgumentException($"Row has {values.Length} values; expected {Headers.Count}.");
            rows.Add(values);
        }

        public void AddNote(string note) => notes.Add(note);

        public int ColumnOf(string header)
        {
            for (int i = 0; i < Headers.Count; i++) if (Headers[i] == header) return i;
            return -1;
        }

        /// <summary>Copies the table to a new tab of <paramref name="book"/> and returns it.</summary>
        public Worksheet DownloadTo(Workbook book, string sheetName = null)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            Worksheet sheet = null;
            book.Batch(() =>
            {
                sheet = book.AddSheet(book.UniqueName(sheetName ?? SheetName ?? Title));
                sheet.SetText(new CellAddress(0, TitleRow), Title);
                for (int column = 0; column < Headers.Count; column++)
                {
                    sheet.SetText(new CellAddress(column, HeaderRow), Headers[column]);
                    CellFormat format = Formats != null && column < Formats.Count ? Formats[column] : CellFormat.General;
                    if (!format.Equals(CellFormat.General) && rows.Count > 0)
                        sheet.SetFormat(new CellRange(new CellAddress(column, FirstDataRow),
                            new CellAddress(column, FirstDataRow + rows.Count - 1)), format);
                }
                for (int row = 0; row < rows.Count; row++)
                    for (int column = 0; column < Headers.Count; column++)
                    {
                        CellValue value = rows[row][column];
                        var address = new CellAddress(column, FirstDataRow + row);
                        if (value.IsNumber) sheet.SetNumber(address, value.Number);
                        else if (value.IsText) sheet.SetText(address, value.Text);
                    }
                int noteRow = FirstDataRow + rows.Count + 1;
                foreach (string note in notes) sheet.SetText(new CellAddress(0, noteRow++), note);
            });
            return sheet;
        }
    }
}
