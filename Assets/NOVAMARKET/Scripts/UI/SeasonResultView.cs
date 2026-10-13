using System;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Planning;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// End of the level: stars, gem, game-over explanation (failed indicator and Atlas step),
    /// the player's numbers against the Atlas reference of the same seed, and the lessons.
    /// </summary>
    public sealed class SeasonResultView
    {
        private readonly ModalLayer modal;
        private readonly VisualElement dialog;
        private readonly Label title;
        private readonly Label subtitle;
        private readonly HudIcon[] stars = new HudIcon[3];
        private readonly VisualElement gemRow;
        private readonly Label gemText;
        private readonly VisualElement alert;
        private readonly Label alertCaption;
        private readonly Label alertText;
        private readonly VisualElement table;
        private readonly VisualElement lessons;
        private readonly Button replayButton;
        private readonly Button closeButton;

        /// <param name="onReplay">Starts a new season with a new seed.</param>
        public SeasonResultView(ModalLayer modal, Action onReplay)
        {
            this.modal = modal;
            dialog = modal.CreateDialog("dialog--wide");
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

            gemRow = HudElements.Box("gem-row");
            gemRow.Add(new HudIcon { Kind = HudIconKind.Gem });
            gemRow.Add(gemText = HudElements.Text(string.Empty, "gem-text"));
            dialog.Add(gemRow);

            alert = HudElements.Box("note-box", "note-box--alert");
            alert.Add(alertCaption = HudElements.Text(string.Empty, "note-caption"));
            alert.Add(alertText = HudElements.Text(string.Empty, "note-text"));
            dialog.Add(alert);

            VisualElement columns = HudElements.Box("result-columns");
            columns.Add(table = HudElements.Box("result-table"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("result-lessons");
            scroll.Add(lessons = new VisualElement());
            columns.Add(scroll);
            dialog.Add(columns);

            dialog.Add(HudElements.Text(
                "Estrellas: 1 con servicio desde 90 %; 2 con servicio desde 95 % y costo hasta 110 % del Atlas; " +
                "3 con servicio desde 95 % y costo igual o menor que el del Atlas.", "result-rules"));

            VisualElement buttons = HudElements.Box("dialog-buttons");
            buttons.Add(closeButton = HudElements.Button("Ver el CEDI", () => modal.Hide(dialog), "dialog-button"));
            buttons.Add(replayButton = HudElements.Button(string.Empty, () =>
            {
                modal.Hide(dialog);
                onReplay?.Invoke();
            }, "dialog-button", "dialog-button--primary"));
            dialog.Add(buttons);
        }

        public void Show(SeasonResult result)
        {
            bool gameOver = result.Outcome == PlanningOutcome.GameOver;
            title.text = gameOver ? "Game over"
                : result.Passed ? "¡Temporada completada!"
                : "Nivel no superado";
            subtitle.text = gameOver
                ? $"El nivel terminó en la semana {result.GameOver.Week} · semilla {result.Seed}"
                : $"Temporada de fin de año · semilla {result.Seed}";
            for (int i = 0; i < stars.Length; i++) stars[i].EnableInClassList("star--on", i < result.Stars);

            HudElements.SetVisible(gemRow, result.EarnedGem);
            gemText.text = $"Ganaste el {SeasonResult.GemName} · +{HudFormat.Number(result.Points)} puntos";

            HudElements.SetVisible(alert, !result.Passed);
            if (gameOver)
            {
                alertCaption.text = $"INDICADOR QUE FALLÓ: {result.GameOver.Indicator.ToUpperInvariant()} ({result.GameOver.Value})";
                alertText.text = $"{result.GameOver.Explanation}\nPaso del Atlas: {result.GameOver.AtlasStep}";
            }
            else if (!result.Passed)
            {
                alertCaption.text = $"INDICADOR QUE FALLÓ: NIVEL DE SERVICIO ({HudFormat.Percent(result.ServiceLevel)})";
                alertText.text = "Necesitas al menos 90 % de servicio para ganar una estrella. Se perdieron demasiadas " +
                                 "ventas durante la temporada.\nRepasa: stock de seguridad SS = Z × desviación × √(L+1) y el nivel " +
                                 "objetivo S = F + SS, ajustando el plan con los avisos de los personajes.";
            }

            FillTable(result);
            FillLessons(result);

            replayButton.text = result.Passed ? "Jugar otra temporada" : "Repetir con semilla nueva";
            HudElements.SetVisible(closeButton, true);
            modal.Show(dialog);
        }

        private void FillTable(SeasonResult result)
        {
            table.Clear();
            table.Add(Row("", "TÚ", "ATLAS", "result-row--head"));
            table.Add(Row("Nivel de servicio", HudFormat.Percent(result.ServiceLevel), HudFormat.Percent(result.ReferenceServiceLevel)));
            CostBreakdown mine = result.Costs, atlas = result.ReferenceCosts;
            table.Add(Row("Mantener inventario", Pesos(mine.Holding), Pesos(atlas.Holding)));
            table.Add(Row("Desborde (cajas en el piso)", Pesos(mine.Overflow), Pesos(atlas.Overflow)));
            table.Add(Row("Costo de pedidos", Pesos(mine.Ordering), Pesos(atlas.Ordering)));
            table.Add(Row("Venta perdida", Pesos(mine.LostSales), Pesos(atlas.LostSales)));
            table.Add(Row("Flete aéreo", Pesos(mine.AirFreight), Pesos(atlas.AirFreight)));
            table.Add(Row("Liquidación", Pesos(mine.Liquidation), Pesos(atlas.Liquidation)));
            table.Add(Row("Costo total", Pesos(mine.Total), Pesos(atlas.Total), "result-row--total"));
            if (result.Outcome == PlanningOutcome.GameOver)
                table.Add(HudElements.Text("Tus costos son hasta la semana del game over; los del Atlas, de toda la temporada.",
                    "result-rules"));
        }

        private void FillLessons(SeasonResult result)
        {
            lessons.Clear();
            lessons.Add(HudElements.Text("LECCIONES", "planning-head-label"));
            foreach (PlanningLesson lesson in result.Lessons)
            {
                VisualElement item = HudElements.Box("lesson");
                if (lesson.IsPositive) item.AddToClassList("lesson--positive");
                item.Add(HudElements.Text(lesson.Title, "lesson-title"));
                item.Add(HudElements.Text(lesson.Text, "lesson-text"));
                item.Add(HudElements.Text("Atlas: " + lesson.AtlasStep, "lesson-step"));
                lessons.Add(item);
            }
        }

        private static VisualElement Row(string label, string mine, string atlas, string extraClass = null)
        {
            VisualElement row = HudElements.Box("result-row");
            if (extraClass != null) row.AddToClassList(extraClass);
            row.Add(HudElements.Text(label, "result-cell-label"));
            row.Add(HudElements.Text(mine, "result-cell"));
            row.Add(HudElements.Text(atlas, "result-cell"));
            return row;
        }

        private static string Pesos(double value) => HudFormat.Pesos((long)Math.Round(value));
    }
}
