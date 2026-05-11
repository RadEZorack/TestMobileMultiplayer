using UnityEngine;


namespace VoxelPlay.GPURendering.Instancing {

    class BatchedMesh {
        public VoxelDefinition voxelDefinition;
        public Material[] materials;
        public FastList<Batch> batches;
        public Batch lastBatch;

        public BatchedMesh(VoxelDefinition voxelDefinition) {
            this.voxelDefinition = voxelDefinition;
            batches = new FastList<Batch>();
            lastBatch = null;
        }
    }
}
