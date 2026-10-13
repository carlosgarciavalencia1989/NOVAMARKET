using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>What happens in a season event (section 6 of the spec).</summary>
    public enum SeasonEventKind
    {
        DamagedPallet,
        Promotion,
        ShipDelay,
        CycleCountGap,
        NewCustomer,
        PurchaseCap,
        CompetitorStockout,
        ClosingNotice
    }

    /// <summary>
    /// An event planned for a week of the season. Demand events also carry the SKU and the weeks
    /// whose demand they multiply; the statistical plan does not know about them.
    /// </summary>
    public sealed class ScheduledEvent
    {
        public ScheduledEvent(SeasonEventKind kind, int week, PlanningCharacter character, CharacterMood mood,
            int sku = -1, int fromWeek = 0, int toWeek = -1, double factor = 1.0)
        {
            Kind = kind;
            Week = week;
            Character = character;
            Mood = mood;
            Sku = sku;
            FromWeek = fromWeek;
            ToWeek = toWeek;
            DemandFactor = factor;
        }

        public SeasonEventKind Kind { get; }

        /// <summary>Week at whose start the event is announced and applied.</summary>
        public int Week { get; }
        public PlanningCharacter Character { get; }
        public CharacterMood Mood { get; }

        /// <summary>Catalogue index of the SKU it affects; -1 when it affects no single SKU.</summary>
        public int Sku { get; }

        /// <summary>First and last week whose demand is multiplied by <see cref="DemandFactor"/>.</summary>
        public int FromWeek { get; }
        public int ToWeek { get; }
        public double DemandFactor { get; }

        public bool ChangesDemand => DemandFactor != 1.0;

        public bool Affects(int sku, int week) => ChangesDemand && sku == Sku && week >= FromWeek && week <= ToWeek;
    }

    /// <summary>
    /// Everything random about one season, fixed by its seed: the week shift of the events (j),
    /// the weekly demand noise and the event multipliers. The player and the Atlas reference
    /// play the same season when they use the same seed.
    /// </summary>
    public sealed class PlanningSeason
    {
        public const double PromotionFactor = 1.9;
        public const double NewCustomerFactor = 1.35;
        public const double CompetitorFactor = 1.5;
        public const double DamagedShare = 0.15;
        public const double CycleCountShare = 0.25;
        public const double PurchaseCapShare = 0.55;

        private readonly double[,] noise;
        private readonly double[,] multipliers;
        private readonly List<ScheduledEvent> events = new List<ScheduledEvent>();

        public PlanningSeason(int seed) : this(seed, PlanningCatalog.Skus) { }

        public PlanningSeason(int seed, IReadOnlyList<PlanningSku> skus)
        {
            Seed = seed;
            Skus = skus ?? throw new ArgumentNullException(nameof(skus));
            var plans = new DemandPlan[skus.Count];
            for (int i = 0; i < skus.Count; i++) plans[i] = new DemandPlan(skus[i]);
            Plans = plans;

            var random = new Random(seed);
            Shift = random.Next(2);
            noise = new double[skus.Count, PlanningRules.ClosingWeek + 1];
            for (int sku = 0; sku < skus.Count; sku++)
            for (int week = 1; week <= PlanningRules.ClosingWeek; week++)
                noise[sku, week] = ClippedNormal(random);

            ScheduleEvents();
            multipliers = new double[skus.Count, PlanningRules.ClosingWeek + 1];
            for (int sku = 0; sku < skus.Count; sku++)
            for (int week = 1; week <= PlanningRules.ClosingWeek; week++)
            {
                double factor = 1.0;
                foreach (ScheduledEvent scheduled in events)
                    if (scheduled.Affects(sku, week)) factor *= scheduled.DemandFactor;
                multipliers[sku, week] = factor;
            }

            double capBase = 0;
            for (int sku = 0; sku < skus.Count; sku++) capBase += plans[sku].Mean(10) * skus[sku].CostPerBox;
            PurchaseCap = (long)Math.Round(PurchaseCapShare * capBase / 1e6, MidpointRounding.AwayFromZero) * 1_000_000;
        }

        public int Seed { get; }
        public IReadOnlyList<PlanningSku> Skus { get; }
        public IReadOnlyList<DemandPlan> Plans { get; }

        /// <summary>Random shift (0 or 1) of the promotion and ship-delay weeks.</summary>
        public int Shift { get; }

        public int PromotionWeek => 4 + Shift;
        public int ShipDelayWeek => 6 + Shift;

        /// <summary>Most the player may spend on orders in the week of the purchase cap, in pesos.</summary>
        public long PurchaseCap { get; }

        /// <summary>All events of the season, in week order (one per week at most).</summary>
        public IReadOnlyList<ScheduledEvent> Events => events;

        public ScheduledEvent EventAt(int week) => events.Find(e => e.Week == week);

        /// <summary>Standard normal noise of a SKU's week, already clipped.</summary>
        public double Noise(int sku, int week) => noise[sku, week];

        /// <summary>Product of the event multipliers of a SKU's week (1 when no event touches it).</summary>
        public double Multiplier(int sku, int week) => multipliers[sku, week];

        /// <summary>Real demand in boxes: max(0, round(mean × mult × (1 + cv × z))).</summary>
        public int Demand(int sku, int week)
        {
            double mean = Plans[sku].Mean(week) * multipliers[sku, week];
            double value = mean * (1.0 + Skus[sku].VariationCoefficient * noise[sku, week]);
            return Math.Max(0, PlanningRules.Round(value));
        }

        private void ScheduleEvents()
        {
            int promo = PromotionWeek;
            events.Add(new ScheduledEvent(SeasonEventKind.DamagedPallet, 3, PlanningCharacter.Julian,
                CharacterMood.Angry, PlanningCatalog.Cream));
            events.Add(new ScheduledEvent(SeasonEventKind.Promotion, promo, PlanningCharacter.Valeria,
                CharacterMood.Happy, PlanningCatalog.Labial, promo + 2, promo + 3, PromotionFactor));
            events.Add(new ScheduledEvent(SeasonEventKind.ShipDelay, ShipDelayWeek, PlanningCharacter.DonHernan,
                CharacterMood.Worried, PlanningCatalog.Perfume));
            events.Add(new ScheduledEvent(SeasonEventKind.CycleCountGap, 8, PlanningCharacter.Sofia,
                CharacterMood.Surprised, PlanningCatalog.GiftSet));
            events.Add(new ScheduledEvent(SeasonEventKind.NewCustomer, 9, PlanningCharacter.Andres,
                CharacterMood.Happy, PlanningCatalog.Cream, 11, 16, NewCustomerFactor));
            events.Add(new ScheduledEvent(SeasonEventKind.PurchaseCap, 10, PlanningCharacter.Marcela,
                CharacterMood.Worried));
            events.Add(new ScheduledEvent(SeasonEventKind.CompetitorStockout, 12, PlanningCharacter.Valeria,
                CharacterMood.Surprised, PlanningCatalog.GiftSet, 13, 16, CompetitorFactor));
            events.Add(new ScheduledEvent(SeasonEventKind.ClosingNotice, 15, PlanningCharacter.Marcela,
                CharacterMood.Neutral));
        }

        /// <summary>Box-Muller standard normal, clipped to ± <see cref="PlanningRules.NoiseClip"/>.</summary>
        private static double ClippedNormal(Random random)
        {
            double u1 = 1.0 - random.NextDouble(); // (0, 1], so the logarithm is finite
            double u2 = random.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return Math.Max(-PlanningRules.NoiseClip, Math.Min(PlanningRules.NoiseClip, z));
        }
    }
}
