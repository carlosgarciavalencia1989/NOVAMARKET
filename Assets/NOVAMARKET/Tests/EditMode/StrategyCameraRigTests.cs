using NovaMarket.CameraControl;
using NUnit.Framework;
using UnityEngine;

namespace NovaMarket.Tests.CameraControl
{
    public class StrategyCameraRigTests
    {
        private const float Tolerance = 0.01f;
        private StrategyCameraSettings settings;

        [SetUp]
        public void SetUp() => settings = new StrategyCameraSettings();

        private StrategyCameraRig CreateRig(float yaw = 0f, float distance = 100f) =>
            new StrategyCameraRig(settings, Vector3.zero, yaw, distance);

        private static void Settle(StrategyCameraRig rig)
        {
            for (int i = 0; i < 600; i++) rig.Tick(1f / 60f);
        }

        [Test]
        public void Camera_LooksDownAtThePitch_FromTheZoomDistance()
        {
            var rig = CreateRig(distance: 100f);

            Assert.AreEqual(settings.Pitch, rig.Rotation.eulerAngles.x, Tolerance);
            Assert.AreEqual(100f, Vector3.Distance(rig.Position, rig.Focus), Tolerance);
            Assert.AreEqual(100f * Mathf.Sin(settings.Pitch * Mathf.Deg2Rad), rig.Position.y, Tolerance);
        }

        [Test]
        public void Pan_Forward_FollowsTheYaw()
        {
            var north = CreateRig(yaw: 0f);
            north.Pan(Vector2.up, 1f);
            Assert.Greater(north.TargetFocus.z, 0f);
            Assert.AreEqual(0f, north.TargetFocus.x, Tolerance);

            var east = CreateRig(yaw: 90f);
            east.Pan(Vector2.up, 1f);
            Assert.Greater(east.TargetFocus.x, 0f);
            Assert.AreEqual(0f, east.TargetFocus.z, Tolerance);
        }

        [Test]
        public void Pan_IsFasterWhenZoomedOut()
        {
            var near = CreateRig(distance: 50f);
            var far = CreateRig(distance: 400f);

            near.Pan(Vector2.right, 0.1f);
            far.Pan(Vector2.right, 0.1f);

            Assert.Greater(far.TargetFocus.x, near.TargetFocus.x);
        }

        [Test]
        public void Pan_StaysInsideTheValleyBounds()
        {
            var rig = CreateRig();
            rig.Pan(new Vector2(1f, 1f), 1000f);

            Rect bounds = settings.FocusBounds;
            Assert.AreEqual(bounds.xMax, rig.TargetFocus.x, Tolerance);
            Assert.AreEqual(bounds.yMax, rig.TargetFocus.z, Tolerance);
        }

        [Test]
        public void Drag_MovesTheFocusImmediately_AndIsClamped()
        {
            var rig = CreateRig();
            rig.Drag(new Vector3(30f, 0f, -20f));
            Assert.AreEqual(new Vector3(30f, 0f, -20f), rig.Focus);

            rig.Drag(new Vector3(-10000f, 0f, 0f));
            Assert.AreEqual(settings.FocusBounds.xMin, rig.Focus.x, Tolerance);
        }

        [Test]
        public void Zoom_IsLimited()
        {
            var rig = CreateRig();

            rig.Zoom(100f);
            Assert.AreEqual(settings.MinDistance, rig.TargetDistance, Tolerance);

            rig.Zoom(-100f);
            Assert.AreEqual(settings.MaxDistance, rig.TargetDistance, Tolerance);
        }

        [Test]
        public void Rotate_StepsSmoothlyToTheTargetYaw()
        {
            var rig = CreateRig(yaw: 30f);
            rig.Rotate(1);

            Assert.AreEqual(30f + settings.RotationStepDegrees, rig.TargetYaw, Tolerance);
            rig.Tick(1f / 60f);
            Assert.That(rig.Yaw, Is.GreaterThan(30f).And.LessThan(rig.TargetYaw), "rotation should ease, not jump");

            Settle(rig);
            Assert.AreEqual(rig.TargetYaw, rig.Yaw, Tolerance);
        }

        [Test]
        public void EdgeScroll_PointsTowardsTheTouchedBorder()
        {
            var screen = new Vector2(1920f, 1080f);

            Assert.AreEqual(new Vector2(-1f, 0f), StrategyCameraRig.EdgeScroll(new Vector2(2f, 540f), screen, 12f));
            Assert.AreEqual(new Vector2(1f, 1f), StrategyCameraRig.EdgeScroll(new Vector2(1919f, 1079f), screen, 12f));
            Assert.AreEqual(Vector2.zero, StrategyCameraRig.EdgeScroll(new Vector2(960f, 540f), screen, 12f));
            Assert.AreEqual(Vector2.zero, StrategyCameraRig.EdgeScroll(new Vector2(-50f, 540f), screen, 12f),
                "a pointer outside the window must not scroll");
        }
    }
}
