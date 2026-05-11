using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [CustomEditor(typeof(VoxelMaterialProfile))]
    public class VoxelMaterialProfileEditor : Editor {

        MaterialFieldCache fields;
        SerializedProperty sourceRenderType;
        VoxelPlayEnvironment _env;

        void OnEnable() {
            fields = new MaterialFieldCache();
            fields.Init(serializedObject);
            sourceRenderType = serializedObject.FindProperty("sourceRenderType");
        }

        VoxelPlayEnvironment env {
            get {
                if (_env == null) {
                    _env = VoxelPlayEnvironment.instance;
                }
                return _env;
            }
        }

        public override void OnInspectorGUI() {
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUILayout.PropertyField(sourceRenderType, new GUIContent("Source Render Type", "The render type this profile was created for. Controls which texture slots are shown."));
            RenderType rt = (RenderType)sourceRenderType.intValue;

            if (!VoxelMaterialDrawer.IsProfileSupported(rt)) {
                EditorGUILayout.HelpBox("Material profiles are not supported for Custom/Invisible render types.", MessageType.Warning);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Separator();
            VoxelMaterialDrawer.DrawMaterialOverrideSection(fields, env, rt);
            VoxelMaterialDrawer.DrawTextureAndColorFields(fields, env, rt);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
