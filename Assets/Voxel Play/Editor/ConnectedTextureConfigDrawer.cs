using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoxelPlay
{
    [CustomPropertyDrawer (typeof (ConnectedTextureConfig))]
    public class ConnectedTextureConfigDrawer : PropertyDrawer
    {
        public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
        {

            float lineHeight = EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            float left = position.x;

            const float W = 60f;
            const float SW = 70f;

            float previewSize = lineHeight * 4f;
            Rect previewRect = new Rect(position.position, new Vector2(previewSize, previewSize));
            previewRect.x += SW * 3 - 5f;

            position.y += 6;

            Rect box = new Rect (position.x - 2f, position.y - 4f, SW * 4f + 12f + previewSize, lineHeight * 3f + 14f);
            GUI.Box (box, GUIContent.none);

            position.height = lineHeight;
            position.width = W;

            EditorGUI.BeginChangeCheck ();

            Rect prevPosition = position;

            SerializedProperty tl = property.FindPropertyRelative ("tl");
            tl.boolValue = Toggle (position, tl.boolValue);
            position.x += SW;
            SerializedProperty t = property.FindPropertyRelative ("t");
            t.boolValue = Toggle (position, t.boolValue);
            position.x += SW;
            SerializedProperty tr = property.FindPropertyRelative ("tr");
            tr.boolValue = Toggle (position, tr.boolValue);

            position = prevPosition;
            position.y += lineHeight + 2f;
            prevPosition = position;

            SerializedProperty l = property.FindPropertyRelative ("l");
            l.boolValue = Toggle (position, l.boolValue);
            position.x += SW;
            SerializedProperty texture = property.FindPropertyRelative ("texture");
            EditorGUI.ObjectField (position, texture, GUIContent.none);

            EditorGUI.LabelField(previewRect, new GUIContent( (Texture2D)texture.objectReferenceValue));

            position.x += SW;
            SerializedProperty r = property.FindPropertyRelative ("r");
            r.boolValue = Toggle (position, r.boolValue);

            position = prevPosition;
            position.y += lineHeight + 2f;

            SerializedProperty bl = property.FindPropertyRelative ("bl");
            bl.boolValue = Toggle (position, bl.boolValue);
            position.x += SW;
            SerializedProperty b = property.FindPropertyRelative ("b");
            b.boolValue = Toggle (position, b.boolValue);
            position.x += SW;
            SerializedProperty br = property.FindPropertyRelative ("br");
            br.boolValue = Toggle (position, br.boolValue);

            position.x += SW * 2 + 10f;
            if (GUI.Button (position, "Delete")) {
                if (EditorUtility.DisplayDialog ("", "Delete this entry?", "Yes", "No")) {
                    ConnectedTexture ct = (ConnectedTexture)property.serializedObject.targetObject;
                    List<ConnectedTextureConfig> od = new List<ConnectedTextureConfig> (ct.config);
                    int index = property.GetArrayIndex ();
                    od.RemoveAt (index);
                    ct.config = od.ToArray ();
                    GUI.changed = true;
                }
            }

            SerializedProperty normalMap = property.FindPropertyRelative("normalMap");
            position.x = left;
            position.y += lineHeight + 2f;
            position.width = 300;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150;
            EditorGUI.ObjectField(position, normalMap, new GUIContent("Optional Normal Map:"));
            EditorGUIUtility.labelWidth = labelWidth;

            if ((EditorGUI.EndChangeCheck () || GUI.enabled) && !Application.isPlaying) {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty (UnityEngine.SceneManagement.SceneManager.GetActiveScene ());
            }
        }

        bool Toggle (Rect position, bool value)
        {
            GUIStyle style = new GUIStyle ("Button");
            style.onActive = style.onNormal;
            style.onFocused = style.onNormal;
            style.onHover = style.onNormal;
            style.active = style.onNormal;
            
            value = EditorGUI.Toggle (position, GUIContent.none, value, style);
            EditorGUI.LabelField(position, value ? "■" : "", style);
            return value;
        }

        public override float GetPropertyHeight (SerializedProperty prop, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 5f + 5f;
        }

    }
}