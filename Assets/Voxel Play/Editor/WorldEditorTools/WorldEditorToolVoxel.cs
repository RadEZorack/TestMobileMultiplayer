using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.NVIDIA;
using UnityEngine.Accessibility;

namespace VoxelPlay {

    public class WorldEditorToolVoxel : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolVoxel");
        public override string title => "Create a new voxel definition from the selected voxel";
        public override string instructions => "";
        public override int priority => 120;
        public override int minOpaque => 1;
        public override bool supportsContinuousMode => false;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;


        public override void DrawInspector () {
            env.sceneEditorCaptureVoxelDefinitionName = EditorGUILayout.TextField("Voxel Definition Filename", env.sceneEditorCaptureVoxelDefinitionName);
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            voxelIndices.Clear();
            VoxelIndex vi = new VoxelIndex();
            vi.chunk = hitInfo.chunk;
            vi.voxelIndex = hitInfo.voxelIndex;
            voxelIndices.Add(vi);
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            if (indices == null || indices.Count == 0) {
                return false;
            }

            VoxelIndex vi = indices[0];
            VoxelChunk chunk = vi.chunk;
            if (chunk == null) return false;

            int voxelIndex = vi.voxelIndex;

            VoxelDefinition sourceDefinition = chunk.voxels[voxelIndex].type;
            if (sourceDefinition == null) return false;

            // Defer the modal confirmation so it's not called inside the OnSceneGUI draw cycle.
            EditorApplication.delayCall += () => {
                if (EditorUtility.DisplayDialog("Create Voxel Definition", "Are you sure you want to create a voxel definition from the selected voxel?", "Yes", "No")) {

                    VoxelDefinition newVoxelDefinition = ScriptableObject.CreateInstance<VoxelDefinition>();
                    EditorUtility.CopySerialized(sourceDefinition, newVoxelDefinition);

                    if (newVoxelDefinition.supportsMicroVoxels && chunk.usesMicroVoxels && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv)) {
                        newVoxelDefinition.microVoxels = mv.Clone();
                    }

                    string path = AssetDatabase.GenerateUniqueAssetPath("Assets/" + env.sceneEditorCaptureVoxelDefinitionName + ".asset");
                    AssetDatabase.CreateAsset(newVoxelDefinition, path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    EditorGUIUtility.PingObject(newVoxelDefinition);
                }
            };

            return true;
        }

    }

}