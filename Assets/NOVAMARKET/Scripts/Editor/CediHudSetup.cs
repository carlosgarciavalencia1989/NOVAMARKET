using System.IO;
using NovaMarket.Interaction;
using NovaMarket.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Sets up the playable layer of the CEDI Medellín scene: the office and gate-booth blockouts,
    /// the selectable facilities, the selection system with its ring and (see the .Hud part) the HUD.
    /// Safe to run again: existing objects are reused and their settings refreshed.
    /// </summary>
    public static partial class CediHudSetup
    {
        private const string Root = "Assets/NOVAMARKET/";
        private const string BlockoutMaterials = Root + "Materials/Blockout/";
        private const string UiMaterialFolder = Root + "Materials/UI";
        private const string RingMaterialPath = UiMaterialFolder + "/M_UI_SelectionRing.mat";

        private static readonly Vector3 OfficeCenter = new Vector3(-48f, 0f, -23f);
        private static readonly Vector3 OfficeSize = new Vector3(14f, 7f, 9f);
        private static readonly Vector3 GateIslandCenter = new Vector3(0f, 0.3f, -38f);

        [MenuItem("NOVAMARKET/HUD/Setup HUD and Selection in CEDI Scene")]
        public static void SetupAll()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject scenarioRoot = GameObject.Find("CEDI_Medellin");
            if (scene.name != "CEDI_Medellin" || scenarioRoot == null)
            {
                Debug.LogError("Open the CEDI_Medellin scene before setting up the HUD.");
                return;
            }

            Transform cedi = scenarioRoot.transform.Find("CEDI");
            Transform systems = GetOrCreateChild(scenarioRoot.transform, "Systems");

            BuildOffice(cedi);
            BuildGuardBooth(cedi.Find("Gate"));
            SetupFacilities(cedi);
            SetupSelection(systems);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CEDI HUD and selection set up.");
        }

        // ------------------------------------------------------------------ blockouts

        /// <summary>Two-storey administrative office at the south-west corner of the yard.</summary>
        private static void BuildOffice(Transform cedi)
        {
            Transform office = GetOrCreateChild(cedi, "Office");
            office.position = OfficeCenter;

            float h = OfficeSize.y;
            float northFace = OfficeSize.z / 2f;
            Box(office, "Office_Body", new Vector3(0f, h / 2f, 0f), OfficeSize, "M_Building_Wall");
            Box(office, "Office_Roof", new Vector3(0f, h + 0.2f, 0f), new Vector3(OfficeSize.x + 0.6f, 0.4f, OfficeSize.z + 0.6f), "M_Concrete");
            Box(office, "Office_Band", new Vector3(0f, h - 0.5f, 0f), new Vector3(OfficeSize.x + 0.1f, 0.6f, OfficeSize.z + 0.1f), "M_Building_Accent");
            Box(office, "Office_Glazing_Ground", new Vector3(1.5f, 1.9f, northFace + 0.06f), new Vector3(9f, 1.4f, 0.1f), "M_Building_Glass", false);
            Box(office, "Office_Glazing_Upper", new Vector3(0f, 4.8f, northFace + 0.06f), new Vector3(12f, 1.4f, 0.1f), "M_Building_Glass", false);
            Box(office, "Office_Door", new Vector3(-5f, 1.2f, northFace + 0.06f), new Vector3(2f, 2.4f, 0.1f), "M_Building_Door", false);
            Box(office, "Office_Canopy", new Vector3(-5f, 2.8f, northFace + 1f), new Vector3(4f, 0.25f, 2f), "M_Building_Accent", false);
        }

        /// <summary>Guard booth on the gate island and one barrier arm per lane at the stop lines.</summary>
        private static void BuildGuardBooth(Transform gate)
        {
            Transform booth = GetOrCreateChild(gate, "GuardBooth");
            booth.position = GateIslandCenter;

            Box(booth, "Booth_Body", new Vector3(0f, 1.25f, 0f), new Vector3(1f, 2.5f, 2.2f), "M_Building_Wall");
            Box(booth, "Booth_Window", new Vector3(0f, 1.8f, 0f), new Vector3(1.04f, 0.8f, 1.8f), "M_Building_Glass", false);
            Box(booth, "Booth_Roof", new Vector3(0f, 2.6f, 0f), new Vector3(1.6f, 0.15f, 2.8f), "M_Building_Accent");
            Box(booth, "Barrier_Inbound", new Vector3(2.3f, 0.8f, -4.6f), new Vector3(3.4f, 0.1f, 0.1f), "M_Marking_Yellow", false);
            Box(booth, "Barrier_Outbound", new Vector3(-2.3f, 0.8f, 4.6f), new Vector3(3.4f, 0.1f, 0.1f), "M_Marking_Yellow", false);
        }

        private static void Box(Transform parent, string name, Vector3 localPosition, Vector3 size, string material,
            bool collider = true)
        {
            Transform box = parent.Find(name);
            if (box == null)
            {
                box = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                box.name = name;
                box.SetParent(parent, false);
            }
            box.localPosition = localPosition;
            box.localRotation = Quaternion.identity;
            box.localScale = size; // primitive cube is 1 m, so its scale is its real size in meters
            box.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(BlockoutMaterials + material + ".mat");

            var boxCollider = box.GetComponent<BoxCollider>();
            if (!collider && boxCollider != null) Object.DestroyImmediate(boxCollider);
        }

        // ------------------------------------------------------------------ selection

        private static void SetupFacilities(Transform cedi)
        {
            CediFacility warehouse = Facility(cedi.Find("Building"), CediFacilityIds.Warehouse, "Bodega principal");
            Facility(cedi.Find("Gate"), CediFacilityIds.Gate, "Portería");
            Facility(cedi.Find("Office"), CediFacilityIds.Office, "Oficina administrativa");
            Facility(cedi.Find("ContainerYard"), CediFacilityIds.ContainerYard, "Patio de contenedores");

            // The warehouse zones are siblings of Building in the hierarchy; clicks on them select it.
            foreach (string zone in new[] { "Receiving", "Storage", "Picking", "Shipping" })
            {
                var link = GetOrAdd<InteractableLink>(cedi.Find(zone).gameObject);
                SetField(link, "target", warehouse);
            }
        }

        private static CediFacility Facility(Transform target, string id, string displayName)
        {
            var facility = GetOrAdd<CediFacility>(target.gameObject);
            SetField(facility, "facilityId", id);
            SetField(facility, "displayName", displayName);
            return facility;
        }

        private static SelectionController SetupSelection(Transform systems)
        {
            Transform ringObject = GetOrCreateChild(systems, "SelectionRing");
            GetOrAdd<MeshFilter>(ringObject.gameObject);
            GetOrAdd<MeshRenderer>(ringObject.gameObject).sharedMaterial = GetRingMaterial();
            var ring = GetOrAdd<SelectionRing>(ringObject.gameObject);

            Transform selectionObject = GetOrCreateChild(systems, "Selection");
            var selection = GetOrAdd<SelectionController>(selectionObject.gameObject);
            SetField(selection, "viewCamera", Camera.main);
            SetField(selection, "ring", ring);
            return selection;
        }

        private static Material GetRingMaterial()
        {
            EnsureFolder(UiMaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, RingMaterialPath);
            }
            material.SetColor("_BaseColor", new Color(1f, 0.82f, 0.36f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ helpers

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T component) ? component : target.AddComponent<T>();

        /// <summary>Sets a private [SerializeField] through the serialization system, like the Inspector.</summary>
        private static void SetField(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            switch (value)
            {
                case string text: property.stringValue = text; break;
                case Object reference: property.objectReferenceValue = reference; break;
                case null: property.objectReferenceValue = null; break;
                default: throw new System.ArgumentException($"Unsupported value for {field}.");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
