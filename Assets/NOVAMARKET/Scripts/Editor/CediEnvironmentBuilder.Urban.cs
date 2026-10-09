using System.Collections.Generic;
using UnityEngine;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Valley floor on a street grid: an industrial zone of warehouses with yards around the CEDI
    /// and, further out, city blocks. The CEDI lot and its surroundings stay free. Everything is
    /// merged into a few meshes per 400 m chunk (ground without shadows, buildings with shadows).
    /// </summary>
    public static partial class CediEnvironmentBuilder
    {
        private const string UrbanMeshFolder = Root + "Models/Generated/Urban";
        private const string UrbanTextureFolder = Root + "Materials/Environment/Textures";

        // Street grid: 88 x 68 m blocks separated by 12 m streets, aligned with the highway.
        private const float GridPitchX = 100f;
        private const float GridPitchZ = 80f;
        private const float StreetWidth = 12f;
        private const float UrbanChunkSize = 400f;

        /// <summary>No block may touch this area: the CEDI lot plus a buffer around it.</summary>
        private static readonly Rect CediClearZone = Rect.MinMaxRect(-120f, -60f, 140f, 80f);
        private static readonly Vector2 IndustrialCenter = new Vector2(0f, -20f);
        private static readonly Vector2 IndustrialRadii = new Vector2(500f, 330f);

        // Submeshes of the ground palette.
        private const int GroundAsphalt = 0;
        private const int GroundSidewalk = 1;
        private const int GroundGrass = 2;
        private const int GroundYard = 3;

        // Submeshes of the building palette.
        private const int FacadeBrickA = 0;
        private const int FacadeCreamA = 3;
        private const int FacadeWhite = 5;
        private const int RoofFlat = 6;
        private const int RoofDark = 7;
        private const int TowerBrick = 8;
        private const int TowerCream = 9;
        private const int WarehouseWallA = 10;
        private const int MetalRoofA = 13;
        private const int MetalRoofB = 14;
        private const int DockDark = 15;
        private const int TruckWhite = 16;
        private const int ContainerRed = 17;

        // Meters covered by one tile of each generated texture.
        private static readonly Vector2 FacadeTile = new Vector2(3.5f, 3f);
        private static readonly Vector2 TowerTile = new Vector2(3f, 3f);
        private static readonly Vector2 WarehouseTile = new Vector2(4f, 16f);
        private static readonly Vector2 MetalRoofTile = new Vector2(12f, 4f);

        private sealed class UrbanChunk
        {
            public readonly MeshBuilder Ground = new MeshBuilder(4);
            public readonly MeshBuilder Buildings = new MeshBuilder(20);
        }

        private struct UrbanTree
        {
            public Vector2 Position;
            public int Kind;
        }

        private static List<UrbanTree> urbanTrees;
        private static List<Vector2> handPlacedTrees;

        private static void BuildUrban(Transform environment)
        {
            EnsureFolder(UrbanMeshFolder);
            EnsureFolder(UrbanTextureFolder);
            Material[] groundPalette = CreateGroundPalette();
            Material[] buildingPalette = CreateBuildingPalette();

            urbanTrees = new List<UrbanTree>();
            handPlacedTrees = new List<Vector2>();
            foreach (Transform t in environment.Find("Vegetation")) handPlacedTrees.Add(new Vector2(t.position.x, t.position.z));

            var industrial = new Dictionary<Vector2Int, UrbanChunk>();
            var rng = new System.Random(Seed + 11);
            int industrialBlocks = 0;

            foreach (Rect block in EnumerateBlocks())
            {
                Rect cell = Expand(block, StreetWidth / 2f);
                if (!IsBuildable(cell)) continue;
                if (!IsIndustrial(block.center)) continue;

                UrbanChunk chunk = GetChunk(industrial, block.center);
                AddGroundPlate(chunk.Ground, cell, 0.03f, GroundAsphalt);
                StampRect(treeBlock, cell);
                BuildIndustrialBlock(chunk, block, rng);
                industrialBlocks++;
            }

            // Streets that cross the corridor to the river bridges.
            foreach (float x in BridgeX)
            {
                UrbanChunk chunk = GetChunk(industrial, new Vector2(x, MetroZ));
                AddGroundPlate(chunk.Ground, Rect.MinMaxRect(x - 6f, RiverZ + RiverHalfWidth + RiverLipWidth,
                    x + 6f, RoadZ - RoadHalfWidth), 0.03f, GroundAsphalt);
            }

            CreateChunkObjects(environment, "IndustrialZone", "Industrial", industrial, groundPalette, buildingPalette);
            Debug.Log($"Urban: {industrialBlocks} industrial blocks.");
        }

        // ------------------------------------------------------------------ grid

        /// <summary>Blocks north of the highway and south of the river, column by column.</summary>
        private static IEnumerable<Rect> EnumerateBlocks()
        {
            float blockX = GridPitchX - StreetWidth, blockZ = GridPitchZ - StreetWidth;
            for (int i = -10; i <= 10; i++)
            {
                float x0 = i * GridPitchX - GridPitchX / 2f + StreetWidth / 2f;
                for (int j = 0; j < 8; j++)
                {
                    float z0 = CorridorNorth + StreetWidth / 2f + j * GridPitchZ;
                    yield return new Rect(x0, z0, blockX, blockZ);
                }
                for (int k = 0; k < 8; k++)
                {
                    float z1 = CorridorSouth - StreetWidth / 2f - k * GridPitchZ;
                    yield return new Rect(x0, z1 - blockZ, blockX, blockZ);
                }
            }
        }

        /// <summary>A cell is buildable on the flat valley floor, away from the CEDI.</summary>
        private static bool IsBuildable(Rect cell)
        {
            if (cell.Overlaps(CediClearZone) || cell.xMax > RoadHalfLength || cell.xMin < -RoadHalfLength) return false;
            for (int sz = 0; sz <= 4; sz++)
            for (int sx = 0; sx <= 4; sx++)
                if (SampleHeight(cell.xMin + cell.width * sx / 4f, cell.yMin + cell.height * sz / 4f) > 0.05f)
                    return false;
            return true;
        }

        private static bool IsIndustrial(Vector2 center)
        {
            Vector2 d = (center - IndustrialCenter) / IndustrialRadii;
            float noise = (Mathf.PerlinNoise(center.x * 0.004f + 7f, center.y * 0.004f + 3f) - 0.5f) * 0.6f;
            return d.sqrMagnitude + noise < 1f;
        }

        private static UrbanChunk GetChunk(Dictionary<Vector2Int, UrbanChunk> chunks, Vector2 position)
        {
            var key = new Vector2Int(Mathf.FloorToInt(position.x / UrbanChunkSize), Mathf.FloorToInt(position.y / UrbanChunkSize));
            if (!chunks.TryGetValue(key, out UrbanChunk chunk)) chunks[key] = chunk = new UrbanChunk();
            return chunk;
        }

        private static void CreateChunkObjects(Transform environment, string groupName, string prefix,
            Dictionary<Vector2Int, UrbanChunk> chunks, Material[] groundPalette, Material[] buildingPalette)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(environment, false);
            foreach (KeyValuePair<Vector2Int, UrbanChunk> pair in chunks)
            {
                string suffix = $"{pair.Key.x + 5}_{pair.Key.y + 5}"; // stable names, so mesh assets update in place
                CreateMeshObject(group, $"{prefix}_Ground_{suffix}", pair.Value.Ground, groundPalette, UrbanMeshFolder,
                    Vector3.zero, false);
                if (pair.Value.Buildings.VertexCount > 0)
                    CreateMeshObject(group, $"{prefix}_Buildings_{suffix}", pair.Value.Buildings, buildingPalette,
                        UrbanMeshFolder, Vector3.zero, true);
            }
        }

        private static bool OverlapsHandPlacedTree(Rect footprint)
        {
            Rect grown = Expand(footprint, 4f);
            foreach (Vector2 p in handPlacedTrees)
                if (grown.Contains(p)) return true;
            return false;
        }

        private static void AddUrbanTree(float x, float z, int kind) =>
            urbanTrees.Add(new UrbanTree { Position = new Vector2(x, z), Kind = kind });

        // ------------------------------------------------------------------ industrial zone

        /// <summary>Splits a block into 1-3 parcels separated by internal service lanes.</summary>
        private static void BuildIndustrialBlock(UrbanChunk chunk, Rect block, System.Random rng)
        {
            const float lane = 8f;
            double roll = rng.NextDouble();
            int parcels = roll < 0.3 ? 1 : roll < 0.75 ? 2 : 3;
            bool splitAlongX = parcels == 3 || rng.NextDouble() < 0.6;

            float length = splitAlongX ? block.width : block.height;
            float size = (length - lane * (parcels - 1)) / parcels;
            for (int p = 0; p < parcels; p++)
            {
                float start = p * (size + lane);
                Rect parcel = splitAlongX
                    ? new Rect(block.xMin + start, block.yMin, size, block.height)
                    : new Rect(block.xMin, block.yMin + start, block.width, size);
                BuildWarehouseParcel(chunk, parcel, splitAlongX, rng);
            }
        }

        /// <summary>
        /// Concrete yard with a warehouse (metal gable roof), loading docks facing the yard and,
        /// sometimes, an office annex, parked trucks or containers.
        /// </summary>
        private static void BuildWarehouseParcel(UrbanChunk chunk, Rect parcel, bool yardAlongZ, System.Random rng)
        {
            // Docks face a street: north/south when parcels sit side by side, else east/west.
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            Vector3 outward = yardAlongZ ? new Vector3(0f, 0f, side) : new Vector3(side, 0f, 0f);
            Vector3 along = yardAlongZ ? Vector3.right : Vector3.forward;
            float depth = yardAlongZ ? parcel.height : parcel.width;
            float yardDepth = Mathf.Min(Range(rng, 15f, 24f), depth * 0.42f);
            const float inset = 3f;

            Rect footprint = parcel;
            if (yardAlongZ)
            {
                footprint.xMin += inset; footprint.xMax -= inset;
                if (side > 0f) { footprint.yMin += inset; footprint.yMax -= yardDepth; }
                else { footprint.yMin += yardDepth; footprint.yMax -= inset; }
            }
            else
            {
                footprint.yMin += inset; footprint.yMax -= inset;
                if (side > 0f) { footprint.xMin += inset; footprint.xMax -= yardDepth; }
                else { footprint.xMin += yardDepth; footprint.xMax -= inset; }
            }
            if (footprint.width < 12f || footprint.height < 12f || OverlapsHandPlacedTree(footprint))
            {
                // No room for a warehouse (or a hand-placed tree is in the way): leave a green lot.
                AddGroundPlate(chunk.Ground, parcel, 0.12f, GroundGrass);
                return;
            }
            AddGroundPlate(chunk.Ground, parcel, 0.12f, GroundYard);

            MeshBuilder b = chunk.Buildings;
            float eave = Range(rng, 8f, 13f);
            int wall = WarehouseWallA + rng.Next(3);
            int roof = rng.NextDouble() < 0.7 ? MetalRoofA : MetalRoofB;
            var center = new Vector3(footprint.center.x, 0f, footprint.center.y);
            b.AddTexturedBox(center + Vector3.up * (eave / 2f), Vector3.right, Vector3.forward, footprint.width / 2f,
                eave / 2f, footprint.height / 2f, wall, roof, WarehouseTile);

            bool ridgeAlongX = footprint.width >= footprint.height;
            float halfLong = (ridgeAlongX ? footprint.width : footprint.height) / 2f + 0.5f;
            float halfShort = (ridgeAlongX ? footprint.height : footprint.width) / 2f + 0.5f;
            b.AddGableRoof(center, ridgeAlongX ? Vector3.right : Vector3.forward,
                ridgeAlongX ? Vector3.forward : Vector3.right, halfLong, halfShort, eave,
                halfShort * Mathf.Tan(6f * Mathf.Deg2Rad), roof, wall, MetalRoofTile);

            // Dock wall: the warehouse side that faces the yard.
            float wallLength = yardAlongZ ? footprint.width : footprint.height;
            float halfDepth = (yardAlongZ ? footprint.height : footprint.width) / 2f;
            Vector3 wallCenter = center + outward * halfDepth;

            // Office annex at one end of the dock wall.
            float officeLength = 0f;
            if (rng.NextDouble() < 0.55 && wallLength > 30f)
            {
                officeLength = Range(rng, 10f, 16f);
                float officeDepth = Range(rng, 7f, 9f);
                Vector3 officeCenter = wallCenter - along * (wallLength / 2f - officeLength / 2f) + outward * (officeDepth / 2f);
                int facade = rng.NextDouble() < 0.5 ? FacadeWhite : FacadeCreamA;
                b.AddTexturedBox(officeCenter + Vector3.up * 3.3f, along, outward, officeLength / 2f, 3.3f,
                    officeDepth / 2f, facade, RoofFlat, FacadeTile);
            }

            bool containers = yardDepth > 16f && rng.NextDouble() < 0.3;
            const float dockPitch = 5f;
            float dockStart = -wallLength / 2f + officeLength + 4f;
            for (float s = dockStart; s < wallLength / 2f - 3.5f; s += dockPitch)
            {
                Vector3 door = wallCenter + along * (s + 1.5f);
                b.AddBox(door + Vector3.up * 2.7f + outward * 0.08f, along, outward, 1.5f, 1.7f, 0.1f, DockDark, DockDark);
                if (!containers && yardDepth > 17f && rng.NextDouble() < 0.35) AddTruck(b, door, outward, along, rng);
            }

            if (containers)
            {
                int count = rng.Next(3, 8);
                Vector3 rowStart = wallCenter + outward * (yardDepth - 5f) - along * (wallLength / 2f - 4f);
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = rowStart + along * (i * 2.7f);
                    int color = ContainerRed + rng.Next(3);
                    b.AddBox(p + Vector3.up * 1.3f, along, outward, 1.22f, 1.3f, 3.03f, color, color);
                    if (rng.NextDouble() < 0.4)
                        b.AddBox(p + Vector3.up * 3.9f, along, outward, 1.22f, 1.3f, 3.03f, ContainerRed + rng.Next(3), color);
                }
            }

            // A couple of trees at the outer corners of the yard.
            Vector3 yardEdge = wallCenter + outward * (yardDepth - 1.5f);
            foreach (float end in new[] { -1f, 1f })
                if (rng.NextDouble() < 0.4)
                {
                    Vector3 p = yardEdge + along * end * (wallLength / 2f + 1f);
                    AddUrbanTree(p.x, p.z, rng.NextDouble() < 0.3 ? TreeGuayacanYellow : TreeBroadleafMid);
                }
        }

        /// <summary>Semi-trailer backed up to a dock door: white trailer and a colored cab.</summary>
        private static void AddTruck(MeshBuilder b, Vector3 door, Vector3 outward, Vector3 along, System.Random rng)
        {
            Vector3 trailer = door + outward * 7.3f;
            b.AddBox(trailer + Vector3.up * 2.55f, along, outward, 1.25f, 1.45f, 6.8f, TruckWhite, TruckWhite);
            int cabColor = rng.NextDouble() < 0.3 ? TruckWhite : ContainerRed + rng.Next(3);
            Vector3 cab = door + outward * 15.6f;
            b.AddBox(cab + Vector3.up * 1.75f, along, outward, 1.25f, 1.45f, 1.2f, cabColor, cabColor);
        }

        // ------------------------------------------------------------------ geometry helpers

        /// <summary>Flat slab on the ground, from y = 0 up to <paramref name="top"/>.</summary>
        private static void AddGroundPlate(MeshBuilder b, Rect rect, float top, int submesh) =>
            b.AddBox(new Vector3(rect.center.x, top / 2f, rect.center.y), Vector3.right, Vector3.forward,
                rect.width / 2f, top / 2f, rect.height / 2f, submesh, submesh);

        private static void StampRect(float[,] grid, Rect rect)
        {
            int x0 = Mathf.Clamp((int)((rect.xMin + TerrainHalf) / AlphaCell), 0, AlphaRes - 1);
            int x1 = Mathf.Clamp((int)((rect.xMax + TerrainHalf) / AlphaCell), 0, AlphaRes - 1);
            int z0 = Mathf.Clamp((int)((rect.yMin + TerrainHalf) / AlphaCell), 0, AlphaRes - 1);
            int z1 = Mathf.Clamp((int)((rect.yMax + TerrainHalf) / AlphaCell), 0, AlphaRes - 1);
            for (int iz = z0; iz <= z1; iz++)
            for (int ix = x0; ix <= x1; ix++)
                grid[iz, ix] = 1f;
        }

        // ------------------------------------------------------------------ palettes and textures

        private static Material[] CreateGroundPalette() => new[]
        {
            CreateMaterial("M_Urban_Asphalt", new Color(0.37f, 0.37f, 0.38f)),
            CreateMaterial("M_Urban_Sidewalk", new Color(0.64f, 0.62f, 0.58f)),
            CreateMaterial("M_Urban_Grass", new Color(0.42f, 0.54f, 0.27f)),
            CreateMaterial("M_Urban_Yard", new Color(0.56f, 0.56f, 0.55f))
        };

        /// <summary>Building materials, in the order of the Facade/Roof/Tower/Warehouse submesh constants.</summary>
        private static Material[] CreateBuildingPalette()
        {
            Texture2D facade = SaveTexture($"{UrbanTextureFolder}/T_Facade_Windows.png", 128, 128, MakeTexture(128, 128, FacadeShade));
            Texture2D tower = SaveTexture($"{UrbanTextureFolder}/T_Tower_Windows.png", 128, 128, MakeTexture(128, 128, TowerShade));
            Texture2D cladding = SaveTexture($"{UrbanTextureFolder}/T_Warehouse_Cladding.png", 64, 256, MakeTexture(64, 256, CladdingShade));
            Texture2D metal = SaveTexture($"{UrbanTextureFolder}/T_Metal_Roof.png", 512, 128, MakeTexture(512, 128, MetalRoofShade));

            return new[]
            {
                CreateTexturedMaterial("M_Facade_Brick_A", new Color(0.60f, 0.42f, 0.33f), facade),
                CreateTexturedMaterial("M_Facade_Brick_B", new Color(0.53f, 0.37f, 0.30f), facade),
                CreateTexturedMaterial("M_Facade_Brick_C", new Color(0.66f, 0.49f, 0.39f), facade),
                CreateTexturedMaterial("M_Facade_Cream_A", new Color(0.82f, 0.77f, 0.66f), facade),
                CreateTexturedMaterial("M_Facade_Cream_B", new Color(0.74f, 0.70f, 0.61f), facade),
                CreateTexturedMaterial("M_Facade_White", new Color(0.86f, 0.85f, 0.81f), facade),
                CreateMaterial("M_Roof_Flat", new Color(0.56f, 0.54f, 0.51f)),
                CreateMaterial("M_Roof_Dark", new Color(0.40f, 0.39f, 0.38f)),
                CreateTexturedMaterial("M_Tower_Brick", new Color(0.58f, 0.43f, 0.35f), tower),
                CreateTexturedMaterial("M_Tower_Cream", new Color(0.80f, 0.77f, 0.70f), tower),
                CreateTexturedMaterial("M_Warehouse_Wall_A", new Color(0.78f, 0.77f, 0.73f), cladding),
                CreateTexturedMaterial("M_Warehouse_Wall_B", new Color(0.64f, 0.67f, 0.68f), cladding),
                CreateTexturedMaterial("M_Warehouse_Wall_C", new Color(0.72f, 0.69f, 0.60f), cladding),
                CreateTexturedMaterial("M_Warehouse_Roof_Metal", new Color(0.70f, 0.71f, 0.72f), metal),
                CreateTexturedMaterial("M_Warehouse_Roof_Oxide", new Color(0.56f, 0.46f, 0.41f), metal),
                CreateMaterial("M_Dock_Door", new Color(0.24f, 0.25f, 0.27f)),
                CreateMaterial("M_Truck_White", new Color(0.86f, 0.86f, 0.84f)),
                CreateMaterial("M_Container_Muted_Red", new Color(0.55f, 0.27f, 0.22f)),
                CreateMaterial("M_Container_Muted_Blue", new Color(0.25f, 0.35f, 0.48f)),
                CreateMaterial("M_Container_Muted_Green", new Color(0.30f, 0.42f, 0.32f))
            };
        }

        private static Material CreateTexturedMaterial(string name, Color color, Texture2D texture)
        {
            Material material = CreateMaterial(name, color);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            return material;
        }

        // Textures are near-white so the material color tints them; one tile = one floor and bay.
        private static Color FacadeShade(float u, float v)
        {
            float wall = 0.93f + 0.07f * TileFbm(u, v, 8, 2, 51);
            bool window = u > 0.22f && u < 0.78f && v > 0.30f && v < 0.82f;
            if (window)
                return Mathf.Abs(u - 0.5f) < 0.016f ? Grey(0.55f) : new Color(0.19f, 0.22f, 0.26f) * (0.85f + 0.3f * v);
            if (u > 0.20f && u < 0.80f && v > 0.26f && v <= 0.30f) return Grey(1f); // sill
            return Grey(wall);
        }

        private static Color TowerShade(float u, float v)
        {
            float wall = 0.93f + 0.07f * TileFbm(u, v, 8, 2, 52);
            bool band = v > 0.32f && v < 0.92f && u > 0.05f && u < 0.95f;
            if (!band) return Grey(wall);
            float mullion = Mathf.Abs(u * 4f - Mathf.Round(u * 4f));
            return mullion < 0.04f ? Grey(0.6f) : new Color(0.23f, 0.27f, 0.32f) * (0.8f + 0.35f * v);
        }

        private static Color CladdingShade(float u, float v)
        {
            float streaks = 0.95f + 0.05f * TileFbm(u, v * 0.25f, 4, 2, 53);
            if (v < 0.075f) return Grey((0.70f + 0.06f * TileFbm(u, v, 16, 2, 54)) * streaks); // concrete plinth
            float rib = Mathf.Abs(u * 8f - Mathf.Round(u * 8f)) * 2f; // 0 at the rib, 1 between ribs
            return Grey((0.84f + 0.14f * Smooth01(rib)) * streaks);
        }

        private static Color MetalRoofShade(float u, float v)
        {
            if (Mathf.Abs(u - 0.5f) < 0.045f) return new Color(0.96f, 0.98f, 1f); // skylight strip
            if (v < 0.015f) return Grey(0.62f);                                   // panel seam
            float corrugation = 0.5f + 0.5f * Mathf.Sin(u * 48f * Mathf.PI * 2f);
            return Grey((0.80f + 0.12f * corrugation) * (0.96f + 0.04f * TileFbm(u, v, 6, 2, 55)));
        }

        private static Color Grey(float value) => new Color(value, value, value);
    }
}
