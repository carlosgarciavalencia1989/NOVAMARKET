using NovaMarket.UI;
using NUnit.Framework;
using UnityEngine;

namespace NovaMarket.Tests.UI
{
    public class MinimapProjectionTests
    {
        private const float Tolerance = 0.001f;
        private readonly MinimapProjection projection = new MinimapProjection(new Vector2(100f, -50f), 1800f);
        private readonly Vector2 size = new Vector2(200f, 200f);

        [Test]
        public void WorldCenter_IsTheMiddleOfTheMap()
        {
            Vector2 local = projection.WorldToLocal(new Vector3(100f, 30f, -50f), size);
            Assert.AreEqual(100f, local.x, Tolerance);
            Assert.AreEqual(100f, local.y, Tolerance);
        }

        [Test]
        public void North_IsUpAndEast_IsRight()
        {
            Vector2 northEast = projection.WorldToLocal(new Vector3(100f + 900f, 0f, -50f + 900f), size);
            Assert.AreEqual(200f, northEast.x, Tolerance, "east edge is the right side");
            Assert.AreEqual(0f, northEast.y, Tolerance, "north edge is the top (UI y grows down)");
        }

        [Test]
        public void LocalToWorld_InvertsWorldToLocal()
        {
            var world = new Vector3(-320f, 0f, 410f);
            Vector3 back = projection.LocalToWorld(projection.WorldToLocal(world, size), size);
            Assert.AreEqual(world.x, back.x, 0.01f);
            Assert.AreEqual(world.z, back.z, 0.01f);
            Assert.AreEqual(0f, back.y, Tolerance, "minimap points are on the ground");
        }

        [Test]
        public void Clicks_CountOnlyInsideTheCircle()
        {
            Assert.IsTrue(MinimapProjection.IsInsideCircle(new Vector2(100f, 100f), size));
            Assert.IsTrue(MinimapProjection.IsInsideCircle(new Vector2(199f, 100f), size));
            Assert.IsFalse(MinimapProjection.IsInsideCircle(new Vector2(5f, 5f), size), "corner is outside the round map");
        }

        [Test]
        public void DragOutside_IsClampedToTheEdge()
        {
            Vector2 clamped = MinimapProjection.ClampToCircle(new Vector2(-300f, 100f), size);
            Assert.AreEqual(0f, clamped.x, Tolerance, "a drag far to the left stops at the left edge");
            Assert.AreEqual(100f, clamped.y, Tolerance);

            var inside = new Vector2(120f, 80f);
            Assert.AreEqual(inside, MinimapProjection.ClampToCircle(inside, size), "points inside are unchanged");
        }
    }
}
