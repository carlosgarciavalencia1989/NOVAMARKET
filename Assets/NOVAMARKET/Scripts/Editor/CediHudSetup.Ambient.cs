using NovaMarket.Ambient;
using NovaMarket.CameraControl;
using NovaMarket.Data;
using NovaMarket.Operation;
using NovaMarket.Simulation.Ambient;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Ambient traffic part (menu NOVAMARKET → Ambient → Setup Ambient Traffic): exports the road network,
    /// builds simple-shape models with two levels of detail (car, taxi, moto, bus, truck, traffic light,
    /// bus stop), the visual profile and settings assets, and the <c>Systems/Ambient</c> runner.
    /// Swapping a model for a 3D pack only means replacing a prefab in the visual profile.
    /// </summary>
    public static partial class CediHudSetup
    {
        private const string AmbientDataFolder = Root + "Data/Ambient";
        private const string AmbientPrefabFolder = Root + "Prefabs/Vehicles/Ambient";
        private const string FurniturePrefabFolder = Root + "Prefabs/Environment";
        private const float LineHeight = 0.045f;
        private const string PeoplePrefabFolder = Root + "Prefabs/Characters";

        private static readonly Color[] CarColours =
        {
            new Color(0.92f, 0.92f, 0.9f), new Color(0.72f, 0.73f, 0.74f), new Color(0.38f, 0.39f, 0.41f),
            new Color(0.08f, 0.08f, 0.09f), new Color(0.62f, 0.09f, 0.08f), new Color(0.14f, 0.27f, 0.52f),
            new Color(0.16f, 0.3f, 0.22f)
        };

        private static readonly Color[] MotoColours =
        {
            new Color(0.75f, 0.1f, 0.08f), new Color(0.07f, 0.07f, 0.08f), new Color(0.12f, 0.3f, 0.65f),
            new Color(0.9f, 0.9f, 0.88f)
        };

        [MenuItem("NOVAMARKET/Ambient/Setup Ambient Traffic (network, models, runner)")]
        public static void SetupAmbient()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject scenarioRoot = GameObject.Find("CEDI_Medellin");
            if (scene.name != "CEDI_Medellin" || scenarioRoot == null)
            {
                Debug.LogError("Open the CEDI_Medellin scene before setting up the ambient traffic.");
                return;
            }
            EnsureFolder(AmbientDataFolder);
            EnsureFolder(AmbientPrefabFolder);
            EnsureFolder(VehicleMaterialFolder);

            RoadNetworkData network = CediEnvironmentBuilder.ExportRoadNetwork();
            AmbientSettingsData settings = LoadOrCreate<AmbientSettingsData>(AmbientDataFolder + "/AmbientSettings.asset");
            FillPedestrianCounts(settings);
            AmbientVisualProfile visuals = BuildVisualProfile();

            Transform systems = GetOrCreateChild(scenarioRoot.transform, "Systems");
            Transform ambient = GetOrCreateChild(systems, "Ambient");
            var runner = GetOrAdd<AmbientTrafficRunner>(ambient.gameObject);
            SetField(runner, "network", network);
            SetField(runner, "settings", settings);
            SetField(runner, "visuals", visuals);
            SetField(runner, "cameraController", Object.FindAnyObjectByType<StrategyCameraController>());
            SetField(runner, "operation", Object.FindAnyObjectByType<CediOperationRunner>());
            var overlay = GetOrAdd<AmbientDebugOverlay>(ambient.gameObject);
            SetField(overlay, "runner", runner);

            var metro = GetOrAdd<MetroRunner>(ambient.gameObject);
            SetField(metro, "line", CediEnvironmentBuilder.ExportMetroLine());
            SetField(metro, "visuals", visuals);

            var people = GetOrAdd<PeopleRunner>(ambient.gameObject);
            SetField(people, "blocks", AssetDatabase.LoadAssetAtPath<CityBlocksData>(CediEnvironmentBuilder.CityBlocksPath));
            SetField(people, "settings", settings);
            SetField(people, "visuals", visuals);
            SetField(people, "traffic", runner);
            SetField(people, "metro", metro);
            SetField(people, "operation", Object.FindAnyObjectByType<CediOperationRunner>());
            SetField(people, "cameraController", Object.FindAnyObjectByType<StrategyCameraController>());
            SetField(overlay, "people", people);
            GetOrAdd<WindController>(ambient.gameObject);
            SetupSmoke(ambient);
            SetupDayLight(ambient, visuals);
            SetupSound(ambient);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Ambient traffic set up (Systems/Ambient).");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset; // keep the values tuned in the Inspector
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ------------------------------------------------------------------ visual profile

        private static AmbientVisualProfile BuildVisualProfile()
        {
            GameObject car = SavePrefab(BuildCar("Car_Sedan", false), AmbientPrefabFolder + "/Car_Sedan.prefab");
            GameObject taxi = SavePrefab(BuildCar("Car_Taxi", true), AmbientPrefabFolder + "/Car_Taxi.prefab");
            GameObject moto = SavePrefab(BuildMoto(), AmbientPrefabFolder + "/Moto.prefab");
            GameObject bus = SavePrefab(BuildBus(), AmbientPrefabFolder + "/Bus.prefab");
            GameObject truck = SavePrefab(BuildTruck(), AmbientPrefabFolder + "/Truck_City.prefab");
            GameObject light = SavePrefab(BuildTrafficLight(), FurniturePrefabFolder + "/TrafficLight_Approach.prefab");
            GameObject shelter = SavePrefab(BuildBusStop(), FurniturePrefabFolder + "/BusStop_Shelter.prefab");
            GameObject train = SavePrefab(BuildMetroTrain(3), AmbientPrefabFolder + "/Metro_Train.prefab");
            GameObject crosswalk = SavePrefab(BuildCrosswalk(), FurniturePrefabFolder + "/Crosswalk_Zebra.prefab");
            EnsureFolder(PeoplePrefabFolder);
            GameObject[] walkers =
            {
                SavePrefab(BuildPerson("Person_A", 1.0f), PeoplePrefabFolder + "/Person_A.prefab"),
                SavePrefab(BuildPerson("Person_B", 0.93f), PeoplePrefabFolder + "/Person_B.prefab")
            };

            AmbientVisualProfile profile = LoadOrCreate<AmbientVisualProfile>(AmbientDataFolder + "/AmbientVisuals.asset");
            var serialized = new SerializedObject(profile);
            SerializedProperty list = serialized.FindProperty("vehicles");
            var entries = new (AmbientVehicleKind Kind, GameObject Prefab, Material[] Colours)[]
            {
                (AmbientVehicleKind.Car, car, Palette("M_Car_", CarColours)),
                (AmbientVehicleKind.Taxi, taxi, new[] { VehicleMaterial("M_Car_TaxiYellow", new Color(0.98f, 0.78f, 0.05f)) }),
                (AmbientVehicleKind.Moto, moto, Palette("M_Moto_", MotoColours)),
                (AmbientVehicleKind.Bus, bus, new[]
                {
                    VehicleMaterial("M_Bus_Green", new Color(0.13f, 0.52f, 0.3f)),
                    VehicleMaterial("M_Bus_Blue", new Color(0.12f, 0.33f, 0.62f)),
                    VehicleMaterial("M_Bus_Orange", new Color(0.86f, 0.42f, 0.08f))
                }),
                (AmbientVehicleKind.Truck, truck, new[]
                {
                    VehicleMaterial("M_Truck_CabWhite", new Color(0.9f, 0.9f, 0.88f)),
                    VehicleMaterial("M_Truck_CabRed", new Color(0.66f, 0.12f, 0.08f)),
                    VehicleMaterial("M_Truck_CabBlue", new Color(0.13f, 0.25f, 0.5f))
                })
            };
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("kind").enumValueIndex = (int)entries[i].Kind;
                SerializedProperty prefabs = entry.FindPropertyRelative("prefabs");
                prefabs.arraySize = 1;
                prefabs.GetArrayElementAtIndex(0).objectReferenceValue = entries[i].Prefab;
                SerializedProperty colours = entry.FindPropertyRelative("bodyColours");
                colours.arraySize = entries[i].Colours.Length;
                for (int c = 0; c < entries[i].Colours.Length; c++)
                    colours.GetArrayElementAtIndex(c).objectReferenceValue = entries[i].Colours[c];
            }
            serialized.FindProperty("trafficLightPrefab").objectReferenceValue = light;
            serialized.FindProperty("busStopPrefab").objectReferenceValue = shelter;
            serialized.FindProperty("metroTrainPrefab").objectReferenceValue = train;
            serialized.FindProperty("crosswalkPrefab").objectReferenceValue = crosswalk;
            SetArray(serialized.FindProperty("pedestrianPrefabs"), walkers);
            SetArray(serialized.FindProperty("clothesColours"), Palette("M_Clothes_", ClothesColours));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Material[] Palette(string prefix, Color[] colours)
        {
            var materials = new Material[colours.Length];
            for (int i = 0; i < colours.Length; i++) materials[i] = VehicleMaterial($"{prefix}{i + 1:00}", colours[i]);
            return materials;
        }

        // ------------------------------------------------------------------ vehicle models (pivot on the ground, front +Z)

        private sealed class ModelParts
        {
            public GameObject Root;
            public Transform Model, Lod0, Lod1;
            public readonly System.Collections.Generic.List<Renderer> Body = new System.Collections.Generic.List<Renderer>();
            public readonly System.Collections.Generic.List<Renderer> Brakes = new System.Collections.Generic.List<Renderer>();
            public readonly System.Collections.Generic.List<Renderer> Heads = new System.Collections.Generic.List<Renderer>();
            public readonly System.Collections.Generic.List<Transform> Wheels = new System.Collections.Generic.List<Transform>();
        }

        private static ModelParts StartModel(string name)
        {
            var parts = new ModelParts { Root = new GameObject(name) };
            parts.Model = Child(parts.Root.transform, "Model");
            parts.Lod0 = Child(parts.Model, "LOD0");
            parts.Lod1 = Child(parts.Model, "LOD1");
            return parts;
        }

        private static GameObject BuildCar(string name, bool taxi)
        {
            ModelParts p = StartModel(name);
            float length = taxi ? 4.0f : 4.3f, width = taxi ? 1.7f : 1.8f, half = length / 2f;
            Material paint = VehicleMaterial(taxi ? "M_Car_TaxiYellow" : "M_Car_01", taxi ? new Color(0.98f, 0.78f, 0.05f) : CarColours[0]);
            p.Body.Add(Piece(p.Lod0, "Body", new Vector3(0f, 0.56f, 0f), new Vector3(width, 0.62f, length), paint));
            Piece(p.Lod0, "Cabin", new Vector3(0f, 1.1f, -0.25f), new Vector3(width - 0.2f, 0.5f, length * 0.5f), Glass());
            p.Body.Add(Piece(p.Lod0, "Roof", new Vector3(0f, 1.38f, -0.3f), new Vector3(width - 0.18f, 0.07f, length * 0.44f), paint));
            Piece(p.Lod0, "BumperFront", new Vector3(0f, 0.34f, half + 0.02f), new Vector3(width + 0.04f, 0.18f, 0.12f), Dark());
            Piece(p.Lod0, "BumperRear", new Vector3(0f, 0.34f, -half - 0.02f), new Vector3(width + 0.04f, 0.18f, 0.12f), Dark());
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                p.Heads.Add(Piece(p.Lod0, "Headlight", new Vector3(x, 0.7f, half + 0.01f), new Vector3(0.34f, 0.12f, 0.04f), HeadOff()));
                p.Brakes.Add(Piece(p.Lod0, "BrakeLight", new Vector3(x, 0.74f, -half - 0.01f), new Vector3(0.34f, 0.12f, 0.04f), BrakeOff()));
            }
            if (taxi) Piece(p.Lod0, "TaxiSign", new Vector3(0f, 1.5f, -0.3f), new Vector3(0.55f, 0.18f, 0.25f), VehicleMaterial("M_Car_TaxiSign", new Color(0.95f, 0.95f, 0.92f)));
            foreach (float z in new[] { half - 0.8f, -half + 0.8f })
            foreach (float x in new[] { -width / 2f + 0.08f, width / 2f - 0.08f })
                p.Wheels.Add(Wheel(p.Lod0, new Vector3(x, 0.32f, z), 0.32f, 0.22f));
            p.Body.Add(Piece(p.Lod1, "Box", new Vector3(0f, 0.7f, 0f), new Vector3(width, 1.3f, length), paint, false));
            return FinishVehicle(p, 0.32f, null);
        }

        private static GameObject BuildMoto()
        {
            ModelParts p = StartModel("Moto");
            Transform lean = Child(p.Lod0, "Lean");
            Material paint = VehicleMaterial("M_Moto_01", MotoColours[0]);
            p.Body.Add(Piece(lean, "Frame", new Vector3(0f, 0.6f, 0.05f), new Vector3(0.26f, 0.38f, 1.25f), paint));
            Piece(lean, "Seat", new Vector3(0f, 0.86f, -0.25f), new Vector3(0.3f, 0.12f, 0.65f), Dark());
            Piece(lean, "Handlebar", new Vector3(0f, 1.0f, 0.5f), new Vector3(0.7f, 0.05f, 0.05f), Dark());
            Piece(lean, "RiderBody", new Vector3(0f, 1.22f, -0.2f), new Vector3(0.42f, 0.6f, 0.3f), VehicleMaterial("M_Rider_Jacket", new Color(0.12f, 0.15f, 0.22f)));
            Piece(lean, "RiderLegs", new Vector3(0f, 0.85f, -0.05f), new Vector3(0.4f, 0.22f, 0.55f), VehicleMaterial("M_Rider_Jeans", new Color(0.18f, 0.24f, 0.36f)));
            p.Body.Add(Piece(lean, "Helmet", new Vector3(0f, 1.64f, -0.15f), new Vector3(0.3f, 0.3f, 0.32f), paint, true, PrimitiveType.Sphere));
            p.Heads.Add(Piece(lean, "Headlight", new Vector3(0f, 0.92f, 0.72f), new Vector3(0.14f, 0.12f, 0.05f), HeadOff()));
            p.Brakes.Add(Piece(lean, "BrakeLight", new Vector3(0f, 0.8f, -0.62f), new Vector3(0.14f, 0.08f, 0.04f), BrakeOff()));
            p.Wheels.Add(Wheel(lean, new Vector3(0f, 0.3f, 0.68f), 0.3f, 0.12f));
            p.Wheels.Add(Wheel(lean, new Vector3(0f, 0.3f, -0.68f), 0.3f, 0.12f));
            p.Body.Add(Piece(p.Lod1, "Box", new Vector3(0f, 0.85f, 0f), new Vector3(0.45f, 1.6f, 1.9f), paint, false));
            return FinishVehicle(p, 0.3f, lean);
        }

        private static GameObject BuildBus()
        {
            ModelParts p = StartModel("Bus");
            const float length = 11.5f, width = 2.5f, half = length / 2f;
            Material paint = VehicleMaterial("M_Bus_Green", new Color(0.13f, 0.52f, 0.3f));
            p.Body.Add(Piece(p.Lod0, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(width, 1.3f, length), paint));
            Piece(p.Lod0, "Windows", new Vector3(0f, 2.1f, -0.1f), new Vector3(width + 0.02f, 0.95f, length - 0.4f), Glass());
            Piece(p.Lod0, "Roof", new Vector3(0f, 2.75f, 0f), new Vector3(width, 0.35f, length), VehicleMaterial("M_Bus_Roof", new Color(0.92f, 0.92f, 0.9f)));
            Piece(p.Lod0, "Windshield", new Vector3(0f, 1.95f, half + 0.01f), new Vector3(width - 0.2f, 1.25f, 0.05f), Glass());
            Piece(p.Lod0, "Sign", new Vector3(0f, 2.78f, half + 0.02f), new Vector3(1.6f, 0.25f, 0.05f), Emissive("M_Light_BusSign", new Color(1f, 0.6f, 0.1f), 1.5f));
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                p.Heads.Add(Piece(p.Lod0, "Headlight", new Vector3(x, 0.65f, half + 0.01f), new Vector3(0.36f, 0.16f, 0.04f), HeadOff()));
                p.Brakes.Add(Piece(p.Lod0, "BrakeLight", new Vector3(x, 0.9f, -half - 0.01f), new Vector3(0.3f, 0.2f, 0.04f), BrakeOff()));
            }
            foreach (float z in new[] { 3.6f, -3.4f })
            foreach (float x in new[] { -1.08f, 1.08f })
                p.Wheels.Add(Wheel(p.Lod0, new Vector3(x, 0.5f, z), 0.5f, 0.3f));
            p.Body.Add(Piece(p.Lod1, "Box", new Vector3(0f, 1.5f, 0f), new Vector3(width, 2.9f, length), paint, false));
            return FinishVehicle(p, 0.5f, null);
        }

        private static GameObject BuildTruck()
        {
            ModelParts p = StartModel("Truck_City");
            const float length = 9.5f, width = 2.5f, half = length / 2f;
            Material paint = VehicleMaterial("M_Truck_CabWhite", new Color(0.9f, 0.9f, 0.88f));
            Material box = VehicleMaterial("M_Truck_Box", new Color(0.86f, 0.86f, 0.83f));
            p.Body.Add(Piece(p.Lod0, "Cab", new Vector3(0f, 1.6f, half - 1.15f), new Vector3(width - 0.1f, 2.3f, 2.3f), paint));
            Piece(p.Lod0, "Windshield", new Vector3(0f, 2.15f, half + 0.01f), new Vector3(width - 0.4f, 0.8f, 0.05f), Glass());
            Piece(p.Lod0, "Chassis", new Vector3(0f, 0.78f, 0f), new Vector3(width - 0.3f, 0.3f, length - 0.2f), Dark());
            Piece(p.Lod0, "Cargo", new Vector3(0f, 2.15f, -1.25f), new Vector3(width, 2.7f, 6.8f), box);
            foreach (float x in new[] { -0.85f, 0.85f })
            {
                p.Heads.Add(Piece(p.Lod0, "Headlight", new Vector3(x, 0.85f, half + 0.01f), new Vector3(0.34f, 0.16f, 0.04f), HeadOff()));
                p.Brakes.Add(Piece(p.Lod0, "BrakeLight", new Vector3(x, 0.9f, -half - 0.01f), new Vector3(0.3f, 0.16f, 0.04f), BrakeOff()));
            }
            foreach (float z in new[] { half - 1.3f, -2.6f, -3.8f })
            foreach (float x in new[] { -1.08f, 1.08f })
                p.Wheels.Add(Wheel(p.Lod0, new Vector3(x, 0.5f, z), 0.5f, 0.3f));
            Piece(p.Lod1, "Box", new Vector3(0f, 1.75f, 0f), new Vector3(width, 3.4f, length), box, false);
            p.Body.Add(Piece(p.Lod1, "CabBox", new Vector3(0f, 1.5f, half - 1.15f), new Vector3(width + 0.02f, 2.3f, 2.3f), paint, false));
            return FinishVehicle(p, 0.5f, null);
        }

        /// <summary>Two levels of detail (full model near, one box farther, nothing when tiny) and the view's references.</summary>
        private static GameObject FinishVehicle(ModelParts p, float wheelRadius, Transform lean)
        {
            var group = p.Model.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.035f, p.Lod0.GetComponentsInChildren<Renderer>()),
                new LOD(0.004f, p.Lod1.GetComponentsInChildren<Renderer>())
            });
            group.RecalculateBounds();

            var view = p.Root.AddComponent<AmbientVehicleView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("model").objectReferenceValue = p.Model;
            SetArray(serialized.FindProperty("bodyRenderers"), p.Body.ToArray());
            SetArray(serialized.FindProperty("wheels"), p.Wheels.ToArray());
            serialized.FindProperty("wheelRadius").floatValue = wheelRadius;
            SetArray(serialized.FindProperty("brakeLights"), p.Brakes.ToArray());
            serialized.FindProperty("brakeOff").objectReferenceValue = BrakeOff();
            serialized.FindProperty("brakeOn").objectReferenceValue = Emissive("M_Light_BrakeOn", new Color(1f, 0.08f, 0.05f), 3f);
            SetArray(serialized.FindProperty("headlights"), p.Heads.ToArray());
            serialized.FindProperty("headlightOff").objectReferenceValue = HeadOff();
            serialized.FindProperty("headlightOn").objectReferenceValue = Emissive("M_Light_HeadOn", new Color(1f, 0.95f, 0.8f), 4f);
            serialized.FindProperty("leanPivot").objectReferenceValue = lean;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return p.Root;
        }

        // ------------------------------------------------------------------ metro train (pivot on the rails, front +Z)

        /// <summary>
        /// Medellín-style metro unit: grey cars with a green stripe and dark windows, four doors per side
        /// and car, bogies under each end. Must match the simulation's car length (22 m) and gap (1 m).
        /// </summary>
        private static GameObject BuildMetroTrain(int cars)
        {
            const float carLength = 22f, gap = 1f, width = 3f, floor = 0.66f;
            var root = new GameObject("Metro_Train");
            Transform model = Child(root.transform, "Model");
            Transform lod0 = Child(model, "LOD0");
            Transform lod1 = Child(model, "LOD1");
            Material body = VehicleMaterial("M_Metro_Body", new Color(0.82f, 0.83f, 0.83f));
            Material stripe = VehicleMaterial("M_Metro_Accent", new Color(0.3f, 0.46f, 0.4f));
            Material doorClosed = VehicleMaterial("M_Metro_Door", new Color(0.7f, 0.71f, 0.72f));
            Material doorOpen = Emissive("M_Metro_DoorOpen", new Color(0.35f, 0.3f, 0.22f), 0.6f);
            var doors = new System.Collections.Generic.List<Renderer>();

            float total = cars * carLength + (cars - 1) * gap;
            for (int c = 0; c < cars; c++)
            {
                float z = -total / 2f + carLength / 2f + c * (carLength + gap);
                Transform car = Child(lod0, $"Car_{c + 1}");
                car.localPosition = new Vector3(0f, 0f, z);
                Piece(car, "Body", new Vector3(0f, floor + 1.85f, 0f), new Vector3(width, 3.7f, carLength), body);
                Piece(car, "Windows", new Vector3(0f, floor + 2.3f, 0f), new Vector3(width + 0.06f, 1.2f, carLength - 1.4f), Glass(), false);
                Piece(car, "Stripe", new Vector3(0f, floor + 0.75f, 0f), new Vector3(width + 0.06f, 0.36f, carLength + 0.04f), stripe, false);
                Piece(car, "Roof", new Vector3(0f, floor + 3.8f, 0f), new Vector3(width - 0.4f, 0.2f, carLength - 2f), Dark(), false);
                foreach (float bz in new[] { -7.5f, 7.5f })
                    Piece(car, "Bogie", new Vector3(0f, 0.35f, bz), new Vector3(2.4f, 0.5f, 2.8f), Dark(), false);
                foreach (float side in new[] { -1f, 1f })
                foreach (float dz in new[] { -7.5f, -2.5f, 2.5f, 7.5f })
                    doors.Add(Piece(car, "Door", new Vector3(side * (width / 2f + 0.04f), floor + 1.25f, dz),
                        new Vector3(0.04f, 2.3f, 1.4f), doorClosed, false));
                Piece(lod1, $"Car_{c + 1}", new Vector3(0f, floor + 1.85f, z), new Vector3(width, 3.7f, carLength), body, false);
            }
            foreach (float end in new[] { -1f, 1f })
            {
                float z = end * (total / 2f + 0.02f);
                Piece(lod0, "Windshield", new Vector3(0f, floor + 2.6f, z), new Vector3(width - 0.4f, 1.3f, 0.05f), Glass(), false);
                foreach (float x in new[] { -0.9f, 0.9f })
                    Piece(lod0, "Headlight", new Vector3(x, floor + 0.9f, z), new Vector3(0.3f, 0.18f, 0.05f),
                        Emissive("M_Light_HeadOn", new Color(1f, 0.95f, 0.8f), 4f), false);
            }

            var group = model.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.03f, lod0.GetComponentsInChildren<Renderer>()),
                new LOD(0.002f, lod1.GetComponentsInChildren<Renderer>())
            });
            group.RecalculateBounds();

            var view = root.AddComponent<MetroTrainView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("model").objectReferenceValue = model;
            SetArray(serialized.FindProperty("doors"), doors.ToArray());
            serialized.FindProperty("doorClosed").objectReferenceValue = doorClosed;
            serialized.FindProperty("doorOpen").objectReferenceValue = doorOpen;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ------------------------------------------------------------------ people (pivot at the feet, front +Z)

        private static readonly Color[] ClothesColours =
        {
            new Color(0.75f, 0.2f, 0.18f), new Color(0.2f, 0.36f, 0.62f), new Color(0.92f, 0.9f, 0.85f),
            new Color(0.15f, 0.15f, 0.17f), new Color(0.95f, 0.78f, 0.2f), new Color(0.28f, 0.55f, 0.35f),
            new Color(0.55f, 0.3f, 0.55f), new Color(0.9f, 0.5f, 0.2f)
        };

        /// <summary>Older settings assets have no people counts yet: give them the recommended ones.</summary>
        private static void FillPedestrianCounts(AmbientSettingsData settings)
        {
            var serialized = new SerializedObject(settings);
            foreach ((string level, int count) in new[] { ("low", 80), ("medium", 150), ("high", 300) })
            {
                SerializedProperty people = serialized.FindProperty(level).FindPropertyRelative("pedestrians");
                if (people.intValue == 0) people.intValue = count;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        /// <summary>
        /// Simple person about 1.75 m tall (× <paramref name="scale"/>): legs and arms on pivots that swing while
        /// walking, clothes coloured by the visual profile. A rigged humanoid with an Animator replaces it.
        /// </summary>
        private static GameObject BuildPerson(string name, float scale)
        {
            var root = new GameObject(name);
            Transform model = Child(root.transform, "Model");
            Transform lod0 = Child(model, "LOD0");
            Transform lod1 = Child(model, "LOD1");
            Material shirt = VehicleMaterial("M_Clothes_01", ClothesColours[0]);
            Material pants = VehicleMaterial("M_Person_Pants", new Color(0.17f, 0.2f, 0.3f));
            Material skin = VehicleMaterial("M_Person_Skin", new Color(0.72f, 0.53f, 0.42f));
            Material hair = VehicleMaterial("M_Person_Hair", new Color(0.12f, 0.08f, 0.06f));
            float hip = 0.92f * scale;

            var limbs = new Transform[4];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.11f : 0.11f;
                Transform leg = Child(lod0, side == 0 ? "LeftLeg" : "RightLeg");
                leg.localPosition = new Vector3(x, hip, 0f);
                Piece(leg, "Leg", new Vector3(0f, -hip / 2f, 0f), new Vector3(0.16f, hip, 0.18f), pants, false);
                Piece(leg, "Shoe", new Vector3(0f, -hip + 0.04f, 0.04f), new Vector3(0.14f, 0.08f, 0.26f), Dark(), false);
                limbs[side] = leg;

                Transform arm = Child(lod0, side == 0 ? "LeftArm" : "RightArm");
                arm.localPosition = new Vector3(side == 0 ? -0.27f : 0.27f, 1.48f * scale, 0f);
                Piece(arm, "Arm", new Vector3(0f, -0.3f * scale, 0f), new Vector3(0.12f, 0.6f * scale, 0.14f), shirt, false);
                limbs[2 + side] = arm;
            }
            Renderer torso = Piece(lod0, "Torso", new Vector3(0f, hip + 0.31f * scale, 0f), new Vector3(0.42f, 0.62f * scale, 0.24f), shirt);
            Piece(lod0, "Head", new Vector3(0f, 1.66f * scale, 0.01f), new Vector3(0.22f, 0.25f, 0.23f), skin, false, PrimitiveType.Sphere);
            Piece(lod0, "Hair", new Vector3(0f, 1.74f * scale, -0.02f), new Vector3(0.23f, 0.12f, 0.22f), hair, false, PrimitiveType.Sphere);
            Renderer box = Piece(lod1, "Body", new Vector3(0f, 0.87f * scale, 0f), new Vector3(0.45f, 1.74f * scale, 0.28f), shirt, false);

            var group = model.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.05f, lod0.GetComponentsInChildren<Renderer>()),
                new LOD(0.005f, lod1.GetComponentsInChildren<Renderer>())
            });
            group.RecalculateBounds();

            var view = root.AddComponent<PedestrianView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("model").objectReferenceValue = model;
            SetArray(serialized.FindProperty("clothes"), new Object[]
            {
                torso, box, limbs[2].GetComponentInChildren<Renderer>(), limbs[3].GetComponentInChildren<Renderer>()
            });
            serialized.FindProperty("leftLeg").objectReferenceValue = limbs[0];
            serialized.FindProperty("rightLeg").objectReferenceValue = limbs[1];
            serialized.FindProperty("leftArm").objectReferenceValue = limbs[2];
            serialized.FindProperty("rightArm").objectReferenceValue = limbs[3];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Zebra stripes across a 12 m street: +Z is the walking direction, stripes run along the street.</summary>
        private static GameObject BuildCrosswalk()
        {
            var root = new GameObject("Crosswalk_Zebra");
            Material paint = VehicleMaterial("M_Road_LineWhite", new Color(0.93f, 0.93f, 0.9f));
            for (float z = -5.5f; z <= 5.51f; z += 1f)
                Piece(root.transform, "Stripe", new Vector3(0f, 0f, z), new Vector3(CityWalkLayout.CrosswalkWidth, 0.02f, 0.5f), paint, false);
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.01f, root.GetComponentsInChildren<Renderer>()) });
            group.RecalculateBounds();
            return root;
        }

        // ------------------------------------------------------------------ street furniture

        /// <summary>Pole on the right kerb, arm over the lane, three-lamp head; stop lines for cars and motos.</summary>
        private static GameObject BuildTrafficLight()
        {
            var root = new GameObject("TrafficLight_Approach");
            Transform lod0 = Child(root.transform, "LOD0");
            Material metal = VehicleMaterial("M_Signal_Metal", new Color(0.23f, 0.24f, 0.25f));
            Material paint = VehicleMaterial("M_Road_LineWhite", new Color(0.93f, 0.93f, 0.9f));
            Piece(lod0, "Pole", new Vector3(3.7f, 2.8f, 0.6f), new Vector3(0.18f, 2.8f, 0.18f), metal, true, PrimitiveType.Cylinder);
            Piece(lod0, "Arm", new Vector3(1.9f, 5.45f, 0.6f), new Vector3(3.6f, 0.14f, 0.14f), metal);
            Piece(lod0, "Head", new Vector3(0.2f, 4.75f, 0.6f), new Vector3(0.42f, 1.08f, 0.32f), metal);
            Material off = LampOff();
            Renderer red = Piece(lod0, "LampRed", new Vector3(0.2f, 5.08f, 0.43f), Vector3.one * 0.24f, off, false, PrimitiveType.Sphere);
            Renderer amber = Piece(lod0, "LampAmber", new Vector3(0.2f, 4.75f, 0.43f), Vector3.one * 0.24f, off, false, PrimitiveType.Sphere);
            Renderer green = Piece(lod0, "LampGreen", new Vector3(0.2f, 4.42f, 0.43f), Vector3.one * 0.24f, off, false, PrimitiveType.Sphere);
            Piece(lod0, "StopLineMotos", new Vector3(0f, LineHeight, -0.3f), new Vector3(5.8f, 0.02f, 0.3f), paint, false);
            Piece(lod0, "StopLineCars", new Vector3(0f, LineHeight, -AmbientTraffic.CarStopBack + 0.2f), new Vector3(5.8f, 0.02f, 0.3f), paint, false);
            Piece(lod0, "MotoBox", new Vector3(0f, LineHeight - 0.005f, -1.85f), new Vector3(5.6f, 0.02f, 2.8f),
                VehicleMaterial("M_Road_MotoBox", new Color(0.36f, 0.45f, 0.38f)), false);

            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.006f, lod0.GetComponentsInChildren<Renderer>()) });
            group.RecalculateBounds();

            var view = root.AddComponent<TrafficLightView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("red").objectReferenceValue = red;
            serialized.FindProperty("amber").objectReferenceValue = amber;
            serialized.FindProperty("green").objectReferenceValue = green;
            serialized.FindProperty("redOn").objectReferenceValue = Emissive("M_Signal_Red", new Color(1f, 0.1f, 0.06f), 4f);
            serialized.FindProperty("amberOn").objectReferenceValue = Emissive("M_Signal_Amber", new Color(1f, 0.62f, 0.05f), 4f);
            serialized.FindProperty("greenOn").objectReferenceValue = Emissive("M_Signal_Green", new Color(0.1f, 1f, 0.45f), 4f);
            serialized.FindProperty("lampOff").objectReferenceValue = off;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Shelter on the kerb (+X away from the road, +Z along the traffic): pad, roof, glass back, bench, sign.</summary>
        private static GameObject BuildBusStop()
        {
            var root = new GameObject("BusStop_Shelter");
            Transform lod0 = Child(root.transform, "LOD0");
            Material metal = VehicleMaterial("M_Signal_Metal", new Color(0.23f, 0.24f, 0.25f));
            Piece(lod0, "Pad", new Vector3(0.6f, 0.06f, 0f), new Vector3(2.0f, 0.12f, 4.4f), VehicleMaterial("M_BusStop_Concrete", new Color(0.66f, 0.65f, 0.62f)), false);
            Piece(lod0, "Roof", new Vector3(0.8f, 2.55f, 0f), new Vector3(1.9f, 0.1f, 4.2f), VehicleMaterial("M_BusStop_Roof", new Color(0.18f, 0.45f, 0.28f)));
            Piece(lod0, "Back", new Vector3(1.5f, 1.3f, 0f), new Vector3(0.05f, 2.1f, 3.9f), Glass());
            foreach (float z in new[] { -1.9f, 1.9f })
                Piece(lod0, "Post", new Vector3(1.5f, 1.25f, z), new Vector3(0.08f, 1.25f, 0.08f), metal, true, PrimitiveType.Cylinder);
            Piece(lod0, "Bench", new Vector3(1.15f, 0.25f, 0f), new Vector3(0.45f, 0.45f, 2.4f), VehicleMaterial("M_BusStop_Bench", new Color(0.45f, 0.3f, 0.18f)));
            Piece(lod0, "SignPole", new Vector3(0.1f, 1.3f, 2.3f), new Vector3(0.07f, 1.3f, 0.07f), metal, true, PrimitiveType.Cylinder);
            Piece(lod0, "Sign", new Vector3(0.1f, 2.7f, 2.3f), new Vector3(0.05f, 0.5f, 0.5f), VehicleMaterial("M_BusStop_Sign", new Color(0.1f, 0.3f, 0.65f)));

            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.008f, lod0.GetComponentsInChildren<Renderer>()) });
            group.RecalculateBounds();
            return root;
        }

        // ------------------------------------------------------------------ building blocks

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// <summary>A primitive without collider (traffic is not selectable), sized in meters.</summary>
        private static Renderer Piece(Transform parent, string name, Vector3 position, Vector3 size, Material material,
            bool shadows = true, PrimitiveType shape = PrimitiveType.Cube)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return renderer;
        }

        /// <summary>Wheel pivot (spins around X) holding a cylinder lying across the vehicle.</summary>
        private static Transform Wheel(Transform parent, Vector3 position, float radius, float width)
        {
            Transform pivot = Child(parent, "Wheel");
            pivot.localPosition = position;
            Renderer tyre = Piece(pivot, "Tyre", Vector3.zero, new Vector3(radius * 2f, width / 2f, radius * 2f),
                VehicleMaterial("M_Vehicle_Tire", new Color(0.06f, 0.06f, 0.06f)), false, PrimitiveType.Cylinder);
            tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return pivot;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Material Glass() => VehicleMaterial("M_Vehicle_Glass", new Color(0.15f, 0.2f, 0.28f));
        private static Material Dark() => VehicleMaterial("M_Vehicle_Chassis", new Color(0.16f, 0.16f, 0.17f));
        private static Material BrakeOff() => VehicleMaterial("M_Light_BrakeOff", new Color(0.35f, 0.04f, 0.03f));
        private static Material HeadOff() => VehicleMaterial("M_Light_HeadOff", new Color(0.85f, 0.85f, 0.8f));
        private static Material LampOff() => VehicleMaterial("M_Signal_LampOff", new Color(0.1f, 0.1f, 0.1f));

        /// <summary>
        /// A lamp that looks switched on whatever the sun does: URP Unlit at full colour (dimmer when
        /// <paramref name="intensity"/> &lt; 1). Unlit needs no emission keyword, which Lit materials lost on save.
        /// </summary>
        private static Material Emissive(string name, Color colour, float intensity)
        {
            Material material = VehicleMaterial(name, colour);
            material.shader = Shader.Find("Universal Render Pipeline/Unlit");
            Color lit = colour * Mathf.Min(1f, intensity);
            lit.a = 1f;
            material.SetColor("_BaseColor", lit);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
