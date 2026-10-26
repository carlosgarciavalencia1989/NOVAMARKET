using NovaMarket.Ambient;
using NovaMarket.CameraControl;
using NovaMarket.Data;
using NovaMarket.Operation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaMarket.Editor
{
    /// <summary>
    /// Sound part (menu NOVAMARKET → Ambient → Setup Ambient Sound): the audio profile asset (empty clips =
    /// temporary synthesised sounds) and the <see cref="AmbientSoundRunner"/> on <c>Systems/Ambient</c>.
    /// </summary>
    public static partial class CediHudSetup
    {
        [MenuItem("NOVAMARKET/Ambient/Setup Ambient Sound")]
        public static void SetupSoundMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform ambient = GameObject.Find("CEDI_Medellin")?.transform.Find("Systems/Ambient");
            if (scene.name != "CEDI_Medellin" || ambient == null)
            {
                Debug.LogError("Open the CEDI_Medellin scene and run Setup Ambient Traffic first.");
                return;
            }
            SetupSound(ambient);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Ambient sound set up (Systems/Ambient).");
        }

        private static void SetupSound(Transform ambient)
        {
            EnsureFolder(AmbientDataFolder);
            var sound = GetOrAdd<AmbientSoundRunner>(ambient.gameObject);
            SetField(sound, "profile", LoadOrCreate<AmbientAudioProfile>(AmbientDataFolder + "/AmbientAudio.asset"));
            SetField(sound, "traffic", ambient.GetComponent<AmbientTrafficRunner>());
            SetField(sound, "metro", ambient.GetComponent<MetroRunner>());
            SetField(sound, "operation", Object.FindAnyObjectByType<CediOperationRunner>());
            SetField(sound, "wind", GetOrAdd<WindController>(ambient.gameObject));
            SetField(sound, "cameraController", Object.FindAnyObjectByType<StrategyCameraController>());
            var overlay = ambient.GetComponent<AmbientDebugOverlay>();
            if (overlay != null) SetField(overlay, "sound", sound);
        }
    }
}
