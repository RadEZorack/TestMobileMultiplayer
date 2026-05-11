using UnityEngine;
using VoxelPlay;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TestModel {

    public class DemoModel : MonoBehaviour {

        public ModelDefinition md;

        void Start() {
            GameObject go = VoxelPlayEnvironment.ModelCreateGameObject(md, offset: Vector3.zero, scale: Vector3.one * 0.2f);
            go.transform.position = Vector3.zero;
        }

#if UNITY_EDITOR
       [MenuItem("CONTEXT/DemoModel/Generate Voxel Prefab")]
        static void CreatePrefabOption(MenuCommand command) {
            DemoModel target = (DemoModel)command.context;
            GameObject go = VoxelPlayEnvironment.ModelCreateGameObject(target.md, offset: Vector3.zero, scale: Vector3.one * 0.2f);
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Tree.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            // store mesh, material and texture which are created by code into the prefab (SaveAsPrefabAsset won't do this)
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            prefab.GetComponent<MeshFilter>().sharedMesh = mesh;
            Material mat = go.GetComponent<Renderer>().sharedMaterial;
            prefab.GetComponent<Renderer>().sharedMaterial = mat;
            AssetDatabase.AddObjectToAsset(mesh, path);
            AssetDatabase.AddObjectToAsset(mat, path);
            AssetDatabase.AddObjectToAsset(mat.mainTexture, path);
            AssetDatabase.SaveAssets();
            DestroyImmediate(go);
            EditorUtility.DisplayDialog("Create Voxel GameObject", $"Object created at {path}", "Ok");
        }

#endif
    }
}

