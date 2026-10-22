using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Procedurally rebuilds the CEDI Medellín environment: Aburrá valley terrain with painted
    /// layers, hillside brick neighbourhoods (barrios), slope forests, afternoon lighting and the
    /// global post-processing volume. Uses a fixed seed, so every run gives the same result.
    /// Also builds the transport corridor (river canal and metro, see the Infrastructure part).
    /// Leaves the CEDI lot, roads and existing valley trees untouched.
    /// </summary>
    public static partial class CediEnvironmentBuilder
    {
        private const string Root = "Assets/NOVAMARKET/";
        private const string TerrainFolder = Root + "Terrain";
        private const string TextureFolder = Root + "Terrain/Textures";
        private const string MaterialFolder = Root + "Materials/Environment";
        private const string TreeMeshFolder = Root + "Models/Generated/Trees";
        private const string BarrioMeshFolder = Root + "Models/Generated/Barrios";
        private const string TreePrefabFolder = Root + "Prefabs/Environment";
        private const string RenderingFolder = Root + "Rendering";

        private const float TerrainSize = 4400f;
        private const float TerrainHalf = TerrainSize / 2f;
        private const float MaxHeight = 1200f;
        private const int HeightRes = 1025;
        private const int AlphaRes = 1024;
        private const float RoadZ = -60f;
        private const float RoadHalfWidth = 6f;
        private const float RoadHalfLength = 1450f;
        private const int Seed = 1616;

        private const float HeightCell = TerrainSize / (HeightRes - 1);
        private const float AlphaCell = TerrainSize / AlphaRes;

        private const int LayerGrassLight = 0;
        private const int LayerGrassDark = 1;
        private const int LayerForest = 2;
        private const int LayerDirt = 3;

        private static float[,] heights;      // meters, [z, x] on the heightmap grid
        private static float[,] heightsBlur;  // blurred heights, for ravine/crest detection
        private static float[,] dirtStamp;    // [z, x] on the alphamap grid, 0..1
        private static float[,] treeBlock;    // [z, x] on the alphamap grid, >0 = no trees
        private static float[,] forestMap;    // [z, x] on the alphamap grid, 0..1
        private static float[,] slopeLandMap; // [z, x] on the alphamap grid, 0 = valley floor
        private static List<Rect> lotRects;

        [MenuItem("NOVAMARKET/Environment/Rebuild CEDI Medellin Environment")]
        public static void BuildAll()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject scenarioRoot = GameObject.Find("CEDI_Medellin");
            if (scene.name != "CEDI_Medellin" || scenarioRoot == null)
            {
                Debug.LogError("Open the CEDI_Medellin scene before rebuilding its environment.");
                return;
            }

            Transform environment = scenarioRoot.transform.Find("Environment");
            EnsureFolders();
            RemoveGenerated(environment);

            dirtStamp = new float[AlphaRes, AlphaRes];
            treeBlock = new float[AlphaRes, AlphaRes];
            lotRects = CollectLotRects();

            BuildHeights();
            TerrainData terrainData = CreateTerrainData();
            Terrain terrain = CreateTerrain(environment, terrainData);

            BuildInfrastructure(environment);
            BuildUrban(environment);
            BuildBarrios(environment);
            PaintTerrain(terrainData);
            PlaceTrees(terrainData, environment);
            terrain.Flush();

            SetupLighting();
            SetupVolume();
            ExportRoadNetwork(); // the traffic follows the streets just built
            ExportMetroLine();
            ExportCityBlocks();

            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            heights = heightsBlur = dirtStamp = treeBlock = forestMap = slopeLandMap = null;
            urbanTrees = null;
            builtBlocks = null;
            handPlacedTrees = null;
            Debug.Log("CEDI Medellin environment rebuilt.");
        }

        // ------------------------------------------------------------------ setup / cleanup

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { TerrainFolder, TextureFolder, MaterialFolder, TreeMeshFolder,
                         BarrioMeshFolder, TreePrefabFolder, RenderingFolder })
                EnsureFolder(folder);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void RemoveGenerated(Transform environment)
        {
            DestroyChild(environment, "Mountains");
            DestroyChild(environment, "Terrain/ValleyFloor");
            DestroyChild(environment, "Terrain/Terrain_Aburra");
            DestroyChild(environment, "Barrios");
            DestroyChild(environment, "Infrastructure");
            DestroyChild(environment, "IndustrialZone");
            DestroyChild(environment, "City");
            DestroyChild(environment, "DistantBuildings"); // blockout boxes, replaced by the urban builder
            GameObject volume = GameObject.Find("Global Volume");
            if (volume != null) Object.DestroyImmediate(volume);
        }

        private static void DestroyChild(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static List<Rect> CollectLotRects()
        {
            var rects = new List<Rect>();
            foreach (string name in new[] { "YardPavement", "ParkingPad", "ContainerPad", "YardLink", "GatePad",
                         "AccessRoad_ToGate", "FloorSlab" })
            {
                GameObject go = GameObject.Find(name);
                if (go == null || !go.TryGetComponent(out Renderer renderer)) continue;
                Bounds b = renderer.bounds;
                rects.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
            }
            return rects;
        }

        // ------------------------------------------------------------------ heights

        /// <summary>
        /// Height in meters at a world position. The valley floor stays at exactly 0 so the existing
        /// blockout sits on it; mountains rise higher to the east/west (Aburrá valley walls) and
        /// lower to the north/south, with ridged noise for crests and ravines (quebradas).
        /// </summary>
        private static float HeightAt(float x, float z)
        {
            float wobble = (Mathf.PerlinNoise(x * 0.0011f + 5.1f, z * 0.0011f + 9.7f) - 0.5f) * 200f;
            float eastWest = Smooth01((Mathf.Abs(x) - (850f + wobble)) / 1000f) * 640f;
            float northSouth = Smooth01((Mathf.Abs(z) - (480f + wobble * 0.8f)) / 950f) * 360f;
            float baseHeight = Mathf.Max(eastWest, northSouth) + 0.3f * Mathf.Min(eastWest, northSouth);
            if (baseHeight <= 0f) return 0f;
            baseHeight *= 0.75f + 0.5f * Fbm(x * 0.0007f + 50f, z * 0.0007f + 50f, 3); // rolling skyline

            // Ridges run downslope: stretched along x on the east/west walls, along z on the north/south ones.
            float eastWestWeight = eastWest / (eastWest + northSouth + 0.001f);
            float ridgesEW = Ridged(x * 0.0011f + 100f, z * 0.0042f + 100f, 4);
            float ridgesNS = Ridged(x * 0.0042f + 300f, z * 0.0011f + 300f, 4);
            float ridges = Mathf.Lerp(ridgesNS, ridgesEW, eastWestWeight);

            float mountain = Mathf.Clamp01(baseHeight / 80f);
            float height = baseHeight * (0.6f + 0.5f * ridges)
                           + mountain * (Fbm(x * 0.006f, z * 0.006f, 4) - 0.5f) * 30f;

            // Keep a pass open where the transport corridor (highway, metro, river) leaves the valley.
            height *= Smooth01((CorridorDistance(z) - 25f) / 240f);
            return Mathf.Max(0f, height);
        }

        private static void BuildHeights()
        {
            heights = new float[HeightRes, HeightRes];
            for (int iz = 0; iz < HeightRes; iz++)
            for (int ix = 0; ix < HeightRes; ix++)
                heights[iz, ix] = HeightAt(ix * HeightCell - TerrainHalf, iz * HeightCell - TerrainHalf);

            heightsBlur = BoxBlur(heights, 6);
        }

        private static TerrainData CreateTerrainData()
        {
            // Assets are updated in place: deleting and recreating one at the same path makes Unity
            // reload it from disk later and silently drop what was set in between (trees, splats).
            string path = TerrainFolder + "/TD_CEDI_Medellin.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, path);
            }
            data.heightmapResolution = HeightRes;
            data.alphamapResolution = AlphaRes;
            data.baseMapResolution = 1024;
            data.size = new Vector3(TerrainSize, MaxHeight, TerrainSize);

            var normalized = new float[HeightRes, HeightRes];
            for (int iz = 0; iz < HeightRes; iz++)
            for (int ix = 0; ix < HeightRes; ix++)
                normalized[iz, ix] = heights[iz, ix] / MaxHeight;
            data.SetHeights(0, 0, normalized);
            CutRiverHoles(data);

            data.terrainLayers = CreateTerrainLayers();
            data.treePrototypes = CreateTreePrototypes();
            return data;
        }

        private static Terrain CreateTerrain(Transform environment, TerrainData data)
        {
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_Aburra";
            go.transform.SetParent(environment.Find("Terrain"), false);
            go.transform.position = new Vector3(-TerrainHalf, 0f, -TerrainHalf);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic |
                                                       StaticEditorFlags.OccluderStatic |
                                                       StaticEditorFlags.OccludeeStatic);

            var terrain = go.GetComponent<Terrain>();
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            terrain.materialTemplate = pipeline.defaultTerrainMaterial;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 2000f;
            terrain.treeDistance = 4000f;
            terrain.treeBillboardDistance = 4000f;
            terrain.treeMaximumFullLODCount = 20000;
            terrain.drawInstanced = true;
            return terrain;
        }

        // ------------------------------------------------------------------ terrain layers

        private static TerrainLayer[] CreateTerrainLayers()
        {
            return new[]
            {
                CreateLayer("GrassLight", 32f, MakeGrassLight()),
                CreateLayer("GrassDark", 32f, MakeGrassDark()),
                CreateLayer("Forest", 30f, MakeForest()),
                CreateLayer("Dirt", 12f, MakeDirt())
            };
        }

        private static TerrainLayer CreateLayer(string name, float tileSize, Color[] pixels)
        {
            string texturePath = $"{TextureFolder}/T_Terrain_{name}.png";
            SaveTexture(texturePath, TextureSize, TextureSize, pixels);

            string layerPath = $"{TerrainFolder}/TL_{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.smoothness = 0f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private const int TextureSize = 512;

        private static Color[] MakeTexture(System.Func<float, float, Color> shade) =>
            MakeTexture(TextureSize, TextureSize, shade);

        private static Color[] MakeTexture(int width, int height, System.Func<float, float, Color> shade)
        {
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = shade((float)x / width, (float)y / height);
            return pixels;
        }

        /// <summary>Writes a generated texture as PNG and imports it as a tiling, mipmapped texture.</summary>
        private static Texture2D SaveTexture(string path, int width, int height, Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.maxTextureSize = Mathf.Max(width, height);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color[] MakeGrassLight() => MakeTexture((u, v) =>
        {
            float patches = TileFbm(u, v, 4, 4, 11);
            float fine = TileFbm(u, v, 64, 2, 12);
            Color c = Color.Lerp(new Color(0.40f, 0.55f, 0.25f), new Color(0.47f, 0.59f, 0.27f), patches);
            return c * (0.9f + 0.2f * fine);
        });

        private static Color[] MakeGrassDark() => MakeTexture((u, v) =>
        {
            float patches = TileFbm(u, v, 4, 4, 21);
            float fine = TileFbm(u, v, 64, 2, 22);
            Color c = Color.Lerp(new Color(0.25f, 0.38f, 0.17f), new Color(0.30f, 0.42f, 0.19f), patches);
            return c * (0.88f + 0.24f * fine);
        });

        private static Color[] MakeForest() => MakeTexture((u, v) =>
        {
            float crowns = TileFbm(u, v, 12, 3, 31);
            float fine = TileFbm(u, v, 48, 2, 32);
            Color c = Color.Lerp(new Color(0.07f, 0.15f, 0.07f), new Color(0.19f, 0.32f, 0.14f), Smooth01(crowns * 2.2f - 0.6f));
            return c * (0.85f + 0.3f * fine);
        });

        private static Color[] MakeDirt() => MakeTexture((u, v) =>
        {
            float patches = TileFbm(u, v, 6, 4, 41);
            float stones = TileFbm(u, v, 96, 1, 42);
            Color c = Color.Lerp(new Color(0.46f, 0.34f, 0.24f), new Color(0.57f, 0.43f, 0.31f), patches);
            if (stones > 0.78f) c = Color.Lerp(c, new Color(0.68f, 0.58f, 0.46f), 0.6f);
            return c;
        });

        // ------------------------------------------------------------------ barrios

        private static void BuildBarrios(Transform environment)
        {
            var parent = new GameObject("Barrios").transform;
            parent.SetParent(environment, false);

            Material[] materials =
            {
                CreateMaterial("M_House_Brick_A", new Color(0.58f, 0.33f, 0.24f)),
                CreateMaterial("M_House_Brick_B", new Color(0.50f, 0.28f, 0.21f)),
                CreateMaterial("M_House_Brick_C", new Color(0.64f, 0.41f, 0.31f)),
                CreateMaterial("M_House_Roof_Concrete", new Color(0.60f, 0.58f, 0.54f)),
                CreateMaterial("M_House_Roof_Dark", new Color(0.40f, 0.39f, 0.38f))
            };

            var rng = new System.Random(Seed);
            var centers = new List<Vector2>();
            for (int attempt = 0; attempt < 6000 && centers.Count < 16; attempt++)
            {
                float x = Range(rng, -1650f, 1650f);
                float z = Range(rng, -1650f, 1650f);
                float h = SampleHeight(x, z);
                float steep = SampleSteepness(x, z);
                if (h < 18f || h > 240f || steep < 7f || steep > 27f) continue;
                if (CorridorDistance(z) < 250f) continue;
                bool tooClose = false;
                foreach (Vector2 c in centers) tooClose |= Vector2.Distance(c, new Vector2(x, z)) < 300f;
                if (tooClose) continue;
                centers.Add(new Vector2(x, z));
            }

            for (int i = 0; i < centers.Count; i++)
                BuildBarrio(parent, $"Barrio_{i + 1:00}", centers[i], materials, rng);
        }

        private static void BuildBarrio(Transform parent, string name, Vector2 center, Material[] materials,
            System.Random rng)
        {
            Vector2 uphill = SampleGradient(center.x, center.y).normalized;
            var up3 = new Vector3(uphill.x, 0f, uphill.y);
            var along3 = new Vector3(uphill.y, 0f, -uphill.x);
            var origin = new Vector3(center.x, 0f, center.y);

            var builder = new MeshBuilder(materials.Length);
            int rows = rng.Next(4, 9);
            float rowLength = Range(rng, 70f, 150f);
            const float rowPitch = 14.5f; // house depth + street
            float halfDepth = rows * rowPitch / 2f;
            int houseCount = 0;

            for (int r = 0; r < rows; r++)
            {
                float rowOffset = (r + 0.5f) * rowPitch - halfDepth;
                float s = -rowLength / 2f;
                while (s < rowLength / 2f)
                {
                    float width = Range(rng, 5.5f, 8f);
                    float depth = Range(rng, 8f, 11f);
                    float gap = Range(rng, 0.3f, 1.2f);
                    float along = s + width / 2f;
                    s += width + gap;

                    float edgeNoise = Mathf.PerlinNoise(center.x * 0.01f + along * 0.03f, rowOffset * 0.05f + center.y * 0.01f);
                    float ellipse = Sq(along / (rowLength / 2f)) + Sq(rowOffset / halfDepth);
                    if (ellipse > 1f - 0.35f * edgeNoise || rng.NextDouble() < 0.1) continue;

                    Vector3 c = origin + up3 * (rowOffset + Range(rng, -0.8f, 0.8f)) + along3 * along;
                    float yaw = Range(rng, -5f, 5f) * Mathf.Deg2Rad;
                    Vector3 right = Rotate(along3, yaw);
                    Vector3 forward = Rotate(up3, yaw);

                    float hMin = float.MaxValue, hMax = float.MinValue;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Vector3 p = c + right * ((corner & 1) == 0 ? -width : width) / 2f
                                      + forward * ((corner & 2) == 0 ? -depth : depth) / 2f;
                        float h = SampleHeight(p.x, p.z);
                        hMin = Mathf.Min(hMin, h);
                        hMax = Mathf.Max(hMax, h);
                    }
                    if (hMax - hMin > 7f || hMin < 3f) continue;

                    double roll = rng.NextDouble();
                    int floors = roll < 0.3 ? 1 : roll < 0.75 ? 2 : 3;
                    float bottom = hMin - 1f;
                    float top = hMax + floors * 2.7f;
                    int brick = rng.Next(0, 3);
                    int roof = rng.NextDouble() < 0.7 ? 3 : 4;
                    Vector3 local = c - origin;
                    builder.AddBox(new Vector3(local.x, (bottom + top) / 2f, local.z), right, forward,
                        width / 2f, (top - bottom) / 2f, depth / 2f, brick, roof);

                    double extra = rng.NextDouble();
                    if (extra < 0.18)
                    {
                        // Rooftop room, same brick.
                        Vector3 offset = right * Range(rng, -1f, 1f) + forward * Range(rng, -1.5f, 1.5f);
                        builder.AddBox(new Vector3(local.x, top + 1.2f, local.z) + offset, right, forward,
                            width * 0.22f, 1.2f, depth * 0.2f, brick, roof);
                    }
                    else if (extra < 0.32)
                    {
                        // Water tank.
                        Vector3 offset = right * Range(rng, -1.5f, 1.5f) + forward * Range(rng, -2f, 2f);
                        builder.AddBox(new Vector3(local.x, top + 0.75f, local.z) + offset, right, forward,
                            0.75f, 0.75f, 0.75f, 3, 3);
                    }

                    Stamp(dirtStamp, c.x, c.z, Mathf.Max(width, depth) / 2f + 3f, 0.75f);
                    Stamp(treeBlock, c.x, c.z, Mathf.Max(width, depth) / 2f + 4f, 1f);
                    houseCount++;
                }
            }

            if (houseCount == 0) return;

            Mesh mesh = SaveOrReplace(builder.ToMesh("MSH_" + name), $"{BarrioMeshFolder}/MSH_{name}.asset");

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic |
                                                       StaticEditorFlags.OccluderStatic |
                                                       StaticEditorFlags.OccludeeStatic);

            // Dirt footpaths: one down to the valley, one up towards the ridge.
            StampPath(center + uphill * (-halfDepth), -1f, 260, rng);
            StampPath(center + uphill * halfDepth, 1f, 90, rng);
        }

        private static void StampPath(Vector2 start, float direction, int steps, System.Random rng)
        {
            Vector2 p = start;
            Vector2 heading = SampleGradient(p.x, p.y).normalized * direction;
            float phase = Range(rng, 0f, 100f);
            for (int i = 0; i < steps; i++)
            {
                Vector2 slope = SampleGradient(p.x, p.y);
                if (slope.sqrMagnitude < 1e-6f) break;
                float wander = (Mathf.PerlinNoise(phase, i * 0.08f) - 0.5f) * 2.2f;
                Vector2 target = slope.normalized * direction;
                target = new Vector2(target.x * Mathf.Cos(wander) - target.y * Mathf.Sin(wander),
                    target.x * Mathf.Sin(wander) + target.y * Mathf.Cos(wander));
                heading = (heading * 0.7f + target * 0.3f).normalized;
                p += heading * 5f;
                if (Mathf.Abs(p.x) > TerrainHalf - 10f || Mathf.Abs(p.y) > TerrainHalf - 10f) break;
                Stamp(dirtStamp, p.x, p.y, 3f, 0.85f);
                Stamp(treeBlock, p.x, p.y, 4f, 1f);
                if (direction < 0f && SampleHeight(p.x, p.y) < 1f) break;
            }
        }

        // ------------------------------------------------------------------ painting

        private static void PaintTerrain(TerrainData data)
        {
            var alpha = new float[AlphaRes, AlphaRes, 4];
            forestMap = new float[AlphaRes, AlphaRes];
            slopeLandMap = new float[AlphaRes, AlphaRes];

            for (int iz = 0; iz < AlphaRes; iz++)
            for (int ix = 0; ix < AlphaRes; ix++)
            {
                float x = (ix + 0.5f) * AlphaCell - TerrainHalf;
                float z = (iz + 0.5f) * AlphaCell - TerrainHalf;
                float h = SampleHeight(x, z);
                float steep = SampleSteepness(x, z);
                float blurred = SampleGrid(heightsBlur, x, z);
                float ravine = blurred - h;
                float crest = h - blurred;

                float n1 = Mathf.PerlinNoise(x * 0.004f + 3.3f, z * 0.004f + 8.1f);
                float n2 = Mathf.PerlinNoise(x * 0.021f + 1.7f, z * 0.021f + 4.4f);
                float n3 = Mathf.PerlinNoise(x * 0.08f + 0.5f, z * 0.08f + 0.5f);

                float slopeLand = SmoothStep(4f, 30f, h);
                float openCrest = SmoothStep(2f, 9f, crest);
                float forest = SmoothStep(0.36f, 0.56f, n1 * 0.7f + n2 * 0.3f) * slopeLand;
                forest = Mathf.Max(forest, SmoothStep(1.5f, 8f, ravine) * slopeLand); // quebradas
                forest = Mathf.Max(forest, SmoothStep(300f, 650f, h) * 0.85f);
                forest *= 1f - openCrest * 0.7f;

                float valleyPatch = (1f - slopeLand) * SmoothStep(0.45f, 0.75f, n1 * 0.6f + n2 * 0.4f) * 0.55f;
                float dark = slopeLand * (1f - forest) + valleyPatch;
                float light = (1f - slopeLand) * (1f - valleyPatch) + openCrest * slopeLand * 0.5f;

                float dirt = dirtStamp[iz, ix] * (0.75f + 0.25f * n3);
                dirt = Mathf.Max(dirt, LotDirt(x, z, n3));
                dirt = Mathf.Max(dirt, RoadShoulderDirt(x, z, n3));
                dirt = Mathf.Max(dirt, SmoothStep(48f, 60f, steep) * 0.5f);
                dirt = Mathf.Clamp01(dirt);

                float sum = light + dark + forest + 1e-5f;
                float rest = (1f - dirt) / sum;
                alpha[iz, ix, LayerGrassLight] = light * rest;
                alpha[iz, ix, LayerGrassDark] = dark * rest;
                alpha[iz, ix, LayerForest] = forest * rest;
                alpha[iz, ix, LayerDirt] = dirt;

                forestMap[iz, ix] = forest * (1f - dirt);
                slopeLandMap[iz, ix] = slopeLand;
            }

            data.SetAlphamaps(0, 0, alpha);
        }

        private static float LotDirt(float x, float z, float noise)
        {
            float distance = float.MaxValue;
            foreach (Rect r in lotRects)
            {
                float dx = Mathf.Max(r.xMin - x, 0f, x - r.xMax);
                float dz = Mathf.Max(r.yMin - z, 0f, z - r.yMax);
                distance = Mathf.Min(distance, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return 1f - SmoothStep(2.5f, 6f + noise * 4f, distance);
        }

        private static float RoadShoulderDirt(float x, float z, float noise)
        {
            if (Mathf.Abs(x) > RoadHalfLength) return 0f;
            float fromEdge = Mathf.Abs(z - RoadZ) - RoadHalfWidth;
            if (fromEdge < -1f) return 0f;
            return (1f - SmoothStep(1.5f, 5f, fromEdge)) * (0.45f + 0.35f * noise);
        }

        // ------------------------------------------------------------------ trees

        private struct TreeKind
        {
            public string Name;
            public Mesh Mesh;
            public Material Canopy;
        }

        private static TreeKind[] treeKinds;

        // Prototype indices, in the order of CreateTreePrototypes.
        private const int TreeBroadleafLight = 0;
        private const int TreeBroadleafMid = 1;
        private const int TreeBroadleafDark = 2;
        private const int TreeRoundMid = 3;
        private const int TreeConifer = 4;
        private const int TreeGuayacanYellow = 5;
        private const int TreeGuayacanPink = 6;

        private static TreePrototype[] CreateTreePrototypes()
        {
            var trunk = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Blockout/M_Tree_Trunk.mat");
            trunk.enableInstancing = true;
            EditorUtility.SetDirty(trunk);

            // Tree meshes and prefabs are deterministic: reuse them when present. Re-saving them
            // while the terrain references them makes Unity drop the terrain's tree instances.
            Mesh broadleaf = LoadOrCreateMesh("MSH_Tree_Broadleaf", () => TreeMeshes.Broadleaf(Seed + 1));
            Mesh round = LoadOrCreateMesh("MSH_Tree_Round", () => TreeMeshes.Round(Seed + 2));
            Mesh conifer = LoadOrCreateMesh("MSH_Tree_Conifer", TreeMeshes.Conifer);
            Mesh guayacan = LoadOrCreateMesh("MSH_Tree_Guayacan", () => TreeMeshes.Guayacan(Seed + 3));

            Material[] canopy = CanopyMaterials();
            treeKinds = new[]
            {
                new TreeKind { Name = "Tree_Broadleaf_Light", Mesh = broadleaf, Canopy = canopy[0] },
                new TreeKind { Name = "Tree_Broadleaf_Mid", Mesh = broadleaf, Canopy = canopy[1] },
                new TreeKind { Name = "Tree_Broadleaf_Dark", Mesh = round, Canopy = canopy[2] },
                new TreeKind { Name = "Tree_Round_Mid", Mesh = round, Canopy = canopy[3] },
                new TreeKind { Name = "Tree_Conifer", Mesh = conifer, Canopy = canopy[4] },
                new TreeKind { Name = "Tree_Guayacan_Amarillo", Mesh = guayacan, Canopy = canopy[5] },
                new TreeKind { Name = "Tree_Guayacan_Rosado", Mesh = guayacan, Canopy = canopy[6] }
            };

            var prototypes = new TreePrototype[treeKinds.Length];
            for (int i = 0; i < treeKinds.Length; i++)
            {
                string path = $"{TreePrefabFolder}/{treeKinds[i].Name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || !prefab.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                {
                    var temp = new GameObject(treeKinds[i].Name);
                    temp.AddComponent<MeshFilter>().sharedMesh = treeKinds[i].Mesh;
                    temp.AddComponent<MeshRenderer>().sharedMaterials = new[] { trunk, treeKinds[i].Canopy };
                    prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                    Object.DestroyImmediate(temp);
                }
                prototypes[i] = new TreePrototype { prefab = prefab, bendFactor = 0f };
            }
            return prototypes;
        }

        private const string WindShader = "NOVAMARKET/Wind Foliage";

        /// <summary>Canopy materials of the terrain trees: they sway with the global wind (WindController).</summary>
        private static Material[] CanopyMaterials() => new[]
        {
            CreateMaterial("M_Tree_Canopy_Light", new Color(0.36f, 0.52f, 0.20f), WindShader),
            CreateMaterial("M_Tree_Canopy_Mid", new Color(0.22f, 0.40f, 0.16f), WindShader),
            CreateMaterial("M_Tree_Canopy_Dark", new Color(0.13f, 0.27f, 0.11f), WindShader),
            CreateMaterial("M_Tree_Canopy_Olive", new Color(0.30f, 0.42f, 0.17f), WindShader),
            CreateMaterial("M_Tree_Conifer", new Color(0.12f, 0.24f, 0.14f), WindShader),
            CreateMaterial("M_Tree_Guayacan_Amarillo", new Color(0.96f, 0.76f, 0.10f), WindShader),
            CreateMaterial("M_Tree_Guayacan_Rosado", new Color(0.90f, 0.48f, 0.66f), WindShader)
        };

        /// <summary>Gives the tree canopies the wind shader without rebuilding the valley.</summary>
        [MenuItem("NOVAMARKET/Environment/Apply Tree Wind Shader")]
        public static void ApplyTreeWind()
        {
            CanopyMaterials();
            AssetDatabase.SaveAssets();
            // The terrain keeps its own copies of the tree materials: refresh them or it keeps the old shader.
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                terrain.terrainData.RefreshPrototypes();
                terrain.Flush();
            }
            Debug.Log("Tree canopies now sway with the wind.");
        }

        private static Mesh LoadOrCreateMesh(string name, System.Func<Mesh> create)
        {
            string path = $"{TreeMeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            Mesh mesh = create();
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Overwrites an existing asset in place (keeping its GUID and every reference to it),
        /// or creates it when missing. Deleting and recreating would break prefab references.
        /// </summary>
        private static T SaveOrReplace<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            string name = asset.name;
            if (asset is Mesh source && existing is Mesh target)
                CopyMesh(source, target); // CopySerialized leaves the mesh's GPU buffers stale
            else
                EditorUtility.CopySerialized(asset, existing);
            existing.name = name;
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void CopyMesh(Mesh source, Mesh target)
        {
            target.Clear();
            target.indexFormat = source.indexFormat;
            target.SetVertices(source.vertices);
            target.SetNormals(source.normals);
            target.SetUVs(0, source.uv);
            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i);
            target.RecalculateBounds();
        }

        private static void PlaceTrees(TerrainData data, Transform environment)
        {
            var rng = new System.Random(Seed + 7);
            var obstacles = new List<Rect>();
            foreach (Rect r in lotRects) obstacles.Add(Expand(r, 12f));
            var existing = new List<Vector2>();
            foreach (Transform t in environment.Find("Vegetation")) existing.Add(new Vector2(t.position.x, t.position.z));

            bool Blocked(float x, float z)
            {
                if (Mathf.Abs(z - RoadZ) < RoadHalfWidth + 7f || InInfrastructure(x, z)) return true;
                if (SampleAlpha(treeBlock, x, z) > 0f || SampleAlpha(dirtStamp, x, z) > 0.35f) return true;
                if (SampleSteepness(x, z) > 40f) return true;
                foreach (Rect r in obstacles) if (r.Contains(new Vector2(x, z))) return true;
                foreach (Vector2 p in existing) if ((p - new Vector2(x, z)).sqrMagnitude < 36f) return true;
                return false;
            }

            var trees = new List<TreeInstance>();

            // Slopes: dense forest patches and quebradas, scattered trees in pasture.
            for (int attempt = 0; attempt < 400000 && trees.Count < 8000; attempt++)
            {
                float x = Range(rng, -TerrainHalf + 5f, TerrainHalf - 5f);
                float z = Range(rng, -TerrainHalf + 5f, TerrainHalf - 5f);
                float forest = SampleAlpha(forestMap, x, z);
                float slopeLand = SampleAlpha(slopeLandMap, x, z);
                float chance = forest * 0.9f + slopeLand * 0.05f;
                if (rng.NextDouble() > chance || Blocked(x, z)) continue;

                float h = SampleHeight(x, z);
                double roll = rng.NextDouble();
                double guayacanChance = h < 200f ? 0.06 : 0.02;
                int kind = roll < guayacanChance ? TreeGuayacanYellow
                    : roll < guayacanChance * 2 ? TreeGuayacanPink
                    : roll < 0.30 ? TreeBroadleafDark
                    : roll < 0.52 ? TreeBroadleafMid
                    : roll < 0.70 ? TreeConifer
                    : roll < 0.85 ? TreeRoundMid
                    : TreeBroadleafLight;
                trees.Add(MakeTree(rng, x, z, kind));
            }

            // Valley floor: a few ornamental trees, many of them guayacanes.
            int valleyTrees = 0;
            for (int attempt = 0; attempt < 20000 && valleyTrees < 220; attempt++)
            {
                float x = Range(rng, -900f, 900f);
                float z = Range(rng, -560f, 560f);
                if (SampleAlpha(slopeLandMap, x, z) > 0.2f || Blocked(x, z)) continue;
                double roll = rng.NextDouble();
                int kind = roll < 0.18 ? TreeGuayacanYellow
                    : roll < 0.36 ? TreeGuayacanPink
                    : roll < 0.6 ? TreeBroadleafLight
                    : roll < 0.85 ? TreeBroadleafMid
                    : TreeRoundMid;
                trees.Add(MakeTree(rng, x, z, kind));
                valleyTrees++;
            }

            // Street, park and yard trees laid out by the urban builder.
            foreach (UrbanTree tree in urbanTrees)
                trees.Add(MakeTree(rng, tree.Position.x, tree.Position.y, tree.Kind));

            data.SetTreeInstances(trees.ToArray(), true);
            Debug.Log($"Trees placed: {trees.Count - valleyTrees - urbanTrees.Count} on slopes, {valleyTrees} in the valley, " +
                      $"{urbanTrees.Count} in town (terrain now holds {data.treeInstanceCount}).");
        }

        private static TreeInstance MakeTree(System.Random rng, float x, float z, int kind)
        {
            float scale = Range(rng, 0.7f, 1.4f);
            return new TreeInstance
            {
                position = new Vector3((x + TerrainHalf) / TerrainSize, 0f, (z + TerrainHalf) / TerrainSize),
                widthScale = scale * Range(rng, 0.85f, 1.15f),
                heightScale = scale,
                rotation = Range(rng, 0f, Mathf.PI * 2f),
                color = Color.white,
                lightmapColor = Color.white,
                prototypeIndex = kind
            };
        }

        // ------------------------------------------------------------------ lighting / volume

        private static void SetupLighting()
        {
            var sun = GameObject.Find("Directional Light").GetComponent<Light>();
            sun.transform.rotation = Quaternion.Euler(25f, 62f, 0f); // low sun from the west-southwest
            sun.color = new Color(1f, 0.85f, 0.70f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.GetUniversalAdditionalLightData().softShadowQuality = SoftShadowQuality.High;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.56f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.27f, 0.22f);

            Material sky = CreateMaterial("M_Sky_Afternoon", Color.white, "Skybox/Procedural");
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.25f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.58f, 0.68f));
            sky.SetColor("_GroundColor", new Color(0.62f, 0.60f, 0.55f)); // matches the haze at the far edge
            sky.SetFloat("_Exposure", 1.15f);
            RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.80f, 0.76f, 0.68f);
            RenderSettings.fogDensity = 0.00035f;

            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            pipeline.shadowDistance = 700f;
            pipeline.shadowCascadeCount = 4;
            pipeline.cascade4Split = new Vector3(0.04f, 0.12f, 0.35f);
            EditorUtility.SetDirty(pipeline);

            var cameraData = Camera.main.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        private static void SetupVolume()
        {
            string path = RenderingFolder + "/VP_CEDI_Medellin.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            var tonemapping = GetOrAddOverride<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = GetOrAddOverride<Bloom>(profile);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);

            var color = GetOrAddOverride<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(12f);
            color.saturation.Override(0f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.93f));

            var whiteBalance = GetOrAddOverride<WhiteBalance>(profile);
            whiteBalance.temperature.Override(5f);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("Global Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static Material CreateMaterial(string name, Color color, string shader = "Universal Render Pipeline/Lit")
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader.name != shader && Shader.Find(shader) != null)
                material.shader = Shader.Find(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
                material.SetColor("_Color", color);
                material.SetFloat("_Smoothness", 0.1f);
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ sampling helpers

        private static float SampleGrid(float[,] grid, float x, float z)
        {
            float gx = Mathf.Clamp((x + TerrainHalf) / HeightCell, 0f, HeightRes - 1.001f);
            float gz = Mathf.Clamp((z + TerrainHalf) / HeightCell, 0f, HeightRes - 1.001f);
            int ix = (int)gx, iz = (int)gz;
            float fx = gx - ix, fz = gz - iz;
            float a = Mathf.Lerp(grid[iz, ix], grid[iz, ix + 1], fx);
            float b = Mathf.Lerp(grid[iz + 1, ix], grid[iz + 1, ix + 1], fx);
            return Mathf.Lerp(a, b, fz);
        }

        private static float SampleHeight(float x, float z) => SampleGrid(heights, x, z);

        /// <summary>Uphill direction scaled by slope (rise over run).</summary>
        private static Vector2 SampleGradient(float x, float z)
        {
            const float d = 4f;
            return new Vector2(SampleHeight(x + d, z) - SampleHeight(x - d, z),
                SampleHeight(x, z + d) - SampleHeight(x, z - d)) / (2f * d);
        }

        private static float SampleSteepness(float x, float z) =>
            Mathf.Atan(SampleGradient(x, z).magnitude) * Mathf.Rad2Deg;

        private static float SampleAlpha(float[,] grid, float x, float z)
        {
            int ix = Mathf.Clamp((int)((x + TerrainHalf) / AlphaCell), 0, AlphaRes - 1);
            int iz = Mathf.Clamp((int)((z + TerrainHalf) / AlphaCell), 0, AlphaRes - 1);
            return grid[iz, ix];
        }

        private static void Stamp(float[,] grid, float x, float z, float radius, float value)
        {
            int cx = (int)((x + TerrainHalf) / AlphaCell);
            int cz = (int)((z + TerrainHalf) / AlphaCell);
            int r = Mathf.CeilToInt(radius / AlphaCell);
            for (int iz = cz - r; iz <= cz + r; iz++)
            for (int ix = cx - r; ix <= cx + r; ix++)
            {
                if (ix < 0 || iz < 0 || ix >= AlphaRes || iz >= AlphaRes) continue;
                float distance = new Vector2(ix - cx, iz - cz).magnitude * AlphaCell;
                float falloff = 1f - SmoothStep(radius * 0.6f, radius, distance);
                grid[iz, ix] = Mathf.Max(grid[iz, ix], value * falloff);
            }
        }

        private static float[,] BoxBlur(float[,] source, int radius)
        {
            int h = source.GetLength(0), w = source.GetLength(1);
            var temp = new float[h, w];
            var result = new float[h, w];
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0f; int count = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int xx = x + k;
                    if (xx < 0 || xx >= w) continue;
                    sum += source[z, xx]; count++;
                }
                temp[z, x] = sum / count;
            }
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0f; int count = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int zz = z + k;
                    if (zz < 0 || zz >= h) continue;
                    sum += temp[zz, x]; count++;
                }
                result[z, x] = sum / count;
            }
            return result;
        }

        // ------------------------------------------------------------------ math helpers

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float SmoothStep(float edge0, float edge1, float x) => Smooth01((x - edge0) / (edge1 - edge0));
        private static float Sq(float v) => v * v;
        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        private static Rect Expand(Rect r, float m) => Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);

        private static Vector3 Rotate(Vector3 v, float radians) =>
            new Vector3(v.x * Mathf.Cos(radians) - v.z * Mathf.Sin(radians), 0f,
                v.x * Mathf.Sin(radians) + v.z * Mathf.Cos(radians));

        private static float Ridged(float x, float z, int octaves)
        {
            float sum = 0f, amplitude = 0.5f, frequency = 1f, weight = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * frequency + 31.7f * i, z * frequency + 17.3f * i) * 2f - 1f);
                n *= n * weight;
                weight = Mathf.Clamp01(n * 1.5f);
                sum += n * amplitude;
                norm += amplitude;
                frequency *= 2.03f;
                amplitude *= 0.5f;
            }
            return Mathf.Clamp01(sum / norm * 1.6f);
        }

        private static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f, amplitude = 0.5f, frequency = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * frequency + 13.1f * i, z * frequency + 7.7f * i) * amplitude;
                norm += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Tileable fractal value noise on the unit square (used for terrain textures).</summary>
        private static float TileFbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amplitude = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += TileValueNoise(u * period, v * period, period, seed + i * 101) * amplitude;
                norm += amplitude;
                period *= 2;
                amplitude *= 0.5f;
            }
            return sum / norm;
        }

        private static float TileValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Smooth01(x - x0), fy = Smooth01(y - y0);
            float Corner(int cx, int cy)
            {
                cx = ((cx % period) + period) % period;
                cy = ((cy % period) + period) % period;
                uint hash = (uint)(cx * 374761393 + cy * 668265263 + seed * 2147483647);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                return ((hash ^ (hash >> 16)) & 0xFFFF) / 65535f;
            }
            float a = Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), fx);
            float b = Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }
    }
}
