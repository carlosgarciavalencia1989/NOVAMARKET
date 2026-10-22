using System;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// The valley wind: a steady breeze from one direction with gusts that build up and die down smoothly,
    /// and a direction that wanders a little. Plain C#: call <see cref="Tick"/>; the scene sends the result to
    /// the foliage shader.
    /// </summary>
    public sealed class WindModel
    {
        private readonly Random random;
        private float gustTarget;
        private float gustTimer;
        private float wander;

        public WindModel(float baseDirectionDegrees = 250f, float strength = 0.35f, int seed = 4)
        {
            BaseDirectionDegrees = baseDirectionDegrees;
            Strength = strength;
            random = new Random(seed);
            Direction = FromDegrees(baseDirectionDegrees);
        }

        /// <summary>Where the wind blows to, degrees clockwise from north (+Z) — 250 = towards the west-southwest.</summary>
        public float BaseDirectionDegrees { get; set; }

        /// <summary>Bend of a tree top in a steady breeze, meters.</summary>
        public float Strength { get; set; }

        /// <summary>Unit direction the wind blows to (world X/Z).</summary>
        public Vector2 Direction { get; private set; }

        /// <summary>Gust intensity now, 0 (calm) to 1 (strong gust).</summary>
        public float Gust { get; private set; }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            gustTimer -= deltaTime;
            if (gustTimer <= 0f)
            {
                // A new gust (or a lull) every few seconds.
                gustTarget = random.NextDouble() < 0.35 ? 0.6f + 0.4f * (float)random.NextDouble() : 0.1f * (float)random.NextDouble();
                gustTimer = 3f + 6f * (float)random.NextDouble();
            }
            float rate = gustTarget > Gust ? 0.8f : 0.35f; // gusts build faster than they fade
            Gust += (gustTarget - Gust) * Math.Min(1f, rate * deltaTime);
            Gust = Math.Max(0f, Math.Min(1f, Gust));

            wander += deltaTime * 0.05f;
            float wobble = (float)Math.Sin(wander) * 12f;
            Direction = FromDegrees(BaseDirectionDegrees + wobble);
        }

        private static Vector2 FromDegrees(float degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return new Vector2((float)Math.Sin(radians), (float)Math.Cos(radians));
        }
    }
}
