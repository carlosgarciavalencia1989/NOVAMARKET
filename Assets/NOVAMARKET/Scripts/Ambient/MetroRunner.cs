using System.Collections.Generic;
using NovaMarket.Data;
using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Runs the valley metro (<see cref="MetroLine"/>) in Play mode and draws its trains. The line is short
    /// (a few trains), so it always runs, wherever the camera is. All rules live in the simulation.
    /// </summary>
    public class MetroRunner : MonoBehaviour
    {
        [SerializeField] private MetroLineData line;
        [SerializeField] private AmbientVisualProfile visuals;
        [Tooltip("Simulation steps per second; the trains glide smoothly between steps.")]
        [SerializeField, Range(5f, 60f)] private float stepsPerSecond = 20f;

        private readonly List<MetroTrainView> views = new List<MetroTrainView>();
        private float accumulator;

        public MetroLine Metro { get; private set; }

        private void Awake()
        {
            if (line == null)
            {
                Debug.LogWarning("MetroRunner: assign the metro line (NOVAMARKET → Ambient → Setup).");
                enabled = false;
                return;
            }
            Metro = new MetroLine(line.ToDefinition(), line.ToSettings());
        }

        private void Start()
        {
            if (Metro == null || visuals == null || visuals.MetroTrainPrefab == null) return;
            var root = new GameObject("MetroTrains").transform;
            root.SetParent(transform, false);
            foreach (MetroTrain train in Metro.Trains)
            {
                GameObject body = Instantiate(visuals.MetroTrainPrefab, root);
                var view = body.GetComponent<MetroTrainView>();
                if (view == null) continue;
                view.Bind(train, Metro.Definition.RailHeight);
                views.Add(view);
            }
        }

        private void Update()
        {
            float step = 1f / Mathf.Max(5f, stepsPerSecond);
            accumulator += Time.deltaTime;
            int steps = 0;
            while (accumulator >= step && steps < 3)
            {
                Metro.Tick(step);
                foreach (MetroTrainView view in views) view.Capture();
                accumulator -= step;
                steps++;
            }
            if (steps == 3) accumulator = 0f;
            float alpha = accumulator / step;
            foreach (MetroTrainView view in views) view.Sync(alpha);
        }
    }
}
