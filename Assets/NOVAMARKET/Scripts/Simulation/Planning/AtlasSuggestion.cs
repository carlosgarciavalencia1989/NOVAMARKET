using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// "Sugerido del Atlas": weekly periodic review with an order-up-to level (section 5 of the
    /// spec). F = plan of weeks w … w+L, σ = CV × mean[w], SS = Z × σ × √(L+1), S = F + SS,
    /// order = ceil(S − (on hand + in transit)), raised to the MOQ when it is positive but smaller.
    /// Keeps every intermediate value so the game can show the calculation step by step.
    /// </summary>
    public sealed class AtlasSuggestion
    {
        private readonly double[] planWeeks;

        private AtlasSuggestion(PlanningSku sku, int week, double[] planWeeks, double sigma, int onHand, int inTransit)
        {
            Sku = sku;
            Week = week;
            this.planWeeks = planWeeks;
            OnHand = onHand;
            InTransit = inTransit;
            Sigma = sigma;

            foreach (double mean in planWeeks) Forecast += mean;
            SafetyStock = PlanningRules.ServiceZ * Sigma * Math.Sqrt(ProtectionWeeks);
            TargetLevel = Forecast + SafetyStock;
            // Rounded first so floating-point noise (51.0000001) does not add a whole box.
            RawOrder = Math.Max(0, (int)Math.Ceiling(Math.Round(TargetLevel - Position, 6)));
            RaisedToMinimum = RawOrder > 0 && RawOrder < sku.MinimumOrder;
            Order = RaisedToMinimum ? sku.MinimumOrder : RawOrder;
        }

        public PlanningSku Sku { get; }

        /// <summary>Week the order is placed in (w).</summary>
        public int Week { get; }

        /// <summary>L: weeks until the order arrives.</summary>
        public int LeadTime => Sku.LeadTimeWeeks;

        /// <summary>L + 1: the order must cover until the next order arrives.</summary>
        public int ProtectionWeeks => LeadTime + 1;

        /// <summary>Plan of weeks w … w+L, in boxes (index 0 = week w).</summary>
        public IReadOnlyList<double> PlanWeeks => planWeeks;

        /// <summary>F: plan demand over the protection interval.</summary>
        public double Forecast { get; }

        /// <summary>σ: weekly standard deviation of demand (CV × mean of week w).</summary>
        public double Sigma { get; }
        public double SafetyStock { get; }

        /// <summary>S = F + SS, the order-up-to level.</summary>
        public double TargetLevel { get; }

        public int OnHand { get; }
        public int InTransit { get; }
        public int Position => OnHand + InTransit;

        /// <summary>ceil(S − position), never negative.</summary>
        public int RawOrder { get; }
        public bool RaisedToMinimum { get; }

        /// <summary>The suggested order in boxes.</summary>
        public int Order { get; }

        /// <summary>Atlas suggestion from the statistical plan of a SKU (no events).</summary>
        public static AtlasSuggestion Compute(DemandPlan plan, int week, int onHand, int inTransit) =>
            Compute(plan.Sku, plan.Mean, week, onHand, inTransit);

        /// <summary>
        /// Atlas suggestion from any weekly plan, e.g. one the player adjusted for announced events.
        /// </summary>
        public static AtlasSuggestion Compute(PlanningSku sku, Func<int, double> weeklyPlan, int week, int onHand,
            int inTransit)
        {
            if (sku == null) throw new ArgumentNullException(nameof(sku));
            if (weeklyPlan == null) throw new ArgumentNullException(nameof(weeklyPlan));
            var weeks = new double[sku.LeadTimeWeeks + 1];
            for (int i = 0; i < weeks.Length; i++) weeks[i] = weeklyPlan(week + i);
            double sigma = sku.VariationCoefficient * weeks[0];
            return new AtlasSuggestion(sku, week, weeks, sigma, onHand, inTransit);
        }

        /// <summary>The calculation as numbered Spanish sentences for the player.</summary>
        public IReadOnlyList<string> Explain()
        {
            string Boxes(double value) => HudFormat.Decimal(value, 1);
            int lastWeek = Week + LeadTime;
            var terms = new List<string>();
            foreach (double mean in planWeeks) terms.Add(Boxes(mean));

            var steps = new List<string>
            {
                $"Plan estadístico de las semanas {Week} a {lastWeek} (L + 1 = {ProtectionWeeks} semanas, " +
                $"porque lo que pidas hoy llega en la semana {Week + LeadTime}): " +
                $"F = {string.Join(" + ", terms)} = {Boxes(Forecast)} cajas.",
                $"Desviación semanal de la demanda = CV × plan de la semana {Week} = " +
                $"{HudFormat.Decimal(Sku.VariationCoefficient, 2)} × {Boxes(planWeeks[0])} = {Boxes(Sigma)} cajas.",
                $"Stock de seguridad SS = Z × desviación × √(L + 1) = {HudFormat.Decimal(PlanningRules.ServiceZ, 2)} × " +
                $"{Boxes(Sigma)} × √{ProtectionWeeks} = {Boxes(SafetyStock)} cajas (Z = 1,65 da 95 % de servicio).",
                $"Nivel objetivo S = F + SS = {Boxes(Forecast)} + {Boxes(SafetyStock)} = {Boxes(TargetLevel)} cajas.",
                $"Posición = en bodega + en tránsito = {HudFormat.Number(OnHand)} + {HudFormat.Number(InTransit)} = " +
                $"{HudFormat.Number(Position)} cajas."
            };

            if (RawOrder == 0)
                steps.Add($"Pedido = S − posición = {Boxes(TargetLevel - Position)}: la posición ya cubre el nivel objetivo, " +
                          "no hace falta pedir.");
            else
                steps.Add($"Pedido = S − posición = {Boxes(TargetLevel - Position)}, redondeado hacia arriba = " +
                          $"{HudFormat.Number(RawOrder)} cajas.");

            if (RaisedToMinimum)
                steps.Add($"Es menor que el pedido mínimo (MOQ) de {HudFormat.Number(Sku.MinimumOrder)} cajas: " +
                          $"se sube a {HudFormat.Number(Order)}.");
            return steps;
        }
    }
}
