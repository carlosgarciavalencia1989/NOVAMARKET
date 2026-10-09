using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Accumulates flat-shaded triangles into a multi-submesh <see cref="Mesh"/>.
    /// Winding is fixed automatically from an outward hint, so callers never worry about it.
    /// </summary>
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] submeshes;

        public MeshBuilder(int submeshCount)
        {
            submeshes = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) submeshes[i] = new List<int>();
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int submesh)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, outward) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }
            normal.Normalize();

            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up);
            submeshes[submesh].Add(start);
            submeshes[submesh].Add(start + 1);
            submeshes[submesh].Add(start + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int submesh)
        {
            AddTriangle(a, b, c, outward, submesh);
            AddTriangle(a, c, d, outward, submesh);
        }

        /// <summary>Oriented box without a bottom face: four walls plus a top (roof) face.</summary>
        public void AddBox(Vector3 center, Vector3 right, Vector3 forward, float halfX, float halfY, float halfZ,
            int sideSubmesh, int topSubmesh)
        {
            Vector3 P(float sx, float sy, float sz) =>
                center + right * (sx * halfX) + Vector3.up * (sy * halfY) + forward * (sz * halfZ);

            AddQuad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), right, sideSubmesh);
            AddQuad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), -right, sideSubmesh);
            AddQuad(P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), forward, sideSubmesh);
            AddQuad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), -forward, sideSubmesh);
            AddQuad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.up, topSubmesh);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// Low-poly stylised tree meshes, sized in meters. Submesh 0 is the trunk, submesh 1 the canopy.
    /// </summary>
    internal static class TreeMeshes
    {
        public static Mesh Broadleaf(int seed)
        {
            var rng = new System.Random(seed);
            var b = new MeshBuilder(2);
            Trunk(b, 4.6f, 0.35f, 0.25f);
            Blob(b, new Vector3(0f, 6.8f, 0f), new Vector3(3.6f, 3.2f, 3.6f), rng);
            Blob(b, new Vector3(1.2f, 7.8f, -0.6f), new Vector3(2.2f, 2.0f, 2.2f), rng);
            return b.ToMesh("Broadleaf");
        }

        public static Mesh Round(int seed)
        {
            var rng = new System.Random(seed);
            var b = new MeshBuilder(2);
            Trunk(b, 3.8f, 0.32f, 0.22f);
            Blob(b, new Vector3(0f, 6.4f, 0f), new Vector3(3.0f, 3.6f, 3.0f), rng);
            Blob(b, new Vector3(-1.0f, 5.6f, 0.8f), new Vector3(2.0f, 1.8f, 2.0f), rng);
            return b.ToMesh("Round");
        }

        public static Mesh Conifer()
        {
            var b = new MeshBuilder(2);
            Trunk(b, 2.5f, 0.3f, 0.22f);
            Cone(b, 1.5f, 7f, 3.2f);
            Cone(b, 5f, 6f, 2.5f);
            Cone(b, 8.5f, 6f, 1.7f);
            return b.ToMesh("Conifer");
        }

        /// <summary>Guayacán: tall trunk and a wide, flat umbrella crown.</summary>
        public static Mesh Guayacan(int seed)
        {
            var rng = new System.Random(seed);
            var b = new MeshBuilder(2);
            Trunk(b, 7f, 0.4f, 0.28f);
            Blob(b, new Vector3(0f, 8.2f, 0f), new Vector3(5.2f, 2.4f, 5.2f), rng);
            Blob(b, new Vector3(2.4f, 8.8f, 1.2f), new Vector3(2.6f, 1.6f, 2.6f), rng);
            Blob(b, new Vector3(-2.2f, 8.6f, -1.5f), new Vector3(2.4f, 1.5f, 2.4f), rng);
            return b.ToMesh("Guayacan");
        }

        private static void Trunk(MeshBuilder b, float height, float bottomRadius, float topRadius)
        {
            const int sides = 6;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                b.AddQuad(d0 * bottomRadius + Vector3.down * 0.5f, d0 * topRadius + Vector3.up * height,
                    d1 * topRadius + Vector3.up * height, d1 * bottomRadius + Vector3.down * 0.5f, d0 + d1, 0);
            }
        }

        private static void Cone(MeshBuilder b, float baseY, float height, float radius)
        {
            const int sides = 8;
            var apex = new Vector3(0f, baseY + height, 0f);
            var baseCenter = new Vector3(0f, baseY, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Vector3 side = (p0 + p1) / 2f - baseCenter;
                b.AddTriangle(p0, apex, p1, side + Vector3.up * (radius / height), 1);
                b.AddTriangle(p0, p1, baseCenter, Vector3.down, 1);
            }
        }

        /// <summary>Irregular faceted ellipsoid: a once-subdivided icosphere with jittered vertices.</summary>
        private static void Blob(MeshBuilder b, Vector3 center, Vector3 radii, System.Random rng)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            var midpoints = new Dictionary<long, int>();
            int Midpoint(int i0, int i1)
            {
                long key = ((long)Mathf.Min(i0, i1) << 32) | (uint)Mathf.Max(i0, i1);
                if (midpoints.TryGetValue(key, out int index)) return index;
                points.Add((points[i0] + points[i1]) / 2f);
                midpoints[key] = points.Count - 1;
                return points.Count - 1;
            }

            var subdivided = new List<int>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                int a = faces[f], c1 = faces[f + 1], c2 = faces[f + 2];
                int ab = Midpoint(a, c1), bc = Midpoint(c1, c2), ca = Midpoint(c2, a);
                subdivided.AddRange(new[] { a, ab, ca, c1, bc, ab, c2, ca, bc, ab, bc, ca });
            }

            for (int i = 0; i < points.Count; i++)
            {
                float jitter = 0.85f + (float)rng.NextDouble() * 0.3f;
                Vector3 n = points[i].normalized * jitter;
                points[i] = center + Vector3.Scale(n, radii);
            }

            for (int f = 0; f < subdivided.Count; f += 3)
            {
                Vector3 a = points[subdivided[f]], c1 = points[subdivided[f + 1]], c2 = points[subdivided[f + 2]];
                b.AddTriangle(a, c1, c2, (a + c1 + c2) / 3f - center, 1);
            }
        }
    }
}
