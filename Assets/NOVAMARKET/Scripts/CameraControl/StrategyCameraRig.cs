using UnityEngine;

namespace NovaMarket.CameraControl
{
    /// <summary>
    /// Plain C# model of an RTS camera orbiting a focus point on the ground: pan, drag, zoom,
    /// stepped rotation, smoothing and valley bounds. Holds no Unity objects, so it is unit-tested
    /// without a scene; <see cref="StrategyCameraController"/> feeds it input and applies the pose.
    /// </summary>
    public sealed class StrategyCameraRig
    {
        private readonly StrategyCameraSettings settings;

        private Vector3 focus;
        private Vector3 targetFocus;
        private float distance;
        private float targetDistance;
        private float yaw;
        private float targetYaw;

        public StrategyCameraRig(StrategyCameraSettings settings, Vector3 focus, float yaw, float distance)
        {
            this.settings = settings;
            this.focus = targetFocus = ClampFocus(focus);
            this.distance = targetDistance = ClampDistance(distance);
            this.yaw = targetYaw = yaw;
        }

        public Vector3 Focus => focus;
        public Vector3 TargetFocus => targetFocus;
        public float Distance => distance;
        public float TargetDistance => targetDistance;
        public float Yaw => yaw;
        public float TargetYaw => targetYaw;

        public Quaternion Rotation => Quaternion.Euler(settings.Pitch, yaw, 0f);
        public Vector3 Position => focus - Rotation * Vector3.forward * distance;

        /// <summary>
        /// Moves the focus in view space: x = right, y = forward (screen up), relative to the yaw.
        /// Speed grows with zoom distance so the screen scrolls at a similar rate at any zoom.
        /// </summary>
        public void Pan(Vector2 direction, float deltaTime)
        {
            if (direction == Vector2.zero) return;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            Vector3 world = Quaternion.Euler(0f, targetYaw, 0f) * new Vector3(direction.x, 0f, direction.y);
            float speed = settings.PanSpeedPerDistance * targetDistance;
            targetFocus = ClampFocus(targetFocus + world * (speed * deltaTime));
        }

        /// <summary>Shifts the focus immediately by a ground-plane offset (used by mouse drag).</summary>
        public void Drag(Vector3 worldDelta)
        {
            Vector3 clamped = ClampFocus(focus + worldDelta);
            Vector3 applied = clamped - focus;
            focus = clamped;
            targetFocus = ClampFocus(targetFocus + applied);
        }

        /// <summary>Glides the focus to a ground point (used by the minimap); keeps zoom and yaw.</summary>
        public void FocusOn(Vector3 point) => targetFocus = ClampFocus(point);

        /// <summary>Positive steps zoom in, negative steps zoom out.</summary>
        public void Zoom(float steps)
        {
            float factor = Mathf.Pow(1f - settings.ZoomStepFraction, steps);
            targetDistance = ClampDistance(targetDistance * factor);
        }

        /// <summary>Positive steps turn the view clockwise (seen from above).</summary>
        public void Rotate(int steps)
        {
            targetYaw += steps * settings.RotationStepDegrees;
        }

        /// <summary>Eases the current pose towards the targets.</summary>
        public void Tick(float deltaTime)
        {
            focus = Vector3.Lerp(focus, targetFocus, DampFactor(settings.MoveSmoothTime, deltaTime));
            distance = Mathf.Lerp(distance, targetDistance, DampFactor(settings.ZoomSmoothTime, deltaTime));
            yaw = Mathf.Lerp(yaw, targetYaw, DampFactor(settings.RotateSmoothTime, deltaTime));
        }

        /// <summary>
        /// Scroll direction for a pointer near the screen border (x = right, y = up).
        /// Zero when the pointer is away from the borders or outside the screen.
        /// </summary>
        public static Vector2 EdgeScroll(Vector2 pointer, Vector2 screenSize, float edgePixels)
        {
            if (pointer.x < 0f || pointer.y < 0f || pointer.x > screenSize.x || pointer.y > screenSize.y)
                return Vector2.zero;

            float x = pointer.x <= edgePixels ? -1f : pointer.x >= screenSize.x - edgePixels ? 1f : 0f;
            float y = pointer.y <= edgePixels ? -1f : pointer.y >= screenSize.y - edgePixels ? 1f : 0f;
            return new Vector2(x, y);
        }

        private Vector3 ClampFocus(Vector3 point)
        {
            Rect bounds = settings.FocusBounds;
            return new Vector3(Mathf.Clamp(point.x, bounds.xMin, bounds.xMax), 0f,
                Mathf.Clamp(point.z, bounds.yMin, bounds.yMax));
        }

        private float ClampDistance(float value) =>
            Mathf.Clamp(value, settings.MinDistance, settings.MaxDistance);

        private static float DampFactor(float smoothTime, float deltaTime) =>
            smoothTime <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / smoothTime);
    }
}
