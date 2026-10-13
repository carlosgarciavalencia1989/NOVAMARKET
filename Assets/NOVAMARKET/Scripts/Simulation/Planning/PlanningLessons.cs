using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// Automatic end-of-level lessons: looks at where the player lost sales or money (promo,
    /// ship delay, new customer, overflow, liquidation) and compares the season with the Atlas
    /// reference.
    /// </summary>
    public static class PlanningLessons
    {
        /// <summary>A window counts as a problem when more than this share of its demand was lost.</summary>
        public const double LostShareThreshold = 0.05;

        public static IReadOnlyList<PlanningLesson> Build(PlanningSimulation player, PlanningSimulation reference)
        {
            var lessons = new List<PlanningLesson>();
            PlanningSeason season = player.Season;

            foreach (ScheduledEvent scheduled in season.Events)
            {
                switch (scheduled.Kind)
                {
                    case SeasonEventKind.Promotion when LostShare(player, scheduled.Sku, scheduled.FromWeek, scheduled.ToWeek, out int lost):
                        lessons.Add(new PlanningLesson("Ventas perdidas de labial en la promo",
                            $"Perdiste {lost} cajas de labial en las semanas {scheduled.FromWeek} y {scheduled.ToWeek} de la " +
                            "promoción 2×1. Con lead time de 1 semana, había que pedir más en la semana anterior a cada " +
                            "semana de promo.",
                            "Ajuste del plan por eventos: plan de la semana × factor de la promo, antes de calcular S."));
                        break;
                    case SeasonEventKind.ShipDelay when LostShare(player, scheduled.Sku, scheduled.Week, PlanningRules.SeasonWeeks, out int lost):
                        lessons.Add(new PlanningLesson("Ventas perdidas de perfume después del retraso",
                            $"Desde el retraso del barco perdiste {lost} cajas de perfume. El stock de seguridad debe " +
                            "calcularse con el lead time real (5 semanas con el retraso), no con el del contrato.",
                            "Stock de seguridad: SS = Z × σ × √(L+1) con el lead time real."));
                        break;
                    case SeasonEventKind.NewCustomer when LostShare(player, scheduled.Sku, scheduled.FromWeek, scheduled.ToWeek, out int lost):
                        lessons.Add(new PlanningLesson("Ventas perdidas de crema por el cliente nuevo",
                            $"Perdiste {lost} cajas de crema entre las semanas {scheduled.FromWeek} y {scheduled.ToWeek}. " +
                            "Los acuerdos de S&OP no llegan solos al pronóstico: se suman al plan a mano.",
                            "S&OP: el plan de demanda acordado reemplaza al estadístico."));
                        break;
                    case SeasonEventKind.CompetitorStockout when LostShare(player, scheduled.Sku, scheduled.FromWeek, scheduled.ToWeek, out int lost):
                        lessons.Add(new PlanningLesson("Ventas perdidas del set en diciembre",
                            $"La competencia se quedó sin stock y perdiste {lost} cajas del set regalo. La información " +
                            "del mercado también ajusta el plan.",
                            "Gestión de la demanda: inteligencia comercial sobre el pronóstico."));
                        break;
                }
            }

            if (player.Costs.Overflow > 0)
                lessons.Add(new PlanningLesson("Costo de desborde",
                    $"Pagaste {HudFormat.Pesos((long)player.Costs.Overflow)} por cajas en el piso. El tamaño del lote " +
                    "también depende del espacio: no pidas por encima del nivel objetivo ni más de lo que cabe en el rack.",
                    "EOQ = √(2·D·S/H) limitado por la capacidad de almacenamiento."));

            if (player.Costs.Liquidation > 0)
            {
                int boxes = 0;
                for (int sku = 0; sku < player.Skus.Count; sku++) boxes += player.LiquidatedBoxes(sku);
                lessons.Add(new PlanningLesson("Costo de liquidación",
                    $"Al cierre liquidaste {boxes} cajas ({HudFormat.Pesos((long)player.Costs.Liquidation)}). Después del " +
                    "pico de diciembre había que bajar los pedidos: desactivar el DAF (factor de ajuste de demanda).",
                    "Plan de enero: S = F + SS con la demanda baja, no con la del pico."));
            }

            if (player.Outcome == PlanningOutcome.Completed)
                lessons.Add(Comparison(player, reference));
            else
                lessons.Add(new PlanningLesson("Lo que logró la política del Atlas",
                    $"Con la misma semilla, la política del Atlas terminó la temporada con " +
                    $"{HudFormat.Percent(reference.ServiceLevel)} de servicio y un costo de " +
                    $"{HudFormat.Pesos((long)reference.Costs.Total)}. Úsala de base y ajústala con los eventos.",
                    "Sugerido del Atlas: revisión periódica semanal con nivel objetivo."));
            return lessons;
        }

        private static PlanningLesson Comparison(PlanningSimulation player, PlanningSimulation reference)
        {
            double difference = reference.Costs.Total - player.Costs.Total;
            string service = $"Tu servicio: {HudFormat.Percent(player.ServiceLevel)}; el del Atlas: " +
                             $"{HudFormat.Percent(reference.ServiceLevel)}.";
            if (difference >= 0)
                return new PlanningLesson("Le ganaste a la política del Atlas",
                    $"Gastaste {HudFormat.Pesos((long)difference)} menos que la política del Atlas. {service} " +
                    "Escuchar a los personajes y ajustar el sugerido paga.",
                    "El sugerido es el punto de partida; los eventos son tu decisión.", isPositive: true);
            return new PlanningLesson("La política del Atlas gastó menos",
                $"La política del Atlas gastó {HudFormat.Pesos((long)-difference)} menos que tú. {service} Revisa en qué " +
                "semanas te alejaste del sugerido sin un evento que lo justificara.",
                "Sugerido del Atlas: revisión periódica semanal con nivel objetivo.");
        }

        /// <summary>True when more than 5 % of a SKU's demand in the weeks was lost; returns the lost boxes.</summary>
        private static bool LostShare(PlanningSimulation simulation, int sku, int fromWeek, int toWeek, out int lost)
        {
            int demand = 0;
            lost = 0;
            foreach (WeekReport report in simulation.History)
            {
                if (report.Week < fromWeek || report.Week > toWeek) continue;
                demand += report.Skus[sku].Demand;
                lost += report.Skus[sku].Lost;
            }
            return demand > 0 && (double)lost / demand > LostShareThreshold;
        }
    }
}
