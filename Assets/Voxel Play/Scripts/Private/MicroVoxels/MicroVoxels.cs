using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VoxelPlay {

    public enum MicroVoxelLayout {
        Default = 0, // Standard microvoxel layout (single material for all microvoxels)
        Slabs = 1,    // Bottom/top slab use different voxel definitions/textures
        TopCap = 2   // Bottom slab uses top-half texture (useful for terrain surface)
    }

    [Serializable]
    public partial class MicroVoxels {

        public const int COUNT_PER_AXIS = 16;
        public const int COUNT_PER_FACE = COUNT_PER_AXIS * COUNT_PER_AXIS;
        public const int COUNT_PER_VOXEL = COUNT_PER_AXIS * COUNT_PER_AXIS * COUNT_PER_AXIS;
        public const int COUNT_PER_VOXEL_HALF = COUNT_PER_VOXEL / 2;
        public const int COUNT_PER_AXIS_MINUS_ONE = COUNT_PER_AXIS - 1;
        public const int COUNT_PER_AXIS_HALF = COUNT_PER_AXIS / 2;

        public const float SIZE = 1f / COUNT_PER_AXIS;

        public static Vector3d SIZE_3D = new Vector3d(SIZE, SIZE, SIZE);
        public static Vector3d HALF_SIZE_3D = SIZE_3D / 2f;

        const int BITS_PER_ULONG = 64; // Bits in ulong

        // Hash constants for fully filled faces (will be computed at runtime for accuracy)
        static readonly ulong FULL_FACE_HASH_BACK;
        static readonly ulong FULL_FACE_HASH_RIGHT;
        static readonly ulong FULL_FACE_HASH_FORWARD;
        static readonly ulong FULL_FACE_HASH_LEFT;
        static readonly ulong FULL_FACE_HASH_TOP;
        static readonly ulong FULL_FACE_HASH_BOTTOM;

        // Hash constants for bottom and top halves (will be computed at runtime for accuracy)
        static readonly ulong BOTTOM_HALF_HASH;
        static readonly ulong TOP_HALF_HASH;
        static MicroVoxels _halfSurfaceVoxelTemplate;

        [NonSerialized]
        public MicroVoxelsPrototype prototype;

        // Secondary material to use for microvoxel rendering (optional)
        public VoxelDefinition secondaryType;
        public MicroVoxelLayout layout = MicroVoxelLayout.Default;

        [NonSerialized]
        public bool needsMeshDataUpdate;

        /// <summary>
        /// If true, this instance is shared by other voxels and should not be modified directly (use Clone() to modify)
        /// </summary>
        [NonSerialized]
        public bool isShared;

        public ulong[] gridData;
        public int count;


        static MicroVoxels () {
            // Create temporary microvoxels to compute full face hashes
            MicroVoxels temp = new MicroVoxels();
            temp.Fill();

            FULL_FACE_HASH_BACK = temp.ComputeFaceHash(Cube.Side.Back);
            FULL_FACE_HASH_RIGHT = temp.ComputeFaceHash(Cube.Side.Right);
            FULL_FACE_HASH_FORWARD = temp.ComputeFaceHash(Cube.Side.Forward);
            FULL_FACE_HASH_LEFT = temp.ComputeFaceHash(Cube.Side.Left);
            FULL_FACE_HASH_TOP = temp.ComputeFaceHash(Cube.Side.Top);
            FULL_FACE_HASH_BOTTOM = temp.ComputeFaceHash(Cube.Side.Bottom);

            BOTTOM_HALF_HASH = ComputeHalfHash(true);
            TOP_HALF_HASH = ComputeHalfHash(false);
        }

        public MicroVoxels () {
            int ulongCount = (COUNT_PER_VOXEL + BITS_PER_ULONG - 1) / BITS_PER_ULONG;
            gridData = new ulong[ulongCount];
            needsMeshDataUpdate = true;
            layout = MicroVoxelLayout.Default;
        }

        public MicroVoxels Clone () {
            MicroVoxels clone = new MicroVoxels();
            clone.gridData = (ulong[])gridData.Clone();
            clone.count = count;
            clone.prototype = prototype;
            clone.needsMeshDataUpdate = needsMeshDataUpdate;
            clone.secondaryType = secondaryType;
            clone.layout = layout;
            clone.isShared = false;
            return clone;
        }

        public void CopyFrom (MicroVoxels mv) {
            gridData = (ulong[])mv.gridData.Clone();
            count = mv.count;
            prototype = mv.prototype;
            needsMeshDataUpdate = mv.needsMeshDataUpdate;
            secondaryType = mv.secondaryType;
            layout = mv.layout;
        }

        /// <summary>
        /// Bottom half filled template with layout TopCap.
        /// Use for terrain surface so side quads render with top-half texture.
        /// Shared instance; edits require copy-on-write.
        /// </summary>
        public static MicroVoxels halfSurfaceVoxelTemplate {
            get {
                if (_halfSurfaceVoxelTemplate == null) {
                    MicroVoxels mv = new MicroVoxels();
                    mv.Clear();
                    for (int index = 0; index < COUNT_PER_VOXEL_HALF; index++) {
                        mv.SetOccupied(index);
                    }
                    mv.layout = MicroVoxelLayout.TopCap;
                    mv.isShared = true;
                    _halfSurfaceVoxelTemplate = mv;
                }
                return _halfSurfaceVoxelTemplate;
            }
        }

        static MicroVoxels _topHalfVoxelTemplate;
        static MicroVoxels _bottomHalfVoxelTemplate;

        /// <summary>
        /// Gets a top-half filled voxel template (shared, non-editable without copy-on-write).
        /// </summary>
        public static MicroVoxels topHalfVoxelTemplate {
            get {
                if (_topHalfVoxelTemplate == null) {
                    MicroVoxels mv = new MicroVoxels();
                    mv.Clear();
                    for (int index = COUNT_PER_VOXEL_HALF; index < COUNT_PER_VOXEL; index++) {
                        mv.SetOccupied(index);
                    }
                    mv.layout = MicroVoxelLayout.Default;
                    mv.isShared = true;
                    _topHalfVoxelTemplate = mv;
                }
                return _topHalfVoxelTemplate;
            }
        }

        /// <summary>
        /// Bottom half filled template with layout Default.
        /// Use for generic bottom slabs without top-cap side texturing.
        /// Shared instance; edits require copy-on-write.
        /// </summary>
        public static MicroVoxels bottomHalfDefaultVoxelTemplate {
            get {
                if (_bottomHalfVoxelTemplate == null) {
                    MicroVoxels mv = new MicroVoxels();
                    mv.Clear();
                    for (int index = 0; index < COUNT_PER_VOXEL_HALF; index++) {
                        mv.SetOccupied(index);
                    }
                    mv.layout = MicroVoxelLayout.Default;
                    mv.isShared = true;
                    _bottomHalfVoxelTemplate = mv;
                }
                return _bottomHalfVoxelTemplate;
            }
        }

        /// <summary>
        /// Compares this MicroVoxels instance with another for equality
        /// </summary>
        /// <param name="other">The other MicroVoxels to compare with</param>
        /// <returns>True if both MicroVoxels are identical, false otherwise</returns>
        public bool Equals (MicroVoxels other) {
            if (other == null) return false;

            // Compare secondary type
            if (secondaryType != other.secondaryType) return false;

            // Compare grid hash code (includes layout and grid data)
            return GetGridHashCode() == other.GetGridHashCode();
        }

        /// <summary>
        /// The full grid hash code includes the layout and the grid data
        /// </summary>
        /// <returns></returns>
        public ulong GetGridHashCode () {
            unchecked {
                ulong hash = 17;
                hash = hash * 31 + (ulong)layout;
                hash ^= hash >> 32;
                int count = gridData.Length;
                for (int i = 0; i < count; i++) {
                    hash = hash * 31 + gridData[i];
                    hash ^= hash >> 32;
                }
                return hash;
            }
        }

        ulong ComputeFaceHash (Cube.Side side) {
            // Compute rotation-invariant hash by using canonical coordinate indexing
            // Instead of sequential traversal, use coordinates in a canonical order
            unchecked {
                ulong hash = 17;
                int baseIndex;
                int strideX, strideY;

                // Precompute face-specific parameters to avoid switch in inner loop
                switch (side) {
                    case Cube.Side.Back: // -Z face (z = 0)
                        baseIndex = 0;
                        strideX = 1;
                        strideY = COUNT_PER_FACE;
                        break;
                    case Cube.Side.Forward: // +Z face (z = last)
                        baseIndex = COUNT_PER_AXIS_MINUS_ONE * COUNT_PER_AXIS;
                        strideX = 1;
                        strideY = COUNT_PER_FACE;
                        break;
                    case Cube.Side.Left: // -X face (x = 0)
                        baseIndex = 0;
                        strideX = COUNT_PER_AXIS;
                        strideY = COUNT_PER_FACE;
                        break;
                    case Cube.Side.Right: // +X face (x = last)
                        baseIndex = COUNT_PER_AXIS_MINUS_ONE;
                        strideX = COUNT_PER_AXIS;
                        strideY = COUNT_PER_FACE;
                        break;
                    case Cube.Side.Bottom: // -Y face (y = 0)
                        baseIndex = 0;
                        strideX = 1;
                        strideY = COUNT_PER_AXIS;
                        break;
                    case Cube.Side.Top: // +Y face (y = last)
                        baseIndex = COUNT_PER_AXIS_MINUS_ONE * COUNT_PER_FACE;
                        strideX = 1;
                        strideY = COUNT_PER_AXIS;
                        break;
                    default:
                        return 0;
                }

                for (int canonicalIndex = 0; canonicalIndex < COUNT_PER_FACE; canonicalIndex++) {
                    int x = canonicalIndex % COUNT_PER_AXIS;
                    int y = canonicalIndex / COUNT_PER_AXIS;

                    // Compute 3D index directly: base + x*strideX + y*strideY
                    int index = baseIndex + x * strideX + y * strideY;
                    int ulongIndex = index / BITS_PER_ULONG;
                    int bitPosition = index % BITS_PER_ULONG;
                    ulong bitValue = ((gridData[ulongIndex] & (1UL << bitPosition)) != 0) ? 1UL : 0UL;

                    hash = hash * 31 + bitValue;
                    hash ^= hash >> 32;
                }

                return hash;
            }
        }


        /// <summary>
        /// The occupancy hash code includes only the grid data
        /// </summary>
        /// <returns></returns>
        public ulong GetOccupancyHashCode () {
            unchecked {
                ulong hash = 17;
                int len = gridData.Length;
                for (int i = 0; i < len; i++) {
                    hash = hash * 31 + gridData[i];
                    hash ^= hash >> 32;
                }
                return hash;
            }
        }


        static ulong ComputeHalfHash (bool bottomHalf) {
            // Create a half-filled voxel and return its occupancy hash code
            MicroVoxels halfVoxel = new MicroVoxels();
            halfVoxel.Clear();

            if (bottomHalf) {
                for (int index = 0; index < COUNT_PER_VOXEL_HALF; index++) {
                    halfVoxel.SetOccupied(index);
                }
            } else {
                for (int index = COUNT_PER_VOXEL_HALF; index < COUNT_PER_VOXEL; index++) {
                    halfVoxel.SetOccupied(index);
                }
            }
            return halfVoxel.GetOccupancyHashCode();
        }

        int GetIndex (int x, int y, int z) {
            return x + (z * COUNT_PER_AXIS) + (y * COUNT_PER_AXIS * COUNT_PER_AXIS);
        }

        public void Fill () {
            int ulongCount = gridData.Length;
            for (int k = 0; k < ulongCount; k++) {
                gridData[k] = ulong.MaxValue;
            }
            count = COUNT_PER_VOXEL;
            needsMeshDataUpdate = true;
        }

        public void Clear () {
            int ulongCount = gridData.Length;
            for (int k = 0; k < ulongCount; k++) {
                gridData[k] = 0;
            }
            count = 0;
            secondaryType = null;
            layout = MicroVoxelLayout.Default;
            needsMeshDataUpdate = true;
        }

        public bool isEmpty => count == 0;

        public bool isFull => count == COUNT_PER_VOXEL;

        public bool isFullSingleMaterial => count == COUNT_PER_VOXEL && layout != MicroVoxelLayout.Slabs;

        public bool SetOccupied (int x, int y, int z) {
            int index = GetIndex(x, y, z);
            return SetOccupied(index);
        }

        public bool SetOccupied (int microVoxelIndex) {
            int ulongIndex = microVoxelIndex / BITS_PER_ULONG;
            int bitPosition = microVoxelIndex % BITS_PER_ULONG;
            if ((gridData[ulongIndex] & (1UL << bitPosition)) == 0) {
                count++;
                gridData[ulongIndex] |= 1UL << bitPosition;
                needsMeshDataUpdate = true;
                return true;
            }
            return false;
        }

        public bool SetUnoccupied (int x, int y, int z) {
            int index = GetIndex(x, y, z);
            return SetUnoccupied(index);
        }

        public bool SetUnoccupied (int microVoxelIndex) {
            int ulongIndex = microVoxelIndex / BITS_PER_ULONG;
            int bitPosition = microVoxelIndex % BITS_PER_ULONG;
            if ((gridData[ulongIndex] & (1UL << bitPosition)) != 0) {
                count--;
                gridData[ulongIndex] &= ~(1UL << bitPosition);
                needsMeshDataUpdate = true;
                return true;
            }
            return false;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public bool IsOccupied (int x, int y, int z) {
            int index = GetIndex(x, y, z);
            return IsOccupied(index);
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public bool IsOccupied (int microVoxelIndex) {
            int ulongIndex = microVoxelIndex / BITS_PER_ULONG;
            int bitPosition = microVoxelIndex % BITS_PER_ULONG;
            return (gridData[ulongIndex] & (1UL << bitPosition)) != 0;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public byte GetOpaqueProportional () {
            return (byte)Mathf.Max(1, 15 * count / COUNT_PER_VOXEL);
        }

        public int CalculateOccupiedCount () {
            int total = 0;
            foreach (ulong val in gridData) {
                ulong value = val;
                while (value != 0) {
                    total++;
                    value &= value - 1; // Clears the least significant bit set
                }
            }
            return total;
        }

        public void WriteToBinaryWriter (BinaryWriter bw) {
            int gridDataLength = gridData.Length;
            for (int k = 0; k < gridDataLength; k++) {
                bw.Write((UInt64)gridData[k]);
            }
        }

        public void ReadFromBinaryReader (BinaryReader br) {
            int gridDataLength = gridData.Length;
            for (int k = 0; k < gridDataLength; k++) {
                gridData[k] = br.ReadUInt64();
            }
            count = CalculateOccupiedCount();
            needsMeshDataUpdate = true;
        }

        [MethodImpl(256)]
        public bool IsFaceFullyCovered (Cube.Side side) {
            if (count < COUNT_PER_FACE) return false;

            ulong faceHash = GetCachedFaceHash(side);

            switch (side) {
                case Cube.Side.Back: return faceHash == FULL_FACE_HASH_BACK;
                case Cube.Side.Right: return faceHash == FULL_FACE_HASH_RIGHT;
                case Cube.Side.Forward: return faceHash == FULL_FACE_HASH_FORWARD;
                case Cube.Side.Left: return faceHash == FULL_FACE_HASH_LEFT;
                case Cube.Side.Top: return faceHash == FULL_FACE_HASH_TOP;
                case Cube.Side.Bottom: return faceHash == FULL_FACE_HASH_BOTTOM;
                default: return false;
            }
        }

        /// <summary>
        /// Checks if this MicroVoxels instance represents a bottom half filled voxel
        /// </summary>
        /// <returns>True if the microvoxels contains only the bottom half, false otherwise</returns>
        public bool IsBottomHalf () {
            if (count != COUNT_PER_VOXEL_HALF) return false;
            return GetOccupancyHashCode() == BOTTOM_HALF_HASH;
        }

        /// <summary>
        /// Checks if this MicroVoxels instance represents a top half filled voxel
        /// </summary>
        /// <returns>True if the microvoxels contains only the top half, false otherwise</returns>
        public bool IsTopHalf () {
            if (count != COUNT_PER_VOXEL_HALF) return false;
            return GetOccupancyHashCode() == TOP_HALF_HASH;
        }

        /// <summary>
        /// Compare faces with separate rotations for each face
        /// </summary>
        public static bool CompareFaces (MicroVoxels mvA, MicroVoxels mvB, Cube.Side sideA, Cube.Side sideB, int rotationA, int rotationB) {
            Cube.Side effectiveSideA = ApplyRotationToSide(sideA, rotationA);
            Cube.Side effectiveSideB = ApplyRotationToSide(sideB, rotationB);
            return mvA.GetCachedFaceHash(effectiveSideA) == mvB.GetCachedFaceHash(effectiveSideB);
        }

        static Cube.Side ApplyRotationToSide (Cube.Side side, int rotation) {
            if (side == Cube.Side.Top || side == Cube.Side.Bottom) {
                return side;
            }
            int sideIndex = (int)side;
            int rotatedIndex = (sideIndex + rotation) % 4;
            return (Cube.Side)rotatedIndex;
        }


        ulong GetCachedFaceHash (Cube.Side side) {
            // Face hashes are now stored in the prototype and should be available when needed
            if (prototype == null || prototype.faceHashes == null) {
                // This should not happen if the code is used correctly - prototypes should be created before face hash access
                return ComputeFaceHash(side);
            }
            return prototype.faceHashes[(int)side];
        }

    }
}

