using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace VoxelPlay {

    public class TerrainGraphEditorWindow : EditorWindow {
        const float PreviewPanelDefaultWidth = 300f;
        const float PreviewPanelDefaultHeight = 372f;
        const float PreviewPanelMinWidth = 100f;
        const float PreviewPanelMinHeight = 160f;
        const float PreviewPanelMaxWidth = 720f;
        const float PreviewPanelMaxHeight = 900f;
        const float PreviewResizeHandleSize = 14f;
        const string PreviewPanelPrefsKeyPrefix = "VoxelPlay.TerrainGraphEditor";
        const float PreviewFollowAnchorRefreshThresholdPixels = 0.75f;

        TerrainGraphView graphView;
        [SerializeField] TerrainDefaultGenerator currentGenerator;
        [SerializeField] TerrainGraphEditorSessionState sessionState;
        [SerializeField] bool hasDraftSnapshot;
        [SerializeField] TerrainDefaultGenerator draftSnapshotGenerator;
        [SerializeField] TerrainGraphView.GraphSnapshot draftSnapshot;
        Label titleLabel;
        ToolbarButton updateWorldNowButton;
        IMGUIContainer previewContainer;
        VisualElement diagnosticsPanel;
        Label diagnosticsSummaryLabel;
        ScrollView diagnosticsScrollView;
        Texture2D terrainPreviewTexture;
        Material previewTextureMat;
        [SerializeField] Vector2 previewCenter;
        [SerializeField] Vector2 previewSize = new Vector2(1024f, 1024f);
        [SerializeField] int previewResolution = 128;
        [SerializeField] Vector2 previewPanelPosition = new Vector2(-1f, -1f);
        [SerializeField] bool previewPanelPositionInitialized;
        [SerializeField] bool previewPanelManuallyPositioned;
        [SerializeField] Vector2 previewPanelSize = new Vector2(PreviewPanelDefaultWidth, PreviewPanelDefaultHeight);
        [SerializeField] Rect minimapRect = new Rect(10f, 30f, 200f, 140f);
        [SerializeField] float previewObservedWaterLevel;
        [SerializeField] float previewObservedMaxHeight;
        [SerializeField] bool previewObservedAddWater;
        [SerializeField] TerrainGraphPreviewRenderer.PreviewMode previewMode = TerrainGraphPreviewRenderer.PreviewMode.Hillshade;
        bool previewDirty = true;
        [SerializeField] bool previewVisible = true;
        [SerializeField] bool diagnosticsVisible = true;
        [SerializeField] bool previewFollowAnchor = true;
        bool previewRefreshQueued;
        string previewStatus = "Preview updates automatically from the current graph.";
        WorldDefinition previewWorld;
        TerrainGraphView.GraphSnapshot previewSnapshot;
        bool previewWorldLookupAttempted;
        bool previewPanning;
        Vector2 previewPanMousePos;
        bool previewHoverActive;
        Vector2 previewHoverLocalPosition;
        bool previewOverlayDragging;
        Vector2 previewOverlayDragOffset;
        bool previewOverlayResizing;
        Vector2 previewOverlayResizeStartPointer;
        Vector2 previewOverlayResizeStartSize;
        bool previewAnchorPoseCached;
        Vector3 previewObservedAnchorWorldPos;
        Vector3 previewObservedAnchorForward = Vector3.forward;
        bool previewObservedAnchorInPreview;

        // Sync toggle: live world refresh when graph output changes
        const int SyncSampleCount = 5;
        static readonly double[] syncSampleCoords = { 0, 0, 100, 200, -150, 75, 50, -300, -200, -100 };
        [SerializeField] bool syncEnabled;
        float[] syncCachedAltitudes;
        float[] syncCachedMoistures;
        bool syncWorldRefreshQueued;

        public static void Open(TerrainDefaultGenerator generator) {
            var window = GetWindow<TerrainGraphEditorWindow>("Terrain Graph");
            window.minSize = new Vector2(600, 400);
            window.Load(generator);
            window.Show();
        }

        [MenuItem("Window/Voxel Play/Terrain Graph Editor")]
        static void OpenFromMenu() {
            var window = GetWindow<TerrainGraphEditorWindow>("Terrain Graph");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        void CreateGUI() {
            EnsureSessionState();
            LoadPreviewPanelPreferences();
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<ValidateCommandEvent>(OnValidateCommand);
            rootVisualElement.RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);

            // Toolbar
            var toolbar = new Toolbar();

            titleLabel = new Label("No generator selected");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.paddingLeft = 8;
            titleLabel.style.paddingTop = 2;
            toolbar.Add(titleLabel);

            toolbar.Add(new ToolbarSpacer { flex = true });

            var newBtn = new ToolbarButton(() => CreateNewGenerator()) { text = "New" };
            newBtn.tooltip = "Create a new terrain generator asset";
            SetToolbarButtonContent(newBtn, "New", "d_CreateAddNew", "CreateAddNew", "d_TreeEditor.Duplicate", "TreeEditor.Duplicate");
            toolbar.Add(newBtn);

            var reloadBtn = new ToolbarButton(() => Reload()) { text = "Reload" };
            reloadBtn.tooltip = "Reload graph from generator (discard unsaved changes)";
            SetToolbarButtonContent(reloadBtn, "Reload", "d_Refresh", "Refresh");
            toolbar.Add(reloadBtn);

            var saveBtn = new ToolbarButton(() => Save()) { text = "Save" };
            saveBtn.tooltip = "Save changes to the generator asset";
            SetToolbarButtonContent(saveBtn, "Save", "d_SaveAs", "SaveAs", "d_Favorite", "Favorite");
            toolbar.Add(saveBtn);

            var layoutBtn = new ToolbarButton(() => graphView?.AutoLayout()) { text = "Auto Layout" };
            SetToolbarButtonContent(layoutBtn, "Auto Layout", "d_Grid.BoxTool", "Grid.BoxTool", "d_GridLayoutGroup Icon", "GridLayoutGroup Icon");
            toolbar.Add(layoutBtn);

            var fitBtn = new ToolbarButton(() => graphView?.FrameAll()) { text = "Fit View" };
            SetToolbarButtonContent(fitBtn, "Fit View", "d_ViewToolZoom", "ViewToolZoom");
            toolbar.Add(fitBtn);

            var minimapToggle = new ToolbarToggle { text = "Minimap", value = true };
            minimapToggle.tooltip = "Show or hide the graph minimap";
            minimapToggle.RegisterValueChangedCallback(evt => graphView?.SetMinimapVisible(evt.newValue));
            SetToolbarToggleContent(minimapToggle, "Minimap", "d_SceneViewOrtho", "SceneViewOrtho");
            toolbar.Add(minimapToggle);

            var previewToggle = new ToolbarToggle { text = "Preview", value = previewVisible };
            previewToggle.tooltip = "Show or hide the terrain preview overlay";
            previewToggle.RegisterValueChangedCallback(evt => {
                previewVisible = evt.newValue;
                UpdatePreviewVisibility();
            });
            SetToolbarToggleContent(previewToggle, "Preview",
                "d_Terrain Icon", "Terrain Icon",
                "d_SceneViewFx", "SceneViewFx",
                "d_PreMatSphere", "PreMatSphere");
            toolbar.Add(previewToggle);

            var diagnosticsToggle = new ToolbarToggle { text = "Diagnostics", value = diagnosticsVisible };
            diagnosticsToggle.tooltip = "Show or hide graph diagnostics inside this window";
            diagnosticsToggle.RegisterValueChangedCallback(evt => {
                diagnosticsVisible = evt.newValue;
                UpdateDiagnosticsVisibility();
            });
            SetToolbarToggleContent(diagnosticsToggle, "Diagnostics", "d_console.warnicon", "console.warnicon", "d_UnityEditor.ConsoleWindow", "UnityEditor.ConsoleWindow");
            toolbar.Add(diagnosticsToggle);

            var syncToggle = new ToolbarToggle { text = "Sync", value = syncEnabled };
            syncToggle.tooltip = "Synchronize changes to the scene by refreshing the world when the terrain output changes. Enabling this also performs an immediate world update";
            syncToggle.RegisterValueChangedCallback(evt => {
                syncEnabled = evt.newValue;
                if (syncEnabled) {
                    SyncEnsureRenderInEditor();
                    SyncRefreshWorldFromGraph(forceRefresh: true);
                }
            });
            SetToolbarToggleContent(syncToggle, "Sync", "d_Linked", "Linked", "d_Refresh", "Refresh");
            toolbar.Add(syncToggle);

            updateWorldNowButton = new ToolbarButton(() => UpdateWorldNow()) { text = "Update World" };
            updateWorldNowButton.tooltip = "Reload the currently loaded world using the current graph";
            SetToolbarButtonContent(updateWorldNowButton, "Update World", "d_Terrain Icon", "Terrain Icon", "d_SceneViewFx", "SceneViewFx", "d_PreMatSphere", "PreMatSphere");
            toolbar.Add(updateWorldNowButton);

            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            rootVisualElement.Add(toolbar);

            // Graph view
            graphView = new TerrainGraphView();
            graphView.OnDirtyChanged = UpdateTitle;
            graphView.OnGraphMutated = OnGraphMutated;
            graphView.OnMinimapRectChanged = rect => minimapRect = rect;
            graphView.SetMinimapRect(minimapRect);
            minimapToggle.SetValueWithoutNotify(graphView.MinimapVisible);
            rootVisualElement.Add(graphView);

            previewContainer = new IMGUIContainer(DrawPreviewPanel);
            previewContainer.style.position = Position.Absolute;
            previewContainer.style.backgroundColor = new Color(0.09f, 0.09f, 0.09f, 0.96f);
            previewContainer.style.borderLeftWidth = 1;
            previewContainer.style.borderRightWidth = 1;
            previewContainer.style.borderTopWidth = 1;
            previewContainer.style.borderBottomWidth = 1;
            previewContainer.style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            previewContainer.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            previewContainer.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            previewContainer.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            previewContainer.RegisterCallback<PointerDownEvent>(OnPreviewOverlayPointerDown);
            previewContainer.RegisterCallback<PointerMoveEvent>(OnPreviewOverlayPointerMove);
            previewContainer.RegisterCallback<PointerUpEvent>(OnPreviewOverlayPointerUp);
            previewContainer.RegisterCallback<PointerEnterEvent>(OnPreviewOverlayPointerEnter);
            previewContainer.RegisterCallback<PointerLeaveEvent>(OnPreviewOverlayPointerLeave);
            previewContainer.RegisterCallback<PointerCaptureOutEvent>(OnPreviewOverlayPointerCaptureOut);
            rootVisualElement.Add(previewContainer);
            ApplyPreviewContainerSize();
            ApplyPreviewContainerPosition();
            QueuePreviewContainerPositionRefresh();
            UpdatePreviewVisibility();

            diagnosticsPanel = BuildDiagnosticsPanel();
            rootVisualElement.Add(diagnosticsPanel);
            UpdateDiagnosticsVisibility();
            RefreshDiagnosticsPanel();

            // Unsaved changes prompt message
            saveChangesMessage = "The terrain graph has pending changes. Save commits them to the terrain generator asset. Discard restores the original asset snapshot before closing.";

            // Load current generator if we had one
            if (currentGenerator != null) {
                Load(currentGenerator);
            } else {
                UpdateWorldActionAvailability();
            }
        }

        void EnsureSessionState() {
            if (sessionState != null) return;
            sessionState = CreateInstance<TerrainGraphEditorSessionState>();
            sessionState.hideFlags = HideFlags.HideAndDontSave;
        }

        bool HasGeneratorBackupFor(TerrainDefaultGenerator generator) {
            return generator != null
                && sessionState != null
                && sessionState.hasGeneratorBackup
                && sessionState.generator == generator
                && !string.IsNullOrEmpty(sessionState.generatorBackupJson);
        }

        bool HasDirtyGeneratorBackup() {
            return currentGenerator != null
                && sessionState != null
                && sessionState.hasGeneratorBackup
                && sessionState.generator == currentGenerator
                && sessionState.generatorBackupDirty;
        }

        bool HasPendingChanges() {
            return (graphView != null && graphView.IsDirty)
                || HasDirtyGeneratorBackup()
                || HasDraftSessionFor(currentGenerator);
        }

        void EnsureCurrentGeneratorBackup() {
            if (currentGenerator == null) return;
            EnsureSessionState();
            if (HasGeneratorBackupFor(currentGenerator)) return;

            sessionState.generator = currentGenerator;
            sessionState.generatorBackupJson = EditorJsonUtility.ToJson(currentGenerator);
            sessionState.hasGeneratorBackup = true;
            sessionState.generatorBackupDirty = false;
            EditorUtility.SetDirty(sessionState);
        }

        void RefreshCurrentGeneratorBackup() {
            if (currentGenerator == null) return;
            EnsureSessionState();

            sessionState.generator = currentGenerator;
            sessionState.generatorBackupJson = EditorJsonUtility.ToJson(currentGenerator);
            sessionState.hasGeneratorBackup = true;
            sessionState.generatorBackupDirty = false;
            EditorUtility.SetDirty(sessionState);
            UpdateTitle();
        }

        void MarkCurrentGeneratorBackupDirty() {
            if (currentGenerator == null) return;
            EnsureCurrentGeneratorBackup();
            if (sessionState == null || sessionState.generator != currentGenerator) return;

            if (!sessionState.generatorBackupDirty) {
                sessionState.generatorBackupDirty = true;
                EditorUtility.SetDirty(sessionState);
            }
            UpdateTitle();
        }

        bool RestoreCurrentGeneratorBackup(bool reloadWorld) {
            if (currentGenerator == null || !HasGeneratorBackupFor(currentGenerator) || !HasDirtyGeneratorBackup()) {
                return false;
            }

            Undo.RecordObject(currentGenerator, "Restore Terrain Generator");
            EditorJsonUtility.FromJsonOverwrite(sessionState.generatorBackupJson, currentGenerator);
            EditorUtility.SetDirty(currentGenerator);

            sessionState.generatorBackupDirty = false;
            EditorUtility.SetDirty(sessionState);

            syncCachedAltitudes = null;
            syncCachedMoistures = null;

            var env = VoxelPlayEnvironment.instance;
            if (reloadWorld && env != null && env.world != null && env.world.terrainGenerator == currentGenerator) {
                env.InvalidateTerrainCaches(currentGenerator);
                env.ReloadWorld(keepWorldChanges: false);
            } else {
                currentGenerator.InvalidateRuntimeCaches();
            }

            SyncPreviewDependencies(currentGenerator);
            SyncPreviewArea(currentGenerator);
            previewDirty = true;
            previewStatus = "Updating preview...";
            QueuePreviewRefresh();
            previewContainer?.MarkDirtyRepaint();
            UpdateTitle();
            RefreshDiagnosticsPanel();
            Repaint();
            return true;
        }

        void DiscardPendingChanges(bool reloadWorld) {
            RestoreCurrentGeneratorBackup(reloadWorld);
            ClearDraftSession();
            if (graphView != null && currentGenerator != null) {
                graphView.LoadFromGenerator(currentGenerator);
            }
            UpdateTitle();
            RefreshDiagnosticsPanel();
        }

        bool PromptToResolvePendingChanges(string actionMessage) {
            if (!HasPendingChanges()) return true;

            int option = EditorUtility.DisplayDialogComplex(
                "Unsaved Terrain Graph Changes",
                actionMessage + "\n\nSave commits the current graph to the terrain generator asset. Discard restores the original asset snapshot and drops the current graph edits.",
                "Save",
                "Cancel",
                "Discard");

            switch (option) {
                case 0:
                    Save();
                    return true;
                case 2:
                    DiscardPendingChanges(reloadWorld: true);
                    return true;
                default:
                    return false;
            }
        }

        void Save() {
            if (graphView != null && currentGenerator != null) {
                EnsureCurrentGeneratorBackup();
                graphView.SaveToGenerator();
                MarkCurrentGeneratorBackupDirty();
                var env = VoxelPlayEnvironment.instance;
                if (env != null && env.world != null && env.world.terrainGenerator == currentGenerator) {
                    env.InvalidateTerrainCaches(currentGenerator);
                } else {
                    currentGenerator.InvalidateRuntimeCaches();
                }
                AssetDatabase.SaveAssets();
                ClearDraftSession();
                RefreshCurrentGeneratorBackup();
                UpdateTitle();
                RefreshDiagnosticsPanel();
                Repaint();
            }
        }

        void OnValidateCommand(ValidateCommandEvent evt) {
            if (evt.commandName != "Save") return;
            if (currentGenerator == null || graphView == null) return;
            evt.StopPropagation();
            evt.PreventDefault();
        }

        void OnKeyDown(KeyDownEvent evt) {
            if (currentGenerator == null || graphView == null) return;
            if (!evt.actionKey || evt.keyCode != KeyCode.S) return;
            if (evt.altKey) return;

            Save();
            evt.StopPropagation();
            evt.PreventDefault();
        }

        void OnExecuteCommand(ExecuteCommandEvent evt) {
            if (evt.commandName != "Save") return;
            if (currentGenerator == null || graphView == null) return;
            Save();
            evt.StopPropagation();
            evt.PreventDefault();
        }

        void CreateNewGenerator() {
            if (!PromptToResolvePendingChanges("Create a new terrain generator asset?")) {
                return;
            }

            if (!EditorUtility.DisplayDialog("New Terrain Generator", "Create a new terrain generator asset?", "Create", "Cancel")) {
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Terrain Generator",
                "TerrainGenerator",
                "asset",
                "Choose a location for the new terrain generator asset"
            );
            if (string.IsNullOrEmpty(path)) return;

            var generator = CreateInstance<TerrainDefaultGenerator>();
            generator.name = Path.GetFileNameWithoutExtension(path);
            generator.Steps = Array.Empty<StepData>();
            generator.nodePositions = null;
            generator.terminalStepIndex = -1;
            generator.moistureTerminalStepIndex = -1;
            generator.graphVersion = 0;
            generator.graphLayoutV2 = new TerrainGraphLayoutData();

            AssetDatabase.CreateAsset(generator, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(generator);
            Selection.activeObject = generator;
            Load(generator);
        }

        void Load(TerrainDefaultGenerator generator) {
            EnsureSessionState();
            if (generator != null && currentGenerator != null && generator != currentGenerator) {
                if (!PromptToResolvePendingChanges("Open a different terrain generator?")) {
                    return;
                }
            }
            currentGenerator = generator;
            EnsureCurrentGeneratorBackup();
            UpdateWorldActionAvailability();
            if (previewPanelPosition.x <= 1f && previewPanelPosition.y <= 1f) {
                previewPanelPosition = new Vector2(-1f, -1f);
                previewPanelPositionInitialized = false;
                previewPanelManuallyPositioned = false;
            }
            previewSnapshot = default;
            previewWorld = null;
            previewWorldLookupAttempted = false;
            previewHoverActive = false;
            ResetPreviewAnchorTracking();
            if (terrainPreviewTexture != null) {
                DestroyImmediate(terrainPreviewTexture);
                terrainPreviewTexture = null;
            }
            if (graphView != null && generator != null) {
                if (HasDraftSessionFor(generator)) {
                    var snapshot = (hasDraftSnapshot && draftSnapshotGenerator == generator)
                        ? draftSnapshot
                        : sessionState.snapshot;
                    graphView.LoadFromSnapshot(generator, snapshot, true);
                } else {
                    graphView.LoadFromGenerator(generator);
                }
                SyncPreviewDependencies(generator);
                SyncPreviewArea(generator);
                if (previewFollowAnchor) {
                    TrySyncPreviewCenterToAssignedAnchor(refreshPreviewIfChanged: false);
                }
                previewDirty = true;
                previewStatus = "Updating preview...";
                QueuePreviewRefresh();
                QueuePreviewContainerPositionRefresh();
                previewContainer?.MarkDirtyRepaint();
                UpdateTitle();
                RefreshDiagnosticsPanel();
            } else if (titleLabel != null) {
                titleLabel.text = "No generator selected";
                hasUnsavedChanges = false;
                previewDirty = true;
                previewStatus = "Select a terrain generator to preview the current graph.";
                previewWorld = null;
                RefreshDiagnosticsPanel();
            }
        }

        void UpdateWorldActionAvailability() {
            if (updateWorldNowButton != null) {
                updateWorldNowButton.SetEnabled(currentGenerator != null);
            }
        }

        void Reload() {
            if (currentGenerator != null) {
                if (!PromptToResolvePendingChanges("Reload the graph from the terrain generator asset?")) {
                    return;
                }
                ClearDraftSession();
                Load(currentGenerator);
            }
        }

        void UpdatePreviewVisibility() {
            if (previewContainer == null) return;
            previewContainer.style.display = previewVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (previewVisible) {
                ApplyPreviewContainerSize();
                ApplyPreviewContainerPosition();
                if (previewFollowAnchor) {
                    TrySyncPreviewCenterToAssignedAnchor(refreshPreviewIfChanged: false);
                }
                if (previewDirty || terrainPreviewTexture == null) {
                    QueuePreviewRefresh();
                }
                previewContainer.MarkDirtyRepaint();
            }
        }

        VisualElement BuildDiagnosticsPanel() {
            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = StyleKeyword.Auto;
            panel.style.right = 10f;
            panel.style.top = 42f;
            panel.style.width = 320f;
            panel.style.maxHeight = 320f;
            panel.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            panel.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            panel.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            panel.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            panel.style.borderTopLeftRadius = 4f;
            panel.style.borderTopRightRadius = 4f;
            panel.style.borderBottomLeftRadius = 4f;
            panel.style.borderBottomRightRadius = 4f;

            var header = new VisualElement();
            header.style.paddingLeft = 10f;
            header.style.paddingRight = 10f;
            header.style.paddingTop = 8f;
            header.style.paddingBottom = 6f;
            header.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f, 1f);

            var title = new Label("Diagnostics");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            diagnosticsSummaryLabel = new Label("No graph loaded");
            diagnosticsSummaryLabel.style.marginTop = 2f;
            diagnosticsSummaryLabel.style.fontSize = 11f;
            diagnosticsSummaryLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            header.Add(diagnosticsSummaryLabel);
            panel.Add(header);

            diagnosticsScrollView = new ScrollView();
            diagnosticsScrollView.style.maxHeight = 272f;
            diagnosticsScrollView.style.paddingLeft = 8f;
            diagnosticsScrollView.style.paddingRight = 8f;
            diagnosticsScrollView.style.paddingTop = 6f;
            diagnosticsScrollView.style.paddingBottom = 8f;
            panel.Add(diagnosticsScrollView);

            return panel;
        }

        void UpdateDiagnosticsVisibility() {
            if (diagnosticsPanel == null) return;
            diagnosticsPanel.style.display = diagnosticsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void RefreshDiagnosticsPanel() {
            if (diagnosticsSummaryLabel == null || diagnosticsScrollView == null) return;

            diagnosticsScrollView.Clear();
            if (graphView == null || currentGenerator == null) {
                diagnosticsSummaryLabel.text = "Select a terrain generator to inspect the graph.";
                return;
            }

            var diagnostics = graphView.BuildDiagnostics();
            int errorCount = diagnostics.Count(d => d.Severity == TerrainGraphDiagnosticSeverity.Error);
            int warningCount = diagnostics.Count(d => d.Severity == TerrainGraphDiagnosticSeverity.Warning);
            int infoCount = diagnostics.Count(d => d.Severity == TerrainGraphDiagnosticSeverity.Info);
            diagnosticsSummaryLabel.text = $"{errorCount} errors, {warningCount} warnings, {infoCount} info";

            if (diagnostics.Count == 0) {
                var empty = new Label("No issues detected in the current graph.");
                empty.style.unityFontStyleAndWeight = FontStyle.Italic;
                empty.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);
                diagnosticsScrollView.Add(empty);
                return;
            }

            foreach (var diagnostic in diagnostics) {
                var item = new Button(() => graphView.FocusElement(diagnostic.Target));
                item.text = string.Empty;
                item.style.flexDirection = FlexDirection.Column;
                item.style.alignItems = Align.FlexStart;
                item.style.marginBottom = 6f;
                item.style.paddingLeft = 8f;
                item.style.paddingRight = 8f;
                item.style.paddingTop = 6f;
                item.style.paddingBottom = 6f;
                item.style.unityTextAlign = TextAnchor.UpperLeft;
                item.style.whiteSpace = WhiteSpace.Normal;

                switch (diagnostic.Severity) {
                    case TerrainGraphDiagnosticSeverity.Error:
                        item.style.backgroundColor = new Color(0.34f, 0.15f, 0.15f, 0.95f);
                        break;
                    case TerrainGraphDiagnosticSeverity.Warning:
                        item.style.backgroundColor = new Color(0.33f, 0.25f, 0.12f, 0.95f);
                        break;
                    default:
                        item.style.backgroundColor = new Color(0.16f, 0.19f, 0.24f, 0.95f);
                        break;
                }

                var title = new Label(diagnostic.Title);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.whiteSpace = WhiteSpace.Normal;
                item.Add(title);

                var message = new Label(diagnostic.Message);
                message.style.fontSize = 11f;
                message.style.marginTop = 2f;
                message.style.whiteSpace = WhiteSpace.Normal;
                item.Add(message);

                item.SetEnabled(diagnostic.Target != null);
                diagnosticsScrollView.Add(item);
            }
        }

        void ApplyPreviewContainerPosition() {
            if (previewContainer == null) return;
            Rect bounds = rootVisualElement.contentRect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            Vector2 panelSize = GetPreviewPanelDisplaySize();
            bool hasInvalidStoredPosition = previewPanelPosition.x <= 1f && previewPanelPosition.y <= 1f;

            if (!previewPanelManuallyPositioned || !previewPanelPositionInitialized || hasInvalidStoredPosition) {
                previewPanelPosition = new Vector2(
                    Mathf.Max(12f, bounds.width - panelSize.x - 12f),
                    Mathf.Max(40f, bounds.height - panelSize.y - 12f)
                );
                previewPanelPositionInitialized = true;
            } else if (previewPanelPosition.x < 0f || previewPanelPosition.y < 0f) {
                previewPanelPosition = new Vector2(
                    Mathf.Max(12f, bounds.width - panelSize.x - 12f),
                    Mathf.Max(40f, bounds.height - panelSize.y - 12f)
                );
                previewPanelManuallyPositioned = false;
            }

            Rect rect = ClampOverlayRect(new Rect(previewPanelPosition, panelSize), bounds.size);
            previewPanelPosition = rect.position;
            previewContainer.style.left = rect.x;
            previewContainer.style.top = rect.y;
        }

        void ApplyPreviewContainerSize(bool savePreferences = false) {
            if (previewContainer == null) return;
            Vector2 panelSize = GetPreviewPanelDisplaySize();
            previewContainer.style.width = panelSize.x;
            previewContainer.style.height = panelSize.y;
            if (savePreferences) {
                SavePreviewPanelPreferences();
            }
        }

        void QueuePreviewContainerPositionRefresh() {
            if (rootVisualElement == null || previewContainer == null) return;
            rootVisualElement.schedule.Execute(() => {
                if (previewContainer == null) return;
                ApplyPreviewContainerSize();
                ApplyPreviewContainerPosition();
                previewContainer.MarkDirtyRepaint();
            }).StartingIn(0);
            rootVisualElement.schedule.Execute(() => {
                if (previewContainer == null) return;
                ApplyPreviewContainerSize();
                ApplyPreviewContainerPosition();
                previewContainer.MarkDirtyRepaint();
            }).StartingIn(16);
        }

        Vector2 GetPreviewPanelDisplaySize() {
            return ClampPreviewPanelSizeToBounds(previewPanelSize, rootVisualElement.contentRect.size);
        }

        static Vector2 ClampPreviewPanelSize(Vector2 size) {
            size.x = Mathf.Clamp(size.x, PreviewPanelMinWidth, PreviewPanelMaxWidth);
            size.y = Mathf.Clamp(size.y, PreviewPanelMinHeight, PreviewPanelMaxHeight);
            return size;
        }

        static Vector2 ClampPreviewPanelSizeToBounds(Vector2 size, Vector2 bounds) {
            size = ClampPreviewPanelSize(size);
            if (bounds.x > 0f) {
                size.x = Mathf.Min(size.x, Mathf.Max(PreviewPanelMinWidth, bounds.x));
            }
            if (bounds.y > 0f) {
                size.y = Mathf.Min(size.y, Mathf.Max(PreviewPanelMinHeight, bounds.y));
            }
            return size;
        }

        void LoadPreviewPanelPreferences() {
            previewPanelSize = new Vector2(
                EditorPrefs.GetFloat(GetPreviewPanelPrefKey("Width"), previewPanelSize.x),
                EditorPrefs.GetFloat(GetPreviewPanelPrefKey("Height"), previewPanelSize.y)
            );
            previewPanelSize = ClampPreviewPanelSize(previewPanelSize);
        }

        void SavePreviewPanelPreferences() {
            EditorPrefs.SetFloat(GetPreviewPanelPrefKey("Width"), previewPanelSize.x);
            EditorPrefs.SetFloat(GetPreviewPanelPrefKey("Height"), previewPanelSize.y);
        }

        static string GetPreviewPanelPrefKey(string suffix) {
            return $"{PreviewPanelPrefsKeyPrefix}.{Application.dataPath}.{suffix}";
        }

        void SetToolbarButtonContent(Button button, string text, params string[] iconNames) {
            button.text = string.Empty;
            button.Add(BuildToolbarContent(text, iconNames));
        }

        void SetToolbarToggleContent(ToolbarToggle toggle, string text, params string[] iconNames) {
            toggle.text = string.Empty;
            var content = BuildToolbarContent(text, iconNames);
            content.style.marginLeft = 2;
            toggle.Add(content);
        }

        VisualElement BuildToolbarContent(string text, params string[] iconNames) {
            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.alignItems = Align.Center;
            content.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;

            Texture icon = GetEditorIcon(iconNames);
            if (icon != null) {
                var image = new Image {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = UnityEngine.UIElements.PickingMode.Ignore
                };
                image.style.width = 16;
                image.style.height = 16;
                image.style.marginRight = 4;
                content.Add(image);
            }

            var label = new Label(text) {
                pickingMode = UnityEngine.UIElements.PickingMode.Ignore
            };
            content.Add(label);
            return content;
        }

        Texture GetEditorIcon(params string[] iconNames) {
            if (iconNames == null) return null;
            for (int i = 0; i < iconNames.Length; i++) {
                if (string.IsNullOrEmpty(iconNames[i])) continue;
                Texture icon = EditorGUIUtility.IconContent(iconNames[i])?.image;
                if (icon != null) return icon;
            }
            return null;
        }

        GUIContent GetEditorIconContent(string text, params string[] iconNames) {
            Texture icon = GetEditorIcon(iconNames);
            return icon != null ? new GUIContent(text, icon) : new GUIContent(text);
        }

        void OnGraphMutated() {
            SaveDraftSession();
            previewDirty = true;
            previewStatus = "Updating preview...";
            QueuePreviewRefresh();
            previewContainer?.MarkDirtyRepaint();
            RefreshDiagnosticsPanel();
            if (syncEnabled) {
                QueueSyncWorldRefresh();
            }
        }

        void UpdateTitle() {
            bool dirty = HasPendingChanges();
            hasUnsavedChanges = dirty;

            // Let EditorWindow show its built-in unsaved marker from hasUnsavedChanges.
            titleContent.text = "Terrain Graph";

            // Update toolbar label
            if (titleLabel != null) {
                if (currentGenerator != null) {
                    titleLabel.text = currentGenerator.name + (dirty ? " *" : "");
                } else {
                    titleLabel.text = "No generator selected";
                }
            }
        }

        void DrawPreviewPanel() {
            if (currentGenerator == null || graphView == null) {
                Rect fullRect = new Rect(0, 0, previewContainer.contentRect.width, previewContainer.contentRect.height);
                EditorGUI.DrawRect(fullRect, new Color(0.11f, 0.11f, 0.11f, 1f));
                GUI.Label(new Rect(10, 10, fullRect.width - 20, 40), "Select a terrain generator to preview the current graph.", EditorStyles.wordWrappedLabel);
                Rect emptyResizeHandleRect = GetPreviewResizeHandleRect(fullRect);
                EditorGUIUtility.AddCursorRect(emptyResizeHandleRect, MouseCursor.ResizeUpLeft);
                return;
            }

            if (previewTextureMat == null) {
                previewTextureMat = Resources.Load<Material>("VoxelPlay/PreviewTexture");
            }

            Rect panelRect = new Rect(0, 0, previewContainer.contentRect.width, previewContainer.contentRect.height);
            EditorGUI.DrawRect(panelRect, new Color(0.09f, 0.09f, 0.09f, 0.96f));

            const float previewHeaderControlTop = 6f;
            const float previewHeaderControlHeight = 20f;
            const float previewHeaderButtonWidth = 20f;
            const float previewHeaderRightPadding = 10f;
            const float previewHeaderGap = 4f;
            const float previewModeWidth = 96f;

            Rect refreshRect = new Rect(
                panelRect.width - previewHeaderRightPadding - previewHeaderButtonWidth,
                previewHeaderControlTop,
                previewHeaderButtonWidth,
                previewHeaderControlHeight
            );
            Rect anchorCenterRect = new Rect(
                refreshRect.x - previewHeaderGap - previewHeaderButtonWidth,
                previewHeaderControlTop,
                previewHeaderButtonWidth,
                previewHeaderControlHeight
            );
            Rect modeRect = new Rect(
                anchorCenterRect.x - previewHeaderGap - previewModeWidth,
                previewHeaderControlTop,
                previewModeWidth,
                previewHeaderControlHeight
            );

            var previewTitleStyle = new GUIStyle(EditorStyles.boldLabel) {
                alignment = TextAnchor.MiddleLeft
            };
            Rect titleRect = new Rect(10, previewHeaderControlTop, modeRect.x - 20f, previewHeaderControlHeight);
            EditorGUI.LabelField(titleRect, "Preview", previewTitleStyle);

            EditorGUI.BeginChangeCheck();
            var newPreviewMode = (TerrainGraphPreviewRenderer.PreviewMode)EditorGUI.EnumPopup(modeRect, GUIContent.none, previewMode, EditorStyles.popup);
            if (EditorGUI.EndChangeCheck()) {
                previewMode = newPreviewMode;
                previewDirty = true;
                previewStatus = "Updating preview...";
                QueuePreviewRefresh();
            }

            Texture refreshIcon = GetEditorIcon("d_Refresh", "Refresh");
            bool canCenterOnAnchor = TryGetAssignedDistanceAnchorPosition(out Vector3 anchorWorldPos, out string anchorName);
            EditorGUI.BeginDisabledGroup(!canCenterOnAnchor);
            bool newPreviewFollowAnchor = GUI.Toggle(anchorCenterRect, previewFollowAnchor, new GUIContent(string.Empty, canCenterOnAnchor
                ? $"Follow distance anchor {anchorName}"
                : "Follow the assigned distance anchor"), EditorStyles.miniButton);
            if (newPreviewFollowAnchor != previewFollowAnchor) {
                previewFollowAnchor = newPreviewFollowAnchor;
                if (previewFollowAnchor) {
                    TrySyncPreviewCenterToAssignedAnchor(refreshPreviewIfChanged: true);
                } else {
                    previewContainer?.MarkDirtyRepaint();
                }
            }
            DrawPreviewTargetButtonContent(anchorCenterRect, previewFollowAnchor);
            EditorGUI.EndDisabledGroup();

            if (GUI.Button(refreshRect, new GUIContent(string.Empty, "Refresh terrain preview"), EditorStyles.miniButton)) {
                RefreshPreview(forceRefresh: true);
            }
            DrawPreviewHeaderButtonContent(refreshRect, refreshIcon, "R");

            Rect statusRect = new Rect(10, 28, panelRect.width - 20, 16);
            EditorGUI.LabelField(statusRect, previewStatus, EditorStyles.miniLabel);

            string coverageLabel = $"Coverage {previewSize.x:0}m x {previewSize.y:0}m";
            string waterLabel = currentGenerator.addWater ? $"Water {currentGenerator.waterLevel:0}m" : "Water off";
            Rect coverageRect = new Rect(10, 44, panelRect.width - 20, 16);
            EditorGUI.LabelField(coverageRect, $"{coverageLabel} | {waterLabel}", EditorStyles.miniLabel);

            Rect centerRect = new Rect(10, 60, panelRect.width - 20, 16);
            EditorGUI.LabelField(centerRect, $"Center ({previewCenter.x:0}, {previewCenter.y:0}) | Wheel zoom, drag pan", EditorStyles.miniLabel);

            float previewSizePixels = Mathf.Min(panelRect.width - 20, panelRect.height - 92);
            Rect imageRect = new Rect(10, 82, previewSizePixels, previewSizePixels);
            HandlePreviewInput(imageRect);

            if (terrainPreviewTexture != null) {
                EditorGUI.DrawPreviewTexture(imageRect, terrainPreviewTexture, previewTextureMat, ScaleMode.StretchToFill);
            } else {
                EditorGUI.DrawRect(imageRect, new Color(0.11f, 0.11f, 0.11f, 1f));
            }

            if (previewDirty) {
                Color staleColor = new Color(0.65f, 0.42f, 0.12f, 0.88f);
                Rect staleRect = new Rect(imageRect.x + 8, imageRect.y + 8, 84, 20);
                EditorGUI.DrawRect(staleRect, staleColor);
                var staleStyle = new GUIStyle(EditorStyles.miniLabel) {
                    alignment = TextAnchor.MiddleCenter
                };
                staleStyle.normal.textColor = Color.white;
                GUI.Label(staleRect, "Preview stale", staleStyle);
            }

            DrawDistanceAnchorMarker(imageRect);
            DrawPreviewNorthIndicator(imageRect);
            DrawPreviewHoverInfo(imageRect, panelRect);

            Rect resizeHandleRect = GetPreviewResizeHandleRect(panelRect);
            EditorGUIUtility.AddCursorRect(resizeHandleRect, MouseCursor.ResizeUpLeft);
        }

        static Rect GetPreviewResizeHandleRect(Rect panelRect) {
            return new Rect(
                panelRect.xMax - PreviewResizeHandleSize,
                panelRect.yMax - PreviewResizeHandleSize,
                PreviewResizeHandleSize,
                PreviewResizeHandleSize
            );
        }

        static Rect GetPreviewResizeHandleRect(Vector2 panelSize) {
            return GetPreviewResizeHandleRect(new Rect(0f, 0f, panelSize.x, panelSize.y));
        }

        void HandlePreviewInput(Rect imageRect) {
            Event e = Event.current;
            if (e == null) return;

            if (previewPanning) {
                EditorGUIUtility.AddCursorRect(imageRect, MouseCursor.Pan);
            }
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.ScrollWheel && imageRect.Contains(e.mousePosition)) {
                float zoomFactor = e.delta.y > 0 ? 1.15f : 1f / 1.15f;
                previewSize *= zoomFactor;
                previewSize.x = Mathf.Clamp(previewSize.x, 8f, 100000f);
                previewSize.y = Mathf.Clamp(previewSize.y, 8f, 100000f);
                RefreshPreview(forceRefresh: true);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && imageRect.Contains(e.mousePosition)) {
                previewPanning = true;
                previewPanMousePos = e.mousePosition;
                GUIUtility.hotControl = controlId;
                e.Use();
                return;
            }

            if (previewPanning && e.type == EventType.MouseDrag) {
                if (previewFollowAnchor) {
                    previewFollowAnchor = false;
                    previewContainer?.MarkDirtyRepaint();
                }
                Vector2 delta = e.mousePosition - previewPanMousePos;
                previewPanMousePos = e.mousePosition;
                previewCenter.x -= delta.x / imageRect.width * previewSize.x;
                previewCenter.y -= delta.y / imageRect.height * previewSize.y;
                RefreshPreview(forceRefresh: true);
                e.Use();
                return;
            }

            if (previewPanning && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp)) {
                previewPanning = false;
                GUIUtility.hotControl = 0;
                e.Use();
                return;
            }
        }

        void RefreshPreview(bool forceRefresh = false) {
            if (currentGenerator == null || graphView == null) {
                previewStatus = "No generator selected.";
                previewSnapshot = default;
                return;
            }

            if (!forceRefresh && !previewDirty && terrainPreviewTexture != null) {
                return;
            }

            var snapshot = graphView.BuildPreviewSnapshot();
            previewSnapshot = snapshot;
            ResolvePreviewWorldIfLoaded(currentGenerator);
            Rect areaXZ = new Rect(
                previewCenter.x - previewSize.x * 0.5f,
                previewCenter.y - previewSize.y * 0.5f,
                previewSize.x,
                previewSize.y
            );
            terrainPreviewTexture = TerrainGraphPreviewRenderer.Render(currentGenerator, snapshot.steps, snapshot.terminalStepIndex,
                snapshot.moistureTerminalStepIndex, previewWorld, areaXZ, previewResolution, terrainPreviewTexture,
                previewMode, out previewStatus);
            previewDirty = false;

            previewContainer?.MarkDirtyRepaint();
        }

        void DrawPreviewHeaderButtonContent(Rect buttonRect, Texture icon, string fallbackText) {
            if (icon != null) {
                const float iconSize = 12f;
                Rect iconRect = new Rect(
                    Mathf.Round(buttonRect.x + (buttonRect.width - iconSize) * 0.5f),
                    Mathf.Round(buttonRect.y + (buttonRect.height - iconSize) * 0.5f),
                    iconSize,
                    iconSize
                );
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                return;
            }

            var style = new GUIStyle(EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = EditorStyles.label.normal.textColor;
            GUI.Label(buttonRect, fallbackText, style);
        }

        void DrawPreviewTargetButtonContent(Rect buttonRect, bool active) {
            Color strokeColor;
            if (!GUI.enabled) {
                strokeColor = new Color(0.52f, 0.52f, 0.52f, 0.9f);
            } else if (active) {
                strokeColor = new Color(0.96f, 0.74f, 0.22f, 1f);
            } else {
                strokeColor = new Color(0.86f, 0.86f, 0.86f, 0.95f);
            }

            Vector2 center = buttonRect.center;
            const float outerRadius = 4.5f;
            const float innerRadius = 1.5f;
            const float armLength = 6.5f;
            const float gap = 2.5f;

            Handles.BeginGUI();
            Handles.color = strokeColor;
            Handles.DrawWireDisc(center, Vector3.forward, outerRadius);
            Handles.DrawWireDisc(center, Vector3.forward, innerRadius);
            Handles.DrawLine(
                new Vector3(center.x - armLength, center.y, 0f),
                new Vector3(center.x - gap, center.y, 0f)
            );
            Handles.DrawLine(
                new Vector3(center.x + gap, center.y, 0f),
                new Vector3(center.x + armLength, center.y, 0f)
            );
            Handles.DrawLine(
                new Vector3(center.x, center.y - armLength, 0f),
                new Vector3(center.x, center.y - gap, 0f)
            );
            Handles.DrawLine(
                new Vector3(center.x, center.y + gap, 0f),
                new Vector3(center.x, center.y + armLength, 0f)
            );
            Handles.EndGUI();
        }

        bool TryGetDistanceAnchorMarkerInfo(Rect imageRect, out Vector2 markerCenter, out Vector2 markerDirection, out string anchorName) {
            markerCenter = default;
            markerDirection = new Vector2(0f, -1f);
            anchorName = null;

            if (!TryGetDistanceAnchorWorldPosition(out Vector3 anchorWorldPos, out anchorName)) return false;

            Rect areaXZ = new Rect(
                previewCenter.x - previewSize.x * 0.5f,
                previewCenter.y - previewSize.y * 0.5f,
                previewSize.x,
                previewSize.y
            );
            if (areaXZ.width <= 0f || areaXZ.height <= 0f) return false;
            if (anchorWorldPos.x < areaXZ.xMin || anchorWorldPos.x > areaXZ.xMax || anchorWorldPos.z < areaXZ.yMin || anchorWorldPos.z > areaXZ.yMax) return false;

            float tx = Mathf.InverseLerp(areaXZ.xMin, areaXZ.xMax, anchorWorldPos.x);
            float tz = Mathf.InverseLerp(areaXZ.yMin, areaXZ.yMax, anchorWorldPos.z);
            markerCenter = new Vector2(
                Mathf.Lerp(imageRect.xMin, imageRect.xMax, tx),
                Mathf.Lerp(imageRect.yMax, imageRect.yMin, tz)
            );

            Vector3 forward3D = Vector3.forward;
            if (!EditorApplication.isPlaying && SceneView.lastActiveSceneView != null) {
                forward3D = SceneView.lastActiveSceneView.camera.transform.forward;
            } else {
                Transform anchorTransform = VoxelPlayEnvironment.instance != null ? VoxelPlayEnvironment.instance.distanceAnchor : null;
                if (anchorTransform != null) {
                    forward3D = anchorTransform.forward;
                }
            }
            Vector2 previewForward = new Vector2(forward3D.x, -forward3D.z);
            if (previewForward.sqrMagnitude > 0.0001f) {
                markerDirection = previewForward.normalized;
            }

            return true;
        }

        void DrawDistanceAnchorMarker(Rect imageRect) {
            if (!TryGetDistanceAnchorMarkerInfo(imageRect, out Vector2 markerCenter, out Vector2 markerDirection, out _)) return;

            Vector2 perpendicular = new Vector2(-markerDirection.y, markerDirection.x);
            Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
            Color outlineColor = new Color(0.12f, 0.02f, 0.02f, 0.98f);
            Color fillColor = new Color(1f, 0.92f, 0.16f, 1f);
            Color centerColor = new Color(0.92f, 0.18f, 0.12f, 1f);
            Color crossColor = new Color(1f, 1f, 1f, 0.95f);
            const float circleOutlineRadius = 5.5f;
            const float circleRadius = 4.25f;
            const float circleCoreRadius = 1.2f;
            const float triangleLength = 16f;
            const float triangleOutlineHalfWidth = 11f;
            const float triangleHalfWidth = 8.5f;

            // The circle marks the anchor position; the triangle fans out in the facing direction.
            Vector2 spreadCenter = markerCenter + markerDirection * triangleLength;
            Vector3[] shadowTriangle = {
                new Vector3(markerCenter.x + 1f, markerCenter.y + 1f, 0f),
                new Vector3(spreadCenter.x - perpendicular.x * triangleOutlineHalfWidth + 1f, spreadCenter.y - perpendicular.y * triangleOutlineHalfWidth + 1f, 0f),
                new Vector3(spreadCenter.x + perpendicular.x * triangleOutlineHalfWidth + 1f, spreadCenter.y + perpendicular.y * triangleOutlineHalfWidth + 1f, 0f)
            };
            Vector3[] outlineTriangle = {
                new Vector3(markerCenter.x, markerCenter.y, 0f),
                new Vector3(spreadCenter.x - perpendicular.x * triangleOutlineHalfWidth, spreadCenter.y - perpendicular.y * triangleOutlineHalfWidth, 0f),
                new Vector3(spreadCenter.x + perpendicular.x * triangleOutlineHalfWidth, spreadCenter.y + perpendicular.y * triangleOutlineHalfWidth, 0f)
            };
            Vector3[] triangle = {
                new Vector3(markerCenter.x, markerCenter.y, 0f),
                new Vector3(spreadCenter.x - perpendicular.x * triangleHalfWidth, spreadCenter.y - perpendicular.y * triangleHalfWidth, 0f),
                new Vector3(spreadCenter.x + perpendicular.x * triangleHalfWidth, spreadCenter.y + perpendicular.y * triangleHalfWidth, 0f)
            };
            Vector3 circleCenter = new Vector3(markerCenter.x, markerCenter.y, 0f);

            Handles.BeginGUI();
            Handles.color = shadowColor;
            Handles.DrawAAConvexPolygon(shadowTriangle);
            Handles.color = outlineColor;
            Handles.DrawAAConvexPolygon(outlineTriangle);
            Handles.color = fillColor;
            Handles.DrawAAConvexPolygon(triangle);
            Handles.color = outlineColor;
            Handles.DrawSolidDisc(circleCenter + new Vector3(0.5f, 0.5f, 0f), Vector3.forward, circleOutlineRadius);
            Handles.color = centerColor;
            Handles.DrawSolidDisc(circleCenter, Vector3.forward, circleRadius);
            Handles.color = crossColor;
            Handles.DrawSolidDisc(circleCenter, Vector3.forward, circleCoreRadius);
            Handles.EndGUI();
        }

        void DrawPreviewNorthIndicator(Rect imageRect) {
            Rect badgeRect = new Rect(imageRect.xMax - 30f, imageRect.y + 8f, 22f, 30f);
            Rect shadowRect = new Rect(badgeRect.x + 1f, badgeRect.y + 1f, badgeRect.width, badgeRect.height);
            Rect bodyRect = badgeRect;
            bodyRect.height -= 2f;

            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(
                shadowRect,
                new Color(0f, 0f, 0f, 0.22f),
                Color.clear
            );
            Handles.DrawSolidRectangleWithOutline(
                bodyRect,
                new Color(0.08f, 0.08f, 0.08f, 0.92f),
                new Color(0.4f, 0.4f, 0.4f, 0.95f)
            );

            Vector2 center = new Vector2(bodyRect.center.x, bodyRect.y + 17f);
            Vector3 tip = new Vector3(center.x, center.y - 2f, 0f);
            Vector3 left = new Vector3(center.x - 4f, center.y + 4f, 0f);
            Vector3 right = new Vector3(center.x + 4f, center.y + 4f, 0f);
            Handles.color = new Color(0.94f, 0.94f, 0.94f, 1f);
            Handles.DrawLine(new Vector3(center.x, center.y + 6f, 0f), new Vector3(center.x, center.y - 1f, 0f));
            Handles.DrawAAConvexPolygon(tip, left, right);
            Handles.EndGUI();

            var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel) {
                alignment = TextAnchor.UpperCenter
            };
            labelStyle.normal.textColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            GUI.Label(new Rect(bodyRect.x, bodyRect.y + 1f, bodyRect.width, 16f), "N", labelStyle);
        }

        void DrawPreviewHoverInfo(Rect imageRect, Rect panelRect) {
            if (!previewHoverActive || currentGenerator == null || graphView == null) return;
            if (!imageRect.Contains(previewHoverLocalPosition)) return;
            if (previewDirty) return;

            var snapshot = previewSnapshot;
            if (snapshot.steps == null) return;
            float tx = imageRect.width > 0f ? Mathf.Clamp01((previewHoverLocalPosition.x - imageRect.x) / imageRect.width) : 0f;
            float tz = imageRect.height > 0f ? Mathf.Clamp01((previewHoverLocalPosition.y - imageRect.y) / imageRect.height) : 0f;
            double worldX = previewCenter.x - previewSize.x * 0.5f + previewSize.x * tx;
            double worldZ = previewCenter.y - previewSize.y * 0.5f + previewSize.y * (1f - tz);

            if (previewWorld == null) {
                ResolvePreviewWorldIfLoaded(currentGenerator);
            }

            if (!TerrainGraphPreviewRenderer.TrySample(currentGenerator, snapshot.steps, snapshot.terminalStepIndex,
                snapshot.moistureTerminalStepIndex, worldX, worldZ, previewWorld, out var sample)) {
                return;
            }

            string biomeName = sample.biome != null ? sample.biome.name : "Unknown";
            string anchorLine = string.Empty;
            if (TryGetDistanceAnchorMarkerInfo(imageRect, out Vector2 markerCenter, out _, out string anchorName)) {
                float dx = previewHoverLocalPosition.x - markerCenter.x;
                float dy = previewHoverLocalPosition.y - markerCenter.y;
                if (dx * dx + dy * dy <= 16f * 16f) {
                    anchorLine = $"Anchor {anchorName}\n";
                }
            }
            string text = $"{anchorLine}X/Z {sample.worldX:0.0}, {sample.worldZ:0.0}\nAlt {sample.altitudeMeters:0.0}m  N {sample.altitudeNormalized:0.###}\nM {sample.moisture:0.###}  Biome {biomeName}";

            var style = new GUIStyle(EditorStyles.miniLabel) {
                fontSize = 10,
                wordWrap = false,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };
            style.normal.textColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            Vector2 size = style.CalcSize(new GUIContent(text));
            const float tooltipPaddingX = 6f;
            const float tooltipPaddingY = 4f;
            Rect tooltipRect = new Rect(
                previewHoverLocalPosition.x + 14f,
                previewHoverLocalPosition.y + 14f,
                Mathf.Max(152f, size.x + tooltipPaddingX * 2f),
                size.y + tooltipPaddingY * 2f
            );
            tooltipRect.x = Mathf.Min(tooltipRect.x, panelRect.width - tooltipRect.width - 8f);
            tooltipRect.y = Mathf.Min(tooltipRect.y, panelRect.height - tooltipRect.height - 8f);
            tooltipRect.x = Mathf.Max(8f, tooltipRect.x);
            tooltipRect.y = Mathf.Max(8f, tooltipRect.y);

            Rect shadowRect = new Rect(tooltipRect.x + 1f, tooltipRect.y + 1f, tooltipRect.width, tooltipRect.height);
            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(
                shadowRect,
                new Color(0f, 0f, 0f, 0.28f),
                Color.clear
            );
            Handles.DrawSolidRectangleWithOutline(
                tooltipRect,
                new Color(0.06f, 0.06f, 0.06f, 0.96f),
                new Color(0.42f, 0.42f, 0.42f, 0.95f)
            );
            Handles.EndGUI();

            GUI.Label(
                new Rect(
                    tooltipRect.x + tooltipPaddingX,
                    tooltipRect.y + tooltipPaddingY,
                    tooltipRect.width - tooltipPaddingX * 2f,
                    tooltipRect.height - tooltipPaddingY * 2f
                ),
                text,
                style
            );
        }

        void QueuePreviewRefresh() {
            if (!previewVisible || previewContainer == null || previewRefreshQueued) return;
            previewRefreshQueued = true;
            previewContainer.schedule.Execute(() => {
                previewRefreshQueued = false;
                if (!previewVisible || currentGenerator == null || graphView == null || !previewDirty) return;
                RefreshPreview(forceRefresh: true);
            }).StartingIn(75);
        }

        void QueueSyncWorldRefresh() {
            if (syncWorldRefreshQueued || previewContainer == null) return;
            syncWorldRefreshQueued = true;
            previewContainer.schedule.Execute(() => {
                syncWorldRefreshQueued = false;
                if (!syncEnabled || currentGenerator == null || graphView == null) return;
                SyncRefreshWorldFromGraph();
            }).StartingIn(150);
        }

        bool SyncRefreshWorldFromGraph(bool forceRefresh = false) {
            if (currentGenerator == null || graphView == null) return false;

            var snapshot = graphView.BuildPreviewSnapshot();
            var steps = snapshot.steps;
            int altIdx = snapshot.terminalStepIndex;
            int moistIdx = snapshot.moistureTerminalStepIndex;

            bool changed = forceRefresh || syncCachedAltitudes == null || syncCachedMoistures == null;
            float[] newAltitudes = new float[SyncSampleCount];
            float[] newMoistures = new float[SyncSampleCount];

            for (int i = 0; i < SyncSampleCount; i++) {
                double sx = syncSampleCoords[i * 2];
                double sz = syncSampleCoords[i * 2 + 1];
                TerrainGraphPreviewRenderer.Evaluate(currentGenerator, steps, altIdx, moistIdx, sx, sz, out float alt, out float moist);
                newAltitudes[i] = alt;
                newMoistures[i] = moist;
                if (!changed && syncCachedAltitudes != null && syncCachedMoistures != null) {
                    if (!Mathf.Approximately(alt, syncCachedAltitudes[i]) || !Mathf.Approximately(moist, syncCachedMoistures[i])) {
                        changed = true;
                    }
                }
            }

            syncCachedAltitudes = newAltitudes;
            syncCachedMoistures = newMoistures;

            if (!changed) return false;

            var env = VoxelPlayEnvironment.instance;
            if (env == null || env.world == null || env.world.terrainGenerator != currentGenerator) return false;

            EnsureCurrentGeneratorBackup();
            graphView.SaveToGenerator();
            MarkCurrentGeneratorBackupDirty();
            env.InvalidateTerrainCaches(currentGenerator);
            env.ReloadWorld(keepWorldChanges: false);
            return true;
        }

        void UpdateWorldNow() {
            SyncEnsureRenderInEditor();
            if (!SyncRefreshWorldFromGraph(forceRefresh: true)) {
                ShowNotification(new GUIContent("No loaded world uses this generator."));
                return;
            }
            RemoveNotification();
        }

        void SyncEnsureRenderInEditor() {
            var env = VoxelPlayEnvironment.instance;
            if (env == null) return;
            if (env.world == null || env.world.terrainGenerator != currentGenerator) return;
            if (!env.renderInEditor) {
                Undo.RecordObject(env, "Enable Render In Editor");
                env.renderInEditor = true;
                EditorUtility.SetDirty(env);
            }
        }

        void SyncPreviewArea(TerrainDefaultGenerator generator) {
            if (generator == null) return;

            if (generator.GetTerrainBounds(out Vector3 center, out Vector3 size)) {
                previewCenter = new Vector2(center.x, center.z);
                previewSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.z));
            }
        }

        void SyncPreviewDependencies(TerrainDefaultGenerator generator) {
            if (generator == null) return;
            previewObservedWaterLevel = generator.waterLevel;
            previewObservedMaxHeight = generator.maxHeight;
            previewObservedAddWater = generator.addWater;
        }

        bool HavePreviewDependenciesChanged(TerrainDefaultGenerator generator) {
            if (generator == null) return false;
            return !Mathf.Approximately(previewObservedWaterLevel, generator.waterLevel)
                || !Mathf.Approximately(previewObservedMaxHeight, generator.maxHeight)
                || previewObservedAddWater != generator.addWater;
        }

        void OnPreviewOverlayPointerDown(PointerDownEvent evt) {
            if (previewContainer == null || evt.button != 0) return;

            Vector2 local = evt.localPosition;
            Vector2 panelSize = GetPreviewPanelDisplaySize();
            if (panelSize.x <= 0f || panelSize.y <= 0f) {
                panelSize = new Vector2(PreviewPanelDefaultWidth, PreviewPanelDefaultHeight);
            }
            if (GetPreviewResizeHandleRect(panelSize).Contains(local)) {
                previewOverlayResizing = true;
                previewOverlayResizeStartPointer = rootVisualElement.WorldToLocal(evt.position);
                previewOverlayResizeStartSize = panelSize;
                previewContainer.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

            float width = panelSize.x;
            bool inHeader = local.y <= 28f && local.x < width - 38f;
            if (!inHeader) return;

            previewOverlayDragging = true;
            previewOverlayDragOffset = local;
            previewContainer.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnPreviewOverlayPointerMove(PointerMoveEvent evt) {
            if (previewContainer == null) return;

            previewHoverActive = true;
            previewHoverLocalPosition = evt.localPosition;
            previewContainer.MarkDirtyRepaint();

            if (!previewContainer.HasPointerCapture(evt.pointerId)) return;

            if (previewOverlayResizing) {
                Vector2 pointerLocal = rootVisualElement.WorldToLocal(evt.position);
                previewPanelSize = ClampPreviewPanelSizeToBounds(previewOverlayResizeStartSize + (pointerLocal - previewOverlayResizeStartPointer), rootVisualElement.contentRect.size);
                ApplyPreviewContainerSize(savePreferences: true);
                ApplyPreviewContainerPosition();
                previewContainer.MarkDirtyRepaint();
                evt.StopPropagation();
                return;
            }

            if (!previewOverlayDragging) return;

            Vector2 dragPointerLocal = rootVisualElement.WorldToLocal(evt.position);
            Rect rect = new Rect(dragPointerLocal - previewOverlayDragOffset, GetPreviewPanelDisplaySize());
            rect = ClampOverlayRect(rect, rootVisualElement.contentRect.size);
            previewPanelPosition = rect.position;
            previewPanelPositionInitialized = true;
            previewPanelManuallyPositioned = true;
            previewContainer.style.left = rect.x;
            previewContainer.style.top = rect.y;
            evt.StopPropagation();
        }

        void OnPreviewOverlayPointerUp(PointerUpEvent evt) {
            if (previewContainer == null || !previewContainer.HasPointerCapture(evt.pointerId)) return;
            previewOverlayDragging = false;
            previewOverlayResizing = false;
            previewContainer.ReleasePointer(evt.pointerId);
            previewHoverActive = true;
            previewHoverLocalPosition = evt.localPosition;
            previewContainer.MarkDirtyRepaint();
            evt.StopPropagation();
        }

        void OnPreviewOverlayPointerEnter(PointerEnterEvent evt) {
            if (previewContainer == null) return;
            previewHoverActive = true;
            previewHoverLocalPosition = evt.localPosition;
            previewContainer.MarkDirtyRepaint();
        }

        void OnPreviewOverlayPointerLeave(PointerLeaveEvent evt) {
            if (previewContainer == null) return;
            previewHoverActive = false;
            previewContainer.MarkDirtyRepaint();
        }

        void OnPreviewOverlayPointerCaptureOut(PointerCaptureOutEvent evt) {
            previewOverlayDragging = false;
            previewOverlayResizing = false;
        }

        void OnWindowGeometryChanged(GeometryChangedEvent evt) {
            ApplyPreviewContainerSize();
            ApplyPreviewContainerPosition();
            if (graphView != null) {
                minimapRect = ClampOverlayRect(minimapRect, graphView.layout.size);
                graphView.SetMinimapRect(minimapRect);
            }
        }

        void OnEditorUpdate() {
            if (!previewVisible || previewContainer == null || currentGenerator == null) return;

            if (previewFollowAnchor) {
                TrySyncPreviewCenterToAssignedAnchor(refreshPreviewIfChanged: true);
            }
            if (previewDirty) return;

            bool hasAnchorInPreview = TryGetObservedAnchorPose(out Vector3 anchorWorldPos, out Vector3 anchorForward, out bool anchorInPreview);
            if (!hasAnchorInPreview) {
                if (previewAnchorPoseCached) {
                    ResetPreviewAnchorTracking();
                    previewContainer.MarkDirtyRepaint();
                }
                return;
            }

            if (!previewAnchorPoseCached
                || (previewObservedAnchorWorldPos - anchorWorldPos).sqrMagnitude > 0.0001f
                || (previewObservedAnchorForward - anchorForward).sqrMagnitude > 0.0001f
                || previewObservedAnchorInPreview != anchorInPreview) {
                previewAnchorPoseCached = true;
                previewObservedAnchorWorldPos = anchorWorldPos;
                previewObservedAnchorForward = anchorForward;
                previewObservedAnchorInPreview = anchorInPreview;
                previewContainer.MarkDirtyRepaint();
            }
        }

        bool TryGetObservedAnchorPose(out Vector3 anchorWorldPos, out Vector3 anchorForward, out bool anchorInPreview) {
            anchorWorldPos = default;
            anchorForward = Vector3.forward;
            anchorInPreview = false;

            if (!TryGetDistanceAnchorWorldPosition(out anchorWorldPos, out _)) {
                return false;
            }

            var env = VoxelPlayEnvironment.instance;
            if (!EditorApplication.isPlaying && SceneView.lastActiveSceneView != null) {
                anchorForward = SceneView.lastActiveSceneView.camera.transform.forward;
            } else {
                Transform anchorTransform = env.distanceAnchor;
                if (anchorTransform != null) {
                    anchorForward = anchorTransform.forward;
                }
            }

            Rect areaXZ = new Rect(
                previewCenter.x - previewSize.x * 0.5f,
                previewCenter.y - previewSize.y * 0.5f,
                previewSize.x,
                previewSize.y
            );
            anchorInPreview = areaXZ.width > 0f
                && areaXZ.height > 0f
                && anchorWorldPos.x >= areaXZ.xMin
                && anchorWorldPos.x <= areaXZ.xMax
                && anchorWorldPos.z >= areaXZ.yMin
                && anchorWorldPos.z <= areaXZ.yMax;

            return true;
        }

        bool TryGetAssignedDistanceAnchorPosition(out Vector3 anchorWorldPos, out string anchorName) {
            return TryGetDistanceAnchorWorldPosition(out anchorWorldPos, out anchorName, requireAssignedAnchor: true);
        }

        bool TrySyncPreviewCenterToAssignedAnchor(bool refreshPreviewIfChanged) {
            if (!TryGetAssignedDistanceAnchorPosition(out Vector3 anchorWorldPos, out _)) {
                return false;
            }

            Vector2 targetCenter = new Vector2(anchorWorldPos.x, anchorWorldPos.z);
            float pixelWorldSizeX = previewResolution > 1 ? previewSize.x / (previewResolution - 1f) : previewSize.x;
            float pixelWorldSizeY = previewResolution > 1 ? previewSize.y / (previewResolution - 1f) : previewSize.y;
            float refreshThreshold = Mathf.Max(pixelWorldSizeX, pixelWorldSizeY) * PreviewFollowAnchorRefreshThresholdPixels;
            if ((previewCenter - targetCenter).sqrMagnitude <= refreshThreshold * refreshThreshold) {
                return true;
            }

            previewCenter = targetCenter;
            if (refreshPreviewIfChanged && previewVisible && previewContainer != null && currentGenerator != null) {
                previewDirty = true;
                previewStatus = "Updating preview...";
                QueuePreviewRefresh();
                previewContainer.MarkDirtyRepaint();
            }
            return true;
        }

        bool TryGetDistanceAnchorWorldPosition(out Vector3 anchorWorldPos, out string anchorName, bool requireAssignedAnchor = false) {
            anchorWorldPos = default;
            anchorName = null;

            var env = VoxelPlayEnvironment.instance;
            if (currentGenerator == null || env == null || env.world == null || env.world.terrainGenerator != currentGenerator) {
                return false;
            }

            Transform anchorTransform = env.distanceAnchor;
            if (requireAssignedAnchor && anchorTransform == null) {
                return false;
            }

            anchorWorldPos = env.currentAnchorPosWS;
            if (anchorWorldPos == Vector3.zero && anchorTransform != null) {
                anchorWorldPos = anchorTransform.position;
            }

            if (anchorTransform == null && anchorWorldPos == Vector3.zero) {
                return false;
            }

            anchorName = anchorTransform != null ? anchorTransform.gameObject.name : "Distance Anchor";
            return true;
        }

        void ResetPreviewAnchorTracking() {
            previewAnchorPoseCached = false;
            previewObservedAnchorWorldPos = default;
            previewObservedAnchorForward = Vector3.forward;
            previewObservedAnchorInPreview = false;
        }

        static Rect ClampOverlayRect(Rect rect, Vector2 bounds) {
            if (bounds.x > 0f) {
                rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, bounds.x - rect.width));
            }
            if (bounds.y > 0f) {
                rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, bounds.y - rect.height));
            }
            return rect;
        }

        void OnInspectorUpdate() {
            if (graphView == null || currentGenerator == null) return;
            graphView.RefreshNodeTerrainContext();
            if (HavePreviewDependenciesChanged(currentGenerator)) {
                SyncPreviewDependencies(currentGenerator);
                previewDirty = true;
                previewStatus = "Updating preview...";
                QueuePreviewRefresh();
                previewContainer?.MarkDirtyRepaint();
            }
        }

        void SaveDraftSession() {
            if (graphView == null || currentGenerator == null) return;
            var snapshot = graphView.BuildEditorSnapshot();

            EnsureSessionState();
            Undo.RegisterCompleteObjectUndo(sessionState, "Terrain Graph Edit");
            sessionState.generator = currentGenerator;
            sessionState.snapshot = snapshot;
            sessionState.hasUnsavedSnapshot = true;
            EditorUtility.SetDirty(sessionState);

            // Also store on the window itself so the draft survives Play Mode
            // domain reloads even if the HideAndDontSave sessionState is lost.
            hasDraftSnapshot = true;
            draftSnapshotGenerator = currentGenerator;
            draftSnapshot = snapshot;
        }

        void ClearDraftSession() {
            if (sessionState != null) {
                Undo.RegisterCompleteObjectUndo(sessionState, "Terrain Graph Save");
                sessionState.generator = currentGenerator;
                sessionState.snapshot = new TerrainGraphView.GraphSnapshot();
                sessionState.hasUnsavedSnapshot = false;
                EditorUtility.SetDirty(sessionState);
            }
            hasDraftSnapshot = false;
            draftSnapshotGenerator = null;
            draftSnapshot = default;
        }

        bool HasDraftSessionFor(TerrainDefaultGenerator generator) {
            if (hasDraftSnapshot && draftSnapshotGenerator == generator) return true;
            return sessionState != null
                && sessionState.hasUnsavedSnapshot
                && sessionState.generator == generator;
        }

        void OnUndoRedoPerformed() {
            if (graphView == null || currentGenerator == null) return;

            // sessionState is the authority during Undo (it participates in Undo).
            // The window-local draft is only a domain-reload backup.
            if (sessionState != null && sessionState.hasUnsavedSnapshot && sessionState.generator == currentGenerator) {
                graphView.LoadFromSnapshot(currentGenerator, sessionState.snapshot, true);
                hasDraftSnapshot = true;
                draftSnapshotGenerator = currentGenerator;
                draftSnapshot = sessionState.snapshot;
            } else {
                // Undo reverted to no-draft state; clear window-local copy too.
                hasDraftSnapshot = false;
                draftSnapshotGenerator = null;
                draftSnapshot = default;
                graphView.LoadFromGenerator(currentGenerator);
            }

            previewSnapshot = default;
            previewWorld = null;
            previewWorldLookupAttempted = false;
            previewDirty = true;
            previewStatus = "Updating preview...";
            QueuePreviewRefresh();
            previewContainer?.MarkDirtyRepaint();
            UpdateTitle();
            RefreshDiagnosticsPanel();
            if (syncEnabled) {
                QueueSyncWorldRefresh();
            }
        }

        public override void SaveChanges() {
            Save();
            base.SaveChanges();
        }

        public override void DiscardChanges() {
            DiscardPendingChanges(reloadWorld: true);
            base.DiscardChanges();
        }

        void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                // Flush pending draft so unsaved changes survive the domain reload.
                if (graphView != null && graphView.IsDirty) {
                    SaveDraftSession();
                }
            }
        }

        void OnDisable() {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (terrainPreviewTexture != null) {
                DestroyImmediate(terrainPreviewTexture);
                terrainPreviewTexture = null;
            }
        }

        void ResolvePreviewWorldIfLoaded(TerrainDefaultGenerator generator) {
            if (generator == null) {
                previewWorld = null;
                previewWorldLookupAttempted = false;
                return;
            }

            if (previewWorld != null && previewWorld.terrainGenerator == generator) {
                return;
            }

            previewWorld = null;

            var env = VoxelPlayEnvironment.instance;
            if (env != null && env.world != null && env.world.terrainGenerator == generator) {
                previewWorld = env.world;
                return;
            }

            if (previewWorldLookupAttempted) return;
            previewWorldLookupAttempted = true;

            var loadedWorlds = Resources.FindObjectsOfTypeAll<WorldDefinition>();
            for (int i = 0; i < loadedWorlds.Length; i++) {
                var world = loadedWorlds[i];
                if (world != null && world.terrainGenerator == generator) {
                    previewWorld = world;
                    return;
                }
            }
        }
    }
}
