
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelPlay {

    public class Region {

        public int x { get; private set; }
        public int z { get; private set; }
        public readonly string id;
        public readonly List<VoxelChunk> chunks;


        public bool IsModifiedSinceLastSave () {
            return chunks.Any(c => c.modifiedTimestamp > VoxelPlayEnvironment.stage);
        }

        public Region (int x, int z) {
            this.x = x;
            this.z = z;
            chunks = new List<VoxelChunk>();
            id = RegionPartitioner.GetStringId(x, z);
        }

        public void AddChunk (VoxelChunk chunk) {
            chunks.Add(chunk);
        }

        /// <summary>
        /// Returns the world bounds for this region (256x256 units per region)
        /// </summary>
        /// <returns>Bounds covering the entire region with a large Y extent</returns>
        public Bounds GetBounds () {
            Vector3 regionMin = new Vector3(x * RegionPartitioner.REGION_SIZE, 0, z * RegionPartitioner.REGION_SIZE);
            Vector3 regionMax = new Vector3(x * RegionPartitioner.REGION_SIZE + RegionPartitioner.REGION_SIZE, 10000, z * RegionPartitioner.REGION_SIZE + RegionPartitioner.REGION_SIZE);
            Vector3 center = (regionMin + regionMax) / 2;
            Vector3 size = regionMax - regionMin;
            return new Bounds(center, size);
        }

    }
}