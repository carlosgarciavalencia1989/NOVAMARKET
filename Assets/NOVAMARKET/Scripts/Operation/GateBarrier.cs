using UnityEngine;

namespace NovaMarket.Operation
{
    /// <summary>
    /// Boom barrier of a gate lane. Put it on the hinge (pivot) of the arm; it rotates around
    /// its local Z axis towards <see cref="openAngle"/> while <see cref="IsOpen"/> is true.
    /// </summary>
    public class GateBarrier : MonoBehaviour
    {
        [Tooltip("Angle of the raised arm in degrees; the sign sets which way it lifts.")]
        [SerializeField] private float openAngle = 80f;
        [Tooltip("Degrees per second.")]
        [SerializeField] private float speed = 110f;

        private float angle;

        public bool IsOpen { get; set; }

        private void Update()
        {
            angle = Mathf.MoveTowards(angle, IsOpen ? openAngle : 0f, speed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
