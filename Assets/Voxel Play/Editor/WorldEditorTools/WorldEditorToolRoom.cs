using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolRoom : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolRoom");
        public override string title => "Create a room with walls, floor, ceiling and entrances";
        public override string instructions => "Hold Shift to keep altitude.";
        public override int priority => 3;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.StructTool;
        public override bool canIgnoreWater => true;
        public override bool supportsContinuousMode => true;
        public override bool showRecentVoxels => true;

        private const int CLEAR_INTERIOR_MARKER = -1;
        private readonly bool[] rotatedWallEntrances = new bool[4];

        private bool altitudeStored;
        private double storedAltitude;

        public override void DrawInspector() {
            EditorGUILayout.LabelField("Room Dimensions", EditorStyles.boldLabel);
            env.sceneEditorBrushSizeX = EditorGUILayout.IntSlider("Width", env.sceneEditorBrushSizeX, 3, 100);
            env.sceneEditorBrushSizeY = EditorGUILayout.IntSlider("Height", env.sceneEditorBrushSizeY, 3, 100);
            env.sceneEditorBrushSizeZ = EditorGUILayout.IntSlider("Depth", env.sceneEditorBrushSizeZ, 3, 100);
            
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
            EditorGUILayout.LabelField("Entrances", EditorStyles.boldLabel);
            env.sceneEditorEnableEntrances = EditorGUILayout.Toggle("Enable Entrances", env.sceneEditorEnableEntrances);
            
            if (env.sceneEditorEnableEntrances) {
                EditorGUI.indentLevel++;
                env.sceneEditorRoomEntranceWidth = EditorGUILayout.IntSlider("Entrance Width", env.sceneEditorRoomEntranceWidth, 1, Mathf.Max(env.sceneEditorBrushSizeX - 2, env.sceneEditorBrushSizeZ - 2));
                env.sceneEditorRoomEntranceHeight = EditorGUILayout.IntSlider("Entrance Height", env.sceneEditorRoomEntranceHeight, 1, env.sceneEditorBrushSizeY - 2);
                env.sceneEditorRoomWallEntrances[0] = EditorGUILayout.Toggle("Front Wall", env.sceneEditorRoomWallEntrances[0]);
                env.sceneEditorRoomWallEntrances[1] = EditorGUILayout.Toggle("Back Wall", env.sceneEditorRoomWallEntrances[1]);
                env.sceneEditorRoomWallEntrances[2] = EditorGUILayout.Toggle("Left Wall", env.sceneEditorRoomWallEntrances[2]);
                env.sceneEditorRoomWallEntrances[3] = EditorGUILayout.Toggle("Right Wall", env.sceneEditorRoomWallEntrances[3]);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Interior", EditorStyles.boldLabel);
            env.sceneEditorRoomClearInterior = EditorGUILayout.Toggle(new GUIContent("Clear Interior", "If enabled, clears all voxels inside the room upon placement."), env.sceneEditorRoomClearInterior);
            
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
            
            EditorGUILayout.Space();
            env.sceneEditorBrushHeightOffset = EditorGUILayout.Slider(new GUIContent("Height Offset (Wheel)", "Use mousewheel to change elevation."), env.sceneEditorBrushHeightOffset, -10f, 10f);
            EditorGUILayout.BeginHorizontal();
            env.sceneEditorPlacementRotation = EditorGUILayout.IntSlider("Rotation (R)", env.sceneEditorPlacementRotation, 0, 3);
            GUILayout.Label(env.sceneEditorPlacementRotation * 90 + "°");
            EditorGUILayout.EndHorizontal();

            // Draw the recent voxels grid
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

            // Ensure we have default voxels
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

            // Use wall voxel as default for floor and ceiling if not set
            if (env.sceneEditorRoomFloorVoxel == null) env.sceneEditorRoomFloorVoxel = env.sceneEditorBuildVoxel;
            if (env.sceneEditorRoomCeilingVoxel == null) env.sceneEditorRoomCeilingVoxel = env.sceneEditorBuildVoxel;

            int sizeX = env.sceneEditorBrushSizeX;
            int sizeY = env.sceneEditorBrushSizeY;
            int sizeZ = env.sceneEditorBrushSizeZ;
            Vector3d topLeftCorner = hitInfo.voxelCenter;
            topLeftCorner.x -= sizeX / 2;
            topLeftCorner.z -= sizeZ / 2;

            int rotationCase = env.sceneEditorPlacementRotation % 4;

            // Clear the class-level array for reuse
            System.Array.Clear(rotatedWallEntrances, 0, rotatedWallEntrances.Length);
            for (int i = 0; i < 4; i++) {
                int angle;
                switch (i) {
                    case 0: angle = 0; break;      // Front
                    case 1: angle = 180; break;    // Back
                    case 2: angle = 270; break;    // Left
                    default: angle = 90; break;    // Right (i == 3)
                }
                int newAngle = (angle + rotationCase * 90) % 360;
                int j;
                switch (newAngle) {
                    case 0:   j = 0; break;   // Front
                    case 180: j = 1; break;   // Back
                    case 270: j = 2; break;   // Left
                    default:  j = 3; break;   // Right (90°)
                }
                rotatedWallEntrances[j] = env.sceneEditorRoomWallEntrances[i];
            }

            // Helper function to apply rotation
            void ApplyRotation(float x, float z, out float rotatedX, out float rotatedZ) {
                switch (rotationCase) {
                    default: // 0°
                        rotatedX = x;
                        rotatedZ = z;
                        break;
                    case 1:   // 90° clockwise
                        rotatedX = sizeZ - 1 - z;
                        rotatedZ = x;
                        break;
                    case 2:   // 180°
                        rotatedX = sizeX - 1 - x;
                        rotatedZ = sizeZ - 1 - z;
                        break;
                    case 3:   // 270° clockwise (or 90° counter-clockwise)
                        rotatedX = z;
                        rotatedZ = sizeX - 1 - x;
                        break;
                }
            }

            // Check if a position is inside an entrance
            bool IsInEntrance(int px, int py, int pz) {
                if (!env.sceneEditorEnableEntrances || py < 1 || py >= env.sceneEditorRoomEntranceHeight + 1) return false;
                
                int entranceSpanStartOffset = Mathf.FloorToInt((env.sceneEditorRoomEntranceWidth - 1) / 2.0f);
                int entranceSpanEndOffset = Mathf.FloorToInt(env.sceneEditorRoomEntranceWidth / 2.0f);

                int currentRotationCase = env.sceneEditorPlacementRotation % 4; 

                int checkX = px;
                int checkZ = pz;
                switch (currentRotationCase) {
                    case 1:
                        checkX = env.sceneEditorBrushSizeZ - 1 - pz;
                        checkZ = px;
                        break;
                    case 2:
                        checkX = env.sceneEditorBrushSizeX - 1 - px;
                        checkZ = env.sceneEditorBrushSizeZ - 1 - pz;
                        break;
                    case 3:
                        checkX = pz;
                        checkZ = env.sceneEditorBrushSizeX - 1 - px;
                        break;
                }

                int effectiveRoomWidth = env.sceneEditorBrushSizeX;  
                int effectiveRoomDepth = env.sceneEditorBrushSizeZ;   

                if (currentRotationCase == 1 || currentRotationCase == 3) { 
                    effectiveRoomWidth = env.sceneEditorBrushSizeZ; 
                    effectiveRoomDepth = env.sceneEditorBrushSizeX;  
                }

                int effectiveCenterX = (effectiveRoomWidth - 1) / 2;
                int effectiveCenterZ = (effectiveRoomDepth - 1) / 2;

                if (rotatedWallEntrances[0] && checkZ == 0 && (checkX >= effectiveCenterX - entranceSpanStartOffset && checkX <= effectiveCenterX + entranceSpanEndOffset)) return true;
                
                if (rotatedWallEntrances[1] && checkZ == effectiveRoomDepth - 1 && (checkX >= effectiveCenterX - entranceSpanStartOffset && checkX <= effectiveCenterX + entranceSpanEndOffset)) return true;
                
                if (rotatedWallEntrances[2] && checkX == 0 && (checkZ >= effectiveCenterZ - entranceSpanStartOffset && checkZ <= effectiveCenterZ + entranceSpanEndOffset)) return true;
                
                if (rotatedWallEntrances[3] && checkX == effectiveRoomWidth - 1 && (checkZ >= effectiveCenterZ - entranceSpanStartOffset && checkZ <= effectiveCenterZ + entranceSpanEndOffset)) return true;
                
                return false;
            }

            // Clear Interior if enabled
            if (env.sceneEditorRoomClearInterior) {
                for (int py = 0; py < sizeY; py++) { 
                    for (int px = 0; px < sizeX; px++) { 
                        for (int pz = 0; pz < sizeZ; pz++) { 
                            ApplyRotation(px, pz, out float rotatedX, out float rotatedZ);
                            VoxelIndex interiorVi = new VoxelIndex();
                            interiorVi.position = new Vector3d(
                                topLeftCorner.x + rotatedX,
                                topLeftCorner.y + py,
                                topLeftCorner.z + rotatedZ
                            );
                            interiorVi.temp = CLEAR_INTERIOR_MARKER;
                            voxelIndices.Add(interiorVi);
                        }
                    }
                }
            }            

            // Create floor (using floor voxel)
            for (int px = 0; px < sizeX; px++) {
                for (int pz = 0; pz < sizeZ; pz++) {
                    ApplyRotation(px, pz, out float rotatedX, out float rotatedZ);
                    vi.position = new Vector3d(
                        topLeftCorner.x + rotatedX,
                        topLeftCorner.y,
                        topLeftCorner.z + rotatedZ
                    );
                    vi.temp = env.sceneEditorRoomFloorVoxel.index;
                    voxelIndices.Add(vi);
                }
            }

            // Create ceiling (using ceiling voxel)
            for (int px = 0; px < sizeX; px++) {
                for (int pz = 0; pz < sizeZ; pz++) {
                    ApplyRotation(px, pz, out float rotatedX, out float rotatedZ);
                    vi.position = new Vector3d(
                        topLeftCorner.x + rotatedX,
                        topLeftCorner.y + sizeY - 1,
                        topLeftCorner.z + rotatedZ
                    );
                    vi.temp = env.sceneEditorRoomCeilingVoxel.index;
                    voxelIndices.Add(vi);
                }
            }

            // Add crenelation on top if enabled
            if (env.sceneEditorAddCrenelationOnTop && env.sceneEditorBuildVoxel != null && sizeY > 0) {
                VoxelDefinition crenelationVoxel = env.sceneEditorCrenelationVoxel != null ? env.sceneEditorCrenelationVoxel : env.sceneEditorBuildVoxel;
                for (int px = 0; px < sizeX; px++) {
                    for (int pz = 0; pz < sizeZ; pz++) {
                        // Only on the outer edge (perimeter)
                        bool isEdge = px == 0 || px == sizeX - 1 || pz == 0 || pz == sizeZ - 1;
                        if (isEdge && ((px + pz) & 1) == 0) { // Alternating pattern
                            ApplyRotation(px, pz, out float rotatedX, out float rotatedZ);
                            vi.position = new Vector3d(
                                topLeftCorner.x + rotatedX,
                                topLeftCorner.y + sizeY,
                                topLeftCorner.z + rotatedZ
                            );
                            vi.temp = crenelationVoxel.index;
                            voxelIndices.Add(vi);
                        }
                    }
                }
            }

            // Create walls (using wall voxel)
            vi.temp = env.sceneEditorBuildVoxel.index;

            for (int py = 1; py < sizeY - 1; py++) {
                for (int px = 0; px < sizeX; px++) {
                    if (!IsInEntrance(px, py, 0)) {
                        ApplyRotation(px, 0, out float rotatedX, out float rotatedZ);
                        vi.position = new Vector3d(
                            topLeftCorner.x + rotatedX,
                            topLeftCorner.y + py,
                            topLeftCorner.z + rotatedZ
                        );
                        voxelIndices.Add(vi);
                    }
                    
                    if (!IsInEntrance(px, py, sizeZ - 1)) {
                        ApplyRotation(px, sizeZ - 1, out float rotatedX, out float rotatedZ);
                        vi.position = new Vector3d(
                            topLeftCorner.x + rotatedX,
                            topLeftCorner.y + py,
                            topLeftCorner.z + rotatedZ
                        );
                        voxelIndices.Add(vi);
                    }
                }

                for (int pz = 1; pz < sizeZ - 1; pz++) {
                    if (!IsInEntrance(0, py, pz)) {
                        ApplyRotation(0, pz, out float rotatedX, out float rotatedZ);
                        vi.position = new Vector3d(
                            topLeftCorner.x + rotatedX,
                            topLeftCorner.y + py,
                            topLeftCorner.z + rotatedZ
                        );
                        voxelIndices.Add(vi);
                    }
                    
                    if (!IsInEntrance(sizeX - 1, py, pz)) {
                        ApplyRotation(sizeX - 1, pz, out float rotatedX, out float rotatedZ);
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

        public override void HighlightVoxels(ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {
        }

        public override void DrawGizmos(VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
            int count = voxelIndices.Count;
            if (count == 0) return;

            // Calculate bounds
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int k = 0; k < count; k++) {
                Vector3 pos = voxelIndices[k].position;
                min.x = Mathf.Min(min.x, pos.x - 0.5f);
                min.y = Mathf.Min(min.y, pos.y - 0.5f);
                min.z = Mathf.Min(min.z, pos.z - 0.5f);
                max.x = Mathf.Max(max.x, pos.x + 0.5f);
                max.y = Mathf.Max(max.y, pos.y + 0.5f);
                max.z = Mathf.Max(max.z, pos.z + 0.5f);
            }

            // Draw bounding box
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            Color boundingColor = Color.yellow;
            boundingColor.a = 0.6f;
            Handles.color = boundingColor;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.DrawWireCube(center, size);
            
            // Draw entrance indicators
            if (env.sceneEditorEnableEntrances) {
                Color entranceColor = Color.green;
                entranceColor.a = 0.8f;
                Handles.color = entranceColor;
                
                int currentRotationCase = env.sceneEditorPlacementRotation % 4;

                int entranceSpanStartOffset = Mathf.FloorToInt((env.sceneEditorRoomEntranceWidth - 1) / 2.0f);
                int entranceSpanEndOffset = Mathf.FloorToInt(env.sceneEditorRoomEntranceWidth / 2.0f);

                int actualBrushSizeX = env.sceneEditorBrushSizeX;
                int actualBrushSizeZ = env.sceneEditorBrushSizeZ;

                int effectiveRoomWidth_of_RotatedRoom = actualBrushSizeX;
                int effectiveRoomDepth_of_RotatedRoom = actualBrushSizeZ;
                if (currentRotationCase == 1 || currentRotationCase == 3) { // 90 or 270 deg rotation
                    effectiveRoomWidth_of_RotatedRoom = actualBrushSizeZ;
                    effectiveRoomDepth_of_RotatedRoom = actualBrushSizeX;
                }
                
                int effectiveCenterX_of_RotatedRoom = (effectiveRoomWidth_of_RotatedRoom - 1) / 2;
                int effectiveCenterZ_of_RotatedRoom = (effectiveRoomDepth_of_RotatedRoom - 1) / 2;

                float gizmo_center_voxel_index_float_X = effectiveCenterX_of_RotatedRoom + (entranceSpanEndOffset - entranceSpanStartOffset) / 2.0f;
                float gizmo_center_voxel_index_float_Z = effectiveCenterZ_of_RotatedRoom + (entranceSpanEndOffset - entranceSpanStartOffset) / 2.0f;
                
                // Clear the class-level array for reuse
                System.Array.Clear(rotatedWallEntrances, 0, rotatedWallEntrances.Length);
                for (int i = 0; i < 4; i++) {
                    int angle;
                    switch (i) {
                        case 0: angle = 0; break;      // Front
                        case 1: angle = 180; break;    // Back
                        case 2: angle = 270; break;    // Left
                        default: angle = 90; break;    // Right (i == 3)
                    }
                    int newAngle = (angle + currentRotationCase * 90) % 360;
                    int j;
                    switch (newAngle) {
                        case 0:   j = 0; break;   // Front
                        case 180: j = 1; break;   // Back
                        case 270: j = 2; break;   // Left
                        default:  j = 3; break;   // Right (90°)
                    }
                    rotatedWallEntrances[j] = env.sceneEditorRoomWallEntrances[i];
                }

                float gizmo_Y_center = (min.y + 1f) + (env.sceneEditorRoomEntranceHeight - 1) / 2.0f + 0.5f;

                for (int k = 0; k < 4; k++) {
                    if (rotatedWallEntrances[k]) {
                        Vector3 entranceCenter = Vector3.zero;
                        Vector3 entranceSize = Vector3.zero;

                        switch (k) {
                            case 0:
                                entranceCenter = new Vector3(min.x + gizmo_center_voxel_index_float_X + 0.5f, gizmo_Y_center, min.z + 0.1f);
                                entranceSize = new Vector3(env.sceneEditorRoomEntranceWidth, env.sceneEditorRoomEntranceHeight, 0.2f);
                                break;
                            case 1:
                                entranceCenter = new Vector3(min.x + gizmo_center_voxel_index_float_X + 0.5f, gizmo_Y_center, max.z - 0.1f);
                                entranceSize = new Vector3(env.sceneEditorRoomEntranceWidth, env.sceneEditorRoomEntranceHeight, 0.2f);
                                break;
                            case 2:
                                entranceCenter = new Vector3(min.x + 0.1f, gizmo_Y_center, min.z + gizmo_center_voxel_index_float_Z + 0.5f);
                                entranceSize = new Vector3(0.2f, env.sceneEditorRoomEntranceHeight, env.sceneEditorRoomEntranceWidth);
                                break;
                            case 3:
                                entranceCenter = new Vector3(max.x - 0.1f, gizmo_Y_center, min.z + gizmo_center_voxel_index_float_Z + 0.5f);
                                entranceSize = new Vector3(0.2f, env.sceneEditorRoomEntranceHeight, env.sceneEditorRoomEntranceWidth);
                                break;
                        }
                        
                        Handles.DrawWireCube(entranceCenter, entranceSize);
                    }
                }
            }
        }

        protected override bool Execute(ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {
            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            int count = indices.Count;
            for (int k = 0; k < count; k++) {
                VoxelIndex vi = indices[k];
                Vector3d pos = vi.position;
                
                if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) continue;
                
                undoManager.SaveChunk(chunk);

                if (vi.temp == CLEAR_INTERIOR_MARKER) {
                    chunk.ClearVoxel(voxelIndex, VoxelPlayEnvironment.FULL_LIGHT); 
                    if (!modifiedChunks.Contains(chunk)) { // Add to refresh list, avoid duplicates if already added
                        modifiedChunks.Add(chunk);    
                    }
                } else if (vi.temp > 0) { 
                    VoxelDefinition voxelToPlace = env.voxelDefinitions[vi.temp];
                    if (voxelToPlace != null) { 
                        chunk.voxels[voxelIndex].Set(voxelToPlace);
                        if (!modifiedChunks.Contains(chunk)) { // Add to refresh list, avoid duplicates
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