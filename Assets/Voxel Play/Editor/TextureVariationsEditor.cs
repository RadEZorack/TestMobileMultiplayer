using UnityEngine;
using UnityEditor;

namespace VoxelPlay
{

    [CustomEditor (typeof (TextureVariations))]
    public class TextureVariationsEditor : Editor {

        SerializedProperty voxelDefinition;
        SerializedProperty config;
        SerializedProperty side;

        void OnEnable ()
        {
            voxelDefinition = serializedObject.FindProperty ("voxelDefinition");
            side = serializedObject.FindProperty ("side");
            config = serializedObject.FindProperty("config");
        }

        public override void OnInspectorGUI ()
        {
            serializedObject.Update ();
            EditorGUILayout.PropertyField (voxelDefinition);
            EditorGUILayout.PropertyField (side);
            EditorGUILayout.HelpBox ("Specify a list of textures with custom probability.", MessageType.Info);
            EditorGUILayout.PropertyField (config, new GUIContent ("Configuration"), true);
            serializedObject.ApplyModifiedProperties ();
        }



    }

}
