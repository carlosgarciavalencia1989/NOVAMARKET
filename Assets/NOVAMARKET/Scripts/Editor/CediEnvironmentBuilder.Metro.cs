using NovaMarket.Data;
using NovaMarket.Simulation.Ambient;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Metro part: exports the line the viaduct carries (tracks, rail height, stations) as
    /// <see cref="MetroLineData"/> for the metro simulation, and rebuilds only the viaduct mesh without
    /// rebuilding the whole valley.
    /// </summary>
    public static partial class CediEnvironmentBuilder
    {
        public const string MetroLinePath = Root + "Data/Ambient/MetroLine_CEDI_Medellin.asset";

        /// <summary>Tracks sit this far each side of the viaduct centre line.</summary>
        private const float MetroTrackOffset = 1.9f;

        /// <summary>Top of the rails (the track boxes stand 0.24 m on the deck).</summary>
        private const float MetroRailTop = MetroDeckTop + 0.24f;

        private const float StationHalfLength = 60f;

        private static Material[] MetroMaterials() => new[]
        {
            CreateMaterial("M_Infra_Concrete", new Color(0.58f, 0.58f, 0.56f)),
            CreateMaterial("M_Metro_Track", new Color(0.33f, 0.31f, 0.29f)),
            CreateMaterial("M_Metro_StationRoof", new Color(0.86f, 0.85f, 0.82f))
        };

        [MenuItem("NOVAMARKET/Ambient/Export Metro Line")]
        public static MetroLineData ExportMetroLine()
        {
            // Right-hand running: eastbound trains on the south track.
            var definition = new MetroLineDefinition(-RoadHalfLength, RoadHalfLength, MetroZ - MetroTrackOffset,
                MetroZ + MetroTrackOffset, MetroRailTop, MetroStationX, StationHalfLength);

            EnsureFolder(Root + "Data/Ambient");
            var data = AssetDatabase.LoadAssetAtPath<MetroLineData>(MetroLinePath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MetroLineData>();
                AssetDatabase.CreateAsset(data, MetroLinePath);
            }
            data.SetGeometry(definition);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        /// <summary>Rebuilds only the viaduct, stations and tracks (the trains are scene objects now).</summary>
        [MenuItem("NOVAMARKET/Environment/Rebuild Metro Line Only")]
        public static void RebuildMetroLine()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform infrastructure = GameObject.Find("CEDI_Medellin")?.transform.Find("Environment/Infrastructure");
            if (scene.name != "CEDI_Medellin" || infrastructure == null)
            {
                Debug.LogError("Open the CEDI_Medellin scene (with its generated environment) first.");
                return;
            }
            DestroyChild(infrastructure, "Metro_Line");
            BuildMetro(infrastructure, MetroMaterials());

            // The parked trains used to be part of the viaduct mesh; their materials are no longer used.
            foreach (string old in new[] { "M_Metro_Train_Body", "M_Metro_Train_Windows", "M_Metro_Train_Accent" })
                AssetDatabase.DeleteAsset($"{MaterialFolder}/{old}.mat");

            ExportMetroLine();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Metro line rebuilt (viaduct without trains) and exported.");
        }
    }
}
