using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [CustomEditor(typeof(VoxelChunk))]
    public class VoxelChunkEditor : Editor {

        SerializedProperty debug, debugVoxelIndex;

        private void OnEnable() {
            debug = serializedObject.FindProperty("debug");
            debugVoxelIndex = serializedObject.FindProperty("debugVoxelIndex");
        }

        public override void OnInspectorGUI() {

            VoxelChunk chunk = (VoxelChunk)target;
            if ((object)chunk == null) return;

            serializedObject.Update();
            EditorGUILayout.PropertyField(debug);
            EditorGUILayout.PropertyField(debugVoxelIndex);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.LabelField("Pool Index", chunk.poolIndex.ToString());
            EditorGUILayout.LabelField("Position", chunk.position.ToString("F0"));
            EditorGUILayout.LabelField("Distance Status", chunk.visibleDistanceStatus.ToString());
            EditorGUILayout.LabelField("Render Status", chunk.renderState.ToString());
            EditorGUILayout.LabelField("   In Queue", chunk.inqueue.ToString());
            EditorGUILayout.LabelField("Above Surface", chunk.isAboveSurface.ToString());
            EditorGUILayout.LabelField("Populated", chunk.isPopulated.ToString());
            EditorGUILayout.LabelField("Is Dirty", chunk.isDirty.ToString()); ;
            EditorGUILayout.LabelField("Modified", chunk.modified.ToString());
            EditorGUILayout.LabelField("Allow Trees", chunk.allowTrees.ToString());
            EditorGUILayout.ObjectField(new GUIContent("Top chunk"), chunk.top, typeof(VoxelChunk), true);
            EditorGUILayout.ObjectField(new GUIContent("Bottom chunk"), chunk.bottom, typeof(VoxelChunk), true);
            EditorGUILayout.ObjectField(new GUIContent("Left chunk"), chunk.left, typeof(VoxelChunk), true);
            EditorGUILayout.ObjectField(new GUIContent("Right chunk"), chunk.right, typeof(VoxelChunk), true);
            EditorGUILayout.ObjectField(new GUIContent("Back chunk"), chunk.back, typeof(VoxelChunk), true);
            EditorGUILayout.ObjectField(new GUIContent("Forward chunk"), chunk.forward, typeof(VoxelChunk), true);
        }

    }

}
