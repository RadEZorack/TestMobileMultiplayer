using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace VoxelPlay {

    public class WorldEditorToolPlant : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolPlant");
        public override string title => "Spawn or remove biome plants on terrain";
        public override string instructions => "Hold shift to remove vegetation.";
        public override int priority => 80;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.TerrainTool;

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushRadius, List<VoxelIndex> voxelIndices) {

            List<VoxelIndex> tempVoxels = BufferPool<VoxelIndex>.Get();
            if (tempVoxels == null) return;

            try {

                Vector3d center = hitInfo.center;
                env.GetVoxelIndices(center, brushRadius, tempVoxels, VoxelPlayEnvironment.FULL_OPAQUE);

                int size = 1 + (brushRadius - 1) * 2;

                Vector3d camPos = GetSceneViewCameraPosition();

                voxelIndices.Clear();
                int count = tempVoxels.Count;

                for (int k = 0; k < count; k++) {
                    VoxelIndex vi = tempVoxels[k];

                    Vector3d pos = env.GetVoxelPosition(vi);

                    int pz = Mathf.FloorToInt((float)(pos.z - center.z + size / 2));
                    int px = Mathf.FloorToInt((float)(pos.x - center.x + size / 2));

                    float mask = ComputeMaskFactor(pz, px, size) - ROUNDNESS;
                    if (mask <= 0) continue;

                    // planting over a terrain voxel?
                    if (!terrainVoxelDefinitions.Contains(vi.chunk.voxels[vi.voxelIndex].typeIndex)) continue;

                    // avoid planting if the surface voxel contains microvoxels
                    if (vi.chunk.microVoxels != null && vi.chunk.microVoxels.ContainsKey(vi.voxelIndex)) continue;

                    // ensure it's no above
                    Vector3d abovePos = pos;
                    abovePos.y++;
                    if (!env.GetVoxelIndex(abovePos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) continue;
                    if (!chunk.voxels[voxelIndex].isEmpty && !chunk.voxels[voxelIndex].type.isVegetation) continue;

                    vi.sqrDistance = mask * env.sceneEditorBrushStrength;
                    voxelIndices.Add(vi);
                }
            }
            finally {
                BufferPool<VoxelIndex>.Release(tempVoxels);
            }
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            int modifiedCount = 0;

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            try {
                int count = indices.Count;

                for (int k = 0; k < count; k++) {

                    VoxelIndex vi = indices[k];

                    float mask = vi.sqrDistance;

                    float rn = WorldRand.GetValue();
                    if (rn > mask) continue;

                    Vector3 abovePos = env.GetVoxelPosition(vi);
                    abovePos.y++;

                    if (shift) {

                        if (!env.GetVoxelIndex(abovePos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) continue;

                        if (chunk.voxels[voxelIndex].type.isVegetation) {
                            undoManager.SaveChunk(chunk);
                            chunk.ClearVoxel(voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                            modifiedChunks.Add(chunk);

                        }
                    } else { // add vegatation

                        if (!env.GetVoxelIndex(abovePos, out VoxelChunk aboveChunk, out int aboveIndex, createChunkIfNotExists: true)) continue;

                        if (!aboveChunk.voxels[aboveIndex].isEmpty) continue;

                        BiomeDefinition biome = env.GetBiome(abovePos);
                        if ((object)biome != null) {
                            if (biome.vegetation.Length > 0) {
                                rn = WorldRand.GetValue();
                                undoManager.SaveChunk(aboveChunk);
                                aboveChunk.SetVoxel(aboveIndex, env.GetVegetation(biome.vegetation, rn));
                                modifiedChunks.Add(aboveChunk);
                            }
                        }
                    }
                }

                modifiedCount = modifiedChunks.Count;
                RefreshModifiedChunks(modifiedChunks);
            }
            finally {
                BufferPool<VoxelChunk>.Release(modifiedChunks);
            }

            return modifiedCount > 0;

        }

    }

}