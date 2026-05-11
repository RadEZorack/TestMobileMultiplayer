using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

	[Serializable]
	public class VoxelPlayCubeTools : UnityEditor.EditorWindow {

		public enum CubeVertexOffsetOption {
			Custom = 0,
			DoorOpensToTheRight = 10,
			DoorOpensToTheLeft = 11
		}

		public string cubeName = "Cube";

		public int textureAtlasSize = 2048;

		public CubeSideSettings[] sides;

		public CubeShadingStyle cubeShadingStyle = CubeShadingStyle.Color;

		public Vector3 scale = Misc.vector3one;

		public Vector3 offset;

		public CubeVertexOffsetOption offsetOption = CubeVertexOffsetOption.Custom;

		public Texture2D icon;

		[MenuItem("Assets/Create/Voxel Play/Cube-Door Creator", false, 1000)]
		public static void ShowWindow () {
			VoxelPlayCubeTools window = GetWindow<VoxelPlayCubeTools>("Cube/Door Creator", true);
			window.minSize = new Vector2(300, 450);
			window.Show();
		}

		void OnEnable () {
			if (sides == null || sides.Length < 6) {
				sides = new CubeSideSettings[6];
				for (int k = 0; k < sides.Length; k++) {
					sides[k].color = Misc.colorWhite;
				}
			}
		}


		void OnGUI () {
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.HelpBox("Create custom cube models.", MessageType.Info);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Separator();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Name", GUILayout.Width(120));
			cubeName = EditorGUILayout.TextField(cubeName);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Shading Style", GUILayout.Width(120));
			cubeShadingStyle = (CubeShadingStyle)EditorGUILayout.EnumPopup(cubeShadingStyle);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Vertex Scale", GUILayout.Width(120));
			scale = EditorGUILayout.Vector3Field("", scale);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(new GUIContent("Offset Option", "Vertex offset option"), GUILayout.Width(120));
			offsetOption = (CubeVertexOffsetOption)EditorGUILayout.EnumPopup(offsetOption);
			EditorGUILayout.EndHorizontal();

			switch (offsetOption) {
				case CubeVertexOffsetOption.DoorOpensToTheLeft:
					offset = new Vector3(0.5f - scale.z * 0.5f, scale.y * 0.5f, 0);
					break;
				case CubeVertexOffsetOption.DoorOpensToTheRight:
					offset = new Vector3(-0.5f + scale.z * 0.5f, scale.y * 0.5f, 0);
					break;
			}

			GUI.enabled = (offsetOption == CubeVertexOffsetOption.Custom);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(new GUIContent("   Custom Offset", "Applied after scale"), GUILayout.Width(120));
			offset = EditorGUILayout.Vector3Field("", offset);
			EditorGUILayout.EndHorizontal();
			GUI.enabled = true;

			EditorGUILayout.Separator();

			if (offsetOption != CubeVertexOffsetOption.Custom) {
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Icon", GUILayout.Width(120));
				icon = (Texture2D)EditorGUILayout.ObjectField(icon, typeof(Texture2D), false);
				EditorGUILayout.EndHorizontal();
			}

			if (cubeShadingStyle != CubeShadingStyle.Color) {
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Texture Atlas Size", GUILayout.Width(120));
				textureAtlasSize = EditorGUILayout.IntField(textureAtlasSize);
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.Separator();

			for (int k = 0; k < sides.Length; k++) {
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(CubeTools.SideNames[k] + " side", GUILayout.Width(120));
				EditorGUILayout.EndHorizontal();

				if (cubeShadingStyle != CubeShadingStyle.Color) {
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField("   Texture", GUILayout.Width(120));
					sides[k].texture = (Texture2D)EditorGUILayout.ObjectField(sides[k].texture, typeof(Texture2D), false);
					EditorGUILayout.EndHorizontal();
				}

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("   Color", GUILayout.Width(120));
				sides[k].color = EditorGUILayout.ColorField(sides[k].color);
				EditorGUILayout.EndHorizontal();

			}

			EditorGUILayout.Separator();
			EditorGUILayout.BeginHorizontal();
			if (offsetOption != CubeVertexOffsetOption.Custom) {
				if (GUILayout.Button("Generate Door Prefab & Voxel Definition", GUILayout.Width(300))) {
					VoxelDefinition vd = GenerateDoorVoxel();
					if (vd != null) {
						EditorUtility.FocusProjectWindow();
						Selection.activeObject = vd;
					}
					GUIUtility.ExitGUI();
				}
			} else {
				if (GUILayout.Button("Generate Cube Prefab", GUILayout.Width(180))) {
					GameObject prefab = GenerateCubePrefab();
					if (prefab != null) {
						EditorUtility.FocusProjectWindow();
						Selection.activeObject = prefab;
					}
					GUIUtility.ExitGUI();
				}
			}
			EditorGUILayout.EndHorizontal();
		}

		GameObject GenerateCubePrefab () {
			return GenerateCubePrefabInternal();
		}

		VoxelDefinition GenerateDoorVoxel () {

			GameObject doorPrefab = GenerateCubePrefab();
			if (doorPrefab == null) {
				return null;
			}
			Door door = doorPrefab.AddComponent<Door>();
			if (offsetOption == CubeVertexOffsetOption.DoorOpensToTheLeft) {
				door.customTag = "left";
			}
			VoxelDefinition doorVoxel = CreateInstance<VoxelDefinition>();
			doorVoxel.renderType = RenderType.Custom;
			doorVoxel.model = doorPrefab;
			doorVoxel.name = "Voxel" + doorPrefab.name;
			doorVoxel.icon = icon;

			if (offsetOption == CubeVertexOffsetOption.DoorOpensToTheRight) {
				doorVoxel.offset = new Vector3(scale.x * 0.5f - scale.z * 0.5f, -0.5f, 0);
			} else {
				doorVoxel.offset = new Vector3(scale.x * -0.5f + scale.z * 0.5f, -0.5f, 0);
			}

			string path = AssetDatabase.GetAssetPath(doorPrefab);
			if (path != null) {
				path = Path.GetDirectoryName(path);
			}
			AssetDatabase.CreateAsset(doorVoxel, path + "/" + doorPrefab.name + ".asset");
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			return doorVoxel;
		}

		string GetPathForNewCube () {
			string path = null;

			// Check any texture to determine path
			for (int k = 0; k < sides.Length; k++) {
				if (sides[k].texture != null) {
					path = AssetDatabase.GetAssetPath(sides[k].texture);
					if (path != null) {
						path = Path.GetDirectoryName(path);
						break;
					}
				}
			}

			if (path == null) {
				if (VoxelPlayEnvironment.instance != null) {
					path = AssetDatabase.GetAssetPath(VoxelPlayEnvironment.instance.world);
					path = Path.GetDirectoryName(path) + "/Models";
				} else {
					path = "Assets/ImportedModels";
					Directory.CreateDirectory(path);
				}
			}

			return path;
		}

		Material GetMaterialForShadingStyle (CubeShadingStyle shadingStyle) {
			return CubeTools.GetMaterialForShadingStyle(shadingStyle);
		}

		Texture2D PackTextures () {
			return CubeTools.PackTextures(sides, textureAtlasSize);
		}

		Mesh GetMesh () {
			return CubeTools.GenerateCubeMesh(sides, scale, offset, cubeShadingStyle);
		}

		GameObject GenerateCubePrefabInternal () {
			string path = GetPathForNewCube();
			string prefabPath = path + "/" + cubeName + ".prefab";

			if (File.Exists(prefabPath)) {
				UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath(prefabPath, typeof(UnityEngine.Object));
				if (existing != null) {
					EditorUtility.DisplayDialog("Error saving prefab", "A prefab with same name already exists on destination folder - choose another name.", "Ok");
					return null;
				}
			}

			// Create material
			Material mat = GetMaterialForShadingStyle(cubeShadingStyle);

			// Pack textures if needed
			Texture2D packedTexture = null;
			if (cubeShadingStyle != CubeShadingStyle.Color) {
				packedTexture = PackTextures();
				if (packedTexture != null) {
					mat = Instantiate(mat);
					mat.mainTexture = packedTexture;
				}
			}

			// Create GameObject with mesh
			GameObject obj = new GameObject("Cube", typeof(MeshRenderer));
			MeshFilter mf = obj.AddComponent<MeshFilter>();
			Mesh mesh = GetMesh();
			mf.mesh = mesh;

			// Save as prefab
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);

			// Store assets inside the prefab
			if (cubeShadingStyle != CubeShadingStyle.Color && packedTexture != null) {
				AssetDatabase.AddObjectToAsset(packedTexture, prefab);
				AssetDatabase.AddObjectToAsset(mat, prefab);
			}
			AssetDatabase.AddObjectToAsset(mesh, prefab);

			// Update prefab components
			prefab.GetComponent<MeshFilter>().sharedMesh = mesh;
			prefab.GetComponent<MeshRenderer>().sharedMaterial = mat;
			MeshCollider mc = prefab.AddComponent<MeshCollider>();
			mc.sharedMesh = mesh;
			mc.convex = true;

			AssetDatabase.SaveAssets();
			DestroyImmediate(obj);

			return prefab;
		}

	}

}