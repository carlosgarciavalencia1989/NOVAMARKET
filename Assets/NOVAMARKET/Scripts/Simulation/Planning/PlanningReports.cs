using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>Costs of the season (or of one week), in pesos, by the concepts of section 7.</summary>
    public sealed class CostBreakdown
    {
        /// <summary>Holding: 0.4 % of the cost of the stock left at the end of each week.</summary>
        public double Holding { get; internal set; }

        /// <summary>Boxes above the rack capacity cost three times as much to keep (on the floor).</summary>
        public double Overflow { get; internal set; }

        /// <summary>Fixed cost of every order placed.</summary>
        public double Ordering { get; internal set; }

        /// <summary>Lost margin of the demand that could not be served (50 % of the cost).</summary>
        public double LostSales { get; internal set; }

        public double AirFreight { get; internal set; }

        /// <summary>Closing: stock above 3 weeks of January demand sold off at 10 % of its cost.</summary>
        public double Liquidation { get; internal set; }

        public double Total => Holding + Overflow + Ordering + LostSales + AirFreight + Liquidation;

        internal void Add(CostBreakdown other)
        {
            Holding += other.Holding;
            Overflow += other.Overflow;
            Ordering += other.Ordering;
            LostSales += other.LostSales;
            AirFreight += other.AirFreight;
            Liquidation += other.Liquidation;
        }
    }

    /// <summary>Boxes ordered or bought that travel to the CEDI and arrive in <see cref="ArrivalWeek"/>.</summary>
    public sealed class TransitLot
    {
        internal TransitLot(int sku, int boxes, int arrivalWeek, int orderedWeek, bool isAirFreight = false)
        {
            Sku = sku;
            Boxes = boxes;
            ArrivalWeek = arrivalWeek;
            OrderedWeek = orderedWeek;
            IsAirFreight = isAirFreight;
        }

        /// <summary>Catalogue index of the SKU.</summary>
        public int Sku { get; }
        public int Boxes { get; internal set; }

        /// <summary>The lot is in the warehouse from the start of this week.</summary>
        public int ArrivalWeek { get; internal set; }
        public int OrderedWeek { get; }

        /// <summary>Brought by plane after the ship delay.</summary>
        public bool IsAirFreight { get; }

        /// <summary>Weeks the ship delay pushed this lot back.</summary>
        public int DelayWeeks { get; internal set; }
    }

    /// <summary>What happened to one SKU in a closed week.</summary>
    public sealed class SkuWeekResult
    {
        internal SkuWeekResult(int sku, int ordered, int demand, int sold, int endingOnHand, int overflowBoxes)
        {
            Sku = sku;
            Ordered = ordered;
            Demand = demand;
            Sold = sold;
            EndingOnHand = endingOnHand;
            OverflowBoxes = overflowBoxes;
        }

        public int Sku { get; }
        public int Ordered { get; }
        public int Demand { get; }
        public int Sold { get; }
        public int Lost => Demand - Sold;

        /// <summary>Boxes in the warehouse after the week's sales (before the next arrivals).</summary>
        public int EndingOnHand { get; }

        /// <summary>Boxes above the rack capacity (on the floor).</summary>
        public int OverflowBoxes { get; }
    }

    /// <summary>A character's line after a closed week.</summary>
    public sealed class WeekComment
    {
        internal WeekComment(PlanningCharacter character, CharacterMood mood, string text)
        {
            Character = character;
            Mood = mood;
            Text = text;
        }

        public PlanningCharacter Character { get; }
        public CharacterMood Mood { get; }
        public string Text { get; }
    }

    /// <summary>Why the level ended early: the failed indicator, what it means and what to review.</summary>
    public sealed class GameOverInfo
    {
        internal GameOverInfo(int week, string indicator, string value, string explanation, string atlasStep)
        {
            Week = week;
            Indicator = indicator;
            Value = value;
            Explanation = explanation;
            AtlasStep = atlasStep;
        }

        /// <summary>Week whose close ended the level.</summary>
        public int Week { get; }
        public string Indicator { get; }
        public string Value { get; }
        public string Explanation { get; }

        /// <summary>The Atlas topic the player should review.</summary>
        public string AtlasStep { get; }
    }

    /// <summary>Result of closing one week.</summary>
    public sealed class WeekReport
    {
        internal WeekReport(int week, IReadOnlyList<SkuWeekResult> skus, CostBreakdown costs,
            IReadOnlyList<TransitLot> arrivals)
        {
            Week = week;
            Skus = skus;
            Costs = costs;
            Arrivals = arrivals;
            foreach (SkuWeekResult result in skus)
            {
                Demand += result.Demand;
                Sold += result.Sold;
                if (result.OverflowBoxes > 0) HasOverflow = true;
            }
        }

        public int Week { get; }
        public IReadOnlyList<SkuWeekResult> Skus { get; }
        public CostBreakdown Costs { get; }

        /// <summary>Lots that reached the warehouse at the start of the next week.</summary>
        public IReadOnlyList<TransitLot> Arrivals { get; }
        public WeekComment Comment { get; internal set; }

        public int Demand { get; }
        public int Sold { get; }
        public int Lost => Demand - Sold;
        public bool HasOverflow { get; }

        /// <summary>Event announced at the start of the next week, if any (also in PendingEvent).</summary>
        public EventDialog NewEvent { get; internal set; }

        /// <summary>Set when this close ended the level early.</summary>
        public GameOverInfo GameOver { get; internal set; }

        /// <summary>True when this was the last week (the closing has been applied).</summary>
        public bool EndedSeason { get; internal set; }
    }
}
