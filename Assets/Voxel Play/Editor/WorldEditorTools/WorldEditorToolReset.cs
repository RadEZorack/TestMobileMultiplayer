using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolReset : WorldEditorToolElevateTerrain {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolReset");
        public override string title => "Reset chunk";
        public override string instructions => "Reset chunks contents to default.";
        public override int priority => 4;
        public override int minOpaque => 0;
        public override bool supportsMicroVoxels => false;
        public override bool supportsContinuousMode => false;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.TerrainTool;
        
        public override void DrawInspector () {
        }

        public override void DrawGizmos (VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {

            VoxelIndex vi = new VoxelIndex();
            voxelIndices.Clear();

            brushSize = 1; // reset a single chunk for now

            Vector3d minCorner = hitInfo.center;
            minCorner.x -= brushSize - 1;
            minCorner.z -= brushSize - 1;
            Vector3d maxCorner = hitInfo.center;
            maxCorner.x += brushSize - 1;
            maxCorner.z += brushSize - 1;
            VoxelChunk chunkMin = env.GetChunk(minCorner);
            VoxelChunk chunkMax = env.GetChunk(maxCorner);
            Boundsd boundsChunkMin = env.GetChunkBounds(chunkMin);
            minCorner = boundsChunkMin.min;
            Boundsd boundsChunkMax = env.GetChunkBounds(chunkMax);
            Boundsd bounds = boundsChunkMin;
            bounds.Encapsulate(boundsChunkMax);

            int size = (int)bounds.size.x;
            int count = size * size;

            for (int k = 0; k < count; k++) {
                int pz = k / size;
                int px = k % size;

                Vector3d pos = minCorner;
                pos.z += pz;
                pos.x += px;
                pos.y = env.GetHeight(pos, allowedVoxelDefinitions: terrainVoxelDefinitions) - 0.1;

                if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) {
                    if (chunk.voxels[voxelIndex].type.isSolid) {
                        vi.chunk = chunk;
                        vi.voxelIndex = voxelIndex;
                        voxelIndices.Add(vi);
                    }
                }
            }
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            int count = indices.Count;
            VoxelChunk lastChunk = null;

            // fix terrain height and biome
            for (int k = 0; k < count; k++) {

                VoxelIndex vi = indices[k];

                if (vi.chunk == lastChunk) continue;
                lastChunk = vi.chunk;

                if (modifiedChunks.Contains(vi.chunk)) continue;
                modifiedChunks.Add(vi.chunk);

                // Get the first entry in the undoManager.undoStack
                foreach (UndoSession undoSession in UndoManager.undoStack) {
                    if (undoSession.chunks.TryGetValue(vi.chunk, out UndoChunkData undoChunkData)) {
                        undoManager.RestoreChunk(vi.chunk, undoChunkData);
                        break;
                    }
                }

                undoManager.SaveChunk(vi.chunk);

            }

            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return modifiedChunks.Count > 0;

        }

    }

}