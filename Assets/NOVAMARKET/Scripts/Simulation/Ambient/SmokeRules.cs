using System;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>The top of an industrial chimney (meters) and how hard its plant works (1 = normal).</summary>
    public readonly struct SmokeSource
    {
        public SmokeSource(Vector3 top, float strength, float phase)
        {
            Top = top;
            Strength = strength;
            Phase = phase;
        }

        public Vector3 Top { get; }
        public float Strength { get; }

        /// <summary>Where the chimney is in its plant cycle (radians), so they do not all puff together.</summary>
        public float Phase { get; }
    }

    /// <summary>How much a diesel exhaust puffs right now: puffs per second and how dark they are (0 = light grey, 1 = black).</summary>
    public readonly struct ExhaustPuff
    {
        public ExhaustPuff(float rate, float darkness)
        {
            Rate = rate;
            Darkness = darkness;
        }

        public float Rate { get; }
        public float Darkness { get; }
        public static ExhaustPuff None => new ExhaustPuff(0f, 0f);
    }

    /// <summary>
    /// Rules of the smoke in the valley: chimneys that work in slow cycles, plumes the wind carries away and
    /// diesel buses and trucks that puff black when they pull away. Plain C#; the scene only emits the particles.
    /// </summary>
    public static class SmokeRules
    {
        /// <summary>Seconds of one plant cycle (the output of a chimney rises and falls over it).</summary>
        public const float CycleSeconds = 90f;

        /// <summary>Puffs per second of a chimney with strength 1 at full output.</summary>
        public const float ChimneyPuffsPerSecond = 6f;

        /// <summary>Output of a chimney now: between 35 % and 100 % of its strength, following its plant cycle.</summary>
        public static float ChimneyOutput(SmokeSource source, float time)
        {
            float cycle = (float)Math.Sin(time * 2.0 * Math.PI / CycleSeconds + source.Phase);
            return source.Strength * (0.675f + 0.325f * cycle);
        }

        /// <summary>
        /// Horizontal speed (m/s) at which the wind carries the smoke. <paramref name="windStrength"/> is the bend of a
        /// tree top in meters (see WindModel): the default breeze (0.35 m) gives about 2 m/s, a gust a bit more.
        /// </summary>
        public static Vector2 Drift(Vector2 windDirection, float windStrength, float gust)
        {
            if (windDirection.LengthSquared() < 1e-6f || windStrength <= 0f) return Vector2.Zero;
            float speed = 0.6f + 4f * windStrength * (0.6f + 0.8f * Math.Max(0f, Math.Min(1f, gust)));
            return Vector2.Normalize(windDirection) * speed;
        }

        /// <summary>
        /// Exhaust of a vehicle between two simulation steps. Only diesel buses and trucks smoke: a thick dark
        /// burst while pulling away at low speed, faint puffs while idling, almost nothing while cruising.
        /// </summary>
        public static ExhaustPuff Exhaust(AmbientVehicleKind kind, float speed, float previousSpeed, float deltaTime)
        {
            if (kind != AmbientVehicleKind.Bus && kind != AmbientVehicleKind.Truck) return ExhaustPuff.None;
            if (deltaTime <= 0f) return ExhaustPuff.None;
            float acceleration = (speed - previousSpeed) / deltaTime;
            if (acceleration > 0.3f && speed < 8f) return new ExhaustPuff(10f, 0.85f);
            if (speed < 0.3f) return new ExhaustPuff(2f, 0.3f);
            return new ExhaustPuff(0.5f, 0.15f);
        }

        /// <summary>
        /// Whole puffs to emit this frame at <paramref name="rate"/> per second; the fraction left over is kept in
        /// <paramref name="carry"/> for the next frame, so low rates still puff now and then.
        /// </summary>
        public static int Emit(ref float carry, float rate, float deltaTime)
        {
            if (rate <= 0f || deltaTime <= 0f) return 0;
            carry += rate * deltaTime;
            int count = (int)carry;
            carry -= count;
            return count;
        }
    }
}
