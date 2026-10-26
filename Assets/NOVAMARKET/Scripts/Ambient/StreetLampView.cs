using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// A street lamp post: lights its lens and the warm pool of light under it when the lamps are on. The pivot
    /// is at the foot of the post, +Z pointing over the road. Swap the model freely; only these renderers matter.
    /// </summary>
    public class StreetLampView : MonoBehaviour
    {
        [SerializeField] private Renderer[] lenses = new Renderer[0];
        [Tooltip("Shown only while the lamp is on (the light it throws on the road).")]
        [SerializeField] private Renderer[] glows = new Renderer[0];
        [SerializeField] private Material lensOff;
        [SerializeField] private Material lensOn;

        private bool? shown;

        public void SetOn(bool on)
        {
            if (shown == on) return;
            shown = on;
            foreach (Renderer lens in lenses)
                if (lens != null && (on ? lensOn : lensOff) != null) lens.sharedMaterial = on ? lensOn : lensOff;
            foreach (Renderer glow in glows)
                if (glow != null) glow.enabled = on;
        }
    }
}
