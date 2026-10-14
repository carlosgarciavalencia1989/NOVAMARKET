using NovaMarket.CameraControl;
using NovaMarket.Data;
using NovaMarket.Interaction;
using NovaMarket.UI;
using NovaMarket.UI.Campaign;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace NovaMarket.Editor
{
    /// <summary>HUD part: font asset, panel settings, minimap camera, event system and the HUD object.</summary>
    public static partial class CediHudSetup
    {
        private const string HudFolder = Root + "UI/Hud";
        private const string FontFolder = Root + "UI/Fonts";
        private const string FontSourcePath = FontFolder + "/Oswald-Variable.ttf";
        private const string FontAssetPath = FontFolder + "/Oswald_SDF.asset";
        private const string SheetFontSourcePath = FontFolder + "/Roboto-Variable.ttf";
        private const string SheetFontAssetPath = FontFolder + "/Roboto_SDF.asset";
        private const string HudLayoutPath = HudFolder + "/Hud.uxml";
        private const string ThemePath = HudFolder + "/HudTheme.tss";
        private const string PanelSettingsPath = HudFolder + "/HudPanelSettings.asset";
        private const string CampaignPath = Root + "Data/Campaign.asset";

        private static void SetupHud(Transform systems, SelectionController selection)
        {
            CreateFontAssets();
            PanelSettings panelSettings = GetPanelSettings();

            Transform minimapObject = GetOrCreateChild(systems, "MinimapCamera");
            var minimapCamera = GetOrAdd<Camera>(minimapObject.gameObject);
            minimapCamera.enabled = false; // MinimapRenderer switches it on for a few frames in Play
            var minimap = GetOrAdd<MinimapRenderer>(minimapObject.gameObject);

            Transform eventSystem = GetOrCreateChild(systems, "EventSystem");
            GetOrAdd<EventSystem>(eventSystem.gameObject);
            GetOrAdd<InputSystemUIInputModule>(eventSystem.gameObject);

            Transform hudObject = GetOrCreateChild(systems, "HUD");
            var document = GetOrAdd<UIDocument>(hudObject.gameObject);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudLayoutPath);
            var dataSource = GetOrAdd<HudDataSource>(hudObject.gameObject);
            var hud = GetOrAdd<HudController>(hudObject.gameObject);

            SetField(hud, "dataSource", dataSource);
            SetField(hud, "selection", selection);
            SetField(hud, "cameraController", Object.FindAnyObjectByType<StrategyCameraController>());
            SetField(hud, "minimap", minimap);

            // The planner campaign (v2) is the default; the season level (v1, future boss) stays available by
            // switching the scenario in the Inspector (the .Operation part links its runner).
            var scenario = new SerializedObject(dataSource);
            scenario.FindProperty("scenario").enumValueIndex = (int)HudScenario.PlannerCampaign;
            scenario.ApplyModifiedPropertiesWithoutUndo();
            var planning = GetOrAdd<PlanningLevelController>(hudObject.gameObject);
            SetField(planning, "dataSource", dataSource);

            var campaign = GetOrAdd<CampaignController>(hudObject.gameObject);
            SetField(campaign, "dataSource", dataSource);
            SetField(campaign, "campaign", AssetDatabase.LoadAssetAtPath<CampaignDatabase>(CampaignPath));
            SetField(campaign, "cameraController", Object.FindAnyObjectByType<StrategyCameraController>());
        }

        /// <summary>Dynamic SDF font assets for UI Toolkit: Oswald for the HUD and Roboto for the sheet.</summary>
        private static void CreateFontAssets()
        {
            CreateFontAsset(FontSourcePath, FontAssetPath, "Oswald_SDF");
            CreateFontAsset(SheetFontSourcePath, SheetFontAssetPath, "Roboto_SDF");
        }

        private static void CreateFontAsset(string sourcePath, string assetPath, string assetName)
        {
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath) != null) return;

            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            FontAsset fontAsset = FontAsset.CreateFontAsset(font);
            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            // The atlas texture and material must live inside the asset to be saved.
            fontAsset.atlasTextures[0].name = assetName + " Atlas";
            fontAsset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(HudFolder + "/Hud.uss");
            AssetDatabase.ImportAsset(HudFolder + "/Sheet.uss");
        }

        private static PanelSettings GetPanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);
            return settings;
        }
    }
}
