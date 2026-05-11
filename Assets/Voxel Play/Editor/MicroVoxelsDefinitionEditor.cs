using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [CustomEditor(typeof(MicroVoxelsDefinition))]
    public class MicroVoxelsDefinitionEditor : Editor {

        static bool microVoxelCustomHeightMode;
        static bool microVoxelBottomSlabMode;
        static bool microVoxelTopSlabMode;
        static int microVoxelHeight = MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;

        GUIContent microVoxelIconClear;
        GUIContent microVoxelIconBottomSlab;
        GUIContent microVoxelIconTopSlab;
        GUIContent microVoxelIconCustomHeight;

        Editor meshPreviewEditor;
        Mesh previewMesh;
        ulong previewHash;

        void OnEnable() {
            SetupMicroVoxelIcons();
        }

        void OnDisable() {
            DestroyMeshPreviewEditor();
        }

        void DestroyMeshPreviewEditor() {
            if (meshPreviewEditor != null) {
                DestroyImmediate(meshPreviewEditor);
                meshPreviewEditor = null;
            }
        }

        public override void OnInspectorGUI() {
            MicroVoxelsDefinition mvd = (MicroVoxelsDefinition)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MicroVoxels Definition", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Draw preview and tools
            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (mvd.microVoxels != null && !mvd.microVoxels.isEmpty) {
                EditorGUILayout.BeginHorizontal();
                DrawMicroVoxelPreview(mvd);
                DrawMicroVoxelLayerPreview(mvd.microVoxels);
                EditorGUILayout.EndHorizontal();
            }

            DrawMicroVoxelToolRibbon(mvd);

            EditorGUILayout.EndVertical();

            // Show stats
            EditorGUILayout.Space();
            if (mvd.microVoxels != null) {
                EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Occupied Count", mvd.microVoxels.count.ToString());
                EditorGUILayout.LabelField("Total Capacity", MicroVoxels.COUNT_PER_VOXEL.ToString());
                float fillPercent = (float)mvd.microVoxels.count / MicroVoxels.COUNT_PER_VOXEL * 100f;
                EditorGUILayout.LabelField("Fill Percentage", fillPercent.ToString("F1") + "%");
                EditorGUILayout.LabelField("Layout", mvd.microVoxels.layout.ToString());
            } else {
                EditorGUILayout.HelpBox("No microvoxels data. Use the tools above to create some.", MessageType.Info);
            }
        }

        void SetupMicroVoxelIcons() {
            microVoxelIconClear = CreateMicroVoxelIcon("TreeEditor.Trash", "Clr", "Clear microvoxels");
            microVoxelIconBottomSlab = CreateIconFromResource("VoxelPlay/Inspector/bottomSlab", "Bot", "Fill bottom slab");
            microVoxelIconTopSlab = CreateIconFromResource("VoxelPlay/Inspector/topSlab", "Top", "Fill top slab");
            microVoxelIconCustomHeight = CreateMicroVoxelIcon("Animation.AddEvent", "H", "Custom height fill");
        }

        static GUIContent CreateMicroVoxelIcon(string iconName, string fallbackText, string tooltip) {
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            if (content == null || content.image == null) {
                content = new GUIContent(fallbackText, tooltip);
            } else {
                content = new GUIContent(content) {
                    tooltip = tooltip
                };
            }
            return content;
        }

        static GUIContent CreateIconFromResource(string resourcePath, string fallbackText, string tooltip) {
            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null) {
                return new GUIContent(tex, tooltip);
            }
            return new GUIContent(fallbackText, tooltip);
        }

        void DrawMicroVoxelPreview(MicroVoxelsDefinition mvd) {
            if (mvd.microVoxels == null || mvd.microVoxels.isEmpty) return;

            Mesh mesh = GetOrCreatePreviewMesh(mvd);
            if (mesh == null) return;

            if (meshPreviewEditor == null || meshPreviewEditor.target != mesh) {
                DestroyMeshPreviewEditor();
                meshPreviewEditor = CreateEditor(mesh);
            }

            Rect previewRect = GUILayoutUtility.GetRect(120f, 150f, GUILayout.ExpandWidth(true));
            bool skipPreviewEvent = Event.current != null && Event.current.type == EventType.ScrollWheel && !previewRect.Contains(Event.current.mousePosition);
            if (!skipPreviewEvent) {
                meshPreviewEditor.OnInteractivePreviewGUI(previewRect, GUIStyle.none);
            }
        }

        Mesh GetOrCreatePreviewMesh(MicroVoxelsDefinition mvd) {
            if (mvd.microVoxels == null) return null;

            ulong currentHash = mvd.microVoxels.GetGridHashCode();
            if (previewMesh != null && currentHash == previewHash) {
                return previewMesh;
            }

            previewHash = currentHash;

            // Build mesh from microvoxels
            MeshingThreadMicroVoxels mesher = new MeshingThreadMicroVoxels();
            if (mvd.microVoxels.prototype == null || mvd.microVoxels.needsMeshDataUpdate) {
                mvd.microVoxels.needsMeshDataUpdate = false;
                mesher.UpdateMeshData(mvd.microVoxels);
            }

            MicroVoxelsPrototype proto = mvd.microVoxels.prototype;
            if (proto == null) return null;

            System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> uvs = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
            int vertexOffset = 0;

            for (int side = 0; side < 6; side++) {
                var verts = proto.sidesVertexData[side].vertices;
                var uv2 = proto.sidesVertexData[side].uvs;
                int sideVertexCount = verts.Count;
                for (int i = 0; i < sideVertexCount; i += 4) {
                    for (int k = 0; k < 4; k++) {
                        vertices.Add(verts[i + k]);
                        uvs.Add(uv2[i + k]);
                    }
                    triangles.Add(vertexOffset + 0);
                    triangles.Add(vertexOffset + 1);
                    triangles.Add(vertexOffset + 3);
                    triangles.Add(vertexOffset + 3);
                    triangles.Add(vertexOffset + 2);
                    triangles.Add(vertexOffset + 0);
                    vertexOffset += 4;
                }
            }

            if (previewMesh == null) {
                previewMesh = new Mesh();
                previewMesh.name = "MicroVoxelsPreview";
            } else {
                previewMesh.Clear();
            }

            previewMesh.SetVertices(vertices);
            previewMesh.SetUVs(0, uvs);
            previewMesh.SetTriangles(triangles, 0);
            previewMesh.RecalculateNormals();

            return previewMesh;
        }

        void DrawMicroVoxelLayerPreview(MicroVoxels mv) {
            Rect rect = GUILayoutUtility.GetRect(48f, 130f, GUILayout.ExpandWidth(false));
            if (Event.current.type == EventType.Repaint) {
                Color bg = EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.9f, 0.9f, 0.9f);
                EditorGUI.DrawRect(rect, bg);
                float layerHeight = rect.height / MicroVoxels.COUNT_PER_AXIS;
                for (int layer = 0; layer < MicroVoxels.COUNT_PER_AXIS; layer++) {
                    Rect layerRect = new Rect(rect.x + 2f, rect.y + rect.height - (layer + 1) * layerHeight + 1f, rect.width - 4f, layerHeight - 2f);
                    float fill = GetLayerFillRatio(mv, layer);
                    Color fillColor = Color.Lerp(new Color(0.25f, 0.25f, 0.25f), new Color(0.2f, 0.7f, 1f), fill);
                    EditorGUI.DrawRect(layerRect, fillColor);
                }
                GUIStyle topStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
                GUIStyle bottomStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerCenter };
                GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 12f), "Top", topStyle);
                GUI.Label(new Rect(rect.x, rect.yMax - 14f, rect.width, 12f), "Bottom", bottomStyle);
            }
        }

        static float GetLayerFillRatio(MicroVoxels mv, int y) {
            if (mv == null) return 0f;
            int start = y * MicroVoxels.COUNT_PER_FACE;
            int filled = 0;
            for (int i = 0; i < MicroVoxels.COUNT_PER_FACE; i++) {
                int idx = start + i;
                int ulongIndex = idx / 64;
                int bit = idx % 64;
                if ((mv.gridData[ulongIndex] & (1UL << bit)) != 0) filled++;
            }
            return (float)filled / MicroVoxels.COUNT_PER_FACE;
        }

        void DrawMicroVoxelToolRibbon(MicroVoxelsDefinition mvd) {
            MicroVoxels current = mvd.microVoxels;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUI.enabled = current != null && !current.isEmpty;
            if (GUILayout.Button(microVoxelIconClear, EditorStyles.toolbarButton, GUILayout.Width(32f))) {
                ApplyMicroVoxelChange(mvd, "Clear MicroVoxels", target => {
                    target.microVoxels = null;
                });
            }
            GUI.enabled = true;

            bool newBottom = GUILayout.Toggle(microVoxelBottomSlabMode, microVoxelIconBottomSlab, EditorStyles.toolbarButton, GUILayout.Width(32f));
            if (newBottom != microVoxelBottomSlabMode) {
                microVoxelBottomSlabMode = newBottom;
                if (microVoxelBottomSlabMode) {
                    microVoxelTopSlabMode = false;
                    microVoxelCustomHeightMode = false;
                    ApplyMicroVoxelTemplate(mvd, MicroVoxels.bottomHalfDefaultVoxelTemplate, "Bottom Slab MicroVoxels");
                }
            }

            bool newTop = GUILayout.Toggle(microVoxelTopSlabMode, microVoxelIconTopSlab, EditorStyles.toolbarButton, GUILayout.Width(32f));
            if (newTop != microVoxelTopSlabMode) {
                microVoxelTopSlabMode = newTop;
                if (microVoxelTopSlabMode) {
                    microVoxelBottomSlabMode = false;
                    microVoxelCustomHeightMode = false;
                    ApplyMicroVoxelTemplate(mvd, MicroVoxels.topHalfVoxelTemplate, "Top Slab MicroVoxels");
                }
            }

            bool newCustom = GUILayout.Toggle(microVoxelCustomHeightMode, microVoxelIconCustomHeight, EditorStyles.toolbarButton, GUILayout.Width(32f));
            if (newCustom != microVoxelCustomHeightMode) {
                microVoxelCustomHeightMode = newCustom;
                if (microVoxelCustomHeightMode) {
                    microVoxelBottomSlabMode = false;
                    microVoxelTopSlabMode = false;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (microVoxelCustomHeightMode) {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                microVoxelHeight = Mathf.Clamp(microVoxelHeight, 1, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE);
                microVoxelHeight = EditorGUILayout.IntSlider("Custom Height", microVoxelHeight, 1, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE);
                bool removingFullHeight = microVoxelHeight >= MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
                if (GUILayout.Button("Apply", GUILayout.Width(70f))) {
                    if (removingFullHeight) {
                        bool confirm = EditorUtility.DisplayDialog(
                            "Remove Microvoxels",
                            "Full-height microvoxels equal to not using microvoxels. Apply will remove them. Continue?",
                            "Remove",
                            "Cancel"
                        );
                        if (confirm) {
                            ApplyMicroVoxelChange(mvd, "Clear MicroVoxels", target => {
                                target.microVoxels = null;
                            });
                        }
                    } else {
                        ApplyMicroVoxelChange(mvd, "Apply MicroVoxel Height", target => {
                            ApplyMicroVoxelHeight(target, microVoxelHeight);
                        });
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        void ApplyMicroVoxelTemplate(MicroVoxelsDefinition mvd, MicroVoxels template, string undoTitle) {
            if (template == null) return;
            ApplyMicroVoxelChange(mvd, undoTitle, target => {
                MicroVoxels clone = template.Clone();
                clone.needsMeshDataUpdate = true;
                clone.isShared = true;
                target.microVoxels = clone;
            });
        }

        void ApplyMicroVoxelHeight(MicroVoxelsDefinition mvd, int layerCount) {
            int clampedLayers = Mathf.Clamp(layerCount, 1, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE);
            MicroVoxels mv = CreateMicroVoxels(mvd, true);
            for (int y = 0; y < clampedLayers; y++) {
                int layerStart = y * MicroVoxels.COUNT_PER_FACE;
                int layerEnd = layerStart + MicroVoxels.COUNT_PER_FACE;
                for (int index = layerStart; index < layerEnd; index++) {
                    mv.SetOccupied(index);
                }
            }
            mv.layout = MicroVoxelLayout.Default;
            mv.isShared = true;
            mvd.microVoxels = mv;
        }

        static MicroVoxels CreateMicroVoxels(MicroVoxelsDefinition mvd, bool clear) {
            MicroVoxels mv = mvd.microVoxels;
            if (mv == null) {
                mv = new MicroVoxels();
            } else if (mv.isShared) {
                mv = mv.Clone();
            }
            if (clear) {
                mv.Clear();
            }
            mv.isShared = true;
            mv.needsMeshDataUpdate = true;
            return mv;
        }

        void ApplyMicroVoxelChange(MicroVoxelsDefinition mvd, string undoTitle, System.Action<MicroVoxelsDefinition> action) {
            if (mvd == null) return;
            Undo.RecordObject(mvd, undoTitle);
            action(mvd);
            DestroyMeshPreviewEditor();
            previewMesh = null;
            EditorUtility.SetDirty(mvd);
            Repaint();
        }

    }

}
