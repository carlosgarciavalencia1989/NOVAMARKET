using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Traffic light of one approach: lights the lamp of the current colour. The pivot is on the moto stop
    /// line at the lane centre, +Z pointing where the traffic goes. Swap the model freely; only the lamp
    /// renderers and their on/off materials matter.
    /// </summary>
    public class TrafficLightView : MonoBehaviour
    {
        [SerializeField] private Renderer red;
        [SerializeField] private Renderer amber;
        [SerializeField] private Renderer green;
        [SerializeField] private Material redOn;
        [SerializeField] private Material amberOn;
        [SerializeField] private Material greenOn;
        [SerializeField] private Material lampOff;

        private SignalAspect shown = (SignalAspect)(-1);

        public void Show(SignalAspect aspect)
        {
            if (aspect == shown) return;
            shown = aspect;
            Set(red, aspect == SignalAspect.Red ? redOn : lampOff);
            Set(amber, aspect == SignalAspect.Amber ? amberOn : lampOff);
            Set(green, aspect == SignalAspect.Green ? greenOn : lampOff);
        }

        private static void Set(Renderer lamp, Material material)
        {
            if (lamp != null && material != null) lamp.sharedMaterial = material;
        }
    }
}
