using System;

namespace VoxelPlay {

    /// <summary>
    /// A voxel index represents the location of a voxel in the world
    /// </summary>
    public struct VoxelIndex : IEquatable<VoxelIndex> {

        /// <summary>
        /// Position in world space.
        /// </summary>
        public Vector3d position;

        /// <summary>
        /// The chunk to which this voxel belongs to
        /// </summary>
        public VoxelChunk chunk;

        /// <summary>
        /// The index of this voxel in the chunk.voxels[] array
        /// </summary>
        public int voxelIndex;

        /// <summary>
        /// The distance to the center (sqr distance) of the given position to GetVoxelIndices call.
        /// </summary>
        public float sqrDistance;

        /// <summary>
        /// The damage applied to a voxel. Used only with VoxelDamage methods.
        /// </summary>
        public int damageTaken;

        /// <summary>
        /// Utility field that can be used freely for any purpose
        /// </summary>
        public int temp;

        /// <summary>
        /// The custom data of this voxel. Used for custom voxel data.
        /// </summary>
        public int customData;
        /// <summary>
        /// Returns the voxel definition of this voxel. Chunk and voxelIndex must be valid.
        /// </summary>
        public VoxelDefinition type {
            get {
                if ((object)chunk != null && voxelIndex >= 0) {
                    return chunk.voxels[voxelIndex].type;
                }
                return null;
            }
            set {
                if (value != null && (object)chunk != null && voxelIndex >= 0) {
                    chunk.voxels[voxelIndex].typeIndex = value.index;
                }
            }
        }

        /// <summary>
        /// Returns the voxel definition (integer index) in the env.voxelDefinitions array for this voxel. Chunk and voxelIndex must be valid.
        /// </summary>
        /// <value>The index of the type.</value>
        public ushort typeIndex {
            get {
                if ((object)chunk != null && voxelIndex >= 0) {
                    return chunk.voxels[voxelIndex].typeIndex;
                }
                return 0;
            }
            set {
                if ((object)chunk != null && voxelIndex >= 0) {
                    chunk.voxels[voxelIndex].typeIndex = value;
                }
            }
        }

        public static VoxelIndex Null = new VoxelIndex();

        public bool Equals(VoxelIndex other) {
            return chunk == other.chunk && voxelIndex == other.voxelIndex;
        }

        public override bool Equals(object obj) {
            return obj is VoxelIndex other && Equals(other);
        }

        public override int GetHashCode() {
            int hash = 17;
            hash = hash * 23 + (chunk != null ? chunk.GetHashCode() : 0);
            hash = hash * 23 + voxelIndex;
            return hash;
        }
    }
}