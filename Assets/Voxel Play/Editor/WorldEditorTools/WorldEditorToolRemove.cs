using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolRemove : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolRemove");
        public override string title => "Remove/destroy voxels";
        public override string instructions => "Hold shift to remove same voxels. Hold alt to remove different voxels.";
        public override int priority => 52;
        public override int minOpaque => 0;
        public override bool supportsMicroVoxels => true;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;

        public override void DrawInspector () {
            env.sceneEditorBrushMicroVoxelSize = EditorGUILayout.IntSlider("MicroVoxel Size", env.sceneEditorBrushMicroVoxelSize, 0, MicroVoxels.COUNT_PER_AXIS);
            if (env.sceneEditorBrushMicroVoxelSize == 0) {
                env.sceneEditorBrushSize = EditorGUILayout.IntSlider("Brush Size", env.sceneEditorBrushSize, 1, 32);
            }
            env.sceneEditorBrushContinuousMode = EditorGUILayout.Toggle(new GUIContent("Continuous Mode", "Hold left button mouse to operate"), env.sceneEditorBrushContinuousMode);
            if (env.sceneEditorBrushContinuousMode) {
                EditorGUI.indentLevel++;
                env.sceneEditorBrushSpeed = EditorGUILayout.Slider("Speed", env.sceneEditorBrushSpeed, 0, 1);
                EditorGUI.indentLevel--;
            }
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {

            voxelIndices.Clear();

            if (env.sceneEditorBrushMicroVoxelSize > 0) return;

            List<VoxelIndex> tempVoxels = BufferPool<VoxelIndex>.Get();
            env.GetVoxelIndices(hitInfo.center, brushSize, tempVoxels);
            int count = tempVoxels.Count;
            Vector3d camPos = GetSceneViewCameraPosition();
            for (int k = 0; k < count; k++) {
                VoxelIndex vi = tempVoxels[k];
                Vector3d pos = env.GetVoxelPosition(vi.chunk, vi.voxelIndex);
                pos += hitInfo.normal * 0.48f;
                Vector3d toCam = (camPos - pos).normalized;
                if (!env.IsSolidAtPosition(pos + toCam)) {
                    voxelIndices.Add(vi);
                }
            }
            BufferPool<VoxelIndex>.Release(tempVoxels);
        }

        public override void HighlightVoxels (ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {
            if (env.sceneEditorBrushMicroVoxelSize == 0 || voxelIndices.Count > 1) {
                base.HighlightVoxels(ref hitInfo, voxelIndices, color, edgeWidth, fadeAmplitude);
                return;
            }

            env.VoxelHighlight(ref hitInfo, color, edgeWidth, env.sceneEditorBrushMicroVoxelSize, fadeAmplitude);
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            int centerVoxelType = env.GetVoxel(hitInfo.center, createChunkIfNotExists: false).typeIndex;

            if (env.sceneEditorBrushMicroVoxelSize > 0) {
                VoxelDefinition vd = env.voxelDefinitions[centerVoxelType];
                undoManager.SaveChunk(hitInfo.chunk);
                if (vd.supportsMicroVoxels) {
                    if (env.MicroVoxelDestroy(ref hitInfo, env.sceneEditorBrushMicroVoxelSize)) {
                        modifiedChunks.Add(hitInfo.chunk);
                    }
                } else {
                    hitInfo.chunk.ClearVoxel(hitInfo.voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                    modifiedChunks.Add(hitInfo.chunk);
                }
            } else {
                int count = indices.Count;
                for (int k = 0; k < count; k++) {
                    VoxelIndex vi = indices[k];
                    int voxelType = vi.chunk.voxels[vi.voxelIndex].typeIndex;
                    bool shouldRemove = true;
                    
                    if (shift) {
                        // Shift: only remove same voxels
                        shouldRemove = voxelType == centerVoxelType;
                    } else if (alt) {
                        // Alt: only remove different voxels
                        shouldRemove = voxelType != centerVoxelType;
                    }
                    // If neither shift nor alt: remove all voxels (shouldRemove remains true)
                    
                    if (shouldRemove) {
                        UpdateChunkElevation(vi.chunk);
                        undoManager.SaveChunk(vi.chunk);
                        vi.chunk.ClearVoxel(vi.voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                        Vector3d pos = env.GetVoxelPosition(vi.chunk, vi.voxelIndex);
                        int z = (vi.voxelIndex / VoxelPlayEnvironment.CHUNK_SIZE) & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
                        int x = vi.voxelIndex & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
                        int elevationIndex = z * VoxelPlayEnvironment.CHUNK_SIZE + x;
                        if (pos.y < vi.chunk.terrainInfo[elevationIndex].groundLevel) {
                            vi.chunk.terrainInfo[elevationIndex].height = (float)pos.y;
                        }
                        modifiedChunks.Add(vi.chunk);
                    }
                }
            }

            int modifiedCount = modifiedChunks.Count;
            RefreshModifiedChunks(modifiedChunks);

            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return modifiedCount > 0;

        }

    }

}