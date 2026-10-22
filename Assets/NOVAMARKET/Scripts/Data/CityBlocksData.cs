using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>
    /// The blocks of the valley and what each holds (city, downtown, park, industrial), recorded by the
    /// environment builder on each rebuild. The people walking build their sidewalks and crossings from it.
    /// Do not edit by hand.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "City Blocks", fileName = "CityBlocks_")]
    public sealed class CityBlocksData : NovaDataAsset
    {
        [Serializable]
        private struct Block
        {
            public Rect area;
            public CityBlockKind kind;
        }

        [SerializeField] private Block[] blocks = new Block[0];

        public int Count => blocks.Length;

        public List<CityBlock> ToBlocks()
        {
            var list = new List<CityBlock>(blocks.Length);
            foreach (Block block in blocks)
                list.Add(new CityBlock(block.area.xMin, block.area.yMin, block.area.xMax, block.area.yMax, block.kind));
            return list;
        }

        /// <summary>Replaces the stored blocks (used by the Editor exporter).</summary>
        public void SetBlocks(IReadOnlyList<CityBlock> source)
        {
            blocks = new Block[source.Count];
            for (int i = 0; i < source.Count; i++)
                blocks[i] = new Block
                {
                    area = Rect.MinMaxRect(source[i].XMin, source[i].ZMin, source[i].XMax, source[i].ZMax),
                    kind = source[i].Kind
                };
        }
    }
}
