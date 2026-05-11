using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public class WorldEditorToolSmoothTerrain : WorldEditorToolElevateTerrain {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolSmooth");
        public override string title => "Smooth terrain";
        public override string instructions => "Smooth terrain altitude differences.";
        public override int priority => 3;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.TerrainTool;

        public override void DrawGizmos (VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            Vector3d center = hitInfo.center;
            int size = brushSize * 2;
            int count = size * size;
            Vector3d corner = center;
            corner.x -= brushSize;
            corner.z -= brushSize;

            // compute average elevation
            float averageElevation = 0;
            float samples = 0;
            for (int k = 0; k < count; k++) {
                int pz = k / size;
                int px = k % size;

                Vector3d pos = corner;
                pos.z += pz;
                pos.x += px;
                float h = env.GetHeight(pos, allowedVoxelDefinitions: terrainVoxelDefinitions);
                pos.y = h - 0.1;

                if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) continue;

                UpdateChunkElevation(chunk);

                int z = (voxelIndex / VoxelPlayEnvironment.CHUNK_SIZE) & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
                int x = voxelIndex & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;

                int elevationIndex = z * VoxelPlayEnvironment.CHUNK_SIZE + x;

                averageElevation += chunk.terrainInfo[elevationIndex].height;
                samples++;
            }
            if (samples == 0) return false;
            averageElevation /= samples;

            // smooth terrain
            for (int k = 0; k < count; k++) {
                int pz = k / size;
                int px = k % size;

                float mask = ComputeMaskFactor(pz, px, size) - ROUNDNESS;
                if (mask <= 0) continue;

                Vector3d pos = corner;
                pos.z += pz;
                pos.x += px;
                pos.y = env.GetHeight(pos, allowedVoxelDefinitions: terrainVoxelDefinitions) - 0.1f;

                if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) continue;

                UpdateChunkElevation(chunk);

                int z = (voxelIndex / VoxelPlayEnvironment.CHUNK_SIZE) & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
                int x = voxelIndex & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;

                int elevationIndex = z * VoxelPlayEnvironment.CHUNK_SIZE + x;

                pos.y = chunk.terrainInfo[elevationIndex].groundLevel;

                float factor = averageElevation - chunk.terrainInfo[elevationIndex].height;
                if (count > 1) {
                    factor *= mask * brushStrength;
                }

                BiomeDefinition biome = chunk.terrainInfo[elevationIndex].biome;
                biome = GetBiomeForColumn(pos, chunk, elevationIndex, biome);
                VoxelChunk surfaceChunk = null;
                int surfaceIndex = -1;
                bool canPlaceHalfSurface = enableHalfStepSurface;

                if (factor < 0) { // lower terrain
                    undoManager.SaveChunk(chunk);
                    chunk.terrainInfo[elevationIndex].height += factor;
                    if (chunk.terrainInfo[elevationIndex].height < averageElevation) {
                        chunk.terrainInfo[elevationIndex].height = averageElevation;
                    }

                    int ny = chunk.terrainInfo[elevationIndex].groundLevel;

                    pos.y++;
                    if (env.GetVoxelIndex(pos, out VoxelChunk aboveChunk, out int aboveIndex, createChunkIfNotExists: false)) {
                        if (aboveChunk.voxels[aboveIndex].type.isVegetation) {
                            undoManager.SaveChunk(aboveChunk);
                            aboveChunk.ClearVoxel(aboveIndex, VoxelPlayEnvironment.FULL_LIGHT);
                        }
                    }

                    Vector3d bottomPos = pos;
                    bottomPos.y = ny;
                    if (!env.GetVoxelIndex(bottomPos, out VoxelChunk bottomChunk, out int bottomIndex, createChunkIfNotExists: true)) continue;

                    chunk.ClearVoxel(voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                    if ((object)biome != null) {
                        undoManager.SaveChunk(bottomChunk);
                        bottomChunk.SetVoxel(bottomIndex, biome.voxelTop);
                        // vegetation-aware and half-surface handling
                        canPlaceHalfSurface = canPlaceHalfSurface && !PlaceVegetationAbove(bottomPos, bottomChunk, bottomIndex, biome, modifiedChunks);
                        surfaceChunk = bottomChunk;
                        surfaceIndex = bottomIndex;
                    }

                } else { // raise terrain
                    undoManager.SaveChunk(chunk);
                    chunk.terrainInfo[elevationIndex].height += factor;
                    if (clampAltitude && chunk.terrainInfo[elevationIndex].height > env.sceneEditorAltitude) {
                        chunk.terrainInfo[elevationIndex].height = env.sceneEditorAltitude;
                    }

                    int ny = chunk.terrainInfo[elevationIndex].groundLevel;

                    // only applies to voxels which have a non opaque voxel on top
                    Vector3d abovePos = pos;
                    abovePos.y = ny;
                    if (!env.GetVoxelIndex(abovePos, out VoxelChunk aboveChunk, out int aboveIndex, createChunkIfNotExists: true)) continue;
                    if (aboveChunk.voxels[aboveIndex].opaque >= VoxelPlayEnvironment.FULL_OPAQUE) continue;

                    if ((object)biome != null) {
                        undoManager.SaveChunk(aboveChunk);

                        chunk.SetVoxel(voxelIndex, biome.voxelDirt);
                        aboveChunk.SetVoxel(aboveIndex, biome.voxelTop);

                        // Check if vegetation is placed - if so, don't place half-surface
                        canPlaceHalfSurface = canPlaceHalfSurface && !PlaceVegetationAbove(abovePos, aboveChunk, aboveIndex, biome, modifiedChunks);
                        surfaceChunk = aboveChunk;
                        surfaceIndex = aboveIndex;
                    } else {
                        // simply repeats voxel
                        undoManager.SaveChunk(aboveChunk);
                        aboveChunk.SetVoxel(aboveIndex, chunk.voxels[voxelIndex].type);
                    }
                }
                // Apply half-voxel surface (shared for both branches)
                if (canPlaceHalfSurface && surfaceChunk != null && surfaceIndex >= 0) {
                    float groundLevel = chunk.terrainInfo[elevationIndex].groundLevel;
                    float frac = chunk.terrainInfo[elevationIndex].height - groundLevel;
                    if (frac < 0.5f) {
                        surfaceChunk.SetMicroVoxels(surfaceIndex, MicroVoxels.halfSurfaceVoxelTemplate);
                    }
                }
                modifiedChunks.Add(chunk);
            }

            int modifiedCount = modifiedChunks.Count;
            RefreshModifiedChunks(modifiedChunks);

            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return modifiedCount > 0;

        }

    }

}