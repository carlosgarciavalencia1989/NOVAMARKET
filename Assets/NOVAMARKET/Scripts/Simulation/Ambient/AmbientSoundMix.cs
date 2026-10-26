using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// Rules of the ambient sound mix: the street (engines, metro, beepers, wind) is only heard with the camera
    /// close to the ground; from the strategy view only the music plays. Engines rise in pitch with speed, and
    /// only the sources nearest to the listener get one of the few 3D voices. Plain C#; the scene moves the sources.
    /// </summary>
    public static class AmbientSoundMix
    {
        /// <summary>Camera height (m) below which the street is heard at full volume, and above which it is silent.</summary>
        public const float StreetHeight = 35f;
        public const float SilentHeight = 90f;

        /// <summary>How much of the street is heard (0..1) with the camera at <paramref name="height"/> meters.</summary>
        public static float NearGround(float height)
        {
            float t = Clamp01((SilentHeight - height) / (SilentHeight - StreetHeight));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Volume of the wind (0..1), louder in gusts.</summary>
        public static float WindVolume(float gust) => 0.5f + 0.5f * Clamp01(gust);

        /// <summary>Pitch of an engine loop: 1 at idle, up to about 1.8 at full speed (diesels rise less).</summary>
        public static float EnginePitch(AmbientVehicleKind kind, float speed)
        {
            float top = AmbientVehicleSpec.For(kind).MaxSpeed;
            float range = kind == AmbientVehicleKind.Bus || kind == AmbientVehicleKind.Truck ? 0.5f
                : kind == AmbientVehicleKind.Moto ? 1f : 0.8f;
            return 1f + range * Clamp01(speed / top);
        }

        /// <summary>Engine volume: a murmur at idle, louder when moving (motos and diesels are louder).</summary>
        public static float EngineVolume(AmbientVehicleKind kind, float speed)
        {
            float loud = kind switch
            {
                AmbientVehicleKind.Bus => 1f,
                AmbientVehicleKind.Truck => 1f,
                AmbientVehicleKind.Moto => 0.8f,
                _ => 0.55f
            };
            return loud * Lerp(0.45f, 1f, Clamp01(speed / 10f));
        }

        /// <summary>
        /// Indices of the <paramref name="count"/> positions nearest to <paramref name="listener"/> and within
        /// <paramref name="radius"/>, nearest first.
        /// </summary>
        public static List<int> Nearest(IReadOnlyList<Vector2> positions, Vector2 listener, int count, float radius)
        {
            var candidates = new List<(float Distance, int Index)>();
            float radiusSq = radius * radius;
            for (int i = 0; i < positions.Count; i++)
            {
                float d = Vector2.DistanceSquared(positions[i], listener);
                if (d <= radiusSq) candidates.Add((d, i));
            }
            candidates.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Index.CompareTo(b.Index));
            var result = new List<int>(Math.Min(count, candidates.Count));
            for (int i = 0; i < candidates.Count && i < count; i++) result.Add(candidates[i].Index);
            return result;
        }

        private static float Clamp01(float x) => Math.Max(0f, Math.Min(1f, x));
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
