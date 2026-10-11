using UnityEngine;
using UnityEngine.Rendering;

namespace NovaMarket.UI
{
    /// <summary>
    /// Top-down orthographic camera that renders the valley into a texture for the HUD minimap.
    /// The terrain does not change, so it renders a few frames at start and then switches off;
    /// call <see cref="Refresh"/> to take a new picture. Fog is disabled for its renders so the
    /// map stays clear from high above.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MinimapRenderer : MonoBehaviour
    {
        [Tooltip("World X/Z at the centre of the minimap.")]
        [SerializeField] private Vector2 worldCenter = Vector2.zero;
        [Tooltip("Side of the square area shown, in meters.")]
        [SerializeField] private float worldSize = 1800f;
        [SerializeField] private float cameraHeight = 1500f;
        [SerializeField] private int resolution = 512;
        [SerializeField] private int framesToRender = 3;

        private Camera mapCamera;
        private RenderTexture texture;
        private int framesLeft;
        private bool savedFog;

        public RenderTexture Texture => texture;
        public MinimapProjection Projection { get; private set; }

        private void Awake()
        {
            Projection = new MinimapProjection(worldCenter, worldSize);
            texture = new RenderTexture(resolution, resolution, 24) { name = "Minimap" };

            mapCamera = GetComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = worldSize / 2f;
            mapCamera.nearClipPlane = 10f;
            mapCamera.farClipPlane = cameraHeight + 500f;
            mapCamera.targetTexture = texture;
            transform.SetPositionAndRotation(new Vector3(worldCenter.x, cameraHeight, worldCenter.y),
                Quaternion.Euler(90f, 0f, 0f)); // looking straight down, north (+Z) at the top
            Refresh();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
        }

        private void OnDestroy()
        {
            if (texture != null) texture.Release();
            Destroy(texture);
        }

        private void LateUpdate()
        {
            if (framesLeft > 0 && --framesLeft == 0) mapCamera.enabled = false;
        }

        /// <summary>Renders the map again (e.g. after the scene changed).</summary>
        public void Refresh()
        {
            framesLeft = framesToRender;
            mapCamera.enabled = true;
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != mapCamera) return;
            savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == mapCamera) RenderSettings.fog = savedFog;
        }
    }
}
