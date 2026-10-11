using NovaMarket.Interaction;
using NovaMarket.Simulation;
using UnityEngine;

namespace NovaMarket.Operation
{
    /// <summary>
    /// Scene body of a simulated vehicle (truck, turbo or forklift). Each frame it copies the
    /// agent's position and smoothly turns towards its heading; it holds no logic of its own.
    /// The prefab's pivot is the vehicle centre on the ground and its front points to +Z.
    /// </summary>
    public class VehicleVisual : InteractableObject, ISelectionDetailsProvider
    {
        [Tooltip("How fast the body turns towards the route direction.")]
        [SerializeField] private float turnSharpness = 6f;
        [Tooltip("Pallet shown on the forks while the forklift carries one (forklifts only).")]
        [SerializeField] private GameObject carriedPallet;

        private VehicleAgent agent;

        public VehicleAgent Agent => agent;

        public SelectionDetails GetDetails() => agent?.Describe();

        public void Bind(VehicleAgent vehicle)
        {
            agent = vehicle;
            gameObject.name = $"{vehicle.Kind}_{vehicle.Plate}";
            transform.SetPositionAndRotation(TargetPosition(), TargetRotation());
            UpdateCarriedPallet();
        }

        private void LateUpdate()
        {
            if (agent == null) return;
            float blend = 1f - Mathf.Exp(-turnSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(TargetPosition(),
                Quaternion.Slerp(transform.rotation, TargetRotation(), blend));
            UpdateCarriedPallet();
        }

        private Vector3 TargetPosition() => new Vector3(agent.Position.X, agent.FloorHeight, agent.Position.Y);

        private Quaternion TargetRotation() =>
            Quaternion.LookRotation(new Vector3(agent.Heading.X, 0f, agent.Heading.Y), Vector3.up);

        private void UpdateCarriedPallet()
        {
            if (carriedPallet != null && agent is ForkliftAgent forklift && carriedPallet.activeSelf != forklift.IsCarrying)
                carriedPallet.SetActive(forklift.IsCarrying);
        }
    }
}
