using UnityEditor;

namespace VoxelPlay {
				
	[CustomEditor (typeof(VoxelPlayLight))]
	public class VoxelPlayLightEditor : Editor {

        SerializedProperty virtualLight, lightColor, lightRange, lightIntensity;

        private void OnEnable() {
            virtualLight = serializedObject.FindProperty("virtualLight");
            lightColor = serializedObject.FindProperty("lightColor");
            lightIntensity = serializedObject.FindProperty("lightIntensity");
            lightRange = serializedObject.FindProperty("lightRange");
        }

        public override void OnInspectorGUI () {
            serializedObject.Update();

            EditorGUILayout.PropertyField(virtualLight);
            if (virtualLight.boolValue) {
                EditorGUILayout.PropertyField(lightColor);
                EditorGUILayout.PropertyField(lightIntensity);
                EditorGUILayout.PropertyField(lightRange);
            }
            serializedObject.ApplyModifiedProperties();
        }

	}

}
