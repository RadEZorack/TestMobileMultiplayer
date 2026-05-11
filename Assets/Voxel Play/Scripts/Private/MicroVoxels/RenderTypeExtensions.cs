
using System.Runtime.CompilerServices;

namespace VoxelPlay {
    public static partial class RenderTypeExtensions {

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsMicroVoxels(this RenderType o) {
            return o == RenderType.Opaque || o == RenderType.Opaque6tex || o == RenderType.OpaqueAnimated;
        }



    }
}