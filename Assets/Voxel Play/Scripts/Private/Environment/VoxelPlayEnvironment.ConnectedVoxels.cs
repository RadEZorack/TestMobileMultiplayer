using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        void InitConnectedVoxels() {

            // Add tile rules
            ConnectedVoxel[] trs = Resources.LoadAll<ConnectedVoxel>(world.resourcesLocation);
            for (int k = 0; k < trs.Length; k++) {
                ConnectedVoxel tr = trs[k];
                tr.Init(this);
            }
        }

    }


}
