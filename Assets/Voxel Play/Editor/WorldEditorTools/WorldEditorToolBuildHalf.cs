using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.Rendering;

namespace VoxelPlay {

    public class WorldEditorToolBuildHalf : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolBuildHalf");
        public override string title => "Add half voxels";
        public override string instructions => "Shift: remove. Control: pick voxel type. Alt: toggle slab altitude. R: rotates.";
        public override int priority => 51;
        public override int minOpaque => 5;
        public override bool supportsMicroVoxels => true;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;
        public override bool canIgnoreWater => true;
        public override bool showRecentVoxels => true;

        static class ShaderParams {
            public static int MicroVoxelsSize = Shader.PropertyToID("_MicroVoxelsSize");
        }

        bool IsWithinDistance (Vector3d pos, float maxDistance) {
            Vector3d startCenter = startHitInfo.voxelCenter;
            Vector3d diff = pos - startCenter;
            diff.x *= startHitInfo.normal.x;
            diff.y *= startHitInfo.normal.y;
            diff.z *= startHitInfo.normal.z;
            double dist = Math.Abs(diff.x) + Math.Abs(diff.y) + Math.Abs(diff.z);
            return dist < maxDistance;
        }

        void ComputeSlabPlacement (ref VoxelHitInfo hitInfo, Vector3d baseCenter, out Vector3d targetPos, out bool isTopHalf) {
            Vector3d center = baseCenter;
            FastVector.Middling(ref center);

            isTopHalf = alt;
            float halfHeight = MicroVoxels.COUNT_PER_AXIS_HALF * MicroVoxels.SIZE; // typically 0.5

            if (shift) {
                // Removal preview: stay inside the current voxel at the chosen half
                center.y += isTopHalf ? halfHeight * 0.5f : -halfHeight * 0.5f;
                targetPos = center;
                return;
            }

            // Placement preview: follow existing slab placement logic against face/microvoxels
            center.y -= 0.499f;
            MicroVoxels mv = env.GetMicroVoxels(center);
            if (mv != null) {
                if (mv.IsOccupied(0, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE, 0)) {
                    center.y += hitInfo.normal.y;
                    isTopHalf = hitInfo.normal.y < 0.0001f;
                } else {
                    isTopHalf = hitInfo.normal.y > 0.0001f;
                }
                center.x += hitInfo.normal.x;
                center.z += hitInfo.normal.z;
            } else {
                center += hitInfo.normal;
            }

            if (isTopHalf) {
                center.y += 0.5f;
            }
            center.y += halfHeight * 0.5f;

            targetPos = center;
        }


        public override void DrawInspector () {
            EditorGUI.BeginChangeCheck();
            env.sceneEditorBuildVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Voxel Definition", env.sceneEditorBuildVoxel, typeof(VoxelDefinition), false);
            if (EditorGUI.EndChangeCheck()) {
                if (env.sceneEditorBuildVoxel != null && env.sceneEditorBuildVoxel.usesMicroVoxels) {
                    env.sceneEditorBrushMicroVoxelSize = 0;
                }
                // Add to recent voxels
                if (env.sceneEditorBuildVoxel != null && VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorBuildVoxel);
                }
            }
            if (env.sceneEditorBrushMicroVoxelSize == 0) {
                env.sceneEditorBrushMicroVoxelSize = 1;
            }
            // Brush size & shape controls
            env.sceneEditorBrushSize = EditorGUILayout.IntSlider("Brush Size", env.sceneEditorBrushSize, 1, 32);
            EditorGUI.BeginChangeCheck();
            env.sceneEditorBrushShape = (Texture2D)EditorGUILayout.ObjectField("Brush Shape", env.sceneEditorBrushShape, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck()) {
                TextureTools.EnsureTextureReadable(env.sceneEditorBrushShape);
                SetMask(env.sceneEditorBrushShape);
            }
            EditorGUILayout.BeginHorizontal();
            env.sceneEditorPlacementRotation = EditorGUILayout.IntSlider("Rotation", env.sceneEditorPlacementRotation, 0, 3);
            GUILayout.Label(env.sceneEditorPlacementRotation * 90 + "°");
            EditorGUILayout.EndHorizontal();
            env.sceneEditorBuildIgnoreWater = EditorGUILayout.Toggle("Ignore Water", env.sceneEditorBuildIgnoreWater);
            env.sceneEditorBrushContinuousMode = EditorGUILayout.Toggle(new GUIContent("Continuous Mode", "Hold left button mouse to operate"), env.sceneEditorBrushContinuousMode);
            if (env.sceneEditorBrushContinuousMode) {
                EditorGUI.indentLevel++;
                env.sceneEditorBrushSpeed = EditorGUILayout.Slider("Speed", env.sceneEditorBrushSpeed, 0, 1);
                env.sceneEditorBuildMaxLength = EditorGUILayout.IntSlider("Max Length", env.sceneEditorBuildMaxLength, 0, 20);
                EditorGUI.indentLevel--;
            }

            // Draw the recent voxels grid
            DrawRecentVoxelsGrid();
        }

        public override void Update () {
            if (keyR) {
                env.sceneEditorPlacementRotation = (env.sceneEditorPlacementRotation + 1) % 4;
            }
        }

        public override void DrawGizmos (VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
            const float SIZE = 1.01f;
            var originalZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            Color highlightColor = Color.cyan;
            highlightColor.a = 0.5f;
            Handles.color = highlightColor;

            if (voxelIndices != null && voxelIndices.Count > 0) {
                int count = voxelIndices.Count;
                for (int k = 0; k < count; k++) {
                    VoxelIndex vi = voxelIndices[k];
                    Vector3d baseCenter = env.GetVoxelPosition(vi.chunk, vi.voxelIndex);
                    ComputeSlabPlacement(ref hitInfo, baseCenter, out Vector3d center, out bool topHalf);
                    float sizeV = MicroVoxels.COUNT_PER_AXIS_HALF * MicroVoxels.SIZE;
                    Handles.DrawWireCube(center, new Vector3(SIZE, sizeV, SIZE));
                }
            } else {
                Vector3d baseCenter = hitInfo.voxelCenter;
                ComputeSlabPlacement(ref hitInfo, baseCenter, out Vector3d center, out _);
                float sizeV = MicroVoxels.COUNT_PER_AXIS_HALF * MicroVoxels.SIZE;
                Handles.DrawWireCube(center, new Vector3(SIZE, sizeV, SIZE));
            }

            Handles.zTest = originalZTest;
        }


        public override bool RayCast (Ray ray, out VoxelHitInfo hitInfo) {
            bool res = base.RayCast(ray, out hitInfo);
            return res;
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            voxelIndices.Clear();

            Vector3d center = hitInfo.voxelCenter;

            int size = brushSize * 2 - 1;
            int count = size * size;
            Vector3d corner = center;
            corner.x -= brushSize - 1;
            corner.z -= brushSize - 1;

            for (int k = 0; k < count; k++) {
                int pz = k / size;
                int px = k % size;

                if (count > 1) {
                    float mask = ComputeMaskFactor(pz, px, size) - ROUNDNESS;
                    if (mask <= 0) continue;
                }

                Vector3d pos = corner;
                pos.z += pz;
                pos.x += px;

                if (isMouseDown) {
                    if (!IsWithinDistance(pos, env.sceneEditorBuildMaxLength)) {
                        continue;
                    }
                }

                if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) {
                    pos += hitInfo.normal;
                    if (!env.IsSolidAtPosition(pos)) {
                        VoxelIndex vi = new VoxelIndex();
                        vi.chunk = chunk;
                        vi.voxelIndex = voxelIndex;
                        voxelIndices.Add(vi);
                    }
                }
            }
        }

        public override void HighlightVoxels (ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {
            if (!hitInfo.voxel.type.isVegetation) {
                env.ClearHighlight();
                return;
            }

            VoxelHitInfo highlightHitInfo = hitInfo;
            if (env.sceneEditorBrushContinuousMode && isMouseDown) {
                highlightHitInfo = startHitInfo;
            }
            base.HighlightVoxels(ref highlightHitInfo, voxelIndices, color, edgeWidth, fadeAmplitude);
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            if (indices.Count == 0) return false;

            // If control key is pressed, select the voxel type from the hit info but don't build anything
            if (control && !hitInfo.voxel.isEmpty) {
                env.sceneEditorBuildVoxel = hitInfo.voxel.type;
                // Add the selected voxel to recent voxels
                VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(hitInfo.voxel.type);
                // Return false to indicate no chunks were modified
                return false;
            }

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            int count = indices.Count;
            for (int i = 0; i < count; i++) {
                VoxelIndex vi = indices[i];
                Vector3d baseCenter = env.GetVoxelPosition(vi.chunk, vi.voxelIndex);
                ComputeSlabPlacement(ref hitInfo, baseCenter, out Vector3d placePos, out bool isTopHalf);

                if (env.GetVoxelIndex(placePos, out VoxelChunk targetChunk, out int targetVoxelIndex, createChunkIfNotExists: true)) {
                    undoManager.SaveChunk(targetChunk);
                    Voxel targetVoxel = targetChunk.voxels[targetVoxelIndex];
                    if (targetVoxel.type.isVegetation) {
                        targetChunk.ClearVoxel(targetVoxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                        modifiedChunks.Add(targetChunk);
                    } else if (shift) {
                        if (env.MicroVoxelDestroySlab(placePos, isTopHalf)) {
                            modifiedChunks.Add(targetChunk);
                        }
                    } else {
                        VoxelDefinition vd = env.sceneEditorBuildVoxel == null ? hitInfo.voxel.type : env.sceneEditorBuildVoxel;
                        env.MicroVoxelPlaceSlab(placePos, vd, hitInfo.voxel.color, topHalf: isTopHalf, rotation: env.sceneEditorPlacementRotation);
                        modifiedChunks.Add(targetChunk);
                    }
                }
            }

            int modifiedCount = modifiedChunks.Count;

            VoxelPlayEnvironmentEditor.currentEditingEnv.RefreshSelection();

            RefreshModifiedChunks(modifiedChunks);
            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return modifiedCount > 0;

        }

    }

}