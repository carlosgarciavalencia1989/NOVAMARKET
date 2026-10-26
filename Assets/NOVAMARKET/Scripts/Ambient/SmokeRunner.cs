using System.Collections.Generic;
using NovaMarket.CameraControl;
using NovaMarket.Data;
using NovaMarket.Simulation.Ambient;
using UnityEngine;
using Num = System.Numerics;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Smoke of the valley in Play mode: plumes from the industrial chimneys and diesel puffs from the buses and
    /// trucks near the camera, both carried by the wind (<see cref="WindController"/>). Two particle systems
    /// draw everything; when and how much each source smokes comes from <see cref="SmokeRules"/>.
    /// </summary>
    public class SmokeRunner : MonoBehaviour
    {
        [SerializeField] private IndustrialChimneysData chimneys;
        [SerializeField] private AmbientTrafficRunner traffic;
        [SerializeField] private WindController wind;
        [SerializeField] private StrategyCameraController cameraController;
        [Tooltip("Soft transparent puff (URP Particles).")]
        [SerializeField] private Material smokeMaterial;

        [Header("Chimneys")]
        [SerializeField] private Color chimneyColour = new Color(0.9f, 0.9f, 0.88f, 0.85f);
        [SerializeField, Min(1f)] private float plumeSeconds = 16f;
        [SerializeField] private Vector2 plumeSize = new Vector2(4f, 14f);
        [SerializeField, Tooltip("m/s the smoke rises when it leaves the chimney.")] private float riseSpeed = 3f;

        [Header("Exhaust")]
        [SerializeField, Tooltip("Only buses and trucks this close to the camera focus smoke (meters).")]
        private float exhaustRadius = 260f;
        [SerializeField, Min(0.5f)] private float puffSeconds = 3f;
        [SerializeField] private Vector2 puffSize = new Vector2(0.9f, 4.5f);

        private readonly Dictionary<AmbientVehicle, (float Speed, float Carry)> exhaust =
            new Dictionary<AmbientVehicle, (float, float)>();
        private readonly List<AmbientVehicle> gone = new List<AmbientVehicle>();
        private List<SmokeSource> sources;
        private float[] chimneyCarry;
        private ParticleSystem plumes;
        private ParticleSystem puffs;

        /// <summary>Particles alive now (chimneys + exhaust), for the debug overlay.</summary>
        public int ParticleCount => (plumes != null ? plumes.particleCount : 0) + (puffs != null ? puffs.particleCount : 0);

        private void Start()
        {
            if (smokeMaterial == null)
            {
                Debug.LogWarning("SmokeRunner: assign the smoke material (NOVAMARKET → Ambient → Setup).");
                enabled = false;
                return;
            }
            sources = chimneys != null ? chimneys.ToSources() : new List<SmokeSource>();
            chimneyCarry = new float[sources.Count];
            plumes = CreateSystem("ChimneySmoke", plumeSeconds, plumeSize, 2000);
            bool exhaustOn = traffic == null || traffic.Quality != AmbientQuality.Low;
            if (exhaustOn) puffs = CreateSystem("Exhaust", puffSeconds, puffSize, 600);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Num.Vector2 drift = Num.Vector2.Zero;
            if (wind != null && wind.isActiveAndEnabled && wind.Wind != null)
                drift = SmokeRules.Drift(wind.Wind.Direction, wind.Wind.Strength, wind.Wind.Gust);
            Carry(plumes, drift, riseSpeed);
            Carry(puffs, drift, 0.6f);

            EmitChimneys(dt);
            EmitExhaust(dt);
        }

        private void EmitChimneys(float dt)
        {
            float time = Time.time;
            var emit = new ParticleSystem.EmitParams { startColor = chimneyColour, applyShapeToPosition = false };
            for (int i = 0; i < sources.Count; i++)
            {
                SmokeSource source = sources[i];
                float rate = SmokeRules.ChimneyPuffsPerSecond * SmokeRules.ChimneyOutput(source, time);
                int count = SmokeRules.Emit(ref chimneyCarry[i], rate, dt);
                for (int k = 0; k < count; k++)
                {
                    emit.position = new Vector3(source.Top.X, source.Top.Y + 0.3f, source.Top.Z) + Random.insideUnitSphere * 0.4f;
                    emit.startSize = plumeSize.x * Random.Range(0.8f, 1.2f);
                    emit.rotation = Random.Range(0f, 360f);
                    plumes.Emit(emit, 1);
                }
            }
        }

        private void EmitExhaust(float dt)
        {
            if (puffs == null || traffic == null || traffic.Traffic == null) return;
            Vector3 focus = cameraController != null && cameraController.Rig != null ? cameraController.Rig.Focus : Vector3.zero;
            float radiusSq = exhaustRadius * exhaustRadius;

            gone.Clear();
            foreach (AmbientVehicle v in exhaust.Keys) gone.Add(v);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            foreach (AmbientVehicle vehicle in traffic.Traffic.Vehicles)
            {
                if (vehicle.Kind != AmbientVehicleKind.Bus && vehicle.Kind != AmbientVehicleKind.Truck) continue;
                float dx = vehicle.Position.X - focus.x, dz = vehicle.Position.Y - focus.z;
                if (dx * dx + dz * dz > radiusSq) continue;
                gone.Remove(vehicle);

                (float previous, float carry) = exhaust.TryGetValue(vehicle, out var state) ? state : (vehicle.Speed, 0f);
                ExhaustPuff puff = SmokeRules.Exhaust(vehicle.Kind, vehicle.Speed, previous, dt);
                int count = SmokeRules.Emit(ref carry, puff.Rate, dt);
                exhaust[vehicle] = (vehicle.Speed, carry);
                if (count == 0) continue;

                float grey = Mathf.Lerp(0.62f, 0.12f, puff.Darkness);
                emit.startColor = new Color(grey, grey, grey, Mathf.Lerp(0.55f, 0.9f, puff.Darkness));
                emit.position = ExhaustOutlet(vehicle);
                for (int k = 0; k < count; k++)
                {
                    emit.startSize = puffSize.x * Random.Range(0.8f, 1.3f);
                    emit.rotation = Random.Range(0f, 360f);
                    puffs.Emit(emit, 1);
                }
            }
            foreach (AmbientVehicle v in gone) exhaust.Remove(v);
        }

        /// <summary>Buses blow out at the rear, low; trucks through a vertical stack behind the cab.</summary>
        private static Vector3 ExhaustOutlet(AmbientVehicle vehicle)
        {
            var position = new Vector3(vehicle.Position.X, 0f, vehicle.Position.Y);
            var forward = new Vector3(vehicle.Heading.X, 0f, vehicle.Heading.Y);
            var left = new Vector3(-forward.z, 0f, forward.x);
            if (vehicle.Kind == AmbientVehicleKind.Bus)
                return position - forward * (vehicle.Length / 2f) + left * (vehicle.Width / 2f - 0.3f) + Vector3.up * 0.5f;
            return position + forward * (vehicle.Length / 2f - 2.6f) + left * (vehicle.Width / 2f - 0.2f) + Vector3.up * 3.7f;
        }

        /// <summary>The wind carries the whole system's smoke; it rises fast at first and then floats.</summary>
        private static void Carry(ParticleSystem system, Num.Vector2 drift, float rise)
        {
            if (system == null) return;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.xMultiplier = drift.X;
            velocity.yMultiplier = rise;
            velocity.zMultiplier = drift.Y;
        }

        private ParticleSystem CreateSystem(string systemName, float lifetime, Vector2 size, int maxParticles)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime * 1.2f);
            main.startSpeed = 0f;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false; // emitted from code, one puff at a time
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // The wind takes over as the smoke slows down: rise fades out, drift grows in.
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            var driftCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1f));
            var riseCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.3f), new Keyframe(1f, 0.12f));
            velocity.x = new ParticleSystem.MinMaxCurve(0f, driftCurve);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, riseCurve);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, driftCurve);

            ParticleSystem.SizeOverLifetimeModule growth = system.sizeOverLifetime;
            growth.enabled = true;
            float ratio = size.x / size.y;
            growth.size = new ParticleSystem.MinMaxCurve(size.y / size.x,
                new AnimationCurve(new Keyframe(0f, ratio), new Keyframe(0.4f, 0.35f), new Keyframe(1f, 1f)));

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(1f, 0.06f),
                        new GradientAlphaKey(0.55f, 0.55f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smokeMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 2f;

            system.Play();
            return system;
        }
    }
}
