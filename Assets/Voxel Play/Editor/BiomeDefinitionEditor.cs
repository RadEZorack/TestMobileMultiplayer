using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [CustomEditor(typeof(BiomeDefinition), isFallback = true)]
    public class BiomeDefinitionEditor : UnityEditor.Editor {

        bool showBiomeSettings = true;
        bool showTerrainVoxels = true;
        bool showUnderground = true;
        bool showTrees = true;
        bool showVegetation = true;

        SerializedProperty zones, showInBiomeMap, biomeMapColor;
        SerializedProperty voxelTop, voxelTopAdditional, voxelDirt, voxelDirtAdditional, voxelLakeBed, voxelWater;
        SerializedProperty ores, undergroundVegDensity, undergroundVegetation, undergroundCeilingVegDensity, undergroundCeilingVegetation;
        SerializedProperty treeDensity, trees;
        SerializedProperty vegetationDensity, vegetation, underwaterVegetationDensity, underwaterVegetation;

        public virtual void OnEnable () {
            // Biome Settings
            zones = serializedObject.FindProperty("zones");
            showInBiomeMap = serializedObject.FindProperty("showInBiomeMap");
            biomeMapColor = serializedObject.FindProperty("biomeMapColor");

            // Terrain Voxels
            voxelTop = serializedObject.FindProperty("voxelTop");
            voxelTopAdditional = serializedObject.FindProperty("voxelTopAdditional");
            voxelDirt = serializedObject.FindProperty("voxelDirt");
            voxelDirtAdditional = serializedObject.FindProperty("voxelDirtAdditional");
            voxelLakeBed = serializedObject.FindProperty("voxelLakeBed");
            voxelWater = serializedObject.FindProperty("voxelWater");

            // Underground
            ores = serializedObject.FindProperty("ores");
            undergroundVegDensity = serializedObject.FindProperty("undergroundVegDensity");
            undergroundVegetation = serializedObject.FindProperty("undergroundVegetation");
            undergroundCeilingVegDensity = serializedObject.FindProperty("undergroundCeilingVegDensity");
            undergroundCeilingVegetation = serializedObject.FindProperty("undergroundCeilingVegetation");

            // Trees
            treeDensity = serializedObject.FindProperty("treeDensity");
            trees = serializedObject.FindProperty("trees");

            // Vegetation
            vegetationDensity = serializedObject.FindProperty("vegetationDensity");
            vegetation = serializedObject.FindProperty("vegetation");
            underwaterVegetationDensity = serializedObject.FindProperty("underwaterVegetationDensity");
            underwaterVegetation = serializedObject.FindProperty("underwaterVegetation");

            voxelTopAdditional.isExpanded = true;
            voxelDirtAdditional.isExpanded = true;
        }

        public override void OnInspectorGUI () {
            BiomeDefinition biome = (BiomeDefinition)target;

            // Biome Settings
            showBiomeSettings = EditorGUILayout.Foldout(showBiomeSettings, "Biome Settings", true);
            if (showBiomeSettings) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(zones, true);
                EditorGUILayout.PropertyField(showInBiomeMap);
                EditorGUILayout.PropertyField(biomeMapColor);
                EditorGUI.indentLevel--;
            }

            // Terrain Voxels
            EditorGUILayout.Space();
            showTerrainVoxels = EditorGUILayout.Foldout(showTerrainVoxels, "Terrain Voxels", true);
            if (showTerrainVoxels) {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.PropertyField(voxelTop, new GUIContent("Voxel Top (Surface)"));
                EditorGUILayout.PropertyField(voxelTopAdditional, new GUIContent("Additional Voxels"), true);
                EditorGUILayout.HelpBox("Add any number of additional voxels for the SURFACE of this biome. The sum of all probabilities must be 1. If the sum is less than 1, the remaining probability will be used by the main voxelTop. For example, if the sum of probabilities for additional voxels is 0.6, the main voxel top will be used in 40% (0.4) of surface voxels in this biome.", MessageType.Info);
                EditorGUILayout.EndVertical();
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.PropertyField(voxelDirt, new GUIContent("Voxel Dirt (Underground)"));
                EditorGUILayout.PropertyField(voxelDirtAdditional, new GUIContent("Additional Voxels"), true);
                EditorGUILayout.HelpBox("Add any number of additional voxels for the UNDERGROUND of this biome. The sum of all probabilities must be 1. If the sum is less than 1, the remaining probability will be used by the main voxelDirt. For example, if the sum of probabilities for additional voxels is 0.6, the main voxel dirt will be used in 40% (0.4) of underground voxels in this biome.", MessageType.Info);
                EditorGUILayout.EndVertical();
                EditorGUILayout.PropertyField(voxelLakeBed, new GUIContent("Lake Bed"));
                EditorGUILayout.PropertyField(voxelWater, new GUIContent("Water (Optional)", "If assigned, this water voxel will be used by the default terrain generator within this biome."));
                EditorGUI.indentLevel--;
            }

            // Vegetation
            EditorGUILayout.Space();
            showVegetation = EditorGUILayout.Foldout(showVegetation, "Vegetation", true);
            if (showVegetation) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(vegetationDensity);
                EditorGUILayout.PropertyField(vegetation, true);
                EditorGUILayout.PropertyField(underwaterVegetationDensity);
                EditorGUILayout.PropertyField(underwaterVegetation, true);
                EditorGUI.indentLevel--;
            }

            // Trees
            EditorGUILayout.Space();
            showTrees = EditorGUILayout.Foldout(showTrees, "Trees", true);
            if (showTrees) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(treeDensity);
                EditorGUILayout.PropertyField(trees, true);
                EditorGUI.indentLevel--;
            }

            // Underground
            EditorGUILayout.Space();
            showUnderground = EditorGUILayout.Foldout(showUnderground, "Underground", true);
            if (showUnderground) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(ores, true);
                EditorGUILayout.PropertyField(undergroundVegDensity, new GUIContent("Underground Vegetation Density"));
                if (undergroundVegDensity.floatValue > 0) {
                    EditorGUILayout.PropertyField(undergroundVegetation, true);
                }
                EditorGUILayout.PropertyField(undergroundCeilingVegDensity, new GUIContent("Ceiling Vegetation Density"));
                if (undergroundCeilingVegDensity.floatValue > 0) {
                    EditorGUILayout.PropertyField(undergroundCeilingVegetation, true);
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}