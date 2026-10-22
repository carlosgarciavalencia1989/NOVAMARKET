using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Small debug box with the frame rate and how many traffic vehicles exist. F3 shows or hides it.
    /// It keeps measuring while hidden, so the numbers can also be read from code (<see cref="AverageFps"/>).
    /// </summary>
    public class AmbientDebugOverlay : MonoBehaviour
    {
        [SerializeField] private AmbientTrafficRunner runner;
        [SerializeField] private PeopleRunner people;
        [SerializeField] private SmokeRunner smoke;
        [SerializeField] private bool visible;
        [Tooltip("Seconds the averages cover.")]
        [SerializeField, Min(1f)] private float window = 5f;

        private float elapsed;
        private int frames;
        private float worstFrame;
        private GUIStyle style;

        /// <summary>Average frames per second over the last window.</summary>
        public float AverageFps { get; private set; }

        /// <summary>Slowest frame of the last window, in milliseconds.</summary>
        public float WorstFrameMs { get; private set; }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;

            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            frames++;
            worstFrame = Mathf.Max(worstFrame, dt);
            if (elapsed < window) return;
            AverageFps = frames / elapsed;
            WorstFrameMs = worstFrame * 1000f;
            elapsed = 0f;
            frames = 0;
            worstFrame = 0f;
        }

        private void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
            string traffic = runner != null && runner.Traffic != null
                ? $"Vehículos: {runner.Traffic.Vehicles.Count} (calidad {runner.Quality}, {runner.PooledViews} modelos en el pool)\n" +
                  $"Radio alrededor de la cámara: {runner.Traffic.Radius:F0} m · lógica {runner.SimulationMs:F2} ms por paso · modelos {runner.ViewsMs:F2} ms" +
                  (people != null ? $"\nPersonas: {people.ActiveViews}" : "") +
                  (smoke != null ? $" · partículas de humo: {smoke.ParticleCount}" : "")
                : "Tráfico apagado";
            GUI.Box(new Rect(Screen.width / 2f - 260f, 8f, 520f, 90f),
                $"FPS promedio ({window:F0} s): {AverageFps:F0}   ·   cuadro más lento: {WorstFrameMs:F1} ms\n{traffic}\nF3 oculta este cuadro", style);
        }
    }
}
