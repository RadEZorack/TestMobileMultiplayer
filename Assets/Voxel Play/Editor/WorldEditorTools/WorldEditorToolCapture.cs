using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.NVIDIA;
using System.IO;

namespace VoxelPlay {

    public class WorldEditorToolCapture : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolCapture");
        public override string title => "Capture voxels into a model definition";
        public override string instructions => "Click to fix/release grid position.";
        public override int priority => 110;
        public override int minOpaque => 5;
        public override bool supportsContinuousMode => false;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;
        GameObject selectionBox;
        Material gridMat;
        bool fixedPosition;
        Vector3d gridPos;


        public WorldEditorToolCapture () : base() {
            DestroyGrid();
        }

        public override void Dispose () {
            DestroyGrid();
        }

        public override void SwitchTool () {
            if (fixedPosition) {
                return;
            }
            DestroyGrid();
        }

        void DestroyGrid () {
            fixedPosition = false;
            VoxelGrid[] grids = Misc.FindObjectsOfType<VoxelGrid>();
            foreach (var grid in grids) {
                Object.DestroyImmediate(grid.gameObject);
            }
            if (gridMat != null) {
                Object.DestroyImmediate(gridMat);
            }
        }

        void ClearContents () {
            Bounds bounds = selectionBox.GetComponent<Renderer>().bounds;
            List<VoxelChunk> chunks = BufferPool<VoxelChunk>.Get();
            try {
                env.GetChunks(bounds, chunks);
                foreach (var chunk in chunks) {
                    undoManager.SaveChunk(chunk);
                }
            }
            finally {
                BufferPool<VoxelChunk>.Release(chunks);
            }
            env.VoxelDestroy(bounds);
        }

        void CaptureGrid () {
            if (string.IsNullOrEmpty(env.sceneEditorCaptureFileName)) {
                EditorUtility.DisplayDialog("Capture Error", "Please enter a model filename", "Ok");
                return;
            }
            if (selectionBox == null) return;
            Bounds bounds = selectionBox.GetComponent<Renderer>().bounds;
            ModelDefinition model = env.ModelWorldCapture(bounds, env.sceneEditorCaptureVoxelTintColor);
            if (model != null) {
                model.offset = env.sceneEditorCaptureOffset;
                string path = AssetDatabase.GenerateUniqueAssetPath("Assets/" + env.sceneEditorCaptureFileName + ".asset");
                AssetDatabase.CreateAsset(model, path);
                EditorGUIUtility.PingObject(model);
            }
        }

        public override void DrawInspector () {
            env.sceneEditorCaptureSize = EditorGUILayout.Vector3IntField("Capture Size", env.sceneEditorCaptureSize);
            env.sceneEditorCaptureSize.x = Mathf.Max(1, Mathf.Min(32, env.sceneEditorCaptureSize.x));
            env.sceneEditorCaptureSize.y = Mathf.Max(1, Mathf.Min(32, env.sceneEditorCaptureSize.y));
            env.sceneEditorCaptureSize.z = Mathf.Max(1, Mathf.Min(32, env.sceneEditorCaptureSize.z));
            env.sceneEditorCaptureOffset = EditorGUILayout.Vector3IntField("Capture Offset", env.sceneEditorCaptureOffset);
            env.sceneEditorCaptureVoxelTintColor = EditorGUILayout.Toggle("Capture Tint Colors", env.sceneEditorCaptureVoxelTintColor);
            env.sceneEditorCaptureFileName = EditorGUILayout.TextField("Model Filename", env.sceneEditorCaptureFileName);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Remove Grid", "Destroy the current grid object"), EditorStyles.miniButton)) {
                DestroyGrid();
                VoxelPlayEnvironmentEditor.UnselectWorldEditorTool();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button(new GUIContent("Clear Contents", "Clears the contents of the grid"), EditorStyles.miniButton)) {
                ClearContents();
            }
            if (GUILayout.Button(new GUIContent("Capture Model", "Creates a model definition with the contents of the grid"), EditorStyles.miniButton)) {
                CaptureGrid();
            }
            EditorGUILayout.EndHorizontal();
            UpdateGrid();
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            voxelIndices.Clear();
            VoxelIndex vi = new VoxelIndex();
            vi.chunk = hitInfo.chunk;
            vi.voxelIndex = hitInfo.voxelIndex;
            voxelIndices.Add(vi);
        }

        public override void HighlightVoxels (ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {

            if (fixedPosition) return;
            if (voxelIndices.Count == 0) return;
            if (voxelIndices[0].chunk == null) return;

            if (selectionBox == null) {
                selectionBox = Resources.Load<GameObject>("VoxelPlay/Prefabs/Grid");
                if (selectionBox == null) return;
                selectionBox = Object.Instantiate(selectionBox);
                selectionBox.name = "Capture Grid";
                Renderer renderer = selectionBox.GetComponent<Renderer>();
                gridMat = renderer.sharedMaterial;
                gridMat = Object.Instantiate(gridMat);
                renderer.material = gridMat;
                gridMat.SetVector("_Color", new Color(0, 1f, 0, 0.5f));
            }

            gridPos = env.GetVoxelPosition(voxelIndices[0]);

            UpdateGrid();
        }

        void UpdateGrid () {
            if (selectionBox == null) return;

            Vector3d pos = gridPos;

            pos += new Vector3d(0.001, env.sceneEditorCaptureSize.y / 2 + 1.001, 0.001);
            pos += new Vector3d(env.sceneEditorCaptureOffset.x, env.sceneEditorCaptureOffset.y, env.sceneEditorCaptureOffset.z);
            if (env.sceneEditorCaptureSize.x % 2 == 0) { pos.x -= 0.5; }
            if (env.sceneEditorCaptureSize.y % 2 == 0) { pos.y -= 0.5; }
            if (env.sceneEditorCaptureSize.z % 2 == 0) { pos.z -= 0.5; }
            selectionBox.transform.position = pos;

            selectionBox.transform.localScale = new Vector3(env.sceneEditorCaptureSize.x, env.sceneEditorCaptureSize.y, env.sceneEditorCaptureSize.z);
            gridMat.SetVector("_Size", new Vector4(env.sceneEditorCaptureSize.x, env.sceneEditorCaptureSize.y, env.sceneEditorCaptureSize.z, 0));
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {
            fixedPosition = !fixedPosition;
            return true;
        }

    }

}