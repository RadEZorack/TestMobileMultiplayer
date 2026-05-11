using System;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [Serializable]
    public class VoxelPlayConstructorTools : EditorWindow {

        bool savingAsNew;

        [MenuItem("Assets/Create/Voxel Play/Constructor Tools", false, 1000)]
        public static void ShowWindow() {
            VoxelPlayConstructorTools window = GetWindow<VoxelPlayConstructorTools>("Constructor", true);
            window.minSize = new Vector2(300, 450);
            window.Show();
        }

        void OnGUI() {
            VoxelPlayEnvironment env = VoxelPlayEnvironment.instance;
            if (env == null) {
                EditorGUILayout.HelpBox("Constructor tools require Voxel Play Environment in the scene.", MessageType.Info);
                return;
            }
            VoxelPlayFirstPersonController fps = VoxelPlayFirstPersonController.instance;
            if (fps == null) {
                EditorGUILayout.HelpBox("Constructor tools require Voxel Play First Person Controller in the scene.", MessageType.Info);
                return;
            }
            if (!Application.isPlaying) {
                EditorGUILayout.HelpBox("Constructor tools are only available during Play Mode.", MessageType.Info);
                return;
            }

            EditorGUILayout.Separator();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Toggle Constructor Mode", GUILayout.Width(250), GUILayout.Height(30))) {
                fps.ToggleConstructor();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!env.constructorMode) {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            fps.constructorSize = EditorGUILayout.Vector3Field("Constructor Size", fps.constructorSize);
            if (GUILayout.Button("Default Size")) {
                fps.constructorSize = new Vector3(15, 15, 15);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            fps.constructorModel = (ModelDefinition)EditorGUILayout.ObjectField("Model", fps.constructorModel, typeof(ModelDefinition), false);
            if (EditorGUI.EndChangeCheck()) {
                if (fps.constructorModel != null) {
                    fps.LoadModel(fps.constructorModel);
                }
            }
            EditorGUILayout.Separator();

            if (savingAsNew) {
                fps.constructorModelFilename = EditorGUILayout.TextField("New file name:", fps.constructorModelFilename);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save")) {
                    if (fps.SaveModel(true)) {
                        savingAsNew = false;
                    }
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Cancel")) {
                    savingAsNew = false;
                }
                EditorGUILayout.EndHorizontal();
            } else {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("New Model")) {
                    fps.NewModel();
                }
                GUI.enabled = fps.constructorModel != null;
                if (GUILayout.Button("Load")) {
                    fps.LoadModel(fps.constructorModel);
                }
                if (GUILayout.Button("Save")) {
                    fps.SaveModel(false);
                    GUIUtility.ExitGUI();
                }
                GUI.enabled = true;
                if (GUILayout.Button("Save As New...")) {
                    savingAsNew = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Separator();
                EditorGUILayout.Separator();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Displace", GUILayout.Width(150));
                if (GUILayout.Button("<X")) {
                    fps.DisplaceModel(-1, 0, 0);
                }

                if (GUILayout.Button("X>")) {
                    fps.DisplaceModel(1, 0, 0);
                }
                if (GUILayout.Button("<Y")) {
                    fps.DisplaceModel(0, -1, 0);
                }

                if (GUILayout.Button("Y>")) {
                    fps.DisplaceModel(0, 1, 0);
                }

                if (GUILayout.Button("<Z")) {
                    fps.DisplaceModel(0, 0, -1);
                }

                if (GUILayout.Button("Z>")) {
                    fps.DisplaceModel(0, 0, 1);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Resize Area", GUILayout.Width(150));
                if (GUILayout.Button("-X")) {
                    fps.ResizeModel(-1, 0, 0);
                }

                if (GUILayout.Button("+X")) {
                    fps.ResizeModel(1, 0, 0);
                }
                if (GUILayout.Button("-Y")) {
                    fps.ResizeModel(0, -1, 0);
                }

                if (GUILayout.Button("+Y")) {
                    fps.ResizeModel(0, 1, 0);
                }

                if (GUILayout.Button("-Z")) {
                    fps.ResizeModel(0, 0, -1);
                }

                if (GUILayout.Button("+Z")) {
                    fps.ResizeModel(0, 0, 1);
                }
                EditorGUILayout.EndHorizontal();

            }
        }

        void NewModel(VoxelPlayFirstPersonController fps) {
            if (!EditorUtility.DisplayDialog("New Model", "Discard any change and start a new model definition?", "Ok", "Cancel")) {
                return;
            }
            fps.NewModel();
        }


    }

}