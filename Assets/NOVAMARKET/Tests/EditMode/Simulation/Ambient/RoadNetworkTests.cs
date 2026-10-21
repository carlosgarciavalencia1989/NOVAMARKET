using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    public class RoadNetworkTests
    {
        [Test]
        public void Lanes_KeepToTheRightAndStopBeforeTheCrossRoad()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.Crossing(false));

            TrafficLane eastbound = TestNetworks.LaneHeading(network, new Vector2(1f, 0f), new Vector2(-150f, 0f));
            TrafficLane westbound = TestNetworks.LaneHeading(network, new Vector2(-1f, 0f), new Vector2(-150f, 0f));
            TrafficLane northbound = TestNetworks.LaneHeading(network, new Vector2(0f, 1f), new Vector2(0f, -100f));

            Assert.AreEqual(-3f, eastbound.Start.Y, 1e-3f, "heading east, the right side is south");
            Assert.AreEqual(3f, westbound.Start.Y, 1e-3f);
            Assert.AreEqual(3f, northbound.Start.X, 1e-3f);
            Assert.AreEqual(-7.5f, eastbound.End.X, 1e-3f, "kerb of the cross road (6 m) plus 1.5 m");
            Assert.AreSame(westbound, eastbound.Opposite);
        }

        [Test]
        public void Crossing_HasStraightLeftAndRightFromEveryApproach_AndNoUTurns()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.Crossing(false));
            TrafficJunction junction = network.Junctions.Single();

            Assert.AreEqual(4, junction.Incoming.Count);
            foreach (TrafficLane lane in junction.Incoming)
            {
                var turns = lane.Next.Cast<TrafficConnector>().Select(c => c.Turn).OrderBy(t => t).ToList();
                CollectionAssert.AreEqual(new[] { TurnKind.Straight, TurnKind.Right, TurnKind.Left }, turns);
            }
        }

        [Test]
        public void Conflicts_CrossingStraightsConflict_RightTurnsFromOppositeSidesDoNot()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.Crossing(false));
            TrafficConnector Movement(Vector2 from, TurnKind turn) =>
                network.Connectors.Single(c => Vector2.Dot(c.From.Direction, from) > 0.99f && c.Turn == turn);

            TrafficConnector eastStraight = Movement(new Vector2(1f, 0f), TurnKind.Straight);
            TrafficConnector northStraight = Movement(new Vector2(0f, 1f), TurnKind.Straight);
            TrafficConnector eastRight = Movement(new Vector2(1f, 0f), TurnKind.Right);
            TrafficConnector westRight = Movement(new Vector2(-1f, 0f), TurnKind.Right);
            TrafficConnector westLeft = Movement(new Vector2(-1f, 0f), TurnKind.Left);

            CollectionAssert.Contains(eastStraight.Conflicts, northStraight);
            CollectionAssert.DoesNotContain(eastRight.Conflicts, westRight);
            CollectionAssert.Contains(eastStraight.Conflicts, westLeft, "a left turn crosses the oncoming lane");
        }

        [Test]
        public void PriorityCrossing_TheSideStreetHasPare_AndLeftTurnsGiveWay()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.Crossing(false, RoadClass.Avenue, RoadClass.Street));
            TrafficJunction junction = network.Junctions.Single();

            foreach (TrafficLane lane in junction.Incoming)
            {
                bool avenue = lane.Class == RoadClass.Avenue;
                Assert.AreEqual(avenue, lane.IsMajor);
                Assert.AreEqual(!avenue, lane.HasStopSign);
            }
            foreach (TrafficConnector movement in junction.Movements)
            {
                int expected = (movement.From.IsMajor ? 0 : 2) + (movement.Turn == TurnKind.Left ? 1 : 0);
                Assert.AreEqual(expected, movement.Priority, $"{movement.Turn} from {movement.From.Class}");
            }
        }

        [Test]
        public void Signal_TheTwoGroupsAreNeverGreenTogether_AndBothGetGreen()
        {
            var signal = new TrafficSignal(30f, 20f, offset: 7f);
            bool greenA = false, greenB = false;
            for (double t = 0; t < 3 * signal.Cycle; t += 0.1)
            {
                SignalAspect a = signal.AspectFor(0, t), b = signal.AspectFor(1, t);
                Assert.IsTrue(a == SignalAspect.Red || b == SignalAspect.Red, $"t = {t}");
                greenA |= a == SignalAspect.Green;
                greenB |= b == SignalAspect.Green;
            }
            Assert.IsTrue(greenA && greenB);
            Assert.AreEqual(60f, signal.Cycle, 1e-3f);
        }

        [Test]
        public void Signal_GreenLeft_CountsDownDuringGreenOnly()
        {
            var signal = new TrafficSignal(30f, 20f);

            Assert.AreEqual(30f, signal.GreenLeft(0, 0.0), 1e-3f);
            Assert.AreEqual(4f, signal.GreenLeft(0, 26.0), 1e-3f);
            Assert.AreEqual(0f, signal.GreenLeft(0, 31.0), "amber");
            Assert.AreEqual(20f, signal.GreenLeft(1, 35.0), 1e-3f, "cross green starts after amber and all-red");
            Assert.AreEqual(0f, signal.GreenLeft(1, 10.0));
        }

        [Test]
        public void CityLayout_HalfStreetsAreOneWayOnTheirAsphalt()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.City());

            // East of the CEDI lot only the cells east of x = 150 exist: a 6 m half street, one way.
            List<TrafficLane> street = network.Lanes
                .Where(l => System.Math.Abs(l.Direction.X) < 0.01f && l.Start.Y > -50f && l.Start.Y < 26f
                            && System.Math.Abs(l.Start.X - 150f) < 4f).ToList();
            Assert.AreEqual(1, street.Count, "one lane: one-way");
            Assert.AreEqual(153f, street[0].Start.X, 1e-3f, "on the asphalt east of the grid line");
            Assert.IsNull(street[0].Opposite);
            Assert.Less(Vector2.Dot(street[0].Direction, new Vector2(0f, 1f)), 0f, "driven with its block on the left");
        }

        [Test]
        public void CityLayout_HasLightsOnTheHighwayAtTheBridges_AndAGateBox()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.City());

            foreach (float x in TestNetworks.BridgeX)
                Assert.IsNotNull(network.Junctions.Single(j => Vector2.Distance(j.Position, new Vector2(x, -60f)) < 0.1f).Signal);
            TrafficLane westbound = network.FindLane(new Vector2(0f, -57f), new Vector2(-1f, 0f));
            Assert.AreEqual(1, westbound.KeepClear.Count);
            Assert.Greater(network.Lanes.Sum(l => l.BusStops.Count), 4);
        }

        [Test]
        public void CityLayout_EveryLaneLeadsSomewhere_AndMostOfTheCityIsReachable()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.City());

            foreach (TrafficLane lane in network.Lanes)
                if (lane.EndJunction != null)
                    Assert.IsNotEmpty(lane.Next, $"lane {lane.Id} from {lane.Start} to {lane.End}");

            // From a highway lane, following the movements reaches nearly every lane.
            TrafficLane start = network.FindLane(new Vector2(0f, -57f), new Vector2(-1f, 0f));
            var seen = new HashSet<TrafficLink> { start };
            var queue = new Queue<TrafficLink>(seen);
            while (queue.Count > 0)
                foreach (TrafficLink next in queue.Dequeue().Next)
                    if (seen.Add(next)) queue.Enqueue(next);
            int reached = network.Lanes.Count(seen.Contains);
            Assert.Greater(reached, network.Lanes.Count * 0.95f, $"{reached} of {network.Lanes.Count} lanes");
        }
    }
}
