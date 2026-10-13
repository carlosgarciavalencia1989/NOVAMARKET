using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Data;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Campaign;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// Start menu over the CEDI: the player picks a role (links of the Atlas chain map, grouped by
    /// lane) and an industrial sector. Roles and sectors come from the campaign data; the ones that are
    /// not enabled show "Próximamente". It also shows the team the game plays for the chosen role.
    /// </summary>
    public sealed class StartMenuView
    {
        private readonly VisualElement layer;
        private readonly VisualElement screen;
        private readonly CampaignDatabase database;
        private readonly CampaignCatalog catalog;
        private readonly Action<RoleData, SectorData> onStart;
        private readonly Dictionary<NovaDataAsset, VisualElement> cards = new Dictionary<NovaDataAsset, VisualElement>();
        private readonly VisualElement castRow;
        private readonly Label summary;
        private readonly Button startButton;

        private RoleData role;
        private SectorData sector;

        public StartMenuView(VisualElement layer, CampaignDatabase database, Action<RoleData, SectorData> onStart)
        {
            this.layer = layer;
            this.database = database;
            this.onStart = onStart;
            catalog = database.ToCatalog();

            screen = HudElements.Box("panel", "menu-screen");
            VisualElement heading = HudElements.Box("menu-heading");
            heading.Add(HudElements.Text("NOVAMARKET", "menu-title"));
            heading.Add(HudElements.Text("Juego de la cadena de suministro · CEDI Medellín, valle de Aburrá", "menu-subtitle"));
            screen.Add(heading);
            screen.Add(HudElements.Box("menu-rule"));

            VisualElement columns = HudElements.Box("menu-columns");
            VisualElement left = HudElements.Box("menu-column", "menu-column--left");
            left.Add(Step("1", "Escoge tu rol"));
            foreach (ChainLane lane in new[] { ChainLane.Plan, ChainLane.Execute, ChainLane.Return })
            {
                RoleData[] roles = database.Roles.Where(r => r != null && r.Lane == lane).ToArray();
                if (roles.Length == 0) continue;
                left.Add(HudElements.Text(roles[0].ToDefinition().LaneName, "lane-title"));
                VisualElement grid = HudElements.Box(roles.Length > 1 ? "choice-grid" : "choice-list");
                foreach (RoleData data in roles) grid.Add(RoleCard(data));
                left.Add(grid);
            }
            left.Add(HudElements.Text("HABILITAR: Finanzas, Riesgo y Tecnología no se juegan; son personajes que te acompañan.",
                "choice-detail"));
            columns.Add(left);

            VisualElement right = HudElements.Box("menu-column");
            right.Add(Step("2", "Escoge tu sector"));
            foreach (SectorData data in database.Sectors.Where(s => s != null)) right.Add(SectorCard(data));
            VisualElement cast = HudElements.Box("cast-box", "menu-cast");
            cast.Add(HudElements.Text("TU EQUIPO EN ESTA PARTIDA (LOS MANEJA EL JUEGO)", "cast-caption"));
            castRow = HudElements.Box("cast-row");
            cast.Add(castRow);

            columns.Add(right);
            screen.Add(columns);
            screen.Add(cast);

            VisualElement footer = HudElements.Box("menu-footer");
            summary = HudElements.Text(string.Empty, "menu-summary");
            footer.Add(summary);
            startButton = HudElements.Button("COMENZAR", Start, "big-button");
            footer.Add(startButton);
            screen.Add(footer);

            layer.Add(screen);
            Select(database.Roles.FirstOrDefault(r => r != null && r.IsPlayable),
                database.Sectors.FirstOrDefault(s => s != null && s.IsPlayable));
        }

        public void Show()
        {
            foreach (VisualElement child in layer.Children()) HudElements.SetVisible(child, child == screen);
            HudElements.SetVisible(layer, true);
        }

        public void Hide() => HudElements.SetVisible(screen, false);

        private static VisualElement Step(string number, string title)
        {
            VisualElement step = HudElements.Box("menu-step");
            step.Add(HudElements.Text(number, "menu-step-number"));
            step.Add(HudElements.Text(title, "menu-step-title"));
            return step;
        }

        private VisualElement RoleCard(RoleData data)
        {
            RoleDefinition definition = data.ToDefinition();
            VisualElement card = Card(data, IconOf(definition.Id), definition.Name, definition.Covers, data.IsPlayable, null);
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (data.IsPlayable) Select(data, sector);
                else ShowSoon($"El rol {definition.Name} llega pronto. Mientras tanto, sus personajes los maneja el juego.");
            });
            return card;
        }

        private VisualElement SectorCard(SectorData data)
        {
            SectorDefinition definition = data.ToDefinition();
            VisualElement card = Card(data, HudIconKind.Gem, definition.Name, definition.Traits, data.IsPlayable,
                $"{HudFormat.Number(definition.PortfolioSize)} SKU");
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (data.IsPlayable) Select(role, data);
                else ShowSoon($"El sector {definition.Name} llega pronto como un paquete de datos nuevo.");
            });
            return card;
        }

        private VisualElement Card(NovaDataAsset data, HudIconKind icon, string name, string detail, bool playable, string size)
        {
            VisualElement card = HudElements.Box("choice-card");
            if (!playable) card.AddToClassList("choice-card--locked");
            card.Add(new HudIcon { Kind = playable ? icon : HudIconKind.Lock }.WithClass("choice-icon"));
            VisualElement texts = HudElements.Box("choice-texts");
            VisualElement nameRow = HudElements.Box("choice-name-row");
            nameRow.Add(HudElements.Text(name, "choice-name"));
            if (size != null) nameRow.Add(HudElements.Text(size, "choice-size"));
            nameRow.Add(HudElements.Text(playable ? "DISPONIBLE" : "PRÓXIMAMENTE", "choice-tag",
                playable ? "choice-tag--ready" : "choice-tag--soon"));
            texts.Add(nameRow);
            texts.Add(HudElements.Text(detail, "choice-detail"));
            card.Add(texts);
            cards[data] = card;
            return card;
        }

        private void Select(RoleData newRole, SectorData newSector)
        {
            role = newRole;
            sector = newSector;
            foreach (KeyValuePair<NovaDataAsset, VisualElement> pair in cards)
                pair.Value.EnableInClassList("choice-card--selected", pair.Key == role || pair.Key == sector);

            castRow.Clear();
            if (role != null)
            {
                HashSet<string> npcs = new HashSet<string>(catalog.CreateCast(role.Id).Select(c => c.Profile.Id));
                foreach (CharacterData character in database.Characters.Where(c => c != null && npcs.Contains(c.Id)))
                    castRow.Add(NpcBadge.Member(character));
            }

            bool ready = role != null && sector != null && catalog.CanPlay(role.Id, sector.Id);
            startButton.SetEnabled(ready);
            summary.EnableInClassList("menu-summary--ready", ready);
            summary.text = ready
                ? $"{role.DisplayName} · {sector.DisplayName}: {sector.ToDefinition().Company}, " +
                  $"{HudFormat.Number(sector.ToDefinition().PortfolioSize)} SKU."
                : "Escoge un rol y un sector disponibles.";
        }

        private void ShowSoon(string text)
        {
            summary.EnableInClassList("menu-summary--ready", false);
            summary.text = text;
        }

        private void Start()
        {
            if (role != null && sector != null && catalog.CanPlay(role.Id, sector.Id)) onStart?.Invoke(role, sector);
        }

        private static HudIconKind IconOf(string roleId) => roleId switch
        {
            "planeador" => HudIconKind.Sheet,
            "abastecer" => HudIconKind.Container,
            "inventarios" => HudIconKind.Rack,
            "almacenar" => HudIconKind.Warehouse,
            "pedidos" => HudIconKind.Office,
            "transporte" => HudIconKind.Truck,
            "ultima-milla" => HudIconKind.Gate,
            _ => HudIconKind.Forklift
        };
    }
}
