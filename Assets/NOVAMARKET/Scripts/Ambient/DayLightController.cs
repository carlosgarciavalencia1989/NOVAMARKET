using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Lights the valley for an hour of the day (<see cref="DayLight"/>): sun angle, colour and intensity, ambient
    /// light, fog and sky, and switches the street lamps and headlights at dusk. The scene stays at a fixed
    /// afternoon (16:30); move <c>hour</c> in the Inspector to preview dusk or night, also outside Play.
    /// </summary>
    [ExecuteAlways]
    public class DayLightController : MonoBehaviour
    {
        private static readonly int Exposure = Shader.PropertyToID("_Exposure");

        [Tooltip("Hour of the day, 0-24. 16.5 = the afternoon the scene was lit with.")]
        [SerializeField, Range(0f, 24f)] private float hour = DayLight.AfternoonHour;
        [SerializeField] private Light sun;
        [SerializeField] private AmbientTrafficRunner traffic;

        [Header("Afternoon values (what 16:30 looks like)")]
        [SerializeField] private Color ambientSky = new Color(0.55f, 0.62f, 0.74f);
        [SerializeField] private Color ambientEquator = new Color(0.62f, 0.56f, 0.48f);
        [SerializeField] private Color ambientGround = new Color(0.32f, 0.27f, 0.22f);
        [SerializeField] private Color fog = new Color(0.80f, 0.76f, 0.68f);
        [SerializeField] private float skyExposure = 1.15f;

        // Edit mode starts as if 16:30 were applied already, so opening the scene changes nothing.
        private float applied = DayLight.AfternoonHour;
        private Material skyCopy;

        public float Hour
        {
            get => hour;
            set => hour = Mathf.Repeat(value, 24f);
        }

        public DayLightState State { get; private set; } = DayLight.Evaluate(DayLight.AfternoonHour);

        private void Start()
        {
            if (Application.isPlaying) Apply(); // lamps and headlights always start right
        }

        private void Update()
        {
            if (!Mathf.Approximately(hour, applied)) Apply();
        }

        private void OnDestroy()
        {
            if (skyCopy != null) Destroy(skyCopy);
        }

        private static Color Dim(Color colour, float scale) =>
            new Color(colour.r * scale, colour.g * scale, colour.b * scale, colour.a);

        private void Apply()
        {
            applied = hour;
            DayLightState s = DayLight.Evaluate(hour);
            State = s;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(s.Pitch, s.Yaw, 0f);
                sun.color = new Color(s.Colour.X, s.Colour.Y, s.Colour.Z);
                sun.intensity = s.Intensity;
            }
            float a = s.AmbientScale;
            RenderSettings.ambientSkyColor = Dim(ambientSky, a);
            RenderSettings.ambientEquatorColor = Dim(ambientEquator, a);
            RenderSettings.ambientGroundColor = Dim(ambientGround, a);
            RenderSettings.fogColor = Dim(fog, a);

            // The sky material is an asset: only a Play-mode copy is darkened, the asset stays untouched.
            if (Application.isPlaying && RenderSettings.skybox != null)
            {
                if (skyCopy == null)
                {
                    skyCopy = new Material(RenderSettings.skybox);
                    RenderSettings.skybox = skyCopy;
                }
                skyCopy.SetFloat(Exposure, skyExposure * Mathf.Lerp(0.15f, 1f, a));
            }

            if (traffic != null)
            {
                traffic.HeadlightsOn = s.LampsOn;
                traffic.LampsOn = s.LampsOn;
            }
        }
    }
}
