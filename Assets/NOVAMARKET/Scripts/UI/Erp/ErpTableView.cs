using System;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Sheet;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Erp
{
    /// <summary>
    /// Draws an <see cref="ErpTable"/> as an ERP result grid: steel header, zebra rows, numbers right
    /// aligned with Colombian formatting, the projection column tinted, and clickable cells.
    /// </summary>
    public static class ErpTableView
    {
        public static ScrollView Build(ErpTable table, Func<int, int, bool> isClickable, Func<int, int, bool> isSelected,
            Action<int, int> onClick)
        {
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.AddToClassList("erp-table-scroll");

            VisualElement head = HudElements.Box("erp-tr", "erp-tr--head");
            for (int column = 0; column < table.Headers.Count; column++)
            {
                Label cell = Cell(table, column, table.Headers[column], "erp-th");
                if (IsNumberColumn(table, column)) cell.AddToClassList("erp-td--number");
                head.Add(cell);
            }
            scroll.Add(head);

            for (int row = 0; row < table.Rows.Count; row++)
            {
                VisualElement line = HudElements.Box("erp-tr");
                if (row % 2 == 1) line.AddToClassList("erp-tr--alt");
                CellValue[] values = table.Rows[row];
                for (int column = 0; column < values.Length; column++)
                {
                    CellValue value = values[column];
                    CellFormat format = table.Formats != null && column < table.Formats.Count ? table.Formats[column] : CellFormat.General;
                    Label cell = Cell(table, column, value.IsNumber ? SheetNumber.Format(value.Number, format) : value.ToString());
                    if (value.IsNumber) cell.AddToClassList("erp-td--number");
                    int r = row, c = column;
                    if (isClickable != null && isClickable(r, c))
                    {
                        cell.AddToClassList("erp-td--click");
                        if (isSelected != null && isSelected(r, c)) cell.AddToClassList("erp-td--selected");
                        cell.pickingMode = PickingMode.Position;
                        cell.RegisterCallback<ClickEvent>(_ => onClick?.Invoke(r, c));
                    }
                    line.Add(cell);
                }
                scroll.Add(line);
            }
            return scroll;
        }

        private static Label Cell(ErpTable table, int column, string text, string extraClass = null)
        {
            Label cell = HudElements.Text(text, "erp-td");
            if (extraClass != null) cell.AddToClassList(extraClass);
            string header = table.Headers[column];
            cell.style.width = WidthOf(header);
            if (header.EndsWith(NovaErp.ProjectionSuffix)) cell.AddToClassList("erp-td--projection");
            return cell;
        }

        private static bool IsNumberColumn(ErpTable table, int column) =>
            table.Formats != null && column < table.Formats.Count && !table.Formats[column].Equals(CellFormat.General);

        /// <summary>Column width by header (months narrow, descriptions and names wide).</summary>
        public static float WidthOf(string header) => header switch
        {
            "Material" => 112,
            "Descripción" => 340,
            "Proveedor" => 300,
            "Cliente" => 330,
            "Observación" => 280,
            "Documento" => 130,
            "UM" or "UM de venta" => 76,
            "Familia" => 150,
            "Origen" or "Modalidad" => 130,
            "Puerto de entrada" => 160,
            _ when header.EndsWith(NovaErp.ProjectionSuffix) => 136,
            _ when header.Length == 6 && header[3] == '-' => 92,
            _ => 150
        };
    }
}
