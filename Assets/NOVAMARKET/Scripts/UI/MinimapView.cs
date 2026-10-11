using NovaMarket.CameraControl;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Round minimap: shows the <see cref="MinimapRenderer"/> picture, the CEDI marker and the
    /// main camera's view. Clicking or dragging inside it makes the camera travel there.
    /// </summary>
    public sealed class MinimapView
    {
        private readonly VisualElement map;
        private readonly MinimapOverlay overlay;
        private readonly MinimapRenderer renderer;
        private readonly StrategyCameraController cameraController;
        private readonly Vector3 markerPosition;
        private readonly Vector3[] groundCorners = new Vector3[4];
        private readonly Vector2[] localCorners = new Vector2[4];

        public MinimapView(VisualElement root, MinimapRenderer renderer, StrategyCameraController cameraController,
            Vector3 markerPosition)
        {
            this.renderer = renderer;
            this.cameraController = cameraController;
            this.markerPosition = markerPosition;
            map = root.Q("minimap");
            overlay = root.Q<MinimapOverlay>("minimapOverlay");

            // A render texture can only be assigned from code; USS cannot reference it.
            map.style.backgroundImage = Background.FromRenderTexture(renderer.Texture);
            map.RegisterCallback<PointerDownEvent>(OnPointerDown);
            map.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            map.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        /// <summary>Call every frame to follow the camera.</summary>
        public void Update()
        {
            Vector2 size = map.contentRect.size;
            if (size.x < 1f || size.y < 1f) return;

            overlay.SetMarker(renderer.Projection.WorldToLocal(markerPosition, size));
            if (!TryGetGroundCorners(cameraController.Camera, groundCorners))
            {
                overlay.SetView(null);
                return;
            }
            for (int i = 0; i < 4; i++) localCorners[i] = renderer.Projection.WorldToLocal(groundCorners[i], size);
            overlay.SetView(localCorners);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !MinimapProjection.IsInsideCircle(evt.localPosition, map.contentRect.size)) return;
            map.CapturePointer(evt.pointerId);
            Travel(evt.localPosition);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (map.HasPointerCapture(evt.pointerId)) Travel(evt.localPosition);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (map.HasPointerCapture(evt.pointerId)) map.ReleasePointer(evt.pointerId);
        }

        private void Travel(Vector2 local)
        {
            Vector2 size = map.contentRect.size;
            Vector2 inside = MinimapProjection.ClampToCircle(local, size);
            cameraController.FocusOn(renderer.Projection.LocalToWorld(inside, size));
        }

        /// <summary>
        /// Where the four screen corners hit the ground plane (y = 0), in the order
        /// top-left, top-right, bottom-right, bottom-left. False if a corner looks at the sky.
        /// </summary>
        private static bool TryGetGroundCorners(Camera camera, Vector3[] corners)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            Vector2[] viewport = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                Ray ray = camera.ViewportPointToRay(viewport[i]);
                if (!ground.Raycast(ray, out float distance)) return false;
                corners[i] = ray.GetPoint(distance);
            }
            return true;
        }
    }
}
