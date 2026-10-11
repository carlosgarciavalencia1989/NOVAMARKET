using NovaMarket.Operation;
using NovaMarket.UI;
using UnityEditor;
using UnityEngine;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Operation part: vehicle materials and simple-shape prefabs (receiving truck, dispatch
    /// turbo, forklift, pallet), the gate barrier hinges and the <c>Systems/Operation</c> runner.
    /// Prefabs are rebuilt in place, so their GUIDs and scene references survive.
    /// </summary>
    public static partial class CediHudSetup
    {
        private const string VehicleMaterialFolder = Root + "Materials/Vehicles";
        private const string VehiclePrefabFolder = Root + "Prefabs/Vehicles";
        private const string EquipmentPrefabFolder = Root + "Prefabs/Equipment";

        private static void SetupOperation(Transform systems, Transform gate)
        {
            EnsureFolder(VehicleMaterialFolder);
            EnsureFolder(VehiclePrefabFolder);
            EnsureFolder(EquipmentPrefabFolder);

            GameObject pallet = SavePrefab(BuildPallet("Pallet"), EquipmentPrefabFolder + "/Pallet.prefab");
            VehicleVisual truck = SavePrefab(BuildReceivingTruck(), VehiclePrefabFolder + "/Truck_Receiving.prefab")
                .GetComponent<VehicleVisual>();
            VehicleVisual turbo = SavePrefab(BuildDispatchTurbo(), VehiclePrefabFolder + "/Turbo_Dispatch.prefab")
                .GetComponent<VehicleVisual>();
            VehicleVisual forklift = SavePrefab(BuildForklift(), VehiclePrefabFolder + "/Forklift.prefab")
                .GetComponent<VehicleVisual>();

            Transform booth = gate.Find("GuardBooth");
            GateBarrier inbound = BuildBarrier(booth, "Inbound", new Vector3(0.6f, 0.8f, -4.6f), 1f);
            GateBarrier outbound = BuildBarrier(booth, "Outbound", new Vector3(-0.6f, 0.8f, 4.6f), -1f);

            Transform operationObject = GetOrCreateChild(systems, "Operation");
            var runner = GetOrAdd<CediOperationRunner>(operationObject.gameObject);
            SetField(runner, "dataSource", Object.FindAnyObjectByType<HudDataSource>());
            SetField(runner, "receivingTruckPrefab", truck);
            SetField(runner, "dispatchTruckPrefab", turbo);
            SetField(runner, "forkliftPrefab", forklift);
            SetField(runner, "palletPrefab", pallet);
            SetField(runner, "inboundBarrier", inbound);
            SetField(runner, "outboundBarrier", outbound);

            var serialized = new SerializedObject(runner);
            SerializedProperty colours = serialized.FindProperty("containerMaterials");
            string[] containers = { "M_Container_Red", "M_Container_Blue", "M_Container_Green" };
            colours.arraySize = containers.Length;
            for (int i = 0; i < containers.Length; i++)
                colours.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>(BlockoutMaterials + containers[i] + ".mat");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ gate barriers

        /// <summary>Hinge at the island edge with the arm as its child, so the arm lifts around the hinge.</summary>
        private static GateBarrier BuildBarrier(Transform booth, string lane, Vector3 hinge, float direction)
        {
            Transform oldArm = booth.Find("Barrier_" + lane); // older setup: arm without a hinge
            if (oldArm != null) Object.DestroyImmediate(oldArm.gameObject);

            Transform pivot = GetOrCreateChild(booth, "BarrierHinge_" + lane);
            pivot.localPosition = hinge;
            pivot.localRotation = Quaternion.identity;
            Box(pivot, "Barrier_Arm", new Vector3(direction * 1.7f, 0f, 0f), new Vector3(3.4f, 0.1f, 0.1f), "M_Marking_Yellow", false);
            var barrier = GetOrAdd<GateBarrier>(pivot.gameObject);

            var serialized = new SerializedObject(barrier);
            serialized.FindProperty("openAngle").floatValue = direction * 80f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return barrier;
        }

        // ------------------------------------------------------------------ prefabs (pivot at ground centre, front +Z)

        private static GameObject BuildReceivingTruck()
        {
            var root = new GameObject("Truck_Receiving");
            Part(root, "Cab", new Vector3(0f, 1.95f, 6.8f), new Vector3(2.5f, 2.7f, 2.4f), VehicleMaterial("M_Vehicle_Cab", new Color(0.9f, 0.9f, 0.87f)));
            Part(root, "Windshield", new Vector3(0f, 2.6f, 8.03f), new Vector3(2.2f, 0.9f, 0.06f), VehicleMaterial("M_Vehicle_Glass", new Color(0.15f, 0.2f, 0.28f)));
            Part(root, "Chassis", new Vector3(0f, 0.85f, 0f), new Vector3(2.3f, 0.3f, 15.6f), VehicleMaterial("M_Vehicle_Chassis", new Color(0.16f, 0.16f, 0.17f)));
            Part(root, "Cargo", new Vector3(0f, 2.4f, -1.6f), new Vector3(2.5f, 2.6f, 12.2f),
                AssetDatabase.LoadAssetAtPath<Material>(BlockoutMaterials + "M_Container_Red.mat"));
            foreach (float z in new[] { 6.6f, -4.6f, -6.0f }) Wheels(root, z, 0.5f);
            Finish(root, new Vector3(2.6f, 3.8f, 16f), null);
            return root;
        }

        private static GameObject BuildDispatchTurbo()
        {
            var root = new GameObject("Turbo_Dispatch");
            Part(root, "Cab", new Vector3(0f, 1.7f, 3.4f), new Vector3(2.3f, 2.4f, 2.2f), VehicleMaterial("M_Vehicle_CabRed", new Color(0.72f, 0.13f, 0.1f)));
            Part(root, "Windshield", new Vector3(0f, 2.3f, 4.53f), new Vector3(2.0f, 0.8f, 0.06f), VehicleMaterial("M_Vehicle_Glass", new Color(0.15f, 0.2f, 0.28f)));
            Part(root, "Chassis", new Vector3(0f, 0.75f, 0f), new Vector3(2.1f, 0.3f, 8.8f), VehicleMaterial("M_Vehicle_Chassis", new Color(0.16f, 0.16f, 0.17f)));
            Part(root, "Cargo", new Vector3(0f, 2.2f, -1.0f), new Vector3(2.4f, 2.6f, 6.4f), VehicleMaterial("M_Vehicle_Box", new Color(0.85f, 0.85f, 0.82f)));
            foreach (float z in new[] { 3.3f, -2.8f }) Wheels(root, z, 0.45f);
            Finish(root, new Vector3(2.5f, 3.5f, 9f), null);
            return root;
        }

        private static GameObject BuildForklift()
        {
            var root = new GameObject("Forklift");
            Material yellow = VehicleMaterial("M_Vehicle_Forklift", new Color(0.95f, 0.72f, 0.1f));
            Material dark = VehicleMaterial("M_Vehicle_Chassis", new Color(0.16f, 0.16f, 0.17f));
            Part(root, "Body", new Vector3(0f, 0.75f, -0.3f), new Vector3(1.2f, 1.0f, 2.0f), yellow);
            Part(root, "Counterweight", new Vector3(0f, 0.9f, -1.25f), new Vector3(1.2f, 1.1f, 0.4f), dark);
            Part(root, "Seat", new Vector3(0f, 1.4f, -0.5f), new Vector3(0.6f, 0.4f, 0.5f), dark);
            Part(root, "GuardRoof", new Vector3(0f, 2.15f, -0.3f), new Vector3(1.1f, 0.08f, 1.3f), dark);
            foreach (float x in new[] { -0.5f, 0.5f })
            {
                Part(root, "GuardPostFront", new Vector3(x, 1.5f, 0.3f), new Vector3(0.08f, 1.3f, 0.08f), dark);
                Part(root, "GuardPostBack", new Vector3(x, 1.5f, -0.9f), new Vector3(0.08f, 1.3f, 0.08f), dark);
                Part(root, "Fork", new Vector3(x * 0.6f, 0.12f, 1.35f), new Vector3(0.12f, 0.06f, 1.1f), dark);
            }
            Part(root, "Mast", new Vector3(0f, 1.35f, 0.78f), new Vector3(1.0f, 2.4f, 0.12f), dark);
            Material tire = VehicleMaterial("M_Vehicle_Tire", new Color(0.06f, 0.06f, 0.06f));
            foreach (float z in new[] { 0.45f, -0.9f })
            foreach (float x in new[] { -0.62f, 0.62f })
                Part(root, "Wheel", new Vector3(x, 0.3f, z), new Vector3(0.25f, 0.6f, 0.6f), tire);

            GameObject carried = BuildPallet("CarriedPallet");
            carried.transform.SetParent(root.transform, false);
            carried.transform.localPosition = new Vector3(0f, 0.15f, 1.4f);
            carried.SetActive(false);

            Finish(root, new Vector3(1.3f, 2.3f, 3.3f), carried);
            return root;
        }

        /// <summary>Wooden pallet (1.0 × 1.2 m) with a cardboard load, pivot at the bottom centre.</summary>
        private static GameObject BuildPallet(string name)
        {
            var root = new GameObject(name);
            Part(root, "Base", new Vector3(0f, 0.07f, 0f), new Vector3(1.0f, 0.14f, 1.2f), VehicleMaterial("M_Pallet_Wood", new Color(0.62f, 0.45f, 0.27f)));
            Part(root, "Load", new Vector3(0f, 0.64f, 0f), new Vector3(0.95f, 1.0f, 1.1f), VehicleMaterial("M_Pallet_Load", new Color(0.76f, 0.62f, 0.42f)));
            return root;
        }

        private static void Wheels(GameObject root, float z, float radius)
        {
            Material tire = VehicleMaterial("M_Vehicle_Tire", new Color(0.06f, 0.06f, 0.06f));
            foreach (float x in new[] { -1.1f, 1.1f })
                Part(root, "Wheel", new Vector3(x, radius, z), new Vector3(0.4f, radius * 2f, radius * 2f), tire);
        }

        /// <summary>Cube part without collider (the root has one box collider for selection).</summary>
        private static void Part(GameObject root, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = size; // a primitive cube is 1 m, so its scale is its real size
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Finish(GameObject root, Vector3 size, GameObject carriedPallet)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y / 2f, 0f);
            box.size = size;
            var visual = root.AddComponent<VehicleVisual>();
            if (carriedPallet != null) SetField(visual, "carriedPallet", carriedPallet);
        }

        private static GameObject SavePrefab(GameObject temporary, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static Material VehicleMaterial(string name, Color color)
        {
            string path = $"{VehicleMaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.25f);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
}
