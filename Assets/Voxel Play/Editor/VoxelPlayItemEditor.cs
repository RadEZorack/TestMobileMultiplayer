using UnityEngine;
using UnityEditor;
using System;
using System.Collections;

namespace VoxelPlay {

    [CustomEditor(typeof(Item), isFallback = true)]
    public class VoxelPlayItemEditor : UnityEditor.Editor {

        SerializedProperty itemDefinition, quantity;

        public virtual void OnEnable() {
            itemDefinition = serializedObject.FindProperty("itemDefinition");
            quantity = serializedObject.FindProperty("quantity");
        }
        public override void OnInspectorGUI() {

            Item item = (Item)target;
            if (item == null) return;

            serializedObject.Update();
            EditorGUILayout.PropertyField(itemDefinition);
            EditorGUILayout.PropertyField(quantity);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Other properties (set at runtime)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("World Position", item.transform.position.ToString());
            EditorGUILayout.LabelField("Creation Time", item.creationTime.ToString());
            EditorGUILayout.LabelField("AutoRotate", item.autoRotate.ToString());
            EditorGUILayout.LabelField("Can Be Destroyed", item.canBeDestroyed.ToString());
            EditorGUILayout.LabelField("Can Pick On Approach", item.canPickOnApproach.ToString());
            EditorGUILayout.LabelField("Is Persistent", item.persistentItem.ToString());
            if (item.itemVoxelIndex >= 0 && item.itemChunk != null) {
                EditorGUILayout.LabelField("Chunk Position", item.itemChunk.position.ToString());
                EditorGUILayout.LabelField("Voxel Index", item.itemVoxelIndex.ToString());
            }
            serializedObject.ApplyModifiedProperties();
        }

    }

}
