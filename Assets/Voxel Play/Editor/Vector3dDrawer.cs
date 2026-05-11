using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [CustomPropertyDrawer(typeof(Vector3d))]
    public class Vector3dDrawer : PropertyDrawer {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, label);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float spacing = 4f;
            float labelW = 12f;
            float fieldW = (position.width - (labelW + spacing) * 3) / 3 + labelW;
            float totalW = labelW + fieldW + spacing;

            Rect xLabelRect = new Rect(position.x, position.y, labelW, position.height);
            Rect xRect = new Rect(xLabelRect.xMax, position.y, fieldW, position.height);
            Rect yLabelRect = new Rect(xRect.xMax + spacing, position.y, labelW, position.height);
            Rect yRect = new Rect(yLabelRect.xMax, position.y, fieldW, position.height);
            Rect zLabelRect = new Rect(yRect.xMax + spacing, position.y, labelW, position.height);
            Rect zRect = new Rect(zLabelRect.xMax, position.y, fieldW, position.height);

            SerializedProperty xProp = property.FindPropertyRelative("x");
            SerializedProperty yProp = property.FindPropertyRelative("y");
            SerializedProperty zProp = property.FindPropertyRelative("z");

            EditorGUI.LabelField(xLabelRect, "X");
            xProp.doubleValue = EditorGUI.DoubleField(xRect, xProp.doubleValue);
            EditorGUI.LabelField(yLabelRect, "Y");
            yProp.doubleValue = EditorGUI.DoubleField(yRect, yProp.doubleValue);
            EditorGUI.LabelField(zLabelRect, "Z");
            zProp.doubleValue = EditorGUI.DoubleField(zRect, zProp.doubleValue);

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
