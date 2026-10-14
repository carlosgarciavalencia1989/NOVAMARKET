using System;
using System.Linq;
using NovaMarket.Data;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Erp;
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
        private readonly VisualElement issuesSection;
        private readonly VisualElement issues;
        private readonly VisualElement goodSection;
        private readonly VisualElement goodData;
        private readonly VisualElement rewards;
        private readonly VisualElement review;
        private readonly VisualElement impact;
        private readonly VisualElement lessons;
        private readonly Button keepButton;
        private readonly ScrollView scroll;
        private readonly VisualElement warning;
        private readonly Label warningText;
        private readonly Button notYetButton;
        private readonly Button deliverButton;
        private Action onDeliver;

        /// <param name="onRetry">Plays the level again with new data.</param>
        /// <param name="onMap">Back to the world map.</param>
        /// <param name="onKeepWorking">Back to the sheet with the cells Andrés flagged highlighted.</param>
        public LevelResultView(ModalLayer modal, CampaignDatabase database, Action onRetry, Action onMap, Action onKeepWorking = null)
        {
            this.modal = modal;
            this.database = database;

            confirm = modal.CreateDialog("deliver-dialog");
            confirm.Add(HudElements.Text("¿Entregar a Andrés?", "dialog-title"));
            confirm.Add(HudElements.Text("Andrés revisa tu hoja tal como está ahora y te califica. Si no pasas, puedes seguir " +
                                         "trabajando y volver a entregar.", "dialog-text"));
            warning = HudElements.Box("review-line", "deliver-warning", "hidden");
            CharacterData andres = database.FindCharacter("andres");
            if (andres != null) warning.Add(NpcBadge.Create(andres, small: true));
            warning.Add(warningText = HudElements.Text(string.Empty, "review-text"));
            confirm.Add(warning);
            VisualElement confirmButtons = HudElements.Box("dialog-buttons");
            confirmButtons.Add(notYetButton = HudElements.Button("Todavía no", () => modal.Hide(confirm), "dialog-button"));
            confirmButtons.Add(deliverButton = HudElements.Button("Entregar", Deliver, "dialog-button", "dialog-button--primary"));
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

            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("result-scroll");
            scroll.Add(criteria = HudElements.Box("result-criteria"));
            scroll.Add(issuesSection = HudElements.Box("result-section"));
            issuesSection.Add(HudElements.Text("PROBLEMA POR PROBLEMA", "detail-section"));
            issuesSection.Add(issues = HudElements.Box("issue-table"));
            scroll.Add(goodSection = HudElements.Box("result-section"));
            goodSection.Add(HudElements.Text("CELDAS QUE ESTABAN BIEN Y TOCASTE", "detail-section"));
            goodSection.Add(goodData = HudElements.Box("good-list"));
            scroll.Add(HudElements.Text("LA REVISIÓN DE ANDRÉS", "detail-section"));
            scroll.Add(review = HudElements.Box("result-review"));
            scroll.Add(impact = HudElements.Box("result-impact"));
            scroll.Add(HudElements.Text("LECCIONES", "detail-section"));
            scroll.Add(lessons = HudElements.Box("result-lessons-list"));
            dialog.Add(scroll);

            VisualElement buttons = HudElements.Box("dialog-buttons");
            buttons.Add(keepButton = HudElements.Button("Seguir trabajando", () =>
            {
                modal.Hide(dialog);
                onKeepWorking?.Invoke();
            }, "dialog-button"));
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

        /// <summary>
        /// Asks before delivering; <paramref name="deliver"/> validates and shows the result. With a
        /// <paramref name="pendingWarning"/> (marked problems not corrected yet) Andrés says it first.
        /// </summary>
        public void Confirm(Action deliver, string pendingWarning = null)
        {
            onDeliver = deliver;
            bool warn = !string.IsNullOrEmpty(pendingWarning);
            HudElements.SetVisible(warning, warn);
            warningText.text = warn ? pendingWarning + " ¿Quieres entregar igual?" : string.Empty;
            notYetButton.text = warn ? "Volver a corregir" : "Todavía no";
            deliverButton.text = warn ? "Entregar igual" : "Entregar";
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

            ShowCleaning(result.Cleaning);

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
            scroll.scrollOffset = Vector2.zero; // every result starts at the stars, not where the last one was left
            modal.Show(dialog);
        }

        /// <summary>Level 1.2: one row per problem and the good cells marked or changed by mistake.</summary>
        private void ShowCleaning(CleaningReview review)
        {
            issues.Clear();
            goodData.Clear();
            HudElements.SetVisible(issuesSection, review != null);
            HudElements.SetVisible(goodSection, review != null && review.GoodData.Count > 0);
            if (review == null) return;

            VisualElement head = HudElements.Box("issue-row", "issue-row--head");
            head.Add(HudElements.Text("Dónde", "issue-cell", "issue-cell--where"));
            head.Add(HudElements.Text("Tipo de problema", "issue-cell", "issue-cell--kind"));
            head.Add(HudElements.Text("Acción", "issue-cell", "issue-cell--action"));
            head.Add(HudElements.Text("Valor en «Serie limpia»", "issue-cell", "issue-cell--value"));
            issues.Add(head);

            foreach (IssueReview issue in review.Issues)
            {
                VisualElement block = HudElements.Box("issue-block", issue.IsCorrect ? "issue-block--ok" : "issue-block--wrong");
                VisualElement row = HudElements.Box("issue-row");
                VisualElement where = HudElements.Box("issue-cell", "issue-cell--where", "issue-where");
                where.Add(new HudIcon { Kind = issue.IsCorrect ? HudIconKind.Check : HudIconKind.Critical });
                where.Add(HudElements.Text(issue.Where, "issue-where-text"));
                row.Add(where);
                row.Add(HudElements.Text(DataIssue.Name(issue.Issue.Kind), "issue-cell", "issue-cell--kind"));
                string chosen = issue.ChosenAction.HasValue ? DataIssue.Name(issue.ChosenAction.Value) : "sin marcar";
                row.Add(Pair("Tuya", chosen, "Correcta", DataIssue.Name(issue.ExpectedAction),
                    issue.ChosenAction == issue.ExpectedAction, "issue-cell--action"));
                row.Add(Pair("Tuyo", issue.PlayerValue, "Correcto", issue.ExpectedValue, issue.WrongMonths.Count == 0, "issue-cell--value"));
                block.Add(row);
                block.Add(HudElements.Text(issue.Message, "issue-message"));
                if (!issue.IsCorrect)
                    block.Add(HudElements.Text($"Atlas: {issue.Issue.Explanation} ({issue.Issue.AtlasStep})", "issue-atlas"));
                issues.Add(block);
            }

            foreach (GoodDataReview good in review.GoodData)
            {
                VisualElement row = HudElements.Box("good-row");
                row.Add(new HudIcon { Kind = HudIconKind.Critical });
                row.Add(HudElements.Text(good.Message, "good-text"));
                goodData.Add(row);
            }
        }

        /// <summary>"Tuyo: 0" over "Correcto: 4.695" (only the first line when both are the same).</summary>
        private static VisualElement Pair(string mineLabel, string mine, string rightLabel, string right, bool same, string column)
        {
            VisualElement cell = HudElements.Box("issue-cell", column);
            cell.Add(HudElements.Text($"{mineLabel}: {mine}", "issue-mine", same ? "issue-mine--ok" : "issue-mine--wrong"));
            if (!same) cell.Add(HudElements.Text($"{rightLabel}: {right}", "issue-right"));
            return cell;
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
