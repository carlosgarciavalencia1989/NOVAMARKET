using System;
using System.Linq;
using NovaMarket.Data;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// What happens when the player delivers: a confirmation, then the result of the level with the three
    /// stars and why each one was won or lost, the gem and the "Excel de planeador" badge, Andrés's review
    /// (with his comparison of what a model would learn from the player's series and from the clean one)
    /// and the lessons.
    /// </summary>
    public sealed class LevelResultView
    {
        private static readonly string[] MonthNames =
            { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

        private readonly ModalLayer modal;
        private readonly CampaignDatabase database;
        private readonly VisualElement confirm;
        private readonly VisualElement dialog;
        private readonly Label title;
        private readonly Label subtitle;
        private readonly HudIcon[] stars = new HudIcon[3];
        private readonly VisualElement criteria;
        private readonly VisualElement rewards;
        private readonly VisualElement review;
        private readonly VisualElement impact;
        private readonly VisualElement lessons;
        private readonly Button keepButton;
        private Action onDeliver;

        /// <param name="onRetry">Plays the level again with new data.</param>
        /// <param name="onMap">Back to the world map.</param>
        public LevelResultView(ModalLayer modal, CampaignDatabase database, Action onRetry, Action onMap)
        {
            this.modal = modal;
            this.database = database;

            confirm = modal.CreateDialog();
            confirm.Add(HudElements.Text("¿Entregar a Andrés?", "dialog-title"));
            confirm.Add(HudElements.Text("Andrés revisa tu hoja tal como está ahora y te califica. Si no pasas, puedes seguir " +
                                         "trabajando y volver a entregar.", "dialog-text"));
            VisualElement confirmButtons = HudElements.Box("dialog-buttons");
            confirmButtons.Add(HudElements.Button("Todavía no", () => modal.Hide(confirm), "dialog-button"));
            confirmButtons.Add(HudElements.Button("Entregar", Deliver, "dialog-button", "dialog-button--primary"));
            confirm.Add(confirmButtons);

            dialog = modal.CreateDialog("dialog--wide", "level-result");
            dialog.Add(title = HudElements.Text(string.Empty, "result-title"));
            dialog.Add(subtitle = HudElements.Text(string.Empty, "result-subtitle"));
            VisualElement starRow = HudElements.Box("stars");
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = new HudIcon { Kind = HudIconKind.Service };
                stars[i].AddToClassList("star");
                starRow.Add(stars[i]);
            }
            dialog.Add(starRow);
            dialog.Add(rewards = HudElements.Box("result-rewards"));

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("result-scroll");
            scroll.Add(criteria = HudElements.Box("result-criteria"));
            scroll.Add(HudElements.Text("LA REVISIÓN DE ANDRÉS", "detail-section"));
            scroll.Add(review = HudElements.Box("result-review"));
            scroll.Add(impact = HudElements.Box("result-impact"));
            scroll.Add(HudElements.Text("LECCIONES", "detail-section"));
            scroll.Add(lessons = HudElements.Box("result-lessons-list"));
            dialog.Add(scroll);

            VisualElement buttons = HudElements.Box("dialog-buttons");
            buttons.Add(keepButton = HudElements.Button("Seguir trabajando", () => modal.Hide(dialog), "dialog-button"));
            buttons.Add(HudElements.Button("Repetir con otros datos", () =>
            {
                modal.HideAll();
                onRetry?.Invoke();
            }, "dialog-button"));
            buttons.Add(HudElements.Button("Volver al mapa", () =>
            {
                modal.HideAll();
                onMap?.Invoke();
            }, "dialog-button", "dialog-button--primary"));
            dialog.Add(buttons);
        }

        public bool IsOpen => modal.IsOpen && !dialog.ClassListContains("hidden");

        /// <summary>Asks before delivering; <paramref name="deliver"/> validates and shows the result.</summary>
        public void Confirm(Action deliver)
        {
            onDeliver = deliver;
            modal.Show(confirm);
        }

        public void Deliver()
        {
            modal.Hide(confirm);
            onDeliver?.Invoke();
        }

        /// <param name="newBest">True when the result beats the best one saved for the level.</param>
        public void Show(LevelResult result, bool newBest)
        {
            LevelInfo level = result.Level;
            title.text = result.Passed ? $"Nivel {level.Id} superado" : $"Nivel {level.Id}: todavía no";
            subtitle.text = $"{level.Title} · paso {level.AtlasStep} del Atlas · " +
                            (result.Passed ? $"{result.Points} puntos{(newBest ? " · nuevo récord" : string.Empty)}"
                                : "sin la primera estrella el nivel no se supera");
            for (int i = 0; i < stars.Length; i++) stars[i].EnableInClassList("star--on", i < result.Stars);

            criteria.Clear();
            for (int i = 0; i < result.Criteria.Count; i++)
            {
                StarCriterion criterion = result.Criteria[i];
                // Without the first star the others are not won yet, even if their condition is met.
                bool pending = criterion.Achieved && !result.Passed && i > 0;
                VisualElement row = HudElements.Box("criterion",
                    pending ? "criterion--pending" : criterion.Achieved ? "criterion--on" : "criterion--off");
                row.Add(new HudIcon { Kind = criterion.Achieved ? HudIconKind.Check : HudIconKind.Critical });
                VisualElement texts = HudElements.Box("criterion-texts");
                texts.Add(HudElements.Text(criterion.Title, "criterion-title"));
                texts.Add(HudElements.Text(pending ? criterion.Detail + " Esta estrella cuenta cuando ganes la primera."
                    : criterion.Detail, "criterion-detail"));
                row.Add(texts);
                criteria.Add(row);
            }

            rewards.Clear();
            if (result.EarnedGem)
            {
                var gem = new HudIcon { Kind = HudIconKind.Gem }.WithClass("result-gem");
                if (ColorUtility.TryParseHtmlString(level.Gem.ColorHex, out Color color)) gem.style.color = color;
                rewards.Add(gem);
                rewards.Add(HudElements.Text($"{level.Gem.Name} ({level.Gem.Shape}) · {level.Gem.Points} puntos", "reward-text"));
            }
            if (result.ExcelBadge)
            {
                rewards.Add(new HudIcon { Kind = HudIconKind.Sheet }.WithClass("result-badge"));
                rewards.Add(HudElements.Text("Insignia «Excel de planeador»: lo calculaste con tus fórmulas", "reward-text"));
            }

            review.Clear();
            foreach (DialogueLine line in result.Debrief)
            {
                VisualElement row = HudElements.Box("review-line");
                CharacterData character = database.FindCharacter(line.CharacterId);
                if (character != null) row.Add(NpcBadge.Create(character, small: true));
                row.Add(HudElements.Text(line.Text, "review-text"));
                review.Add(row);
            }

            impact.Clear();
            var damaged = result.DamagedSkus.ToList();
            HudElements.SetVisible(impact, damaged.Count > 0);
            if (damaged.Count > 0)
            {
                impact.Add(ImpactRow("SKU", "Promedio mensual (tu serie / limpia)", "Índice estacional más afectado", true));
                foreach (SkuImpact sku in damaged)
                    impact.Add(ImpactRow(sku.Code,
                        $"{HudFormat.Number((long)Math.Round(sku.PlayerLevel))} / {HudFormat.Number((long)Math.Round(sku.CleanLevel))}",
                        $"{MonthNames[sku.Month]}: {HudFormat.Decimal(sku.PlayerIndex, 2)} / {HudFormat.Decimal(sku.CleanIndex, 2)}", false));
            }

            lessons.Clear();
            foreach (string lesson in result.Lessons) lessons.Add(HudElements.Text("• " + lesson, "lesson-line"));

            keepButton.text = result.Passed ? "Ver mi hoja" : "Seguir trabajando";
            modal.Show(dialog);
        }

        private static VisualElement ImpactRow(string code, string level, string index, bool head)
        {
            VisualElement row = HudElements.Box("impact-row");
            if (head) row.AddToClassList("impact-row--head");
            row.Add(HudElements.Text(code, "impact-cell", "impact-cell--code"));
            row.Add(HudElements.Text(level, "impact-cell"));
            row.Add(HudElements.Text(index, "impact-cell"));
            return row;
        }
    }
}
