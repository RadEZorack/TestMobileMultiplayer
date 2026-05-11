using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolFloor : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolFloor");
        public override string title => "Create a flat floor";
        public override string instructions => "Hold Shift to keep altitude.";
        public override int priority => 1;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.StructTool;
        public override bool canIgnoreWater => true;
        public override bool supportsContinuousMode => true;
        public override bool showRecentVoxels => true;

        private bool altitudeStored;
        private double storedAltitude;
        private bool clearAbove = true;

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
            env.sceneEditorBrushSizeZ = EditorGUILayout.IntSlider("Depth", env.sceneEditorBrushSizeZ, 1, 100);
            env.sceneEditorBrushHeightOffset = EditorGUILayout.Slider(new GUIContent("Height Offset (Wheel)", "Use mousewheel to change elevation."), env.sceneEditorBrushHeightOffset, -10f, 10f);
            EditorGUILayout.BeginHorizontal();
            env.sceneEditorPlacementRotation = EditorGUILayout.IntSlider("Rotation (R)", env.sceneEditorPlacementRotation, 0, 3);
            GUILayout.Label(env.sceneEditorPlacementRotation * 90 + "°");
            EditorGUILayout.EndHorizontal();
            
            clearAbove = EditorGUILayout.Toggle(new GUIContent("Clear Above", "Remove all voxels above the floor when placed"), clearAbove);
            
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
            float maxDistance = shift ? float.MaxValue : MAX_DISTANCE;
            if (env.RayCast(ray, out hitInfo, minOpaque: VoxelPlayEnvironment.FULL_OPAQUE, ignoreWater: IgnoreWaterOption.IgnoreWater, maxDistance: maxDistance)) {
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

            if (env.sceneEditorBuildVoxel == null) {
                env.sceneEditorBuildVoxel = env.defaultVoxel;
                if (env.sceneEditorBuildVoxel == null) {
                    return;
                }
            }

            int sizeX = env.sceneEditorBrushSizeX;
            int sizeZ = env.sceneEditorBrushSizeZ;
            Vector3d topLeftCorner = hitInfo.voxelCenter;

            int rotationCase = env.sceneEditorPlacementRotation % 4;

            for (int pz = 0; pz < sizeZ; pz++) {
                for (int px = 0; px < sizeX; px++) {
                    // Position relative to top-left corner (without rotation)
                    double relativeX = px;
                    double relativeZ = pz;

                    // Apply rotation around the top-left corner using the optimized approach
                    double rotatedX, rotatedZ;

                    // Optimize for common rotation angles (0, 90, 180, 270 degrees)
                    switch (rotationCase) {
                        default: // 0 degrees - no rotation
                            rotatedX = relativeX;
                            rotatedZ = relativeZ;
                            break;
                        case 1: // 90 degrees
                            rotatedX = -relativeZ;
                            rotatedZ = relativeX;
                            break;
                        case 2: // 180 degrees
                            rotatedX = -relativeX;
                            rotatedZ = -relativeZ;
                            break;
                        case 3: // 270 degrees
                            rotatedX = relativeZ;
                            rotatedZ = -relativeX;
                            break;
                    }

                    // Apply the rotated position to the top-left corner
                    vi.position = new Vector3d(
                        topLeftCorner.x + rotatedX,
                        topLeftCorner.y,
                        topLeftCorner.z + rotatedZ
                    );

                    voxelIndices.Add(vi);
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

            Vector3d center = (min + max) * 0.5;
            Vector3d size = new Vector3d(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1); // Add 1 to include the full voxel
            Handles.DrawWireCube(center, size);

            // Draw yellow wireframe enclosing all affected chunks
            if (clearAbove) {
                Handles.color = Color.yellow;
                VoxelChunk lastChunk = null;
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;
                double minZ = double.MaxValue, maxZ = double.MinValue;
                bool hasBounds = false;

                for (int k = 0; k < count; k++) {
                    Vector3d pos = voxelIndices[k].position;
                    if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) {
                        if (chunk != null && chunk != lastChunk) {
                            double chunkHalfSize = VoxelPlayEnvironment.CHUNK_HALF_SIZE;
                            double chunkMinX = chunk.position.x - chunkHalfSize;
                            double chunkMaxX = chunk.position.x + chunkHalfSize;
                            double chunkMinY = chunk.position.y - chunkHalfSize;
                            double chunkMaxY = chunk.position.y + chunkHalfSize;
                            double chunkMinZ = chunk.position.z - chunkHalfSize;
                            double chunkMaxZ = chunk.position.z + chunkHalfSize;

                            if (!hasBounds) {
                                minX = chunkMinX;
                                maxX = chunkMaxX;
                                minY = chunkMinY;
                                maxY = chunkMaxY;
                                minZ = chunkMinZ;
                                maxZ = chunkMaxZ;
                                hasBounds = true;
                            } else {
                                if (chunkMinX < minX) minX = chunkMinX;
                                if (chunkMaxX > maxX) maxX = chunkMaxX;
                                if (chunkMinY < minY) minY = chunkMinY;
                                if (chunkMaxY > maxY) maxY = chunkMaxY;
                                if (chunkMinZ < minZ) minZ = chunkMinZ;
                                if (chunkMaxZ > maxZ) maxZ = chunkMaxZ;
                            }
                            lastChunk = chunk;
                        }
                    }
                }

                if (hasBounds) {
                    Vector3d chunkBoundsCenter = new Vector3d((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5);
                    Vector3d chunkBoundsSize = new Vector3d(maxX - minX, maxY - minY, maxZ - minZ);
                    Handles.DrawWireCube(chunkBoundsCenter, chunkBoundsSize);
                }
            }
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            HashSet<VoxelChunk> floorChunks = new HashSet<VoxelChunk>();

            // Calculate floor bounds
            double floorMinX = double.MaxValue, floorMaxX = double.MinValue;
            double floorMinZ = double.MaxValue, floorMaxZ = double.MinValue;
            double floorY = hitInfo.voxelCenter.y;

            int count = indices.Count;
            for (int k = 0; k < count; k++) {
                VoxelIndex vi = indices[k];
                Vector3d pos = vi.position;
                if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) continue;
                
                undoManager.SaveChunk(chunk);
                chunk.SetVoxel(voxelIndex, env.sceneEditorBuildVoxel);
                modifiedChunks.Add(chunk);
                floorChunks.Add(chunk);

                // Update floor bounds
                floorMinX = System.Math.Min(floorMinX, pos.x);
                floorMaxX = System.Math.Max(floorMaxX, pos.x);
                floorMinZ = System.Math.Min(floorMinZ, pos.z);
                floorMaxZ = System.Math.Max(floorMaxZ, pos.z);
            }

            // Clear voxels above the floor
            if (clearAbove && count > 0) {
                foreach (VoxelChunk chunk in floorChunks) {
                    ClearVoxelsAboveInChunk(chunk, floorY, floorMinX, floorMaxX, floorMinZ, floorMaxZ, modifiedChunks);
                }
            }

            int modifiedCount = modifiedChunks.Count;
            RefreshModifiedChunks(modifiedChunks);

            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return modifiedCount > 0;

        }

        void ClearVoxelsAboveInChunk (VoxelChunk chunk, double floorY, double floorMinX, double floorMaxX, double floorMinZ, double floorMaxZ, List<VoxelChunk> modifiedChunks) {
            if (chunk == null || !chunk.HasContent()) return;

            // Calculate floor Y level within chunk
            double chunkMinY = chunk.position.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            int floorYInChunk = (int)(floorY - chunkMinY);
            
            if (floorYInChunk < 0) floorYInChunk = -1; // Floor is below chunk
            if (floorYInChunk >= VoxelPlayEnvironment.CHUNK_SIZE) return; // Floor is above chunk

            // Convert world coordinates to local chunk coordinates
            double chunkMinX = chunk.position.x - VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            double chunkMinZ = chunk.position.z - VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            int startX = (int)(floorMinX - chunkMinX);
            int endX = (int)(floorMaxX - chunkMinX);
            int startZ = (int)(floorMinZ - chunkMinZ);
            int endZ = (int)(floorMaxZ - chunkMinZ);

            // Clamp to chunk bounds
            if (startX < 0) startX = 0;
            if (endX >= VoxelPlayEnvironment.CHUNK_SIZE) endX = VoxelPlayEnvironment.CHUNK_SIZE - 1;
            if (startZ < 0) startZ = 0;
            if (endZ >= VoxelPlayEnvironment.CHUNK_SIZE) endZ = VoxelPlayEnvironment.CHUNK_SIZE - 1;

            // Clear from floorY+1 to top of chunk
            int startY = floorYInChunk + 1;
            int endY = VoxelPlayEnvironment.CHUNK_SIZE - 1;
            
            if (startY <= endY && startX <= endX && startZ <= endZ) {
                undoManager.SaveChunk(chunk);
                chunk.ClearVoxels(startY, endY, startX, endX, startZ, endZ, VoxelPlayEnvironment.FULL_LIGHT);
                if (!modifiedChunks.Contains(chunk)) {
                    modifiedChunks.Add(chunk);
                }
            }

            // Recursively clear chunk.top if it exists
            if (chunk.top != null) {
                ClearVoxelsAboveInChunk(chunk.top, floorY, floorMinX, floorMaxX, floorMinZ, floorMaxZ, modifiedChunks);
            }
        }

    }

}