using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;


namespace VoxelPlay {

    /// <summary>
    /// Manages a collection of recently used voxel definitions for the world editor tools
    /// </summary>
    [Serializable]
    public class RecentVoxelDefinitions {

        const int MAX_RECENT_VOXELS = 16;
        const string PLAYERPREFS_KEY = "VoxelPlay_RecentVoxels";

        // List of recently used voxel definitions (most recent first)
        List<string> recentVoxelNames = new List<string>();

        /// <summary>
        /// Adds a voxel definition to the recent list. If it already exists, moves it to the front.
        /// </summary>
        public void AddVoxel (VoxelDefinition voxelDefinition) {
            if (voxelDefinition == null)
                return;

            // Remove if already exists
            string voxelName = voxelDefinition.name;
            int index = recentVoxelNames.IndexOf(voxelName);
            if (index >= 0) {
                recentVoxelNames.RemoveAt(index);
            }

            // Add to front
            recentVoxelNames.Insert(0, voxelName);

            // Trim if needed
            while (recentVoxelNames.Count > MAX_RECENT_VOXELS) {
                recentVoxelNames.RemoveAt(recentVoxelNames.Count - 1);
            }

            // Save to PlayerPrefs
            SaveToPlayerPrefs();
        }

        /// <summary>
        /// Gets the list of recent voxel definitions
        /// </summary>
        public List<VoxelDefinition> GetRecentVoxels (VoxelPlayEnvironment env) {
            List<VoxelDefinition> result = new List<VoxelDefinition>();
            foreach (string voxelName in recentVoxelNames) {
                VoxelDefinition vd = env.GetVoxelDefinition(voxelName);
                if (vd != null) {
                    result.Add(vd);
                }
            }
            return result;
        }

        /// <summary>
        /// Saves the recent voxel definitions to PlayerPrefs
        /// </summary>
        private void SaveToPlayerPrefs () {
            string serializedData = string.Join("|", recentVoxelNames);
            PlayerPrefs.SetString(PLAYERPREFS_KEY, serializedData);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads the recent voxel definitions from PlayerPrefs
        /// </summary>
        public void LoadFromPlayerPrefs () {
            recentVoxelNames.Clear();

            if (PlayerPrefs.HasKey(PLAYERPREFS_KEY)) {
                string serializedData = PlayerPrefs.GetString(PLAYERPREFS_KEY);
                if (!string.IsNullOrEmpty(serializedData)) {
                    string[] names = serializedData.Split('|');
                    foreach (string name in names) {
                        if (!string.IsNullOrEmpty(name)) {
                            recentVoxelNames.Add(name);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Clears the recent voxel definitions
        /// </summary>
        public void Clear () {
            recentVoxelNames.Clear();
            PlayerPrefs.DeleteKey(PLAYERPREFS_KEY);
        }
    }

    public partial class VoxelPlayEnvironmentEditor : Editor {

        readonly List<VoxelIndex> voxelIndices = new List<VoxelIndex>();
        VoxelHitInfo lastHighlightInfo;
        static bool isMouseDown;
        int sceneEditorHighlightedLastFrame, sceneEditorExecutionLastFrame;
        readonly List<Type> toolTypes = new List<Type>();
        static Type selectedToolType, lastToolType;
        Vector2 lastMousePosition;

        readonly Dictionary<Type, WorldEditorTool> toolInstances = new Dictionary<Type, WorldEditorTool>();
        readonly HashSet<int> terrainVoxels = new HashSet<int>();

        VoxelPlayTerrainGenerator currentTerrainGenerator;

        UndoManager undoManager;

        public static VoxelPlayEnvironmentEditor currentEditingEnv;
        bool pendingChanges;

        // Recent voxel definitions used in the world editor tools
        RecentVoxelDefinitions recentVoxelDefinitions = new RecentVoxelDefinitions();

        // Modified chunks list
        List<VoxelChunk> modifiedChunksList = new List<VoxelChunk>();
        Vector2 modifiedChunksScrollPosition;
        int modifiedChunksSortMode = 0; // 0 = Nearest to Camera, 1 = Recently Modified (default: Nearest)
        int lastSelectedToolTab = -1;
        bool showModifiedChunksSection = true;
        const int MAX_VISIBLE_MODIFIED_CHUNKS = 10;
        const string PLAYERPREFS_KEY_SHOW_MODIFIED_CHUNKS = "VoxelPlay_ShowModifiedChunks";
        const string PLAYERPREFS_KEY_MODIFIED_CHUNKS_SORT_MODE = "VoxelPlay_ModifiedChunksSortMode";

        // Focused chunk wireframe
        VoxelChunk focusedChunk;
        bool showAllModifiedChunks;
        bool raypickMode;
        const float ICON_SIZE = 40;

        void WorldEditorInit () {
            // Initialize recent voxel definitions
            recentVoxelDefinitions.LoadFromPlayerPrefs();

            // Load modified chunks section visibility preference
            showModifiedChunksSection = PlayerPrefs.GetInt(PLAYERPREFS_KEY_SHOW_MODIFIED_CHUNKS, 1) != 0;
            // Automatically enable raypick mode when showModifiedChunksSection is enabled
            raypickMode = showModifiedChunksSection;

            // Load sort mode preference (default: 0 = Nearest)
            modifiedChunksSortMode = PlayerPrefs.GetInt(PLAYERPREFS_KEY_MODIFIED_CHUNKS_SORT_MODE, 0);

            if (undoManager == null) {
                undoManager = CreateInstance<UndoManager>();
            }
            undoManager.env = env;
            Undo.undoRedoPerformed += PerformUndo;

            isMouseDown = false;
            LoadTools();
            if (env.sceneEditorBrushShape == null) {
                env.sceneEditorBrushShape = Resources.Load("VoxelPlay/Brushes/Brush2") as Texture2D;
            }

            // figure out which voxel definitions can be considered as part of terrain
            if (env.initialized) {
                LoadTerrainVoxelDefinitions();
            } else {
                env.OnInitialized += LoadTerrainVoxelDefinitions;
            }

            if (!Application.isPlaying && expandSceneEditor && renderInEditor.boolValue) {
                FocusSceneView();
            }

            currentEditingEnv = this;
            SceneView.duringSceneGui += OnScene;
            EditorSceneManager.sceneSaving += OnSceneSaving;

            // Refresh modified chunks list when inspector loads
            RefreshModifiedChunksList();
        }

        void WorldEditorDispose () {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            SceneView.duringSceneGui -= OnScene;
            currentEditingEnv = null;

            foreach (var tool in toolInstances.Values) {
                if (tool != null) {
                    tool.Dispose();
                }
            }
            if (undoManager != null) {
                Undo.undoRedoPerformed -= PerformUndo;
                DestroyImmediate(undoManager);
            }
        }

        void OnSceneSaving (Scene scene, string path) {
            if (env == null || !env.runInEditMode) return;
            if (pendingChanges) {
                SaveWorldInEditor();
            }
        }

        void PerformUndo () {
            if (undoManager != null) {
                undoManager.PerformUndo();
            }
            lastMousePosition = Vector2.zero;
            pendingChanges = true;
        }

        void LoadTerrainVoxelDefinitions () {
            if (env.world == null) return;
            currentTerrainGenerator = env.world.terrainGenerator;
            if (env.world.terrainGenerator == null) return;

            List<VoxelDefinition> vds = new List<VoxelDefinition>();
            env.world.terrainGenerator.GetTerrainVoxelDefinitions(vds);
            foreach (VoxelDefinition vd in vds) {
                if (vd != null && !terrainVoxels.Contains(vd.index)) terrainVoxels.Add(vd.index);
            }
        }

        void LoadTools () {
            toolInstances.Clear();

            var types = TypeCache.GetTypesDerivedFrom<WorldEditorTool>();
            foreach (var type in types) {
                if (!type.IsAbstract) {
                    var instance = Activator.CreateInstance(type) as WorldEditorTool;
                    toolInstances[type] = instance;
                }
            }

            // Crear una lista temporal de las instancias
            var sortedInstances = new List<WorldEditorTool>(toolInstances.Values);

            // Ordenar las instancias por prioridad
            sortedInstances.Sort((t1, t2) => t1.priority.CompareTo(t2.priority));

            // Limpiar y rellenar toolTypes con el orden correcto
            toolTypes.Clear();
            foreach (var instance in sortedInstances) {
                toolTypes.Add(instance.GetType());
            }

        }

        void DrawTools (WorldEditorToolCategory category) {

            GUIStyle activeStyle = new GUIStyle(GUI.skin.button);
            GUIStyle disabledStyle = new GUIStyle(GUI.skin.button);
            disabledStyle.normal.background = Texture2D.blackTexture;

            EditorGUILayout.BeginHorizontal();

            var active = selectedToolType == null;
            if (GUILayout.Toggle(active, new GUIContent("X", "Deselect tool"), active ? activeStyle : disabledStyle, GUILayout.Width(ICON_SIZE), GUILayout.Height(ICON_SIZE))) {
                selectedToolType = null;
            }

            int toolsPerRow = (int)((EditorGUIUtility.currentViewWidth - 24) / (ICON_SIZE + 4));

            int count = toolTypes.Count;
            int drawn = 1;
            for (int k = 0; k < count; k++) {
                if (drawn % toolsPerRow == 0) {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
                var type = toolTypes[k];
                var tool = toolInstances[type];
                if (tool.category != category) continue;
                var content = new GUIContent(tool.icon, string.IsNullOrEmpty(tool.instructions) ? tool.title : tool.title + "\n" + tool.instructions);
                active = selectedToolType == type;
                if (GUILayout.Toggle(active, content, active ? activeStyle : disabledStyle, GUILayout.Width(ICON_SIZE), GUILayout.Height(ICON_SIZE))) {
                    selectedToolType = type;
                }
                drawn++;
            }
            EditorGUILayout.EndHorizontal();

            if (selectedToolType != null) {
                var tool = toolInstances[selectedToolType];
                GUILayout.Label(tool.title, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(tool.instructions)) {
                    EditorGUILayout.HelpBox(tool.instructions, MessageType.Info);
                }
                tool.DrawInspector();
            }

            if (selectedToolType != lastToolType) {
                if (lastToolType != null) {
                    toolInstances[lastToolType].SwitchTool();
                }
                EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
            }
            lastToolType = selectedToolType;
        }

        public static void OnScene (SceneView sceneView) {
            currentEditingEnv?.OnSceneEditor();
            // Draw focused chunk wireframe when world editor is expanded and modified chunks section is shown
            if (currentEditingEnv != null && expandSceneEditor && currentEditingEnv.showModifiedChunksSection) {
                currentEditingEnv.DrawFocusedChunkWireframe();
            }
        }

        void OnSceneEditor () {

            if (!expandSceneEditor || !renderInEditor.boolValue) return;
            if (env == null || !env.initialized || env.world == null) return;
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            bool shift = (e.modifiers & EventModifiers.Shift) != 0;
            bool control = (e.modifiers & EventModifiers.Control) != 0;
            bool alt = (e.modifiers & EventModifiers.Alt) != 0;
            bool keyR = e.type == EventType.KeyDown && e.keyCode == KeyCode.R;
            float mouseWheel = e.type == EventType.ScrollWheel ? e.delta.y : 0f;

            if (keyR) {
                e.Use();
            } else if (mouseWheel != 0) {
                e.Use();
                RefreshSelection();
            }

            // Handle raypick mode when no tool is selected
            if (selectedToolType == null) {
                if (raypickMode) {
                    if (EditorWindow.mouseOverWindow == SceneView.currentDrawingSceneView) {
                        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                        // Only handle click, not hover
                        if (e.isMouse && e.button == 0) {
                            if (e.type == EventType.MouseDown) {
                                HandleRaypickClick(e, mousePos);
                            }
                            // Always capture click to prevent losing VoxelPlayEnvironment selection
                            e.Use();
                        }
                    }
                }
                return;
            }
            var tool = toolInstances[selectedToolType];
            tool.SetControlKeys(shift, control, alt, keyR, isMouseDown, mouseWheel);

            if (EditorWindow.mouseOverWindow != SceneView.currentDrawingSceneView) {
                if (isMouseDown) {
                    tool.EndExecution();
                    undoManager.EndChangeGroup();
                    isMouseDown = false;
                    env.VoxelHighlight(false);
                }
                tool.ExitSceneView();
                // Draw persistent gizmos even when mouse is outside
                tool.DrawPersistentGizmos();
                return;
            } else {
                if (Application.isFocused && EditorWindow.focusedWindow != SceneView.currentDrawingSceneView) {
                    EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
                }
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive)); // prevents default selection

            if (terrainVoxels.Count == 0 || env.world.terrainGenerator != currentTerrainGenerator) {
                LoadTerrainVoxelDefinitions();
            }
            tool.SetTerrainVoxelDefinitions(terrainVoxels);
            tool.SetUndoManager(undoManager);

            if (e.isMouse && e.button == 0) {
                if (e.type == EventType.MouseDown) {
                    // Clear focused chunk when clicking in scene (unless in raypick mode)
                    if (!raypickMode) {
                        focusedChunk = null;
                    }
                    undoManager.StartChangeGroup();
                    tool.StartExecution(lastHighlightInfo);
                    isMouseDown = true;
                } else if (e.type == EventType.MouseDrag && isMouseDown) {

                } else if (e.type == EventType.MouseUp) {
                    tool.EndExecution();
                    undoManager.EndChangeGroup();
                    isMouseDown = false;
                }
            }

            tool.Update();

            tool.DrawLabel(lastHighlightInfo);

            tool.DrawGizmos(lastHighlightInfo, voxelIndices);

            // Draw persistent gizmos (always visible)
            tool.DrawPersistentGizmos();

            // Highlight voxels
            int thisFrame = Time.frameCount;
            if (thisFrame != sceneEditorHighlightedLastFrame) {
                sceneEditorHighlightedLastFrame = thisFrame;

                if (e.mousePosition != lastMousePosition) {
                    lastMousePosition = e.mousePosition;
                    Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

                    if (!tool.RayCast(ray, out VoxelHitInfo hitInfo)) {
                        env.VoxelHighlight(false);
                        return;
                    }

                    if (hitInfo.point == lastHighlightInfo.point) return;
                    lastHighlightInfo = hitInfo;
                }

                tool.SetMask(env.sceneEditorBrushShape);
                tool.SelectVoxels(ref lastHighlightInfo, env.sceneEditorBrushSize, voxelIndices);

                Color highlightColor = Color.cyan;
                highlightColor.a = 0.45f;
                tool.HighlightVoxels(ref lastHighlightInfo, voxelIndices, highlightColor, edgeWidth: 2f, fadeAmplitude: 0);
            }

            // Execute tool
            if (thisFrame == sceneEditorExecutionLastFrame) return;
            sceneEditorExecutionLastFrame = thisFrame;

            if (isMouseDown) {
                if (tool.ExecuteTool(ref lastHighlightInfo, env.sceneEditorBrushSize, env.sceneEditorBrushStrength, voxelIndices)) {
                    pendingChanges = true;
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    lastHighlightInfo.point = Vector3d.zero;
                }
            }
        }

        void ClearFocusedChunk () {
            focusedChunk = null;
        }

        void HandleRaypickClick (Event e, Vector2 mousePos) {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            Vector3d rayOrigin = new Vector3d(ray.origin.x, ray.origin.y, ray.origin.z);
            Rayd rayD = new Rayd(rayOrigin, ray.direction);

            Vector3 cameraPos = WorldEditorTool.GetSceneViewCameraPosition();
            Vector3d cameraPosD = new Vector3d(cameraPos.x, cameraPos.y, cameraPos.z);

            VoxelChunk modifiedChunkHit = null;
            double modifiedChunkDistance = double.MaxValue;

            // 1) Iterate through all modified chunks and check which one intersects the ray
            if (modifiedChunksList != null) {
                int count = modifiedChunksList.Count;
                for (int i = 0; i < count; i++) {
                    VoxelChunk chunk = modifiedChunksList[i];
                    if (chunk == null) continue;

                    Boundsd chunkBounds = env.GetChunkBounds(chunk);
                    Bounds bounds = chunkBounds; // Implicit conversion from Boundsd to Bounds

                    if (rayD.Intersects(bounds)) {
                        double distSqr = FastVector.SqrDistanceByValue(cameraPosD, chunk.position);
                        if (distSqr < modifiedChunkDistance) {
                            modifiedChunkDistance = distSqr;
                            modifiedChunkHit = chunk;
                        }
                    }
                }
            }

            // 2) Perform env.RayCast and get the nearest chunk
            VoxelChunk raycastChunkHit = null;
            double raycastChunkDistance = double.MaxValue;

            if (env.RayCast(rayOrigin, ray.direction, out VoxelHitInfo hitInfo, maxDistance: 1000f, createChunksIfNeeded: false)) {
                if (hitInfo.chunk != null) {
                    raycastChunkHit = hitInfo.chunk;
                    raycastChunkDistance = hitInfo.sqrDistance;
                }
            }

            // 3) Pick the nearest one to the sceneview camera
            VoxelChunk selectedChunk = null;
            if (modifiedChunkHit != null && raycastChunkHit != null) {
                // Both hit - pick the closest to camera
                selectedChunk = modifiedChunkDistance < raycastChunkDistance ? modifiedChunkHit : raycastChunkHit;
            } else if (modifiedChunkHit != null) {
                selectedChunk = modifiedChunkHit;
            } else if (raycastChunkHit != null) {
                selectedChunk = raycastChunkHit;
            }

            // 4) Set focused chunk
            if (selectedChunk != null) {
                focusedChunk = selectedChunk;
                showAllModifiedChunks = false;
                // 5) If the chunk is in the modified list, scroll to it
                if (selectedChunk.modified) {
                    ScrollToChunkInList(selectedChunk);
                }
                SceneView.RepaintAll();
            }
        }

        void ScrollToChunkInList (VoxelChunk chunk) {
            if (chunk == null || modifiedChunksList == null) return;

            int index = modifiedChunksList.IndexOf(chunk);
            if (index >= 0) {
                const float ROW_HEIGHT = 20f;
                modifiedChunksScrollPosition.y = index * ROW_HEIGHT;
            }
        }

        void DrawFocusedChunkWireframe () {
            Color originalColor = Handles.color;
            UnityEngine.Rendering.CompareFunction originalZTest = Handles.zTest;

            // Draw occluded parts first (behind objects) with reduced opacity
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
            Color translucentColor = Color.yellow;
            translucentColor.a = 0.2f;

            // If focused chunk exists, draw only that one (takes priority over "Select All")
            if (focusedChunk != null) {
                Handles.color = translucentColor;
                Vector3 center = new Vector3((float)focusedChunk.position.x, (float)focusedChunk.position.y, (float)focusedChunk.position.z);
                Vector3 size = new Vector3(VoxelPlayEnvironment.CHUNK_SIZE, VoxelPlayEnvironment.CHUNK_SIZE, VoxelPlayEnvironment.CHUNK_SIZE);
                Handles.DrawWireCube(center, size);
            } else if (showAllModifiedChunks && modifiedChunksList != null) {
                Handles.color = translucentColor;
                int count = modifiedChunksList.Count;
                Vector3 size = new Vector3(VoxelPlayEnvironment.CHUNK_SIZE, VoxelPlayEnvironment.CHUNK_SIZE, VoxelPlayEnvironment.CHUNK_SIZE);
                for (int i = 0; i < count; i++) {
                    VoxelChunk chunk = modifiedChunksList[i];
                    if (chunk == null) continue;
                    Vector3 center = new Vector3((float)chunk.position.x, (float)chunk.position.y, (float)chunk.position.z);
                    Handles.DrawWireCube(center, size);
                }
            }

            // Draw visible parts (in front of objects) with full opacity
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = Color.yellow;

            if (focusedChunk != null) {
                Vector3 center = new Vector3((float)focusedChunk.position.x, (float)focusedChunk.position.y, (float)focusedChunk.position.z);
                Vector3 size = new Vector3(VoxelPlayEnvironment.CHUNK_SIZE, VoxelPlayEnvironment.CHUNK_SIZE, VoxelPlayEnvironment.CHUNK_SIZE);
                Handles.DrawWireCube(center, size);
            } else if (showAllModifiedChunks && modifiedChunksList != null) {
                int count = modifiedChunksList.Count;
                Vector3 size = new Vector3(VoxelPlayEnvironment.CHUNK_SIZE, VoxelPlayEnvironment.CHUNK_SIZE, VoxelPlayEnvironment.CHUNK_SIZE);
                for (int i = 0; i < count; i++) {
                    VoxelChunk chunk = modifiedChunksList[i];
                    if (chunk == null) continue;
                    Vector3 center = new Vector3((float)chunk.position.x, (float)chunk.position.y, (float)chunk.position.z);
                    Handles.DrawWireCube(center, size);
                }
            }

            Handles.color = originalColor;
            Handles.zTest = originalZTest;
        }

        public static void UnselectWorldEditorTool () {
            selectedToolType = null;
            isMouseDown = false;
        }

        public static void SelectWorldEditorTool (Type toolType) {
            selectedToolType = toolType;
            isMouseDown = false;
        }

        public void RefreshSelection () {
            lastHighlightInfo.point.y = lastHighlightInfo.voxelCenter.y = lastMousePosition.y = sceneEditorHighlightedLastFrame = 9999999;
        }

        void SaveWorldInEditor () {
            if (env.SaveGameBinary(makeBackup: env.sceneEditorAutomaticBackup)) {
                Debug.Log("World saved to " + env.saveFilename);
                AssetDatabase.Refresh();
                pendingChanges = false;
            }
        }

        public void AddRecentVoxel (VoxelDefinition voxelDefinition) {
            if (voxelDefinition != null) {
                recentVoxelDefinitions.AddVoxel(voxelDefinition);
            }
        }

        public List<VoxelDefinition> GetRecentVoxels (VoxelPlayEnvironment env) {
            return recentVoxelDefinitions.GetRecentVoxels(env);
        }

        void RefreshModifiedChunksList () {
            modifiedChunksList.Clear();
            if (env != null && env.initialized) {
                List<VoxelChunk> tempChunks = BufferPool<VoxelChunk>.Get();
                try {
                    env.GetChunks(tempChunks, ChunkModifiedFilter.OnlyModified);
                    modifiedChunksList.AddRange(tempChunks);
                    SortModifiedChunksList();
                }
                finally {
                    BufferPool<VoxelChunk>.Release(tempChunks);
                }
            }
        }

        void SortModifiedChunksList () {
            if (modifiedChunksList.Count == 0) return;

            if (modifiedChunksSortMode == 0) {
                // Sort by nearest to camera
                Vector3 cameraPos = WorldEditorTool.GetSceneViewCameraPosition();
                Vector3d cameraPosD = new Vector3d(cameraPos.x, cameraPos.y, cameraPos.z);
                modifiedChunksList.Sort((a, b) => {
                    double distA = FastVector.SqrDistanceByValue(a.position, cameraPosD);
                    double distB = FastVector.SqrDistanceByValue(b.position, cameraPosD);
                    return distA.CompareTo(distB);
                });
            } else {
                // Sort by recently modified (highest timestamp first)
                modifiedChunksList.Sort((a, b) => b.modifiedTimestamp.CompareTo(a.modifiedTimestamp));
            }
        }

        void FocusCameraOnChunk (VoxelChunk chunk) {
            if (chunk == null || SceneView.lastActiveSceneView == null) return;

            // Toggle: if already focused, unfocus it
            if (focusedChunk == chunk) {
                focusedChunk = null;
                SceneView.RepaintAll();
                return;
            }

            focusedChunk = chunk;
            // Hide "Select All" wireframes when focusing on a chunk
            showAllModifiedChunks = false;
            Vector3 chunkPos = new Vector3((float)chunk.position.x, (float)chunk.position.y, (float)chunk.position.z);
            SceneView.lastActiveSceneView.LookAt(chunkPos);
            SceneView.RepaintAll();
        }

        void ResetChunk (VoxelChunk chunk) {
            if (chunk == null || env == null) return;
            
            // Unfocus if this chunk is focused
            if (focusedChunk == chunk) {
                focusedChunk = null;
            }

            // Register with Unity's Undo system
            undoManager.StartChangeGroup();
            undoManager.SaveChunk(chunk);
            env.ChunkReset(chunk);
            undoManager.EndChangeGroup();

            pendingChanges = true;
            RefreshModifiedChunksList();
        }

        void DrawModifiedChunksSection () {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            bool newShowState = EditorGUILayout.ToggleLeft("Show Modified Chunks", showModifiedChunksSection, EditorStyles.boldLabel);
            if (newShowState != showModifiedChunksSection) {
                showModifiedChunksSection = newShowState;
                // Automatically enable/disable raypick mode based on showModifiedChunksSection
                raypickMode = showModifiedChunksSection;
                if (!raypickMode) {
                    focusedChunk = null;
                }
                PlayerPrefs.SetInt(PLAYERPREFS_KEY_SHOW_MODIFIED_CHUNKS, showModifiedChunksSection ? 1 : 0);
                PlayerPrefs.Save();
            } else {
                // Keep raypick mode in sync with showModifiedChunksSection
                raypickMode = showModifiedChunksSection;
            }
            EditorGUILayout.EndHorizontal();

            if (!showModifiedChunksSection) {
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // Sort mode buttons styled like toolbar
            GUIStyle nearestButtonStyle = modifiedChunksSortMode == 0 ? EditorStyles.miniButtonMid : EditorStyles.miniButtonMid;
            GUIStyle recentButtonStyle = modifiedChunksSortMode == 1 ? EditorStyles.miniButtonMid : EditorStyles.miniButtonMid;

            // Apply active state styling
            if (modifiedChunksSortMode == 0) {
                nearestButtonStyle = new GUIStyle(EditorStyles.miniButtonMid);
                nearestButtonStyle.normal.background = nearestButtonStyle.active.background;
            }
            if (modifiedChunksSortMode == 1) {
                recentButtonStyle = new GUIStyle(EditorStyles.miniButtonMid);
                recentButtonStyle.normal.background = recentButtonStyle.active.background;
            }

            if (GUILayout.Button(new GUIContent("Nearest", "Sort by distance to SceneView camera"), nearestButtonStyle, GUILayout.Width(60))) {
                if (modifiedChunksSortMode == 0) {
                    // Already selected - refresh list
                    RefreshModifiedChunksList();
                } else {
                    modifiedChunksSortMode = 0;
                    PlayerPrefs.SetInt(PLAYERPREFS_KEY_MODIFIED_CHUNKS_SORT_MODE, modifiedChunksSortMode);
                    PlayerPrefs.Save();
                    SortModifiedChunksList();
                }
            }
            if (GUILayout.Button(new GUIContent("Recent", "Sort by modification order (most recently modified first)"), recentButtonStyle, GUILayout.Width(60))) {
                if (modifiedChunksSortMode == 1) {
                    // Already selected - refresh list
                    RefreshModifiedChunksList();
                } else {
                    modifiedChunksSortMode = 1;
                    PlayerPrefs.SetInt(PLAYERPREFS_KEY_MODIFIED_CHUNKS_SORT_MODE, modifiedChunksSortMode);
                    PlayerPrefs.Save();
                    SortModifiedChunksList();
                }
            }
            if (GUILayout.Toggle(showAllModifiedChunks, "View All", EditorStyles.miniButtonMid, GUILayout.Width(70))) {
                if (!showAllModifiedChunks) {
                    showAllModifiedChunks = true;
                    // Clear focused chunk when showing all
                    focusedChunk = null;
                    SceneView.RepaintAll();
                }
            } else {
                if (showAllModifiedChunks) {
                    showAllModifiedChunks = false;
                    SceneView.RepaintAll();
                }
            }
            if (GUILayout.Button("Reset All", EditorStyles.miniButtonRight, GUILayout.Width(70))) {
                if (EditorUtility.DisplayDialog("Reset All Modified Chunks", "This will reset all " + modifiedChunksList.Count + " modified chunks to their original terrain. Continue?", "Yes", "No")) {
                    // Unfocus any focused chunk when resetting all
                    focusedChunk = null;
                    // Register all resets as a single undo group
                    undoManager.StartChangeGroup();
                    int count = modifiedChunksList.Count;
                    for (int i = 0; i < count; i++) {
                        if (modifiedChunksList[i] != null) {
                            undoManager.SaveChunk(modifiedChunksList[i]);
                            env.ChunkReset(modifiedChunksList[i]);
                        }
                    }
                    undoManager.EndChangeGroup();
                    pendingChanges = true;
                    RefreshModifiedChunksList();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (raypickMode) {
                EditorGUILayout.HelpBox("Click on SceneView to select a chunk", MessageType.Info);
            }

            if (modifiedChunksList.Count == 0) {
                EditorGUILayout.HelpBox("No modified chunks found. Click 'Nearest' or 'Recent' to refresh.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Count: " + modifiedChunksList.Count, EditorStyles.miniLabel);

            const float ROW_HEIGHT = 20f;
            int visibleRows = Mathf.Min(modifiedChunksList.Count, MAX_VISIBLE_MODIFIED_CHUNKS);
            float scrollViewHeight = visibleRows * ROW_HEIGHT + 5;
            modifiedChunksScrollPosition = EditorGUILayout.BeginScrollView(modifiedChunksScrollPosition, GUILayout.Height(scrollViewHeight));

            for (int i = 0; i < modifiedChunksList.Count; i++) {
                VoxelChunk chunk = modifiedChunksList[i];
                if (chunk == null) continue;

                bool isFocused = focusedChunk == chunk;
                Color originalColor = GUI.color;
                if (isFocused) {
                    GUI.color = Color.yellow;
                }

                EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_HEIGHT));

                // Calculate chunk coordinates
                int chunkX = (int)(chunk.position.x / VoxelPlayEnvironment.CHUNK_SIZE);
                int chunkY = (int)(chunk.position.y / VoxelPlayEnvironment.CHUNK_SIZE);
                int chunkZ = (int)(chunk.position.z / VoxelPlayEnvironment.CHUNK_SIZE);

                // Display chunk info
                string chunkInfo = string.Format("({0},{1},{2})", chunkX, chunkY, chunkZ);
                EditorGUILayout.LabelField(chunkInfo, GUILayout.Width(80));
                EditorGUILayout.LabelField(string.Format("Pos: ({0:F1},{1:F1},{2:F1})", chunk.position.x, chunk.position.y, chunk.position.z), GUILayout.Width(150));

                if (GUILayout.Button("Focus", EditorStyles.miniButtonLeft, GUILayout.Width(50))) {
                    FocusCameraOnChunk(chunk);
                }
                if (GUILayout.Button("Reset", EditorStyles.miniButtonRight, GUILayout.Width(50))) {
                    if (EditorUtility.DisplayDialog("Reset Chunk", "Reset chunk at " + chunkInfo + " to original terrain?", "Yes", "No")) {
                        ResetChunk(chunk);
                        break;
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (isFocused) {
                    GUI.color = originalColor;
                }
            }

            EditorGUILayout.EndScrollView();
        }

    }

}
