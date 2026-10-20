using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Scene body of a traffic vehicle. It copies the pose of its <see cref="AmbientVehicle"/> and shows
    /// what the agent does (wheels turning, brake lights, a moto leaning in curves); it has no logic of its
    /// own and no collider (traffic cannot be selected). To use a model from a 3D pack, put it under
    /// <see cref="model"/> and fill the optional references: everything works without them.
    /// </summary>
    public class AmbientVehicleView : MonoBehaviour
    {
        [Tooltip("Child holding the visible model. Its local position/rotation/scale fix packs whose pivot or " +
                 "forward axis differ (the root stays on the ground at the body centre, front towards +Z).")]
        [SerializeField] private Transform model;
        [Tooltip("Renderers painted with the vehicle's body colour (from the visual profile).")]
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];
        [Tooltip("Wheel pivots; they spin around their local X axis.")]
        [SerializeField] private Transform[] wheels = new Transform[0];
        [SerializeField, Min(0.05f)] private float wheelRadius = 0.32f;
        [Header("Lights")]
        [SerializeField] private Renderer[] brakeLights = new Renderer[0];
        [SerializeField] private Material brakeOff;
        [SerializeField] private Material brakeOn;
        [SerializeField] private Renderer[] headlights = new Renderer[0];
        [SerializeField] private Material headlightOff;
        [SerializeField] private Material headlightOn;
        [Header("Motos")]
        [Tooltip("Pivot that leans into curves (motos); leave empty for other vehicles.")]
        [SerializeField] private Transform leanPivot;
        [SerializeField, Range(0f, 40f)] private float maxLeanDegrees = 25f;
        private AmbientVehicle vehicle;
        private bool braking;
        private bool headlightsShown;
        private float lean;
        private float wheelAngle;
        private Vector3 fromPosition, toPosition;
        private Quaternion fromRotation, toRotation;

        public AmbientVehicle Vehicle => vehicle;
        public Transform Model => model;

        public void Bind(AmbientVehicle agent, Material bodyColour)
        {
            vehicle = agent;
            gameObject.name = agent.ToString();
            if (bodyColour != null)
                foreach (Renderer body in bodyRenderers)
                    if (body != null) body.sharedMaterial = bodyColour;
            braking = !agent.IsBraking; // force the first refresh
            lean = 0f;
            fromPosition = toPosition = Position();
            fromRotation = toRotation = Rotation();
            transform.SetPositionAndRotation(toPosition, toRotation);
            gameObject.SetActive(true);
            Sync(1f, 0f, false);
        }

        public void Unbind()
        {
            vehicle = null;
            gameObject.SetActive(false);
        }

        /// <summary>Called right after each simulation step: the pose to glide to until the next step.</summary>
        public void Capture()
        {
            if (vehicle == null) return;
            fromPosition = toPosition;
            fromRotation = toRotation;
            toPosition = Position();
            toRotation = Rotation();
        }

        /// <summary>
        /// Called every frame by the runner (one loop for all vehicles, no Update per object). The simulation
        /// steps a few times per second; in between the body glides from the previous pose to the last one.
        /// </summary>
        /// <param name="alpha">How far the frame is between the last two simulation steps (0..1).</param>
        public void Sync(float alpha, float deltaTime, bool lightsOn)
        {
            if (vehicle == null) return;
            float previousYaw = transform.eulerAngles.y;
            transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, alpha),
                Quaternion.Slerp(fromRotation, toRotation, alpha));

            if (wheels.Length > 0)
            {
                wheelAngle = (wheelAngle + vehicle.Speed * deltaTime / wheelRadius * Mathf.Rad2Deg) % 360f;
                foreach (Transform wheel in wheels)
                    if (wheel != null) wheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
            }

            if (leanPivot != null && deltaTime > 0f)
            {
                // Lean with the turn rate (degrees per second) and the speed.
                float yawRate = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) / deltaTime;
                float wanted = Mathf.Clamp(-yawRate * vehicle.Speed * 0.012f, -maxLeanDegrees, maxLeanDegrees);
                lean = Mathf.Lerp(lean, wanted, 1f - Mathf.Exp(-6f * deltaTime));
                leanPivot.localRotation = Quaternion.Euler(0f, 0f, lean);
            }

            if (braking != vehicle.IsBraking)
            {
                braking = vehicle.IsBraking;
                SetMaterial(brakeLights, braking ? brakeOn : brakeOff);
            }
            if (headlightsShown != lightsOn)
            {
                headlightsShown = lightsOn;
                SetMaterial(headlights, lightsOn ? headlightOn : headlightOff);
            }
        }

        private Vector3 Position() => new Vector3(vehicle.Position.X, 0f, vehicle.Position.Y);

        private Quaternion Rotation() =>
            Quaternion.LookRotation(new Vector3(vehicle.Heading.X, 0f, vehicle.Heading.Y), Vector3.up);

        private static void SetMaterial(Renderer[] renderers, Material material)
        {
            if (material == null) return;
            foreach (Renderer part in renderers)
                if (part != null) part.sharedMaterial = material;
        }
    }
}
