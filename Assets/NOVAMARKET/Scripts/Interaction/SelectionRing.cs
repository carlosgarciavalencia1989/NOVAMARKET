using UnityEngine;

namespace NovaMarket.Interaction
{
    /// <summary>
    /// Age of Empires–style selection ring: a flat elliptical band on the ground around the
    /// selected object, sized from its renderers' bounds, with a slow pulse.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SelectionRing : MonoBehaviour
    {
        [Tooltip("Band width in meters.")]
        [SerializeField] private float width = 0.7f;
        [Tooltip("Extra space between the object's footprint and the ring, in meters.")]
        [SerializeField] private float margin = 1.5f;
        [Tooltip("Height above the object's base, so the ring does not flicker against the floor.")]
        [SerializeField] private float groundOffset = 0.15f;
        [SerializeField] private float pulseAmount = 0.03f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField, Range(16, 128)] private int segments = 72;

        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Transform target;
        private Vector3 localBase;

        private void Awake()
        {
            mesh = new Mesh { name = "SelectionRing" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            Hide();
        }

        private void OnDestroy() => Destroy(mesh);

        private void Update()
        {
            if (!meshRenderer.enabled) return;
            if (target == null)
            {
                Hide(); // the selected object was destroyed (e.g. a truck left the CEDI)
                return;
            }
            Follow();
            float scale = 1f + pulseAmount * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            transform.localScale = new Vector3(scale, 1f, scale);
        }

        /// <summary>
        /// Places the ring around every mesh under <paramref name="target"/>, measured in the
        /// target's own axes so the ring keeps fitting while a vehicle moves and turns.
        /// </summary>
        public void Show(Transform target)
        {
            if (!TryGetLocalBounds(target, out Bounds bounds))
            {
                Hide();
                return;
            }

            // An ellipse through the corners of the footprint rectangle has radii extent·√2.
            float radiusX = bounds.extents.x * Mathf.Sqrt(2f) + margin;
            float radiusZ = bounds.extents.z * Mathf.Sqrt(2f) + margin;
            BuildEllipseRing(mesh, radiusX, radiusZ, width, segments);

            this.target = target;
            localBase = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Follow();
            meshRenderer.enabled = true;
        }

        public void Hide()
        {
            meshRenderer.enabled = false;
            target = null;
        }

        private void Follow() =>
            transform.SetPositionAndRotation(target.TransformPoint(localBase) + Vector3.up * groundOffset,
                Quaternion.Euler(0f, target.eulerAngles.y, 0f));

        /// <summary>Bounds of all meshes under the target, in the target's local space.</summary>
        public static bool TryGetLocalBounds(Transform target, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            Matrix4x4 toTarget = target.worldToLocalMatrix;
            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toLocal = toTarget * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (corner & 4) == 0 ? mesh.min.z : mesh.max.z);
                    Vector3 local = toLocal.MultiplyPoint3x4(point);
                    if (!found) bounds = new Bounds(local, Vector3.zero);
                    else bounds.Encapsulate(local);
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Fills <paramref name="target"/> with a flat elliptical band in the XZ plane, centred at
        /// the origin. The radii are measured to the middle of the band.
        /// </summary>
        public static void BuildEllipseRing(Mesh target, float radiusX, float radiusZ, float bandWidth, int segmentCount)
        {
            var vertices = new Vector3[segmentCount * 2];
            var triangles = new int[segmentCount * 6];
            float half = bandWidth / 2f;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / segmentCount;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(cos * (radiusX - half), 0f, sin * (radiusZ - half));
                vertices[i * 2 + 1] = new Vector3(cos * (radiusX + half), 0f, sin * (radiusZ + half));

                int next = (i + 1) % segmentCount;
                int t = i * 6;
                // Clockwise seen from above, so the band faces up.
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1;
                triangles[t + 4] = next * 2;
                triangles[t + 5] = next * 2 + 1;
            }

            target.Clear();
            target.vertices = vertices;
            target.triangles = triangles;
            target.RecalculateNormals();
            target.RecalculateBounds();
        }
    }
}
