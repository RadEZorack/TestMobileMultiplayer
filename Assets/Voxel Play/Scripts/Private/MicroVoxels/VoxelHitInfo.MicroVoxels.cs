using System;

namespace VoxelPlay {

    public partial struct VoxelHitInfo {

        public bool hasMicroVoxels {
            get {
                return chunk != null && chunk.microVoxels != null && chunk.microVoxels.ContainsKey(voxelIndex);
            }
        }

        /// <summary>
        /// Returns the slab selection based on hit position and microvoxel presence.
        /// -1 = bottom slab, 0 = no microvoxels present, 1 = top slab.
        /// </summary>
        public int slab {
            get {
                return point.y > Math.Floor(voxelCenter.y) + 0.5 ? 1 : -1;
            }
        }
    }

}