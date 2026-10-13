using System;
using UnityEngine;

namespace NovaMarket.CameraControl
{
    /// <summary>
    /// Tunable values for the strategy camera. Distances in meters, speeds in m/s, angles in degrees.
    /// Defaults frame the CEDI Medellín valley.
    /// </summary>
    [Serializable]
    public sealed class StrategyCameraSettings
    {
        [Header("View")]
        [SerializeField, Range(20f, 80f)] private float pitch = 50f;
        [SerializeField, Range(20f, 70f)] private float fieldOfView = 40f;

        [Header("Zoom (distance from the focus point)")]
        [SerializeField] private float minDistance = 35f;
        [SerializeField] private float maxDistance = 450f;
        [SerializeField] private float startDistance = 190f;
        [SerializeField, Range(0.05f, 0.5f)] private float zoomStepFraction = 0.15f;

        [Header("Movement")]
        [Tooltip("Pan speed per meter of zoom distance: zoomed out moves faster.")]
        [SerializeField] private float panSpeedPerDistance = 0.8f;
        [Tooltip("Width in pixels of the screen border that scrolls the camera.")]
        [SerializeField] private float edgeScrollPixels = 12f;
        [Tooltip("Area the focus point may move in: the flat floor of the valley (x, z).")]
        [SerializeField] private Rect focusBounds = Rect.MinMaxRect(-780f, -420f, 780f, 420f);
        [Tooltip("Minimum height of the camera above the terrain.")]
        [SerializeField] private float groundClearance = 15f;
        [Tooltip("Minimum space between the camera and building roofs (downtown towers).")]
        [SerializeField] private float buildingClearance = 10f;

        [Header("Rotation")]
        [SerializeField] private float rotationStepDegrees = 45f;

        [Header("Smoothing (seconds)")]
        [SerializeField] private float moveSmoothTime = 0.12f;
        [SerializeField] private float zoomSmoothTime = 0.15f;
        [SerializeField] private float rotateSmoothTime = 0.2f;

        public float Pitch => pitch;
        public float FieldOfView => fieldOfView;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float StartDistance => startDistance;
        public float ZoomStepFraction => zoomStepFraction;
        public float PanSpeedPerDistance => panSpeedPerDistance;
        public float EdgeScrollPixels => edgeScrollPixels;
        public Rect FocusBounds => focusBounds;
        public float GroundClearance => groundClearance;
        public float BuildingClearance => buildingClearance;
        public float RotationStepDegrees => rotationStepDegrees;
        public float MoveSmoothTime => moveSmoothTime;
        public float ZoomSmoothTime => zoomSmoothTime;
        public float RotateSmoothTime => rotateSmoothTime;
    }
}
