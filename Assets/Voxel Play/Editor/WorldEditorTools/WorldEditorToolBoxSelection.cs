using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace VoxelPlay {

    public class WorldEditorToolBoxSelection : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolSelection");
        public override string title => "Box selection tool for copy, paste, delete, and fill operations";
        public override string instructions => "Click corners to select. Esc: Cancel. C: Copy. X: Cut. V: Paste. Del: Delete. F: Fill. G: Replace. R: Rotate paste.";
        public override int priority => 40;
        public override int minOpaque => 0;
        public override bool supportsContinuousMode => false;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;
        public override bool showRecentVoxels => true;

        // Selection state
        Vector3d selectionStart;
        Vector3d selectionEnd;
        bool hasFirstCorner;
        bool selectionActive;

        // Clipboard data
        static List<ClipboardVoxel> clipboard = new List<ClipboardVoxel>();
        static Vector3d clipboardSize;
        static Vector3d clipboardCenter;

        // Preview for paste
        bool showPastePreview;
        Vector3d pastePreviewPosition;

        // Fill operation
        bool showFillMode;

        // Replace operation
        bool showReplaceMode;

        [System.Serializable]
        class ClipboardVoxel {
            public Vector3d relativePosition;
            public VoxelDefinition voxelDefinition;
            public int textureRotation;
            public Color32 tintColor;
            public MicroVoxels microVoxels;
        }


        public override void SwitchTool () {
            hasFirstCorner = false;
            selectionActive = false;
            showPastePreview = false;
            showFillMode = false;
            showReplaceMode = false;
        }

        void ClearSelection () {
            hasFirstCorner = false;
            selectionActive = false;
            showFillMode = false;
            showReplaceMode = false;
            env.sceneEditorBrushHeightOffset = 0; // Reset elevation
        }

        void CopySelection () {
            if (!selectionActive) return;

            clipboard.Clear();

            Vector3d min = new Vector3d(
                Mathf.Min((float)selectionStart.x, (float)selectionEnd.x),
                Mathf.Min((float)selectionStart.y, (float)selectionEnd.y),
                Mathf.Min((float)selectionStart.z, (float)selectionEnd.z)
            );
            Vector3d max = new Vector3d(
                Mathf.Max((float)selectionStart.x, (float)selectionEnd.x),
                Mathf.Max((float)selectionStart.y, (float)selectionEnd.y),
                Mathf.Max((float)selectionStart.z, (float)selectionEnd.z)
            );

            clipboardCenter = (min + max) * 0.5;
            clipboardSize = max - min + Vector3d.one;

            for (double x = min.x; x <= max.x; x++) {
                for (double y = min.y; y <= max.y; y++) {
                    for (double z = min.z; z <= max.z; z++) {
                        Vector3d pos = new Vector3d(x, y, z);
                        if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) {
                            if (chunk != null && voxelIndex >= 0) {
                                Voxel voxel = chunk.voxels[voxelIndex];
                                if (voxel.typeIndex > 0) {
                                    MicroVoxels microVoxels = null;
                                    if (chunk.usesMicroVoxels && chunk.microVoxels != null && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv)) {
                                        microVoxels = mv.Clone();
                                    }
                                    ClipboardVoxel cv = new ClipboardVoxel {
                                        relativePosition = pos - clipboardCenter,
                                        voxelDefinition = env.voxelDefinitions[voxel.typeIndex],
                                        textureRotation = voxel.GetTextureRotation(),
                                        tintColor = voxel.color,
                                        microVoxels = microVoxels
                                    };
                                    clipboard.Add(cv);
                                }
                            }
                        }
                    }
                }
            }
            env.sceneEditorBrushHeightOffset = 0; // Reset elevation after copy
        }

        void DeleteSelection () {
            if (!selectionActive) return;

            // Start undo group
            undoManager.StartChangeGroup();

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            Vector3d min = new Vector3d(
                Mathf.Min((float)selectionStart.x, (float)selectionEnd.x),
                Mathf.Min((float)selectionStart.y, (float)selectionEnd.y),
                Mathf.Min((float)selectionStart.z, (float)selectionEnd.z)
            );
            Vector3d max = new Vector3d(
                Mathf.Max((float)selectionStart.x, (float)selectionEnd.x),
                Mathf.Max((float)selectionStart.y, (float)selectionEnd.y),
                Mathf.Max((float)selectionStart.z, (float)selectionEnd.z)
            );

            for (double x = min.x; x <= max.x; x++) {
                for (double y = min.y; y <= max.y; y++) {
                    for (double z = min.z; z <= max.z; z++) {
                        Vector3d pos = new Vector3d(x, y, z);
                        if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) {
                            if (chunk != null && voxelIndex >= 0) {
                                if (chunk.voxels[voxelIndex].typeIndex > 0) {
                                    undoManager.SaveChunk(chunk);
                                    chunk.ClearVoxel(voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                                    modifiedChunks.Add(chunk);
                                }
                            }
                        }
                    }
                }
            }

            RefreshModifiedChunks(modifiedChunks);
            BufferPool<VoxelChunk>.Release(modifiedChunks);

            // End undo group
            undoManager.EndChangeGroup();

            ClearSelection();
        }

        void FillSelection () {
            if (!selectionActive || env.sceneEditorBuildVoxel == null) return;

            // Start undo group
            undoManager.StartChangeGroup();

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            Vector3d min = new Vector3d(
                Mathf.Min((float)selectionStart.x, (float)selectionEnd.x),
                Mathf.Min((float)selectionStart.y, (float)selectionEnd.y),
                Mathf.Min((float)selectionStart.z, (float)selectionEnd.z)
            );
            Vector3d max = new Vector3d(
                Mathf.Max((float)selectionStart.x, (float)selectionEnd.x),
                Mathf.Max((float)selectionStart.y, (float)selectionEnd.y),
                Mathf.Max((float)selectionStart.z, (float)selectionEnd.z)
            );

            for (double x = min.x; x <= max.x; x++) {
                for (double y = min.y; y <= max.y; y++) {
                    for (double z = min.z; z <= max.z; z++) {
                        Vector3d pos = new Vector3d(x, y, z);
                        if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) {
                            undoManager.SaveChunk(chunk);
                            chunk.SetVoxel(voxelIndex, env.sceneEditorBuildVoxel);
                            modifiedChunks.Add(chunk);
                        }
                    }
                }
            }

            RefreshModifiedChunks(modifiedChunks);
            BufferPool<VoxelChunk>.Release(modifiedChunks);

            // End undo group
            undoManager.EndChangeGroup();

            ClearSelection();
        }

        void ReplaceSelection () {
            if (!selectionActive || env.sceneEditorReplaceSourceVoxel == null || env.sceneEditorBuildVoxel == null) return;

            env.AddVoxelDefinition(env.sceneEditorReplaceSourceVoxel);
            env.AddVoxelDefinition(env.sceneEditorBuildVoxel);
            int sourceIndex = env.sceneEditorReplaceSourceVoxel.index;
            if (sourceIndex <= 0) return;

            // Start undo group
            undoManager.StartChangeGroup();

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            Vector3d min = new Vector3d(
                Mathf.Min((float)selectionStart.x, (float)selectionEnd.x),
                Mathf.Min((float)selectionStart.y, (float)selectionEnd.y),
                Mathf.Min((float)selectionStart.z, (float)selectionEnd.z)
            );
            Vector3d max = new Vector3d(
                Mathf.Max((float)selectionStart.x, (float)selectionEnd.x),
                Mathf.Max((float)selectionStart.y, (float)selectionEnd.y),
                Mathf.Max((float)selectionStart.z, (float)selectionEnd.z)
            );

            for (double x = min.x; x <= max.x; x++) {
                for (double y = min.y; y <= max.y; y++) {
                    for (double z = min.z; z <= max.z; z++) {
                        Vector3d pos = new Vector3d(x, y, z);
                        if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) {
                            if (chunk != null && voxelIndex >= 0) {
                                if (chunk.voxels[voxelIndex].typeIndex == sourceIndex) {
                                    undoManager.SaveChunk(chunk);
                                    chunk.SetVoxel(voxelIndex, env.sceneEditorBuildVoxel);
                                    modifiedChunks.Add(chunk);
                                }
                            }
                        }
                    }
                }
            }

            RefreshModifiedChunks(modifiedChunks);
            BufferPool<VoxelChunk>.Release(modifiedChunks);

            // End undo group
            undoManager.EndChangeGroup();

            ClearSelection();
        }

        void PasteClipboard (Vector3d position) {
            if (clipboard.Count == 0) return;

            // Start undo group
            undoManager.StartChangeGroup();

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();

            foreach (var cv in clipboard) {
                Vector3d pos = position + cv.relativePosition;
                pos.y += env.sceneEditorBrushHeightOffset; // Apply height offset
                if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) {
                    undoManager.SaveChunk(chunk);
                    chunk.voxels[voxelIndex].Set(cv.voxelDefinition, cv.tintColor);
                    chunk.voxels[voxelIndex].SetTextureRotation(cv.textureRotation);
                    if (cv.microVoxels != null) {
                        chunk.SetMicroVoxels(voxelIndex, cv.microVoxels.Clone());
                    } else {
                        chunk.ClearMicroVoxels(voxelIndex);
                    }
                    modifiedChunks.Add(chunk);
                }
            }

            RefreshModifiedChunks(modifiedChunks);
            BufferPool<VoxelChunk>.Release(modifiedChunks);

            // End undo group
            undoManager.EndChangeGroup();

            showPastePreview = false;
            env.sceneEditorBrushHeightOffset = 0; // Reset elevation after paste
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            voxelIndices.Clear();

            if (showPastePreview) {
                // Don't select voxels when in paste mode
                return;
            }

            VoxelIndex vi = new VoxelIndex();
            vi.position = hitInfo.voxelCenter;
            vi.chunk = hitInfo.chunk;
            vi.voxelIndex = hitInfo.voxelIndex;
            voxelIndices.Add(vi);
        }

        public override void HighlightVoxels (ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {
            if (showPastePreview) {
                pastePreviewPosition = hitInfo.voxelCenter;
                return;
            }

            // Always show the highlight for the current voxel
            base.HighlightVoxels(ref hitInfo, voxelIndices, color, edgeWidth, fadeAmplitude);

            // Update the end position when dragging
            if (hasFirstCorner) {
                selectionEnd = hitInfo.voxelCenter;
                selectionEnd.y += env.sceneEditorBrushHeightOffset; // Apply height offset to selection box
            }
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {
            if (showPastePreview) {
                PasteClipboard(hitInfo.voxelCenter);
                return true;
            }

            if (!hasFirstCorner) {
                selectionStart = hitInfo.voxelCenter;
                selectionEnd = hitInfo.voxelCenter;
                hasFirstCorner = true;
                selectionActive = false;
                env.sceneEditorBrushHeightOffset = 0; // Reset elevation when selecting first corner
            } else {
                selectionEnd = hitInfo.voxelCenter;
                selectionEnd.y += env.sceneEditorBrushHeightOffset; // Apply height offset when setting second corner
                selectionActive = true;
                hasFirstCorner = false;
            }

            return true;
        }

        public override void Update () {
            // Handle keyboard shortcuts
            Event e = Event.current;
            if (e.type == EventType.KeyDown) {
                switch (e.keyCode) {
                    case KeyCode.Escape:
                        if (showPastePreview) {
                            showPastePreview = false;
                        } else {
                            ClearSelection();
                        }
                        e.Use();
                        break;
                    case KeyCode.C:
                        if (selectionActive) {
                            CopySelection();
                            e.Use();
                        }
                        break;
                    case KeyCode.X:
                        if (selectionActive) {
                            CopySelection();
                            DeleteSelection();
                            e.Use();
                        }
                        break;
                    case KeyCode.V:
                        if (clipboard.Count > 0) {
                            showPastePreview = true;
                            showFillMode = false;
                            e.Use();
                        }
                        break;
                    case KeyCode.Delete:
                        if (selectionActive) {
                            DeleteSelection();
                            e.Use();
                        }
                        break;
                    case KeyCode.F:
                        if (selectionActive) {
                            showFillMode = true;
                            showReplaceMode = false;
                            e.Use();
                        }
                        break;
                    case KeyCode.G:
                        if (selectionActive) {
                            showReplaceMode = true;
                            showFillMode = false;
                            e.Use();
                        }
                        break;
                }
            }

            // Handle mouse wheel for elevation in both paste mode and selection mode
            if (mouseWheel != 0) {
                float newHeight = env.sceneEditorBrushHeightOffset + (mouseWheel > 0 ? -1 : 1);
                env.sceneEditorBrushHeightOffset = Mathf.Max(0, newHeight); // Ensure height doesn't go below 0
                mouseWheel = 0;
            }

            if (showPastePreview) {
                // Handle rotation during paste
                if (keyR) {
                    // Rotate clipboard data
                    foreach (var cv in clipboard) {
                        double x = cv.relativePosition.x;
                        double z = cv.relativePosition.z;
                        cv.relativePosition.x = -z;
                        cv.relativePosition.z = x;
                        cv.textureRotation = (cv.textureRotation + 3) % 4;
                    }

                    double sizeX = clipboardSize.x;
                    double sizeZ = clipboardSize.z;
                    clipboardSize.x = sizeZ;
                    clipboardSize.z = sizeX;
                }
            }
        }

        public override void DrawGizmos (VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
            // Draw paste preview
            if (showPastePreview && clipboard.Count > 0) {
                // Calculate the correct center for the main bounding box
                Vector3d bBoxDisplayCenter = pastePreviewPosition;
                bBoxDisplayCenter.y += env.sceneEditorBrushHeightOffset; // Apply height offset
                if (((int)clipboardSize.x) % 2 == 0 && clipboardSize.x > 0) bBoxDisplayCenter.x += 0.5;
                if (((int)clipboardSize.y) % 2 == 0 && clipboardSize.y > 0) bBoxDisplayCenter.y += 0.5;
                if (((int)clipboardSize.z) % 2 == 0 && clipboardSize.z > 0) bBoxDisplayCenter.z += 0.5;

                // Draw bounding box
                Color boundingBoxColor = new Color(0, 0.5f, 1f, 0.2f);
                Handles.color = boundingBoxColor;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

                Handles.DrawWireCube((Vector3)bBoxDisplayCenter, (Vector3)clipboardSize);

                // Draw individual voxel previews
                // Group voxels by type for more efficient drawing
                Dictionary<VoxelDefinition, List<Vector3>> voxelsByType = new Dictionary<VoxelDefinition, List<Vector3>>();

                foreach (var cv in clipboard) {
                    Vector3d rawTargetPos = pastePreviewPosition + cv.relativePosition;
                    rawTargetPos.y += env.sceneEditorBrushHeightOffset; // Apply height offset
                    Vector3 voxelDrawCenter = new Vector3(
                        Mathf.Floor((float)rawTargetPos.x) + 0.5f,
                        Mathf.Floor((float)rawTargetPos.y) + 0.5f,
                        Mathf.Floor((float)rawTargetPos.z) + 0.5f
                    );

                    if (!voxelsByType.ContainsKey(cv.voxelDefinition)) {
                        voxelsByType[cv.voxelDefinition] = new List<Vector3>();
                    }
                    voxelsByType[cv.voxelDefinition].Add(voxelDrawCenter);
                }

                // Draw voxels grouped by type with slightly different colors
                int typeIndex = 0;
                foreach (var kvp in voxelsByType) {
                    // Vary the color slightly for different voxel types
                    float hueShift = (typeIndex * 0.1f) % 1.0f;
                    Color typeColor = Color.HSVToRGB((0.55f + hueShift) % 1.0f, 0.6f, 0.8f);
                    typeColor.a = 0.5f;
                    Handles.color = typeColor;

                    // Draw all voxels of this type
                    foreach (var pos in kvp.Value) {
                        Handles.DrawWireCube(pos, Vector3.one);
                    }

                    typeIndex++;
                }

                // Draw corner handles for the bounding box
                Handles.color = new Color(0, 0.8f, 1f, 0.8f);
                float handleSize = 0.2f;
                Vector3 displayBBoxCenterF = (Vector3)bBoxDisplayCenter;
                Vector3 clipboardSizeF = (Vector3)clipboardSize;
                Vector3 halfSize = clipboardSizeF * 0.5f;
                Vector3[] corners = new Vector3[8];
                corners[0] = displayBBoxCenterF + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
                corners[1] = displayBBoxCenterF + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
                corners[2] = displayBBoxCenterF + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
                corners[3] = displayBBoxCenterF + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
                corners[4] = displayBBoxCenterF + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
                corners[5] = displayBBoxCenterF + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
                corners[6] = displayBBoxCenterF + new Vector3(-halfSize.x, halfSize.y, halfSize.z);
                corners[7] = displayBBoxCenterF + new Vector3(halfSize.x, halfSize.y, halfSize.z);

                foreach (var corner in corners) {
                    Handles.DrawWireCube(corner, Vector3.one * handleSize);
                }
                return;
            }
        }

        public override void DrawPersistentGizmos () {
            // Draw selection box (both when selecting and when selection is active)
            if (hasFirstCorner || selectionActive) {
                Color highlightColor;
                if (showFillMode) {
                    highlightColor = Color.cyan;
                } else if (showReplaceMode) {
                    highlightColor = new Color(1f, 0.5f, 0f); // orange
                } else {
                    highlightColor = Color.yellow;
                }

                highlightColor.a = 0.6f;
                Handles.color = highlightColor;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

                Vector3d min = new Vector3d(
                    Mathf.Min((float)selectionStart.x, (float)selectionEnd.x),
                    Mathf.Min((float)selectionStart.y, (float)selectionEnd.y),
                    Mathf.Min((float)selectionStart.z, (float)selectionEnd.z)
                );
                Vector3d max = new Vector3d(
                    Mathf.Max((float)selectionStart.x, (float)selectionEnd.x),
                    Mathf.Max((float)selectionStart.y, (float)selectionEnd.y),
                    Mathf.Max((float)selectionStart.z, (float)selectionEnd.z)
                );

                Vector3 center = (min + max) * 0.5f;
                Vector3 size = max - min + Vector3d.one;

                Handles.DrawWireCube(center, size);

                // Draw corner handles for better visibility
                float handleSize = 0.2f;
                Vector3[] corners = new Vector3[8];
                corners[0] = new Vector3((float)(min.x - 0.5), (float)(min.y - 0.5), (float)(min.z - 0.5));
                corners[1] = new Vector3((float)(max.x + 0.5), (float)(min.y - 0.5), (float)(min.z - 0.5));
                corners[2] = new Vector3((float)(min.x - 0.5), (float)(max.y + 0.5), (float)(min.z - 0.5));
                corners[3] = new Vector3((float)(min.x - 0.5), (float)(min.y - 0.5), (float)(max.z + 0.5));
                corners[4] = new Vector3((float)(max.x + 0.5), (float)(max.y + 0.5), (float)(min.z - 0.5));
                corners[5] = new Vector3((float)(max.x + 0.5), (float)(min.y - 0.5), (float)(max.z + 0.5));
                corners[6] = new Vector3((float)(min.x - 0.5), (float)(max.y + 0.5), (float)(max.z + 0.5));
                corners[7] = new Vector3((float)(max.x + 0.5), (float)(max.y + 0.5), (float)(max.z + 0.5));

                foreach (var corner in corners) {
                    Handles.DrawWireCube(corner, Vector3.one * handleSize);
                }
            }
        }

        public override void DrawLabel (VoxelHitInfo hitInfo) {
            if (hasFirstCorner && !selectionActive) {
                Vector3d current = hitInfo.voxelCenter;
                Vector3d min = new Vector3d(
                    Mathf.Min((float)selectionStart.x, (float)current.x),
                    Mathf.Min((float)selectionStart.y, (float)current.y),
                    Mathf.Min((float)selectionStart.z, (float)current.z)
                );
                Vector3d max = new Vector3d(
                    Mathf.Max((float)selectionStart.x, (float)current.x),
                    Mathf.Max((float)selectionStart.y, (float)current.y),
                    Mathf.Max((float)selectionStart.z, (float)current.z)
                );
                Vector3d size = max - min + Vector3d.one;

                string label = $"Size: {size.x} x {size.y} x {size.z}";
                Handles.Label(hitInfo.point + Vector3.one * 0.5f, label);
            }
        }

        public override void DrawInspector () {
            EditorGUILayout.LabelField("Selection Info", EditorStyles.boldLabel);

            if (selectionActive) {
                Vector3d min = new Vector3d(
                    Mathf.Min((float)selectionStart.x, (float)selectionEnd.x),
                    Mathf.Min((float)selectionStart.y, (float)selectionEnd.y),
                    Mathf.Min((float)selectionStart.z, (float)selectionEnd.z)
                );
                Vector3d max = new Vector3d(
                    Mathf.Max((float)selectionStart.x, (float)selectionEnd.x),
                    Mathf.Max((float)selectionStart.y, (float)selectionEnd.y),
                    Mathf.Max((float)selectionStart.z, (float)selectionEnd.z)
                );
                Vector3d size = max - min + Vector3d.one;

                EditorGUI.BeginChangeCheck();
                Vector3 newStart = EditorGUILayout.Vector3Field("Start", (Vector3)selectionStart);
                Vector3 newEnd = EditorGUILayout.Vector3Field("End", (Vector3)selectionEnd);
                if (EditorGUI.EndChangeCheck()) {
                    selectionStart = new Vector3d(newStart);
                    selectionEnd = new Vector3d(newEnd);
                }

                using (new EditorGUI.DisabledScope(true)) {
                    EditorGUILayout.Vector3Field("Size", size);
                    EditorGUILayout.IntField("Volume", (int)(size.x * size.y * size.z));
                }
            } else if (hasFirstCorner) {
                EditorGUI.BeginChangeCheck();
                Vector3 newStart = EditorGUILayout.Vector3Field("Start", (Vector3)selectionStart);
                if (EditorGUI.EndChangeCheck()) {
                    selectionStart = new Vector3d(newStart);
                }
                EditorGUILayout.LabelField("Click to set second corner...");
            } else {
                EditorGUILayout.LabelField("Click to set first corner");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Operations", EditorStyles.boldLabel);

            // Basic operations
            GUI.enabled = selectionActive;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Copy (C)", "Copy selected voxels to clipboard"), EditorStyles.miniButton)) {
                CopySelection();
            }
            if (GUILayout.Button(new GUIContent("Cut (X)", "Copy and delete selected voxels"), EditorStyles.miniButton)) {
                CopySelection();
                DeleteSelection();
            }
            GUI.enabled = clipboard.Count > 0;
            if (GUILayout.Button(new GUIContent("Paste (V)", "Paste clipboard contents"), EditorStyles.miniButton)) {
                showPastePreview = true;
                showFillMode = false;
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Fill (F)", "Fill selection with voxel"), EditorStyles.miniButton)) {
                showFillMode = true;
                showReplaceMode = false;
            }
            if (GUILayout.Button(new GUIContent("Replace (G)", "Replace one voxel type with another"), EditorStyles.miniButton)) {
                showReplaceMode = true;
                showFillMode = false;
            }
            if (GUILayout.Button(new GUIContent("Delete (Del)", "Delete selected voxels"), EditorStyles.miniButton)) {
                DeleteSelection();
            }
            EditorGUILayout.EndHorizontal();


            // Fill mode UI
            if (showFillMode && selectionActive) {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Fill Settings", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                env.sceneEditorBuildVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Fill Voxel", env.sceneEditorBuildVoxel, typeof(VoxelDefinition), false);
                if (EditorGUI.EndChangeCheck() && env.sceneEditorBuildVoxel != null) {
                    if (VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                        VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorBuildVoxel);
                    }
                }
                if (env.sceneEditorBuildVoxel != null && GUILayout.Button("Execute Fill")) {
                    FillSelection();
                    showFillMode = false;
                }
            }

            // Replace mode UI
            if (showReplaceMode && selectionActive) {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Replace Settings", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                env.sceneEditorReplaceSourceVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Source Voxel", env.sceneEditorReplaceSourceVoxel, typeof(VoxelDefinition), false);
                env.sceneEditorBuildVoxel = (VoxelDefinition)EditorGUILayout.ObjectField("Target Voxel", env.sceneEditorBuildVoxel, typeof(VoxelDefinition), false);
                if (EditorGUI.EndChangeCheck()) {
                    if (env.sceneEditorReplaceSourceVoxel != null && VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                        VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorReplaceSourceVoxel);
                    }
                    if (env.sceneEditorBuildVoxel != null && VoxelPlayEnvironmentEditor.currentEditingEnv != null) {
                        VoxelPlayEnvironmentEditor.currentEditingEnv.AddRecentVoxel(env.sceneEditorBuildVoxel);
                    }
                }
                if (env.sceneEditorReplaceSourceVoxel != null && env.sceneEditorBuildVoxel != null && GUILayout.Button("Execute Replace")) {
                    ReplaceSelection();
                    showReplaceMode = false;
                }
            }

            // Clipboard info
            if (clipboard.Count > 0) {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Clipboard", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true)) {
                    EditorGUILayout.Vector3Field("Size", clipboardSize);
                    EditorGUILayout.IntField("Voxel Count", clipboard.Count);
                }
            }

            EditorGUILayout.Space();
            GUI.enabled = selectionActive || hasFirstCorner;
            if (GUILayout.Button(new GUIContent("Clear Selection (Esc)", "Clear current selection"), EditorStyles.miniButton)) {
                ClearSelection();
            }
            GUI.enabled = true;

            // Recent voxels grid with drag & drop support (only when slots are visible)
            if ((showFillMode || showReplaceMode) && selectionActive) {
                DrawDraggableRecentVoxelsGrid();
            }

            // Add rotation and elevation controls
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Placement Settings", EditorStyles.boldLabel);
            env.sceneEditorBrushHeightOffset = EditorGUILayout.Slider(new GUIContent("Height Offset (Wheel)", "Use mousewheel to change elevation."), env.sceneEditorBrushHeightOffset, 0f, 10f); // Changed min value to 0
            EditorGUILayout.BeginHorizontal();
            env.sceneEditorPlacementRotation = EditorGUILayout.IntSlider("Rotation (R)", env.sceneEditorPlacementRotation, 0, 3);
            GUILayout.Label(env.sceneEditorPlacementRotation * 90 + "°");
            EditorGUILayout.EndHorizontal();
        }

        int _dragSourceIndex = -1;

        void DrawDraggableRecentVoxelsGrid () {
            if (VoxelPlayEnvironmentEditor.currentEditingEnv == null) return;
            var recentVoxels = VoxelPlayEnvironmentEditor.currentEditingEnv.GetRecentVoxels(env);
            if (recentVoxels.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recent Voxels (drag into slots above)", EditorStyles.boldLabel);

            int columns = 4;
            int rows = Mathf.CeilToInt(recentVoxels.Count / (float)columns);
            float cellHeight = 70;
            float gridHeight = rows * cellHeight;
            Rect gridRect = GUILayoutUtility.GetRect(0, gridHeight, GUILayout.ExpandWidth(true));
            float cellWidth = gridRect.width / columns;

            Event evt = Event.current;

            for (int i = 0; i < recentVoxels.Count; i++) {
                int col = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(gridRect.x + col * cellWidth, gridRect.y + row * cellHeight, cellWidth, cellHeight);

                VoxelDefinition vd = recentVoxels[i];
                Texture2D icon = null;
                if (vd.icon != null) icon = vd.icon;
                else if (vd.textureSample != null) icon = vd.textureSample;
                else if (vd.textureSide != null) icon = vd.textureSide;
                else if (vd.textureTop != null) icon = vd.textureTop;

                if (evt.type == EventType.MouseDown && cellRect.Contains(evt.mousePosition)) {
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    _dragSourceIndex = i;
                    evt.Use();
                }

                if (evt.type == EventType.MouseDrag && _dragSourceIndex == i) {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { vd };
                    DragAndDrop.StartDrag(vd.name);
                    _dragSourceIndex = -1;
                    evt.Use();
                }

                // Click-to-select: assign to Fill Voxel / Target Voxel slot
                if (evt.type == EventType.MouseUp && _dragSourceIndex == i && cellRect.Contains(evt.mousePosition)) {
                    env.sceneEditorBuildVoxel = vd;
                    VoxelPlayEnvironmentEditor.currentEditingEnv?.AddRecentVoxel(vd);
                    _dragSourceIndex = -1;
                    evt.Use();
                }

                GUI.Button(cellRect, new GUIContent(icon, vd.name));
            }

            if (evt.type == EventType.MouseUp) {
                _dragSourceIndex = -1;
            }

            EditorGUILayout.Space();
        }

        public override bool RayCast (Ray ray, out VoxelHitInfo hitInfo) {
            if (showPastePreview || hasFirstCorner) {
                // In paste mode or during selection, ignore vegetation by using minOpaque = 4
                return base.RayCast(ray, out hitInfo, minOpaque: 4);
            }
            return base.RayCast(ray, out hitInfo);
        }
    }
}