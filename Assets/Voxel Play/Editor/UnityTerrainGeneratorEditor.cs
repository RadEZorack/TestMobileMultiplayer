using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    [CustomEditor(typeof(UnityTerrainGenerator), isFallback = true)]
    public class UnityTerrainGeneratorEditor : Editor {

        Color titleColor;
        static GUIStyle titleLabelStyle, boxStyle, sectionHeaderStyle;
        UnityTerrainGenerator tg;
        int terrainTextureSize = 16;
        int treeTextureSize = 64;
        int vegetationTextureSize = 64;
        float frondDensity = 0.5f;
        bool cleanFolders;
        bool expandTerrainTextures, expandTrees, expandVegetation;

        [Range(16, 256)]
        int thumbnailSize = 104;

        [Range(0.1f, 2f)]
        float treeScale = 1f;

        SerializedProperty terrainData, addWater, waterLevel, vegetationDensity, waterVoxel, minHeight, enableHalfStepSurface, extendWaterBeyondTerrain;
        Dictionary<Texture2D, VoxelDefinition> textureVoxels;
        List<string> pendingTextureImports;
        int generatedTerrainVoxels, generatedTreeModels, generatedVegetationVoxels;
        List<string> generationWarnings;
        string[] textureIndices;
        int[] textureIndicesValues;
        Terrain activeTerrain;
        static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

        static Terrain FindSceneTerrain(TerrainData td) {
            if (td == null) return null;
            Terrain[] allTerrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Terrain t in allTerrains) {
                if (t.terrainData == td) return t;
            }
            return null;
        }

        bool NeedsTerrainMappingRefresh() {
            if (tg == null) return false;
            if (tg.terrainData == null) return true;
            return tg.splatSettings == null || tg.splatSettings.Length < Mathf.Max(tg.terrainData.terrainLayers.Length, 1)
                || tg.detailSettings == null || tg.detailSettings.Length < Mathf.Max(tg.terrainData.detailPrototypes.Length, 1)
                || tg.treeSettings == null || tg.treeSettings.Length < Mathf.Max(tg.terrainData.treePrototypes.Length, 1);
        }

        void SyncTerrainMappings() {
            if (tg == null) return;

            if (tg.terrainData == null) {
                Terrain sceneTerrain = Terrain.activeTerrain;
                if (sceneTerrain == null) {
                    Terrain[] terrains = Terrain.activeTerrains;
                    if (terrains != null && terrains.Length > 0) {
                        sceneTerrain = terrains[0];
                    }
                }
                if (sceneTerrain != null && sceneTerrain.terrainData != null) {
                    tg.terrainData = sceneTerrain.terrainData;
                    tg.terrainPos = sceneTerrain.GetPosition();
                    EditorUtility.SetDirty(tg);
                }
            }

            if (tg.terrainData != null) {
                tg.ExamineTerrainData();
                activeTerrain = FindSceneTerrain(tg.terrainData);
            }
        }

        static Texture2D GetPreviewTexture(UnityEngine.Object source, ref bool previewPending) {
            if (source == null) return null;
            Texture2D preview = AssetPreview.GetAssetPreview(source);
            if (preview == null) {
                if (AssetPreview.IsLoadingAssetPreview(source.GetInstanceID())) {
                    previewPending = true;
                }
                preview = AssetPreview.GetMiniThumbnail(source);
            }
            return preview;
        }

        bool RefreshPendingPreviews() {
            if (tg == null || tg.terrainData == null) return false;

            bool previewPending = false;
            bool changed = false;

            for (int k = 0; k < tg.terrainData.treePrototypes.Length && k < tg.treeSettings.Length; k++) {
                GameObject prefab = tg.terrainData.treePrototypes[k].prefab;
                Texture2D preview = GetPreviewTexture(prefab, ref previewPending);
                if (preview != null && tg.treeSettings[k].preview != preview) {
                    tg.treeSettings[k].preview = preview;
                    changed = true;
                }
            }

            for (int k = 0; k < tg.terrainData.detailPrototypes.Length && k < tg.detailSettings.Length; k++) {
                Texture2D preview = null;
                string previewName = tg.detailSettings[k].previewName;
                if (tg.terrainData.detailPrototypes[k].prototype != null) {
                    preview = GetPreviewTexture(tg.terrainData.detailPrototypes[k].prototype, ref previewPending);
                    previewName = tg.terrainData.detailPrototypes[k].prototype.name;
                } else if (tg.terrainData.detailPrototypes[k].prototypeTexture != null) {
                    preview = tg.terrainData.detailPrototypes[k].prototypeTexture;
                    previewName = tg.terrainData.detailPrototypes[k].prototypeTexture.name;
                }

                if (preview != null && tg.detailSettings[k].preview != preview) {
                    tg.detailSettings[k].preview = preview;
                    changed = true;
                }
                if (!string.Equals(tg.detailSettings[k].previewName, previewName, StringComparison.Ordinal)) {
                    tg.detailSettings[k].previewName = previewName;
                    changed = true;
                }
            }

            if (changed) {
                EditorUtility.SetDirty(tg);
            }

            return previewPending;
        }

        public virtual void OnEnable() {
            titleColor = EditorGUIUtility.isProSkin ? new Color(0.52f, 0.66f, 0.9f) : new Color(0.12f, 0.16f, 0.4f);
            tg = target as UnityTerrainGenerator;
            if (tg == null)
                return;
            terrainData = serializedObject.FindProperty("terrainData");
            minHeight = serializedObject.FindProperty("minHeight");
            addWater = serializedObject.FindProperty("addWater");
            waterLevel = serializedObject.FindProperty("waterLevel");
            waterVoxel = serializedObject.FindProperty("waterVoxel");
            vegetationDensity = serializedObject.FindProperty("vegetationDensity");
            enableHalfStepSurface = serializedObject.FindProperty("enableHalfStepSurface");
            extendWaterBeyondTerrain = serializedObject.FindProperty("extendWaterBeyondTerrain");
            if (NeedsTerrainMappingRefresh()) {
                SyncTerrainMappings();
            }
            if (RefreshPendingPreviews()) {
                Repaint();
            }
            activeTerrain = FindSceneTerrain(tg.terrainData);

        }


        public override void OnInspectorGUI() {

            if (tg == null)
                return;

            if (NeedsTerrainMappingRefresh()) {
                SyncTerrainMappings();
            }
            bool previewLoading = RefreshPendingPreviews();

            serializedObject.Update();

            if (titleLabelStyle == null) {
                titleLabelStyle = new GUIStyle(EditorStyles.label);
            }
            titleLabelStyle.normal.textColor = titleColor;
            titleLabelStyle.fontStyle = FontStyle.Bold;
            if (boxStyle == null) {
                boxStyle = new GUIStyle(GUI.skin.box);
            }
            if (sectionHeaderStyle == null) {
                sectionHeaderStyle = new GUIStyle(EditorStyles.foldout);
            }
            sectionHeaderStyle.SetFoldoutColor();

            VoxelPlayEnvironment sceneEnv = VoxelPlayEnvironment.instance != null ? VoxelPlayEnvironment.instance : UnityEngine.Object.FindFirstObjectByType<VoxelPlayEnvironment>();
            if (sceneEnv == null) {
                EditorGUILayout.HelpBox("A VoxelPlayEnvironment component is required in the scene for terrain generation to work.", MessageType.Warning);
                EditorGUILayout.Separator();
            } else if (Selection.activeGameObject == null || Selection.activeGameObject.GetComponent<VoxelPlayEnvironment>() == null) {
                if (GUILayout.Button("Go to Voxel Play Environment")) {
                    Selection.activeGameObject = sceneEnv.gameObject;
                }
                EditorGUILayout.Separator();
            }

            EditorGUILayout.BeginHorizontal();
            TerrainData prevTD = (TerrainData)terrainData.objectReferenceValue;
            EditorGUILayout.PropertyField(terrainData);
            TerrainData td = (TerrainData)terrainData.objectReferenceValue;
            if (td != prevTD) {
                serializedObject.ApplyModifiedProperties();
                if (td != null) {
                    Terrain matching = UnityTerrainGenerator.FindMatchingTerrain(td);
                    if (matching != null) {
                        tg.terrainPos = matching.GetPosition();
                        activeTerrain = matching;
                        EditorUtility.SetDirty(tg);
                    }
                }
                tg.ExamineTerrainData();
                previewLoading = RefreshPendingPreviews() || previewLoading;
                if (td != null && sceneEnv != null) {
                    UpdateWorldBoundsFromTerrain(sceneEnv);
                }
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(60))) {
                Terrain matching = UnityTerrainGenerator.FindMatchingTerrain(tg.terrainData);
                if (matching != null) {
                    tg.terrainPos = matching.GetPosition();
                    activeTerrain = matching;
                    EditorUtility.SetDirty(tg);
                }
                tg.ExamineTerrainData();
                if (sceneEnv != null) {
                    UpdateWorldBoundsFromTerrain(sceneEnv);
                    if (!sceneEnv.renderInEditor) {
                        sceneEnv.renderInEditor = true;
                        EditorUtility.SetDirty(sceneEnv);
                    }
                    sceneEnv.ReloadWorld();
                    ForceTerrainChunksBelowCamera(sceneEnv);
                    GUIUtility.ExitGUI();
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (activeTerrain != null) {
                bool visible = EditorGUILayout.Toggle(new GUIContent("Show/Hide Terrain", "Toggle visibility of the Unity terrain in the scene."), activeTerrain.enabled);
                if (activeTerrain.enabled != visible) {
                    activeTerrain.enabled = visible;
                }
            }

            if (sceneEnv != null) {
                bool chunksVisible = sceneEnv.ChunksVisible;
                bool newChunksVisible = EditorGUILayout.Toggle(new GUIContent("Show/Hide Voxels", "Toggle visibility of the generated voxel chunks."), chunksVisible);
                if (newChunksVisible != chunksVisible) {
                    if (newChunksVisible && sceneEnv.chunksCreated == 0) {
                        sceneEnv.ReloadWorld(keepWorldChanges: false);
                        GUIUtility.ExitGUI();
                        return;
                    } else {
                        sceneEnv.ChunksToggle(newChunksVisible);
                    }
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.PropertyField(minHeight, new GUIContent("Min Height", "Minimum terrain height. Voxels below this level use the bedrock voxel."));
            EditorGUILayout.PropertyField(addWater, new GUIContent("Add Water", "Enable water generation on the terrain."));
            if (addWater.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(waterLevel, new GUIContent("Water Level", "Y coordinate of the water surface."));
                EditorGUILayout.PropertyField(waterVoxel, new GUIContent("Water Voxel", "Voxel definition used for water."));
                EditorGUILayout.PropertyField(extendWaterBeyondTerrain, new GUIContent("Extend Water", "Fill chunks beyond the terrain bounds with water up to the water level."));
                EditorGUI.indentLevel--;
            }
            bool prevHalfStep = enableHalfStepSurface.boolValue;
            EditorGUILayout.PropertyField(enableHalfStepSurface, new GUIContent("Half-Step Surface", "Enable half-voxel on terrain surface when fractional height < 0.5."));
            bool needCreate = false;
            bool needAssign = false;
            bool allIgnored = true;
            bool needsReload = false;
            if (tg.terrainData != null) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Thumbnail Size", "Size of the texture preview thumbnails in pixels."), GUILayout.Width(EditorGUIUtility.labelWidth));
                thumbnailSize = EditorGUILayout.IntSlider(thumbnailSize, 16, 256);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Separator();
                expandTerrainTextures = EditorGUILayout.Foldout(expandTerrainTextures, new GUIContent("Terrain Textures", "Terrain layer to voxel definition mappings."), true, sectionHeaderStyle);
                if (expandTerrainTextures) {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Texture Size", "Resolution of the generated voxel textures in pixels."), GUILayout.Width(EditorGUIUtility.labelWidth));
                    terrainTextureSize = EditorGUILayout.IntField(terrainTextureSize);
                    EditorGUILayout.EndHorizontal();
                    for (int k = 0; k < tg.terrainData.terrainLayers.Length && k < tg.splatSettings.Length; k++) {
                        EditorGUILayout.LabelField("Texture " + (k + 1), GUILayout.Width(80));
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent(tg.splatSettings[k].preview), boxStyle, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize));
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Voxel Top", "Voxel definition used for the surface of this terrain layer."), GUILayout.Width(80));
                        tg.splatSettings[k].top = (VoxelDefinition)EditorGUILayout.ObjectField(tg.splatSettings[k].top, typeof(VoxelDefinition), false);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Smooth", "Smoothing applied to the generated texture. 0 = sharp, 1 = maximum blur."), GUILayout.Width(80));
                        tg.splatSettings[k].smoothPower = EditorGUILayout.Slider(tg.splatSettings[k].smoothPower, 0, 1f);
                        EditorGUILayout.EndHorizontal();

                        int textureCount = tg.terrainData.terrainLayers.Length;
                        if (textureIndices == null || textureIndices.Length != textureCount) {
                            textureIndices = new string[textureCount];
                            textureIndicesValues = new int[textureCount];
                            for (int t = 0; t < textureCount; t++) {
                                textureIndices[t] = "Texture " + (t + 1);
                                textureIndicesValues[t] = (t + 1);
                            }
                        }

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Dirt With", "Which terrain texture to blend with when generating the dirt voxel texture."), GUILayout.Width(80));
                        tg.splatSettings[k].dirtWith = EditorGUILayout.IntPopup(tg.splatSettings[k].dirtWith, textureIndices, textureIndicesValues);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Blend Power", "How much of the secondary texture is blended into the dirt texture. 0 = no blend, 1 = full blend."), GUILayout.Width(80));
                        tg.splatSettings[k].blendPower = EditorGUILayout.Slider(tg.splatSettings[k].blendPower, 0, 1f);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Voxel Dirt", "Voxel definition used for underground layers of this terrain layer."), GUILayout.Width(80));
                        tg.splatSettings[k].dirt = (VoxelDefinition)EditorGUILayout.ObjectField(tg.splatSettings[k].dirt, typeof(VoxelDefinition), false);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Weight Bias", "Adjusts layer priority in blended areas. Positive values make this layer win over others with similar alpha weights."), GUILayout.Width(80));
                        float prevBias = tg.splatSettings[k].weightBias;
                        tg.splatSettings[k].weightBias = EditorGUILayout.Slider(tg.splatSettings[k].weightBias, -1f, 1f);
                        if (prevBias != tg.splatSettings[k].weightBias) {
                            needsReload = true;
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Action", "Create: generate voxel definitions. Assigned: use existing definitions. Ignore: skip this layer."), GUILayout.Width(80));
                        tg.splatSettings[k].action = (UnityTerrainGenerator.TerrainResourceAction)EditorGUILayout.EnumPopup(tg.splatSettings[k].action);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        if (tg.splatSettings[k].action != UnityTerrainGenerator.TerrainResourceAction.Ignore)
                            allIgnored = false;
                        if (tg.splatSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Create) {
                            needCreate = true;
                        } else if ((tg.splatSettings[k].top == null || tg.splatSettings[k].dirt == null) && tg.splatSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Assigned) {
                            needAssign = true;
                        }
                        EditorGUILayout.Separator();
                    }
                }

                EditorGUILayout.Separator();
                expandTrees = EditorGUILayout.Foldout(expandTrees, new GUIContent("Trees"), true, sectionHeaderStyle);
                if (expandTrees) {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Texture Size", "Resolution of the generated tree voxel textures in pixels."), GUILayout.Width(EditorGUIUtility.labelWidth));
                    treeTextureSize = EditorGUILayout.IntField(treeTextureSize);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Frond Density", "Controls how dense the foliage texture is generated. 0 = sparse, 1 = full."), GUILayout.Width(EditorGUIUtility.labelWidth));
                    frondDensity = EditorGUILayout.Slider(frondDensity, 0f, 1f);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Tree Scale", "Scale multiplier applied to tree model dimensions."), GUILayout.Width(EditorGUIUtility.labelWidth));
                    treeScale = EditorGUILayout.Slider(treeScale, 0.1f, 2f);
                    EditorGUILayout.EndHorizontal();
                    for (int k = 0; k < tg.terrainData.treePrototypes.Length && k < tg.treeSettings.Length; k++) {
                        EditorGUILayout.LabelField("Tree " + (k + 1), GUILayout.Width(80));
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent(tg.treeSettings[k].preview), boxStyle, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize));
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Model Def.", "Voxel model definition used for this tree prototype."), GUILayout.Width(80));
                        tg.treeSettings[k].md = (ModelDefinition)EditorGUILayout.ObjectField(tg.treeSettings[k].md, typeof(ModelDefinition), false);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Smooth", "Smoothing applied to the generated tree texture."), GUILayout.Width(80));
                        tg.treeSettings[k].smoothPower = EditorGUILayout.Slider(tg.treeSettings[k].smoothPower, 0, 1f);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Action", "Create: generate model definition. Assigned: use existing. Ignore: skip this tree."), GUILayout.Width(80));
                        tg.treeSettings[k].action = (UnityTerrainGenerator.TerrainResourceAction)EditorGUILayout.EnumPopup(tg.treeSettings[k].action);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        if (tg.treeSettings[k].action != UnityTerrainGenerator.TerrainResourceAction.Ignore)
                            allIgnored = false;
                        if (tg.treeSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Create) {
                            needCreate = true;
                        } else if (tg.treeSettings[k].md == null && tg.treeSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Assigned) {
                            needAssign = true;
                        }
                        EditorGUILayout.Separator();
                    }
                }

                EditorGUILayout.Separator();
                expandVegetation = EditorGUILayout.Foldout(expandVegetation, new GUIContent("Vegetation", "Detail layer to vegetation voxel mappings."), true, sectionHeaderStyle);
                if (expandVegetation) {
                    EditorGUILayout.PropertyField(vegetationDensity, new GUIContent("Vegetation Density", "Global probability multiplier for vegetation placement. 0 = none, 1 = full density."));
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Texture Size", "Resolution of the generated vegetation voxel textures in pixels."), GUILayout.Width(EditorGUIUtility.labelWidth));
                    vegetationTextureSize = EditorGUILayout.IntField(vegetationTextureSize);
                    EditorGUILayout.EndHorizontal();
                    for (int k = 0; k < tg.terrainData.detailPrototypes.Length && k < tg.detailSettings.Length; k++) {
                        EditorGUILayout.LabelField("Detail " + (k + 1), GUILayout.Width(EditorGUIUtility.labelWidth));
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent(tg.detailSettings[k].preview), boxStyle, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize));
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Voxel Def.", "Voxel definition used for this vegetation detail layer."), GUILayout.Width(80));
                        tg.detailSettings[k].vd = (VoxelDefinition)EditorGUILayout.ObjectField(tg.detailSettings[k].vd, typeof(VoxelDefinition), false);
                        EditorGUILayout.EndHorizontal();
                        if (tg.detailSettings[k].vd == null && tg.detailSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Assigned) {
                            tg.detailSettings[k].action = UnityTerrainGenerator.TerrainResourceAction.Create;
                        }
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent("Action", "Create: generate voxel definition. Assigned: use existing. Ignore: skip this detail."), GUILayout.Width(80));
                        tg.detailSettings[k].action = (UnityTerrainGenerator.TerrainResourceAction)EditorGUILayout.EnumPopup(tg.detailSettings[k].action);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        if (tg.detailSettings[k].action != UnityTerrainGenerator.TerrainResourceAction.Ignore)
                            allIgnored = false;
                        if (tg.detailSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Create) {
                            needCreate = true;
                        } else if (tg.detailSettings[k].vd == null && tg.detailSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Assigned) {
                            needAssign = true;
                        }
                        EditorGUILayout.Separator();
                    }
                }
            }
            EditorGUILayout.Separator();
            if (!allIgnored) {
                if (needAssign) {
                    EditorGUILayout.HelpBox("Please check all 'Assigned' resources are correctly set in the list above or press 'Refresh' to reload TerrainData info.", MessageType.Warning);
                } else if (needCreate) {
                    EditorGUILayout.HelpBox("Press 'Generate' to create voxel definitions for the terrain resources.", MessageType.Info);
                    if (GUILayout.Button("Generate")) {
                        Generate(cleanFolders);
                    }
                }
            }

            if (tg.terrainData != null) {
                EditorGUILayout.Separator();
                if (needsReload && sceneEnv != null) {
                    EditorGUILayout.HelpBox("Weight bias changed. Press 'Apply Weight Changes' to reload the world with the new settings.", MessageType.Info);
                    if (GUILayout.Button("Apply Weight Changes")) {
                        EditorUtility.SetDirty(tg);
                        tg.InvalidateCache();
                        sceneEnv.ReloadWorld(keepWorldChanges: false);
                        GUIUtility.ExitGUI();
                        return;
                    }
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Clean Folders", "Delete all previously generated files before regenerating."), GUILayout.Width(EditorGUIUtility.labelWidth));
                cleanFolders = EditorGUILayout.Toggle(cleanFolders);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Generate Terrain Voxels")) {
                    Generate(true);
                    tg.InvalidateCache();
                    if (sceneEnv != null) {
                        if (!sceneEnv.renderInEditor) {
                            sceneEnv.renderInEditor = true;
                            EditorUtility.SetDirty(sceneEnv);
                        }
                        sceneEnv.ReloadWorld();
                        ForceTerrainChunksBelowCamera(sceneEnv);
                        GUIUtility.ExitGUI();
                        return;
                    }
                }
            }

            EditorGUILayout.Separator();

            serializedObject.ApplyModifiedProperties();

            if (sceneEnv != null && enableHalfStepSurface.boolValue != prevHalfStep) {
                tg.InvalidateCache();
                sceneEnv.ReloadWorld(keepWorldChanges: false);
                GUIUtility.ExitGUI();
                return;
            }

            if (previewLoading) {
                Repaint();
            }

        }


        void UpdateWorldBoundsFromTerrain (VoxelPlayEnvironment env) {
            if (env.world.infinite || tg.terrainData == null) return;
            const int GAP = 16;
            Vector3 terrainSize = tg.terrainData.size;
            float bottom = Mathf.Min(tg.terrainPos.y, tg.terrainPos.y + tg.minHeight);
            float top = tg.terrainPos.y + terrainSize.y;
            float verticalSize = top - bottom;
            Vector3 center = tg.terrainPos + terrainSize * 0.5f;
            center.y = bottom + verticalSize * 0.5f;
            Vector3 extents = terrainSize * 0.5f + Vector3.one * GAP;
            extents.y = verticalSize * 0.5f + GAP;
            extents.x = Mathf.Ceil(extents.x / 16f) * 16f;
            extents.y = Mathf.Ceil(extents.y / 16f) * 16f;
            extents.z = Mathf.Ceil(extents.z / 16f) * 16f;
            bool changed = false;
            if (center != env.world.center) {
                env.world.center = center;
                changed = true;
            }
            if (extents != env.world.extents) {
                env.world.extents = extents;
                changed = true;
            }
            if (changed) {
                EditorUtility.SetDirty(env.world);
            }
        }

        /// <summary>
        /// If the scene camera is above the terrain and outside the vertical chunk distance,
        /// force a small 3x3 chunk patch at the terrain surface so the user can see something.
        /// </summary>
        void ForceTerrainChunksBelowCamera (VoxelPlayEnvironment env) {
            if (tg.terrainData == null) return;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            Vector3 camPos = sceneView.camera.transform.position;
            Vector3 terrainMin = tg.terrainPos;
            Vector3 terrainMax = terrainMin + tg.terrainData.size;

            // Only act if camera XZ is within terrain bounds
            if (camPos.x < terrainMin.x || camPos.x > terrainMax.x || camPos.z < terrainMin.z || camPos.z > terrainMax.z) return;

            // Sample terrain height at camera XZ
            float nx = (camPos.x - terrainMin.x) / tg.terrainData.size.x;
            float nz = (camPos.z - terrainMin.z) / tg.terrainData.size.z;
            float terrainHeight = terrainMin.y + tg.terrainData.GetInterpolatedHeight(nx, nz);

            // Only force chunks if camera is too far above terrain surface
            float verticalRange = env.visibleChunksVerticalDistance * VoxelPlayEnvironment.CHUNK_SIZE;
            if (camPos.y - terrainHeight <= verticalRange) return;

            float chunkSize = VoxelPlayEnvironment.CHUNK_SIZE;
            float patchSize = 3 * chunkSize;
            Vector3d center = new Vector3d(camPos.x, terrainHeight, camPos.z);
            Vector3d size = new Vector3d(patchSize, chunkSize * 2, patchSize);
            env.ChunkRedraw(new Boundsd(center, size), refreshLightmap: true, refreshMesh: true, ignoreFrustum: true);
        }

        static void CleanDirectory(string path) {
            if (Directory.Exists(path)) {
                DirectoryInfo di = new DirectoryInfo(path);
                foreach (FileInfo file in di.GetFiles()) {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories()) {
                    dir.Delete(true);
                }
            }
        }

        void Generate(bool forceGeneration = false) {
            generatedTerrainVoxels = 0;
            generatedTreeModels = 0;
            generatedVegetationVoxels = 0;
            generationWarnings = new List<string>();
            pendingTextureImports = new List<string>();
            try {
                if (cleanFolders) {
                    string basePath = GetPath();
                    CleanDirectory(basePath + "/TerrainVoxels");
                    CleanDirectory(basePath + "/Trees");
                    CleanDirectory(basePath + "/Vegetation");
                    AssetDatabase.Refresh();
                }

                EditorUtility.DisplayProgressBar("Generating Terrain Voxels", "Generating terrain textures...", 0f);
                GenerateTerrainVoxels(forceGeneration);

                EditorUtility.DisplayProgressBar("Generating Terrain Voxels", "Generating tree models...", 0.4f);
                GenerateTreeModels(forceGeneration);

                EditorUtility.DisplayProgressBar("Generating Terrain Voxels", "Generating vegetation...", 0.7f);
                GenerateVegetationVoxels(forceGeneration);

                EditorUtility.DisplayProgressBar("Generating Terrain Voxels", "Applying texture settings...", 0.9f);
                AssetDatabase.StartAssetEditing();
                try {
                    foreach (string texPath in pendingTextureImports) {
                        TextureImporter imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
                        if (imp != null) {
                            imp.isReadable = true;
                            imp.filterMode = FilterMode.Point;
                            imp.mipmapEnabled = false;
                            imp.SaveAndReimport();
                        }
                    }
                } finally {
                    AssetDatabase.StopAssetEditing();
                }

                EditorUtility.DisplayProgressBar("Generating Terrain Voxels", "Saving assets...", 0.95f);
                EditorUtility.SetDirty(tg);
                AssetDatabase.SaveAssets();
            } finally {
                pendingTextureImports = null;
                EditorUtility.ClearProgressBar();
            }

            string outputPath = GetPath();
            string summary = "Output path: " + outputPath + "\n\nTerrain voxels: " + generatedTerrainVoxels + "\nTree models: " + generatedTreeModels + "\nVegetation voxels: " + generatedVegetationVoxels;
            if (generationWarnings.Count > 0) {
                summary += "\n\nWarnings (" + generationWarnings.Count + "):\n" + string.Join("\n", generationWarnings);
            }
            EditorUtility.DisplayDialog("Generation Complete", summary, "OK");
        }


        string GetPath() {
            WorldDefinition wd = VoxelPlayEnvironment.instance.world;
            string path;
            string name;
            if (wd == null) {
                path = AssetDatabase.GetAssetPath(terrainData.objectReferenceValue);
                name = terrainData.name;
            } else {
                path = AssetDatabase.GetAssetPath(wd);
                name = wd.name;
            }
            return Path.GetDirectoryName(path) + "/Resources/" + name;
        }

        Texture2D CreateTextureFile(Texture2D tex, string path, string filename, float smoothPower) {
            if (smoothPower > 0) {
                TextureTools.Smooth(tex, smoothPower);
            }
            byte[] texBytes = tex.EncodeToPNG();
            string fullPath = path + "/" + filename + ".png";
            System.IO.File.WriteAllBytes(fullPath, texBytes);
            AssetDatabase.ImportAsset(fullPath);
            pendingTextureImports.Add(fullPath);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            return tex;
        }

        VoxelDefinition GenerateVoxelFromTexture(Texture2D textureTop, Texture2D textureSide, Texture2D textureDirt, int textureSize, string path, string voxelDefinitionName, RenderType renderType, float smoothPower) {
            // Prepare top texture
            Texture2D texTop = null;
            if (textureTop != null) {
                texTop = Instantiate(textureTop);
                texTop.name = textureTop.name;
                if (texTop.width != textureSize || texTop.height != textureSize) {
                    TextureTools.ScaleTexture(texTop, textureSize, textureSize, FilterMode.Bilinear);
                }
                string texName = Sanitize(texTop.name);
                texTop = CreateTextureFile(texTop, path, texName, smoothPower);
            }

            // Side texture
            Texture2D texSide = null;
            if (textureSide != null) {
                if (textureSide == textureTop) {
                    texSide = texTop;
                } else {
                    texSide = Instantiate(textureSide);
                    texSide.name = textureSide.name;
                    if (texSide.width != textureSize || texSide.height != textureSize) {
                        TextureTools.ScaleTexture(texSide, textureSize, textureSize, FilterMode.Bilinear);
                    }
                    // Save texture
                    string texSideName = Sanitize(texSide.name);
                    texSide = CreateTextureFile(texSide, path, texSideName, smoothPower);
                }
            }

            // Dirt texture
            Texture2D texDirt = null;
            if (textureDirt != null) {
                if (textureDirt == textureTop) {
                    texDirt = texTop;
                } else {
                    texDirt = Instantiate(textureDirt);
                    texDirt.name = textureDirt.name;
                    if (texDirt.width != textureSize || texDirt.height != textureSize) {
                        TextureTools.ScaleTexture(texDirt, textureSize, textureSize, FilterMode.Bilinear);
                    }
                    // Save texture
                    string texDirtName = Sanitize(texDirt.name);
                    texDirt = CreateTextureFile(texDirt, path, texDirtName, smoothPower);
                }
            }

            // Setup and save voxel definition
            VoxelDefinition vd = CreateInstance<VoxelDefinition>();
            vd.renderType = renderType;
            vd.textureTop = texTop;
            vd.textureSide = texSide;
            vd.textureBottom = texDirt;
            switch (renderType) {
                case RenderType.CutoutCross:
                    vd.navigatable = false;
                    vd.windAnimation = true;
                    break;
                case RenderType.Cutout:
                    vd.navigatable = false;
                    vd.windAnimation = true;
                    break;
                default:
                    vd.navigatable = true;
                    vd.windAnimation = false;
                    break;
            }
            vd.name = Sanitize(voxelDefinitionName);
            string fullPath = path + "/" + vd.name + ".asset";
            AssetDatabase.CreateAsset(vd, fullPath);
            return vd;
        }


        void GenerateTerrainVoxels(bool forceGeneration) {
            string path = GetPath() + "/TerrainVoxels";
            CheckDirectory(path);
            int totalLayers = Mathf.Min(tg.terrainData.terrainLayers.Length, tg.splatSettings.Length);
            for (int k = 0; k < totalLayers; k++) {
                EditorUtility.DisplayProgressBar("Generating Terrain Voxels", "Terrain texture " + (k + 1) + " / " + totalLayers, (float)k / totalLayers * 0.4f);
                if (forceGeneration || tg.splatSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Create) {
                    if (tg.splatSettings[k].preview == null)
                        continue;
                    try {
                        TextureTools.EnsureTextureReadable(tg.splatSettings[k].preview);
                        int dirtWith = Mathf.Clamp(tg.splatSettings[k].dirtWith - 1, 0, tg.splatSettings.Length - 1);
                        if (tg.splatSettings[dirtWith].preview != null) {
                            TextureTools.EnsureTextureReadable(tg.splatSettings[dirtWith].preview);
                        }
                        string texBaseName = tg.splatSettings[k].preview.name;
                        Texture2D texTop = Instantiate(tg.splatSettings[k].preview) as Texture2D;
                        texTop.name = texBaseName + " Top";

                        // Apply diffuseRemap if present
                        if (k < tg.terrainData.terrainLayers.Length) {
                            ApplyDiffuseRemap(texTop, tg.terrainData.terrainLayers[k]);
                        }

                        Texture2D texOther = tg.splatSettings[dirtWith].preview != null ? Instantiate(tg.splatSettings[dirtWith].preview) as Texture2D : Instantiate(tg.splatSettings[k].preview) as Texture2D;

                        // Apply diffuseRemap to secondary blend layer as well
                        if (dirtWith < tg.terrainData.terrainLayers.Length) {
                            ApplyDiffuseRemap(texOther, tg.terrainData.terrainLayers[dirtWith]);
                        }
                        Texture2D texDirt = CreateDirtTexture(texTop, texOther, tg.splatSettings[k].blendPower);
                        texDirt.name = texBaseName + " Dirt";
                        Texture2D texSide = CreateSideTexture(texTop, texDirt);
                        texSide.name = texBaseName + " Side";
                        tg.splatSettings[k].top = GenerateVoxelFromTexture(texTop, texSide, texDirt, terrainTextureSize, path, "VoxelTerrainTop " + texBaseName, RenderType.Opaque, tg.splatSettings[k].smoothPower);
                        tg.splatSettings[k].dirt = GenerateVoxelFromTexture(texDirt, texDirt, texDirt, terrainTextureSize, path, "VoxelTerrainDirt " + texBaseName, RenderType.Opaque, tg.splatSettings[k].smoothPower);

                        // Link biome counterparts
                        if (tg.splatSettings[k].top != null && tg.splatSettings[k].dirt != null) {
                            tg.splatSettings[k].top.biomeDirtCounterpart = tg.splatSettings[k].dirt;
                            tg.splatSettings[k].dirt.biomeSurfaceCounterpart = tg.splatSettings[k].top;
                        }

                        tg.splatSettings[k].action = UnityTerrainGenerator.TerrainResourceAction.Assigned;
                        generatedTerrainVoxels++;
                    } catch (System.Exception ex) {
                        string layerName = tg.splatSettings[k].preview != null ? tg.splatSettings[k].preview.name : "Layer " + k;
                        generationWarnings.Add("Terrain texture '" + layerName + "': " + ex.Message);
                    }
                }
            }
        }

        Texture2D CreateDirtTexture(Texture2D texTop, Texture2D texOther, float blendAmount) {
            if (texTop.width != terrainTextureSize || texTop.height != terrainTextureSize) {
                TextureTools.ScaleTexture(texTop, terrainTextureSize, terrainTextureSize);
            }
            if (texOther.width != terrainTextureSize || texOther.height != terrainTextureSize) {
                TextureTools.ScaleTexture(texOther, terrainTextureSize, terrainTextureSize);
            }
            Color32[] colorsTop = texTop.GetPixels32();
            Color32[] colorsOther = texOther.GetPixels32();
            for (int k = 0; k < colorsTop.Length; k++) {
                colorsTop[k].r = (byte)Mathf.Lerp(colorsTop[k].r, colorsOther[k].r, blendAmount);
                colorsTop[k].g = (byte)Mathf.Lerp(colorsTop[k].g, colorsOther[k].g, blendAmount);
                colorsTop[k].b = (byte)Mathf.Lerp(colorsTop[k].b, colorsOther[k].b, blendAmount);
                colorsTop[k].a = (byte)Mathf.Lerp(colorsTop[k].a, colorsOther[k].a, blendAmount);
            }
            Texture2D tex = new Texture2D(texTop.width, texTop.height, TextureFormat.ARGB32, false);
            tex.SetPixels32(colorsTop);
            tex.Apply(true);
            return tex;
        }

        void ApplyDiffuseRemap(Texture2D tex, TerrainLayer layer) {
            Vector4 remapMin = layer.diffuseRemapMin;
            Vector4 remapMax = layer.diffuseRemapMax;
            if (remapMin != Vector4.zero || remapMax != Vector4.one) {
                Color32[] pixels = tex.GetPixels32();
                for (int p = 0; p < pixels.Length; p++) {
                    pixels[p].r = (byte)(Mathf.Lerp(remapMin.x, remapMax.x, pixels[p].r / 255f) * 255);
                    pixels[p].g = (byte)(Mathf.Lerp(remapMin.y, remapMax.y, pixels[p].g / 255f) * 255);
                    pixels[p].b = (byte)(Mathf.Lerp(remapMin.z, remapMax.z, pixels[p].b / 255f) * 255);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
            }
        }

        Texture2D CreateSideTexture(Texture2D texTop, Texture2D texDirt) {
            // Make side texture
            Color32[] colors = texTop.GetPixels32();
            Color32[] colorsDirt = texDirt.GetPixels32();
            int h = texTop.height;
            int w = texTop.width;
            int y0 = (int)(h * 0.6f);
            int i = y0 * w;
            for (int y = y0; y < h; y++) {
                float threshold = Mathf.Clamp01(2f * ((float)(y - y0)) / (h - y0));
                for (int x = 0; x < w; x++, i++) {
                    if (UnityEngine.Random.value < threshold) {
                        colorsDirt[i].r = (byte)Mathf.Lerp(colorsDirt[i].r, colors[i].r, threshold);
                        colorsDirt[i].g = (byte)Mathf.Lerp(colorsDirt[i].g, colors[i].g, threshold);
                        colorsDirt[i].b = (byte)Mathf.Lerp(colorsDirt[i].b, colors[i].b, threshold);
                        colorsDirt[i].a = (byte)Mathf.Lerp(colorsDirt[i].a, colors[i].a, threshold);
                    }
                }
            }
            Texture2D sideTexture = new Texture2D(w, h, TextureFormat.ARGB32, false);
            sideTexture.SetPixels32(colorsDirt);
            sideTexture.Apply(true);
            return sideTexture;
        }


        void GenerateVegetationVoxels(bool forceGeneration) {
            if (tg.terrainData.detailPrototypes.Length == 0) return;

            string path = GetPath() + "/Vegetation";
            CheckDirectory(path);
            for (int k = 0; k < tg.terrainData.detailPrototypes.Length && k < tg.detailSettings.Length; k++) {
                if (forceGeneration || tg.detailSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Create) {
                    if (tg.detailSettings[k].preview == null)
                        continue;
                    try {
                        TextureTools.EnsureTextureReadable(tg.detailSettings[k].preview);
                        MakeTransparentTexture(tg.detailSettings[k].preview);
                        tg.detailSettings[k].vd = GenerateVoxelFromTexture(null, tg.detailSettings[k].preview, null, vegetationTextureSize, path, "VoxelVegetation " + tg.detailSettings[k].previewName, RenderType.CutoutCross, 0);
                        DetailPrototype dp = tg.terrainData.detailPrototypes[k];
                        tg.detailSettings[k].vd.vegetationMinHeight = dp.minHeight;
                        tg.detailSettings[k].vd.vegetationMaxHeight = dp.maxHeight;
                        EditorUtility.SetDirty(tg.detailSettings[k].vd);
                        tg.detailSettings[k].action = UnityTerrainGenerator.TerrainResourceAction.Assigned;
                        generatedVegetationVoxels++;
                    } catch (System.Exception ex) {
                        string vegName = tg.detailSettings[k].previewName ?? ("Detail " + k);
                        generationWarnings.Add("Vegetation '" + vegName + "': " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Assumes transparent color is first color in the color array of the texture
        /// </summary>
        void MakeTransparentTexture(Texture2D originalTex) {
            // Use RenderTexture blit to convert compressed/mismatched formats to ARGB32
            RenderTexture rt = RenderTexture.GetTemporary(originalTex.width, originalTex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(originalTex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D workTex = new Texture2D(originalTex.width, originalTex.height, TextureFormat.ARGB32, false);
            workTex.ReadPixels(new Rect(0, 0, originalTex.width, originalTex.height), 0, 0);
            workTex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Color32[] colors = workTex.GetPixels32();
            Color32 transpColor = colors[0];
            int colorsLength = colors.Length;
            for (int k = 1; k < colorsLength; k++) {
                Color32 c = colors[k];
                if (c.r == transpColor.r && c.g == transpColor.g && c.b == transpColor.b && c.a == transpColor.a) {
                    colors[k] = Misc.color32Transparent;
                }
            }
            workTex.SetPixels32(colors);
            workTex.Apply();

            // Copy back to original - resize original to ARGB32 if needed
            TextureTools.EnsureTextureReadable(originalTex);
            originalTex.Reinitialize(originalTex.width, originalTex.height, TextureFormat.ARGB32, false);
            originalTex.SetPixels32(colors);
            originalTex.Apply();
        }

        void GenerateTreeModels(bool forceGeneration) {
            if (tg.terrainData.treePrototypes.Length == 0) return;

            string path = GetPath() + "/Trees";
            CheckDirectory(path);
            List<ModelBit> bits = new List<ModelBit>();
            HashSet<int> usedVoxelIndices = new HashSet<int>();

            for (int k = 0; k < tg.terrainData.treePrototypes.Length && k < tg.treeSettings.Length; k++) {
                if (forceGeneration || tg.treeSettings[k].action == UnityTerrainGenerator.TerrainResourceAction.Create) {
                    try {
                        // Get tree size
                        GameObject o = tg.terrainData.treePrototypes[k].prefab;
                        if (o == null) {
                            generationWarnings.Add("Tree prototype " + k + ": prefab is null.");
                            continue;
                        }
                        // Look for LOD0
                        MeshRenderer[] rr = o.GetComponentsInChildren<MeshRenderer>();
                        if (rr.Length == 0) {
                            generationWarnings.Add("Tree '" + o.name + "': no MeshRenderer found.");
                            continue;
                        }
                        MeshRenderer r = rr[0];
                        for (int b = 0; b < rr.Length; b++) {
                            if (rr[b].name.Contains("LOD0")) {
                                r = rr[b];
                                break;
                            }
                        }
                        // Get bounds of renderer
                        Bounds bounds = r.bounds;
                        int sizeX = (int)(bounds.size.x * treeScale);
                        int sizeY = (int)(bounds.size.y * treeScale);
                        int sizeZ = (int)(bounds.size.z * treeScale);
                        if (sizeX == 0 || sizeY == 0 || sizeZ == 0) {
                            generationWarnings.Add("Tree '" + o.name + "': computed size is zero.");
                            continue;
                        }

                        // Build model definition
                        ModelDefinition md = CreateInstance<ModelDefinition>();
                        md.sizeX = sizeX;
                        md.sizeY = sizeY;
                        md.sizeZ = sizeZ;
                        md.fitToTerrain = true;
                        bits.Clear();
                        usedVoxelIndices.Clear();
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        Mesh mesh = mf.sharedMesh;
                        Vector3[] vertices = mesh.vertices;
                        Vector2[] uvs = mesh.uv;
                        for (int m = 0; m < mesh.subMeshCount; m++) {
                            int[] triangles = mesh.GetTriangles(m);
                            for (int i = 0; i < triangles.Length; i += 3) {
                                int i1 = triangles[i];
                                int i2 = triangles[i + 1];
                                int i3 = triangles[i + 2];
                                Vector3 v1 = vertices[i1];
                                Vector3 v2 = vertices[i2];
                                Vector3 v3 = vertices[i3];
                                Vector2 uv1 = uvs[i1];
                                Vector2 uv2 = uvs[i2];
                                Vector2 uv3 = uvs[i3];
                                AddModelBit(bits, usedVoxelIndices, md, (v1 + v2 + v3) * treeScale / 3f, (uv1 + uv2 + uv3) / 3f, r.sharedMaterials[m], path, tg.treeSettings[k].smoothPower);
                            }
                        }
                        md.SetBits(bits.ToArray());

                        string treeName = Sanitize(tg.terrainData.treePrototypes[k].prefab.name);
                        md.name = "Tree " + treeName;
                        string fullPath = path + "/" + md.name + ".asset";
                        AssetDatabase.CreateAsset(md, fullPath);
                        tg.treeSettings[k].md = md;
                        tg.treeSettings[k].action = UnityTerrainGenerator.TerrainResourceAction.Assigned;
                        generatedTreeModels++;
                    } catch (System.Exception ex) {
                        string treeName = tg.terrainData.treePrototypes[k].prefab != null ? tg.terrainData.treePrototypes[k].prefab.name : "Tree " + k;
                        generationWarnings.Add("Tree '" + treeName + "': " + ex.Message);
                    }
                }
            }
        }

        Texture2D lastFlatTex;
        Color lastFlatColor;

        void AddModelBit(List<ModelBit> bits, HashSet<int> usedVoxelIndices, ModelDefinition md, Vector3 pos, Vector2 uv, Material mat, string path, float smoothPower) {
            Texture2D tex = (Texture2D)mat.mainTexture;
            if (tex == null) {
                if (mat.color == lastFlatColor && lastFlatTex != null) {
                    tex = lastFlatTex;
                } else {
                    tex = new Texture2D(1, 1);
                    tex.name = mat.name;
                    Color32[] colors = new Color32[1];
                    colors[0] = mat.color;
                    tex.SetPixels32(colors);
                    tex.Apply();
                    lastFlatTex = tex;
                    lastFlatColor = mat.color;
                }
            }
            int y = Mathf.Clamp(Mathf.FloorToInt(pos.y), 0, md.sizeY - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(pos.z) + md.sizeZ / 2, 0, md.sizeZ - 1);
            int x = Mathf.Clamp(Mathf.FloorToInt(pos.x) + md.sizeX / 2, 0, md.sizeX - 1);
            int voxelIndex = y * md.sizeZ * md.sizeX + z * md.sizeX + x;
            if (!usedVoxelIndices.Add(voxelIndex))
                return;
            ModelBit bit = new ModelBit();
            bit.voxelIndex = voxelIndex;
            VoxelDefinition vd;
            if (textureVoxels == null) {
                textureVoxels = new Dictionary<Texture2D, VoxelDefinition>();
            }
            if (!textureVoxels.TryGetValue(tex, out vd)) {
                TextureTools.EnsureTextureReadable(tex);
                RenderType rt = RenderType.Cutout;
                string matName = mat.name.ToUpper();
                string texName = tex.name.ToUpper();
                if (matName.Contains("BRANCH") || matName.Contains("BARK") || texName.Contains("BARK") || matName.Contains("TRUNK")) {
                    rt = RenderType.Opaque;
                    vd = GenerateVoxelFromTexture(tex, tex, tex, treeTextureSize, path, "VoxelTree " + tex.name, rt, smoothPower);
                } else {
                    Texture2D frondTex = GenerateFrondTexture(tex, uv, treeTextureSize);
                    vd = GenerateVoxelFromTexture(frondTex, frondTex, frondTex, treeTextureSize, path, "VoxelTree " + frondTex.name, rt, smoothPower);
                }
                textureVoxels[tex] = vd;
            }
            bit.voxelDefinition = vd;
            bits.Add(bit);
        }

        Texture2D GenerateFrondTexture(Texture2D tex, Vector2 uv, int textureSize) {
            Color32[] sourceColors = tex.GetPixels32();
            // Extract representative colors around uv position
            int w = textureSize;
            int h = textureSize;
            int x = Mathf.Clamp((int)(w * uv.x), 0, w - 1);
            int y = Mathf.Clamp((int)(h * uv.y), 0, h - 1);
            List<Color32> repColors = new List<Color32>();
            int gap = textureSize / 2;
            for (int y0 = y - gap; y0 < y + gap; y0++) {
                int ty = Mathf.Clamp(y0, 0, h - 1);
                for (int x0 = x - gap; x0 < x + gap; x0++) {
                    int tx = Mathf.Clamp(x0, 0, w - 1);
                    int colorIndex = ty * w + tx;
                    if (colorIndex >= sourceColors.Length) colorIndex = 0;
                    repColors.Add(sourceColors[colorIndex]);
                }
            }
            Color32[] colors = repColors.ToArray();
            Color32[] newColors = new Color32[w * h];
            int i = 0;
            for (int k = 0; k < newColors.Length; k++) {
                if (UnityEngine.Random.value > frondDensity)
                    continue;
                for (int c = 0; c < colors.Length; c++) {
                    if (colors[i].a > 128)
                        break;
                    i++;
                    if (i >= colors.Length)
                        i = 0;
                }
                newColors[k] = colors[i];
                newColors[k].a = 255;
                i++;
                if (i >= colors.Length)
                    i = 0;
            }
            Texture2D subTex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            subTex.filterMode = FilterMode.Point;
            subTex.name = tex.name + " Frond";
            subTex.SetPixels32(newColors);
            subTex.Apply(true);
            return subTex;
        }

        void CheckDirectory(string path) {
            Directory.CreateDirectory(path);
        }

        string Sanitize(string s) {
            if (string.IsNullOrEmpty(s))
                return "";
            int k = s.IndexOf("(Clone)");
            if (k >= 0) {
                s = s.Substring(0, k);
            }
            for (int i = 0; i < invalidFileNameChars.Length; i++) {
                if (s.IndexOf(invalidFileNameChars[i]) >= 0) {
                    s = s.Replace(invalidFileNameChars[i], '_');
                }
            }
            return s;
        }




    }

}
