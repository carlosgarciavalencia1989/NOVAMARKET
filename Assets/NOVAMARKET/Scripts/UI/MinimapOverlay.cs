using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Draws on top of the minimap image: the CEDI marker and the outline of the ground area the
    /// main camera sees (a trapezoid, because the camera looks at the valley at an angle).
    /// </summary>
    [UxmlElement]
    public partial class MinimapOverlay : VisualElement
    {
        private static readonly Color ViewFill = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color ViewStroke = new Color(1f, 0.97f, 0.88f, 0.95f);
        private static readonly Color MarkerFill = new Color(0.94f, 0.78f, 0.35f);
        private static readonly Color MarkerStroke = new Color(0.1f, 0.07f, 0.04f);

        private readonly Vector2[] view = new Vector2[4];
        private bool hasView;
        private Vector2 marker;
        private bool hasMarker;

        public MinimapOverlay()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>Camera footprint corners in local pixels, or null to hide it.</summary>
        public void SetView(Vector2[] corners)
        {
            hasView = corners != null;
            if (hasView) corners.CopyTo(view, 0);
            MarkDirtyRepaint();
        }

        public void SetMarker(Vector2 local)
        {
            marker = local;
            hasMarker = true;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;

            if (hasMarker)
            {
                const float half = 5f;
                painter.fillColor = MarkerFill;
                painter.strokeColor = MarkerStroke;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(marker + new Vector2(0f, -half - 1f));
                painter.LineTo(marker + new Vector2(half + 1f, 0f));
                painter.LineTo(marker + new Vector2(0f, half + 1f));
                painter.LineTo(marker + new Vector2(-half - 1f, 0f));
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
            }

            if (!hasView) return;
            painter.fillColor = ViewFill;
            painter.strokeColor = ViewStroke;
            painter.lineWidth = 1.5f;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(view[0]);
            for (int i = 1; i < view.Length; i++) painter.LineTo(view[i]);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }
    }
}
