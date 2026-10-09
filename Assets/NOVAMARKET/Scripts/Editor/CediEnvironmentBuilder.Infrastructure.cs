using UnityEditor;
using UnityEngine;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Transport corridor parallel to the highway, as in the Aburrá valley: the canalised
    /// Medellín river (a concrete trapezoidal channel sunk through terrain holes) and the
    /// elevated metro line on a viaduct, with two stations and two parked trains.
    /// </summary>
    public static partial class CediEnvironmentBuilder
    {
        private const string InfrastructureMeshFolder = Root + "Models/Generated/Infrastructure";

        private const float MetroZ = -84f;
        private const float MetroDeckHalfWidth = 4.5f;
        private const float MetroDeckBottom = 10.2f;
        private const float MetroDeckTop = 11.4f;
        private const float MetroColumnSpacing = 32f;

        private const float RiverZ = -116f;
        private const float RiverHalfWidth = 16f;      // top of the channel walls
        private const float RiverBedHalfWidth = 11.5f;
        private const float RiverDepth = 5.5f;
        private const float RiverWaterDepth = 4.2f;    // water surface below ground
        private const float RiverLipWidth = 5f;        // walkway that hides the terrain-hole edge
        private const float RiverHalfLength = RoadHalfLength;

        // The corridor (highway + metro + river) stays flat across the whole terrain.
        private const float CorridorNorth = RoadZ + RoadHalfWidth;
        private const float CorridorSouth = RiverZ - RiverHalfWidth - RiverLipWidth;

        private static readonly float[] BridgeX = { -450f, -150f, 150f, 450f };
        private static readonly float[] MetroStationX = { -330f, 420f };

        /// <summary>Distance in meters from the transport corridor band (0 inside it).</summary>
        private static float CorridorDistance(float z) => Mathf.Max(0f, Mathf.Max(CorridorSouth - z, z - CorridorNorth));

        /// <summary>True where trees must not grow: river channel, walkways and metro deck.</summary>
        private static bool InInfrastructure(float x, float z)
        {
            if (Mathf.Abs(x) > RiverHalfLength + 10f) return false;
            if (Mathf.Abs(z - RiverZ) < RiverHalfWidth + RiverLipWidth + 2f) return true;
            return Mathf.Abs(z - MetroZ) < MetroDeckHalfWidth + 3f;
        }

        /// <summary>Cuts the river channel out of the terrain (the channel mesh sits in the hole).</summary>
        private static void CutRiverHoles(TerrainData data)
        {
            int resolution = data.holesResolution;
            float cell = TerrainSize / resolution;
            var solid = new bool[resolution, resolution];
            for (int iz = 0; iz < resolution; iz++)
            {
                float z0 = iz * cell - TerrainHalf, z1 = z0 + cell;
                bool riverRow = z1 > RiverZ - RiverHalfWidth && z0 < RiverZ + RiverHalfWidth;
                for (int ix = 0; ix < resolution; ix++)
                {
                    float x0 = ix * cell - TerrainHalf, x1 = x0 + cell;
                    solid[iz, ix] = !(riverRow && x1 > -RiverHalfLength && x0 < RiverHalfLength);
                }
            }
            data.SetHoles(0, 0, solid);
        }

        private static void BuildInfrastructure(Transform environment)
        {
            EnsureFolder(InfrastructureMeshFolder);
            var parent = new GameObject("Infrastructure").transform;
            parent.SetParent(environment, false);

            Material concrete = CreateMaterial("M_Infra_Concrete", new Color(0.58f, 0.58f, 0.56f));
            Material walkway = CreateMaterial("M_Infra_Walkway", new Color(0.64f, 0.63f, 0.60f));
            Material stained = CreateMaterial("M_Infra_Concrete_Stained", new Color(0.38f, 0.38f, 0.34f));
            Material water = CreateMaterial("M_River_Water", new Color(0.30f, 0.33f, 0.27f));
            water.SetFloat("_Smoothness", 0.85f);

            BuildRiver(parent, new[] { concrete, walkway, stained, water });
            BuildMetro(parent, new[]
            {
                concrete,
                CreateMaterial("M_Metro_Track", new Color(0.33f, 0.31f, 0.29f)),
                CreateMaterial("M_Metro_StationRoof", new Color(0.86f, 0.85f, 0.82f)),
                CreateMaterial("M_Metro_Train_Body", new Color(0.82f, 0.83f, 0.83f)),
                CreateMaterial("M_Metro_Train_Windows", new Color(0.16f, 0.18f, 0.20f)),
                CreateMaterial("M_Metro_Train_Accent", new Color(0.30f, 0.46f, 0.40f))
            });
        }

        // ------------------------------------------------------------------ river

        private static void BuildRiver(Transform parent, Material[] materials)
        {
            const int concrete = 0, walkway = 1, stained = 2, water = 3;
            var b = new MeshBuilder(materials.Length);
            float L = RiverHalfLength;
            float stainY = -3.4f;

            foreach (float side in new[] { -1f, 1f })
            {
                float top = RiverHalfWidth * side;
                float bed = RiverBedHalfWidth * side;
                float stainZ = Mathf.Lerp(top, bed, -stainY / RiverDepth);
                Vector3 inward = new Vector3(0f, 0.6f, -side);

                // Sloped channel wall: clean concrete above, water-stained below.
                b.AddQuad(V(-L, 0f, top), V(L, 0f, top), V(L, stainY, stainZ), V(-L, stainY, stainZ), inward, concrete);
                b.AddQuad(V(-L, stainY, stainZ), V(L, stainY, stainZ), V(L, -RiverDepth, bed), V(-L, -RiverDepth, bed), inward, stained);

                // Walkway along the edge (covers the stepped edge of the terrain hole) and railing wall.
                float outer = (RiverHalfWidth + RiverLipWidth) * side;
                b.AddQuad(V(-L - RiverLipWidth, 0.04f, top), V(L + RiverLipWidth, 0.04f, top),
                    V(L + RiverLipWidth, 0.04f, outer), V(-L - RiverLipWidth, 0.04f, outer), Vector3.up, walkway);
                b.AddBox(V(0f, 0.5f, top + 0.25f * side), Vector3.right, Vector3.forward, L, 0.5f, 0.25f, concrete, concrete);
            }

            // Water surface.
            float waterHalf = Mathf.Lerp(RiverHalfWidth, RiverBedHalfWidth, RiverWaterDepth / RiverDepth);
            b.AddQuad(V(-L, -RiverWaterDepth, -waterHalf), V(L, -RiverWaterDepth, -waterHalf),
                V(L, -RiverWaterDepth, waterHalf), V(-L, -RiverWaterDepth, waterHalf), Vector3.up, water);

            // Channel ends: end walls and walkway caps over the partial hole cells.
            foreach (float end in new[] { -1f, 1f })
            {
                float x = L * end;
                b.AddQuad(V(x, 0f, -RiverHalfWidth), V(x, 0f, RiverHalfWidth), V(x, -RiverDepth, RiverBedHalfWidth),
                    V(x, -RiverDepth, -RiverBedHalfWidth), new Vector3(-end, 0f, 0f), concrete);
                float outer = RiverHalfWidth + RiverLipWidth;
                b.AddQuad(V(x, 0.04f, -outer), V(x, 0.04f, outer), V(x + RiverLipWidth * end, 0.04f, outer),
                    V(x + RiverLipWidth * end, 0.04f, -outer), Vector3.up, walkway);
            }

            // Street bridges.
            foreach (float x in BridgeX)
            {
                float half = RiverHalfWidth + 1.5f;
                b.AddBox(V(x, 0.15f, 0f), Vector3.right, Vector3.forward, 6.5f, 0.45f, half, concrete, walkway);
                b.AddBox(V(x - 6.2f, 0.95f, 0f), Vector3.right, Vector3.forward, 0.3f, 0.5f, half, concrete, concrete);
                b.AddBox(V(x + 6.2f, 0.95f, 0f), Vector3.right, Vector3.forward, 0.3f, 0.5f, half, concrete, concrete);
            }

            CreateMeshObject(parent, "River_Canal", b, materials, InfrastructureMeshFolder, new Vector3(0f, 0f, RiverZ), true);
        }

        // ------------------------------------------------------------------ metro

        private static void BuildMetro(Transform parent, Material[] materials)
        {
            const int concrete = 0, track = 1, roof = 2;
            var b = new MeshBuilder(materials.Length);
            float L = RoadHalfLength;

            // Viaduct deck, parapets and the two tracks.
            float deckHalfHeight = (MetroDeckTop - MetroDeckBottom) / 2f;
            b.AddBox(V(0f, MetroDeckBottom + deckHalfHeight, 0f), Vector3.right, Vector3.forward, L, deckHalfHeight,
                MetroDeckHalfWidth, concrete, concrete);
            foreach (float side in new[] { -1f, 1f })
            {
                b.AddBox(V(0f, MetroDeckTop + 0.45f, (MetroDeckHalfWidth - 0.15f) * side), Vector3.right, Vector3.forward,
                    L, 0.45f, 0.15f, concrete, concrete);
                b.AddBox(V(0f, MetroDeckTop + 0.12f, 1.9f * side), Vector3.right, Vector3.forward, L, 0.12f, 1.1f, track, track);
            }

            // Columns with hammerhead capitals; catenary masts every other column.
            int columns = Mathf.FloorToInt(L / MetroColumnSpacing);
            for (int i = -columns; i <= columns; i++)
            {
                float x = i * MetroColumnSpacing;
                if (NearStation(x, 70f)) continue;
                b.AddBox(V(x, MetroDeckBottom / 2f - 0.3f, 0f), Vector3.right, Vector3.forward, 0.8f,
                    MetroDeckBottom / 2f + 0.3f, 0.8f, concrete, concrete);
                b.AddBox(V(x, MetroDeckBottom - 0.6f, 0f), Vector3.right, Vector3.forward, 0.9f, 0.6f, 3.6f, concrete, concrete);
                if (i % 2 == 0)
                    b.AddBox(V(x, MetroDeckTop + 2.6f, 0f), Vector3.right, Vector3.forward, 0.12f, 2.6f, 0.12f, track, track);
            }

            foreach (float x in MetroStationX) AddStation(b, x, concrete, roof, track);

            AddTrain(b, MetroStationX[1] - 12f, 3, 1.9f);
            AddTrain(b, -60f, 3, -1.9f);

            CreateMeshObject(parent, "Metro_Line", b, materials, InfrastructureMeshFolder, new Vector3(0f, 0f, MetroZ), true);
        }

        private static bool NearStation(float x, float halfLength)
        {
            foreach (float s in MetroStationX)
                if (Mathf.Abs(x - s) < halfLength) return true;
            return false;
        }

        /// <summary>Elevated station: wide platform on twin columns, light roof on posts, stair tower.</summary>
        private static void AddStation(MeshBuilder b, float x, int concrete, int roof, int dark)
        {
            const float halfLength = 60f, halfWidth = 9f;
            float deckHalfHeight = (MetroDeckTop - MetroDeckBottom) / 2f;
            // Top sits 4 cm above the viaduct deck to avoid z-fighting.
            b.AddBox(V(x, MetroDeckBottom + deckHalfHeight, 0f), Vector3.right, Vector3.forward, halfLength,
                deckHalfHeight + 0.04f, halfWidth, concrete, concrete);

            for (float px = x - halfLength + 6f; px <= x + halfLength - 5f; px += 18f)
            foreach (float side in new[] { -1f, 1f })
            {
                b.AddBox(V(px, MetroDeckBottom / 2f, 5.5f * side), Vector3.right, Vector3.forward, 0.7f,
                    MetroDeckBottom / 2f, 0.7f, concrete, concrete);
                b.AddBox(V(px, MetroDeckTop + 2.6f, (halfWidth - 0.6f) * side), Vector3.right, Vector3.forward, 0.18f,
                    2.6f, 0.18f, dark, dark);
            }

            // Two slightly offset roof planes give the canopy some shape.
            b.AddBox(V(x, MetroDeckTop + 5.45f, -4.6f), Vector3.right, Vector3.forward, halfLength - 4f, 0.25f, 5.2f, roof, roof);
            b.AddBox(V(x, MetroDeckTop + 5.95f, 4.6f), Vector3.right, Vector3.forward, halfLength - 4f, 0.25f, 5.2f, roof, roof);

            // Stair and elevator tower down to the walkway between metro and river.
            b.AddBox(V(x + 20f, (MetroDeckTop + 3f) / 2f, -halfWidth - 3f), Vector3.right, Vector3.forward, 5f,
                (MetroDeckTop + 3f) / 2f, 3f, concrete, roof);
        }

        /// <summary>Metro unit of several cars on the track at local z <paramref name="trackZ"/>.</summary>
        private static void AddTrain(MeshBuilder b, float startX, int cars, float trackZ)
        {
            const int body = 3, windows = 4, accent = 5;
            const float carHalfLength = 11f, halfWidth = 1.5f, gap = 1f;
            float floor = MetroDeckTop + 0.9f;
            for (int i = 0; i < cars; i++)
            {
                float x = startX + i * (2f * carHalfLength + gap);
                b.AddBox(V(x, floor + 1.85f, trackZ), Vector3.right, Vector3.forward, carHalfLength, 1.85f, halfWidth, body, body);
                b.AddBox(V(x, floor + 2.25f, trackZ), Vector3.right, Vector3.forward, carHalfLength - 0.6f, 0.65f,
                    halfWidth + 0.03f, windows, body);
                b.AddBox(V(x, floor + 0.75f, trackZ), Vector3.right, Vector3.forward, carHalfLength + 0.02f, 0.18f,
                    halfWidth + 0.03f, accent, body);
            }
        }

        // ------------------------------------------------------------------ shared

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>Saves the built mesh as an asset (in place) and adds a static renderer for it.</summary>
        private static GameObject CreateMeshObject(Transform parent, string name, MeshBuilder builder, Material[] materials,
            string folder, Vector3 position, bool castShadows)
        {
            Mesh mesh = SaveOrReplace(builder.ToMesh("MSH_" + name), $"{folder}/MSH_{name}.asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic |
                                                       StaticEditorFlags.OccluderStatic |
                                                       StaticEditorFlags.OccludeeStatic);
            return go;
        }
    }
}
