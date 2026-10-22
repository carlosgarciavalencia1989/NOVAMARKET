using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Scene body of a person (street, metro platform or CEDI). It glides to the pose of its
    /// <see cref="Pedestrian"/> and walks: with an <see cref="Animator"/> it sets the float parameter
    /// <c>Speed</c> (m/s) — a Mixamo, Synty or Quaternius humanoid with an idle/walk blend works as is;
    /// without one it swings the simple model's legs and arms. No logic and no collider.
    /// </summary>
    public class PedestrianView : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");

        [Tooltip("Child holding the visible model (its local transform fixes packs with other pivots or axes).")]
        [SerializeField] private Transform model;
        [Tooltip("Optional: an Animator with a float parameter \"Speed\" (m/s).")]
        [SerializeField] private Animator animator;
        [Tooltip("Renderers painted with the clothes colour of the visual profile (optional).")]
        [SerializeField] private Renderer[] clothes = new Renderer[0];
        [Header("Simple model (when there is no Animator)")]
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField, Range(0f, 50f)] private float swingDegrees = 28f;
        [SerializeField, Tooltip("Strides per meter walked.")] private float stridesPerMeter = 0.75f;

        private Pedestrian person;
        private Vector3 fromPosition, toPosition;
        private Quaternion fromRotation, toRotation;
        private float phase;
        private bool hasSpeedParameter;

        public Pedestrian Person => person;

        public void Bind(Pedestrian agent, Material clothesColour)
        {
            person = agent;
            gameObject.name = agent.ToString();
            if (clothesColour != null)
                foreach (Renderer part in clothes)
                    if (part != null) part.sharedMaterial = clothesColour;
            hasSpeedParameter = false;
            if (animator != null)
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    if (parameter.nameHash == SpeedParameter) hasSpeedParameter = true;
            fromPosition = toPosition = Position();
            fromRotation = toRotation = Rotation();
            transform.SetPositionAndRotation(toPosition, toRotation);
            phase = (agent.Id % 7) * 0.9f;
            gameObject.SetActive(true);
        }

        public void Unbind()
        {
            person = null;
            gameObject.SetActive(false);
        }

        /// <summary>Called right after each simulation step.</summary>
        public void Capture()
        {
            if (person == null) return;
            fromPosition = toPosition;
            fromRotation = toRotation;
            toPosition = Position();
            toRotation = Rotation();
        }

        /// <param name="alpha">How far the frame is between the last two simulation steps (0..1).</param>
        public void Sync(float alpha, float deltaTime)
        {
            if (person == null) return;
            transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, alpha),
                Quaternion.Slerp(fromRotation, toRotation, alpha));

            if (hasSpeedParameter)
            {
                animator.SetFloat(SpeedParameter, person.Speed);
                return;
            }
            if (leftLeg == null) return;
            phase += person.Speed * stridesPerMeter * deltaTime * Mathf.PI;
            float swing = person.Speed > 0.05f ? Mathf.Sin(phase) * swingDegrees : 0f;
            leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);
        }

        private Vector3 Position() => new Vector3(person.Position.X, person.Height, person.Position.Y);

        private Quaternion Rotation() =>
            Quaternion.LookRotation(new Vector3(person.Heading.X, 0f, person.Heading.Y), Vector3.up);
    }
}
