using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// A route on the ground made of straight segments. Points are world X/Z in meters
    /// (<see cref="Vector2.X"/> = world x, <see cref="Vector2.Y"/> = world z).
    /// </summary>
    public sealed class Polyline
    {
        private readonly Vector2[] points;
        private readonly float[] cumulative;

        public Polyline(IEnumerable<Vector2> points)
        {
            this.points = RemoveDuplicates(points);
            if (this.points.Length < 2) throw new ArgumentException("A polyline needs at least two distinct points.", nameof(points));

            cumulative = new float[this.points.Length];
            for (int i = 1; i < this.points.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(this.points[i - 1], this.points[i]);
        }

        public Polyline(params Vector2[] points) : this((IEnumerable<Vector2>)points) { }

        public float Length => cumulative[cumulative.Length - 1];
        public IReadOnlyList<Vector2> Points => points;
        public Vector2 Start => points[0];
        public Vector2 End => points[points.Length - 1];

        /// <summary>Point at a distance from the start, clamped to the route.</summary>
        public Vector2 PointAt(float distance)
        {
            int i = SegmentAt(distance, out float t);
            return Vector2.Lerp(points[i], points[i + 1], t);
        }

        /// <summary>Unit direction of travel at a distance from the start.</summary>
        public Vector2 DirectionAt(float distance)
        {
            int i = SegmentAt(distance, out _);
            return Vector2.Normalize(points[i + 1] - points[i]);
        }

        private int SegmentAt(float distance, out float t)
        {
            distance = Math.Max(0f, Math.Min(Length, distance));
            int i = 0;
            while (i < points.Length - 2 && cumulative[i + 1] < distance) i++;
            float segment = cumulative[i + 1] - cumulative[i];
            t = segment > 0f ? (distance - cumulative[i]) / segment : 0f;
            return i;
        }

        private static Vector2[] RemoveDuplicates(IEnumerable<Vector2> source)
        {
            var result = new List<Vector2>();
            foreach (Vector2 point in source)
                if (result.Count == 0 || Vector2.DistanceSquared(result[result.Count - 1], point) > 1e-6f)
                    result.Add(point);
            return result.ToArray();
        }
    }
}
