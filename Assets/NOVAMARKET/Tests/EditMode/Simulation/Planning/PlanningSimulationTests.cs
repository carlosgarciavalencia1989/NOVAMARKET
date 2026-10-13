using System;
using System.Linq;
using NovaMarket.Simulation.Planning;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Planning
{
    public class PlanningSimulationTests
    {
        private const int Seed = 7;
        private const int Lab = PlanningCatalog.Labial;
        private const int Per = PlanningCatalog.Perfume;
        private const int Set = PlanningCatalog.GiftSet;
        private const int Cre = PlanningCatalog.Cream;

        private static readonly int[] NoOrders = new int[4];

        /// <summary>Answers events with the default option and closes weeks until the given week starts.</summary>
        private static void PlayUntil(PlanningSimulation simulation, int week, Func<PlanningSimulation, int[]> orders = null)
        {
            while (simulation.Week < week && !simulation.IsFinished)
            {
                if (simulation.PendingEvent != null) simulation.ResolveEvent(0);
                simulation.CloseWeek(orders?.Invoke(simulation) ?? NoOrders);
            }
        }

        [Test]
        public void InitialState_HasTwoWeeksOnHandAndTheLeadTimeInTransit()
        {
            var simulation = new PlanningSimulation(Seed);

            Assert.AreEqual(1, simulation.Week);
            Assert.AreEqual(91, simulation.OnHand(Lab)); // round(45.3 × 2)
            Assert.AreEqual(86, simulation.OnHand(Per)); // round(43 × 2)
            Assert.AreEqual(17, simulation.OnHand(Set)); // round(8.3 × 2)
            Assert.AreEqual(28, simulation.OnHand(Cre)); // round(14.2 × 2)

            Assert.AreEqual(0, simulation.InTransitBoxes(Lab), "LT 1 has nothing in transit.");
            var perfumeLots = simulation.InTransit.Where(lot => lot.Sku == Per).ToArray();
            CollectionAssert.AreEqual(new[] { 2, 3 }, perfumeLots.Select(lot => lot.ArrivalWeek));
            CollectionAssert.AreEqual(new[] { 43, 43 }, perfumeLots.Select(lot => lot.Boxes));
            Assert.AreEqual(8, simulation.InTransitBoxes(Set));
            Assert.AreEqual(14, simulation.InTransitBoxes(Cre));
        }

        [Test]
        public void CloseWeek_SellsWhatIsOnHandAndChargesTheWeeklyCosts()
        {
            var simulation = new PlanningSimulation(Seed);
            PlanningSeason season = simulation.Season;
            var orders = new[] { 10, 0, 5, 0 };

            WeekReport report = simulation.CloseWeek(orders);

            double holding = 0, lost = 0;
            int[] start = { 91, 86, 17, 28 };
            for (int sku = 0; sku < 4; sku++)
            {
                int demand = season.Demand(sku, 1);
                int sold = Math.Min(start[sku], demand);
                Assert.AreEqual(demand, report.Skus[sku].Demand);
                Assert.AreEqual(sold, report.Skus[sku].Sold);
                Assert.AreEqual(start[sku] - sold, report.Skus[sku].EndingOnHand);
                holding += (start[sku] - sold) * simulation.Skus[sku].CostPerBox * 0.004;
                lost += (demand - sold) * simulation.Skus[sku].CostPerBox * 0.5;
            }
            Assert.AreEqual(holding, report.Costs.Holding, 1e-6);
            Assert.AreEqual(lost, report.Costs.LostSales, 1e-6);
            Assert.AreEqual(250_000 + 250_000, report.Costs.Ordering, 1e-6, "One order cost per SKU ordered.");
            Assert.AreEqual(2, simulation.Week);
        }

        [Test]
        public void Orders_ArriveAfterTheirLeadTime()
        {
            var simulation = new PlanningSimulation(Seed);
            simulation.CloseWeek(new[] { 30, 40, 0, 0 });

            // Labial (LT 1) ordered in week 1 is in the warehouse at the start of week 2.
            WeekReport first = simulation.History[0];
            Assert.IsTrue(first.Arrivals.Any(lot => lot.Sku == Lab && lot.Boxes == 30));
            Assert.AreEqual(first.Skus[Lab].EndingOnHand + 30, simulation.OnHand(Lab));

            // Perfume (LT 3) ordered in week 1 arrives in week 4.
            Assert.IsTrue(simulation.InTransit.Any(lot => lot.Sku == Per && lot.Boxes == 40 && lot.ArrivalWeek == 4));
        }

        [Test]
        public void LostSales_AreNotBackordered()
        {
            var simulation = new PlanningSimulation(Seed);
            PlayUntil(simulation, 4);

            foreach (WeekReport report in simulation.History)
                Assert.AreEqual(report.Skus[Lab].Demand, simulation.Season.Demand(Lab, report.Week),
                    "Each week's demand is only that week's, never last week's shortage.");
            Assert.AreEqual(0, simulation.OnHand(Lab));
            Assert.Greater(simulation.TotalLost, 0);
        }

        [Test]
        public void Overflow_ChargesTripleHoldingOnBoxesAboveTheRack()
        {
            var simulation = new PlanningSimulation(Seed);
            int[] orders = PlanningPolicies.AtlasOrders(simulation);
            orders[Lab] += 400;
            simulation.CloseWeek(orders);
            int start = simulation.OnHand(Lab);

            WeekReport report = simulation.CloseWeek(PlanningPolicies.AtlasOrders(simulation));

            int ending = start - report.Skus[Lab].Sold;
            int overflow = ending - simulation.RackCapacity(Lab);
            Assert.Greater(overflow, 0);
            Assert.AreEqual(overflow, report.Skus[Lab].OverflowBoxes);
            Assert.AreEqual(overflow * 432_000 * 0.012, report.Costs.Overflow, 1e-6);
            Assert.AreEqual(PlanningCharacter.Julian, report.Comment.Character);
        }

        [Test]
        public void Overflow_ThreeWeeksInARow_IsGameOver()
        {
            var simulation = new PlanningSimulation(Seed);
            PlayUntil(simulation, 10, s => s.Week == 1 ? new[] { 900, 0, 0, 0 } : PlanningPolicies.AtlasOrders(s));

            Assert.AreEqual(PlanningOutcome.GameOver, simulation.Outcome);
            Assert.AreEqual("Desborde de bodega", simulation.GameOver.Indicator);
            Assert.AreEqual(4, simulation.GameOver.Week, "Overflow in weeks 2, 3 and 4.");
            Assert.Throws<InvalidOperationException>(() => simulation.CloseWeek(NoOrders));
        }

        [Test]
        public void ServiceBelow85PercentAfterWeekFour_IsGameOver()
        {
            var simulation = new PlanningSimulation(Seed);
            PlayUntil(simulation, 10);

            Assert.AreEqual(PlanningOutcome.GameOver, simulation.Outcome);
            Assert.AreEqual(4, simulation.GameOver.Week);
            Assert.AreEqual("Nivel de servicio acumulado", simulation.GameOver.Indicator);
            Assert.Less(simulation.ServiceLevel, 0.85);
            StringAssert.Contains("SS", simulation.GameOver.AtlasStep);
        }

        [Test]
        public void ReferenceWithoutGameOver_PlaysTheWholeSeasonAndLiquidates()
        {
            PlanningSimulation reference = PlanningPolicies.PlayReference(Seed);

            Assert.AreEqual(PlanningOutcome.Completed, reference.Outcome);
            Assert.AreEqual(16, reference.History.Count);
            Assert.AreEqual(17, reference.Week);
            Assert.IsTrue(reference.History[15].EndedSeason);

            double liquidation = 0;
            for (int sku = 0; sku < 4; sku++)
            {
                int inventory = reference.OnHand(sku) + reference.InTransitBoxes(sku);
                int expected = Math.Max(0, inventory - reference.ClosingAllowance(sku));
                Assert.AreEqual(expected, reference.LiquidatedBoxes(sku));
                liquidation += expected * reference.Skus[sku].CostPerBox * 0.10;
            }
            Assert.AreEqual(liquidation, reference.Costs.Liquidation, 1e-6);
        }

        [Test]
        public void ClosingAllowance_IsThreeWeeksOfJanuary() =>
            Assert.AreEqual(109, new PlanningSimulation(Seed).ClosingAllowance(Lab)); // round(3 × 36.2)

        [Test]
        public void SameSeed_PlaysTheSameSeason()
        {
            PlanningSimulation a = PlanningPolicies.PlayReference(Seed);
            PlanningSimulation b = PlanningPolicies.PlayReference(Seed);

            Assert.AreEqual(a.TotalDemand, b.TotalDemand);
            Assert.AreEqual(a.Costs.Total, b.Costs.Total, 1e-6);
        }

        [Test]
        public void DifferentSeeds_ChangeDemandAndEventWeeks()
        {
            var shifts = Enumerable.Range(1, 20).Select(seed => new PlanningSeason(seed).Shift).Distinct().Count();
            var demands = Enumerable.Range(1, 20).Select(seed => new PlanningSeason(seed).Demand(Lab, 1)).Distinct().Count();

            Assert.AreEqual(2, shifts, "The promo and ship-delay weeks move between seeds.");
            Assert.Greater(demands, 3);
        }

        [Test]
        public void Noise_IsClippedAndEventsMultiplyTheDemand()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var season = new PlanningSeason(seed);
                for (int sku = 0; sku < 4; sku++)
                for (int week = 1; week <= 17; week++)
                    Assert.LessOrEqual(Math.Abs(season.Noise(sku, week)), 2.3);

                int promo = season.PromotionWeek;
                Assert.AreEqual(1.9, season.Multiplier(Lab, promo + 2));
                Assert.AreEqual(1.9, season.Multiplier(Lab, promo + 3));
                Assert.AreEqual(1.0, season.Multiplier(Lab, promo + 1));
                Assert.AreEqual(1.35, season.Multiplier(Cre, 11));
                Assert.AreEqual(1.0, season.Multiplier(Cre, 10));
                Assert.AreEqual(1.5, season.Multiplier(Set, 16));
            }
        }

        [Test]
        public void Events_FollowTheScheduleWithOnePerWeek()
        {
            var season = new PlanningSeason(Seed);
            int[] weeks = season.Events.Select(e => e.Week).ToArray();

            CollectionAssert.AllItemsAreUnique(weeks);
            Assert.AreEqual(8, weeks.Length);
            Assert.AreEqual(SeasonEventKind.DamagedPallet, season.EventAt(3).Kind);
            Assert.AreEqual(SeasonEventKind.Promotion, season.EventAt(4 + season.Shift).Kind);
            Assert.AreEqual(SeasonEventKind.ShipDelay, season.EventAt(6 + season.Shift).Kind);
            Assert.AreEqual(PlanningCharacter.DonHernan, season.EventAt(6 + season.Shift).Character);
            Assert.AreEqual(SeasonEventKind.ClosingNotice, season.EventAt(15).Kind);
        }

        [Test]
        public void PendingEvent_BlocksTheWeekUntilAnswered()
        {
            var simulation = new PlanningSimulation(Seed);
            PlayUntil(simulation, 3, PlanningPolicies.AtlasOrders);

            Assert.IsNotNull(simulation.PendingEvent);
            Assert.AreEqual(SeasonEventKind.DamagedPallet, simulation.PendingEvent.Kind);
            Assert.AreEqual(PlanningCharacter.Julian, simulation.PendingEvent.Character);
            Assert.AreSame(simulation.PendingEvent, simulation.History[1].NewEvent);
            Assert.Throws<InvalidOperationException>(() => simulation.CloseWeek(NoOrders));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.ResolveEvent(1));

            simulation.ResolveEvent(0);
            Assert.IsNull(simulation.PendingEvent);
            Assert.AreEqual(1, simulation.EventsAnswered);
            Assert.DoesNotThrow(() => simulation.CloseWeek(NoOrders));
        }

        [Test]
        public void DamagedPallet_RemovesFifteenPercentOfTheCream()
        {
            var simulation = new PlanningSimulation(Seed);
            PlayUntil(simulation, 2, PlanningPolicies.AtlasOrders);
            simulation.CloseWeek(PlanningPolicies.AtlasOrders(simulation));
            WeekReport week2 = simulation.History[1];

            int before = week2.Skus[Cre].EndingOnHand + week2.Arrivals.Where(l => l.Sku == Cre).Sum(l => l.Boxes);
            int lost = (int)Math.Round(before * 0.15, MidpointRounding.AwayFromZero);
            Assert.AreEqual(before - lost, simulation.OnHand(Cre));
            StringAssert.Contains($"Perdimos {lost} cajas", simulation.PendingEvent.Message);
        }

        [Test]
        public void ShipDelay_PushesThePerfumeBackTwoWeeks_AndAirFreightBringsHalf()
        {
            var season = new PlanningSeason(Seed);
            var waiting = new PlanningSimulation(season);
            var flying = new PlanningSimulation(season);
            PlayUntil(waiting, season.ShipDelayWeek, PlanningPolicies.AtlasOrders);
            PlayUntil(flying, season.ShipDelayWeek, PlanningPolicies.AtlasOrders);

            Assert.AreEqual(SeasonEventKind.ShipDelay, waiting.PendingEvent.Kind);
            Assert.AreEqual(2, waiting.PendingEvent.Options.Count);
            var perfume = waiting.InTransit.Where(l => l.Sku == Per).ToArray();
            Assert.IsNotEmpty(perfume);
            Assert.IsTrue(perfume.All(l => l.DelayWeeks == 2 && l.ArrivalWeek >= season.ShipDelayWeek + 2));

            int total = waiting.InTransitBoxes(Per);
            waiting.ResolveEvent(0);
            flying.ResolveEvent(1);

            int half = (int)Math.Round(total / 2.0, MidpointRounding.AwayFromZero);
            TransitLot air = flying.InTransit.Single(l => l.IsAirFreight);
            Assert.AreEqual(half, air.Boxes);
            Assert.AreEqual(season.ShipDelayWeek + 1, air.ArrivalWeek);
            Assert.AreEqual(total, flying.InTransitBoxes(Per), "Air freight moves boxes, it does not add them.");
            Assert.AreEqual(4_500_000, flying.Costs.AirFreight, 1e-6);
            Assert.AreEqual(0, waiting.Costs.AirFreight, 1e-6);
        }

        [Test]
        public void CycleCountGap_RemovesAQuarterOfTheGiftSets()
        {
            var simulation = new PlanningSimulation(Seed);
            PlayUntil(simulation, 8, PlanningPolicies.AtlasOrders);

            Assert.AreEqual(SeasonEventKind.CycleCountGap, simulation.PendingEvent.Kind);
            Assert.AreEqual(PlanningCharacter.Sofia, simulation.PendingEvent.Character);
        }

        [Test]
        public void PurchaseCap_LimitsTheOrdersOfWeekTenOnly()
        {
            var simulation = new PlanningSimulation(Seed);
            PlayUntil(simulation, 10, PlanningPolicies.AtlasOrders);
            simulation.ResolveEvent(0);

            Assert.AreEqual(simulation.Season.PurchaseCap, simulation.PurchaseCap);
            var big = new[] { 500, 500, 0, 0 };
            Assert.IsTrue(simulation.ExceedsPurchaseCap(big));
            Assert.Throws<InvalidOperationException>(() => simulation.CloseWeek(big));

            int[] scaled = PlanningPolicies.ScaleToCap(simulation, big);
            Assert.LessOrEqual(simulation.OrderValue(scaled), simulation.PurchaseCap.Value);
            Assert.AreEqual(scaled[0] * 432_000.0 / (scaled[1] * 960_000.0), 432_000.0 / 960_000.0, 0.02,
                "Both orders shrink by the same factor.");

            simulation.CloseWeek(scaled);
            Assert.IsNull(simulation.PurchaseCap, "The cap only lasts one week.");
        }

        [Test]
        public void PurchaseCap_IsFiftyFivePercentOfWeekTenAtCost_RoundedToMillions()
        {
            var season = new PlanningSeason(Seed);
            double value = 0;
            for (int sku = 0; sku < 4; sku++) value += season.Plans[sku].Mean(10) * season.Skus[sku].CostPerBox;
            Assert.AreEqual(Math.Round(0.55 * value / 1e6, MidpointRounding.AwayFromZero) * 1e6, season.PurchaseCap);
        }

        [Test]
        public void AnnouncedFactor_IsOneUntilTheEventIsAnnounced()
        {
            var simulation = new PlanningSimulation(Seed);
            int promo = simulation.Season.PromotionWeek;
            PlayUntil(simulation, promo - 1, PlanningPolicies.AtlasOrders);
            Assert.AreEqual(1.0, simulation.AnnouncedFactor(Lab, promo + 2));

            PlayUntil(simulation, promo, PlanningPolicies.AtlasOrders);
            Assert.AreEqual(1.9, simulation.AnnouncedFactor(Lab, promo + 2));
            Assert.AreEqual(1.0, simulation.AnnouncedFactor(Lab, promo + 4));
            Assert.AreEqual(simulation.Suggest(Lab).Forecast,
                AtlasSuggestion.Compute(simulation.Season.Plans[Lab], simulation.Week, simulation.OnHand(Lab),
                    simulation.InTransitBoxes(Lab)).Forecast, "The Atlas suggestion ignores the events.");
        }

        [Test]
        public void WeeklyComment_FollowsThePriorityOfTheSpec()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                PlanningSimulation reference = PlanningPolicies.PlayReference(seed);
                foreach (WeekReport report in reference.History)
                {
                    bool manyLost = report.Demand > 0 && (double)report.Lost / report.Demand > 0.05;
                    if (manyLost) Assert.AreEqual(PlanningCharacter.Valeria, report.Comment.Character);
                    else if (report.HasOverflow) Assert.AreEqual(PlanningCharacter.Julian, report.Comment.Character);
                    else
                        CollectionAssert.Contains(new[] { PlanningCharacter.Marcela, PlanningCharacter.Andres },
                            report.Comment.Character);
                }
            }
        }

        [Test]
        public void CloseWeek_RejectsInvalidOrders()
        {
            var simulation = new PlanningSimulation(Seed);
            Assert.Throws<ArgumentException>(() => simulation.CloseWeek(new[] { 1, 2 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.CloseWeek(new[] { -1, 0, 0, 0 }));
            Assert.Throws<InvalidOperationException>(() => simulation.ResolveEvent(0));
        }
    }
}
