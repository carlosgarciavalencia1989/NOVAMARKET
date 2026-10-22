using System.IO;
using NovaMarket.Ambient;
using NovaMarket.CameraControl;
using NovaMarket.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Smoke part (menu NOVAMARKET → Ambient → Setup Smoke): a generated soft puff texture, its URP particle
    /// material and the <see cref="SmokeRunner"/> on <c>Systems/Ambient</c>. The chimneys come from the
    /// environment builder (rebuild the environment to move them).
    /// </summary>
    public static partial class CediHudSetup
    {
        private const string SmokeTexturePath = Root + "Materials/Ambient/T_Smoke_Puff.png";
        private const string SmokeMaterialPath = Root + "Materials/Ambient/M_Ambient_Smoke.mat";
        private const int PuffResolution = 64;

        [MenuItem("NOVAMARKET/Ambient/Setup Smoke (chimneys and exhaust)")]
        public static void SetupSmokeMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform ambient = GameObject.Find("CEDI_Medellin")?.transform.Find("Systems/Ambient");
            if (scene.name != "CEDI_Medellin" || ambient == null)
            {
                Debug.LogError("Open the CEDI_Medellin scene and run Setup Ambient Traffic first.");
                return;
            }
            SetupSmoke(ambient);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Smoke set up (Systems/Ambient).");
        }

        private static void SetupSmoke(Transform ambient)
        {
            var smoke = GetOrAdd<SmokeRunner>(ambient.gameObject);
            SetField(smoke, "chimneys", AssetDatabase.LoadAssetAtPath<IndustrialChimneysData>(CediEnvironmentBuilder.ChimneysPath));
            SetField(smoke, "traffic", ambient.GetComponent<AmbientTrafficRunner>());
            SetField(smoke, "wind", GetOrAdd<WindController>(ambient.gameObject));
            SetField(smoke, "cameraController", Object.FindAnyObjectByType<StrategyCameraController>());
            SetField(smoke, "smokeMaterial", SmokeMaterial());
            var overlay = ambient.GetComponent<AmbientDebugOverlay>();
            if (overlay != null) SetField(overlay, "smoke", smoke);
        }

        /// <summary>Transparent, softly lit URP particle material with the generated puff.</summary>
        private static Material SmokeMaterial()
        {
            EnsureFolder(Root + "Materials/Ambient");
            Texture2D puff = SavePuffTexture();
            var material = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
                AssetDatabase.CreateAsset(material, SmokeMaterialPath);
            }
            material.SetTexture("_BaseMap", puff);
            material.SetColor("_BaseColor", Color.white);
            // Transparent, alpha blended, no depth write (what the Inspector sets for Surface Type = Transparent).
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_ReceiveShadows", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A round, slightly lumpy white puff whose alpha fades to the edges.</summary>
        private static Texture2D SavePuffTexture()
        {
            var texture = new Texture2D(PuffResolution, PuffResolution, TextureFormat.RGBA32, false);
            var pixels = new Color[PuffResolution * PuffResolution];
            for (int y = 0; y < PuffResolution; y++)
            for (int x = 0; x < PuffResolution; x++)
            {
                float u = (x + 0.5f) / PuffResolution * 2f - 1f, v = (y + 0.5f) / PuffResolution * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float lumps = 0.7f + 0.3f * Mathf.PerlinNoise(x * 0.11f + 3f, y * 0.11f + 7f)
                                   + 0.15f * (Mathf.PerlinNoise(x * 0.27f + 11f, y * 0.27f + 5f) - 0.5f);
                float alpha = Mathf.Clamp01(Mathf.SmoothStep(1f, 0f, r) * lumps);
                float shade = 0.88f + 0.12f * (1f - r);
                pixels[y * PuffResolution + x] = new Color(shade, shade, shade, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(SmokeTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(SmokeTexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SmokeTexturePath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = PuffResolution;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexturePath);
        }
    }
}
