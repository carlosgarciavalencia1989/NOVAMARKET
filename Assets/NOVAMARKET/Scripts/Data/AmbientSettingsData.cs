using System;
using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>Quality levels of the scene life: how many vehicles and people move around the camera.</summary>
    public enum AmbientQuality
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// How lively the city is at each quality level, and the mix of vehicles. Medium is the default;
    /// lower it on slow computers. Values are read when Play starts or the level changes.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Ambient Settings", fileName = "AmbientSettings")]
    public sealed class AmbientSettingsData : NovaDataAsset
    {
        [Serializable]
        private struct Level
        {
            [Min(0)] public int vehicles;
            [Min(0)] public int pedestrians;
        }

        [SerializeField] private Level low = new Level { vehicles = 60, pedestrians = 80 };
        [SerializeField] private Level medium = new Level { vehicles = 120, pedestrians = 150 };
        [SerializeField] private Level high = new Level { vehicles = 200, pedestrians = 300 };

        [Header("Vehicle mix (Medellín: many motos)")]
        [SerializeField, Range(0.3f, 0.4f)] private float motoShare = 0.35f;
        [SerializeField, Range(0f, 0.2f)] private float busShare = 0.06f;
        [SerializeField, Range(0f, 0.2f)] private float truckShare = 0.09f;
        [SerializeField, Range(0f, 1f), Tooltip("Yellow taxis among the cars.")] private float taxiShareOfCars = 0.3f;
        [SerializeField, Min(1f)] private float busDwellSeconds = 12f;

        public int VehiclesFor(AmbientQuality quality) => quality switch
        {
            AmbientQuality.Low => low.vehicles,
            AmbientQuality.High => high.vehicles,
            _ => medium.vehicles
        };

        public int PedestriansFor(AmbientQuality quality) => quality switch
        {
            AmbientQuality.Low => low.pedestrians,
            AmbientQuality.High => high.pedestrians,
            _ => medium.pedestrians
        };

        public PedestrianSettings ToPedestrianSettings(AmbientQuality quality) =>
            new PedestrianSettings { Count = PedestriansFor(quality) };

        public AmbientTrafficSettings ToTrafficSettings(AmbientQuality quality) => new AmbientTrafficSettings
        {
            VehicleCount = VehiclesFor(quality),
            MotoShare = motoShare,
            BusShare = busShare,
            TruckShare = truckShare,
            TaxiShareOfCars = taxiShareOfCars,
            BusDwellSeconds = busDwellSeconds
        };
    }
}
