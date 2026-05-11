using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

using UnityEditor.Experimental.GraphView;

namespace VoxelPlay {

    public class TerrainGraphView : GraphView {

        public enum TerrainGraphOutputKind {
            Altitude = 0,
            Moisture = 1
        }

        public TerrainDefaultGenerator generator;
        public Node outputNode;
        public Node moistureOutputNode;
        TerrainGraphSearchWindow searchWindow;
        MiniMap miniMap;
        bool isLoading;
        bool suppressDirtyNotifications;
        bool dirtyPending;
        bool isDraggingMinimap;
        bool initialAutoLayoutQueued;
        int initialAutoLayoutAttempts;
        int initialAutoLayoutPassesRemaining;
        bool initialAutoLayoutHasRun;
        bool graphMutationNotificationQueued;
        bool hasLastPointerPosition;
        Vector2 lastPointerPositionLocal;
        Vector2 minimapDragOffset;
        Rect minimapRect = new Rect(10, 30, 200, 140);
        readonly HashSet<string> completedInitialAutoLayoutKeys = new HashSet<string>();
        string queuedInitialAutoLayoutKey;

        public bool IsDirty { get; private set; }
        public Action OnDirtyChanged;
        public Action OnGraphMutated;
        public Action<Rect> OnMinimapRectChanged;

        public TerrainGraphView() {
            // Standard GraphView manipulators
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());
            RegisterCallback<PointerMoveEvent>(evt => UpdateLastPointerPosition(evt.position));
            RegisterCallback<PointerDownEvent>(evt => UpdateLastPointerPosition(evt.position));

            // Grid background
            var styleSheet = Resources.Load<StyleSheet>("VoxelPlay/TerrainGraphEditor");
            if (styleSheet != null) {
                styleSheets.Add(styleSheet);
            }
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // Search window for node creation
            searchWindow = ScriptableObject.CreateInstance<TerrainGraphSearchWindow>();
            searchWindow.Init(this);
            nodeCreationRequest = ctx => {
                SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), searchWindow);
            };

            // Handle graph changes
            graphViewChanged = OnGraphChanged;
            serializeGraphElements = SerializeSelectedGraphElements;
            canPasteSerializedData = CanPasteTerrainGraphData;
            unserializeAndPaste = UnserializeTerrainGraphData;

            // Style
            style.flexGrow = 1;

            // Minimap
            miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(minimapRect);
            miniMap.RegisterCallback<PointerDownEvent>(OnMinimapPointerDown);
            miniMap.RegisterCallback<PointerMoveEvent>(OnMinimapPointerMove);
            miniMap.RegisterCallback<PointerUpEvent>(OnMinimapPointerUp);
            miniMap.RegisterCallback<PointerCaptureOutEvent>(OnMinimapPointerCaptureOut);
            Add(miniMap);
        }

        public bool MinimapVisible => miniMap != null && miniMap.visible;

        public void ToggleMinimap() {
            SetMinimapVisible(!MinimapVisible);
        }

        public void SetMinimapVisible(bool visible) {
            if (miniMap != null) {
                miniMap.visible = visible;
            }
        }

        public Rect GetMinimapRect() {
            return minimapRect;
        }

        public void SetMinimapRect(Rect rect) {
            minimapRect = rect;
            if (miniMap != null) {
                miniMap.SetPosition(minimapRect);
            }
        }

        public void MarkDirty() {
            if (isLoading) return;
            CancelQueuedInitialAutoLayout(markHandledIfRun: true);
            if (suppressDirtyNotifications) {
                dirtyPending = true;
                return;
            }
            QueueGraphMutationNotification();
            if (!IsDirty) {
                IsDirty = true;
                OnDirtyChanged?.Invoke();
            }
        }

        void QueueGraphMutationNotification() {
            if (graphMutationNotificationQueued) return;
            graphMutationNotificationQueued = true;
            schedule.Execute(() => {
                graphMutationNotificationQueued = false;
                if (!isLoading) {
                    OnGraphMutated?.Invoke();
                }
            });
        }

        void BeginBulkMutation() {
            suppressDirtyNotifications = true;
            dirtyPending = false;
        }

        void EndBulkMutation() {
            suppressDirtyNotifications = false;
            if (dirtyPending) {
                dirtyPending = false;
                MarkDirty();
            }
        }

        void ClearDirty() {
            if (IsDirty) {
                IsDirty = false;
                OnDirtyChanged?.Invoke();
            }
        }

        // --- Port compatibility: any float output can connect to any float input ---

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) {
            var compatible = new List<Port>();
            ports.ForEach(port => {
                if (port == startPort) return;
                if (port.node == startPort.node) return;
                if (port.direction == startPort.direction) return;
                Port outputPort = startPort.direction == Direction.Output ? startPort : port;
                Port inputPort = startPort.direction == Direction.Input ? startPort : port;
                var previewEdge = new Edge {
                    output = outputPort,
                    input = inputPort
                };
                if (WouldIntroduceAltitudeOnlyNodeInMoistureBranch(previewEdge)) return;
                compatible.Add(port);
            });
            return compatible;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            base.BuildContextualMenu(evt);

            VisualElement targetElement = evt.target as VisualElement;
            Edge targetEdge = evt.target as Edge ?? targetElement?.GetFirstAncestorOfType<Edge>();
            var organizableSelection = GetOrganizableSelectionNodes();

            if (evt.target == this || evt.target is GraphView || evt.target is GridBackground) {
                int insertIndex = Mathf.Min(2, evt.menu.MenuItems().Count);
                Vector2 mousePosition = evt.mousePosition;
                evt.menu.InsertAction(insertIndex, "Add Reroute", _ => {
                    CreateRerouteNode(contentViewContainer.WorldToLocal(mousePosition), centerOnPosition: true);
                });
            }

            if (targetEdge != null) {
                Vector2 mousePosition = evt.mousePosition;
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Add Reroute", _ => {
                    InsertRerouteOnEdge(targetEdge, contentViewContainer.WorldToLocal(mousePosition), centerOnPosition: true);
                });
            }

            if (organizableSelection.Count > 1) {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Organize/Align Left", _ => AlignSelectionLeft());
                evt.menu.AppendAction("Organize/Align Top", _ => AlignSelectionTop());
            }
        }

        // --- Node creation ---

        public TerrainStepNode CreateNode(TerrainStepType operation, Vector2 position, bool allowAutoConnect = true) {
            bool shouldAutoConnectToOutput = allowAutoConnect && ShouldAutoConnectFirstNodeToOutput(operation);
            float terrainMaxHeight = generator != null ? generator.maxHeight : 1f;
            var node = new TerrainStepNode(operation, terrainMaxHeight);
            node.SetPosition(new Rect(position.x, position.y, 0, 0));
            node.OnValueChanged = OnNodeValueChanged;
            AddElement(node);
            if (shouldAutoConnectToOutput) {
                var outputInput = GetOutputNodeInput();
                if (outputInput != null) {
                    AddElement(node.OutputPort.ConnectTo(outputInput));
                }
            }
            MarkDirty();
            return node;
        }

        public TerrainRerouteNode CreateRerouteNode(Vector2 position, bool centerOnPosition = false) {
            var reroute = new TerrainRerouteNode();
            if (centerOnPosition) {
                float halfSize = TerrainRerouteNode.NodeSize * 0.5f;
                position -= new Vector2(halfSize, halfSize);
            }
            reroute.SetPosition(new Rect(position.x, position.y, TerrainRerouteNode.NodeSize, TerrainRerouteNode.NodeSize));
            AddElement(reroute);
            MarkDirty();
            return reroute;
        }

        public TerrainRerouteNode InsertRerouteOnEdge(Edge edge, Vector2 position, bool centerOnPosition = false) {
            if (edge == null || edge.output == null || edge.input == null) return null;

            Port sourcePort = edge.output;
            Port targetPort = edge.input;
            sourcePort.Disconnect(edge);
            targetPort.Disconnect(edge);
            edge.output = null;
            edge.input = null;
            RemoveElement(edge);

            var reroute = CreateRerouteNode(position, centerOnPosition);
            AddElement(sourcePort.ConnectTo(reroute.InputPort));
            AddElement(reroute.OutputPort.ConnectTo(targetPort));
            return reroute;
        }

        bool ShouldAutoConnectFirstNodeToOutput(TerrainStepType operation) {
            if (isLoading) return false;
            if (!TerrainStepNode.IsGenerator(operation)) return false;
            if (outputNode == null) return false;

            var outputInput = GetOutputNodeInput(TerrainGraphOutputKind.Altitude);
            if (outputInput == null || outputInput.connected) return false;

            return nodes.ToList().OfType<TerrainStepNode>().Count() == 0;
        }

        public void RefreshNodeTerrainContext() {
            float terrainMaxHeight = generator != null ? generator.maxHeight : 1f;
            foreach (var node in nodes.ToList().OfType<TerrainStepNode>()) {
                node.SetTerrainMaxHeight(terrainMaxHeight);
            }
        }

        void OnMinimapPointerDown(PointerDownEvent evt) {
            if (miniMap == null || evt.button != 0) return;
            isDraggingMinimap = true;
            Vector2 pointerLocal = this.WorldToLocal(evt.position);
            minimapDragOffset = pointerLocal - minimapRect.position;
            miniMap.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnMinimapPointerMove(PointerMoveEvent evt) {
            if (!isDraggingMinimap || miniMap == null || !miniMap.HasPointerCapture(evt.pointerId)) return;
            Vector2 pointerLocal = this.WorldToLocal(evt.position);
            Rect rect = minimapRect;
            rect.position = pointerLocal - minimapDragOffset;
            SetMinimapRect(ClampOverlayRect(rect, layout.size));
            OnMinimapRectChanged?.Invoke(minimapRect);
            evt.StopPropagation();
        }

        void OnMinimapPointerUp(PointerUpEvent evt) {
            if (miniMap == null || !miniMap.HasPointerCapture(evt.pointerId)) return;
            isDraggingMinimap = false;
            miniMap.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnMinimapPointerCaptureOut(PointerCaptureOutEvent evt) {
            isDraggingMinimap = false;
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

        public void CreateOutputNodes(Vector2 altitudePosition, Vector2 moisturePosition) {
            CreateOutputNode(TerrainGraphOutputKind.Altitude, altitudePosition);
            CreateOutputNode(TerrainGraphOutputKind.Moisture, moisturePosition);
        }

        public void CreateOutputNode(TerrainGraphOutputKind kind, Vector2 position) {
            Node existingNode = kind == TerrainGraphOutputKind.Altitude ? outputNode : moistureOutputNode;
            if (existingNode != null) return;

            var node = new Node();
            bool isAltitude = kind == TerrainGraphOutputKind.Altitude;
            node.title = isAltitude ? "Altitude Output" : "Moisture Output";
            node.tooltip = isAltitude
                ? "Final normalized terrain altitude.\nThis value is later scaled by the generator Max Height and then adjusted by water/beach rules."
                : "Final normalized moisture value.\nExpected range is usually 0..1 and is used for biome sampling.\nNodes feeding this output should use normalized or percentage editing, not meters.";
            node.titleContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            node.capabilities &= ~Capabilities.Deletable;

            var inputPort = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
            inputPort.portName = isAltitude ? "Altitude" : "Moisture";
            inputPort.tooltip = isAltitude
                ? "Connect the final step of the altitude graph here.\nExpected range is usually 0..1 before Max Height scaling."
                : "Connect the final step of the moisture graph here.\nExpected range is usually 0..1.\nUse normalized or percentage-edited nodes for this branch.";
            node.inputContainer.Add(inputPort);

            node.SetPosition(new Rect(position.x, position.y, 0, 0));
            node.RefreshExpandedState();
            node.RefreshPorts();
            AddElement(node);

            if (isAltitude) {
                outputNode = node;
            } else {
                moistureOutputNode = node;
            }
        }

        Port GetOutputNodeInput() {
            return GetOutputNodeInput(TerrainGraphOutputKind.Altitude);
        }

        Port GetOutputNodeInput(TerrainGraphOutputKind kind) {
            Node node = kind == TerrainGraphOutputKind.Altitude ? outputNode : moistureOutputNode;
            if (node == null) return null;
            return node.inputContainer.Q<Port>();
        }

        // Sentinel description for auto-inserted Copy steps (never shown in UI)
        const string SYNTHETIC_COPY = "[synthetic]";

        [Serializable]
        public struct GraphSnapshot {
            public bool hasEditorState;
            public int graphVersion;
            public Vector2 altitudeOutputPosition;
            public Vector2 moistureOutputPosition;
            public EditorNodeSnapshot[] editorNodes;
            public TerrainGraphRerouteData[] editorReroutes;
            public EditorEdgeSnapshot[] editorEdges;
            public StepData[] steps;
            public NodeLayout[] nodePositions;
            public NodeLayout[] legacyNodePositions;
            public int terminalStepIndex;
            public int moistureTerminalStepIndex;
        }

        [Serializable]
        public struct EditorNodeSnapshot {
            public TerrainStepType operation;
            public StepData data;
            public Vector2 position;
            public int stepIndex;
        }

        [Serializable]
        public struct EditorEdgeSnapshot {
            public TerrainGraphEndpointKind sourceKind;
            public int sourceIndex;
            public TerrainGraphEndpointKind targetKind;
            public int targetIndex;
            public int targetPortIndex;
        }

        [Serializable]
        struct ClipboardData {
            public ClipboardNodeData[] nodes;
            public ClipboardRerouteData[] reroutes;
            public ClipboardEdgeData[] edges;
        }

        [Serializable]
        struct ClipboardNodeData {
            public TerrainStepType operation;
            public StepData data;
            public Vector2 position;
        }

        [Serializable]
        struct ClipboardRerouteData {
            public Vector2 position;
        }

        [Serializable]
        struct ClipboardEdgeData {
            public TerrainGraphEndpointKind sourceKind;
            public int sourceIndex;
            public TerrainGraphEndpointKind targetKind;
            public int targetIndex;
            public int targetPortIndex;
        }

        // --- Load from generator ---

        public void LoadFromGenerator(TerrainDefaultGenerator gen) {
            Vector2 altitudeOutputPosition = new Vector2(800f, 180f);
            Vector2 moistureOutputPosition = new Vector2(800f, 320f);
            NodeLayout[] layoutPositions = null;
            int graphVersion = gen != null ? gen.graphVersion : 0;

            if (gen != null) {
                if (graphVersion >= 2) {
                    altitudeOutputPosition = gen.graphLayoutV2.altitudeOutputPosition;
                    moistureOutputPosition = gen.graphLayoutV2.moistureOutputPosition;
                    layoutPositions = gen.graphLayoutV2.stepPositions;
                } else {
                    layoutPositions = gen.nodePositions;
                    if (layoutPositions != null && layoutPositions.Length > 0) {
                        altitudeOutputPosition = layoutPositions[0].position;
                        moistureOutputPosition = GetDefaultMoistureOutputPosition(altitudeOutputPosition);
                    }
                }
            }

            LoadFromSnapshot(gen, new GraphSnapshot {
                steps = gen != null && gen.Steps != null ? gen.Steps : Array.Empty<StepData>(),
                nodePositions = layoutPositions,
                terminalStepIndex = gen != null ? gen.terminalStepIndex : -1,
                moistureTerminalStepIndex = gen != null ? gen.moistureTerminalStepIndex : -1,
                graphVersion = graphVersion,
                altitudeOutputPosition = altitudeOutputPosition,
                moistureOutputPosition = moistureOutputPosition,
                editorReroutes = gen != null ? gen.graphEditorStateV2.reroutes : null,
                editorEdges = ConvertPersistedEditorEdges(gen != null ? gen.graphEditorStateV2.edges : null)
            }, false);
        }

        public void LoadFromSnapshot(TerrainDefaultGenerator gen, GraphSnapshot snapshot, bool markDirtyAfterLoad) {
            CancelQueuedInitialAutoLayout(markHandledIfRun: true);
            completedInitialAutoLayoutKeys.Clear();
            generator = gen;
            isLoading = true;

            // Clear existing graph
            DeleteElements(graphElements.ToList());
            outputNode = null;
            moistureOutputNode = null;

            if (snapshot.hasEditorState) {
                LoadEditorSnapshot(snapshot, markDirtyAfterLoad);
                return;
            }

            var steps = snapshot.steps;
            var positions = snapshot.nodePositions;
            int stepCount = steps != null ? steps.Length : 0;
            bool usesGraphLayoutV2 = snapshot.graphVersion >= 2;
            bool hasLegacyPositions = !usesGraphLayoutV2 && positions != null && positions.Length == stepCount + 1;
            bool hasGraphPositions = usesGraphLayoutV2 && positions != null && positions.Length == stepCount;
            bool needsInitialAutoLayout = !usesGraphLayoutV2 || !hasGraphPositions;

            Vector2 altitudeOutputPosition = usesGraphLayoutV2
                ? snapshot.altitudeOutputPosition
                : hasLegacyPositions ? positions[0].position : new Vector2(800f, 180f);
            Vector2 moistureOutputPosition = usesGraphLayoutV2
                ? snapshot.moistureOutputPosition
                : GetDefaultMoistureOutputPosition(altitudeOutputPosition);
            CreateOutputNodes(altitudeOutputPosition, moistureOutputPosition);

            if (steps == null || steps.Length == 0) {
                MaybeInjectLegacyMoistureNode(snapshot);
                isLoading = false;
                IsDirty = false;
                OnDirtyChanged?.Invoke();
                return;
            }

            // Detect synthetic Copies: must have the sentinel description AND precede an op with implicit value flow
            var skipIndices = new HashSet<int>();
            for (int i = 0; i < steps.Length - 1; i++) {
                if (steps[i].operation == TerrainStepType.Copy
                    && steps[i].description == SYNTHETIC_COPY
                    && TerrainStepNode.UsesImplicitValueFlow(steps[i + 1].operation)) {
                    skipIndices.Add(i);
                }
            }

            // Create nodes (skip only synthetic Copy steps)
            var nodes = new TerrainStepNode[steps.Length];
            int nodeIndex = 0;
            for (int i = 0; i < steps.Length; i++) {
                if (skipIndices.Contains(i)) continue;

                float nx = hasGraphPositions ? positions[i].position.x
                    : hasLegacyPositions ? positions[i + 1].position.x
                    : nodeIndex * 250f;
                float ny = hasGraphPositions ? positions[i].position.y
                    : hasLegacyPositions ? positions[i + 1].position.y
                    : 100f;

                var node = CreateNode(steps[i].operation, new Vector2(nx, ny), allowAutoConnect: false);
                node.ApplyFromStepData(steps[i]);
                node.StepIndex = i;
                node.UpdateDisplayTitle();
                nodes[i] = node;
                nodeIndex++;
            }

            if (SnapshotHasPersistedVisualState(snapshot)) {
                LoadPersistedVisualState(snapshot, nodes);
            } else {
                CreateRuntimeConnections(snapshot, nodes, steps, skipIndices);
            }

            isLoading = false;
            IsDirty = markDirtyAfterLoad;
            OnDirtyChanged?.Invoke();

            if (ShouldQueueInitialAutoLayout(gen, needsInitialAutoLayout, out string migrationKey)) {
                QueueInitialAutoLayout(migrationKey);
            }
        }

        void LoadEditorSnapshot(GraphSnapshot snapshot, bool markDirtyAfterLoad) {
            Vector2 altitudeOutputPosition = snapshot.graphVersion >= 2
                ? snapshot.altitudeOutputPosition
                : new Vector2(800f, 180f);
            Vector2 moistureOutputPosition = snapshot.graphVersion >= 2
                ? snapshot.moistureOutputPosition
                : GetDefaultMoistureOutputPosition(altitudeOutputPosition);
            CreateOutputNodes(altitudeOutputPosition, moistureOutputPosition);

            var editorNodes = snapshot.editorNodes ?? Array.Empty<EditorNodeSnapshot>();
            var createdNodes = new TerrainStepNode[editorNodes.Length];
            for (int i = 0; i < editorNodes.Length; i++) {
                var node = CreateNode(editorNodes[i].operation, editorNodes[i].position, allowAutoConnect: false);
                node.ApplyFromStepData(editorNodes[i].data);
                node.StepIndex = editorNodes[i].stepIndex;
                node.UpdateDisplayTitle();
                createdNodes[i] = node;
            }

            var createdReroutes = CreatePersistedReroutes(snapshot.editorReroutes);
            var editorEdges = snapshot.editorEdges ?? Array.Empty<EditorEdgeSnapshot>();
            foreach (var edgeSnapshot in editorEdges) {
                TryCreatePersistedEditorEdge(edgeSnapshot, createdNodes, createdReroutes);
            }

            isLoading = false;
            IsDirty = markDirtyAfterLoad;
            OnDirtyChanged?.Invoke();
        }

        bool ShouldQueueInitialAutoLayout(TerrainDefaultGenerator gen, bool needsInitialAutoLayout, out string migrationKey) {
            migrationKey = null;
            if (!needsInitialAutoLayout) return false;

            if (gen == null) return true;

            string assetPath = AssetDatabase.GetAssetPath(gen);
            migrationKey = !string.IsNullOrEmpty(assetPath)
                ? assetPath
                : gen.GetInstanceID().ToString();

            if (completedInitialAutoLayoutKeys.Contains(migrationKey)) return false;
            if (initialAutoLayoutQueued && queuedInitialAutoLayoutKey == migrationKey) return false;
            return true;
        }

        void QueueInitialAutoLayout(string migrationKey = null) {
            if (initialAutoLayoutQueued) return;
            initialAutoLayoutQueued = true;
            initialAutoLayoutAttempts = 0;
            initialAutoLayoutPassesRemaining = 1;
            initialAutoLayoutHasRun = false;
            queuedInitialAutoLayoutKey = migrationKey;
            EditorApplication.delayCall -= RunQueuedInitialAutoLayout;
            EditorApplication.delayCall += RunQueuedInitialAutoLayout;
        }

        void CancelQueuedInitialAutoLayout(bool markHandledIfRun = false) {
            EditorApplication.delayCall -= RunQueuedInitialAutoLayout;
            if (markHandledIfRun && initialAutoLayoutHasRun && !string.IsNullOrEmpty(queuedInitialAutoLayoutKey)) {
                completedInitialAutoLayoutKeys.Add(queuedInitialAutoLayoutKey);
            }
            initialAutoLayoutQueued = false;
            initialAutoLayoutAttempts = 0;
            initialAutoLayoutPassesRemaining = 0;
            initialAutoLayoutHasRun = false;
            queuedInitialAutoLayoutKey = null;
        }

        void CompleteQueuedInitialAutoLayout() {
            if (initialAutoLayoutHasRun && !string.IsNullOrEmpty(queuedInitialAutoLayoutKey)) {
                completedInitialAutoLayoutKeys.Add(queuedInitialAutoLayoutKey);
            }
            initialAutoLayoutQueued = false;
            initialAutoLayoutAttempts = 0;
            initialAutoLayoutPassesRemaining = 0;
            initialAutoLayoutHasRun = false;
            queuedInitialAutoLayoutKey = null;
        }

        void RunQueuedInitialAutoLayout() {
            EditorApplication.delayCall -= RunQueuedInitialAutoLayout;

            if (panel == null || layout.width <= 0f || layout.height <= 0f) {
                initialAutoLayoutAttempts++;
                if (initialAutoLayoutAttempts < 4) {
                    EditorApplication.delayCall += RunQueuedInitialAutoLayout;
                    return;
                }
                CancelQueuedInitialAutoLayout();
                return;
            }

            bool previousLoadingState = isLoading;
            isLoading = true;
            try {
                AutoLayout();
            } finally {
                isLoading = previousLoadingState;
            }
            initialAutoLayoutHasRun = true;

            initialAutoLayoutPassesRemaining--;
            if (initialAutoLayoutPassesRemaining > 0) {
                EditorApplication.delayCall += RunQueuedInitialAutoLayout;
                return;
            }

            CompleteQueuedInitialAutoLayout();
            ClearDirty();
            FrameAll();
        }

        static Vector2 GetDefaultMoistureOutputPosition(Vector2 altitudeOutputPosition) {
            return altitudeOutputPosition + new Vector2(0f, 140f);
        }

        void MaybeInjectLegacyMoistureNode(GraphSnapshot snapshot) {
            if (generator == null) return;
            if (snapshot.moistureTerminalStepIndex >= 0) return;
            Texture2D legacyMoistureTexture = generator.GetResolvedLegacyMoistureTexture();
            if (legacyMoistureTexture == null) return;

            var moistureOutputInput = GetOutputNodeInput(TerrainGraphOutputKind.Moisture);
            if (moistureOutputInput == null || moistureOutputInput.connected) return;

            Vector2 moistureOutputPosition = moistureOutputNode != null
                ? moistureOutputNode.GetPosition().position
                : GetDefaultMoistureOutputPosition(new Vector2(800f, 180f));
            Vector2 nodePosition = moistureOutputPosition + new Vector2(-260f, 0f);
            var node = CreateNode(TerrainStepType.SampleHeightMapTexture, nodePosition, allowAutoConnect: false);
            node.ApplyFromStepData(new StepData {
                enabled = true,
                operation = TerrainStepType.SampleHeightMapTexture,
                description = generator.moisture != null ? "Legacy Moisture" : "Default Moisture",
                noiseTexture = legacyMoistureTexture,
                frecuency = generator.GetResolvedLegacyMoistureScale(),
                noiseRangeMin = 0f,
                noiseRangeMax = 1f,
                editorHeightUnitMask = TerrainStepNode.GetNormalizedHeightUnitMask(),
                editorHeightPercentMask = 0u
            });
            node.UpdateDisplayTitle();
            AddElement(node.OutputPort.ConnectTo(moistureOutputInput));
        }

        TerrainRerouteNode[] CreatePersistedReroutes(TerrainGraphRerouteData[] rerouteSnapshots) {
            var snapshots = rerouteSnapshots ?? Array.Empty<TerrainGraphRerouteData>();
            var reroutes = new TerrainRerouteNode[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++) {
                var reroute = CreateRerouteNode(snapshots[i].position);
                reroutes[i] = reroute;
            }
            return reroutes;
        }

        bool SnapshotHasPersistedVisualState(GraphSnapshot snapshot) {
            return (snapshot.editorReroutes != null && snapshot.editorReroutes.Length > 0)
                || (snapshot.editorEdges != null && snapshot.editorEdges.Length > 0);
        }

        void LoadPersistedVisualState(GraphSnapshot snapshot, TerrainStepNode[] nodes) {
            var reroutes = CreatePersistedReroutes(snapshot.editorReroutes);
            var editorEdges = snapshot.editorEdges ?? Array.Empty<EditorEdgeSnapshot>();
            for (int i = 0; i < editorEdges.Length; i++) {
                TryCreatePersistedEditorEdge(editorEdges[i], nodes, reroutes);
            }

            if (GetOutputNodeInput(TerrainGraphOutputKind.Moisture)?.connected != true) {
                MaybeInjectLegacyMoistureNode(snapshot);
            }
        }

        void TryCreatePersistedEditorEdge(EditorEdgeSnapshot edgeSnapshot, TerrainStepNode[] stepNodes, TerrainRerouteNode[] reroutes) {
            Port sourcePort = ResolveOutputPort(edgeSnapshot.sourceKind, edgeSnapshot.sourceIndex, stepNodes, reroutes);
            Port targetPort = ResolveInputPort(edgeSnapshot.targetKind, edgeSnapshot.targetIndex, edgeSnapshot.targetPortIndex, stepNodes, reroutes);
            if (sourcePort == null || targetPort == null) return;
            AddElement(sourcePort.ConnectTo(targetPort));
        }

        Port ResolveOutputPort(TerrainGraphEndpointKind kind, int index, TerrainStepNode[] stepNodes, TerrainRerouteNode[] reroutes) {
            switch (kind) {
                case TerrainGraphEndpointKind.StepNode:
                    if (stepNodes != null && index >= 0 && index < stepNodes.Length && stepNodes[index] != null) {
                        return stepNodes[index].OutputPort;
                    }
                    break;
                case TerrainGraphEndpointKind.Reroute:
                    if (reroutes != null && index >= 0 && index < reroutes.Length && reroutes[index] != null) {
                        return reroutes[index].OutputPort;
                    }
                    break;
            }
            return null;
        }

        Port ResolveInputPort(TerrainGraphEndpointKind kind, int index, int targetPortIndex, TerrainStepNode[] stepNodes, TerrainRerouteNode[] reroutes) {
            switch (kind) {
                case TerrainGraphEndpointKind.StepNode:
                    if (stepNodes != null && index >= 0 && index < stepNodes.Length && stepNodes[index] != null) {
                        return targetPortIndex == 1 ? stepNodes[index].InputPortB : stepNodes[index].InputPort;
                    }
                    break;
                case TerrainGraphEndpointKind.Reroute:
                    if (reroutes != null && index >= 0 && index < reroutes.Length && reroutes[index] != null) {
                        return reroutes[index].InputPort;
                    }
                    break;
                case TerrainGraphEndpointKind.AltitudeOutput:
                    return GetOutputNodeInput(TerrainGraphOutputKind.Altitude);
                case TerrainGraphEndpointKind.MoistureOutput:
                    return GetOutputNodeInput(TerrainGraphOutputKind.Moisture);
            }
            return null;
        }

        int FindPreviousActiveNode(TerrainStepNode[] nodes, int fromIndex) {
            for (int i = fromIndex - 1; i >= 0; i--) {
                if (nodes[i] != null) return i;
            }
            return -1;
        }

        void ConnectImplicitValueInput(TerrainStepNode[] nodes, StepData[] steps, HashSet<int> skipIndices, int stepIndex, Port inputPort) {
            if (inputPort == null || stepIndex <= 0) return;

            bool hasImplicitInput = TerrainStepNode.HasImplicitValueAndSingleRefOp(steps[stepIndex].operation)
                ? steps[stepIndex].inputIndex1 >= 0
                : steps[stepIndex].inputIndex0 >= 0;
            if (!hasImplicitInput) return;

            if (skipIndices.Contains(stepIndex - 1)) {
                // Synthetic Copy preceded this: connect to the Copy's real source
                int sourceIdx = steps[stepIndex - 1].inputIndex0;
                if (sourceIdx >= 0 && sourceIdx < steps.Length && nodes[sourceIdx] != null) {
                    AddElement(nodes[sourceIdx].OutputPort.ConnectTo(inputPort));
                }
                return;
            }

            int prevIdx = FindPreviousActiveNode(nodes, stepIndex);
            if (prevIdx >= 0) {
                AddElement(nodes[prevIdx].OutputPort.ConnectTo(inputPort));
            }
        }

        void CreateRuntimeConnections(GraphSnapshot snapshot, TerrainStepNode[] nodes, StepData[] steps, HashSet<int> skipIndices) {
            for (int i = 0; i < steps.Length; i++) {
                if (skipIndices.Contains(i) || nodes[i] == null) continue;
                TerrainStepType op = steps[i].operation;

                if (TerrainStepNode.HasImplicitValueAndSingleRefOp(op)) {
                    ConnectImplicitValueInput(nodes, steps, skipIndices, i, nodes[i].InputPort);
                    int i0 = steps[i].inputIndex0;
                    if (i0 >= 0 && i0 < steps.Length && nodes[i0] != null) {
                        AddElement(nodes[i0].OutputPort.ConnectTo(nodes[i].InputPortB));
                    }
                } else if (TerrainStepNode.IsImplicitValueOp(op)) {
                    ConnectImplicitValueInput(nodes, steps, skipIndices, i, nodes[i].InputPort);
                } else if (TerrainStepNode.HasTwoInputs(op)) {
                    int i0 = steps[i].inputIndex0;
                    int i1 = steps[i].inputIndex1;
                    if (i0 >= 0 && i0 < steps.Length && nodes[i0] != null) {
                        AddElement(nodes[i0].OutputPort.ConnectTo(nodes[i].InputPort));
                    }
                    if (i1 >= 0 && i1 < steps.Length && nodes[i1] != null) {
                        AddElement(nodes[i1].OutputPort.ConnectTo(nodes[i].InputPortB));
                    }
                } else if (TerrainStepNode.IsSingleRefOp(op)) {
                    int i0 = steps[i].inputIndex0;
                    if (i0 >= 0 && i0 < steps.Length && nodes[i0] != null) {
                        AddElement(nodes[i0].OutputPort.ConnectTo(nodes[i].InputPort));
                    }
                }
            }

            int termIdx = snapshot.terminalStepIndex;
            if (snapshot.graphVersion < 2 && (termIdx < 0 || termIdx >= nodes.Length || nodes[termIdx] == null)) {
                for (int i = nodes.Length - 1; i >= 0; i--) {
                    if (nodes[i] != null) { termIdx = i; break; }
                }
            }
            if (termIdx >= 0 && termIdx < nodes.Length && nodes[termIdx] != null) {
                Port outPort = GetOutputNodeInput(TerrainGraphOutputKind.Altitude);
                if (outPort != null) {
                    AddElement(nodes[termIdx].OutputPort.ConnectTo(outPort));
                }
            }

            int moistureTermIdx = snapshot.moistureTerminalStepIndex;
            if (moistureTermIdx >= 0 && moistureTermIdx < nodes.Length && nodes[moistureTermIdx] != null) {
                Port moistureOutPort = GetOutputNodeInput(TerrainGraphOutputKind.Moisture);
                if (moistureOutPort != null) {
                    AddElement(nodes[moistureTermIdx].OutputPort.ConnectTo(moistureOutPort));
                }
            } else {
                MaybeInjectLegacyMoistureNode(snapshot);
            }
        }

        // --- Save to generator ---

        public void SaveToGenerator() {
            if (generator == null) return;
            CancelQueuedInitialAutoLayout(markHandledIfRun: true);

            Undo.RecordObject(generator, "Terrain Graph Change");
            var snapshot = BuildSnapshot(updateNodeTitles: true);
            var editorSnapshot = BuildEditorSnapshot();
            generator.Steps = snapshot.steps;
            generator.nodePositions = snapshot.legacyNodePositions;
            generator.terminalStepIndex = snapshot.terminalStepIndex;
            generator.moistureTerminalStepIndex = snapshot.moistureTerminalStepIndex;
            generator.graphVersion = snapshot.graphVersion;
            generator.graphLayoutV2 = new TerrainGraphLayoutData {
                altitudeOutputPosition = snapshot.altitudeOutputPosition,
                moistureOutputPosition = snapshot.moistureOutputPosition,
                stepPositions = snapshot.nodePositions
            };

            // Convert edge indices from editor node order to step order so they
            // match the nodes array built from generator.Steps during loading.
            var editorNodes = editorSnapshot.editorNodes;
            var persistedEdges = editorSnapshot.editorEdges;
            if (editorNodes != null && persistedEdges != null) {
                for (int i = 0; i < persistedEdges.Length; i++) {
                    var edge = persistedEdges[i];
                    if (edge.sourceKind == TerrainGraphEndpointKind.StepNode
                        && edge.sourceIndex >= 0 && edge.sourceIndex < editorNodes.Length) {
                        edge.sourceIndex = editorNodes[edge.sourceIndex].stepIndex;
                    }
                    if (edge.targetKind == TerrainGraphEndpointKind.StepNode
                        && edge.targetIndex >= 0 && edge.targetIndex < editorNodes.Length) {
                        edge.targetIndex = editorNodes[edge.targetIndex].stepIndex;
                    }
                    persistedEdges[i] = edge;
                }
            }

            generator.graphEditorStateV2 = new TerrainGraphEditorStateData {
                reroutes = editorSnapshot.editorReroutes ?? Array.Empty<TerrainGraphRerouteData>(),
                edges = ConvertEditorEdgesToPersisted(persistedEdges ?? Array.Empty<EditorEdgeSnapshot>())
            };
            // Remove hidden legacy fallback once graph data has been persisted.
            generator.moisture = null;
            generator.moistureScale = 0.2f;
            EditorUtility.SetDirty(generator);

            ClearDirty();
        }

        public GraphSnapshot BuildPreviewSnapshot() {
            return BuildSnapshot(updateNodeTitles: false);
        }

        public GraphSnapshot BuildEditorSnapshot() {
            var snapshot = new GraphSnapshot {
                hasEditorState = true,
                graphVersion = 2,
                altitudeOutputPosition = outputNode != null ? outputNode.GetPosition().position : new Vector2(800f, 180f),
                moistureOutputPosition = moistureOutputNode != null ? moistureOutputNode.GetPosition().position : new Vector2(800f, 320f),
                editorNodes = Array.Empty<EditorNodeSnapshot>(),
                editorReroutes = Array.Empty<TerrainGraphRerouteData>(),
                editorEdges = Array.Empty<EditorEdgeSnapshot>(),
                steps = Array.Empty<StepData>(),
                nodePositions = Array.Empty<NodeLayout>(),
                legacyNodePositions = Array.Empty<NodeLayout>(),
                terminalStepIndex = -1,
                moistureTerminalStepIndex = -1
            };

            var allNodes = nodes.ToList().OfType<TerrainStepNode>().ToList();
            var reroutes = nodes.ToList().OfType<TerrainRerouteNode>().ToList();
            if (allNodes.Count == 0 && reroutes.Count == 0) {
                return snapshot;
            }

            var nodeIndices = new Dictionary<TerrainStepNode, int>();
            var editorNodes = new EditorNodeSnapshot[allNodes.Count];
            for (int i = 0; i < allNodes.Count; i++) {
                var node = allNodes[i];
                nodeIndices[node] = i;
                editorNodes[i] = new EditorNodeSnapshot {
                    operation = node.Operation,
                    data = node.CollectStepData(),
                    position = node.GetPosition().position,
                    stepIndex = node.StepIndex
                };
            }

            var rerouteIndices = new Dictionary<TerrainRerouteNode, int>();
            var editorReroutes = new TerrainGraphRerouteData[reroutes.Count];
            for (int i = 0; i < reroutes.Count; i++) {
                rerouteIndices[reroutes[i]] = i;
                editorReroutes[i] = new TerrainGraphRerouteData {
                    position = reroutes[i].GetPosition().position
                };
            }

            var edgeList = new List<EditorEdgeSnapshot>();
            foreach (var edge in edges.ToList()) {
                if (TryBuildEditorEdgeSnapshot(edge, nodeIndices, rerouteIndices, out EditorEdgeSnapshot edgeSnapshot)) {
                    edgeList.Add(edgeSnapshot);
                }
            }

            snapshot.editorNodes = editorNodes;
            snapshot.editorReroutes = editorReroutes;
            snapshot.editorEdges = edgeList.ToArray();
            return snapshot;
        }

        GraphSnapshot BuildSnapshot(bool updateNodeTitles) {
            var snapshot = new GraphSnapshot {
                graphVersion = 2,
                steps = Array.Empty<StepData>(),
                nodePositions = SaveStepPositions(new List<TerrainStepNode>()),
                legacyNodePositions = SaveLegacyNodePositions(new List<TerrainStepNode>()),
                altitudeOutputPosition = outputNode != null ? outputNode.GetPosition().position : new Vector2(800f, 180f),
                moistureOutputPosition = moistureOutputNode != null ? moistureOutputNode.GetPosition().position : new Vector2(800f, 320f),
                terminalStepIndex = -1,
                moistureTerminalStepIndex = -1
            };

            var editorSnapshot = BuildEditorSnapshot();
            snapshot.editorReroutes = editorSnapshot.editorReroutes;
            snapshot.editorEdges = editorSnapshot.editorEdges;

            // Collect all step nodes (exclude output node)
            var allNodes = nodes.ToList().OfType<TerrainStepNode>().ToList();
            if (allNodes.Count == 0) {
                return snapshot;
            }

            BuildStepGraphContext(allNodes, out Dictionary<TerrainStepNode, List<TerrainStepNode>> inputMap,
                out TerrainStepNode altitudeTerminalNode, out TerrainStepNode moistureTerminalNode);

            var altitudeReachable = CollectReachableNodes(altitudeTerminalNode, inputMap);
            var moistureReachable = CollectReachableNodes(moistureTerminalNode, inputMap);

            // Find reachable nodes from any output
            var reachable = new HashSet<TerrainStepNode>();
            bool outputConnected = altitudeReachable.Count > 0 || moistureReachable.Count > 0;
            if (outputConnected) {
                foreach (var node in altitudeReachable) {
                    reachable.Add(node);
                }
                foreach (var node in moistureReachable) {
                    reachable.Add(node);
                }
            } else {
                // No output connected: treat all nodes as reachable to preserve their enabled state
                foreach (var node in allNodes) {
                    reachable.Add(node);
                }
            }

            // Sort both reachable and unreachable nodes topologically
            var sortedReachable = TopologicalSort(reachable, inputMap);
            var unreachableSet = new HashSet<TerrainStepNode>();
            foreach (var node in allNodes) {
                if (!reachable.Contains(node)) {
                    unreachableSet.Add(node);
                }
            }
            var sortedUnreachable = TopologicalSort(unreachableSet, inputMap);

            // Build StepData array
            var stepList = new List<StepData>();
            var nodeToStepIndex = new Dictionary<TerrainStepNode, int>();
            var orderedNodes = new List<TerrainStepNode>();
            var availableOutputNodes = new HashSet<TerrainStepNode>();

            // Phase 1: reachable nodes with Copy insertion for implicit-value ops
            foreach (var node in sortedReachable) {
                SerializeNode(node, true, stepList, nodeToStepIndex, orderedNodes, availableOutputNodes);
            }

            // Phase 2: unreachable nodes (topologically sorted, preserved with enabled=false)
            foreach (var node in sortedUnreachable) {
                SerializeNode(node, false, stepList, nodeToStepIndex, orderedNodes, availableOutputNodes);
            }

            snapshot.steps = stepList.ToArray();
            snapshot.nodePositions = SaveStepPositions(orderedNodes);
            snapshot.legacyNodePositions = SaveLegacyNodePositions(orderedNodes);
            snapshot.terminalStepIndex = altitudeTerminalNode != null
                && availableOutputNodes.Contains(altitudeTerminalNode)
                && nodeToStepIndex.ContainsKey(altitudeTerminalNode)
                ? nodeToStepIndex[altitudeTerminalNode] : -1;
            snapshot.moistureTerminalStepIndex = moistureTerminalNode != null
                && availableOutputNodes.Contains(moistureTerminalNode)
                && nodeToStepIndex.ContainsKey(moistureTerminalNode)
                ? nodeToStepIndex[moistureTerminalNode] : -1;

            if (updateNodeTitles) {
                foreach (var kvp in nodeToStepIndex) {
                    kvp.Key.StepIndex = kvp.Value;
                    kvp.Key.UpdateDisplayTitle();
                }
            }

            return snapshot;
        }

        void SerializeNode(TerrainStepNode node, bool isReachable, List<StepData> stepList,
            Dictionary<TerrainStepNode, int> nodeToStepIndex, List<TerrainStepNode> orderedNodes,
            HashSet<TerrainStepNode> availableOutputNodes) {

            var op = node.Operation;
            bool disconnectedImplicit = false;

            bool usesImplicitValueFlow = TerrainStepNode.UsesImplicitValueFlow(op);
            bool hasImplicitAndRefInput = TerrainStepNode.HasImplicitValueAndSingleRefOp(op);

            // Insert synthetic Copy for ops whose primary value comes from the implicit flow
            if (usesImplicitValueFlow) {
                TerrainStepNode sourceNode = ResolveUpstreamStepNode(node.InputPort);
                if (sourceNode != null && nodeToStepIndex.ContainsKey(sourceNode) && availableOutputNodes.Contains(sourceNode)) {
                    int sourceIdx = nodeToStepIndex[sourceNode];
                    if (sourceIdx != stepList.Count - 1) {
                        stepList.Add(new StepData {
                            enabled = isReachable,
                            operation = TerrainStepType.Copy,
                            description = SYNTHETIC_COPY,
                            inputIndex0 = sourceIdx
                        });
                        orderedNodes.Add(null); // null = synthetic Copy
                    }
                } else {
                    disconnectedImplicit = true;
                }
            }

            var step = node.CollectStepData();

            // Override enabled for unreachable nodes
            if (!isReachable) {
                step.enabled = false;
            }

            // Mark disconnected implicit-value ops so they don't get false connections on load
            if (disconnectedImplicit) {
                step.enabled = false;
                if (hasImplicitAndRefInput) {
                    step.inputIndex1 = -1;
                } else {
                    step.inputIndex0 = -1;
                }
            } else if (hasImplicitAndRefInput) {
                // Ops with implicit flow + explicit reference use inputIndex0 for the ref and inputIndex1 as the implicit-flow sentinel.
                step.inputIndex1 = 0;
            }

            // Resolve inputIndex for reference-based operations
            bool needsInputA = TerrainStepNode.IsSingleRefOp(op) || TerrainStepNode.HasTwoInputs(op) || hasImplicitAndRefInput;
            bool needsInputB = TerrainStepNode.HasTwoInputs(op);
            Port inputPortA = hasImplicitAndRefInput ? node.InputPortB : node.InputPort;

            if (needsInputA) {
                bool connectedA = ResolvePortIndex(inputPortA, nodeToStepIndex, availableOutputNodes, out int idxA);
                step.inputIndex0 = connectedA ? idxA : -1;
                if (!connectedA) {
                    step.enabled = false;
                }
            }
            if (needsInputB) {
                bool connectedB = ResolvePortIndex(node.InputPortB, nodeToStepIndex, availableOutputNodes, out int idxB);
                step.inputIndex1 = connectedB ? idxB : -1;
                if (!connectedB) {
                    step.enabled = false;
                }
            }

            nodeToStepIndex[node] = stepList.Count;
            stepList.Add(step);
            orderedNodes.Add(node);
            if (step.enabled) {
                availableOutputNodes.Add(node);
            }
        }

        bool ResolvePortIndex(Port port, Dictionary<TerrainStepNode, int> nodeToStepIndex,
            HashSet<TerrainStepNode> availableOutputNodes, out int index) {
            index = 0;
            TerrainStepNode source = ResolveUpstreamStepNode(port);
            if (source != null && nodeToStepIndex.ContainsKey(source) && availableOutputNodes.Contains(source)) {
                index = nodeToStepIndex[source];
                return true;
            }
            return false;
        }

        void BuildStepGraphContext(List<TerrainStepNode> allNodes, out Dictionary<TerrainStepNode, List<TerrainStepNode>> inputMap,
            out TerrainStepNode altitudeTerminalNode, out TerrainStepNode moistureTerminalNode) {
            inputMap = new Dictionary<TerrainStepNode, List<TerrainStepNode>>();
            foreach (var node in allNodes) {
                inputMap[node] = new List<TerrainStepNode>();

                if (node.InputPort != null) {
                    TerrainStepNode source = ResolveUpstreamStepNode(node.InputPort);
                    if (source != null && !inputMap[node].Contains(source)) {
                        inputMap[node].Add(source);
                    }
                }

                if (node.InputPortB != null) {
                    TerrainStepNode source = ResolveUpstreamStepNode(node.InputPortB);
                    if (source != null && !inputMap[node].Contains(source)) {
                        inputMap[node].Add(source);
                    }
                }
            }

            altitudeTerminalNode = ResolveUpstreamStepNode(GetOutputNodeInput(TerrainGraphOutputKind.Altitude));
            moistureTerminalNode = ResolveUpstreamStepNode(GetOutputNodeInput(TerrainGraphOutputKind.Moisture));
        }

        HashSet<TerrainStepNode> CollectReachableNodes(TerrainStepNode terminalNode, Dictionary<TerrainStepNode, List<TerrainStepNode>> inputMap) {
            var reachable = new HashSet<TerrainStepNode>();
            if (terminalNode == null) return reachable;

            var queue = new Queue<TerrainStepNode>();
            queue.Enqueue(terminalNode);
            reachable.Add(terminalNode);
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                if (inputMap.TryGetValue(current, out var inputs)) {
                    foreach (var inp in inputs) {
                        if (reachable.Add(inp)) {
                            queue.Enqueue(inp);
                        }
                    }
                }
            }
            return reachable;
        }

        List<TerrainStepNode> TopologicalSort(HashSet<TerrainStepNode> nodes, Dictionary<TerrainStepNode, List<TerrainStepNode>> inputMap) {
            var inDegree = new Dictionary<TerrainStepNode, int>();
            var dependents = new Dictionary<TerrainStepNode, List<TerrainStepNode>>();

            foreach (var node in nodes) {
                if (!inDegree.ContainsKey(node)) inDegree[node] = 0;
                if (!dependents.ContainsKey(node)) dependents[node] = new List<TerrainStepNode>();
            }

            foreach (var node in nodes) {
                if (inputMap.TryGetValue(node, out var inputs)) {
                    foreach (var inp in inputs) {
                        if (nodes.Contains(inp)) {
                            inDegree[node]++;
                            dependents[inp].Add(node);
                        }
                    }
                }
            }

            var queue = new Queue<TerrainStepNode>();
            foreach (var kvp in inDegree) {
                if (kvp.Value == 0) queue.Enqueue(kvp.Key);
            }

            var sorted = new List<TerrainStepNode>();
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                sorted.Add(current);
                foreach (var dep in dependents[current]) {
                    inDegree[dep]--;
                    if (inDegree[dep] == 0) {
                        queue.Enqueue(dep);
                    }
                }
            }

            return sorted;
        }

        bool TryBuildEditorEdgeSnapshot(Edge edge, Dictionary<TerrainStepNode, int> nodeIndices,
            Dictionary<TerrainRerouteNode, int> rerouteIndices, out EditorEdgeSnapshot snapshot) {
            snapshot = default;
            if (edge == null || edge.output == null || edge.input == null) return false;

            if (!TryResolveEndpoint(edge.output.node, edge.output, nodeIndices, rerouteIndices, isInput: false,
                out TerrainGraphEndpointKind sourceKind, out int sourceIndex, out _)) {
                return false;
            }

            if (!TryResolveEndpoint(edge.input.node, edge.input, nodeIndices, rerouteIndices, isInput: true,
                out TerrainGraphEndpointKind targetKind, out int targetIndex, out int targetPortIndex)) {
                return false;
            }

            snapshot = new EditorEdgeSnapshot {
                sourceKind = sourceKind,
                sourceIndex = sourceIndex,
                targetKind = targetKind,
                targetIndex = targetIndex,
                targetPortIndex = targetPortIndex
            };
            return true;
        }

        bool TryResolveEndpoint(Node node, Port port, Dictionary<TerrainStepNode, int> nodeIndices,
            Dictionary<TerrainRerouteNode, int> rerouteIndices, bool isInput,
            out TerrainGraphEndpointKind kind, out int index, out int targetPortIndex) {
            kind = TerrainGraphEndpointKind.StepNode;
            index = -1;
            targetPortIndex = -1;

            if (node is TerrainStepNode stepNode) {
                if (!nodeIndices.TryGetValue(stepNode, out index)) return false;
                kind = TerrainGraphEndpointKind.StepNode;
                if (isInput) {
                    targetPortIndex = port == stepNode.InputPortB ? 1 : 0;
                }
                return true;
            }

            if (node is TerrainRerouteNode rerouteNode) {
                if (!rerouteIndices.TryGetValue(rerouteNode, out index)) return false;
                kind = TerrainGraphEndpointKind.Reroute;
                targetPortIndex = 0;
                return true;
            }

            if (isInput && node == outputNode) {
                kind = TerrainGraphEndpointKind.AltitudeOutput;
                return true;
            }

            if (isInput && node == moistureOutputNode) {
                kind = TerrainGraphEndpointKind.MoistureOutput;
                return true;
            }

            return false;
        }

        EditorEdgeSnapshot[] ConvertPersistedEditorEdges(TerrainGraphEditorEdgeData[] edgesData) {
            if (edgesData == null || edgesData.Length == 0) return Array.Empty<EditorEdgeSnapshot>();
            var edges = new EditorEdgeSnapshot[edgesData.Length];
            for (int i = 0; i < edgesData.Length; i++) {
                edges[i] = new EditorEdgeSnapshot {
                    sourceKind = edgesData[i].sourceKind,
                    sourceIndex = edgesData[i].sourceIndex,
                    targetKind = edgesData[i].targetKind,
                    targetIndex = edgesData[i].targetIndex,
                    targetPortIndex = edgesData[i].targetPortIndex
                };
            }
            return edges;
        }

        TerrainGraphEditorEdgeData[] ConvertEditorEdgesToPersisted(EditorEdgeSnapshot[] edgesData) {
            if (edgesData == null || edgesData.Length == 0) return Array.Empty<TerrainGraphEditorEdgeData>();
            var edges = new TerrainGraphEditorEdgeData[edgesData.Length];
            for (int i = 0; i < edgesData.Length; i++) {
                edges[i] = new TerrainGraphEditorEdgeData {
                    sourceKind = edgesData[i].sourceKind,
                    sourceIndex = edgesData[i].sourceIndex,
                    targetKind = edgesData[i].targetKind,
                    targetIndex = edgesData[i].targetIndex,
                    targetPortIndex = edgesData[i].targetPortIndex
                };
            }
            return edges;
        }

        TerrainStepNode ResolveUpstreamStepNode(Port port, Edge candidateEdge = null, HashSet<Port> visitedPorts = null, HashSet<Edge> visitedEdges = null) {
            if (port == null) return null;
            visitedPorts ??= new HashSet<Port>();
            visitedEdges ??= new HashSet<Edge>();
            if (!visitedPorts.Add(port)) return null;

            foreach (var edge in EnumerateConnectedEdges(port, candidateEdge)) {
                if (edge == null || !visitedEdges.Add(edge)) continue;

                Port otherPort = edge.input == port ? edge.output : edge.input;
                if (otherPort == null) continue;

                if (otherPort.node is TerrainStepNode stepNode) {
                    // Prefer canonical upstream connections, but accept any step output found through malformed reroutes.
                    if (otherPort == stepNode.OutputPort || otherPort.direction == Direction.Output) {
                        return stepNode;
                    }
                    continue;
                }

                if (otherPort.node is TerrainRerouteNode rerouteNode) {
                    TerrainStepNode upstream = ResolveUpstreamThroughReroute(rerouteNode, otherPort, candidateEdge, visitedPorts, visitedEdges);
                    if (upstream != null) {
                        return upstream;
                    }
                }
            }

            return null;
        }

        TerrainStepNode ResolveUpstreamThroughReroute(TerrainRerouteNode rerouteNode, Port entryPort, Edge candidateEdge,
            HashSet<Port> visitedPorts, HashSet<Edge> visitedEdges) {
            if (rerouteNode == null) return null;

            Port oppositePort = entryPort == rerouteNode.InputPort ? rerouteNode.OutputPort : rerouteNode.InputPort;
            if (oppositePort != null) {
                TerrainStepNode upstream = ResolveUpstreamStepNode(oppositePort, candidateEdge, visitedPorts, visitedEdges);
                if (upstream != null) {
                    return upstream;
                }
            }

            // Fallback for malformed reroutes whose useful connection ended on the same port we entered.
            if (entryPort != null && entryPort != oppositePort) {
                return ResolveUpstreamStepNode(entryPort, candidateEdge, visitedPorts, visitedEdges);
            }

            return null;
        }

        NodeLayout[] SaveStepPositions(List<TerrainStepNode> orderedNodes) {
            var positions = new NodeLayout[orderedNodes.Count];
            for (int i = 0; i < orderedNodes.Count; i++) {
                if (orderedNodes[i] != null) {
                    positions[i].position = orderedNodes[i].GetPosition().position;
                }
            }
            return positions;
        }

        NodeLayout[] SaveLegacyNodePositions(List<TerrainStepNode> orderedNodes) {
            // Index 0 = altitude output node position, rest = step nodes
            var positions = new NodeLayout[orderedNodes.Count + 1];
            if (outputNode != null) {
                positions[0].position = outputNode.GetPosition().position;
            }
            for (int i = 0; i < orderedNodes.Count; i++) {
                if (orderedNodes[i] != null) {
                    positions[i + 1].position = orderedNodes[i].GetPosition().position;
                }
            }
            return positions;
        }

        // --- Auto layout ---

        public void AutoLayout() {
            BeginBulkMutation();
            try {
                var stepNodes = nodes.ToList().OfType<TerrainStepNode>().ToList();
                if (stepNodes.Count == 0) {
                    if (outputNode != null || moistureOutputNode != null) {
                        Vector2 altitudePosition = new Vector2(400f, 180f);
                        if (outputNode != null) {
                            outputNode.SetPosition(new Rect(altitudePosition, Vector2.zero));
                        }
                        if (moistureOutputNode != null) {
                            moistureOutputNode.SetPosition(new Rect(GetDefaultMoistureOutputPosition(altitudePosition), Vector2.zero));
                        }
                    }
                    return;
                }

                const int maxLayoutPasses = 10;
                const float positionTolerance = 0.5f;
                float positionToleranceSqr = positionTolerance * positionTolerance;

                bool LayoutChanged(NodeLayout[] previousStepPositions, Vector2 previousAltitudeOutputPosition, Vector2 previousMoistureOutputPosition) {
                    for (int i = 0; i < stepNodes.Count; i++) {
                        Vector2 currentPosition = stepNodes[i].GetPosition().position;
                        if ((currentPosition - previousStepPositions[i].position).sqrMagnitude > positionToleranceSqr) {
                            return true;
                        }
                    }

                    if (outputNode != null) {
                        Vector2 currentOutputPosition = outputNode.GetPosition().position;
                        if ((currentOutputPosition - previousAltitudeOutputPosition).sqrMagnitude > positionToleranceSqr) {
                            return true;
                        }
                    }

                    if (moistureOutputNode != null) {
                        Vector2 currentMoistureOutputPosition = moistureOutputNode.GetPosition().position;
                        if ((currentMoistureOutputPosition - previousMoistureOutputPosition).sqrMagnitude > positionToleranceSqr) {
                            return true;
                        }
                    }

                    return false;
                }

                for (int layoutPass = 0; layoutPass < maxLayoutPasses; layoutPass++) {
                    NodeLayout[] previousStepPositions = SaveStepPositions(stepNodes);
                    Vector2 previousAltitudeOutputPosition = outputNode != null ? outputNode.GetPosition().position : Vector2.zero;
                    Vector2 previousMoistureOutputPosition = moistureOutputNode != null ? moistureOutputNode.GetPosition().position : Vector2.zero;

                    BuildStepGraphContext(stepNodes, out Dictionary<TerrainStepNode, List<TerrainStepNode>> inputSources,
                        out TerrainStepNode altitudeTerminalNode, out TerrainStepNode moistureTerminalNode);

                    var outputTargets = new Dictionary<TerrainStepNode, List<TerrainStepNode>>();
                    var primaryInputSource = new Dictionary<TerrainStepNode, TerrainStepNode>();
                    foreach (var node in stepNodes) {
                        outputTargets[node] = new List<TerrainStepNode>();
                        if (inputSources.TryGetValue(node, out var sources) && sources.Count > 0) {
                            TerrainStepNode primarySource = ResolveUpstreamStepNode(node.InputPort);
                            if (primarySource != null) {
                                primaryInputSource[node] = primarySource;
                            }
                            foreach (var source in sources) {
                                if (outputTargets.TryGetValue(source, out var targets) && !targets.Contains(node)) {
                                    targets.Add(node);
                                }
                            }
                        }
                    }

                    // Compute column depth via topological sort
                    var stepNodeSet = new HashSet<TerrainStepNode>(stepNodes);
                    var depth = new Dictionary<TerrainStepNode, int>();
                    var inDegree = new Dictionary<TerrainStepNode, int>();
                    foreach (var node in stepNodes) {
                        depth[node] = 0;
                        int degree = 0;
                        foreach (var src in inputSources[node]) {
                            if (stepNodeSet.Contains(src)) degree++;
                        }
                        inDegree[node] = degree;
                    }

                    var queue = new Queue<TerrainStepNode>();
                    foreach (var node in stepNodes) {
                        if (inDegree[node] == 0) queue.Enqueue(node);
                    }

                    while (queue.Count > 0) {
                        var current = queue.Dequeue();
                        foreach (var target in outputTargets[current]) {
                            int newDepth = depth[current] + 1;
                            if (newDepth > depth[target]) {
                                depth[target] = newDepth;
                            }
                            inDegree[target]--;
                            if (inDegree[target] == 0) {
                                queue.Enqueue(target);
                            }
                        }
                    }

                    float xSpacing = 280f;
                    float baseX = 110f;
                    float baseY = 110f;
                    float bandGapY = 110f;
                    float bandPaddingY = 70f;
                    float sectionGapY = 180f;
                    float minSectionHeight = 220f;
                    int maxDepthIndex = depth.Count > 0 ? depth.Values.Max() : 0;
                    int columnsPerBand = Mathf.Clamp(maxDepthIndex + 1, 1, 8);

                    float GetNodeLayoutHeight(TerrainStepNode node) {
                        return Mathf.Max(96f, TerrainStepNode.EstimateNodeHeight(node.Operation) + 22f);
                    }

                    float GetColumnLayoutHeight(List<TerrainStepNode> columnNodes, float nodeGap) {
                        if (columnNodes == null || columnNodes.Count == 0) return 0f;
                        float height = 0f;
                        for (int i = 0; i < columnNodes.Count; i++) {
                            height += GetNodeLayoutHeight(columnNodes[i]);
                            if (i < columnNodes.Count - 1) {
                                height += nodeGap;
                            }
                        }
                        return height;
                    }

                    HashSet<TerrainStepNode> BuildMainPath(TerrainStepNode terminalNode) {
                        var mainPath = new HashSet<TerrainStepNode>();
                        var current = terminalNode;
                        while (current != null && mainPath.Add(current)) {
                            if (!primaryInputSource.TryGetValue(current, out current)) {
                                current = null;
                            }
                        }
                        return mainPath;
                    }

                    float LayoutSection(List<TerrainStepNode> sectionNodes, HashSet<TerrainStepNode> mainPathNodes, float sectionTopY, out float sectionCenterY) {
                        if (sectionNodes == null || sectionNodes.Count == 0) {
                            sectionCenterY = sectionTopY + minSectionHeight * 0.5f;
                            return sectionTopY + minSectionHeight;
                        }

                        const float nodeGap = 44f;
                        const int barycenterPasses = 6;
                        var sectionColumns = new SortedDictionary<int, List<TerrainStepNode>>();
                        var sectionNodeSet = new HashSet<TerrainStepNode>(sectionNodes);
                        foreach (var node in sectionNodes) {
                            int nodeDepth = depth[node];
                            if (!sectionColumns.ContainsKey(nodeDepth)) sectionColumns[nodeDepth] = new List<TerrainStepNode>();
                            sectionColumns[nodeDepth].Add(node);
                        }
                        foreach (var column in sectionColumns.Values) {
                            column.Sort((a, b) => a.GetPosition().y.CompareTo(b.GetPosition().y));
                        }

                        Dictionary<TerrainStepNode, float> BuildNormalizedOrderMap() {
                            var orderMap = new Dictionary<TerrainStepNode, float>();
                            foreach (var column in sectionColumns.Values) {
                                float denominator = Mathf.Max(1f, column.Count - 1f);
                                for (int i = 0; i < column.Count; i++) {
                                    orderMap[column[i]] = column.Count > 1 ? i / denominator : 0.5f;
                                }
                            }
                            return orderMap;
                        }

                        float ComputeBarycenter(TerrainStepNode node, Dictionary<TerrainStepNode, List<TerrainStepNode>> neighborMap,
                            Dictionary<TerrainStepNode, float> normalizedOrder, float fallbackValue) {
                            if (!neighborMap.TryGetValue(node, out var neighbors) || neighbors == null || neighbors.Count == 0) {
                                return fallbackValue;
                            }

                            float sum = 0f;
                            int count = 0;
                            for (int i = 0; i < neighbors.Count; i++) {
                                var neighbor = neighbors[i];
                                if (!sectionNodeSet.Contains(neighbor)) continue;
                                if (!normalizedOrder.TryGetValue(neighbor, out float neighborOrder)) continue;
                                sum += neighborOrder;
                                count++;
                            }
                            return count > 0 ? sum / count : fallbackValue;
                        }

                        void ReorderColumn(List<TerrainStepNode> column, Dictionary<TerrainStepNode, List<TerrainStepNode>> neighborMap) {
                            if (column == null || column.Count <= 1) return;
                            var normalizedOrder = BuildNormalizedOrderMap();
                            var currentOrder = new Dictionary<TerrainStepNode, int>();
                            for (int i = 0; i < column.Count; i++) {
                                currentOrder[column[i]] = i;
                            }

                            column.Sort((a, b) => {
                                float fallbackA = normalizedOrder.TryGetValue(a, out float orderA) ? orderA : currentOrder[a];
                                float fallbackB = normalizedOrder.TryGetValue(b, out float orderB) ? orderB : currentOrder[b];
                                float scoreA = ComputeBarycenter(a, neighborMap, normalizedOrder, fallbackA);
                                float scoreB = ComputeBarycenter(b, neighborMap, normalizedOrder, fallbackB);
                                int comparison = scoreA.CompareTo(scoreB);
                                if (comparison != 0) return comparison;
                                return currentOrder[a].CompareTo(currentOrder[b]);
                            });
                        }

                        var orderedDepths = sectionColumns.Keys.ToList();
                        for (int pass = 0; pass < barycenterPasses; pass++) {
                            for (int i = 1; i < orderedDepths.Count; i++) {
                                ReorderColumn(sectionColumns[orderedDepths[i]], inputSources);
                            }
                            for (int i = orderedDepths.Count - 2; i >= 0; i--) {
                                ReorderColumn(sectionColumns[orderedDepths[i]], outputTargets);
                            }
                        }

                        var globalBandIndices = sectionColumns.Keys
                            .Select(depthIndex => depthIndex / columnsPerBand)
                            .Distinct()
                            .OrderBy(band => band)
                            .ToList();
                        var bandMap = new Dictionary<int, int>();
                        for (int i = 0; i < globalBandIndices.Count; i++) {
                            bandMap[globalBandIndices[i]] = i;
                        }

                        var bandHeights = new float[globalBandIndices.Count];
                        var bandTops = new float[globalBandIndices.Count];
                        var bandStartColumns = new Dictionary<int, int>();
                        var actualColumnMap = new Dictionary<TerrainStepNode, int>();
                        for (int i = 0; i < globalBandIndices.Count; i++) {
                            int globalBand = globalBandIndices[i];
                            int bandStartDepth = globalBand * columnsPerBand;
                            int bandEndDepth = bandStartDepth + columnsPerBand - 1;
                            int bandStartColumn = 0;

                            for (int depthIndex = bandStartDepth; depthIndex <= bandEndDepth; depthIndex++) {
                                if (!sectionColumns.TryGetValue(depthIndex, out var columnNodes) || columnNodes.Count == 0) continue;

                                int localColumn = depthIndex % columnsPerBand;
                                for (int nodeIndex = 0; nodeIndex < columnNodes.Count; nodeIndex++) {
                                    var node = columnNodes[nodeIndex];
                                    if (!inputSources.TryGetValue(node, out var sources) || sources == null || sources.Count == 0) continue;

                                    for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++) {
                                        var source = sources[sourceIndex];
                                        if (source == null || !depth.TryGetValue(source, out int sourceDepth)) continue;

                                        int sourceBand = sourceDepth / columnsPerBand;
                                        if (sourceBand == globalBand) continue;

                                        int sourceActualColumn;
                                        if (sectionNodeSet.Contains(source)) {
                                            if (!actualColumnMap.TryGetValue(source, out sourceActualColumn)) continue;
                                        } else {
                                            sourceActualColumn = Mathf.Max(0, Mathf.RoundToInt((source.GetPosition().x - baseX) / xSpacing));
                                        }

                                        int sourceLocalColumn = sourceDepth % columnsPerBand;
                                        int minTargetColumn = sourceActualColumn + (sourceLocalColumn == columnsPerBand - 1 ? 0 : 1);
                                        bandStartColumn = Mathf.Max(bandStartColumn, minTargetColumn - localColumn);
                                    }
                                }
                            }

                            bandStartColumns[globalBand] = bandStartColumn;
                            for (int depthIndex = bandStartDepth; depthIndex <= bandEndDepth; depthIndex++) {
                                if (!sectionColumns.TryGetValue(depthIndex, out var columnNodes) || columnNodes.Count == 0) continue;

                                int actualColumn = bandStartColumn + (depthIndex % columnsPerBand);
                                for (int nodeIndex = 0; nodeIndex < columnNodes.Count; nodeIndex++) {
                                    actualColumnMap[columnNodes[nodeIndex]] = actualColumn;
                                }
                            }
                        }

                        for (int i = 0; i < globalBandIndices.Count; i++) {
                            int globalBand = globalBandIndices[i];
                            int bandStartDepth = globalBand * columnsPerBand;
                            int bandEndDepth = bandStartDepth + columnsPerBand - 1;
                            float maxColumnHeight = 0f;
                            for (int depthIndex = bandStartDepth; depthIndex <= bandEndDepth; depthIndex++) {
                                if (sectionColumns.TryGetValue(depthIndex, out var columnNodes) && columnNodes.Count > 0) {
                                    float columnHeight = GetColumnLayoutHeight(columnNodes, nodeGap);
                                    if (columnHeight > maxColumnHeight) {
                                        maxColumnHeight = columnHeight;
                                    }
                                }
                            }
                            bandHeights[i] = Mathf.Max(minSectionHeight, maxColumnHeight + bandPaddingY * 2f);
                            bandTops[i] = i == 0 ? sectionTopY : bandTops[i - 1] + bandHeights[i - 1] + bandGapY;
                        }

                        foreach (var kvp in sectionColumns) {
                            int nodeDepth = kvp.Key;
                            int localBand = bandMap[nodeDepth / columnsPerBand];
                            int globalBand = nodeDepth / columnsPerBand;
                            int actualColumn = bandStartColumns.TryGetValue(globalBand, out int bandStartColumn)
                                ? bandStartColumn + (nodeDepth % columnsPerBand)
                                : nodeDepth % columnsPerBand;
                            float x = baseX + actualColumn * xSpacing;
                            int count = kvp.Value.Count;
                            float columnHeight = GetColumnLayoutHeight(kvp.Value, nodeGap);
                            float startY = bandTops[localBand] + (bandHeights[localBand] - columnHeight) * 0.5f;

                            TerrainStepNode mainNode = null;
                            if (mainPathNodes != null) {
                                for (int i = 0; i < count; i++) {
                                    if (mainPathNodes.Contains(kvp.Value[i])) {
                                        mainNode = kvp.Value[i];
                                        break;
                                    }
                                }
                            }

                            if (mainNode == null) {
                                float currentY = startY;
                                for (int i = 0; i < count; i++) {
                                    var node = kvp.Value[i];
                                    node.SetPosition(new Rect(x, currentY, 0, 0));
                                    currentY += GetNodeLayoutHeight(node);
                                    if (i < count - 1) {
                                        currentY += nodeGap;
                                    }
                                }
                                continue;
                            }

                            float bandCenterY = bandTops[localBand] + bandHeights[localBand] * 0.5f;
                            float mainHeight = GetNodeLayoutHeight(mainNode);
                            float mainTopY = bandCenterY - mainHeight * 0.5f;
                            var aboveNodes = new List<TerrainStepNode>();
                            var belowNodes = new List<TerrainStepNode>();
                            float mainOriginalY = mainNode.GetPosition().y;
                            foreach (var node in kvp.Value) {
                                if (node == mainNode) continue;
                                if (node.GetPosition().y <= mainOriginalY) {
                                    aboveNodes.Add(node);
                                } else {
                                    belowNodes.Add(node);
                                }
                            }
                            if (aboveNodes.Count == 0 && belowNodes.Count > 1) {
                                aboveNodes.Add(belowNodes[0]);
                                belowNodes.RemoveAt(0);
                            } else if (belowNodes.Count == 0 && aboveNodes.Count > 1) {
                                belowNodes.Add(aboveNodes[aboveNodes.Count - 1]);
                                aboveNodes.RemoveAt(aboveNodes.Count - 1);
                            }

                            float aboveHeight = GetColumnLayoutHeight(aboveNodes, nodeGap);
                            float aboveY = mainTopY - nodeGap - aboveHeight;
                            for (int i = 0; i < aboveNodes.Count; i++) {
                                var node = aboveNodes[i];
                                node.SetPosition(new Rect(x, aboveY, 0, 0));
                                aboveY += GetNodeLayoutHeight(node);
                                if (i < aboveNodes.Count - 1) {
                                    aboveY += nodeGap;
                                }
                            }

                            mainNode.SetPosition(new Rect(x, mainTopY, 0, 0));

                            float belowY = mainTopY + mainHeight + nodeGap;
                            for (int i = 0; i < belowNodes.Count; i++) {
                                var node = belowNodes[i];
                                node.SetPosition(new Rect(x, belowY, 0, 0));
                                belowY += GetNodeLayoutHeight(node);
                                if (i < belowNodes.Count - 1) {
                                    belowY += nodeGap;
                                }
                            }
                        }

                        float sectionBottomY = bandTops[globalBandIndices.Count - 1] + bandHeights[globalBandIndices.Count - 1];
                        sectionCenterY = (sectionTopY + sectionBottomY) * 0.5f;
                        return sectionBottomY;
                    }

                    var altitudeReachable = CollectReachableNodes(altitudeTerminalNode, inputSources);
                    var moistureReachable = CollectReachableNodes(moistureTerminalNode, inputSources);
                    var altitudeMainPath = BuildMainPath(altitudeTerminalNode);
                    var moistureMainPath = BuildMainPath(moistureTerminalNode);
                    var altitudeSectionNodes = altitudeReachable.OrderBy(node => depth[node]).ThenBy(node => node.GetPosition().y).ToList();
                    var moistureSectionNodes = moistureReachable
                        .Where(node => !altitudeReachable.Contains(node))
                        .OrderBy(node => depth[node]).ThenBy(node => node.GetPosition().y)
                        .ToList();
                    var reservedNodes = new HashSet<TerrainStepNode>(altitudeSectionNodes);
                    reservedNodes.UnionWith(moistureSectionNodes);
                    var draftNodes = stepNodes
                        .Where(node => !reservedNodes.Contains(node))
                        .OrderBy(node => depth[node]).ThenBy(node => node.GetPosition().y)
                        .ToList();

                    float altitudeSectionTop = baseY;
                    float altitudeSectionBottom = LayoutSection(altitudeSectionNodes, altitudeMainPath, altitudeSectionTop, out float altitudeSectionCenterY);
                    float moistureSectionTop = altitudeSectionBottom + sectionGapY;
                    float moistureSectionBottom = LayoutSection(moistureSectionNodes, moistureMainPath, moistureSectionTop, out float moistureSectionCenterY);

                    if (draftNodes.Count > 0) {
                        float draftSectionTop = moistureSectionBottom + sectionGapY;
                        LayoutSection(draftNodes, null, draftSectionTop, out _);
                    }

                    float defaultOutputX = baseX + xSpacing;
                    float altitudeOutputX = defaultOutputX;
                    float altitudeOutputY = altitudeSectionCenterY;
                    if (altitudeTerminalNode != null) {
                        altitudeOutputX = altitudeTerminalNode.GetPosition().x + xSpacing;
                        altitudeOutputY = altitudeTerminalNode.GetPosition().y;
                    }

                    float moistureOutputX = defaultOutputX;
                    float moistureOutputY = moistureSectionCenterY;
                    if (moistureTerminalNode != null) {
                        moistureOutputX = moistureTerminalNode.GetPosition().x + xSpacing;
                        if (moistureSectionNodes.Contains(moistureTerminalNode)) {
                            moistureOutputY = moistureTerminalNode.GetPosition().y;
                        }
                    }

                    if (outputNode != null) {
                        outputNode.SetPosition(new Rect(altitudeOutputX, altitudeOutputY, 0, 0));
                    }
                    if (moistureOutputNode != null) {
                        moistureOutputNode.SetPosition(new Rect(moistureOutputX, moistureOutputY, 0, 0));
                    }

                    if (!LayoutChanged(previousStepPositions, previousAltitudeOutputPosition, previousMoistureOutputPosition)) {
                        break;
                    }
                }
            } finally {
                EndBulkMutation();
            }
        }

        // --- Graph change handling ---

        GraphViewChange OnGraphChanged(GraphViewChange change) {
            QueueBridgeEdgesForDeletedReroutes(ref change);

            // Validate new edges: prevent cycles
            if (change.edgesToCreate != null) {
                var toRemove = new List<Edge>();
                foreach (var edge in change.edgesToCreate) {
                    bool invalidMoistureBranch = WouldIntroduceAltitudeOnlyNodeInMoistureBranch(edge);
                    if (WouldCreateCycle(edge) || invalidMoistureBranch) {
                        toRemove.Add(edge);
                        continue;
                    }
                }
                foreach (var edge in toRemove) {
                    change.edgesToCreate.Remove(edge);
                }
            }

            if (change.edgesToCreate != null || change.elementsToRemove != null || change.movedElements != null) {
                MarkDirty();
            }

            return change;
        }

        void QueueBridgeEdgesForDeletedReroutes(ref GraphViewChange change) {
            if (change.elementsToRemove == null || change.elementsToRemove.Count == 0) return;

            var deletedNodes = new HashSet<Node>(change.elementsToRemove.OfType<Node>());
            var deletedReroutes = new HashSet<TerrainRerouteNode>(deletedNodes.OfType<TerrainRerouteNode>());
            if (deletedReroutes.Count == 0) return;

            change.edgesToCreate ??= new List<Edge>();
            var queuedConnections = new HashSet<(Port source, Port target)>();
            var visitedReroutes = new HashSet<TerrainRerouteNode>();

            foreach (var reroute in deletedReroutes) {
                if (reroute == null || visitedReroutes.Contains(reroute)) continue;
                var component = CollectDeletedRerouteComponent(reroute, deletedReroutes, visitedReroutes);
                QueueBridgeEdgesForDeletedRerouteComponent(component, deletedNodes, change.edgesToCreate, queuedConnections);
            }
        }

        HashSet<TerrainRerouteNode> CollectDeletedRerouteComponent(TerrainRerouteNode start,
            HashSet<TerrainRerouteNode> deletedReroutes, HashSet<TerrainRerouteNode> visitedReroutes) {
            var component = new HashSet<TerrainRerouteNode>();
            if (start == null) return component;

            var stack = new Stack<TerrainRerouteNode>();
            stack.Push(start);
            visitedReroutes.Add(start);

            while (stack.Count > 0) {
                var current = stack.Pop();
                if (!component.Add(current)) continue;

                foreach (var port in EnumerateReroutePorts(current)) {
                    foreach (var edge in port.connections) {
                        Port otherPort = edge?.input == port ? edge.output : edge?.input;
                        if (otherPort?.node is TerrainRerouteNode nextReroute
                            && deletedReroutes.Contains(nextReroute)
                            && visitedReroutes.Add(nextReroute)) {
                            stack.Push(nextReroute);
                        }
                    }
                }
            }

            return component;
        }

        void QueueBridgeEdgesForDeletedRerouteComponent(HashSet<TerrainRerouteNode> component, HashSet<Node> deletedNodes,
            List<Edge> edgesToCreate, HashSet<(Port source, Port target)> queuedConnections) {
            if (component == null || component.Count == 0 || edgesToCreate == null) return;

            foreach (var reroute in component) {
                if (reroute == null || reroute.InputPort == null) continue;

                foreach (var incomingEdge in reroute.InputPort.connections) {
                    Port sourcePort = incomingEdge?.output;
                    if (sourcePort == null || sourcePort.direction != Direction.Output) continue;
                    if (sourcePort.node is Node sourceNode && deletedNodes.Contains(sourceNode)) continue;
                    if (sourcePort.node is TerrainRerouteNode sourceReroute && component.Contains(sourceReroute)) continue;

                    var visitedComponentReroutes = new HashSet<TerrainRerouteNode>();
                    TraverseDeletedRerouteComponent(sourcePort, reroute, component, deletedNodes, edgesToCreate,
                        queuedConnections, visitedComponentReroutes);
                }
            }
        }

        void TraverseDeletedRerouteComponent(Port sourcePort, TerrainRerouteNode reroute, HashSet<TerrainRerouteNode> component,
            HashSet<Node> deletedNodes, List<Edge> edgesToCreate, HashSet<(Port source, Port target)> queuedConnections,
            HashSet<TerrainRerouteNode> visitedComponentReroutes) {
            if (sourcePort == null || reroute == null || component == null || !visitedComponentReroutes.Add(reroute)) return;

            foreach (var outgoingEdge in reroute.OutputPort.connections) {
                Port targetPort = outgoingEdge?.input;
                if (targetPort == null || targetPort == reroute.InputPort) continue;

                if (targetPort.node is TerrainRerouteNode nextReroute && component.Contains(nextReroute)) {
                    TraverseDeletedRerouteComponent(sourcePort, nextReroute, component, deletedNodes, edgesToCreate,
                        queuedConnections, visitedComponentReroutes);
                    continue;
                }

                if (targetPort.direction != Direction.Input) continue;
                if (targetPort.node is Node targetNode && deletedNodes.Contains(targetNode)) continue;
                if (sourcePort.node == targetPort.node) continue;

                var connection = (source: sourcePort, target: targetPort);
                if (!queuedConnections.Add(connection)) continue;
                if (targetPort.connections.Any(edge => edge != null && edge.output == sourcePort && edge.input == targetPort)) continue;

                edgesToCreate.Add(new Edge {
                    output = sourcePort,
                    input = targetPort
                });
            }
        }

        IEnumerable<Port> EnumerateReroutePorts(TerrainRerouteNode reroute) {
            if (reroute == null) yield break;
            if (reroute.InputPort != null) yield return reroute.InputPort;
            if (reroute.OutputPort != null) yield return reroute.OutputPort;
        }

        bool WouldIntroduceAltitudeOnlyNodeInMoistureBranch(Edge newEdge) {
            var moistureReachable = CollectMoistureReachableNodes(newEdge);
            return moistureReachable.Any(node => TerrainStepNode.IsAltitudeOnlyOp(node.Operation));
        }

        HashSet<TerrainStepNode> CollectMoistureReachableNodes(Edge candidateEdge = null) {
            var reachable = new HashSet<TerrainStepNode>();
            var visitedPorts = new HashSet<Port>();
            CollectReachableNodesFromInputPort(GetOutputNodeInput(TerrainGraphOutputKind.Moisture), reachable, visitedPorts, candidateEdge);
            return reachable;
        }

        string GetMoistureMetersWarning(IEnumerable<TerrainStepNode> moistureReachable) {
            var meterNodes = moistureReachable
                .Where(node => node != null && node.UsesMetersHeightUnits())
                .Distinct()
                .ToList();
            if (meterNodes.Count == 0) return null;

            string nodeList = string.Join(", ", meterNodes
                .Take(3)
                .Select(node => string.IsNullOrEmpty(node.title) ? TerrainStepNode.GetOperationLabel(node.Operation) : node.title));
            if (meterNodes.Count > 3) {
                nodeList += ", ...";
            }
            return $"Terrain Graph: moisture branch contains meter-edited nodes. Moisture values should use normalized or percentage units, not meters. Affected nodes: {nodeList}.";
        }

        bool WouldCreateCycle(Edge newEdge) {
            var targetNode = newEdge?.input?.node as Node;
            var sourceNode = newEdge?.output?.node as Node;
            if (targetNode == null || sourceNode == null) return false;

            var visited = new HashSet<Node>();
            var stack = new Stack<Node>();
            stack.Push(targetNode);

            while (stack.Count > 0) {
                var current = stack.Pop();
                if (current == sourceNode) return true;
                if (!visited.Add(current)) continue;

                foreach (var edge in GetOutgoingEdges(current, newEdge)) {
                    if (edge.input?.node is Node nextNode) {
                        stack.Push(nextNode);
                    }
                }
            }

            return false;
        }

        void CollectReachableNodesFromInputPort(Port port, HashSet<TerrainStepNode> reachable, HashSet<Port> visitedPorts,
            Edge candidateEdge = null, HashSet<Edge> visitedEdges = null) {
            if (port == null || visitedPorts == null || !visitedPorts.Add(port)) return;
            visitedEdges ??= new HashSet<Edge>();

            foreach (var edge in EnumerateConnectedEdges(port, candidateEdge)) {
                if (edge == null || !visitedEdges.Add(edge)) continue;

                Port otherPort = edge.input == port ? edge.output : edge.input;
                if (otherPort == null) continue;

                if (otherPort.node is TerrainStepNode stepNode) {
                    if (reachable.Add(stepNode)) {
                        CollectReachableNodesFromInputPort(stepNode.InputPort, reachable, visitedPorts, candidateEdge, visitedEdges);
                        CollectReachableNodesFromInputPort(stepNode.InputPortB, reachable, visitedPorts, candidateEdge, visitedEdges);
                    }
                } else if (otherPort.node is TerrainRerouteNode rerouteNode) {
                    CollectReachableNodesThroughReroute(rerouteNode, otherPort, reachable, visitedPorts, visitedEdges, candidateEdge);
                }
            }
        }

        void CollectReachableNodesThroughReroute(TerrainRerouteNode rerouteNode, Port entryPort, HashSet<TerrainStepNode> reachable,
            HashSet<Port> visitedPorts, HashSet<Edge> visitedEdges, Edge candidateEdge) {
            if (rerouteNode == null) return;

            Port oppositePort = entryPort == rerouteNode.InputPort ? rerouteNode.OutputPort : rerouteNode.InputPort;
            if (oppositePort != null) {
                CollectReachableNodesFromInputPort(oppositePort, reachable, visitedPorts, candidateEdge, visitedEdges);
            }

            if (entryPort != null && entryPort != oppositePort) {
                CollectReachableNodesFromInputPort(entryPort, reachable, visitedPorts, candidateEdge, visitedEdges);
            }
        }

        IEnumerable<Edge> GetOutgoingEdges(Node node, Edge candidateEdge = null) {
            if (node == null) yield break;

            if (node is TerrainStepNode stepNode) {
                foreach (var edge in EnumeratePortEdges(stepNode.OutputPort, candidateEdge)) {
                    yield return edge;
                }
                yield break;
            }

            if (node is TerrainRerouteNode rerouteNode) {
                foreach (var edge in EnumeratePortEdges(rerouteNode.OutputPort, candidateEdge)) {
                    yield return edge;
                }
            }
        }

        IEnumerable<Edge> EnumeratePortEdges(Port outputPort, Edge candidateEdge = null) {
            if (outputPort == null) yield break;

            if (candidateEdge != null && candidateEdge.output == outputPort) {
                yield return candidateEdge;
            }

            foreach (var edge in outputPort.connections) {
                if (edge == null || edge == candidateEdge) continue;
                yield return edge;
            }
        }

        IEnumerable<Edge> EnumerateConnectedEdges(Port port, Edge candidateEdge = null) {
            if (port == null) yield break;

            if (candidateEdge != null && (candidateEdge.input == port || candidateEdge.output == port)) {
                yield return candidateEdge;
            }

            foreach (var edge in port.connections) {
                if (edge == null || edge == candidateEdge) continue;
                yield return edge;
            }
        }

        void OnNodeValueChanged() {
            MarkDirty();
        }

        public List<TerrainGraphDiagnostic> BuildDiagnostics() {
            var diagnostics = new List<TerrainGraphDiagnostic>();
            var stepNodes = nodes.ToList().OfType<TerrainStepNode>().ToList();
            if (outputNode == null) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Error, "Missing altitude output", "The graph is missing the Altitude Output node."));
                return diagnostics;
            }

            TerrainStepNode altitudeTerminal = ResolveUpstreamStepNode(GetOutputNodeInput(TerrainGraphOutputKind.Altitude));
            if (altitudeTerminal == null) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Error,
                    "Altitude output disconnected",
                    "Connect a step to the Altitude Output node so the generator can produce terrain height.",
                    outputNode));
            }

            TerrainStepNode moistureTerminal = ResolveUpstreamStepNode(GetOutputNodeInput(TerrainGraphOutputKind.Moisture));
            if (moistureTerminal == null) {
                string message = generator != null && generator.GetResolvedLegacyMoistureTexture() != null
                    ? "No graph moisture output is connected. The generator will fall back to the legacy moisture texture."
                    : "No graph moisture output is connected. Moisture will default to 0 everywhere.";
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                    "Moisture output disconnected",
                    message,
                    moistureOutputNode));
            }

            var altitudeReachable = new HashSet<TerrainStepNode>();
            CollectReachableNodesFromInputPort(GetOutputNodeInput(TerrainGraphOutputKind.Altitude), altitudeReachable, new HashSet<Port>());
            var moistureReachable = CollectMoistureReachableNodes();
            var reachable = new HashSet<TerrainStepNode>(altitudeReachable);
            reachable.UnionWith(moistureReachable);

            var invalidMoistureNodes = moistureReachable.Where(node => TerrainStepNode.IsAltitudeOnlyOp(node.Operation)).ToList();
            if (invalidMoistureNodes.Count > 0) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                    "Altitude-only node in moisture branch",
                    "The moisture branch contains Beach Mask, Flatten Or Raise or Island, which are meant for altitude shaping.",
                    invalidMoistureNodes[0]));
            }

            string moistureMetersWarning = GetMoistureMetersWarning(moistureReachable);
            if (!string.IsNullOrEmpty(moistureMetersWarning)) {
                TerrainStepNode target = moistureReachable.FirstOrDefault(node => node != null && node.UsesMetersHeightUnits());
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                    "Moisture uses meters",
                    moistureMetersWarning,
                    target));
            }

            foreach (var node in stepNodes) {
                ValidateNode(node, reachable.Contains(node), diagnostics);
            }

            if (generator != null) {
                var snapshot = BuildPreviewSnapshot();
                if (altitudeTerminal != null && snapshot.terminalStepIndex < 0) {
                    diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Error,
                        "Altitude output cannot be evaluated",
                        "The Altitude Output is connected, but the terminal node is disabled or depends on missing inputs.",
                        altitudeTerminal));
                }
                if (moistureTerminal != null && snapshot.moistureTerminalStepIndex < 0) {
                    diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                        "Moisture output cannot be evaluated",
                        "The Moisture Output is connected, but the terminal node is disabled or depends on missing inputs. The generator will fall back to legacy moisture if available.",
                        moistureTerminal));
                }
                ValidateProbeRanges(snapshot, diagnostics);
            }

            diagnostics.Sort((a, b) => {
                int severity = a.Severity.CompareTo(b.Severity);
                return severity != 0 ? severity : string.Compare(a.Title, b.Title, StringComparison.Ordinal);
            });
            return diagnostics;
        }

        void ValidateNode(TerrainStepNode node, bool isReachable, List<TerrainGraphDiagnostic> diagnostics) {
            if (node == null || diagnostics == null) return;

            if (!isReachable) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Info,
                    "Unconnected node",
                    "This node does not contribute to Altitude or Moisture output.",
                    node));
            }

            if (TerrainStepNode.UsesImplicitValueFlow(node.Operation) && ResolveUpstreamStepNode(node.InputPort) == null) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                    "Missing main input",
                    "This operation expects an incoming value on its main input.",
                    node));
            }

            if (TerrainStepNode.HasImplicitValueAndSingleRefOp(node.Operation) && ResolveUpstreamStepNode(node.InputPortB) == null) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                    "Missing reference input",
                    "This operation expects a reference input on its secondary port.",
                    node));
            }

            if (TerrainStepNode.IsSingleRefOp(node.Operation) && ResolveUpstreamStepNode(node.InputPort) == null) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                    "Missing reference input",
                    "This operation expects a connected input.",
                    node));
            }

            if (TerrainStepNode.HasTwoInputs(node.Operation)) {
                if (ResolveUpstreamStepNode(node.InputPort) == null || ResolveUpstreamStepNode(node.InputPortB) == null) {
                    diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                        "Missing second input",
                        "Both inputs must be connected for this two-input operation to work as expected.",
                        node));
                }
            }

            switch (node.Operation) {
                case TerrainStepType.SampleHeightMapTexture:
                case TerrainStepType.SampleRidgeNoiseFromTexture:
                    if (node.noiseTexture == null) {
                        diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                            "Missing noise texture",
                            "Assign a texture to this sampler or it will evaluate as empty data.",
                            node));
                    }
                    if (node.noiseRangeMin > node.noiseRangeMax) {
                        diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                            "Invalid sampler range",
                            "Min is greater than Max. Swap them or the sampler remap will be inverted.",
                            node));
                    }
                    break;
                case TerrainStepType.SampleHeightMapUnityTerrain:
                    if (node.terrainData == null) {
                        diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                            "Missing terrain data",
                            "Assign a Unity TerrainData asset to this sampler.",
                            node));
                    }
                    if (node.noiseRangeMin > node.noiseRangeMax) {
                        diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                            "Invalid sampler range",
                            "Min is greater than Max. Swap them or the sampler remap will be inverted.",
                            node));
                    }
                    break;
                case TerrainStepType.SampleHeightMapFractal:
                    if (node.noiseRangeMin > node.noiseRangeMax) {
                        diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                            "Invalid sampler range",
                            "Min is greater than Max. Swap them or the fractal remap will be inverted.",
                            node));
                    }
                    break;
                case TerrainStepType.Clamp:
                case TerrainStepType.Select:
                case TerrainStepType.Fill:
                case TerrainStepType.Test:
                    if (node.min > node.max) {
                        diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                            "Invalid range",
                            "Range Min is greater than Range Max.",
                            node));
                    }
                    break;
                case TerrainStepType.Remap:
                    if (node.min > node.max) {
                        diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                            "Invalid remap source range",
                            "From Min is greater than From Max.",
                            node));
                    } else if (Mathf.Approximately(node.min, node.max)) {
                        diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                            "Collapsed remap range",
                            "From Min and From Max are equal, so this node will always output To Min.",
                            node));
                    }
                    break;
            }
        }

        void ValidateProbeRanges(GraphSnapshot snapshot, List<TerrainGraphDiagnostic> diagnostics) {
            if (generator == null || diagnostics == null) return;
            int altIdx = snapshot.terminalStepIndex;
            int moistIdx = snapshot.moistureTerminalStepIndex;
            if (altIdx < 0 && moistIdx < 0) return;

            double[] coords = { 0, 0, 100, 200, -150, 75, 50, -300, -200, -100, 350, 180, -420, 260, 600, -500 };
            float minAltitude = float.MaxValue;
            float maxAltitude = float.MinValue;
            float minMoisture = float.MaxValue;
            float maxMoisture = float.MinValue;

            for (int i = 0; i < coords.Length / 2; i++) {
                TerrainGraphPreviewRenderer.Evaluate(generator, snapshot.steps, altIdx, moistIdx,
                    coords[i * 2], coords[i * 2 + 1], out float altitude, out float moisture);
                minAltitude = Mathf.Min(minAltitude, altitude);
                maxAltitude = Mathf.Max(maxAltitude, altitude);
                minMoisture = Mathf.Min(minMoisture, moisture);
                maxMoisture = Mathf.Max(maxMoisture, moisture);
            }

            if (altIdx >= 0 && (minAltitude < -0.001f || maxAltitude > 1.001f)) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                    "Altitude probes outside 0..1",
                    $"Sampled altitude values currently span {minAltitude:0.###} .. {maxAltitude:0.###}. The final altitude is expected to stay close to 0..1 before Max Height scaling.",
                    outputNode));
            }

            if (moistIdx >= 0 && (minMoisture < -0.001f || maxMoisture > 1.001f)) {
                diagnostics.Add(new TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity.Warning,
                    "Moisture probes outside 0..1",
                    $"Sampled moisture values currently span {minMoisture:0.###} .. {maxMoisture:0.###}. Moisture is expected to stay in 0..1.",
                    moistureOutputNode));
            }
        }

        public void FocusElement(GraphElement element) {
            if (element == null) return;
            ClearSelection();
            AddToSelection(element);
            FrameSelection();
        }

        string SerializeSelectedGraphElements(IEnumerable<GraphElement> elements) {
            if (elements == null) return string.Empty;

            var selectedStepNodes = elements.OfType<TerrainStepNode>().ToList();
            var selectedReroutes = elements.OfType<TerrainRerouteNode>().ToList();
            if (selectedStepNodes.Count == 0 && selectedReroutes.Count == 0) {
                return string.Empty;
            }

            var nodeIndices = new Dictionary<TerrainStepNode, int>();
            var serializedNodes = new ClipboardNodeData[selectedStepNodes.Count];
            for (int i = 0; i < selectedStepNodes.Count; i++) {
                var node = selectedStepNodes[i];
                nodeIndices[node] = i;
                serializedNodes[i] = new ClipboardNodeData {
                    operation = node.Operation,
                    data = node.CollectStepData(),
                    position = node.GetPosition().position
                };
            }

            var rerouteIndices = new Dictionary<TerrainRerouteNode, int>();
            var serializedReroutes = new ClipboardRerouteData[selectedReroutes.Count];
            for (int i = 0; i < selectedReroutes.Count; i++) {
                var reroute = selectedReroutes[i];
                rerouteIndices[reroute] = i;
                serializedReroutes[i] = new ClipboardRerouteData {
                    position = reroute.GetPosition().position
                };
            }

            var serializedEdges = new List<ClipboardEdgeData>();
            foreach (var edge in edges.ToList()) {
                if (edge?.output == null || edge.input == null) continue;
                if (!IsClipboardEndpoint(edge.output.node, nodeIndices, rerouteIndices)) continue;
                if (!IsClipboardEndpoint(edge.input.node, nodeIndices, rerouteIndices)) continue;
                if (TryBuildClipboardEdgeData(edge, nodeIndices, rerouteIndices, out ClipboardEdgeData edgeData)) {
                    serializedEdges.Add(edgeData);
                }
            }

            return JsonUtility.ToJson(new ClipboardData {
                nodes = serializedNodes,
                reroutes = serializedReroutes,
                edges = serializedEdges.ToArray()
            });
        }

        bool CanPasteTerrainGraphData(string serializedData) {
            if (string.IsNullOrEmpty(serializedData)) return false;
            try {
                var data = JsonUtility.FromJson<ClipboardData>(serializedData);
                return (data.nodes != null && data.nodes.Length > 0)
                    || (data.reroutes != null && data.reroutes.Length > 0);
            } catch {
                return false;
            }
        }

        void UnserializeTerrainGraphData(string operationName, string serializedData) {
            if (!CanPasteTerrainGraphData(serializedData)) return;

            var data = JsonUtility.FromJson<ClipboardData>(serializedData);
            var nodeSnapshots = data.nodes ?? Array.Empty<ClipboardNodeData>();
            var rerouteSnapshots = data.reroutes ?? Array.Empty<ClipboardRerouteData>();
            var edgeSnapshots = data.edges ?? Array.Empty<ClipboardEdgeData>();
            Vector2 offset = new Vector2(40f, 40f);

            var createdNodes = new TerrainStepNode[nodeSnapshots.Length];
            var createdReroutes = new TerrainRerouteNode[rerouteSnapshots.Length];

            BeginBulkMutation();
            try {
                ClearSelection();

                for (int i = 0; i < nodeSnapshots.Length; i++) {
                    var snapshot = nodeSnapshots[i];
                    var node = CreateNode(snapshot.operation, snapshot.position + offset, allowAutoConnect: false);
                    node.ApplyFromStepData(snapshot.data);
                    node.UpdateDisplayTitle();
                    createdNodes[i] = node;
                    AddToSelection(node);
                }

                for (int i = 0; i < rerouteSnapshots.Length; i++) {
                    var reroute = CreateRerouteNode(rerouteSnapshots[i].position + offset);
                    createdReroutes[i] = reroute;
                    AddToSelection(reroute);
                }

                for (int i = 0; i < edgeSnapshots.Length; i++) {
                    TryCreateClipboardEdge(edgeSnapshots[i], createdNodes, createdReroutes);
                }
            } finally {
                EndBulkMutation();
            }
        }

        bool IsClipboardEndpoint(Node node, Dictionary<TerrainStepNode, int> nodeIndices,
            Dictionary<TerrainRerouteNode, int> rerouteIndices) {
            if (node is TerrainStepNode stepNode) {
                return nodeIndices.ContainsKey(stepNode);
            }
            if (node is TerrainRerouteNode rerouteNode) {
                return rerouteIndices.ContainsKey(rerouteNode);
            }
            return false;
        }

        bool TryBuildClipboardEdgeData(Edge edge, Dictionary<TerrainStepNode, int> nodeIndices,
            Dictionary<TerrainRerouteNode, int> rerouteIndices, out ClipboardEdgeData edgeData) {
            edgeData = default;
            if (!TryResolveClipboardEndpoint(edge.output.node, edge.output, nodeIndices, rerouteIndices, false,
                out TerrainGraphEndpointKind sourceKind, out int sourceIndex, out _)) {
                return false;
            }

            if (!TryResolveClipboardEndpoint(edge.input.node, edge.input, nodeIndices, rerouteIndices, true,
                out TerrainGraphEndpointKind targetKind, out int targetIndex, out int targetPortIndex)) {
                return false;
            }

            edgeData = new ClipboardEdgeData {
                sourceKind = sourceKind,
                sourceIndex = sourceIndex,
                targetKind = targetKind,
                targetIndex = targetIndex,
                targetPortIndex = targetPortIndex
            };
            return true;
        }

        bool TryResolveClipboardEndpoint(Node node, Port port, Dictionary<TerrainStepNode, int> nodeIndices,
            Dictionary<TerrainRerouteNode, int> rerouteIndices, bool isInput,
            out TerrainGraphEndpointKind kind, out int index, out int targetPortIndex) {
            kind = TerrainGraphEndpointKind.StepNode;
            index = -1;
            targetPortIndex = -1;

            if (node is TerrainStepNode stepNode) {
                if (!nodeIndices.TryGetValue(stepNode, out index)) return false;
                kind = TerrainGraphEndpointKind.StepNode;
                if (isInput) {
                    targetPortIndex = port == stepNode.InputPortB ? 1 : 0;
                }
                return true;
            }

            if (node is TerrainRerouteNode rerouteNode) {
                if (!rerouteIndices.TryGetValue(rerouteNode, out index)) return false;
                kind = TerrainGraphEndpointKind.Reroute;
                targetPortIndex = 0;
                return true;
            }

            return false;
        }

        void TryCreateClipboardEdge(ClipboardEdgeData edgeData, TerrainStepNode[] stepNodes, TerrainRerouteNode[] reroutes) {
            Port sourcePort = ResolveClipboardOutputPort(edgeData.sourceKind, edgeData.sourceIndex, stepNodes, reroutes);
            Port targetPort = ResolveClipboardInputPort(edgeData.targetKind, edgeData.targetIndex, edgeData.targetPortIndex, stepNodes, reroutes);
            if (sourcePort == null || targetPort == null) return;
            AddElement(sourcePort.ConnectTo(targetPort));
        }

        Port ResolveClipboardOutputPort(TerrainGraphEndpointKind kind, int index, TerrainStepNode[] stepNodes, TerrainRerouteNode[] reroutes) {
            switch (kind) {
                case TerrainGraphEndpointKind.StepNode:
                    return stepNodes != null && index >= 0 && index < stepNodes.Length ? stepNodes[index]?.OutputPort : null;
                case TerrainGraphEndpointKind.Reroute:
                    return reroutes != null && index >= 0 && index < reroutes.Length ? reroutes[index]?.OutputPort : null;
                default:
                    return null;
            }
        }

        Port ResolveClipboardInputPort(TerrainGraphEndpointKind kind, int index, int targetPortIndex,
            TerrainStepNode[] stepNodes, TerrainRerouteNode[] reroutes) {
            switch (kind) {
                case TerrainGraphEndpointKind.StepNode:
                    if (stepNodes == null || index < 0 || index >= stepNodes.Length || stepNodes[index] == null) return null;
                    return targetPortIndex == 1 ? stepNodes[index].InputPortB : stepNodes[index].InputPort;
                case TerrainGraphEndpointKind.Reroute:
                    return reroutes != null && index >= 0 && index < reroutes.Length ? reroutes[index]?.InputPort : null;
                default:
                    return null;
            }
        }

        public void AlignSelectionLeft() {
            AlignSelectedNodes(nodes => {
                float x = nodes.Min(node => node.GetPosition().xMin);
                foreach (var node in nodes) {
                    Rect rect = node.GetPosition();
                    rect.x = x;
                    node.SetPosition(rect);
                }
            });
        }

        public void AlignSelectionTop() {
            AlignSelectedNodes(nodes => {
                float y = nodes.Min(node => node.GetPosition().yMin);
                foreach (var node in nodes) {
                    Rect rect = node.GetPosition();
                    rect.y = y;
                    node.SetPosition(rect);
                }
            });
        }

        public void DistributeSelectionHorizontally() {
            AlignSelectedNodes(nodes => {
                var ordered = nodes.OrderBy(node => node.GetPosition().xMin).ToList();
                if (ordered.Count < 3) return;
                float minX = ordered.First().GetPosition().xMin;
                float maxX = ordered.Last().GetPosition().xMax;
                float totalWidth = ordered.Sum(node => node.GetPosition().width);
                float spacing = (maxX - minX - totalWidth) / (ordered.Count - 1);
                float currentX = minX + ordered[0].GetPosition().width + spacing;
                for (int i = 1; i < ordered.Count - 1; i++) {
                    Rect rect = ordered[i].GetPosition();
                    rect.x = currentX;
                    ordered[i].SetPosition(rect);
                    currentX += rect.width + spacing;
                }
            });
        }

        public void DistributeSelectionVertically() {
            AlignSelectedNodes(nodes => {
                var ordered = nodes.OrderBy(node => node.GetPosition().yMin).ToList();
                if (ordered.Count < 3) return;
                float minY = ordered.First().GetPosition().yMin;
                float maxY = ordered.Last().GetPosition().yMax;
                float totalHeight = ordered.Sum(node => node.GetPosition().height);
                float spacing = (maxY - minY - totalHeight) / (ordered.Count - 1);
                float currentY = minY + ordered[0].GetPosition().height + spacing;
                for (int i = 1; i < ordered.Count - 1; i++) {
                    Rect rect = ordered[i].GetPosition();
                    rect.y = currentY;
                    ordered[i].SetPosition(rect);
                    currentY += rect.height + spacing;
                }
            });
        }

        void AlignSelectedNodes(Action<List<Node>> applyLayout) {
            var selectedNodes = GetOrganizableSelectionNodes();
            if (selectedNodes.Count < 2 || applyLayout == null) return;

            BeginBulkMutation();
            try {
                applyLayout(selectedNodes);
            } finally {
                EndBulkMutation();
            }
        }

        // --- Helpers ---

        void UpdateLastPointerPosition(Vector2 worldPosition) {
            hasLastPointerPosition = true;
            lastPointerPositionLocal = contentViewContainer.WorldToLocal(worldPosition);
        }

        List<Node> GetOrganizableSelectionNodes() {
            return selection.OfType<Node>()
                .Where(node => node != null && node != outputNode && node != moistureOutputNode)
                .ToList();
        }

        public Vector2 GetViewportCenterLocal() {
            return contentViewContainer.WorldToLocal(worldBound.center);
        }

        public Vector2 GetPointerCreationLocal(TerrainStepType operation) {
            if (!hasLastPointerPosition) {
                return GetViewportCreationLocal(operation);
            }
            const float estimatedNodeWidth = 220f;
            float estimatedNodeHeight = TerrainStepNode.EstimateNodeHeight(operation);
            return lastPointerPositionLocal - new Vector2(estimatedNodeWidth * 0.5f, estimatedNodeHeight * 0.5f);
        }

        public Vector2 GetPointerRerouteLocal() {
            float halfSize = TerrainRerouteNode.NodeSize * 0.5f;
            if (!hasLastPointerPosition) {
                return GetViewportCenterLocal() - new Vector2(halfSize, halfSize);
            }
            return lastPointerPositionLocal - new Vector2(halfSize, halfSize);
        }

        public Vector2 GetViewportCreationLocal(TerrainStepType operation) {
            Rect viewport = worldBound;
            float estimatedNodeHeight = TerrainStepNode.EstimateNodeHeight(operation);
            Vector2 anchor = new Vector2(
                viewport.xMin + viewport.width / 4f,
                viewport.center.y - estimatedNodeHeight * 0.5f
            );
            return contentViewContainer.WorldToLocal(anchor);
        }

        public Vector2 ScreenToLocal(Vector2 screenPos) {
            var worldPos = screenPos - (Vector2)worldBound.position;
            return contentViewContainer.WorldToLocal(worldPos);
        }
    }
}
