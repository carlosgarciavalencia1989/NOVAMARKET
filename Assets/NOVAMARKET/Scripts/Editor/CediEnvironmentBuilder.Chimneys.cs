using System.Collections.Generic;
using NovaMarket.Data;
using NovaMarket.Simulation.Ambient;
using UnityEditor;
using UnityEngine;
using Num = System.Numerics;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Chimneys part: a few warehouses of the industrial zone get a concrete stack with a red band that rises
    /// through the roof. They are picked with their own random numbers, so the rest of the layout does not
    /// change, and their tops are saved as <see cref="IndustrialChimneysData"/> for the smoke.
    /// </summary>
    public static partial class CediEnvironmentBuilder
    {
        public const string ChimneysPath = Root + "Data/Ambient/IndustrialChimneys_CEDI_Medellin.asset";

        private const int MaxChimneys = 14;
        private const float ChimneyChance = 0.18f;
        private const float ChimneySpacing = 110f;

        private struct BuiltWarehouse
        {
            public Vector3 Center;
            public Vector3 Long;   // unit axis along the ridge
            public Vector3 Short;  // unit axis across the ridge
            public float HalfLong;
            public float HalfShort;
            public float Ridge;    // height of the ridge, meters
        }

        private static List<BuiltWarehouse> builtWarehouses;
        private static List<SmokeSource> builtChimneys;

        private static void RecordWarehouse(Vector3 center, bool ridgeAlongX, float halfLong, float halfShort, float ridge) =>
            builtWarehouses?.Add(new BuiltWarehouse
            {
                Center = center,
                Long = ridgeAlongX ? Vector3.right : Vector3.forward,
                Short = ridgeAlongX ? Vector3.forward : Vector3.right,
                HalfLong = halfLong,
                HalfShort = halfShort,
                Ridge = ridge
            });

        /// <summary>Adds a stack to some of the warehouses just built, at least <see cref="ChimneySpacing"/> apart.</summary>
        private static void BuildChimneys(Dictionary<Vector2Int, UrbanChunk> industrial)
        {
            builtChimneys = new List<SmokeSource>();
            var rng = new System.Random(Seed + 17);
            foreach (BuiltWarehouse w in builtWarehouses)
            {
                if (builtChimneys.Count >= MaxChimneys) break;
                if (rng.NextDouble() > ChimneyChance || w.HalfLong < 12f) continue;

                // Near one end of the building, off the ridge line.
                float alongSign = rng.NextDouble() < 0.5 ? -1f : 1f;
                float acrossSign = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 foot = w.Center + w.Long * (alongSign * w.HalfLong * 0.6f) + w.Short * (acrossSign * w.HalfShort * 0.35f);
                if (TooCloseToChimney(foot)) continue;

                float height = w.Ridge + Range(rng, 10f, 17f);
                float half = Range(rng, 0.8f, 1.1f);
                MeshBuilder b = GetChunk(industrial, new Vector2(foot.x, foot.z)).Buildings;
                float bandBottom = height - 3.2f, bandTop = height - 1.6f;
                b.AddBox(foot + Vector3.up * (bandBottom / 2f), Vector3.right, Vector3.forward, half, bandBottom / 2f, half,
                    RoofFlat, RoofFlat);
                b.AddBox(foot + Vector3.up * ((bandBottom + bandTop) / 2f), Vector3.right, Vector3.forward, half + 0.05f,
                    (bandTop - bandBottom) / 2f, half + 0.05f, ContainerRed, ContainerRed);
                b.AddBox(foot + Vector3.up * ((bandTop + height) / 2f), Vector3.right, Vector3.forward, half,
                    (height - bandTop) / 2f, half, RoofFlat, RoofDark);

                float strength = Range(rng, 0.7f, 1.2f);
                float phase = Range(rng, 0f, 2f * Mathf.PI);
                builtChimneys.Add(new SmokeSource(new Num.Vector3(foot.x, height, foot.z), strength, phase));
            }
        }

        private static bool TooCloseToChimney(Vector3 foot)
        {
            foreach (SmokeSource c in builtChimneys)
            {
                float dx = c.Top.X - foot.x, dz = c.Top.Z - foot.z;
                if (dx * dx + dz * dz < ChimneySpacing * ChimneySpacing) return true;
            }
            return false;
        }

        /// <summary>Saves the chimneys of the last urban build (only during a full rebuild).</summary>
        private static void ExportChimneys()
        {
            if (builtChimneys == null) return;
            EnsureFolder(Root + "Data/Ambient");
            var data = AssetDatabase.LoadAssetAtPath<IndustrialChimneysData>(ChimneysPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<IndustrialChimneysData>();
                AssetDatabase.CreateAsset(data, ChimneysPath);
            }
            data.SetSources(builtChimneys);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"Industrial chimneys exported: {builtChimneys.Count}.");
        }
    }
}
