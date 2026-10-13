using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// A character's message about a season event: what happened, an Atlas hint and the options
    /// the player can choose. Most events only have one option (acknowledge).
    /// </summary>
    public sealed class EventDialog
    {
        internal EventDialog(ScheduledEvent scheduled, string title, string message, string atlasHint,
            IReadOnlyList<string> options)
        {
            Event = scheduled;
            Title = title;
            Message = message;
            AtlasHint = atlasHint;
            Options = options;
        }

        public ScheduledEvent Event { get; }
        public SeasonEventKind Kind => Event.Kind;
        public int Week => Event.Week;
        public PlanningCharacter Character => Event.Character;
        public CharacterMood Mood => Event.Mood;
        public string Title { get; }
        public string Message { get; }
        public string AtlasHint { get; }

        /// <summary>Option labels; index 0 is the default the Atlas reference always picks.</summary>
        public IReadOnlyList<string> Options { get; }

        /// <summary>The answer, once given.</summary>
        public int? ChosenOption { get; internal set; }
    }

    /// <summary>Events part: applies the events of each week and writes the characters' lines.</summary>
    public sealed partial class PlanningSimulation
    {
        private const double LostSalesCommentShare = 0.05;
        private const double CoverageCommentWeeks = 5.0;

        private EventDialog ApplyEventsOfWeek()
        {
            ScheduledEvent scheduled = Season.EventAt(Week);
            if (scheduled == null) return null;
            announced.Add(scheduled);
            PendingEvent = CreateDialog(scheduled);
            return PendingEvent;
        }

        private EventDialog CreateDialog(ScheduledEvent scheduled)
        {
            const string Ok = "Entendido";
            switch (scheduled.Kind)
            {
                case SeasonEventKind.DamagedPallet:
                {
                    int lost = TakeShare(scheduled.Sku, PlanningSeason.DamagedShare);
                    return new EventDialog(scheduled, "Estiba dañada",
                        $"Se cayó una estiba de {Skus[scheduled.Sku].Name} en el pasillo de racks. Perdimos " +
                        $"{lost} cajas, el 15 % de lo que había en bodega. Ahora quedan {onHand[scheduled.Sku]}.",
                        "Tu posición de inventario cambió. El Sugerido del Atlas la recalcula con lo que de verdad hay: " +
                        "revísalo antes de pedir crema.",
                        new[] { "Entendido, reviso la crema" });
                }

                case SeasonEventKind.Promotion:
                {
                    int first = scheduled.FromWeek, second = scheduled.ToWeek;
                    return new EventDialog(scheduled, "Promoción 2×1 de labial",
                        $"¡Mercadeo aprobó un 2×1 del {Skus[scheduled.Sku].Name} en las semanas {first} y {second}! " +
                        "Esperamos vender 1,9 veces el plan en esas dos semanas. El pronóstico estadístico no sabe nada " +
                        "de la promo.",
                        $"Con lead time de 1 semana, lo que pidas en la semana {first - 1} llega justo para la semana " +
                        $"{first}, y lo de la semana {first} para la {second}. Suma al sugerido unas 0,9 veces el plan " +
                        "de cada semana de promo.",
                        new[] { "¡Me preparo para la promo!" });
                }

                case SeasonEventKind.ShipDelay:
                {
                    int sku = scheduled.Sku;
                    foreach (TransitLot lot in transit)
                    {
                        if (lot.Sku != sku) continue;
                        lot.ArrivalWeek += 2;
                        lot.DelayWeeks += 2;
                    }
                    int delayed = InTransitBoxes(sku);
                    const string hint = "Con el retraso, el lead time real pasa de 3 a 5 semanas. El stock de seguridad " +
                                        "debe calcularse con el lead time real: SS = Z × desviación × √(L+1).";
                    if (delayed == 0)
                        return new EventDialog(scheduled, "Retraso del barco",
                            "El barco del perfume quedó retenido en Cartagena dos semanas. Por suerte hoy no tienes " +
                            "perfume en camino, pero piensa en el lead time de tus próximos pedidos.",
                            hint, new[] { Ok });

                    int half = PlanningRules.Round(delayed / 2.0);
                    return new EventDialog(scheduled, "Retraso del barco",
                        $"El barco con el {Skus[sku].Name} quedó retenido en Cartagena. Todo lo que viene en camino " +
                        $"({delayed} cajas) llegará 2 semanas tarde. Puedo traer la mitad ({half} cajas) por avión: " +
                        $"llega la semana {Week + 1} y cuesta {HudFormat.Pesos(PlanningRules.AirFreightCost)}.",
                        hint,
                        new[] { "Esperar el barco", $"Traer la mitad por avión ({HudFormat.Pesos(PlanningRules.AirFreightCost)})" });
                }

                case SeasonEventKind.CycleCountGap:
                {
                    int before = onHand[scheduled.Sku];
                    int lost = TakeShare(scheduled.Sku, PlanningSeason.CycleCountShare);
                    return new EventDialog(scheduled, "Descuadre en el conteo cíclico",
                        $"El conteo cíclico del {Skus[scheduled.Sku].Name} no cuadra: el sistema decía {before} cajas " +
                        $"y en el rack solo hay {onHand[scheduled.Sku]}. Ajusté el inventario: faltan {lost} cajas (25 %).",
                        "El pedido se calcula sobre la posición real. Contar seguido evita pedir de menos por " +
                        "confiar en el sistema.",
                        new[] { "Gracias, Sofía" });
                }

                case SeasonEventKind.NewCustomer:
                    return new EventDialog(scheduled, "Cliente nuevo aprobado en S&OP",
                        $"En la reunión de S&OP aprobamos una cadena de droguerías nueva: la demanda de la " +
                        $"{Skus[scheduled.Sku].Name} sube 35 % desde la semana {scheduled.FromWeek} hasta la " +
                        $"{scheduled.ToWeek}.",
                        "Los acuerdos de S&OP no están en el pronóstico estadístico: súmalos al plan a mano. Con lead " +
                        $"time de 2 semanas, lo que pidas en la semana {Week} llega en la {Week + 2}.",
                        new[] { "Lo sumo al plan" });

                case SeasonEventKind.PurchaseCap:
                    PurchaseCap = Season.PurchaseCap;
                    return new EventDialog(scheduled, "Tope de compras",
                        "La caja está apretada este mes. Esta semana el valor total de los pedidos no puede pasar de " +
                        $"{HudFormat.Pesos(Season.PurchaseCap)}.",
                        "Si el sugerido supera el tope, prioriza los SKU de lead time largo y mayor riesgo de venta " +
                        "perdida: lo de lead time corto se recupera la semana siguiente.",
                        new[] { "Entendido, cuido el presupuesto" });

                case SeasonEventKind.CompetitorStockout:
                    return new EventDialog(scheduled, "La competencia se quedó sin stock",
                        $"Nuestro competidor se quedó sin sets de regalo para diciembre. Esperamos vender 1,5 veces el " +
                        $"plan del {Skus[scheduled.Sku].Name} en las semanas {scheduled.FromWeek} a {scheduled.ToWeek}.",
                        $"El set tiene lead time de 2 semanas: lo que pidas hoy llega en la semana {Week + 2}. Ajusta el " +
                        "plan de esas semanas y vuelve a calcular el pedido.",
                        new[] { "¡A aprovecharlo!" });

                default:
                {
                    var allowed = new List<string>();
                    for (int sku = 0; sku < Skus.Count; sku++)
                        allowed.Add($"{Skus[sku].Code}: {HudFormat.Decimal(PlanMean(sku, PlanningRules.ClosingWeek), 1)} " +
                                    $"cajas/semana, máximo {ClosingAllowance(sku)}");
                    return new EventDialog(scheduled, "Aviso de cierre de temporada",
                        "Así cerramos la temporada. En enero la demanda baja: " + string.Join("; ", allowed) + ". " +
                        "Al cierre, todo lo que tengas (en bodega + en tránsito) por encima de 3 semanas de enero " +
                        "se liquida al 10 % del costo.",
                        "Después del pico, baja los pedidos: desactiva el DAF (factor de ajuste de demanda) y no " +
                        "pidas para diciembre lo que se va a vender en enero.",
                        new[] { Ok });
                }
            }
        }

        /// <summary>Removes a share of a SKU's stock (damage, count gap) and returns the boxes lost.</summary>
        private int TakeShare(int sku, double share)
        {
            int lost = Math.Min(onHand[sku], PlanningRules.Round(onHand[sku] * share));
            onHand[sku] -= lost;
            return lost;
        }

        /// <summary>
        /// One character comments the week: lost sales above 5 % (Valeria), boxes on the floor
        /// (Julián), more than 5 weeks of cover (Marcela) or a clean week (Andrés).
        /// </summary>
        private WeekComment CommentOn(WeekReport report)
        {
            if (report.Demand > 0 && (double)report.Lost / report.Demand > LostSalesCommentShare)
            {
                SkuWeekResult worst = report.Skus[0];
                foreach (SkuWeekResult result in report.Skus)
                    if (result.Lost > worst.Lost) worst = result;
                return new WeekComment(PlanningCharacter.Valeria, CharacterMood.Angry,
                    $"Perdimos {report.Lost} cajas de venta esta semana ({HudFormat.Percent((double)report.Lost / report.Demand)} " +
                    $"de la demanda). Lo que más faltó: {Skus[worst.Sku].Name}, {worst.Lost} cajas. Las tiendas nos reclaman.");
            }

            if (report.HasOverflow)
            {
                SkuWeekResult worst = report.Skus[0];
                foreach (SkuWeekResult result in report.Skus)
                    if (result.OverflowBoxes > worst.OverflowBoxes) worst = result;
                string streak = overflowStreak > 1
                    ? $" Llevamos {overflowStreak} semanas así; a la tercera seguida no respondo."
                    : string.Empty;
                return new WeekComment(PlanningCharacter.Julian, CharacterMood.Angry,
                    $"Tengo {worst.OverflowBoxes} cajas de {Skus[worst.Sku].Name} en el piso: no caben en el rack. " +
                    "Cada caja en el piso cuesta el triple." + streak);
            }

            int coveredSku = -1;
            double coverage = 0;
            for (int sku = 0; sku < Skus.Count; sku++)
            {
                double mean = PlanMean(sku, Math.Min(Week, PlanningRules.ClosingWeek));
                double weeks = mean > 0 ? onHand[sku] / mean : 0;
                if (weeks > CoverageCommentWeeks && weeks > coverage)
                {
                    coverage = weeks;
                    coveredSku = sku;
                }
            }
            if (coveredSku >= 0)
                return new WeekComment(PlanningCharacter.Marcela, CharacterMood.Worried,
                    $"Tenemos {HudFormat.Decimal(coverage, 1)} semanas de cobertura de {Skus[coveredSku].Name}. " +
                    "Ese inventario es plata quieta en la bodega.");

            return new WeekComment(PlanningCharacter.Andres, CharacterMood.Happy,
                $"Semana limpia: vendimos {report.Sold} de {report.Demand} cajas, sin desborde ni exceso. ¡Así se planea!");
        }
    }
}
