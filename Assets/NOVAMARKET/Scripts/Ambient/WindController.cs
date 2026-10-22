using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Sends the valley wind (<see cref="WindModel"/>) to the foliage shader every frame as the global
    /// <c>_WindParams</c> (direction, strength, gust). Trees whose canopy uses "NOVAMARKET/Wind Foliage" sway.
    /// </summary>
    [ExecuteAlways]
    public class WindController : MonoBehaviour
    {
        private static readonly int WindParams = Shader.PropertyToID("_WindParams");

        [Tooltip("Where the wind blows to, degrees clockwise from north (+Z).")]
        [SerializeField, Range(0f, 360f)] private float direction = 250f;
        [Tooltip("Bend of a tree top in a steady breeze, meters.")]
        [SerializeField, Range(0f, 2f)] private float strength = 0.35f;
        [SerializeField] private int seed = 4;

        private WindModel wind;

        public WindModel Wind => wind;

        private void OnEnable() => wind = new WindModel(direction, strength, seed);

        private void Update()
        {
            wind ??= new WindModel(direction, strength, seed);
            wind.BaseDirectionDegrees = direction;
            wind.Strength = strength;
            wind.Tick(Application.isPlaying ? Time.deltaTime : 0.02f);
            Shader.SetGlobalVector(WindParams, new Vector4(wind.Direction.X, wind.Direction.Y, wind.Strength, wind.Gust));
        }

        private void OnDisable() => Shader.SetGlobalVector(WindParams, Vector4.zero);
    }
}
