using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using NUnit;

namespace VoxelPlay {

    public class WorldEditorToolPaint : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolPaint");
        public override string title => "Paint existing voxels with new voxel definitions";
        public override int priority => 60;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;
        public override bool showRecentVoxels => true;
        public override int minOpaque => 1;

        public override void DrawInspector () {
            env.sceneEditorBrushSize = EditorGUILayout.IntSlider("Brush Size", env.sceneEditorBrushSize, 1, 32);
            env.sceneEditorBrushContinuousMode = EditorGUILayout.Toggle(new GUIContent("Continuous Mode", "Hold left button mouse to operate"), env.sceneEditorBrushContinuousMode);
            if (env.sceneEditorBrushContinuousMode) {
                EditorGUI.indentLevel++;
                env.sceneEditorBrushSpeed = EditorGUILayout.Slider("Speed", env.sceneEditorBrushSpeed, 0, 1);
                EditorGUI.indentLevel--;
            }
            EditorGUI.BeginChangeCheck();
            env.sceneEditorVoxelDefinition = (VoxelDefinition)EditorGUILayout.ObjectField("Voxel Definition", env.sceneEditorVoxelDefinition, typeof(VoxelDefinition), false);
            if (EditorGUI.EndChangeCheck() && env.sceneEditorVoxelDefinition != null) {
                // Add to recent voxels
                if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorVoxelDefinition);
                }
            }
            if (env.enableTinting) {
                env.sceneEditorTintColor = EditorGUILayout.ColorField("Tint Color", env.sceneEditorTintColor);
            }
            env.sceneEditorPaintIgnoreVegetation = EditorGUILayout.Toggle(
                new GUIContent("Ignore Vegetation", "Skip vegetation voxels when painting."),
                env.sceneEditorPaintIgnoreVegetation);
            env.sceneEditorPaintDepth = EditorGUILayout.IntSlider(
                new GUIContent("Paint Depth", "Number of voxels to paint below the surface. 0 = surface only. Stops at empty, non-terrain, or bedrock voxels."),
                env.sceneEditorPaintDepth, 0, 256);
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            List<VoxelIndex> tempVoxels = BufferPool<VoxelIndex>.Get();
            env.GetVoxelIndices(hitInfo.center, brushSize, tempVoxels, mustHaveContent: true);
            int count = tempVoxels.Count;
            Vector3d camPos = GetSceneViewCameraPosition();
            voxelIndices.Clear();
            bool ignoreVegetation = env.sceneEditorPaintIgnoreVegetation;
            for (int k = 0; k < count; k++) {
                VoxelIndex vi = tempVoxels[k];
                if (ignoreVegetation && vi.chunk.voxels[vi.voxelIndex].type != null && vi.chunk.voxels[vi.voxelIndex].type.isVegetation) continue;
                Vector3d pos = env.GetVoxelPosition(vi.chunk, vi.voxelIndex);
                pos += hitInfo.normal * 0.48f;
                Vector3d toCam = (camPos - pos).normalized;
                if (!env.IsSolidAtPosition(pos + toCam)) {
                    voxelIndices.Add(vi);
                }
            }
            BufferPool<VoxelIndex>.Release(tempVoxels);
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            if (env.sceneEditorVoxelDefinition == null || indices.Count == 0) return false;

            env.AddVoxelDefinition(env.sceneEditorVoxelDefinition);
            if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorVoxelDefinition);
            }

            int paintDepth = env.sceneEditorPaintDepth;
            VoxelDefinition bedrockVoxel = null;
            if (paintDepth > 0 && env.world != null) {
                if (env.world.terrainGenerator is TerrainDefaultGenerator tg) {
                    bedrockVoxel = tg.bedrockVoxel;
                } else if (env.world.terrainGenerator is UnityTerrainGenerator utg) {
                    bedrockVoxel = utg.bedrockVoxel;
                }
            }

            ushort targetTypeIndex = (ushort)env.sceneEditorVoxelDefinition.index;
            byte lightIntensity = env.sceneEditorVoxelDefinition.lightIntensity;

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            HashSet<VoxelChunk> savedChunks = new HashSet<VoxelChunk>();
            bool changes = false;
            foreach (var index in indices) {
                if (index.chunk.voxels[index.voxelIndex].type != env.sceneEditorVoxelDefinition) {
                    changes = true;
                    if (savedChunks.Add(index.chunk)) {
                        undoManager.SaveChunk(index.chunk);
                        modifiedChunks.Add(index.chunk);
                    }
                    index.chunk.voxels[index.voxelIndex].typeIndex = targetTypeIndex;   // preserves microvoxels
                    if (lightIntensity > 0) {
                        index.chunk.AddLightSource(index.voxelIndex, lightIntensity);
                        env.SetTorchLightmap(index.chunk, index.voxelIndex, lightIntensity);
                    }
                }

                // Paint depth: walk downward from each surface voxel
                if (paintDepth > 0) {
                    Vector3d belowPos = env.GetVoxelPosition(index.chunk, index.voxelIndex);
                    for (int d = 0; d < paintDepth; d++) {
                        belowPos.y--;
                        if (!env.GetVoxelIndex(belowPos, out VoxelChunk belowChunk, out int belowIndex, createChunkIfNotExists: false)) break;
                        Voxel v = belowChunk.voxels[belowIndex];
                        if (v.isEmpty) break;
                        if (terrainVoxelDefinitions != null && !terrainVoxelDefinitions.Contains(v.typeIndex)) break;
                        if (bedrockVoxel != null && v.type == bedrockVoxel) break;
                        if (v.typeIndex != targetTypeIndex) {
                            changes = true;
                            if (savedChunks.Add(belowChunk)) {
                                undoManager.SaveChunk(belowChunk);
                                modifiedChunks.Add(belowChunk);
                            }
                            belowChunk.voxels[belowIndex].typeIndex = targetTypeIndex;  // preserves microvoxels
                            if (lightIntensity > 0) {
                                belowChunk.AddLightSource(belowIndex, lightIntensity);
                                env.SetTorchLightmap(belowChunk, belowIndex, lightIntensity);
                            }
                        }
                    }
                }
            }

            RefreshModifiedChunks(modifiedChunks);
            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return changes;
        }

    }

}