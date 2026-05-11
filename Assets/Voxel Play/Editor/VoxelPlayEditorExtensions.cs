using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

	public class VoxelPlayEditorExtensions {

		[MenuItem("GameObject/Voxel Play/Create Voxel Play Environment", false, priority = 100)]
		static void CreateVoxelPlayMenuOption (MenuCommand menuCommand) {
			CreateVPE(false);
		}

		[MenuItem("GameObject/Voxel Play/Create Voxel Play Environment (with Default UI and Input Controller assets)", false, priority = 100)]
		static void CreateVoxelPlayMenuWithUIControllersOption (MenuCommand menuCommand) {
			CreateVPE(true);
		}

		static void CreateVPE (bool useDefaultAssets) {
			// Create a custom game object
			if (Misc.FindObjectOfType<VoxelPlayEnvironment>() != null) {
				EditorUtility.DisplayDialog("Voxel Play Environment already created!", "Voxel Play Environment script has been found in the scene. Only one can per scene can be created.", "Ok");
				return;
			}
			GameObject go = new GameObject("Voxel Play Environment");
			Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
			go.transform.position = Misc.vector3zero;
			go.transform.localRotation = Quaternion.Euler(0, 0, 0);
			go.transform.localScale = new Vector3(1f, 1f, 1f);
			Selection.activeObject = go;
			VoxelPlayEnvironment env = go.AddComponent<VoxelPlayEnvironment>();
			if (useDefaultAssets) {
				env.AssignDefaultAssets();
			}
		}

		[MenuItem("GameObject/Voxel Play/Create First Person Controller", false, priority = 100)]
		static void CreateFPSController (MenuCommand menuCommand) {
			// Create a custom game object
			VoxelPlayEnvironment env = Misc.FindObjectOfType<VoxelPlayEnvironment>();
			if (env == null) {
				EditorUtility.DisplayDialog("Voxel Play Environment not found!", "Voxel Play Environment must be created first..", "Ok");
				return;
			}
			// Disable other cameras
			Camera[] cams = Misc.FindObjectsOfType<Camera>();
			for (int k = 0; k < cams.Length; k++) {
				if (cams[k] == Camera.main) cams[k].tag = "Untagged";
				if (cams[k].gameObject.activeInHierarchy) cams[k].gameObject.SetActive(false);
			}
			GameObject go = Object.Instantiate<GameObject>(Resources.Load<GameObject>("VoxelPlay/Prefabs/FPSController"));
			go.name = "Voxel Play FPS Controller";
			Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
			go.transform.localRotation = Quaternion.Euler(0, 0, 0);
			go.transform.localScale = new Vector3(1f, 1f, 1f);
			Selection.activeObject = go;
			env.AssignDefaultAssets();
		}


		[MenuItem("GameObject/Voxel Play/Create Third Person Controller", false, priority = 100)]
		static void CreateTPController (MenuCommand menuCommand) {
			// Create a custom game object
			VoxelPlayEnvironment env = Misc.FindObjectOfType<VoxelPlayEnvironment>();
			if (env == null) {
				EditorUtility.DisplayDialog("Voxel Play Environment not found!", "Voxel Play Environment must be created first..", "Ok");
				return;
			}
			GameObject go = Object.Instantiate<GameObject>(Resources.Load<GameObject>("VoxelPlay/Prefabs/ThirdPersonController"));
			go.name = "Voxel Play Third Person Controller";
			Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
			go.transform.localRotation = Quaternion.Euler(0, 0, 0);
			Selection.activeObject = go;
			env.AssignDefaultAssets();
		}

	}
}