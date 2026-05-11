using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoxelPlay {
	[CustomPropertyDrawer(typeof(StepData))]
	public class TerrainStepDataDrawer : PropertyDrawer {

		// Draw the property inside the given rect
		public override void OnGUI (Rect position, SerializedProperty property, GUIContent label) {

			position.height -= 5f;
			Rect box = new Rect(position.x - 2f, position.y - 2f, position.width + 4f, position.height + 4f);
			EditorGUI.DrawRect(box, new Color(0, 0, 0.175f, 0.15f));

			float lineHeight = EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
			position.height = EditorGUIUtility.singleLineHeight;

			ITerrainDefaultGenerator tg = (ITerrainDefaultGenerator)property.serializedObject.targetObject;
			if (tg.Steps == null)
				return;

			int[] stepIndices = new int[tg.Steps.Length];
			for (int k = 0; k < tg.Steps.Length; k++) {
				stepIndices[k] = k;
			}
			GUIContent[] stepLabels = new GUIContent[tg.Steps.Length];
			for (int k = 0; k < tg.Steps.Length; k++) {
				stepLabels[k] = new GUIContent("Step " + k.ToString());
			}

			EditorGUIUtility.labelWidth = 120;

			SerializedProperty enabled = property.FindPropertyRelative("enabled");
			Rect prevPosition = position;
			position.x -= 14;
			position.width = 35;
			EditorGUI.PropertyField(position, enabled, GUIContent.none);
			position = prevPosition;
			SerializedProperty stepType = property.FindPropertyRelative("operation");
			int index = property.GetArrayIndex();
			position.width -= 60;
			EditorGUI.PropertyField(position, stepType, new GUIContent("Step " + index));
			position.x = position.x + position.width + 5;
			position.width = 60;
			if (GUI.Button(position, "Help")) {
				string helpMsg = "";
				switch (stepType.intValue) {
					case (int)TerrainStepType.SampleHeightMapTexture:
						helpMsg = "Reads a value from a heightmap texture (Voxel Play provides 3 noise textures inside Resources/Worlds/Earth/Noise folder, but you can provide your own heightmaps). Frecuency: the scale used to sample the texture. Min/Max: the output range. Sampled values are mapped to this range.";
						break;
					case (int)TerrainStepType.SampleRidgeNoiseFromTexture:
						helpMsg = "Same than Sample Height Map Texture but applies the following formula to the noise value:  2 * (0.5 - abs(0.5-value)). Produces more acute reliefs, usually to generate rivers (when inverted) or pointy mountains.";
						break;
					case (int)TerrainStepType.SampleHeightMapUnityTerrain:
						helpMsg = "Same than Sample Height Map Texture but reads from the heightmap of an existing terrain in the scene. Note: if you want to recreate an existing terrain including vegetation and trees but with voxel style, check the Unity Terrain Generator instead.";
						break;
					case (int)TerrainStepType.Constant:
						helpMsg = "Outputs a constant value.";
						break;
					case (int)TerrainStepType.Copy:
						helpMsg = "Copies a result from any previous step.";
						break;
					case (int)TerrainStepType.Random:
						helpMsg = "Produces a random value in the 0-1 range.";
						break;
					case (int)TerrainStepType.Invert:
						helpMsg = "Inverts previous value. Equals to 1-value.";
						break;
					case (int)TerrainStepType.AddAndMultiply:
						helpMsg = "Adds a number to the previous value and multiply the result by another number.";
						break;
					case (int)TerrainStepType.MultiplyAndAdd:
						helpMsg = "Multiplies previous value by a number and add another number to the result.";
						break;
					case (int)TerrainStepType.Exponential:
						helpMsg = "Applies an exponential function to the previous value. Usually used to increase valleys and mountains shapes.";
						break;
					case (int)TerrainStepType.Remap:
						helpMsg = "Linearly remaps the incoming value from a source range into a target range. This operation does not clamp automatically, so values outside the source range will continue beyond the target range.";
						break;
					case (int)TerrainStepType.Abs:
						helpMsg = "Outputs the absolute value of the previous result.";
						break;
					case (int)TerrainStepType.Terraces:
						helpMsg = "Quantizes the incoming value into terrace bands. Smoothness softens the transitions and Strength blends the terraced result with the original input.";
						break;
					case (int)TerrainStepType.Threshold:
						helpMsg = "Checks a value for a minimum. If the value is equal or greater than the minimum, output that value plus an optional number. If the value is less than the minimum, replace it by a constant value.";
						break;
					case (int)TerrainStepType.FlattenOrRaise:
						helpMsg = "Checks a value for a minimum. If the value is equal or greater than the minimum, multiply that value by a number. If the value is less than the minimum, output it as is.";
						break;
					case (int)TerrainStepType.BlendAdditive:
						helpMsg = "Adds 2 previous values with custom weights. Equals to (value1 * weight1 + value2 * weight2).";
						break;
					case (int)TerrainStepType.BlendMultiply:
						helpMsg = "Multiplies 2 previous values.";
						break;
					case (int)TerrainStepType.Min:
						helpMsg = "Outputs the smaller of 2 previous values.";
						break;
					case (int)TerrainStepType.Max:
						helpMsg = "Outputs the larger of 2 previous values.";
						break;
					case (int)TerrainStepType.Subtract:
						helpMsg = "Subtracts input B from input A.";
						break;
					case (int)TerrainStepType.Divide:
						helpMsg = "Divides input A by input B. If input B is zero or nearly zero, the output becomes 0 to avoid invalid values.";
						break;
					case (int)TerrainStepType.Clamp:
						helpMsg = "Ensures a value falls between a given range. If a value is 0.3 and the desired range is 0.4..0.8, clamp will output 0.4. If the value is 0.9, clamp will output 0.8. If the value is between 0.4 and 0.8, Clamp won’t modify the value.";
						break;
					case (int)TerrainStepType.Select:
						helpMsg = "Filters values from a previous step by given a valid range. Any value outside the range will be replaced by a constant.";
						break;
					case (int)TerrainStepType.Fill:
						helpMsg = "Replaces any value from a previous step in the given range by a constant.";
						break;
					case (int)TerrainStepType.Island:
						helpMsg = "This operator will reduce the height value based on the distance to 0,0,0 position.";
						break;
					case (int)TerrainStepType.Test:
						helpMsg = "Checks if a previous value falls inside a given range. If it does, test will output 1. If not, it will output 0.";
						break;
					case (int)TerrainStepType.SampleHeightMapFractal:
						helpMsg = "Builds procedural fractal noise. Frequency, octaves, persistence and lacunarity control the shape, and the result is remapped to the configured min/max range.";
						break;
					case (int)TerrainStepType.Shift:
						helpMsg = "Adds a value to the previous result.";
						break;
					case (int)TerrainStepType.BeachMask:
						helpMsg = "Useful to ensure no beaches occur on certain world areas. If the value from a chosen step is greater than certain threshold value then it will mask out any potential beach (shore voxel) on that position";
						break;
				}
				if (!string.IsNullOrEmpty(helpMsg)) {
					EditorUtility.DisplayDialog("Step Operator Description", helpMsg, "Ok");
				}
			}
			position = prevPosition;
			if (enabled.boolValue) {
				position.y += lineHeight;
				SerializedProperty stepLabel = property.FindPropertyRelative("description");
				EditorGUI.PropertyField(position, stepLabel, new GUIContent("User Description"));
				switch (stepType.intValue) {
					case (int)TerrainStepType.SampleHeightMapTexture:
					case (int)TerrainStepType.SampleRidgeNoiseFromTexture:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("noiseTexture"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("frecuency"), new GUIContent("Frequency", "The scale applied to the noise texture"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("offset"), new GUIContent("Offset", "Offset applied to the sampling coordinates"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("noiseRangeMin"), new GUIContent("Min", "The value of noise is mapped to min-max range"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("noiseRangeMax"), new GUIContent("Max", "The value of noise is mapped to min-max range"));
						break;
					case (int)TerrainStepType.SampleHeightMapFractal:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("frecuency"), new GUIContent("Frequency", "Base frequency applied to the procedural fractal noise"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("octaves"), new GUIContent("Octaves", "Number of procedural noise layers to be combined"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("persistence"), new GUIContent("Persistence", "Multiplier to the amplitude value of previous octave"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("lacunarity"), new GUIContent("Lacunarity", "Multiplier to the frequency value of previous octave"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("noiseRangeMin"), new GUIContent("Min", "The final value of noise is mapped to min-max range"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("noiseRangeMax"), new GUIContent("Max", "The final value of noise is mapped to min-max range"));
						break;
					case (int)TerrainStepType.SampleHeightMapUnityTerrain:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("terrainData"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("frecuency"), new GUIContent("Frequency", "The scale applied to the heightmap"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("offset"), new GUIContent("Offset", "Offset applied to the sampling coordinates"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("noiseRangeMin"), new GUIContent("Min", "The value of heightmap is mapped to min-max range"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("noiseRangeMax"), new GUIContent("Max", "The value of heightmap is mapped to min-max range"));
						break;
					case (int)TerrainStepType.Constant:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param"), new GUIContent("Constant", "Outputs a constant value."));
						break;
					case (int)TerrainStepType.Copy:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Copy Output From", "Copies a result from a previous step."));
						break;
					case (int)TerrainStepType.Random:
						break;
					case (int)TerrainStepType.Invert:
						break;
					case (int)TerrainStepType.Shift:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param"), new GUIContent("Add", "The value to add to the previous result."));
						break;
					case (int)TerrainStepType.BeachMask:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Mask Source", "If mask value is zero and altitude is at beach level then altitude will be reduced."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("threshold"), new GUIContent("Threshold", "Values greater than this threshold will cancel beach."));
						break;
					case (int)TerrainStepType.AddAndMultiply:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param"), new GUIContent("Add", "The value to add to the previous result."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param2"), new GUIContent("Then Multiply", "Multiply the result by this value."));
						break;
					case (int)TerrainStepType.MultiplyAndAdd:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param"), new GUIContent("Multiply", "Multiply the value by this value."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param2"), new GUIContent("Then Add", "Add this value to the result."));
						break;
					case (int)TerrainStepType.Exponential:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param"), new GUIContent("Exponent", "Result = exp(distance to 0, exponent)"));
						break;
					case (int)TerrainStepType.Remap:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("min"), new GUIContent("From Min", "Lower bound of the source range."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("max"), new GUIContent("From Max", "Upper bound of the source range."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("threshold"), new GUIContent("To Min", "Lower bound of the target range."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("thresholdParam"), new GUIContent("To Max", "Upper bound of the target range."));
						break;
					case (int)TerrainStepType.Abs:
						break;
					case (int)TerrainStepType.Terraces:
						position.y += lineHeight;
						SerializedProperty terraceSteps = property.FindPropertyRelative("octaves");
						terraceSteps.intValue = Mathf.Clamp(EditorGUI.IntField(position, new GUIContent("Steps", "How many terrace bands are generated."), terraceSteps.intValue), 2, 64);
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param"), new GUIContent("Smoothness", "How soft the transitions are between terraces. 0 creates hard steps."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param2"), new GUIContent("Strength", "Blend between the original input and the terraced result."));
						break;
					case (int)TerrainStepType.Island:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param"), new GUIContent("Radius", "Reduces terrain height beyond distance to 0,0,0."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("param2"), new GUIContent("Slope Multiplier", "Multiplier for the slope beyond the radius (0.01 - 5)."));
						break;
					case (int)TerrainStepType.Threshold:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input", "The source for the threshold operation"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("threshold"), new GUIContent("Threshold", "Only values greater than threshold are preserved. Otherwise 0 is output."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("thresholdShift"), new GUIContent("If Greater, Add", "A value that is added to the previous value if it passes the threshold."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("thresholdParam"), new GUIContent("If Not, Output...", "A value that is set if the previous value does not pass the threshold."));
						break;
					case (int)TerrainStepType.FlattenOrRaise:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("threshold"), new GUIContent("Min Elevation", "Values greater than this threshold will be flattened."));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("thresholdParam"), new GUIContent("Multiplier", "Flatten multiplier."));
						break;
					case (int)TerrainStepType.BlendAdditive:
						position.y += lineHeight;
						prevPosition = position;
						position.width = 190;
						float labelWidth = EditorGUIUtility.labelWidth;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input A", "One of the inputs to combine"));
						position.x += 190;
						position.width = 120;
						EditorGUIUtility.labelWidth = 60;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("weight0"), new GUIContent("Weight", "Input A is multiplied by Weight"));
						position = prevPosition;
						position.y += lineHeight;
						prevPosition = position;
						position.width = 190;
						EditorGUIUtility.labelWidth = labelWidth;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex1"), stepLabels, stepIndices, new GUIContent("Input B", "The other part of combination"));
						position.x += 190;
						position.width = 120;
						EditorGUIUtility.labelWidth = 60;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("weight1"), new GUIContent("Weight", "Input A is multiplied by Weight"));
						position = prevPosition;
						break;
					case (int)TerrainStepType.BlendMultiply:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input A", "Result = input A * input B"));
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex1"), stepLabels, stepIndices, new GUIContent("Input B", "Result = input A * input B"));
						break;
					case (int)TerrainStepType.Min:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input A", "Result = min(input A, input B)"));
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex1"), stepLabels, stepIndices, new GUIContent("Input B", "Result = min(input A, input B)"));
						break;
					case (int)TerrainStepType.Max:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input A", "Result = max(input A, input B)"));
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex1"), stepLabels, stepIndices, new GUIContent("Input B", "Result = max(input A, input B)"));
						break;
					case (int)TerrainStepType.Subtract:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input A", "Result = input A - input B"));
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex1"), stepLabels, stepIndices, new GUIContent("Input B", "Result = input A - input B"));
						break;
					case (int)TerrainStepType.Divide:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input A", "Result = input A / input B"));
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex1"), stepLabels, stepIndices, new GUIContent("Input B", "Result = input A / input B"));
						break;
					case (int)TerrainStepType.Clamp:
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("min"), new GUIContent("Min", "Outputs value or Min if value < Min"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("max"), new GUIContent("Max", "Outputs value or Max if value > Max"));
						break;
					case (int)TerrainStepType.Select:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input", "Choose a step as a source"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("min"), new GUIContent("Range Min", "Outputs 0 if value is less than Min"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("max"), new GUIContent("Range Max", "Outputs 0 if value is greater than Max"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("thresholdParam"), new GUIContent("Outside Value", "Outputs a different value if it's out of range"));
						break;
					case (int)TerrainStepType.Fill:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input", "Choose a step as a source"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("min"), new GUIContent("Range Min", "Outputs fill value if value is between min and max"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("max"), new GUIContent("Range Max", "Outputs fill value if value is between min and max"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("thresholdParam"), new GUIContent("Fill Value", "Replaces input value if it's inside the min-max range"));
						break;
					case (int)TerrainStepType.Test:
						position.y += lineHeight;
						EditorGUI.IntPopup(position, property.FindPropertyRelative("inputIndex0"), stepLabels, stepIndices, new GUIContent("Input", "Choose a step as a source"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("min"), new GUIContent("Range Min", "Outputs 0 if value is less than Min"));
						position.y += lineHeight;
						EditorGUI.PropertyField(position, property.FindPropertyRelative("max"), new GUIContent("Range Max", "Outputs 0 if value is greater than Max"));
						break;
				}

				// Buttons
				position.x += 20;
				position.y += lineHeight;
				const float BUTTON_WIDTH = 60;
				const float BUTTON_SPACING = 70;
				position.width = BUTTON_WIDTH;
				bool markSceneChanges = false;

				if (GUI.Button(position, "Add")) {
					List<StepData> od = new List<StepData>(tg.Steps);
					StepData stepData = new StepData();
					stepData.inputIndex0 = index;
					od.Insert(index + 1, stepData);
					tg.Steps = od.ToArray();
					// Shift any input reference
					for (int k = 0; k < tg.Steps.Length; k++) {
						if (tg.Steps[k].inputIndex0 > index)
							tg.Steps[k].inputIndex0++;
						if (tg.Steps[k].inputIndex1 > index)
							tg.Steps[k].inputIndex1++;

					}
					markSceneChanges = true;
				}
				position.x += BUTTON_SPACING;
				if (GUI.Button(position, "Remove")) {
					List<StepData> od = new List<StepData>(tg.Steps);
					od.RemoveAt(index);
					tg.Steps = od.ToArray();
					// Shift any input reference
					for (int k = 0; k < tg.Steps.Length; k++) {
						if (tg.Steps[k].inputIndex0 >= index)
							tg.Steps[k].inputIndex0--;
						if (tg.Steps[k].inputIndex1 >= index)
							tg.Steps[k].inputIndex1--;
					}
					markSceneChanges = true;
				}
				if (index > 0) {
					position.x += BUTTON_SPACING;
					if (GUI.Button(position, "Up")) {
						StepData o = tg.Steps[index - 1];
						tg.Steps[index - 1] = tg.Steps[index];
						tg.Steps[index] = o;
						// Shift any input reference
						for (int k = 0; k < tg.Steps.Length; k++) {
							if (tg.Steps[k].inputIndex0 == index)
								tg.Steps[k].inputIndex0--;
							if (tg.Steps[k].inputIndex1 == index)
								tg.Steps[k].inputIndex1--;
						}
						markSceneChanges = true;
					}
				}
				if (index < tg.Steps.Length - 1) {
					position.x += BUTTON_SPACING;
					if (GUI.Button(position, "Down")) {
						StepData o = tg.Steps[index + 1];
						tg.Steps[index + 1] = tg.Steps[index];
						tg.Steps[index] = o;
						// Shift any input reference
						for (int k = 0; k < tg.Steps.Length; k++) {
							if (tg.Steps[k].inputIndex0 == index)
								tg.Steps[k].inputIndex0++;
							if (tg.Steps[k].inputIndex1 == index)
								tg.Steps[k].inputIndex1++;
						}
						markSceneChanges = true;
					}
				}

				if (markSceneChanges && !Application.isPlaying) {
					UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
				}

			}

		}

		public override float GetPropertyHeight (SerializedProperty property, GUIContent label) {
			float lineHeight = EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
			int numLines = 3;
			switch (property.FindPropertyRelative("operation").intValue) {
				case (int)TerrainStepType.SampleHeightMapTexture:
				case (int)TerrainStepType.SampleRidgeNoiseFromTexture:
				case (int)TerrainStepType.SampleHeightMapUnityTerrain:
					numLines += 5;
					break;
				case (int)TerrainStepType.SampleHeightMapFractal:
					numLines += 6;
					break;
				case (int)TerrainStepType.BlendAdditive:
				case (int)TerrainStepType.BlendMultiply:
				case (int)TerrainStepType.Min:
				case (int)TerrainStepType.Max:
				case (int)TerrainStepType.Subtract:
				case (int)TerrainStepType.Divide:
				case (int)TerrainStepType.Clamp:
				case (int)TerrainStepType.MultiplyAndAdd:
				case (int)TerrainStepType.AddAndMultiply:
				case (int)TerrainStepType.FlattenOrRaise:
				case (int)TerrainStepType.BeachMask:
				case (int)TerrainStepType.Island:
					numLines += 2;
					break;
				case (int)TerrainStepType.Remap:
					numLines += 4;
					break;
				case (int)TerrainStepType.Terraces:
					numLines += 3;
					break;
				case (int)TerrainStepType.Test:
					numLines += 3;
					break;
				case (int)TerrainStepType.Threshold:
				case (int)TerrainStepType.Select:
				case (int)TerrainStepType.Fill:
					numLines += 4;
					break;
				case (int)TerrainStepType.Random:
				case (int)TerrainStepType.Invert:
				case (int)TerrainStepType.Abs:
					break;
				default:
					numLines++;
					break;
			}
			float height = property.FindPropertyRelative("enabled").boolValue ? lineHeight * numLines : lineHeight;
			return height + 5f;
		}
	}
}
