using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Scene body of a metro train: its pivot is the train centre on the rails, front towards +Z. It glides
    /// between simulation steps and opens its doors at the platforms; no logic and no collider. To use a
    /// model from a 3D pack, put it under <see cref="model"/> and fill the door references (optional).
    /// </summary>
    public class MetroTrainView : MonoBehaviour
    {
        [Tooltip("Child holding the visible model (its local transform fixes packs with other pivots or axes).")]
        [SerializeField] private Transform model;
        [Tooltip("Door panels that change material while the doors are open.")]
        [SerializeField] private Renderer[] doors = new Renderer[0];
        [SerializeField] private Material doorClosed;
        [SerializeField] private Material doorOpen;

        private MetroTrain train;
        private float railHeight;
        private bool doorsShown;
        private Vector3 fromPosition, toPosition;
        private Quaternion fromRotation, toRotation;

        public MetroTrain Train => train;

        public void Bind(MetroTrain agent, float rails)
        {
            train = agent;
            railHeight = rails;
            gameObject.name = agent.ToString();
            fromPosition = toPosition = Position();
            fromRotation = toRotation = Rotation();
            transform.SetPositionAndRotation(toPosition, toRotation);
            doorsShown = !agent.DoorsOpen;
            Sync(1f);
        }

        /// <summary>Called right after each simulation step.</summary>
        public void Capture()
        {
            if (train == null) return;
            fromPosition = toPosition;
            fromRotation = toRotation;
            toPosition = Position();
            toRotation = Rotation();
            // A train returning on the other track jumps there: no gliding across.
            if ((toPosition - fromPosition).sqrMagnitude > 400f)
            {
                fromPosition = toPosition;
                fromRotation = toRotation;
            }
        }

        /// <param name="alpha">How far the frame is between the last two simulation steps (0..1).</param>
        public void Sync(float alpha)
        {
            if (train == null) return;
            transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, alpha),
                Quaternion.Slerp(fromRotation, toRotation, alpha));
            if (doorsShown == train.DoorsOpen) return;
            doorsShown = train.DoorsOpen;
            Material material = doorsShown ? doorOpen : doorClosed;
            if (material == null) return;
            foreach (Renderer door in doors)
                if (door != null) door.sharedMaterial = material;
        }

        private Vector3 Position() => new Vector3(train.Position.X, railHeight, train.Position.Y);

        private Quaternion Rotation() =>
            Quaternion.LookRotation(new Vector3(train.Heading.X, 0f, train.Heading.Y), Vector3.up);
    }
}
