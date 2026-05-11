using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolModel : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolModel");
        public override string title => "Spawn a model definition";
        public override string instructions => "Press R to rotate.";
        public override int priority => 70;
        public override bool supportsContinuousMode => false;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.SculptTool;

        GameObject modelPreview;
        ModelDefinition highlightedModel;

        public override void DrawInspector () {
            env.sceneEditorModelDefinition = (ModelDefinition)EditorGUILayout.ObjectField("Model", env.sceneEditorModelDefinition, typeof(ModelDefinition), false);
            EditorGUILayout.BeginHorizontal();
            env.sceneEditorPlacementRotation = EditorGUILayout.IntSlider("Rotation", env.sceneEditorPlacementRotation, 0, 3);
            GUILayout.Label(env.sceneEditorPlacementRotation * 90 + "°");
            EditorGUILayout.EndHorizontal();

            env.sceneEditorModelAsGameObject = EditorGUILayout.Toggle("Create GameObject", env.sceneEditorModelAsGameObject);
            if (env.sceneEditorModelAsGameObject) {
                EditorGUILayout.BeginHorizontal();
                env.sceneEditorModelGameObjectScale = EditorGUILayout.Vector3Field("Scale", env.sceneEditorModelGameObjectScale);
                EditorGUILayout.EndHorizontal();
            } else {
                env.sceneEditorPlacementFitTerrain = EditorGUILayout.Toggle("Fit Terrain", env.sceneEditorPlacementFitTerrain);
            }
        }

        public override void ExitSceneView () {
            if (modelPreview != null) {
                modelPreview.transform.position = new Vector3(0, -10000, 0);
            }
        }

        public override void Dispose () {
            DestroyModel();
        }

        public override void SwitchTool () {
            DestroyModel();
        }

        void DestroyModel () {
            if (modelPreview != null) {
                Object.DestroyImmediate(modelPreview);
            }
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushRadius, List<VoxelIndex> voxelIndices) {

            if (highlightedModel != env.sceneEditorModelDefinition && modelPreview != null) {
                DestroyModel();
            }
            highlightedModel = env.sceneEditorModelDefinition;
            if (highlightedModel == null) return;

            voxelIndices.Clear();
            VoxelIndex vi = new VoxelIndex();
            vi.chunk = hitInfo.chunk;
            vi.voxelIndex = hitInfo.voxelIndex;
            voxelIndices.Add(vi);

        }

        public override void Update () {
            if (keyR) {
                env.sceneEditorPlacementRotation = (env.sceneEditorPlacementRotation + 1) % 4;
            }
        }

        public override void HighlightVoxels (ref VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices, Color color, float edgeWidth, float fadeAmplitude) {

            if (highlightedModel == null) return;

            if (voxelIndices.Count == 0) return;

            VoxelIndex vi = voxelIndices[0];
            if (vi.chunk == null) return;

            if (env.sceneEditorModelAsGameObject) {
                modelPreview = env.ModelHighlight(highlightedModel, hitInfo.point, env.sceneEditorPlacementRotation * 90);
                if (modelPreview == null) return;
                modelPreview.transform.localScale = env.sceneEditorModelGameObjectScale;
            } else {
                modelPreview = env.ModelHighlight(highlightedModel, env.GetVoxelPosition(vi) + Misc.vector3up, env.sceneEditorPlacementRotation * 90);
                if (modelPreview == null) return;
                modelPreview.transform.localScale = Misc.vector3one;
            }
        }

        // New method to validate model definitions outside of the repaint cycle
        private bool ValidateModelDefinition() {
            if (env.sceneEditorModelDefinition == null) return true;
            
                // Check for temporary voxel definitions
                foreach (var bit in env.sceneEditorModelDefinition.bits) {
                    if (env.IsVoxelDefinitionTemporary(bit.voxelDefinition)) {
                        bool addToWorld = EditorUtility.DisplayDialog(
                            "Voxel definition is not registered", 
                            "Some voxel definitions in this model (such as " + bit.voxelDefinition.name + 
                            ") are not stored within a folder under the world definition folder (it's recommended that you store the world definition and its dependencies inside a Resources folder).\n" +
                            "This can cause issues when loading the world from start.\n" +
                            "Would you like to add the missing voxel definitions to the World Definition More Voxels section?", 
                            "Ok", "Cancel");
                            
                        if (addToWorld) {
                            foreach (var nbit in env.sceneEditorModelDefinition.bits) {
                                if (env.IsVoxelDefinitionTemporary(nbit.voxelDefinition)) {
                                    env.world.AddVoxelDefinition(nbit.voxelDefinition);
                                }
                            }
                            return true;
                        } else {
                            return false;
                        }
                    }
            }
            return true;
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            if (env.sceneEditorModelDefinition == null) return false;

            List<VoxelIndex> modifiedIndices = BufferPool<VoxelIndex>.Get();
            int modifiedCount = 0;

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            try {

                if (env.sceneEditorModelAsGameObject) {
                    Vector3d pos = hitInfo.point;
                    GameObject obj = env.VoxelCreateGameObject(env.sceneEditorModelDefinition);
                    obj.transform.position = pos + env.sceneEditorModelDefinition.offset;
                    obj.transform.localScale = env.sceneEditorModelGameObjectScale;
                    obj.transform.localRotation = Quaternion.Euler(0, env.sceneEditorPlacementRotation * 90, 0);
                    Undo.RegisterCreatedObjectUndo(obj, "Create Voxel GameObject");
                } else {

                    foreach (var bit in env.sceneEditorModelDefinition.bits) {
                        if (env.IsVoxelDefinitionTemporary(bit.voxelDefinition)) {   
                            EditorApplication.delayCall += () => ValidateModelDefinition();            
                            return false;
                        }
                    }

                    Vector3d pos = env.GetVoxelPosition(hitInfo.center + Misc.vector3up * 0.5f);
                    env.ModelPlace(pos, env.sceneEditorModelDefinition, indices: modifiedIndices, previewMode: true, rotationDegrees: env.sceneEditorPlacementRotation * 90, fitTerrain: env.sceneEditorPlacementFitTerrain);

                    VoxelChunk modifiedChunk = null;
                    foreach (var mvi in modifiedIndices) {
                        VoxelChunk chunk = mvi.chunk;
                        if (modifiedChunk != chunk) {
                            modifiedChunk = chunk;
                            undoManager.SaveChunk(chunk);
                            modifiedChunks.Add(chunk);
                        }
                    }
                    env.ModelPlace(pos, env.sceneEditorModelDefinition, indices: modifiedIndices, previewMode: false, rotationDegrees: env.sceneEditorPlacementRotation * 90, fitTerrain: env.sceneEditorPlacementFitTerrain);

                    modifiedCount = modifiedChunks.Count;
                    RefreshModifiedChunks(modifiedChunks);
                }
            }
            finally {
                BufferPool<VoxelChunk>.Release(modifiedChunks);
                BufferPool<VoxelIndex>.Release(modifiedIndices);
            }

            return modifiedCount > 0;

        }

    }

}