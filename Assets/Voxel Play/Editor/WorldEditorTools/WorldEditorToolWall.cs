using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolWall : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolWall");
        public override string title => "Create a wall with adjustable width, height and thickness";
        public override string instructions => "Hold Shift to keep altitude.";
        public override int priority => 2;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.StructTool;
        public override bool canIgnoreWater => true;
        public override bool supportsContinuousMode => true;
        public override bool showRecentVoxels => true;

        private bool altitudeStored;
        private double storedAltitude;

        public override void DrawInspector () {
            EditorGUI.BeginChangeCheck();
            env.sceneEditorBuildVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Voxel Definition", env.sceneEditorBuildVoxel, typeof(VoxelDefinition), false);
            if (EditorGUI.EndChangeCheck() && env.sceneEditorBuildVoxel != null) {
                // Add to recent voxels
                if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorBuildVoxel);
                }
            }

            env.sceneEditorBrushSizeX = EditorGUILayout.IntSlider("Width", env.sceneEditorBrushSizeX, 1, 100);
            env.sceneEditorBrushSizeY = EditorGUILayout.IntSlider("Height", env.sceneEditorBrushSizeY, 1, 100);
            env.sceneEditorWallThickness = EditorGUILayout.IntSlider("Thickness", env.sceneEditorWallThickness, 1, 100);
            env.sceneEditorBrushHeightOffset = EditorGUILayout.Slider(new GUIContent("Height Offset (Wheel)", "Use mousewheel to change elevation."), env.sceneEditorBrushHeightOffset, -10f, 10f);
            EditorGUILayout.BeginHorizontal();
            env.sceneEditorPlacementRotation = EditorGUILayout.IntSlider("Rotation (R)", env.sceneEditorPlacementRotation, 0, 3);
            GUILayout.Label(env.sceneEditorPlacementRotation * 90 + "°");
            EditorGUILayout.EndHorizontal();

            // Draw the recent voxels grid
            DrawRecentVoxelsGrid();
        }

        public override void DrawLabel (VoxelHitInfo hitInfo) {
            float labelOffset = supportsMicroVoxels && env.sceneEditorBrushMicroVoxelSize > 0 ? 0.1f : 0.5f;
            hitInfo.voxelCenter.y += env.sceneEditorBrushHeightOffset;
            string label = hitInfo.voxelCenter.ToString("F2");
            Handles.Label(hitInfo.point + new Vector3(labelOffset, labelOffset, labelOffset), label);
        }

        public override bool RayCast (Ray ray, out VoxelHitInfo hitInfo) {
            const float MAX_DISTANCE = 15f;
            if (env.RayCast(ray, out hitInfo, minOpaque: VoxelPlayEnvironment.FULL_OPAQUE, ignoreWater: IgnoreWaterOption.IgnoreWater)) {
                hitInfo.voxelCenter += hitInfo.normal;
                hitInfo.voxelCenter.y += env.sceneEditorBrushHeightOffset;
                if (shift) {
                    if (!altitudeStored) {
                        storedAltitude = hitInfo.voxelCenter.y;
                        altitudeStored = true;
                    }
                    hitInfo.voxelCenter.y = storedAltitude;
                    hitInfo.point.y = storedAltitude - env.sceneEditorBrushHeightOffset;
                }
                return true;
            }

            Vector3d hitPoint = ray.origin + ray.direction * MAX_DISTANCE;
            FastVector.Middling(ref hitPoint); ;
            Vector3d voxelCenter = hitPoint;
            voxelCenter.y += env.sceneEditorBrushHeightOffset;
            if (shift) {
                if (!altitudeStored) {
                    storedAltitude = voxelCenter.y;
                    altitudeStored = true;
                }
                hitInfo.voxelCenter.y = storedAltitude;
                hitInfo.point.y = storedAltitude - env.sceneEditorBrushHeightOffset;
            }

            hitInfo = new VoxelHitInfo {
                voxelCenter = voxelCenter,
                point = hitPoint
            };
            return true;
        }

        public override void Update () {
            if (mouseWheel < 0) {
                env.sceneEditorBrushHeightOffset++;
                mouseWheel = 0;
            } else if (mouseWheel > 0) {
                env.sceneEditorBrushHeightOffset--;
                mouseWheel = 0;
            }
            if (keyR) {
                env.sceneEditorPlacementRotation = (env.sceneEditorPlacementRotation + 1) % 4;
            }
            // Reset altitude storage when shift is released
            if (!shift) {
                altitudeStored = false;
            }
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            VoxelIndex vi = new VoxelIndex();
            voxelIndices.Clear();

            if (env.sceneEditorBuildVoxel == null || env.sceneEditorBuildVoxel.doNotSave) {
                foreach (var vd in env.voxelDefinitions) {
                    if (vd.renderType.isOpaque() && !vd.doNotSave) {
                        env.sceneEditorBuildVoxel = vd;
                        break;
                    }
                }
                if (env.sceneEditorBuildVoxel == null) {
                    return;
                }
            }

            int sizeX = env.sceneEditorBrushSizeX;
            int sizeY = env.sceneEditorBrushSizeY;
            int sizeZ = env.sceneEditorWallThickness;
            Vector3d topLeftCorner = hitInfo.voxelCenter;

            int rotationCase = env.sceneEditorPlacementRotation % 4;

            for (int py = 0; py < sizeY; py++) {
                for (int px = 0; px < sizeX; px++) {
                    for (int pz = 0; pz < sizeZ; pz++) {
                        double relativeX = px;
                        double relativeZ = pz;

                        double rotatedX, rotatedZ;

                        switch (rotationCase) {
                            default:
                                rotatedX = relativeX;
                                rotatedZ = relativeZ;
                                break;
                            case 1:
                                rotatedX = -relativeZ;
                                rotatedZ = relativeX;
                                break;
                            case 2:
                                rotatedX = -relativeX;
                                rotatedZ = -relativeZ;
                                break;
                            case 3:
                                rotatedX = relativeZ;
                                rotatedZ = -relativeX;
                                break;
                        }

                        vi.position = new Vector3d(
                            topLeftCorner.x + rotatedX,
                            topLeftCorner.y + py,
                            topLeftCorner.z + rotatedZ
                        );

                        voxelIndices.Add(vi);
                    }
                }
            }
        }

        public override void HighlightVoxels (ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {
        }

        public override void DrawGizmos (VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
            int count = voxelIndices.Count;
            if (count == 0) return;

            Color highlightColor = Color.cyan;
            highlightColor.a = 0.45f;
            Handles.color = highlightColor;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            // Calculate bounds
            Vector3d min = voxelIndices[0].position;
            Vector3d max = voxelIndices[0].position;

            for (int k = 1; k < count; k++) {
                Vector3d pos = voxelIndices[k].position;
                min.x = System.Math.Min(min.x, pos.x);
                min.y = System.Math.Min(min.y, pos.y);
                min.z = System.Math.Min(min.z, pos.z);
                max.x = System.Math.Max(max.x, pos.x);
                max.y = System.Math.Max(max.y, pos.y);
                max.z = System.Math.Max(max.z, pos.z);
            }

            // Draw bounding box
            Vector3d center = (min + max) * 0.5;
            Vector3d size = new Vector3d(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
            Handles.DrawWireCube(center, size);
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            int count = indices.Count;
            for (int k = 0; k < count; k++) {
                VoxelIndex vi = indices[k];
                Vector3d pos = vi.position;
                if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) continue;
                undoManager.SaveChunk(chunk);
                chunk.SetVoxel(voxelIndex, env.sceneEditorBuildVoxel);
                modifiedChunks.Add(chunk);
            }

            int modifiedCount = modifiedChunks.Count;
            RefreshModifiedChunks(modifiedChunks);

            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return modifiedCount > 0;

        }

    }

}