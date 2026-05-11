using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoxelPlay {
    
    [CustomPropertyDrawer(typeof(ConnectedVoxelConfig))]
    public class ConnectedVoxelConfigDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

            SerializedProperty fo = property.FindPropertyRelative("foldout");

            float lineHeight = EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;

            const float W = 80f;
            const float SW = 80f;

            position.height = lineHeight;

            fo.boolValue = EditorGUI.Foldout(position, fo.boolValue, "");

            position.x += 5;
            position.width -= 5;

            if (fo.boolValue) {

                position.y += 6;

                float previewHeight = GetPropertyHeight(property, label);

                float editorWidth = position.width;
                Rect box = new Rect(position.x - 2f, position.y, editorWidth, previewHeight);
                GUI.Box(box, GUIContent.none);

                position.y += 4;
                position.width = W;

                EditorGUI.BeginChangeCheck();

                // top slice
                GUI.Label(position, "Top Slice:");
                position.x += 90;
                Rect prevPosition = position;

                SerializedProperty tl2 = property.FindPropertyRelative("tl2");
                EditorGUI.PropertyField(position, tl2, GUIContent.none);
                position.x += SW;
                SerializedProperty t2 = property.FindPropertyRelative("t2");
                EditorGUI.PropertyField(position, t2, GUIContent.none);
                position.x += SW;
                SerializedProperty tr2 = property.FindPropertyRelative("tr2");
                EditorGUI.PropertyField(position, tr2, GUIContent.none);

                position.x = editorWidth - position.width;
                if (GUI.Button(position, "Delete")) {
                    if (EditorUtility.DisplayDialog("", "Delete this entry?", "Yes", "No")) {
                        ConnectedTexture ct = (ConnectedTexture)property.serializedObject.targetObject;
                        List<ConnectedTextureConfig> od = new List<ConnectedTextureConfig>(ct.config);
                        int index = property.GetArrayIndex();
                        od.RemoveAt(index);
                        ct.config = od.ToArray();
                        GUI.changed = true;
                    }
                }


                position = prevPosition;
                position.y += lineHeight + 2f;
                prevPosition = position;

                SerializedProperty l2 = property.FindPropertyRelative("l2");
                EditorGUI.PropertyField(position, l2, GUIContent.none);
                position.x += SW;
                SerializedProperty tc = property.FindPropertyRelative("tc");
                EditorGUI.PropertyField(position, tc, GUIContent.none);
                position.x += SW;
                SerializedProperty r2 = property.FindPropertyRelative("r2");
                EditorGUI.PropertyField(position, r2, GUIContent.none);

                position = prevPosition;
                position.y += lineHeight + 2f;
                prevPosition = position;

                SerializedProperty bl2 = property.FindPropertyRelative("bl2");
                EditorGUI.PropertyField(position, bl2, GUIContent.none);
                position.x += SW;
                SerializedProperty b2 = property.FindPropertyRelative("b2");
                EditorGUI.PropertyField(position, b2, GUIContent.none);
                position.x += SW;
                SerializedProperty br2 = property.FindPropertyRelative("br2");
                EditorGUI.PropertyField(position, br2, GUIContent.none);

                // middle slice
                position = prevPosition;
                position.x -= 90;
                position.y += lineHeight + 10f;

                GUI.Label(position, "Middle Slice:");
                position.x += 90;
                prevPosition = position;

                SerializedProperty tl = property.FindPropertyRelative("tl");
                EditorGUI.PropertyField(position, tl, GUIContent.none);
                position.x += SW;
                SerializedProperty t = property.FindPropertyRelative("t");
                EditorGUI.PropertyField(position, t, GUIContent.none);
                position.x += SW;
                SerializedProperty tr = property.FindPropertyRelative("tr");
                EditorGUI.PropertyField(position, tr, GUIContent.none);

                position = prevPosition;
                position.y += lineHeight + 2f;
                prevPosition = position;

                SerializedProperty l = property.FindPropertyRelative("l");
                EditorGUI.PropertyField(position, l, GUIContent.none);
                position.x += SW;

                GUI.Label(position, "     (Center)");

                position.x += SW;
                SerializedProperty r = property.FindPropertyRelative("r");
                EditorGUI.PropertyField(position, r, GUIContent.none);

                position = prevPosition;
                position.y += lineHeight + 2f;
                prevPosition = position;

                SerializedProperty bl = property.FindPropertyRelative("bl");
                EditorGUI.PropertyField(position, bl, GUIContent.none);
                position.x += SW;
                SerializedProperty b = property.FindPropertyRelative("b");
                EditorGUI.PropertyField(position, b, GUIContent.none);
                position.x += SW;
                SerializedProperty br = property.FindPropertyRelative("br");
                EditorGUI.PropertyField(position, br, GUIContent.none);

                // bottom slice
                position = prevPosition;
                position.x -= 90;
                position.y += lineHeight + 10f;
                GUI.Label(position, "Bottom Slice:");
                position.x += 90;
                prevPosition = position;

                SerializedProperty tl0 = property.FindPropertyRelative("tl0");
                EditorGUI.PropertyField(position, tl0, GUIContent.none);
                position.x += SW;
                SerializedProperty t0 = property.FindPropertyRelative("t0");
                EditorGUI.PropertyField(position, t0, GUIContent.none);
                position.x += SW;
                SerializedProperty tr0 = property.FindPropertyRelative("tr0");
                EditorGUI.PropertyField(position, tr0, GUIContent.none);

                position = prevPosition;
                position.y += lineHeight + 2f;
                prevPosition = position;

                SerializedProperty l0 = property.FindPropertyRelative("l0");
                EditorGUI.PropertyField(position, l0, GUIContent.none);
                position.x += SW;

                SerializedProperty bc = property.FindPropertyRelative("bc");
                EditorGUI.PropertyField(position, bc, GUIContent.none);

                position.x += SW;
                SerializedProperty r0 = property.FindPropertyRelative("r0");
                EditorGUI.PropertyField(position, r0, GUIContent.none);

                position = prevPosition;
                position.y += lineHeight + 2f;
                prevPosition = position;

                SerializedProperty bl0 = property.FindPropertyRelative("bl0");
                EditorGUI.PropertyField(position, bl0, GUIContent.none);
                position.x += SW;
                SerializedProperty b0 = property.FindPropertyRelative("b0");
                EditorGUI.PropertyField(position, b0, GUIContent.none);
                position.x += SW;
                SerializedProperty br0 = property.FindPropertyRelative("br0");
                EditorGUI.PropertyField(position, br0, GUIContent.none);

                // actions
                position = prevPosition;
                position.x -= 90;
                position.y += lineHeight + 10f;
                EditorGUIUtility.labelWidth = 90;
                position.width = 350;

                SerializedProperty action = property.FindPropertyRelative("action");
                EditorGUI.PropertyField(position, action, new GUIContent("Action"));
                position.y += lineHeight + 2f;

                switch (action.intValue) {
                    case (int)ConnectedVoxelConfigAction.Replace:
                        SerializedProperty replacementModel = property.FindPropertyRelative("replacementVoxelDefinition");
                        EditorGUI.PropertyField(position, replacementModel, new GUIContent("Replace With"));
                        break;
                    case (int)ConnectedVoxelConfigAction.Cycle:
                    case (int)ConnectedVoxelConfigAction.Random:
                        SerializedProperty replacementModelSet = property.FindPropertyRelative("replacementVoxelDefinitionSet");
                        EditorGUI.PropertyField(position, replacementModelSet, new GUIContent("Replace With"), true);
                        break;
                }

                if ((EditorGUI.EndChangeCheck() || GUI.enabled) && !Application.isPlaying) {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label) {
            SerializedProperty foldout = prop.FindPropertyRelative("foldout");
            if (!foldout.boolValue) return EditorGUIUtility.singleLineHeight;

            int lines = 16;
            SerializedProperty action = prop.FindPropertyRelative("action");
            if (action.intValue == (int)ConnectedVoxelConfigAction.Replace) {
                lines++;
            } else if (action.intValue == (int)ConnectedVoxelConfigAction.Cycle || action.intValue == (int)ConnectedVoxelConfigAction.Random) {
                SerializedProperty reps = prop.FindPropertyRelative("replacementVoxelDefinitionSet");
                lines += reps.arraySize + 2;
            }
            return EditorGUIUtility.singleLineHeight * lines;
        }

    }
}