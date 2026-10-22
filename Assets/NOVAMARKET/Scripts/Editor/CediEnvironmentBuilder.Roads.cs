using System.Collections.Generic;
using NovaMarket.Data;
using NovaMarket.Simulation.Ambient;
using UnityEditor;
using UnityEngine;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Road network part: the street grid the urban builder lays out (same blocks, same buildable test)
    /// becomes the traffic network, saved as <see cref="RoadNetworkData"/>. Rebuilding the environment
    /// exports it again, so streets and traffic always match.
    /// </summary>
    public static partial class CediEnvironmentBuilder
    {
        public const string RoadNetworkPath = Root + "Data/Ambient/RoadNetwork_CEDI_Medellin.asset";

        /// <summary>North-south grid lines that are avenues (they cross the river on the outer bridges).</summary>
        private static readonly float[] AvenueX = { -450f, 450f };

        /// <summary>East-west avenues: the second street north of the highway and the second south of the river.</summary>
        private static readonly float[] AvenueZ = { CorridorNorth + 2f * GridPitchZ, CorridorSouth - 2f * GridPitchZ };

        public const string CityBlocksPath = Root + "Data/Ambient/CityBlocks_CEDI_Medellin.asset";

        /// <summary>Saves the blocks recorded by the last urban build (only during a full rebuild).</summary>
        private static void ExportCityBlocks()
        {
            if (builtBlocks == null) return;
            EnsureFolder(Root + "Data/Ambient");
            var data = AssetDatabase.LoadAssetAtPath<CityBlocksData>(CityBlocksPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CityBlocksData>();
                AssetDatabase.CreateAsset(data, CityBlocksPath);
            }
            data.SetBlocks(builtBlocks);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"City blocks exported: {builtBlocks.Count}.");
        }

        [MenuItem("NOVAMARKET/Ambient/Export Road Network")]
        public static RoadNetworkData ExportRoadNetwork()
        {
            bool ownHeights = heights == null;
            if (ownHeights) BuildHeights();

            var cells = new List<StreetCell>();
            foreach (Rect block in EnumerateBlocks())
            {
                Rect cell = Expand(block, StreetWidth / 2f);
                if (IsBuildable(cell)) cells.Add(new StreetCell(cell.xMin, cell.yMin, cell.xMax, cell.yMax));
            }
            RoadNetworkDefinition definition = CityRoadLayout.Build(cells, CorridorNorth, CorridorSouth, BridgeX, AvenueX, AvenueZ);
            if (ownHeights) heights = heightsBlur = null;

            EnsureFolder(Root + "Data/Ambient");
            var data = AssetDatabase.LoadAssetAtPath<RoadNetworkData>(RoadNetworkPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<RoadNetworkData>();
                AssetDatabase.CreateAsset(data, RoadNetworkPath);
            }
            data.SetDefinition(definition);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            RoadNetwork network = RoadNetwork.Build(definition);
            Debug.Log($"Road network exported: {cells.Count} blocks, {definition.Roads.Count} roads, " +
                      $"{network.Lanes.Count} lanes, {network.Junctions.Count} crossings.");
            return data;
        }
    }
}
