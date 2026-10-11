using NovaMarket.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace NovaMarket.Tests.Interaction
{
    public class SelectionRingTests
    {
        [Test]
        public void EllipseRing_HasTheRequestedRadiiAndFacesUp()
        {
            var mesh = new Mesh();
            try
            {
                SelectionRing.BuildEllipseRing(mesh, radiusX: 30f, radiusZ: 20f, bandWidth: 1f, segmentCount: 64);

                Assert.AreEqual(128, mesh.vertexCount);
                Assert.AreEqual(61f, mesh.bounds.size.x, 0.01f);
                Assert.AreEqual(41f, mesh.bounds.size.z, 0.01f);
                Assert.AreEqual(0f, mesh.bounds.size.y, 0.001f, "the ring is flat");
                foreach (Vector3 normal in mesh.normals)
                    Assert.Greater(normal.y, 0.99f, "the ring must be visible from above");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
