using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolTower : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolTower");
        public override string title => "Create a cylindrical tower with walls, floor, ceiling and an optional entrance";
        public override string instructions => "Hold Shift to keep altitude. Use R to rotate entrance.";
        public override int priority => 4; 
        public override WorldEditorToolCategory category => WorldEditorToolCategory.StructTool;
        public override bool canIgnoreWater => true;
        public override bool supportsContinuousMode => true;
        public override bool showRecentVoxels => true;

        private const int CLEAR_INTERIOR_MARKER = -1;

        private bool altitudeStored;
        private double storedAltitude;

        public override void DrawInspector() {
            EditorGUILayout.LabelField("Tower Dimensions", EditorStyles.boldLabel);
            env.sceneEditorBrushSizeX = EditorGUILayout.IntSlider("Radius", env.sceneEditorBrushSizeX, 2, 50);
            env.sceneEditorBrushSizeY = EditorGUILayout.IntSlider("Height", env.sceneEditorBrushSizeY, 3, 100);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            env.sceneEditorBuildVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Wall Voxel", env.sceneEditorBuildVoxel, typeof(VoxelDefinition), false);
            if (EditorGUI.EndChangeCheck() && env.sceneEditorBuildVoxel != null) {
                if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorBuildVoxel);
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            env.sceneEditorRoomFloorVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Floor Voxel", env.sceneEditorRoomFloorVoxel, typeof(VoxelDefinition), false);
            if (EditorGUI.EndChangeCheck() && env.sceneEditorRoomFloorVoxel != null) {
                if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorRoomFloorVoxel);
                }
            }
            if (GUILayout.Button("Copy", GUILayout.Width(50))) {
                env.sceneEditorRoomFloorVoxel = env.sceneEditorBuildVoxel;
                if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorRoomFloorVoxel);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            env.sceneEditorRoomCeilingVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Ceiling Voxel", env.sceneEditorRoomCeilingVoxel, typeof(VoxelDefinition), false);
            if (EditorGUI.EndChangeCheck() && env.sceneEditorRoomCeilingVoxel != null) {
                if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorRoomCeilingVoxel);
                }
            }
            if (GUILayout.Button("Copy", GUILayout.Width(50))) {
                env.sceneEditorRoomCeilingVoxel = env.sceneEditorBuildVoxel;
                if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorRoomCeilingVoxel);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Entrance", EditorStyles.boldLabel);
            env.sceneEditorEnableEntrances = EditorGUILayout.Toggle("Enable Entrance", env.sceneEditorEnableEntrances); 

            if (env.sceneEditorEnableEntrances) {
                EditorGUI.indentLevel++;
                env.sceneEditorRoomEntranceWidth = EditorGUILayout.IntSlider("Entrance Width", env.sceneEditorRoomEntranceWidth, 1, Mathf.Max(env.sceneEditorBrushSizeX * 2 - 2, 3)); 
                env.sceneEditorRoomEntranceHeight = EditorGUILayout.IntSlider("Entrance Height", env.sceneEditorRoomEntranceHeight, 1, Mathf.Max(1, env.sceneEditorBrushSizeY - 2));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Interior", EditorStyles.boldLabel);
            env.sceneEditorRoomClearInterior = EditorGUILayout.Toggle(new GUIContent("Clear Interior", "If enabled, clears all voxels inside the tower upon placement."), env.sceneEditorRoomClearInterior);

            EditorGUILayout.Space();
            env.sceneEditorBrushHeightOffset = EditorGUILayout.Slider(new GUIContent("Height Offset (Wheel)", "Use mousewheel to change elevation."), env.sceneEditorBrushHeightOffset, -10f, 10f);
            EditorGUILayout.BeginHorizontal();
            env.sceneEditorPlacementRotation = EditorGUILayout.IntSlider("Rotation (R)", env.sceneEditorPlacementRotation, 0, 3); 
            GUILayout.Label(env.sceneEditorPlacementRotation * 90 + "°");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            env.sceneEditorAddCrenelationOnTop = EditorGUILayout.Toggle("Add Crenelation on Top", env.sceneEditorAddCrenelationOnTop);
            if (env.sceneEditorAddCrenelationOnTop) {
                EditorGUILayout.BeginHorizontal();
                env.sceneEditorCrenelationVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Crenelation Voxel", env.sceneEditorCrenelationVoxel, typeof(VoxelDefinition), false);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) {
                    env.sceneEditorCrenelationVoxel = env.sceneEditorBuildVoxel;
                    if (env.sceneEditorCrenelationVoxel != null && VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                        VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorCrenelationVoxel);
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (env.sceneEditorCrenelationVoxel != null && VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorCrenelationVoxel);
                }
            }

            DrawRecentVoxelsGrid();
        }

        public override void DrawLabel(VoxelHitInfo hitInfo) {
            float labelOffset = supportsMicroVoxels && env.sceneEditorBrushMicroVoxelSize > 0 ? 0.1f : 0.5f;
            hitInfo.voxelCenter.y += env.sceneEditorBrushHeightOffset;
            string label = hitInfo.voxelCenter.ToString("F2");
            Handles.Label(hitInfo.point + new Vector3(labelOffset, labelOffset, labelOffset), label);
        }

        public override bool RayCast(Ray ray, out VoxelHitInfo hitInfo) {
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
            FastVector.Middling(ref hitPoint);
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

        public override void Update() {
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
        
        public override void SelectVoxels(ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            VoxelIndex vi = new VoxelIndex();
            voxelIndices.Clear();

            if (env.sceneEditorBuildVoxel == null || env.sceneEditorBuildVoxel.doNotSave) {
                foreach (var vd in env.voxelDefinitions) {
                    if (vd.renderType.isOpaque() && !vd.doNotSave) {
                        env.sceneEditorBuildVoxel = vd;
                        break;
                    }
                }
                if (env.sceneEditorBuildVoxel == null) return;
            }

            if (env.sceneEditorRoomFloorVoxel == null) env.sceneEditorRoomFloorVoxel = env.sceneEditorBuildVoxel;
            if (env.sceneEditorRoomCeilingVoxel == null) env.sceneEditorRoomCeilingVoxel = env.sceneEditorBuildVoxel;

            int radius = env.sceneEditorBrushSizeX;
            int height = env.sceneEditorBrushSizeY;
            Vector3d center = hitInfo.voxelCenter;
            float R = (float)radius;
            float R_minus_half_sq = (R - 0.5f) * (R - 0.5f);
            float R_plus_half_sq = (R + 0.5f) * (R + 0.5f);

            if (env.sceneEditorRoomClearInterior) {
                for (int py = 0; py < height; py++) {
                    for (int px = -radius; px <= radius; px++) {
                        for (int pz = -radius; pz <= radius; pz++) {
                            if (px * px + pz * pz <= R_plus_half_sq) {
                                VoxelIndex interiorVi = new VoxelIndex();
                                interiorVi.position = new Vector3d(
                                    center.x + px,
                                    center.y + py,
                                    center.z + pz
                                );
                                interiorVi.temp = CLEAR_INTERIOR_MARKER;
                                voxelIndices.Add(interiorVi);
                            }
                        }
                    }
                }
            }

            for (int px = -radius; px <= radius; px++) {
                for (int pz = -radius; pz <= radius; pz++) {
                    if (px * px + pz * pz <= R_plus_half_sq) {
                        vi.position = new Vector3d(center.x + px, center.y, center.z + pz);
                        vi.temp = env.sceneEditorRoomFloorVoxel.index;
                        voxelIndices.Add(vi);
                    }
                }
            }

            if (height > 0) {
                for (int px = -radius; px <= radius; px++) {
                    for (int pz = -radius; pz <= radius; pz++) {
                        if (px * px + pz * pz <= R_plus_half_sq) {
                            vi.position = new Vector3d(center.x + px, center.y + height - 1, center.z + pz);
                            vi.temp = env.sceneEditorRoomCeilingVoxel.index;
                            voxelIndices.Add(vi);
                        }
                    }
                }
            }

            if (env.sceneEditorAddCrenelationOnTop && height > 0 && env.sceneEditorBuildVoxel != null) {
                for (int px = -radius; px <= radius; px++) {
                    for (int pz = -radius; pz <= radius; pz++) {
                        float distSq = px * px + pz * pz;
                        if (distSq > R_minus_half_sq && distSq < R_plus_half_sq) {
                            if (((px + pz) & 1) == 0) {
                                vi.position = new Vector3d(center.x + px, center.y + height, center.z + pz);
                                vi.temp = env.sceneEditorBuildVoxel.index;
                                voxelIndices.Add(vi);
                            }
                        }
                    }
                }
            }
            
            bool IsInEntrance(int currentX, int currentZ, int currentYInTower) {
                if (!env.sceneEditorEnableEntrances || currentYInTower < 1 || currentYInTower >= env.sceneEditorRoomEntranceHeight + 1) return false;

                double angleRad = System.Math.Atan2(currentZ, currentX);
                double angleDeg = angleRad * (180.0 / System.Math.PI); 
                if (angleDeg < 0) angleDeg += 360;

                float entranceFacingAngleDeg = 0;
                switch (env.sceneEditorPlacementRotation % 4) {
                    case 0: entranceFacingAngleDeg = 90; break;
                    case 1: entranceFacingAngleDeg = 0; break;
                    case 2: entranceFacingAngleDeg = 270; break;
                    case 3: entranceFacingAngleDeg = 180; break;
                }
                
                float entranceLinearWidth = env.sceneEditorRoomEntranceWidth;
                float wallOuterRadius = radius - 0.5f;
                if (wallOuterRadius < 0.5f) wallOuterRadius = 0.5f;
                
                double halfAngularWidthDeg = (entranceLinearWidth / (2.0 * System.Math.PI * wallOuterRadius)) * 360.0 / 2.0;
                if (wallOuterRadius < 0.5f) halfAngularWidthDeg = 90;

                double lowerBound = entranceFacingAngleDeg - halfAngularWidthDeg;
                double upperBound = entranceFacingAngleDeg + halfAngularWidthDeg;

                if (lowerBound < 0) {
                    return (angleDeg >= 360 + lowerBound && angleDeg <= 360) || (angleDeg >= 0 && angleDeg <= upperBound);
                } else if (upperBound >= 360) {
                    return (angleDeg >= lowerBound && angleDeg <= 360) || (angleDeg >= 0 && angleDeg <= upperBound - 360);
                } else {
                    return angleDeg >= lowerBound && angleDeg <= upperBound;
                }
            }

            if (env.sceneEditorBuildVoxel != null && height > 2) {
                 vi.temp = env.sceneEditorBuildVoxel.index;
                for (int pyTower = 1; pyTower < height - 1; pyTower++) {
                    for (int px = -radius; px <= radius; px++) {
                        for (int pz = -radius; pz <= radius; pz++) {
                            float distSq = px * px + pz * pz;
                            if (distSq > R_minus_half_sq && distSq < R_plus_half_sq) {
                                 if (!IsInEntrance(px, pz, pyTower)) {
                                    vi.position = new Vector3d(center.x + px, center.y + pyTower, center.z + pz);
                                    voxelIndices.Add(vi);
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void HighlightVoxels(ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {
        }

        public override void DrawGizmos(VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
            int radius = env.sceneEditorBrushSizeX;
            int height = Mathf.Max(1, env.sceneEditorBrushSizeY);
            
            Vector3 gizmoBoxCenter = hitInfo.voxelCenter; 
            gizmoBoxCenter.y += (height -1) * 0.5f;

            Vector3 gizmoBoxSize = new Vector3(radius * 2 + 1, height, radius * 2 + 1); 

            Color boundingColor = Color.yellow;
            boundingColor.a = 0.6f;
            Handles.color = boundingColor;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.DrawWireCube(gizmoBoxCenter, gizmoBoxSize);

            if (env.sceneEditorEnableEntrances && height > 2) {
                Color entranceColor = Color.green;
                entranceColor.a = 0.8f;
                Handles.color = entranceColor;

                float entranceWidth = env.sceneEditorRoomEntranceWidth;
                float entranceHeight = Mathf.Min(env.sceneEditorRoomEntranceHeight, height - 2);
                if (entranceHeight < 1) return;

                Vector3 towerBaseCenter = hitInfo.voxelCenter;
                Vector3 entranceGizmoCenter = towerBaseCenter;
                entranceGizmoCenter.y += 1f + (entranceHeight -1) * 0.5f;

                float offsetOnBoxSurface = radius + 0.5f; 
                Vector3 entranceGizmoSize = Vector3.zero;

                switch (env.sceneEditorPlacementRotation % 4) {
                    case 0:
                        entranceGizmoCenter.z += offsetOnBoxSurface;
                        entranceGizmoSize = new Vector3(entranceWidth, entranceHeight, 0.2f);
                        break;
                    case 1:
                        entranceGizmoCenter.x += offsetOnBoxSurface;
                        entranceGizmoSize = new Vector3(0.2f, entranceHeight, entranceWidth);
                        break;
                    case 2:
                        entranceGizmoCenter.z -= offsetOnBoxSurface;
                        entranceGizmoSize = new Vector3(entranceWidth, entranceHeight, 0.2f);
                        break;
                    case 3:
                        entranceGizmoCenter.x -= offsetOnBoxSurface;
                        entranceGizmoSize = new Vector3(0.2f, entranceHeight, entranceWidth);
                        break;
                }
                Handles.DrawWireCube(entranceGizmoCenter, entranceGizmoSize);
            }
        }

        protected override bool Execute(ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {
            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            int count = indices.Count;
            if (count == 0) return false;

            for (int k = 0; k < count; k++) {
                VoxelIndex vi = indices[k];
                Vector3d pos = vi.position;
                
                if (vi.temp != CLEAR_INTERIOR_MARKER && (vi.temp <= 0 || vi.temp >= env.voxelDefinitions.Length)) {
                    continue;
                }

                if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) continue;
                
                undoManager.SaveChunk(chunk);

                if (vi.temp == CLEAR_INTERIOR_MARKER) {
                    chunk.ClearVoxel(voxelIndex, VoxelPlayEnvironment.FULL_LIGHT); 
                    if (!modifiedChunks.Contains(chunk)) { 
                        modifiedChunks.Add(chunk);    
                    }
                } else if (vi.temp > 0) {
                    VoxelDefinition voxelToPlace = env.voxelDefinitions[vi.temp];
                    if (voxelToPlace != null && !voxelToPlace.doNotSave) { 
                        chunk.voxels[voxelIndex].Set(voxelToPlace);
                        if (!modifiedChunks.Contains(chunk)) { 
                           modifiedChunks.Add(chunk);
                        }
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