using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace VoxelPlay {

    public class WorldEditorToolRiver : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolRiver");
        public override string title => "Paint a river";
        public override int priority => 70;
        public override int minOpaque => 1;
        public override bool supportsContinuousMode => true;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.TerrainTool;
        public override bool showRecentVoxels => true;

        public WorldEditorToolRiver () : base() {
            if (env == null) return;
            if (env.sceneEditorRiverWaterVoxel == null) {
                env.sceneEditorRiverWaterVoxel = env.currentWaterVoxelDefinition;
            }
            if (env.sceneEditorRiverShoreVoxel == null && env.world != null && env.world.terrainGenerator != null && env.world.terrainGenerator is TerrainDefaultGenerator) {
                TerrainDefaultGenerator tg = (TerrainDefaultGenerator)env.world.terrainGenerator;
                env.sceneEditorRiverShoreVoxel = tg.shoreVoxel;
            }
        }

        public override void DrawInspector () {
            env.sceneEditorBrushSize = EditorGUILayout.IntSlider("Brush Size", env.sceneEditorBrushSize, 1, 16);
            env.sceneEditorBrushContinuousMode = EditorGUILayout.Toggle(new GUIContent("Continuous Mode", "Hold left button mouse to operate"), env.sceneEditorBrushContinuousMode);
            if (env.sceneEditorBrushContinuousMode) {
                EditorGUI.indentLevel++;
                env.sceneEditorBrushSpeed = EditorGUILayout.Slider("Speed", env.sceneEditorBrushSpeed, 0, 1);
                EditorGUI.indentLevel--;
            }
            env.sceneEditorRiverDepth = EditorGUILayout.IntSlider("River Depth", env.sceneEditorRiverDepth, 1, 10);
            EditorGUI.BeginChangeCheck();
            env.sceneEditorRiverWaterVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Water Voxel", env.sceneEditorRiverWaterVoxel, typeof(VoxelDefinition), false);
            if (EditorGUI.EndChangeCheck() && env.sceneEditorRiverWaterVoxel != null) {
                // Add to recent voxels
                if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorRiverWaterVoxel);
                }
            }
            env.sceneEditorRiverAddShore = EditorGUILayout.Toggle("Add Shore", env.sceneEditorRiverAddShore);
            if (env.sceneEditorRiverAddShore) {
                EditorGUI.BeginChangeCheck();
                env.sceneEditorRiverShoreVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Shore Voxel", env.sceneEditorRiverShoreVoxel, typeof(VoxelDefinition), false);
                if (EditorGUI.EndChangeCheck() && env.sceneEditorRiverShoreVoxel != null) {
                    // Add to recent voxels
                    if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                        VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorRiverShoreVoxel);
                    }
                }
            }
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {

            VoxelDefinition shoreVoxel = env.sceneEditorRiverAddShore ? env.sceneEditorRiverShoreVoxel : env.sceneEditorRiverWaterVoxel;
            bool usesShore = shoreVoxel != null;

            voxelIndices.Clear();
            if (env.currentWaterVoxelDefinition == null) {
                return;
            }

            Vector3d center = hitInfo.center;

            List<VoxelIndex> tempVoxels = BufferPool<VoxelIndex>.Get();
            env.GetVoxelIndices(center, brushSize, env.sceneEditorRiverDepth, brushSize, tempVoxels, VoxelPlayEnvironment.FULL_OPAQUE);

            // dirty hack: we use the damageTaken field to temporarily store the type of voxel to be placed
            int count = tempVoxels.Count;
            for (int k = 0; k < count; k++) {
                VoxelIndex vi = tempVoxels[k];
                Vector3d pos = env.GetVoxelPosition(vi.chunk, vi.voxelIndex);
                if (pos.y < center.y - env.sceneEditorRiverDepth) continue;
                vi.damageTaken = env.currentWaterVoxelDefinition.index;
                if (pos.y >= center.y) {
                    double dz = center.z - pos.z;
                    double dx = center.x - pos.x;
                    if (usesShore && brushSize > 0 && dz * dz + dx * dx > (brushSize - 1) * (brushSize - 1)) {
                        vi.damageTaken = shoreVoxel.index;
                    }
                }
                voxelIndices.Add(vi);
            }
            BufferPool<VoxelIndex>.Release(tempVoxels);
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            int count = indices.Count;

            const int HALF_WATER_LEVEL = 8; // 0..15 range

            for (int k = 0; k < count; k++) {
                VoxelIndex vi = indices[k];
                VoxelDefinition vd = env.GetVoxelDefinition(vi.damageTaken);
                // Detect if there are microvoxels in the target voxel BEFORE changing it
                bool hadMicroVoxels = env.IsMicroVoxelAtPosition(vi.chunk, vi.voxelIndex);
                undoManager.SaveChunk(vi.chunk);
                vi.chunk.SetVoxel(vi.voxelIndex, vd);
                // If placing water on a voxel that previously had microvoxels, set water height to half
                if ((vd != null && (vd.isLiquid || vd.renderType == RenderType.Water)) && hadMicroVoxels) {
                    vi.chunk.voxels[vi.voxelIndex].SetWaterLevel(HALF_WATER_LEVEL);
                }
                // If placing a shore/solid voxel over a position that had microvoxels, preserve half-block using microvoxels
                if ((vd != null && !(vd.isLiquid || vd.renderType == RenderType.Water)) && hadMicroVoxels) {
                    vi.chunk.SetMicroVoxels(vi.voxelIndex, MicroVoxels.halfSurfaceVoxelTemplate);
                }
                modifiedChunks.Add(vi.chunk);
                // clear any plant above
                Vector3d pos = env.GetVoxelPosition(vi);
                pos.y++;
                if (env.GetVoxelIndex(pos, out VoxelChunk aboveChunk, out int aboveIndex)) {
                    if (aboveChunk.voxels[aboveIndex].type.isVegetation) {
                        undoManager.SaveChunk(aboveChunk);
                        aboveChunk.ClearVoxel(aboveIndex, VoxelPlayEnvironment.FULL_LIGHT);
                        modifiedChunks.Add(aboveChunk);
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