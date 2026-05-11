using System;

using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [CustomEditor (typeof(TerrainDefaultGenerator), isFallback = true)]
	public class TerrainDefaultGeneratorEditor : Editor {

		[Serializable]
		class StepsWrapper {
			[SerializeField]
			public StepData[] steps;
		}

		public override void OnInspectorGUI () {
			serializedObject.UpdateIfRequiredOrScript();

			EditorGUILayout.Space(4);
			GUIContent openGraphContent = GetEditorIconContent("Open in Graph Editor",
				"d_GridLayoutGroup Icon", "GridLayoutGroup Icon",
				"d_ProfilerTimeline", "ProfilerTimeline",
				"d_SceneViewTools", "SceneViewTools");
			if (GUILayout.Button(openGraphContent, GUILayout.Height(28))) {
				TerrainGraphEditorWindow.Open((TerrainDefaultGenerator)target);
			}
			EditorGUILayout.Space(4);

			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((TerrainDefaultGenerator)target), typeof(MonoScript), false);
			}

			DrawPropertiesExcluding(serializedObject, "m_Script", "steps", "moisture", "moistureScale", "hint");

			serializedObject.ApplyModifiedProperties();
		}

		static GUIContent GetEditorIconContent(string text, params string[] iconNames) {
			for (int i = 0; i < iconNames.Length; i++) {
				if (string.IsNullOrEmpty(iconNames[i])) continue;
				GUIContent iconContent = EditorGUIUtility.IconContent(iconNames[i]);
				if (iconContent != null && iconContent.image != null) {
					return new GUIContent(text, iconContent.image);
				}
			}
			return new GUIContent(text);
		}

		[MenuItem ("CONTEXT/TerrainDefaultGenerator/Clear Steps")]
		static void ClearSteps (MenuCommand command) {
			try {
				if (EditorUtility.DisplayDialog("Clear Generation Steps", "Are you sure you want to remove all generator steps?", "Yes", "No")) {
					ITerrainDefaultGenerator thisTG = (ITerrainDefaultGenerator)command.context;
					thisTG.Steps = new StepData[0];
					EditorUtility.SetDirty (command.context);
				}
			} catch {
			}
		}

		[MenuItem ("CONTEXT/TerrainDefaultGenerator/Copy Steps")]
		static void CopySteps (MenuCommand command) {
			try {
				ITerrainDefaultGenerator tg = (ITerrainDefaultGenerator)command.context;
				StepsWrapper sw = new StepsWrapper ();
				sw.steps = tg.Steps;
				string text = JsonUtility.ToJson (sw);
				EditorGUIUtility.systemCopyBuffer = text;
			} catch {
			}
		}

		[MenuItem ("CONTEXT/TerrainDefaultGenerator/Paste Steps")]
		static void PasteSteps (MenuCommand command) {
			try {
				string text = EditorGUIUtility.systemCopyBuffer;
				StepsWrapper sw = JsonUtility.FromJson<StepsWrapper> (text);
				StepData[] refSteps = sw.steps;
				StepData[] newSteps = new StepData[refSteps.Length];
				for (int k = 0; k < refSteps.Length; k++) {
					newSteps [k] = refSteps [k];
				}
				ITerrainDefaultGenerator thisTG = (ITerrainDefaultGenerator)command.context;
				thisTG.Steps = newSteps;
				EditorUtility.SetDirty (command.context);
			} catch {

			}
		}

	}



}
