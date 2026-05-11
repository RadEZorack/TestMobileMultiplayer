using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [CustomEditor(typeof(VoxelPlayFirstPersonController), isFallback = true)]
    public class FirstPersonControllerEditor : UnityEditor.Editor {

        SerializedProperty useThirdPartyController, startOnFlat, startOnFlatIterations, _characterHeight, unstuck;
        SerializedProperty enableCrosshair, crosshairMaxDistance, crosshairScale, targetAnimationScale, targetAnimationSpeed, crosshairNormalColor, crosshairOnTargetColor, crosshairHitLayerMask, changeOnBlock, autoInvertColors;
        SerializedProperty voxelHighlight, voxelHighlightColor, voxelHighlightEdge;
        SerializedProperty loadModel, constructorSize;

        VoxelPlayFirstPersonController fps;
        VoxelPlayEnvironment env;

       public virtual void OnEnable() {
            useThirdPartyController = serializedObject.FindProperty("useThirdPartyController");
            startOnFlat = serializedObject.FindProperty("startOnFlat");
            startOnFlatIterations = serializedObject.FindProperty("startOnFlatIterations");
            _characterHeight = serializedObject.FindProperty("_characterHeight");
            unstuck = serializedObject.FindProperty("unstuck");

            enableCrosshair = serializedObject.FindProperty("enableCrosshair");
            crosshairMaxDistance = serializedObject.FindProperty("crosshairMaxDistance");
            crosshairHitLayerMask = serializedObject.FindProperty("crosshairHitLayerMask");
            crosshairScale = serializedObject.FindProperty("crosshairScale");
            targetAnimationScale = serializedObject.FindProperty("targetAnimationScale");
            targetAnimationSpeed = serializedObject.FindProperty("targetAnimationSpeed");
            crosshairNormalColor = serializedObject.FindProperty("crosshairNormalColor");
            crosshairOnTargetColor = serializedObject.FindProperty("crosshairOnTargetColor");
            changeOnBlock = serializedObject.FindProperty("changeOnBlock");
            autoInvertColors = serializedObject.FindProperty("autoInvertColors");

            voxelHighlight = serializedObject.FindProperty("voxelHighlight");
            voxelHighlightColor = serializedObject.FindProperty("voxelHighlightColor");
            voxelHighlightEdge = serializedObject.FindProperty("voxelHighlightEdge");

            loadModel = serializedObject.FindProperty("loadModel");
            constructorSize = serializedObject.FindProperty("constructorSize");

            fps = (VoxelPlayFirstPersonController)target;
            env = VoxelPlayEnvironment.instance;
        }


        public override void OnInspectorGUI() {

            if (env != null && env.constructorMode) {
                DrawBuildModeOptions();
                return;
            }

            EditorGUILayout.Separator();

            serializedObject.Update();
            EditorGUILayout.PropertyField(useThirdPartyController);
            EditorGUILayout.HelpBox("Enable this checkbox to allow other controllers to take control over the camera and character movement.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();

            if (fps.CheckCharacterController()) {
                DrawDefaultInspector();
                return;
            }

            EditorGUILayout.PropertyField(startOnFlat);
            if (startOnFlat.boolValue) {
                EditorGUILayout.PropertyField(startOnFlatIterations);
            }

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Basic Behaviour", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_characterHeight);
            EditorGUILayout.PropertyField(unstuck);

            EditorGUILayout.PropertyField(enableCrosshair);
            if (enableCrosshair.boolValue) {
                EditorGUILayout.PropertyField(crosshairHitLayerMask);
                EditorGUILayout.PropertyField(crosshairMaxDistance);
                EditorGUILayout.PropertyField(crosshairScale);
                EditorGUILayout.PropertyField(targetAnimationScale);
                EditorGUILayout.PropertyField(targetAnimationSpeed);
                EditorGUILayout.PropertyField(crosshairNormalColor);
                EditorGUILayout.PropertyField(crosshairOnTargetColor);
                EditorGUILayout.PropertyField(changeOnBlock);
                EditorGUILayout.PropertyField(autoInvertColors);
            }

            EditorGUILayout.PropertyField(voxelHighlight, new GUIContent("Enable Voxel Highlight"));
            if (voxelHighlight.boolValue) {
                EditorGUILayout.PropertyField(voxelHighlightColor);
                EditorGUILayout.PropertyField(voxelHighlightEdge);
            }

            if (serializedObject.ApplyModifiedProperties()) {
                fps.ResetCrosshairPosition();
            }
        }


        public void DrawBuildModeOptions() {

            serializedObject.Update();

            EditorGUILayout.PropertyField(loadModel, new GUIContent("Model"));
            EditorGUILayout.PropertyField(constructorSize, new GUIContent("Constructor Size", "Default constructor size."));

            serializedObject.ApplyModifiedProperties();
        }




    }

}
