using System;
using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>
    /// Which models draw each kind of traffic vehicle. To use a 3D pack (Kenney, Quaternius, Synty…)
    /// replace the prefabs here: each one needs an <c>AmbientVehicleView</c> on its root, its pivot on the
    /// ground at the body centre and its front towards +Z (the view's model offset fixes packs that differ).
    /// The traffic logic never changes.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Ambient Visual Profile", fileName = "AmbientVisuals")]
    public sealed class AmbientVisualProfile : NovaDataAsset
    {
        [Serializable]
        private struct KindVisuals
        {
            public AmbientVehicleKind kind;
            [Tooltip("Model variants; the vehicle's variant number picks one.")]
            public GameObject[] prefabs;
            [Tooltip("Body colours given to the renderers the view marks as body (optional).")]
            public Material[] bodyColours;
        }

        [SerializeField] private KindVisuals[] vehicles = new KindVisuals[0];

        [Header("Street furniture")]
        [Tooltip("Traffic light of one approach (pole, arm, head, stop lines), pivot at the moto stop line, +Z = traffic direction.")]
        [SerializeField] private GameObject trafficLightPrefab;
        [Tooltip("Metro train (all its cars), pivot at the train centre on top of the rails, +Z = front. Needs a MetroTrainView.")]
        [SerializeField] private GameObject metroTrainPrefab;
        [Tooltip("Bus shelter, pivot on the kerb, +Z = traffic direction.")]
        [SerializeField] private GameObject busStopPrefab;
        [Tooltip("Zebra crossing paint, pivot at its centre on the street, +Z = walking direction across the street.")]
        [SerializeField] private GameObject crosswalkPrefab;
        [Tooltip("Street lamp post along the highway and avenues, pivot at the foot of the post, +Z = over the road. Needs a StreetLampView.")]
        [SerializeField] private GameObject streetLampPrefab;

        [Header("People")]
        [Tooltip("Person models (each needs a PedestrianView on its root, pivot at the feet, front +Z).")]
        [SerializeField] private GameObject[] pedestrianPrefabs = new GameObject[0];
        [Tooltip("Clothes colours given to the renderers the view marks as clothes (optional).")]
        [SerializeField] private Material[] clothesColours = new Material[0];

        public GameObject TrafficLightPrefab => trafficLightPrefab;
        public GameObject BusStopPrefab => busStopPrefab;
        public GameObject MetroTrainPrefab => metroTrainPrefab;
        public GameObject CrosswalkPrefab => crosswalkPrefab;
        public GameObject StreetLampPrefab => streetLampPrefab;

        /// <summary>A person model for a variant (null when none is set).</summary>
        public GameObject PedestrianPrefabFor(int variant) =>
            pedestrianPrefabs != null && pedestrianPrefabs.Length > 0 ? pedestrianPrefabs[Mathf.Abs(variant) % pedestrianPrefabs.Length] : null;

        public Material ClothesColourFor(int variant) =>
            clothesColours != null && clothesColours.Length > 0 ? clothesColours[Mathf.Abs(variant * 3 + 1) % clothesColours.Length] : null;

        /// <summary>The prefab for a vehicle kind and variant (null when the kind has none).</summary>
        public GameObject PrefabFor(AmbientVehicleKind kind, int variant)
        {
            foreach (KindVisuals visuals in vehicles)
                if (visuals.kind == kind && visuals.prefabs != null && visuals.prefabs.Length > 0)
                    return visuals.prefabs[Mathf.Abs(variant) % visuals.prefabs.Length];
            return null;
        }

        /// <summary>A body colour for a kind and variant (null keeps the prefab's own materials).</summary>
        public Material BodyColourFor(AmbientVehicleKind kind, int variant)
        {
            foreach (KindVisuals visuals in vehicles)
                if (visuals.kind == kind && visuals.bodyColours != null && visuals.bodyColours.Length > 0)
                    return visuals.bodyColours[Mathf.Abs(variant * 5 + 3) % visuals.bodyColours.Length];
            return null;
        }
    }
}
