using UnityEditor;
using UnityEngine;

namespace VoxelPlay {

    [CustomEditor(typeof(ConnectedVoxel))]
    public class ConnectedVoxelEditor : Editor {

        SerializedProperty voxelDefinition;
        SerializedProperty additionalVoxelDefinitions;
        SerializedProperty ruleEvent;
        SerializedProperty config;
        SerializedProperty ignoreVegetation;

        void OnEnable () {
            voxelDefinition = serializedObject.FindProperty("voxelDefinition");
            additionalVoxelDefinitions = serializedObject.FindProperty("additionalVoxelDefinitions");
            ruleEvent = serializedObject.FindProperty("ruleEvent");
            config = serializedObject.FindProperty("config");
            ignoreVegetation = serializedObject.FindProperty("ignoreVegetation");
        }

        public override void OnInspectorGUI () {
            serializedObject.Update();
            EditorGUILayout.PropertyField(voxelDefinition, new GUIContent("Placing Voxel", "These rules will be applied when placing this voxel in the world."));
            EditorGUILayout.PropertyField(additionalVoxelDefinitions, new GUIContent("Also apply to", "Also apply these rules to other voxel definitions."));
            EditorGUILayout.PropertyField(ruleEvent, new GUIContent("Event", "Choose if these rules are applied when placing a voxel or when rendering. If rules are applied when placing, the voxel will actually be changed in the chunk. However, if you select 'When Rendering', the contents of the chunk won't be modified, only the representation will change."));
            EditorGUILayout.PropertyField(ignoreVegetation, new GUIContent("Ignore Vegetation", "If true, vegetation will be ignored when checking if a neighbour is empty or not."));
            EditorGUILayout.HelpBox("Specify which adjacent prefabs are connected and which action and prefabs must be used in each case.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            if (GUILayout.Button("Expand All", GUILayout.Width(100))) {
                ToggleExpand(true);
            }
            if (GUILayout.Button("Collapse All", GUILayout.Width(100))) {
                ToggleExpand(false);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(config, new GUIContent("Configuration"), true);
            serializedObject.ApplyModifiedProperties();
        }

        void ToggleExpand (bool expanded) {
            ConnectedVoxel c = (ConnectedVoxel)target;
            if (c != null && c.config != null) {
                for (int k = 0; k < c.config.Length; k++) {
                    c.config[k].foldout = expanded;
                }
            }
            EditorUtility.SetDirty(config.objectReferenceValue);
        }
    }

}
