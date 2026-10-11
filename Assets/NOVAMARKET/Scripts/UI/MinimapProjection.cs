using UnityEngine;

namespace NovaMarket.UI
{
    /// <summary>
    /// Maps between world ground positions (meters) and the square minimap. North (+Z) is up.
    /// Normalized coordinates go 0..1 with y up; local coordinates are UI pixels with y down.
    /// </summary>
    public sealed class MinimapProjection
    {
        private readonly Vector2 center;
        private readonly float size;

        /// <param name="worldCenter">World X/Z at the middle of the minimap.</param>
        /// <param name="worldSize">Side of the square area shown, in meters.</param>
        public MinimapProjection(Vector2 worldCenter, float worldSize)
        {
            center = worldCenter;
            size = worldSize;
        }

        public Vector2 WorldCenter => center;
        public float WorldSize => size;

        public Vector2 WorldToNormalized(Vector3 world) =>
            new Vector2((world.x - center.x) / size + 0.5f, (world.z - center.y) / size + 0.5f);

        public Vector3 NormalizedToWorld(Vector2 normalized) =>
            new Vector3(center.x + (normalized.x - 0.5f) * size, 0f, center.y + (normalized.y - 0.5f) * size);

        public Vector2 WorldToLocal(Vector3 world, Vector2 elementSize)
        {
            Vector2 normalized = WorldToNormalized(world);
            return new Vector2(normalized.x * elementSize.x, (1f - normalized.y) * elementSize.y);
        }

        public Vector3 LocalToWorld(Vector2 local, Vector2 elementSize) =>
            NormalizedToWorld(new Vector2(local.x / elementSize.x, 1f - local.y / elementSize.y));

        /// <summary>True when a local point lies inside the round minimap.</summary>
        public static bool IsInsideCircle(Vector2 local, Vector2 elementSize)
        {
            Vector2 offset = local - elementSize / 2f;
            float radius = Mathf.Min(elementSize.x, elementSize.y) / 2f;
            return offset.sqrMagnitude <= radius * radius;
        }

        /// <summary>Moves a local point outside the round minimap onto its edge (for drags that leave it).</summary>
        public static Vector2 ClampToCircle(Vector2 local, Vector2 elementSize)
        {
            Vector2 center = elementSize / 2f;
            float radius = Mathf.Min(elementSize.x, elementSize.y) / 2f;
            return center + Vector2.ClampMagnitude(local - center, radius);
        }
    }
}
