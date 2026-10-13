using System.Collections.Generic;
using NovaMarket.Simulation;
using NovaMarket.UI;
using UnityEngine;

namespace NovaMarket.Operation
{
    /// <summary>
    /// Shows the planning level's inventory in the warehouse: each SKU fills one rack row
    /// (RackRow_01 = first SKU …) with pallets in its colour, on top of the row.
    /// Pallets above the SKU's rack capacity are put on the aisle floor, so overflow is visible.
    /// It only mirrors <see cref="NovaMarket.Simulation.Planning.PlanningOperationBridge.PalletsBySku"/>.
    /// </summary>
    public class RackStockDisplay : MonoBehaviour
    {
        private const int Columns = 12;
        private const float FirstColumnX = -11.4f;
        private const float ColumnSpacing = 1.3f;
        private const float LineOffset = 0.55f;
        private const float LayerHeight = 1.3f;
        private const int Layers = 1; // the biggest SKU (perfume) needs 24 positions; a second layer would hit the roof

        /// <summary>Top of the RackRow blockouts: warehouse floor + 6 m.</summary>
        private const float RackTop = CediLayout.IndoorFloor + 6f;
        private const float FloorOffset = 1.0f;
        private const int MaxFloorPallets = Columns * 2;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private PlanningLevelController level;
        [SerializeField] private GameObject palletPrefab;
        [Tooltip("Colour of the load of each SKU, in catalogue order (labial, perfume, set, crema).")]
        [SerializeField] private Color[] skuColors =
        {
            new Color(0.93f, 0.42f, 0.40f),
            new Color(0.58f, 0.45f, 0.85f),
            new Color(0.96f, 0.76f, 0.30f),
            new Color(0.60f, 0.84f, 0.92f)
        };

        private readonly List<List<GameObject>> racked = new List<List<GameObject>>();
        private readonly List<List<GameObject>> onFloor = new List<List<GameObject>>();
        private int[] shown = new int[0];
        private Transform palletRoot;
        private MaterialPropertyBlock block;

        private void LateUpdate()
        {
            var bridge = level != null ? level.Bridge : null;
            if (bridge?.Simulation == null || palletPrefab == null) return;

            IReadOnlyList<int> pallets = bridge.PalletsBySku;
            if (!Changed(pallets)) return;
            for (int sku = 0; sku < pallets.Count && sku < ForkliftNetwork.Obstacles.Count - 1; sku++)
            {
                int rackPallets = bridge.Simulation.Skus[sku].PalletsFor(bridge.Simulation.RackCapacity(sku));
                int inRack = Mathf.Min(pallets[sku], Mathf.Min(rackPallets, SlotsPerRow));
                Show(racked, sku, inRack, slot => RackSlot(sku, slot));
                Show(onFloor, sku, Mathf.Min(pallets[sku] - inRack, MaxFloorPallets), slot => FloorSlot(sku, slot));
            }
        }

        private static int SlotsPerRow => Columns * 2 * Layers;

        private bool Changed(IReadOnlyList<int> pallets)
        {
            bool changed = shown.Length != pallets.Count;
            if (changed) shown = new int[pallets.Count];
            for (int i = 0; i < pallets.Count; i++)
            {
                if (shown[i] == pallets[i]) continue;
                shown[i] = pallets[i];
                changed = true;
            }
            return changed;
        }

        private void Show(List<List<GameObject>> pool, int sku, int count, System.Func<int, Vector3> place)
        {
            while (pool.Count <= sku) pool.Add(new List<GameObject>());
            List<GameObject> list = pool[sku];
            while (list.Count < count) list.Add(CreatePallet(sku, place(list.Count)));
            for (int i = 0; i < list.Count; i++)
                if (list[i].activeSelf != i < count) list[i].SetActive(i < count);
        }

        /// <summary>
        /// Slots on top of the rack row (the blockout racks are solid boxes, so pallets inside them
        /// would be hidden): two lines along the row, west to east, then a second layer.
        /// </summary>
        private static Vector3 RackSlot(int row, int slot)
        {
            var rack = ForkliftNetwork.Obstacles[row];
            int column = slot % Columns;
            bool north = slot / Columns % 2 == 1;
            int layer = slot / (Columns * 2);
            float z = (rack.min.Y + rack.max.Y) / 2f + (north ? LineOffset : -LineOffset);
            return new Vector3(FirstColumnX + column * ColumnSpacing, RackTop + layer * LayerHeight, z);
        }

        /// <summary>Overflow pallets stand on the aisle floor, along the south face of the row.</summary>
        private static Vector3 FloorSlot(int row, int slot)
        {
            var rack = ForkliftNetwork.Obstacles[row];
            float x = FirstColumnX + slot % Columns * ColumnSpacing;
            float y = CediLayout.IndoorFloor + (slot >= Columns ? LayerHeight : 0f); // second layer stacked on top
            return new Vector3(x, y, rack.min.Y - FloorOffset);
        }

        private GameObject CreatePallet(int sku, Vector3 position)
        {
            if (palletRoot == null)
            {
                palletRoot = new GameObject("RackPallets").transform;
                palletRoot.SetParent(transform, false);
            }
            GameObject pallet = Instantiate(palletPrefab, position, Quaternion.identity, palletRoot);
            pallet.name = $"RackPallet_{sku}";
            foreach (Collider part in pallet.GetComponentsInChildren<Collider>()) Destroy(part); // clicks reach the racks

            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColor, skuColors[sku % skuColors.Length]);
            foreach (Renderer part in pallet.GetComponentsInChildren<Renderer>())
                if (part.name == "Load") part.SetPropertyBlock(block);
            return pallet;
        }
    }
}
