using System.Collections.Generic;

namespace VoxelPlay {

    // Fallback terrain generator for empty worlds
    public class NullTerrainGenerator : VoxelPlayTerrainGenerator {

        public override void GetTerrainVoxelDefinitions (List<VoxelDefinition> voxelDefinitions) {
            return;
        }

        public override bool PaintChunk (VoxelChunk chunk) {
            return false;
        }

    }

}