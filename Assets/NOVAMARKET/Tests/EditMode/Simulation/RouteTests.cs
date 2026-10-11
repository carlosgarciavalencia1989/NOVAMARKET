using System.Numerics;
using NovaMarket.Simulation;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation
{
    public class RouteTests
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void Polyline_MeasuresAndSamplesItsSegments()
        {
            var line = new Polyline(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 5));

            Assert.AreEqual(15f, line.Length, Tolerance);
            Assert.AreEqual(new Vector2(4, 0), line.PointAt(4));
            Assert.AreEqual(new Vector2(10, 2), line.PointAt(12));
            Assert.AreEqual(new Vector2(0, 1), line.DirectionAt(12));
            Assert.AreEqual(line.End, line.PointAt(100), "distances past the end are clamped");
        }

        [Test]
        public void Polyline_IgnoresRepeatedPoints()
        {
            var line = new Polyline(new Vector2(0, 0), new Vector2(0, 0), new Vector2(3, 4));
            Assert.AreEqual(2, line.Points.Count);
            Assert.AreEqual(5f, line.Length, Tolerance);
        }

        [Test]
        public void Mover_BrakesToAStopAtTheEnd()
        {
            var mover = new RouteMover(Vector2.Zero, Vector2.UnitX);
            mover.SetPlan(new[] { new RouteLeg(new Polyline(Vector2.Zero, new Vector2(30, 0)), 10f) });

            for (int i = 0; i < 2000 && !mover.IsIdle; i++) mover.Tick(0.02f);

            Assert.IsTrue(mover.IsIdle);
            Assert.AreEqual(30f, mover.Position.X, Tolerance);
            Assert.AreEqual(0f, mover.Speed, Tolerance);
        }

        [Test]
        public void Mover_StopsAtTheQueueLimit()
        {
            var mover = new RouteMover(Vector2.Zero, Vector2.UnitX);
            mover.SetPlan(new[] { new RouteLeg(new Polyline(Vector2.Zero, new Vector2(30, 0)), 10f) });

            for (int i = 0; i < 2000; i++) mover.Tick(0.02f, limit: 12f);

            Assert.IsFalse(mover.IsIdle, "the leg is not finished while queued");
            Assert.AreEqual(12f, mover.Position.X, Tolerance);
        }

        [Test]
        public void Mover_FacesBackwardsWhenReversing()
        {
            var mover = new RouteMover(Vector2.Zero, -Vector2.UnitX);
            mover.SetPlan(new[] { new RouteLeg(new Polyline(Vector2.Zero, new Vector2(20, 0)), 2f, reverse: true) });

            mover.Tick(0.5f);

            Assert.IsTrue(mover.IsReversing);
            Assert.Greater(mover.Position.X, 0f, "it moves towards +X");
            Assert.AreEqual(-1f, mover.Heading.X, Tolerance, "but the front looks to -X");
        }

        [Test]
        public void Mover_DrivesLegsInOrder()
        {
            var mover = new RouteMover(Vector2.Zero, Vector2.UnitX);
            mover.SetPlan(new[]
            {
                new RouteLeg(new Polyline(Vector2.Zero, new Vector2(10, 0)), 5f),
                new RouteLeg(new Polyline(new Vector2(10, 0), new Vector2(10, 8)), 5f)
            });

            for (int i = 0; i < 3000 && !mover.IsIdle; i++) mover.Tick(0.02f);

            Assert.AreEqual(2, mover.CompletedLegs);
            Assert.AreEqual(new Vector2(10, 8), mover.Position);
        }

        [Test]
        public void Docks_AreNumberedLikeTheScene()
        {
            var simulation = new CediOperationSimulation(SampleOperationData.Create());
            Assert.AreEqual(3, simulation.Docks.FreeCount(DockKind.Receiving));
            Assert.AreEqual(3, simulation.Docks.FreeCount(DockKind.Shipping));
            Assert.AreEqual(1, simulation.Docks.Get(DockKind.Receiving, 0).Number);
            Assert.AreEqual(4, simulation.Docks.Get(DockKind.Shipping, 0).Number);
        }
    }
}
