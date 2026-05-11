using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolMicroVoxels : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolMicroVoxels") ?? Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolVoxel");
        public override string title => "Create MicroVoxels Definition from the selected voxel";
        public override string instructions => "Click on a voxel with microvoxels to capture its shape.";
        public override int priority => 121;
        public override int minOpaque => 1;
        public override bool supportsContinuousMode => false;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;


        public override void DrawInspector() {
            env.sceneEditorMicroVoxelsDefinitionName = EditorGUILayout.TextField("MicroVoxels Filename", env.sceneEditorMicroVoxelsDefinitionName);
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Select a voxel that contains microvoxels data to capture its shape into a reusable asset.", MessageType.Info);
        }

        public override void SelectVoxels(ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            voxelIndices.Clear();
            VoxelIndex vi = new VoxelIndex();
            vi.chunk = hitInfo.chunk;
            vi.voxelIndex = hitInfo.voxelIndex;
            voxelIndices.Add(vi);
        }

        protected override bool Execute(ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            if (indices == null || indices.Count == 0) {
                return false;
            }

            VoxelIndex vi = indices[0];
            VoxelChunk chunk = vi.chunk;
            if (chunk == null) return false;

            int voxelIndex = vi.voxelIndex;

            // Check if the voxel has microvoxels
            MicroVoxels mv = null;
            bool hasMicroVoxels = false;

            // First check chunk-level microvoxels
            if (chunk.usesMicroVoxels && chunk.microVoxels != null && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels chunkMv)) {
                mv = chunkMv;
                hasMicroVoxels = true;
            }

            // Also check if the voxel definition has microvoxels
            VoxelDefinition vd = chunk.voxels[voxelIndex].type;
            if (!hasMicroVoxels && vd != null && vd.usesMicroVoxels) {
                mv = vd.microVoxels;
                hasMicroVoxels = true;
            }

            if (!hasMicroVoxels || mv == null || mv.isEmpty) {
                EditorApplication.delayCall += () => {
                    EditorUtility.DisplayDialog("No MicroVoxels", "The selected voxel does not contain microvoxels data.", "Ok");
                };
                return false;
            }

            // Defer the modal confirmation so it's not called inside the OnSceneGUI draw cycle.
            EditorApplication.delayCall += () => {
                if (EditorUtility.DisplayDialog("Create MicroVoxels Definition", 
                    "Create a MicroVoxels Definition asset from the selected voxel's microvoxels?", 
                    "Yes", "No")) {

                    MicroVoxelsDefinition newMicroVoxelsDef = ScriptableObject.CreateInstance<MicroVoxelsDefinition>();
                    newMicroVoxelsDef.microVoxels = mv.Clone();
                    newMicroVoxelsDef.microVoxels.isShared = true;

                    string path = AssetDatabase.GenerateUniqueAssetPath("Assets/" + env.sceneEditorMicroVoxelsDefinitionName + ".asset");
                    AssetDatabase.CreateAsset(newMicroVoxelsDef, path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    EditorGUIUtility.PingObject(newMicroVoxelsDef);
                    Selection.activeObject = newMicroVoxelsDef;
                }
            };

            return true;
        }

    }

}
