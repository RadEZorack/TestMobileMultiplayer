using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace VoxelPlay {

    public class WorldEditorToolTree : WorldEditorToolPlant {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolTree");
        public override string instructions => "Spawn trees on terrain. Hold shift to remove trees.";
        public override int priority => 90;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.TerrainTool;
        public override void DrawInspector () {
            env.sceneEditorBrushSize = EditorGUILayout.IntSlider("Brush Size", env.sceneEditorBrushSize, 1, 32);
            EditorGUI.BeginChangeCheck();
            env.sceneEditorBrushShape = (Texture2D)EditorGUILayout.ObjectField("Brush Shape", env.sceneEditorBrushShape, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck()) {
                TextureTools.EnsureTextureReadable(env.sceneEditorBrushShape);
            }
            env.sceneEditorBrushStrength = EditorGUILayout.Slider("Tree Density", env.sceneEditorBrushStrength, 0.001f, 1f);
        }


        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {

            List<VoxelIndex> tempVoxels = BufferPool<VoxelIndex>.Get();
            if (tempVoxels == null) return;
            voxelIndices.Clear();
            if (env.sceneEditorBrushMicroVoxelSize > 0) return;

            try {
                if (shift) {
                    env.GetVoxelIndices(hitInfo.center, brushSize, tempVoxels);
                    int count = tempVoxels.Count;
                    for (int k = 0; k < count; k++) {
                        VoxelIndex vi = tempVoxels[k];
                        Voxel voxel = vi.chunk.voxels[vi.voxelIndex];
                        if (voxel.type.isTree) {
                            voxelIndices.Add(vi);
                        }
                    }
                } else {

                    Vector3d center = hitInfo.center;
                    env.GetVoxelIndices(center, brushSize, tempVoxels, VoxelPlayEnvironment.FULL_OPAQUE);

                    int size = 1 + (brushSize - 1) * 2;

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

                        pos.y += 0.45;
                        Vector3d toCam = (camPos - pos).normalized;
                        // voxel visible?
                        if (!env.IsSolidAtPosition(pos + toCam)) {
                            // ensure it's not solid on top
                            Vector3d abovePos = pos;
                            abovePos.y++;
                            if (env.GetVoxelIndex(abovePos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) {
                                if (!chunk.voxels[voxelIndex].type.isSolid) {
                                    vi.sqrDistance = mask * env.sceneEditorBrushStrength;
                                    voxelIndices.Add(vi);
                                }
                            }
                        }
                    }
                }
            }
            finally {
                BufferPool<VoxelIndex>.Release(tempVoxels);
            }
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            int modifiedCount = 0;

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            int treeCount = Mathf.CeilToInt(brushSize * brushStrength);

            try {
                int count = indices.Count;

                for (int t = 0; t < count; t++) {

                    int k = Random.Range(0, count);

                    VoxelIndex vi = indices[k];

                    Vector3d pos = env.GetVoxelPosition(vi);

                    if (shift) {
                        if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) continue;

                        if (chunk.voxels[voxelIndex].type.isTree) {
                            undoManager.SaveChunk(chunk);
                            chunk.ClearVoxel(voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                            modifiedChunks.Add(chunk);
                        }
                    } else {
                        float mask = vi.sqrDistance;
                        float rn = WorldRand.GetValue();
                        if (rn > mask) continue;

                        if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) continue;

                        BiomeDefinition biome = env.GetBiome(pos);
                        if (biome != null && biome.trees.Length > 0) {
                            VoxelDefinition vd = chunk.voxels[voxelIndex].type;
                            if (!terrainVoxelDefinitions.Contains(vd.index)) continue;

                            rn = Random.value;
                            env.TreePlace(pos, env.GetTree(biome.trees, rn), modifiedChunks, canPlantOnModifiedChunks: true, previewMode: true);
                            foreach (var modifiedChunk in modifiedChunks) {
                                undoManager.SaveChunk(modifiedChunk);
                            }
                            env.TreePlace(pos, env.GetTree(biome.trees, rn), modifiedChunks, canPlantOnModifiedChunks: true, previewMode: false);
                            treeCount--;
                            if (treeCount <= 0) break;
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