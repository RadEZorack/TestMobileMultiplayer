using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.Rendering;

namespace VoxelPlay {

    public class WorldEditorToolBuild : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolBuild");
        public override string title => "Add voxels";
        public override string instructions => "Shift: remove a voxel. Control: select voxel type from scene.";
        public override int priority => 50;
        public override int minOpaque => 0;
        public override bool supportsMicroVoxels => true;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;
        public override bool canIgnoreWater => true;
        public override bool showRecentVoxels => true;

        static class ShaderParams {
            public static int MicroVoxelsSize = Shader.PropertyToID("_MicroVoxelsSize");
        }


        Editor meshPreviewEditor;
        GameObject voxelPreview;

        public override void Dispose () {
            try {
                DestroyMeshPreviewEditor();
                DestroyVoxelPreview();
            }
            catch { }
        }

        public override void SwitchTool () {
            DestroyMeshPreviewEditor();
            DestroyVoxelPreview();
        }

        void DestroyMeshPreviewEditor () {
            if (meshPreviewEditor != null) {
                UnityEngine.Object.DestroyImmediate(meshPreviewEditor);
                meshPreviewEditor = null;
            }
        }

        void DestroyVoxelPreview () {
            if (voxelPreview != null) {
                UnityEngine.Object.DestroyImmediate(voxelPreview);
                voxelPreview = null;
            }
        }


        public override void DrawInspector () {
            DestroyVoxelPreview();
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
            if (env.sceneEditorBrushMicroVoxelSize == 0 && env.sceneEditorBuildVoxel != null) {
                if (env.sceneEditorBuildVoxel.usesMicroVoxels) {
                    Mesh mesh = env.sceneEditorBuildVoxel.GetMicroVoxelsMesh();
                    if (mesh != null) {
                        if (meshPreviewEditor != null && meshPreviewEditor.target != mesh) {
                            DestroyMeshPreviewEditor();
                        }
                        if (meshPreviewEditor == null) {
                            meshPreviewEditor = Editor.CreateEditor(mesh);
                        }
                        meshPreviewEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(64, 64), new GUIStyle());
                    }
                } else if (meshPreviewEditor != null) {
                    DestroyMeshPreviewEditor();
                }
            } else if (meshPreviewEditor != null) {
                DestroyMeshPreviewEditor();
            }
            env.sceneEditorBrushMicroVoxelSize = EditorGUILayout.IntSlider("MicroVoxel Size", env.sceneEditorBrushMicroVoxelSize, 0, MicroVoxels.COUNT_PER_AXIS);

            if (env.sceneEditorBrushMicroVoxelSize == 0) {
                DestroyVoxelPreview();
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
            }

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
            if (!isMouseDown) {
                if (shift) {
                    if (supportsMicroVoxels && env.sceneEditorBrushMicroVoxelSize > 0 && hitInfo.voxel.type.supportsMicroVoxels) {
                        Boundsd bounds = env.GetHighlightBounds(ref hitInfo, env.sceneEditorBrushMicroVoxelSize);
                        float size = (float)bounds.size.x;
                        DrawArrow(bounds.center + hitInfo.normal * size * 1.5f, -hitInfo.normal, size);
                    } else if (hitInfo.voxel.isSolid) {
                        DrawArrow(hitInfo.center + hitInfo.normal * 0.8f, -hitInfo.normal, 0.3f);
                    }
                } else {
                    if (supportsMicroVoxels && env.sceneEditorBrushMicroVoxelSize > 0 && hitInfo.voxel.type.supportsMicroVoxels) {
                        Boundsd bounds = env.GetHighlightBounds(ref hitInfo, env.sceneEditorBrushMicroVoxelSize);
                        float size = (float)bounds.size.x;
                        DrawArrow(bounds.center + hitInfo.normal * size * 0.5f, hitInfo.normal, size);
                    } else if (hitInfo.voxel.isSolid) {
                        DrawArrow(hitInfo.center + hitInfo.normal * 0.5f, hitInfo.normal, 0.3f);
                    }
                }
            }

            if (env.sceneEditorBrushMicroVoxelSize > 0) {
                // draw cube around voxel containing the micro voxels
                var originalZTest = Handles.zTest;
                Handles.zTest = CompareFunction.LessEqual;
                Handles.color = new Color(1, 1, 1, 0.35f);
                Vector3d center = hitInfo.point;
                if (!env.IsMicroVoxelAtPosition(center)) {
                    center -= hitInfo.normal * 0.5f;
                }
                FastVector.Middling(ref center);
                const float SIZE = 1.01f;
                Handles.DrawWireCube(center, new Vector3(SIZE, SIZE, SIZE));
                Handles.zTest = originalZTest;
            } else if (env.sceneEditorBuildVoxel != null) {
                // Show voxel preview with microvoxels
                if (env.sceneEditorBuildVoxel.usesMicroVoxels) {
                    if (hitInfo.voxel.isSolid) {
                        voxelPreview = MicroVoxelsHighlight(env.sceneEditorBuildVoxel, hitInfo.voxelCenter + hitInfo.normal);
                    } else {
                        voxelPreview = MicroVoxelsHighlight(env.sceneEditorBuildVoxel, hitInfo.voxelCenter);
                    }
                    voxelPreview.transform.localRotation = Quaternion.Euler(0, env.sceneEditorPlacementRotation * 90, 0);
                }
            }
        }




        /// <summary>
        /// Shows a temporary hologram of a voxel definition with microvoxels at a given position. Used by the world editor tools.
        /// </summary>
        /// <returns>The highlight.</returns>
        /// <param name="vd">Voxel definition.</param>
        /// <param name="position">Position.</param>
        GameObject MicroVoxelsHighlight (VoxelDefinition vd, Vector3d position) {
            if (vd == null) return null;

            if (vd.microVoxelsPreviewGO == null) {
                vd.microVoxelsPreviewGO = Resources.Load<GameObject>("VoxelPlay/Prefabs/MicroVoxelsPreview");
                if (vd.microVoxelsPreviewGO == null) return null;
                vd.microVoxelsPreviewGO = UnityEngine.Object.Instantiate(vd.microVoxelsPreviewGO);
                vd.microVoxelsPreviewGO.name = "MicroVoxelsPreview";
            }
            GameObject previewGO = vd.microVoxelsPreviewGO;
            if (previewGO.TryGetComponent(out MeshFilter meshFilter)) {
                meshFilter.mesh = vd.GetMicroVoxelsMesh();
            }
            if (previewGO.TryGetComponent(out MeshRenderer meshRenderer)) {
                meshRenderer.sharedMaterial.SetFloat(ShaderParams.MicroVoxelsSize, MicroVoxels.COUNT_PER_AXIS);
            }
            previewGO.transform.SetParent(env.worldRoot, false);
            previewGO.transform.localPosition = position.vector3;
            previewGO.SetActive(true);

            return previewGO;
        }


        public override bool RayCast (Ray ray, out VoxelHitInfo hitInfo) {
            bool res = base.RayCast(ray, out hitInfo);
            // in continous mode, make it eaiser to build rows of voxels over the same surface by comparing the new normal with the start hit normal
            if (env.sceneEditorBrushContinuousMode && isMouseDown && executionCount > 0 && hitInfo.normal != startHitInfo.normal && !hitInfo.voxel.type.isVegetation) {
                hitInfo = startHitInfo;
                return false;
            }
            return res;
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

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {

            voxelIndices.Clear();

            if (env.sceneEditorBrushMicroVoxelSize > 0) return;

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

                if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: !shift)) {
                    if (shift) {
                        if (chunk != null && chunk.voxels[voxelIndex].typeIndex > 0) {
                            VoxelIndex vi = new VoxelIndex();
                            vi.chunk = chunk;
                            vi.voxelIndex = voxelIndex;
                            voxelIndices.Add(vi);
                        }
                    } else {
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
        }

        public override void HighlightVoxels (ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {
            VoxelHitInfo highlightHitInfo = hitInfo;
            if (env.sceneEditorBrushContinuousMode && isMouseDown) {
                highlightHitInfo = startHitInfo;
            }
            if (env.sceneEditorBrushMicroVoxelSize == 0 || voxelIndices.Count > 1) {
                base.HighlightVoxels(ref highlightHitInfo, voxelIndices, color, edgeWidth, fadeAmplitude);
                return;
            }
            env.VoxelHighlight(ref highlightHitInfo, color, edgeWidth, env.sceneEditorBrushMicroVoxelSize, fadeAmplitude);
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            // If control key is pressed, select the voxel type from the hit info but don't build anything
            if (control && !hitInfo.voxel.isEmpty) {
                env.sceneEditorBuildVoxel = hitInfo.voxel.type;
                // Add the selected voxel to recent voxels
                VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(hitInfo.voxel.type);
                // Return false to indicate no chunks were modified
                return false;
            }

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            int modifiedCount;

            if (hitInfo.voxel.type.isVegetation) {
                undoManager.SaveChunk(hitInfo.chunk);
                hitInfo.chunk.ClearVoxel(hitInfo.voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                modifiedChunks.Add(hitInfo.chunk);
                modifiedCount = modifiedChunks.Count;
                VoxelPlayEnvironmentEditor.currentEditingEnv.RefreshSelection();
            } else {

                if (env.sceneEditorBrushMicroVoxelSize > 0) {
                    if (shift) {
                        undoManager.SaveChunk(hitInfo.chunk);
                        if (!hitInfo.voxel.type.supportsMicroVoxels) {
                            hitInfo.chunk.ClearVoxel(hitInfo.voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                            modifiedChunks.Add(hitInfo.chunk);
                        } else {
                            if (env.MicroVoxelDestroy(ref hitInfo, env.sceneEditorBrushMicroVoxelSize)) {
                                modifiedChunks.Add(hitInfo.chunk);
                            }
                        }
                    } else {
                        undoManager.SaveChunk(hitInfo.chunk);
                        VoxelDefinition vd = env.sceneEditorBuildVoxel == null ? hitInfo.voxel.type : env.sceneEditorBuildVoxel;
                        if (env.MicroVoxelPlace(ref hitInfo, env.sceneEditorBrushMicroVoxelSize, vd, probability: 1f, hitInfo.voxel.color, hitInfo.voxel.GetTextureRotation())) {
                            modifiedChunks.Add(hitInfo.chunk);
                        }
                    }
                    modifiedCount = modifiedChunks.Count;
                    float size = MicroVoxels.SIZE;
                    if (env.microVoxelsSnap) {
                        size *= env.sceneEditorBrushMicroVoxelSize;
                    }
                    if (modifiedCount > 0 && IsWithinDistance(hitInfo.voxelCenter, (env.sceneEditorBuildMaxLength - 1) * size)) {
                        if (shift) {
                            hitInfo.voxelCenter -= startHitInfo.normal * size;
                        } else {
                            hitInfo.voxelCenter += startHitInfo.normal * size;
                        }
                    }

                } else {
                    int count = indices.Count;
                    bool placedMicroVoxels = false;
                    for (int k = 0; k < count; k++) {
                        VoxelIndex vi = indices[k];
                        Vector3d pos = env.GetVoxelPosition(vi.chunk, vi.voxelIndex);
                        if (shift) {
                            undoManager.SaveChunk(vi.chunk);
                            vi.chunk.ClearVoxel(vi.voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                            modifiedChunks.Add(vi.chunk);
                        } else {
                            bool containsMicroVoxels = env.IsMicroVoxelAtPosition(pos, out MicroVoxels mv) && !mv.isFull;
                            if (startHitInfo.normal.y < 1 || !containsMicroVoxels) {
                                pos += startHitInfo.normal;
                            }
                            if (env.GetVoxelIndex(pos, out VoxelChunk otherChunk, out _, createChunkIfNotExists: true)) {
                                VoxelDefinition vd = env.sceneEditorBuildVoxel == null ? vi.chunk.voxels[vi.voxelIndex].type : env.sceneEditorBuildVoxel;
                                if (vd != null) {
                                    if (vd.index <= 0) {
                                        env.world.AddVoxelDefinition(vd);
                                        env.ReloadTextures();
                                        EditorUtility.SetDirty(env.world);
                                    }
                                    undoManager.SaveChunk(otherChunk);

                                    if (containsMicroVoxels) {
                                        env.MicroVoxelPlaceSlab(pos, vd, Misc.colorWhite, rotation: env.sceneEditorPlacementRotation, topHalf: true);
                                        placedMicroVoxels = true;
                                    } else {
                                        env.VoxelPlace(pos, vd, playSound: false, Misc.colorWhite, rotation: env.sceneEditorPlacementRotation);
                                        placedMicroVoxels = vd.usesMicroVoxels;
                                    }
                                    modifiedChunks.Add(otherChunk);
                                }
                            }
                        }
                    }
                    modifiedCount = modifiedChunks.Count;
                    if (modifiedCount > 0 && !placedMicroVoxels) {
                        if (shift) {
                            hitInfo.voxelCenter -= startHitInfo.normal;
                        } else {
                            hitInfo.voxelCenter += startHitInfo.normal;
                        }
                    }
                }
            }

            RefreshModifiedChunks(modifiedChunks);

            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return modifiedCount > 0;

        }

    }

}