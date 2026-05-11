using UnityEngine;

namespace VoxelPlay {

    [CreateAssetMenu(menuName = "Voxel Play/MicroVoxels Definition", fileName = "MicroVoxelsDefinition", order = 103)]
    public class MicroVoxelsDefinition : ScriptableObject {

        public MicroVoxels microVoxels;

        void OnEnable() {
            if (microVoxels != null) {
                microVoxels.isShared = true;
            }
        }

        /// <summary>
        /// Creates a clone of the microvoxels data for safe modification
        /// </summary>
        public MicroVoxels GetMicroVoxelsClone() {
            if (microVoxels == null) return null;
            return microVoxels.Clone();
        }

    }

}
