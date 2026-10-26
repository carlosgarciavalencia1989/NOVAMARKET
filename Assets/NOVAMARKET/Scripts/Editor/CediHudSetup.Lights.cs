using System.IO;
using NovaMarket.Ambient;
using NovaMarket.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Lights part (menu NOVAMARKET → Ambient → Setup Day Light): the street lamp model (post, arm, lens and a
    /// warm pool of light on the road), its entry in the visual profile and the <see cref="DayLightController"/>
    /// on <c>Systems/Ambient</c>, wired to the sun and the traffic.
    /// </summary>
    public static partial class CediHudSetup
    {
        private const string GlowTexturePath = Root + "Materials/Ambient/T_Lamp_Glow.png";
        private const string GlowMaterialPath = Root + "Materials/Ambient/M_Lamp_Glow.mat";
        private const int GlowResolution = 64;

        [MenuItem("NOVAMARKET/Ambient/Setup Day Light (street lamps, hour)")]
        public static void SetupDayLightMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform ambient = GameObject.Find("CEDI_Medellin")?.transform.Find("Systems/Ambient");
            if (scene.name != "CEDI_Medellin" || ambient == null)
            {
                Debug.LogError("Open the CEDI_Medellin scene and run Setup Ambient Traffic first.");
                return;
            }
            SetupDayLight(ambient, null);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Day light set up (Systems/Ambient).");
        }

        /// <param name="visuals">The profile to add the lamp to (null = load it).</param>
        private static void SetupDayLight(Transform ambient, AmbientVisualProfile visuals)
        {
            EnsureFolder(FurniturePrefabFolder);
            GameObject lamp = SavePrefab(BuildStreetLamp(), FurniturePrefabFolder + "/StreetLamp_Post.prefab");
            visuals ??= AssetDatabase.LoadAssetAtPath<AmbientVisualProfile>(AmbientDataFolder + "/AmbientVisuals.asset");
            if (visuals != null)
            {
                var serialized = new SerializedObject(visuals);
                serialized.FindProperty("streetLampPrefab").objectReferenceValue = lamp;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(visuals);
            }

            var day = GetOrAdd<DayLightController>(ambient.gameObject);
            Light sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
            SetField(day, "sun", sun);
            SetField(day, "traffic", ambient.GetComponent<AmbientTrafficRunner>());
        }

        /// <summary>9 m post on the kerb (pivot at its foot), arm reaching 2.4 m over the road (+Z), lens below the head.</summary>
        private static GameObject BuildStreetLamp()
        {
            var root = new GameObject("StreetLamp_Post");
            Transform lod0 = Child(root.transform, "LOD0");
            Material metal = VehicleMaterial("M_Lamp_Post", new Color(0.42f, 0.44f, 0.45f));
            Material lensOff = VehicleMaterial("M_Lamp_LensOff", new Color(0.78f, 0.77f, 0.72f));
            Piece(lod0, "Post", new Vector3(0f, 4.5f, 0f), new Vector3(0.2f, 4.5f, 0.2f), metal, true, PrimitiveType.Cylinder);
            Piece(lod0, "Arm", new Vector3(0f, 8.9f, 1.2f), new Vector3(0.12f, 0.12f, 2.4f), metal);
            Piece(lod0, "Head", new Vector3(0f, 8.88f, 2.45f), new Vector3(0.38f, 0.18f, 0.85f), metal);
            Renderer lens = Piece(lod0, "Lens", new Vector3(0f, 8.77f, 2.45f), new Vector3(0.3f, 0.04f, 0.7f), lensOff, false);

            Renderer glow = Piece(root.transform, "Glow", new Vector3(0f, 0.12f, 4.5f), new Vector3(10f, 10f, 1f),
                GlowMaterial(), false, PrimitiveType.Quad);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.receiveShadows = false;
            glow.enabled = false;

            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.004f, lod0.GetComponentsInChildren<Renderer>()) });
            group.RecalculateBounds();

            var view = root.AddComponent<StreetLampView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("lenses").arraySize = 1;
            serialized.FindProperty("lenses").GetArrayElementAtIndex(0).objectReferenceValue = lens;
            serialized.FindProperty("glows").arraySize = 1;
            serialized.FindProperty("glows").GetArrayElementAtIndex(0).objectReferenceValue = glow;
            serialized.FindProperty("lensOff").objectReferenceValue = lensOff;
            serialized.FindProperty("lensOn").objectReferenceValue = Emissive("M_Lamp_LensOn", new Color(1f, 0.86f, 0.6f), 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Additive, unlit warm disc that fades to the edges: the light a lamp throws on the road.</summary>
        private static Material GlowMaterial()
        {
            EnsureFolder(Root + "Materials/Ambient");
            Texture2D texture = SaveGlowTexture();
            var material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, GlowMaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(1f, 0.72f, 0.42f, 0.55f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D SaveGlowTexture()
        {
            var texture = new Texture2D(GlowResolution, GlowResolution, TextureFormat.RGBA32, false);
            var pixels = new Color[GlowResolution * GlowResolution];
            for (int y = 0; y < GlowResolution; y++)
            for (int x = 0; x < GlowResolution; x++)
            {
                float u = (x + 0.5f) / GlowResolution * 2f - 1f, v = (y + 0.5f) / GlowResolution * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                float alpha = (1f - r) * (1f - r);
                pixels[y * GlowResolution + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(GlowTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(GlowTexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GlowTexturePath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = GlowResolution;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath);
        }
    }
}
