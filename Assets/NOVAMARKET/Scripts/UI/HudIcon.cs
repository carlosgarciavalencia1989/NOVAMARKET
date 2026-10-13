using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>Pictograms the HUD can draw.</summary>
    public enum HudIconKind
    {
        None,
        Warehouse,
        Gate,
        Office,
        Container,
        Forklift,
        Rack,
        Budget,
        Truck,
        Worker,
        Service,
        Check,
        Gem,
        Info,
        Success,
        Warning,
        Critical,
        Lock,
        Erp,
        Sheet,
        Map
    }

    /// <summary>
    /// Vector pictogram drawn with Painter2D in the element's text colour (USS <c>color</c>), so
    /// icons need no image assets and recolour from the stylesheet. Shapes are designed on a
    /// 24 × 24 grid and scaled to the element's size.
    /// </summary>
    [UxmlElement]
    public partial class HudIcon : VisualElement
    {
        private static readonly Color Detail = new Color(0f, 0f, 0f, 0.55f);

        private HudIconKind kind;
        private float scale;
        private Vector2 offset;

        public HudIcon()
        {
            AddToClassList("hud-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        [UxmlAttribute]
        public HudIconKind Kind
        {
            get => kind;
            set
            {
                kind = value;
                MarkDirtyRepaint();
            }
        }

        private Vector2 P(float x, float y) => offset + new Vector2(x, y) * scale;

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (kind == HudIconKind.None || rect.width < 1f || rect.height < 1f) return;

            scale = Mathf.Min(rect.width, rect.height) / 24f;
            offset = new Vector2((rect.width - 24f * scale) / 2f, (rect.height - 24f * scale) / 2f);
            Color color = resolvedStyle.color;
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            switch (kind)
            {
                case HudIconKind.Warehouse:
                    Polygon(painter, color, P(2, 11), P(12, 4), P(22, 11), P(22, 21), P(2, 21));
                    Polygon(painter, Detail, P(8, 13), P(16, 13), P(16, 21), P(8, 21));
                    Line(painter, color, 1.2f, P(8, 16), P(16, 16));
                    Line(painter, color, 1.2f, P(8, 18.5f), P(16, 18.5f));
                    break;
                case HudIconKind.Gate:
                    Polygon(painter, color, P(3, 7), P(8, 7), P(8, 21), P(3, 21));
                    Polygon(painter, Detail, P(4, 9), P(7, 9), P(7, 12), P(4, 12));
                    Line(painter, color, 2.6f, P(8, 13), P(22, 13));
                    Line(painter, Detail, 2.6f, P(12, 13), P(14, 13));
                    Line(painter, Detail, 2.6f, P(17, 13), P(19, 13));
                    break;
                case HudIconKind.Office:
                    Polygon(painter, color, P(5, 3), P(19, 3), P(19, 21), P(5, 21));
                    for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 2; column++)
                    {
                        float x = 7.5f + column * 5.5f, y = 5.5f + row * 4.5f;
                        Polygon(painter, Detail, P(x, y), P(x + 3.5f, y), P(x + 3.5f, y + 2.5f), P(x, y + 2.5f));
                    }
                    Polygon(painter, Detail, P(10.5f, 17), P(13.5f, 17), P(13.5f, 21), P(10.5f, 21));
                    break;
                case HudIconKind.Container:
                    Polygon(painter, color, P(2, 7), P(22, 7), P(22, 18), P(2, 18));
                    for (float x = 6f; x < 21f; x += 4f) Line(painter, Detail, 1.4f, P(x, 9), P(x, 16));
                    break;
                case HudIconKind.Forklift:
                    Polygon(painter, color, P(3, 9), P(13, 9), P(15, 13), P(15, 18), P(3, 18));
                    Polygon(painter, Detail, P(5, 10.5f), P(11, 10.5f), P(12.5f, 13), P(5, 13));
                    Line(painter, color, 1.6f, P(4, 9), P(5, 4), P(12, 4), P(13, 9));
                    Line(painter, color, 2f, P(17, 3), P(17, 19));
                    Line(painter, color, 2f, P(17, 19), P(23, 19));
                    Circle(painter, color, P(6, 19.5f), 2.6f * scale);
                    Circle(painter, color, P(13, 19.5f), 2.6f * scale);
                    Circle(painter, Detail, P(6, 19.5f), 1.1f * scale);
                    Circle(painter, Detail, P(13, 19.5f), 1.1f * scale);
                    break;
                case HudIconKind.Rack:
                    Line(painter, color, 2f, P(3, 3), P(3, 21));
                    Line(painter, color, 2f, P(21, 3), P(21, 21));
                    Line(painter, color, 2f, P(3, 11), P(21, 11));
                    Line(painter, color, 2f, P(3, 20), P(21, 20));
                    Polygon(painter, color, P(5, 5), P(11, 5), P(11, 10), P(5, 10));
                    Polygon(painter, color, P(13, 14), P(19, 14), P(19, 19), P(13, 19));
                    Polygon(painter, color, P(5, 15), P(10, 15), P(10, 19), P(5, 19));
                    break;
                case HudIconKind.Budget:
                    Circle(painter, color, P(12, 12), 9.5f * scale);
                    Ring(painter, Detail, P(12, 12), 7f * scale, 1.3f * scale);
                    Line(painter, Detail, 2f, P(14.5f, 8.5f), P(10, 8.5f));
                    Line(painter, Detail, 2f, P(10, 8.5f), P(10, 12), P(14, 12), P(14, 15.5f), P(9.5f, 15.5f));
                    Line(painter, Detail, 1.6f, P(12, 6.5f), P(12, 17.5f));
                    break;
                case HudIconKind.Truck:
                    Polygon(painter, color, P(1, 6), P(14, 6), P(14, 17), P(1, 17));
                    Polygon(painter, color, P(15, 9), P(20, 9), P(23, 13), P(23, 17), P(15, 17));
                    Polygon(painter, Detail, P(16.5f, 10.5f), P(19.5f, 10.5f), P(21, 13), P(16.5f, 13));
                    Circle(painter, color, P(5.5f, 18.5f), 2.6f * scale);
                    Circle(painter, color, P(18.5f, 18.5f), 2.6f * scale);
                    Circle(painter, Detail, P(5.5f, 18.5f), 1.1f * scale);
                    Circle(painter, Detail, P(18.5f, 18.5f), 1.1f * scale);
                    break;
                case HudIconKind.Worker:
                    Circle(painter, color, P(12, 8), 4f * scale);
                    Polygon(painter, color, P(7, 5.5f), P(17, 5.5f), P(16, 3.5f), P(12, 2.5f), P(8, 3.5f));
                    Polygon(painter, color, P(4, 22), P(5, 16), P(8.5f, 13.5f), P(15.5f, 13.5f), P(19, 16), P(20, 22));
                    Line(painter, Detail, 1.4f, P(9.5f, 14.5f), P(9.5f, 22));
                    Line(painter, Detail, 1.4f, P(14.5f, 14.5f), P(14.5f, 22));
                    break;
                case HudIconKind.Service:
                    Star(painter, color, P(12, 12.5f), 10.5f * scale, 4.4f * scale);
                    break;
                case HudIconKind.Check:
                    Line(painter, color, 3f, P(4.5f, 12.5f), P(10, 18), P(20, 6));
                    break;
                case HudIconKind.Gem:
                    Polygon(painter, color, P(7, 4), P(17, 4), P(22, 9.5f), P(12, 21), P(2, 9.5f));
                    Line(painter, Detail, 1.2f, P(2, 9.5f), P(22, 9.5f));
                    Line(painter, Detail, 1.2f, P(9, 9.5f), P(12, 21), P(15, 9.5f));
                    Line(painter, Detail, 1.2f, P(7, 4), P(9, 9.5f), P(12, 4), P(15, 9.5f), P(17, 4));
                    break;
                case HudIconKind.Lock:
                    Line(painter, color, 2.4f, P(8, 11), P(8, 7.5f), P(9.5f, 4.5f), P(12, 3.5f), P(14.5f, 4.5f), P(16, 7.5f), P(16, 11));
                    Polygon(painter, color, P(5, 11), P(19, 11), P(19, 21), P(5, 21));
                    Circle(painter, Detail, P(12, 15), 1.8f * scale);
                    Line(painter, Detail, 1.6f, P(12, 15.5f), P(12, 18.5f));
                    break;
                case HudIconKind.Erp:
                    Polygon(painter, color, P(2, 4), P(22, 4), P(22, 17), P(2, 17));
                    Polygon(painter, Detail, P(4, 6), P(20, 6), P(20, 15), P(4, 15));
                    Line(painter, color, 1.4f, P(6, 8.5f), P(13, 8.5f));
                    Line(painter, color, 1.4f, P(6, 11), P(17, 11));
                    Line(painter, color, 1.4f, P(6, 13.2f), P(11, 13.2f));
                    Polygon(painter, color, P(10, 17), P(14, 17), P(15, 20), P(9, 20));
                    Line(painter, color, 1.6f, P(7, 21), P(17, 21));
                    break;
                case HudIconKind.Sheet:
                    Polygon(painter, color, P(3, 3), P(21, 3), P(21, 21), P(3, 21));
                    Polygon(painter, Detail, P(3, 3), P(21, 3), P(21, 7), P(3, 7));
                    for (float y = 11f; y < 21f; y += 4f) Line(painter, Detail, 1.2f, P(3, y), P(21, y));
                    Line(painter, Detail, 1.2f, P(9, 3), P(9, 21));
                    Line(painter, Detail, 1.2f, P(15, 3), P(15, 21));
                    break;
                case HudIconKind.Map:
                    Polygon(painter, color, P(2, 5), P(8, 3), P(16, 5), P(22, 3), P(22, 19), P(16, 21), P(8, 19), P(2, 21));
                    Line(painter, Detail, 1.2f, P(8, 3), P(8, 19));
                    Line(painter, Detail, 1.2f, P(16, 5), P(16, 21));
                    Line(painter, Detail, 1.6f, P(4.5f, 15), P(7, 12), P(11, 14), P(14, 9), P(19, 8));
                    break;
                case HudIconKind.Info:
                    Circle(painter, color, P(12, 12), 10f * scale);
                    Circle(painter, Detail, P(12, 7), 1.6f * scale);
                    Line(painter, Detail, 2.6f, P(12, 11), P(12, 17.5f));
                    break;
                case HudIconKind.Success:
                    Circle(painter, color, P(12, 12), 10f * scale);
                    Line(painter, Detail, 2.6f, P(7, 12.5f), P(10.5f, 16), P(17, 8.5f));
                    break;
                case HudIconKind.Warning:
                case HudIconKind.Critical:
                    Polygon(painter, color, P(12, 2.5f), P(22.5f, 21), P(1.5f, 21));
                    Line(painter, Detail, 2.6f, P(12, 9), P(12, 14.5f));
                    Circle(painter, Detail, P(12, 18), 1.5f * scale);
                    break;
            }
        }

        private static void Polygon(Painter2D painter, Color color, params Vector2[] points)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
            painter.ClosePath();
            painter.Fill();
        }

        private void Line(Painter2D painter, Color color, float width, params Vector2[] points)
        {
            painter.strokeColor = color;
            painter.lineWidth = width * scale;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
            painter.Stroke();
        }

        private static void Circle(Painter2D painter, Color color, Vector2 center, float radius)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.ClosePath();
            painter.Fill();
        }

        private static void Ring(Painter2D painter, Color color, Vector2 center, float radius, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.ClosePath();
            painter.Stroke();
        }

        private static void Star(Painter2D painter, Color color, Vector2 center, float outer, float inner)
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = (-90f + i * 36f) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? outer : inner;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Polygon(painter, color, points);
        }
    }
}
