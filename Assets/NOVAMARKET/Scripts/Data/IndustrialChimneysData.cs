using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Ambient;
using UnityEngine;
using Num = System.Numerics;

namespace NovaMarket.Data
{
    /// <summary>
    /// The chimneys of the industrial zone (the top of each one and how hard its plant works), recorded by the
    /// environment builder on each rebuild. The smoke rises from them. Do not edit the positions by hand.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Industrial Chimneys", fileName = "IndustrialChimneys_")]
    public sealed class IndustrialChimneysData : NovaDataAsset
    {
        [Serializable]
        private struct Chimney
        {
            public Vector3 top;
            [Range(0.3f, 2f)] public float strength;
            public float phase;
        }

        [SerializeField] private Chimney[] chimneys = new Chimney[0];

        public int Count => chimneys.Length;

        public List<SmokeSource> ToSources()
        {
            var list = new List<SmokeSource>(chimneys.Length);
            foreach (Chimney c in chimneys)
                list.Add(new SmokeSource(new Num.Vector3(c.top.x, c.top.y, c.top.z), c.strength, c.phase));
            return list;
        }

        /// <summary>Replaces the stored chimneys (used by the Editor exporter).</summary>
        public void SetSources(IReadOnlyList<SmokeSource> source)
        {
            chimneys = new Chimney[source.Count];
            for (int i = 0; i < source.Count; i++)
                chimneys[i] = new Chimney
                {
                    top = new Vector3(source[i].Top.X, source[i].Top.Y, source[i].Top.Z),
                    strength = source[i].Strength,
                    phase = source[i].Phase
                };
        }
    }
}
