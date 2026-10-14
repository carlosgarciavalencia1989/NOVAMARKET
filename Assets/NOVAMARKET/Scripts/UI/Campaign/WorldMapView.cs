using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Data;
using NovaMarket.Simulation.Campaign;
using NovaMarket.Simulation.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// Map of world 1 (Forecasting): one node per step of the Atlas flowchart along a path, the boss
    /// level at the end, and a detail card for the selected level (Atlas step, mode, characters, gem,
    /// stars). Built levels can be played; the rest are locked.
    /// </summary>
    public sealed class WorldMapView
    {
        /// <summary>The planner's boss level (the v1 season), shown locked at the end of the path.</summary>
        public static readonly LevelInfo BossLevel = new LevelInfo("JEFE", "Temporada de fin de año", "Carril PLANEAR completo",
            "16 semanas y 4 SKU: cada semana recorres todo el carril PLANEAR, de la historia a la orden de compra.",
            ScaffoldMode.Free, null, false);

        private static readonly string[] WorldOneCast = { "andres", "sofia" };
        private static readonly string[] BossCast = { "andres", "valeria", "marcela", "paula", "julian", "sofia" };

        private readonly VisualElement layer;
        private readonly VisualElement screen;
        private readonly CampaignDatabase database;
        private readonly Action<LevelInfo> onPlay;
        private readonly Label subtitle;
        private readonly VisualElement tabs;
        private readonly VisualElement board;
        private readonly MapPath path;
        private readonly VisualElement detail;
        private readonly List<(LevelInfo Level, VisualElement Node)> nodes = new List<(LevelInfo, VisualElement)>();
        private LevelInfo selected;

        /// <summary>Best result of each level, shown in the detail card (null = nothing played).</summary>
        public CampaignProgress Progress { get; set; }

        public WorldMapView(VisualElement layer, CampaignDatabase database, Action<LevelInfo> onPlay, Action onBack)
        {
            this.layer = layer;
            this.database = database;
            this.onPlay = onPlay;

            screen = HudElements.Box("panel", "map-screen");
            VisualElement header = HudElements.Box("map-header");
            header.Add(new HudIcon { Kind = HudIconKind.Map }.WithClass("choice-icon"));
            header.Add(HudElements.Text("Mapa de mundos", "map-title"));
            subtitle = HudElements.Text(string.Empty, "map-subtitle");
            header.Add(subtitle);
            header.Add(HudElements.Box("planning-spacer"));
            header.Add(HudElements.Button("Cambiar rol o sector", onBack, "workspace-button"));
            screen.Add(header);

            tabs = HudElements.Box("world-tabs");
            screen.Add(tabs);

            VisualElement body = HudElements.Box("map-body");
            board = HudElements.Box("map-board");
            path = new MapPath();
            board.Add(path);
            board.RegisterCallback<GeometryChangedEvent>(_ => LayoutNodes());
            body.Add(board);
            detail = HudElements.Box("level-detail");
            body.Add(detail);
            screen.Add(body);
            layer.Add(screen);

            foreach (LevelInfo level in ForecastWorld.Levels.Append(BossLevel)) AddNode(level);
        }

        public void Show(RoleDefinition role, SectorDefinition sector)
        {
            subtitle.text = $"{role.Name} · {sector.Name}";
            tabs.Clear();
            for (int i = 0; i < role.Worlds.Count; i++)
            {
                VisualElement tab = HudElements.Box("world-tab");
                tab.Add(HudElements.Text($"Mundo {i + 1} · {role.Worlds[i]}", "world-tab-text"));
                if (i == 0) tab.AddToClassList("world-tab--active");
                else
                {
                    tab.AddToClassList("world-tab--locked");
                    tab.Add(new HudIcon { Kind = HudIconKind.Lock });
                }
                tabs.Add(tab);
            }
            Select(selected ?? ForecastWorld.Levels[0]);
            foreach (VisualElement child in layer.Children()) HudElements.SetVisible(child, child == screen);
            HudElements.SetVisible(layer, true);
        }

        public void Hide() => HudElements.SetVisible(screen, false);

        private void AddNode(LevelInfo level)
        {
            bool boss = level == BossLevel;
            VisualElement node = HudElements.Box("map-node");
            if (!level.IsBuilt) node.AddToClassList("map-node--locked");
            if (boss) node.AddToClassList("map-node--boss");
            VisualElement circle = HudElements.Box("map-node-circle");
            circle.Add(HudElements.Text(boss ? "JEFE" : level.Id, "map-node-id"));
            if (!level.IsBuilt) circle.Add(new HudIcon { Kind = HudIconKind.Lock });
            node.Add(circle);
            // The label sits outside the node, above or below it (chosen in LayoutNodes) so the path never crosses it.
            VisualElement label = HudElements.Box("map-node-label");
            label.Add(HudElements.Text(level.Title, "map-node-title"));
            label.Add(HudElements.Text(boss ? "NIVEL JEFE" : $"Paso {level.AtlasStep} del Atlas", "map-node-step"));
            node.Add(label);
            node.RegisterCallback<ClickEvent>(_ => Select(level));
            board.Add(node);
            nodes.Add((level, node));
        }

        /// <summary>
        /// Nodes zigzag low/high from left to right with the boss a little further away. Each label goes on
        /// the outer side (below the low nodes, above the high ones): both neighbours of a node are on the
        /// other side, so the path never runs over a label.
        /// </summary>
        private void LayoutNodes()
        {
            Rect rect = board.contentRect;
            if (rect.width < 10) return;
            var centres = new List<Vector2>();
            int count = nodes.Count;
            float bossGap = 1.45f; // the last step is wider than the others
            float step = (rect.width - 210f) / (count - 2 + bossGap);
            for (int i = 0; i < count; i++)
            {
                bool boss = nodes[i].Level == BossLevel;
                bool high = i % 2 == 1;
                float x = 100f + step * (boss ? count - 2 + bossGap : i);
                float y = rect.height * 0.5f + (high ? -1f : 1f) * rect.height * 0.2f;
                centres.Add(new Vector2(x, y));
                VisualElement node = nodes[i].Node;
                float radius = boss ? 62f : 48f;
                node.style.left = x - 75f;
                node.style.top = y - radius;
                VisualElement label = node.Q(className: "map-node-label");
                label.EnableInClassList("map-node-label--above", high);
                label.EnableInClassList("map-node-label--below", !high);
            }
            path.SetPoints(centres, ForecastWorld.Levels.Count(l => l.IsBuilt) - 1);
        }

        private void Select(LevelInfo level)
        {
            selected = level;
            foreach ((LevelInfo info, VisualElement node) in nodes) node.EnableInClassList("map-node--selected", info == level);
            foreach ((LevelInfo info, VisualElement node) in nodes)
                node.EnableInClassList("map-node--done", Progress?.Best(info.Id)?.Stars > 0);

            detail.Clear();
            bool boss = level == BossLevel;
            detail.Add(HudElements.Text(boss ? "NIVEL JEFE DEL PLANEADOR" : $"NIVEL {level.Id} · MUNDO 1", "detail-id"));
            detail.Add(HudElements.Text(level.Title, "detail-title"));
            detail.Add(HudElements.Text(boss ? level.AtlasStep : $"Paso {level.AtlasStep} del Atlas", "detail-step"));
            detail.Add(HudElements.Text(level.Summary, "detail-summary"));

            if (level.IsBuilt)
            {
                detail.Add(HudElements.Text("MODO", "detail-section"));
                detail.Add(Row(null, ModeText(level.Mode)));
            }

            detail.Add(HudElements.Text("PERSONAJES", "detail-section"));
            VisualElement cast = HudElements.Box("cast-row");
            foreach (string id in boss ? BossCast : WorldOneCast)
            {
                CharacterData character = database.FindCharacter(id);
                if (character != null) cast.Add(NpcBadge.Member(character));
            }
            detail.Add(cast);

            if (level.Gem != null)
            {
                detail.Add(HudElements.Text("RECOMPENSA", "detail-section"));
                var gem = new HudIcon { Kind = HudIconKind.Gem }.WithClass("detail-gem");
                if (ColorUtility.TryParseHtmlString(level.Gem.ColorHex, out Color color)) gem.style.color = color;
                detail.Add(Row(gem, $"{level.Gem.Name} ({level.Gem.Shape}) · {level.Gem.Points} puntos + 100 por estrella"));
                VisualElement stars = HudElements.Box("detail-stars");
                LevelRecord best = Progress?.Best(level.Id);
                for (int i = 0; i < 3; i++)
                    stars.Add(new HudIcon { Kind = HudIconKind.Service }.WithClass(best != null && i < best.Stars ? "detail-star--on" : "detail-star"));
                detail.Add(Row(stars, best == null ? "Sin jugar"
                    : best.Stars == 0 ? "Jugado, sin superar"
                    : $"Tu mejor resultado: {best.Stars} {(best.Stars == 1 ? "estrella" : "estrellas")} · {best.Points} puntos"));
            }

            detail.Add(HudElements.Box("detail-spacer"));
            Button play = HudElements.Button(level.IsBuilt ? "JUGAR NIVEL" : boss ? "SE DESBLOQUEA AL TERMINAR LOS MUNDOS" : "PRÓXIMAMENTE",
                () => onPlay?.Invoke(level), "big-button", "detail-play");
            play.SetEnabled(level.IsBuilt);
            detail.Add(play);
        }

        private static VisualElement Row(VisualElement icon, string text)
        {
            VisualElement row = HudElements.Box("detail-row");
            if (icon != null) row.Add(icon);
            Label label = HudElements.Text(text, "detail-text");
            if (icon == null) label.style.marginLeft = 0;
            row.Add(label);
            return row;
        }

        private static string ModeText(ScaffoldMode mode) => mode switch
        {
            ScaffoldMode.Guided => "Guiado: Andrés explica cada paso y deja parte del trabajo hecho.",
            ScaffoldMode.EmptyBoxes => "Casillas vacías: la fórmula completa sin valores; si te equivocas, una pista.",
            _ => "Libre: solo los datos y el resultado que debes entregar."
        };

        /// <summary>The path between the level nodes: solid up to the last built level, dotted after it.</summary>
        private sealed class MapPath : VisualElement
        {
            private List<Vector2> points = new List<Vector2>();
            private int lastBuilt;

            public MapPath()
            {
                AddToClassList("map-path");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetPoints(List<Vector2> centres, int lastBuiltIndex)
            {
                points = centres;
                lastBuilt = lastBuiltIndex;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                if (points.Count < 2) return;
                Painter2D painter = context.painter2D;
                Color gold = resolvedStyle.color;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector2 a = points[i], b = points[i + 1];
                    Vector2 control = new Vector2((a.x + b.x) / 2f, (a.y + b.y) / 2f);
                    if (i < lastBuilt)
                    {
                        painter.strokeColor = gold;
                        painter.lineWidth = 7f;
                        painter.BeginPath();
                        painter.MoveTo(a);
                        painter.BezierCurveTo(new Vector2(control.x, a.y), new Vector2(control.x, b.y), b);
                        painter.Stroke();
                        continue;
                    }
                    painter.fillColor = new Color(gold.r, gold.g, gold.b, 0.45f);
                    for (float t = 0.08f; t < 0.95f; t += 0.07f)
                    {
                        Vector2 p = Bezier(a, new Vector2(control.x, a.y), new Vector2(control.x, b.y), b, t);
                        painter.BeginPath();
                        painter.Arc(p, 3.2f, Angle.Degrees(0f), Angle.Degrees(360f));
                        painter.ClosePath();
                        painter.Fill();
                    }
                }
            }

            private static Vector2 Bezier(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, float t)
            {
                float u = 1 - t;
                return u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b;
            }
        }
    }
}
