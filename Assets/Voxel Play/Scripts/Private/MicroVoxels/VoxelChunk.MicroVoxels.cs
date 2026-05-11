using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelChunk : MonoBehaviour {

        public bool usesMicroVoxels;
        public Dictionary<int, MicroVoxels> microVoxels;

        public void SetMicroVoxels (int voxelIndex, MicroVoxels mv) {
            if (mv == null || mv.isEmpty || mv.isFullSingleMaterial) {
                ClearMicroVoxels(voxelIndex);
                return;
            }
            if (microVoxels == null) {
                microVoxels = new Dictionary<int, MicroVoxels>();
            }
            microVoxels[voxelIndex] = mv;
            byte newOpaque = mv.GetOpaqueProportional();
            if (voxels[voxelIndex].opaque != newOpaque) {
                voxels[voxelIndex].opaque = newOpaque;
                voxelSignature = -1;
            }
            usesMicroVoxels = true;
        }

        public bool ClearMicroVoxels (int voxelIndex) {
            if (microVoxels == null) {
                usesMicroVoxels = false;
                return false;
            }
            if (microVoxels.Remove(voxelIndex)) {
                voxels[voxelIndex].opaque = voxels[voxelIndex].type.opaque;
            }
            usesMicroVoxels = microVoxels.Count > 0;
            return true;
        }

        public bool ClearMicroVoxels () {
            if (microVoxels == null) {
                usesMicroVoxels = false;
                return false;
            }
            foreach (var kvp in microVoxels) {
                voxels[kvp.Key].opaque = voxels[kvp.Key].type.opaque;
            }
            microVoxels.Clear();
            microVoxels = null;
            return true;
        }
    }

}