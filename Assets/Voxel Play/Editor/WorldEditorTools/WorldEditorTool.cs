using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoxelPlay {

    public enum WorldEditorToolCategory {
        StructTool,
        TerrainTool,
        SculptTool
    }

    public abstract class WorldEditorTool {

        public abstract WorldEditorToolCategory category { get; }
        public abstract Texture2D icon { get; }
        public abstract string title { get; }
        public virtual string instructions => null;
        public abstract int priority { get; }
        public virtual int minOpaque => VoxelPlayEnvironment.FULL_OPAQUE;
        public virtual bool supportsContinuousMode => true;
        public virtual bool supportsMicroVoxels => false;
        public virtual bool canIgnoreWater => false;
        public virtual bool showRecentVoxels => false;
        public virtual void ExitSceneView () { }
        public abstract void SelectVoxels (ref VoxelHitInfo hitInfo, int radius, List<VoxelIndex> voxelIndices);
        protected abstract bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices);

        protected VoxelPlayEnvironment env;
        protected Color32[] mask;
        protected int maskWidth, maskHeight;
        protected VoxelHitInfo startHitInfo;
        protected float startTime;
        protected HashSet<int> terrainVoxelDefinitions;
        protected float lastExecutionTime;
        protected int executionCount;
        protected bool shift, control, keyR, isMouseDown, alt;
        protected float mouseWheel;
        protected UndoManager undoManager;

        Texture2D currentMask;
        protected const float ROUNDNESS = 0.2f;

        public WorldEditorTool () {
            env = VoxelPlayEnvironment.instance;
        }

        public virtual void DrawInspector () {
            env.sceneEditorBrushSize = EditorGUILayout.IntSlider("Brush Size", env.sceneEditorBrushSize, 1, 32);
            EditorGUI.BeginChangeCheck();
            env.sceneEditorBrushShape = (Texture2D)EditorGUILayout.ObjectField("Brush Shape", env.sceneEditorBrushShape, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck()) {
                TextureTools.EnsureTextureReadable(env.sceneEditorBrushShape);
            }
            env.sceneEditorBrushStrength = EditorGUILayout.Slider("Shape Blend Strength", env.sceneEditorBrushStrength, 0.001f, 1f);
            if (supportsContinuousMode) {
                env.sceneEditorBrushContinuousMode = EditorGUILayout.Toggle(new GUIContent("Continuous Mode", "Hold left button mouse to operate"), env.sceneEditorBrushContinuousMode);
                if (env.sceneEditorBrushContinuousMode) {
                    EditorGUI.indentLevel++;
                    env.sceneEditorBrushSpeed = EditorGUILayout.Slider("Speed", env.sceneEditorBrushSpeed, 0, 1);
                    EditorGUI.indentLevel--;
                }
            }
        }

        /// <summary>
        /// Draws the recent voxels grid if enabled
        /// </summary>
        protected void DrawRecentVoxelsGrid () {
            if (!showRecentVoxels) return;

            if (VoxelPlayEnvironmentEditor.currentEditingEnv == null) return;

            List<VoxelDefinition> recentVoxels = VoxelPlayEnvironmentEditor.currentEditingEnv.GetRecentVoxels(env);
            if (recentVoxels.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recent Voxels", EditorStyles.boldLabel);

            // Calculate grid dimensions
            int columns = 4;
            int rows = Mathf.CeilToInt(recentVoxels.Count / (float)columns);

            // Create content for each voxel
            GUIContent[] contents = new GUIContent[recentVoxels.Count];
            for (int i = 0; i < recentVoxels.Count; i++) {
                VoxelDefinition vd = recentVoxels[i];
                Texture2D icon = null;

                // Try to get the icon
                if (vd.icon != null) {
                    icon = vd.icon;
                } else if (vd.textureSample != null) {
                    icon = vd.textureSample;
                } else if (vd.textureSide != null) {
                    icon = vd.textureSide;
                } else if (vd.textureTop != null) {
                    icon = vd.textureTop;
                }

                contents[i] = new GUIContent(icon, vd.name);
            }

            // Draw the grid
            int selectedIndex = GUILayout.SelectionGrid(-1, contents, columns, GUILayout.Height(rows * 70));
            if (selectedIndex >= 0 && selectedIndex < recentVoxels.Count) {
                // Set the selected voxel
                if (category == WorldEditorToolCategory.StructTool || category == WorldEditorToolCategory.SculptTool) {
                    env.sceneEditorBuildVoxel = recentVoxels[selectedIndex];
                }
            }

            EditorGUILayout.Space();
        }

        public void SetTerrainVoxelDefinitions (HashSet<int> terrainVoxelDefinitions) {
            this.terrainVoxelDefinitions = terrainVoxelDefinitions;
        }

        public void SetUndoManager (UndoManager undoManager) {
            this.undoManager = undoManager;
        }

        public void SetControlKeys (bool shift, bool control, bool alt, bool keyR, bool isMouseDown, float mouseWheel) {
            this.shift = shift;
            this.control = control;
            this.alt = alt;
            this.keyR = keyR;
            this.isMouseDown = isMouseDown;
            this.mouseWheel = mouseWheel;
        }

        public virtual void DrawLabel (VoxelHitInfo hitInfo) {
            float labelOffset = supportsMicroVoxels && env.sceneEditorBrushMicroVoxelSize > 0 ? 0.1f : 0.5f;
            string label = hitInfo.point.ToString("F2") + " Rot: " + hitInfo.voxel.GetTextureRotationDegrees();
            if (hitInfo.voxel.type != null) {
                label += " " + hitInfo.voxel.type.name;
            }
            Handles.Label(hitInfo.point + new Vector3(labelOffset, labelOffset, labelOffset), label);
        }


        public virtual void DrawGizmos (VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
        }

        public virtual void DrawPersistentGizmos () {
            // Override this method to draw gizmos that should always be visible,
            // even when the mouse is outside the scene view
        }

        public virtual void Update () {
        }

        protected void DrawArrow (Vector3 start, Vector3 direction, float arrowLength) {
            if (direction.sqrMagnitude == 0) return;
            Vector3 end = start + direction * arrowLength;
            Handles.DrawLine(start, end);
            Handles.ConeHandleCap(0, end, Quaternion.LookRotation(direction), arrowLength * 0.3f, EventType.Repaint);
        }

        public void SetMask (Texture2D maskTexture) {
            if (currentMask == maskTexture) return;
            currentMask = maskTexture;

            if (maskTexture == null) {
                maskWidth = maskHeight = 0;
                return;
            }
            mask = maskTexture.GetPixels32();
            maskWidth = maskTexture.width;
            maskHeight = maskTexture.height;
        }

        public virtual bool RayCast (Ray ray, out VoxelHitInfo hitInfo) {
            return RayCast(ray, out hitInfo, minOpaque);
        }

        public virtual bool RayCast (Ray ray, out VoxelHitInfo hitInfo, int minOpaque) {
            if (supportsMicroVoxels && env.sceneEditorBrushMicroVoxelSize > 0) {
                return env.RayCast(ray, out hitInfo, microVoxels: true, ignoreWater: canIgnoreWater ? IgnoreWaterOption.IgnoreWater : IgnoreWaterOption.IncludeWater, minOpaque: minOpaque);
            }
            return env.RayCast(ray, out hitInfo, minOpaque: minOpaque, ignoreWater: env.sceneEditorBuildIgnoreWater && canIgnoreWater ? IgnoreWaterOption.IgnoreWater : IgnoreWaterOption.IncludeWater);
        }

        public virtual void HighlightVoxels (ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {
            env.VoxelHighlight(voxelIndices, color, edgeWidth, fadeAmplitude);
        }

        /// <summary>
        /// Triggered when user selects another tool
        /// </summary>
        public virtual void SwitchTool () { }

        /// <summary>
        /// Triggered when the inspector is fully destroyed
        /// </summary>
        public virtual void Dispose () { }

        public virtual void StartExecution (VoxelHitInfo hitInfo) {
            startHitInfo = hitInfo;
            startTime = Time.time;
            lastExecutionTime = 0;
            executionCount = 0;
        }

        public virtual void EndExecution () {
        }


        public bool ExecuteTool (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            float now = Time.time;
            if (supportsContinuousMode && env.sceneEditorBrushContinuousMode) {
                if (1f - env.sceneEditorBrushSpeed > (now - lastExecutionTime)) return false;
                lastExecutionTime = now;
                if (executionCount == 0) {
                    lastExecutionTime += 0.1f; // small delay in first click
                }
            } else {
                if (executionCount > 0) return false;
            }
            executionCount++;

            bool enableDetailGenerators = env.enableDetailGenerators; // disable detail generators during brush execution
            env.enableDetailGenerators = false;
            bool result = Execute(ref hitInfo, brushSize, brushStrength, indices);
            env.enableDetailGenerators = enableDetailGenerators;

            return result;
        }

        protected float ComputeMaskFactor (Vector3d center, Vector3d pos, float brushSize) {
            if (maskWidth == 0) return 1f;

            if (brushSize < 1) brushSize = 1f;

            float dx = (float)(pos.x - center.x) / brushSize;
            float dz = (float)(pos.z - center.z) / brushSize;
            dx = dx * 0.5f + 0.5f;
            dz = dz * 0.5f + 0.5f;

            int tw = (int)(dx * maskWidth);
            int th = (int)(dz * maskHeight);

            if (tw < 0) tw = 0; else if (tw >= maskWidth) tw = maskWidth - 1;
            if (th < 0) th = 0; else if (th >= maskHeight) th = maskHeight - 1;

            return mask[th * maskWidth + tw].a / 255f;
        }

        /// <summary>
        /// Computes the mask factor at coordinates inside a stamp of given size
        /// </summary>
        protected float ComputeMaskFactor (int pz, int px, int size) {
            if (maskWidth == 0) return 1f;
            if (size <= 0) return 1f;
            int th = (int)((pz + 0.5f) * maskHeight / size);
            int tw = (int)((px + 0.5f) * maskWidth / size);
            if (th < 0) th = 0; else if (th >= maskHeight) th = maskHeight - 1;
            if (tw < 0) tw = 0; else if (tw >= maskWidth) tw = maskWidth - 1;
            return mask[th * maskWidth + tw].a / 255f;
        }

        protected void UpdateChunkElevation (VoxelChunk chunk) {
            if (chunk.terrainInfo != null) return;
            Vector3d pos = chunk.position;
            pos.x -= VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            pos.z -= VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            if (chunk.modified) {
                // Use terrain voxel definitions mask to avoid trees influencing ground level when computing real height
                env.GetHeightMapInfoReal(pos.x, pos.z, out chunk.terrainInfo, terrainVoxelDefinitions);
            } else {
                env.GetHeightMapInfoFast(pos.x, pos.z, out chunk.terrainInfo);
            }
        }


        protected void RefreshModifiedChunks (List<VoxelChunk> modifiedChunks) {
            VoxelChunk lastChunk = null;
            foreach (var chunk in modifiedChunks) {
                if (lastChunk == chunk) continue;
                env.ChunkRedraw(chunk, includeNeighbours: true, refreshLightmap: true, refreshMesh: true);
                lastChunk = chunk;
            }
        }

        /// <summary>
        /// Gets the Scene View camera position. Returns Vector3.zero if no active SceneView is available.
        /// </summary>
        public static Vector3 GetSceneViewCameraPosition () {
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null) {
                return SceneView.lastActiveSceneView.camera.transform.position;
            }
            return Vector3.zero;
        }
    }
}