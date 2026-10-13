using System;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// The ground rectangle a vehicle covers (X-Z, meters): centre, heading, length and width. Used to
    /// keep routes away from buildings and vehicles away from each other (separating axis test).
    /// </summary>
    public readonly struct Footprint
    {
        public Footprint(Vector2 centre, Vector2 heading, float length, float width)
        {
            Centre = centre;
            Forward = heading.LengthSquared() > 1e-8f ? Vector2.Normalize(heading) : new Vector2(0f, 1f);
            HalfLength = length / 2f;
            HalfWidth = width / 2f;
        }

        public Vector2 Centre { get; }
        public Vector2 Forward { get; }
        public float HalfLength { get; }
        public float HalfWidth { get; }

        /// <summary>Right-hand side of the heading (traffic keeps right).</summary>
        public Vector2 Right => new Vector2(Forward.Y, -Forward.X);

        /// <summary>True when the two rectangles, each grown by half of <paramref name="gap"/>, touch.</summary>
        public bool Overlaps(Footprint other, float gap = 0f)
        {
            float grow = gap / 2f;
            foreach (Vector2 axis in new[] { Forward, Right, other.Forward, other.Right })
            {
                float distance = Math.Abs(Vector2.Dot(other.Centre - Centre, axis));
                float reach = Extent(axis, grow) + other.Extent(axis, grow);
                if (distance >= reach) return false;
            }
            return true;
        }

        /// <summary>
        /// Gap between the two rectangles along the separating axis that keeps them furthest apart:
        /// positive when they are apart, negative when they overlap (how deep).
        /// </summary>
        public float Separation(Footprint other)
        {
            float best = float.MinValue;
            foreach (Vector2 axis in new[] { Forward, Right, other.Forward, other.Right })
            {
                float distance = Math.Abs(Vector2.Dot(other.Centre - Centre, axis));
                best = Math.Max(best, distance - Extent(axis, 0f) - other.Extent(axis, 0f));
            }
            return best;
        }

        /// <summary>True when the rectangle comes closer than <paramref name="margin"/> to an axis-aligned box.</summary>
        public bool Overlaps(Vector2 min, Vector2 max, float margin = 0f)
        {
            Vector2 boxCentre = (min + max) / 2f;
            Vector2 boxHalf = (max - min) / 2f + new Vector2(margin, margin);
            foreach (Vector2 axis in new[] { new Vector2(1f, 0f), new Vector2(0f, 1f), Forward, Right })
            {
                float distance = Math.Abs(Vector2.Dot(boxCentre - Centre, axis));
                float boxReach = boxHalf.X * Math.Abs(axis.X) + boxHalf.Y * Math.Abs(axis.Y);
                if (distance >= Extent(axis, 0f) + boxReach) return false;
            }
            return true;
        }

        private float Extent(Vector2 axis, float grow) =>
            (HalfLength + grow) * Math.Abs(Vector2.Dot(Forward, axis)) + (HalfWidth + grow) * Math.Abs(Vector2.Dot(Right, axis));
    }
}
