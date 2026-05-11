using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;
using UnityEngine.Rendering;
using UnityEditorInternal;


namespace VoxelPlay {

    [CustomEditor(typeof(VoxelPlayEnvironment), isFallback = true)]
    public partial class VoxelPlayEnvironmentEditor : Editor {

        SerializedProperty debugLevel, enableGeneration;
        SerializedProperty world, enableBuildMode, buildMode, welcomeMessage, welcomeMessageDuration, renderInEditor, renderInEditorDetail, editorChunkDistanceMultiplier, generateAroundCamera, renderInEditorAreaCenter, renderInEditorAreaSize;
        SerializedProperty enableConsole, showConsole, enableInventory, defaultPickupRadius, enableStatusBar, enableLoadingPanel, loadingText, initialWaitTime, initialWaitText;
        SerializedProperty loadSavedGame, saveFilename, restorePlayerPosition, enableDynamicLoad, fallbackVoxelDefinition;
        SerializedProperty enableDebugWindow, showFPS;
        SerializedProperty placementAnimation, placementAnimDuration;
        SerializedProperty placementAnimThrowArcMagnitude;
        SerializedProperty placementAnimSpin, placementAnimSpinTurns;
        SerializedProperty placementAnimFade;
        SerializedProperty placementAnimUseGhostCollider;
        SerializedProperty placementAnimElasticPopAmount;
        SerializedProperty globalIllumination, ambientLight, daylightShadowAtten, enableSmoothLighting, enableFogSkyBlending, textureSize, enableShadows, obscuranceMode, obscuranceIntensity;
        SerializedProperty shadowsOnWater, realisticWater;
        SerializedProperty enableTinting, enableColoredShadows, enableBevel, bevelWidth, enableOutline, outlineColor, outlineThreshold, enableCurvature;
        SerializedProperty seeThrough, seeThroughTarget, seeThroughRadius, seeThroughHeightOffset, seeThroughAlpha;
        SerializedProperty useOriginShift, originShiftDistanceThreshold;
        SerializedProperty enableReliefMapping, reliefStrength, reliefMaxDistance, reliefIterations, reliefIterationsBinarySearch;
        SerializedProperty enableBrightPointLights, enableURPNativeLights, brightPointsMaxDistance;
        SerializedProperty diffuseWrap;
        SerializedProperty enableNormalMap, enablePBR, usePixelLights;
        SerializedProperty hqFiltering, mipMapBias, filterMode, reflectionProbeUsage, doubleSidedGlass, transparentBling, damageParticles, useComputeBuffers, usePostProcessing, smartBiomeSurface, enableBiomeMixing, biomeMixingSpread;
        SerializedProperty maxChunks, prewarmChunksInEditor, visibleChunksDistance, visibleChunksVerticalDistance, distanceAnchor, unloadFarChunks, unloadFarChunksMode, unloadFarNavMesh;
        SerializedProperty adjustCameraFarClip, forceChunkDistance, maxCPUTimePerFrame, lowMemoryMode, delayedInitialization, onlyRenderInFrustum;
        SerializedProperty microVoxelsSnap;
#if !UNITY_WEBGL
        SerializedProperty multiThreadGeneration;
#endif
        SerializedProperty serverMode, enableColliders, enableTrees, denseTrees, enableVegetation, enableDetailGenerators, enableNavMesh, navMeshResolution, hideChunksInHierarchy;
        SerializedProperty sun, fogAmount, fogDistance, fogDistanceAuto, fogFallOff, fogTint, enableClouds;
        SerializedProperty uiCanvasPrefab, inputControllerPC, inputControllerMobile, crosshairPrefab, crosshairTexture, consoleBackgroundColor, statusBarBackgroundColor;
        SerializedProperty defaultBuildSound, defaultPickupSound, defaultImpactSound, defaultDestructionSound, defaultVoxel, defaultWaterVoxel;
        SerializedProperty layerParticles, layerVoxels, layerClouds, particlePoolSize;
        SerializedProperty previewTouchUIinEditor;
        SerializedProperty instancingCullingMode, instancingCullingPadding;
        SerializedProperty enableFarChunksRendering, farChunksShadows, farChunksShadowIntensity, farChunksWaterColorOverride, farChunksWaterColor, farChunksShoreColor, farChunksWaterReflections, farChunksWaterReflectionsIntensity, farChunksDeepWater;
        SerializedProperty sceneEditorAutomaticBackup;

        const string VP_SECTION_WORLD_SETTINGS = "VoxelPlayWorldSection";
        const string VP_SECTION_TERRAIN_SETTINGS = "VoxelPlayTerrainGeneratorSection";
        const string VP_SECTION_SCENE_EDITOR = "VoxelPlaySceneEditorSection";
        const string VP_SECTION_QUALITY = "VoxelPlayExpandQualitySection";
        const string VP_SECTION_RENDERING = "VoxelPlayExpandRenderingSection";
        const string VP_SECTION_STATS = "VoxelPlayVoxelStatsSection";
        const string VP_SECTION_GENERATION = "VoxelPlayVoxelGenerationSection";
        const string VP_SECTION_SKY = "VoxelPlaySkySection";
        const string VP_SECTION_SAVELOAD = "VoxelPlaySaveLoadSection";
        const string VP_SECTION_GAME_FEATURES = "VoxelPlayInGameSection";
        const string VP_SECTION_DEFAULTS = "VoxelPlayDefaultsSection";
        const string VP_SECTION_ADVANCED = "VoxelPlayAdvancedSection";

        const string WORLD_EDITOR_ENABLED_LABEL = " World Editor (ON)";
        const string WORLD_EDITOR_ENABLED_LABEL_UNSAVED = " World Editor (ON) (Unsaved Changes)";
        const string WORLD_EDITOR_LABEL = " World Editor";

        VoxelPlayEnvironment env;
        WorldDefinition cachedWorld;
        VoxelPlayTerrainGenerator cachedTerrainGenerator;
        Editor cachedWorldEditor, cachedTerrainGeneratorEditor;
        static GUIStyle titleLabelStyle, boxStyle;
        static int cookieIndex = -1;
        Color titleColor;
        static GUIStyle sectionHeaderStyle;
        static bool expandWorldSettings, expandTerrainGeneratorSettings, expandSceneEditor;
        static bool expandQualitySection, expandRenderingSection, expandStatsSection, expandVoxelGenerationSection, expandSkySection, expandSaveLoadSection, expandInGameSection, expandDefaultsSection, expandAdvancedSection;
        bool enableCurvatureFromShader;
        string[] chunkSizeOptions;
        int[] chunkSizeValues;
        int chunkNewSize;
        string[] microVoxelsSizeOptions;
        int[] microVoxelsSizeValues;
        int microVoxelsNewSize;
        string curvatureAmount;
        bool voxelPadding;
        int maxMaterialsPerChunk;

        [MenuItem("Assets/Create/Voxel Play/Online Documentation", false, 2001)]
        public static void ShowDocs () {
            Application.OpenURL("https://kronnect.com/docs/voxel-play/");
        }

        [MenuItem("Assets/Create/Voxel Play/Tutorials", false, 2002)]
        public static void ShowTutorials () {
            Application.OpenURL("https://youtube.com/playlist?list=PLqzCcLYG3btgt5wsMqTf7ANjrwacdjfr8");
        }

        [MenuItem("Assets/Create/Voxel Play/Support Forum", false, 2003)]
        public static void ShowSupport () {
            Application.OpenURL("https://kronnect.com/support");
        }


        public virtual void OnEnable () {
            titleColor = EditorGUIUtility.isProSkin ? new Color(0.52f, 0.66f, 0.9f) : new Color(0.12f, 0.16f, 0.4f);
            debugLevel = serializedObject.FindProperty("debugLevel");
            world = serializedObject.FindProperty("world");
            enableGeneration = serializedObject.FindProperty("enableGeneration");
            placementAnimation = serializedObject.FindProperty("placementAnimation");
            placementAnimDuration = serializedObject.FindProperty("placementAnimDuration");
            placementAnimThrowArcMagnitude = serializedObject.FindProperty("placementAnimThrowArcMagnitude");
            placementAnimSpin = serializedObject.FindProperty("placementAnimSpin");
            placementAnimSpinTurns = serializedObject.FindProperty("placementAnimSpinTurns");
            placementAnimElasticPopAmount = serializedObject.FindProperty("placementAnimElasticPopAmount");
            placementAnimFade = serializedObject.FindProperty("placementAnimFade");
            placementAnimUseGhostCollider = serializedObject.FindProperty("placementAnimUseGhostCollider");
            enableBuildMode = serializedObject.FindProperty("enableBuildMode");
            buildMode = serializedObject.FindProperty("buildMode");
            welcomeMessage = serializedObject.FindProperty("welcomeMessage");
            welcomeMessageDuration = serializedObject.FindProperty("welcomeMessageDuration");
            renderInEditor = serializedObject.FindProperty("renderInEditor");
            generateAroundCamera = serializedObject.FindProperty("generateAroundCamera");
            renderInEditorDetail = serializedObject.FindProperty("renderInEditorDetail");
            editorChunkDistanceMultiplier = serializedObject.FindProperty("editorChunkDistanceMultiplier");
            renderInEditorAreaCenter = serializedObject.FindProperty("renderInEditorAreaCenter");
            renderInEditorAreaSize = serializedObject.FindProperty("renderInEditorAreaSize");
            enableConsole = serializedObject.FindProperty("enableConsole");
            consoleBackgroundColor = serializedObject.FindProperty("consoleBackgroundColor");
            showConsole = serializedObject.FindProperty("showConsole");
            enableInventory = serializedObject.FindProperty("enableInventory");
            defaultPickupRadius = serializedObject.FindProperty("defaultPickupRadius");
            prewarmChunksInEditor = serializedObject.FindProperty("prewarmChunksInEditor");
            smartBiomeSurface = serializedObject.FindProperty("smartBiomeSurface");
            enableBiomeMixing = serializedObject.FindProperty("enableBiomeMixing");
            biomeMixingSpread = serializedObject.FindProperty("biomeMixingSpread");
            enableLoadingPanel = serializedObject.FindProperty("enableLoadingPanel");
            loadingText = serializedObject.FindProperty("loadingText");
            initialWaitTime = serializedObject.FindProperty("initialWaitTime");
            initialWaitText = serializedObject.FindProperty("initialWaitText");
            loadSavedGame = serializedObject.FindProperty("loadSavedGame");
            saveFilename = serializedObject.FindProperty("saveFilename");
            restorePlayerPosition = serializedObject.FindProperty("restorePlayerPosition");
            enableDynamicLoad = serializedObject.FindProperty("enableDynamicLoad");
            fallbackVoxelDefinition = serializedObject.FindProperty("fallbackVoxelDefinition");
            enableDebugWindow = serializedObject.FindProperty("enableDebugWindow");
            showFPS = serializedObject.FindProperty("showFPS");
            diffuseWrap = serializedObject.FindProperty("diffuseWrap");

            globalIllumination = serializedObject.FindProperty("globalIllumination");
            ambientLight = serializedObject.FindProperty("ambientLight");
            daylightShadowAtten = serializedObject.FindProperty("daylightShadowAtten");
            enableSmoothLighting = serializedObject.FindProperty("enableSmoothLighting");
            obscuranceMode = serializedObject.FindProperty("obscuranceMode");
            obscuranceIntensity = serializedObject.FindProperty("obscuranceIntensity");

            enableReliefMapping = serializedObject.FindProperty("enableReliefMapping");
            reliefStrength = serializedObject.FindProperty("reliefStrength");
            reliefMaxDistance = serializedObject.FindProperty("reliefMaxDistance");
            reliefIterations = serializedObject.FindProperty("reliefIterations");
            reliefIterationsBinarySearch = serializedObject.FindProperty("reliefIterationsBinarySearch");

            enableNormalMap = serializedObject.FindProperty("enableNormalMap");
            enablePBR = serializedObject.FindProperty("enablePBR");
            usePixelLights = serializedObject.FindProperty("usePixelLights");
            enableBevel = serializedObject.FindProperty("enableBevel");
            bevelWidth = serializedObject.FindProperty("bevelWidth");

            enableBrightPointLights = serializedObject.FindProperty("enableBrightPointLights");
            enableURPNativeLights = serializedObject.FindProperty("enableURPNativeLights");
            brightPointsMaxDistance = serializedObject.FindProperty("brightPointsMaxDistance");

            enableFogSkyBlending = serializedObject.FindProperty("enableFogSkyBlending");
            textureSize = serializedObject.FindProperty("textureSize");
            realisticWater = serializedObject.FindProperty("realisticWater");
            shadowsOnWater = serializedObject.FindProperty("shadowsOnWater");
            enableShadows = serializedObject.FindProperty("enableShadows");
            enableTinting = serializedObject.FindProperty("enableTinting");
            enableColoredShadows = serializedObject.FindProperty("enableColoredShadows");
            enableCurvature = serializedObject.FindProperty("enableCurvature");
            enableOutline = serializedObject.FindProperty("enableOutline");
            outlineColor = serializedObject.FindProperty("outlineColor");
            outlineThreshold = serializedObject.FindProperty("outlineThreshold");
            doubleSidedGlass = serializedObject.FindProperty("doubleSidedGlass");
            transparentBling = serializedObject.FindProperty("transparentBling");
            damageParticles = serializedObject.FindProperty("damageParticles");
            hqFiltering = serializedObject.FindProperty("hqFiltering");
            mipMapBias = serializedObject.FindProperty("mipMapBias");
            filterMode = serializedObject.FindProperty("filterMode");
            reflectionProbeUsage = serializedObject.FindProperty("reflectionProbeUsage");
            useComputeBuffers = serializedObject.FindProperty("useComputeBuffers");
            usePostProcessing = serializedObject.FindProperty("usePostProcessing");

            seeThrough = serializedObject.FindProperty("seeThrough");
            seeThroughTarget = serializedObject.FindProperty("seeThroughTarget");
            seeThroughRadius = serializedObject.FindProperty("seeThroughRadius");
            seeThroughHeightOffset = serializedObject.FindProperty("_seeThroughHeightOffset");
            seeThroughAlpha = serializedObject.FindProperty("seeThroughAlpha");

            useOriginShift = serializedObject.FindProperty("useOriginShift");
            originShiftDistanceThreshold = serializedObject.FindProperty("originShiftDistanceThreshold");

            maxChunks = serializedObject.FindProperty("maxChunks");
            visibleChunksDistance = serializedObject.FindProperty("_visibleChunksDistance");
            visibleChunksVerticalDistance = serializedObject.FindProperty("visibleChunksVerticalDistance");
            distanceAnchor = serializedObject.FindProperty("distanceAnchor");
            unloadFarChunks = serializedObject.FindProperty("unloadFarChunks");
            unloadFarChunksMode = serializedObject.FindProperty("unloadFarChunksMode");
            unloadFarNavMesh = serializedObject.FindProperty("unloadFarNavMesh");
            adjustCameraFarClip = serializedObject.FindProperty("adjustCameraFarClip");

            forceChunkDistance = serializedObject.FindProperty("forceChunkDistance");
            maxCPUTimePerFrame = serializedObject.FindProperty("maxCPUTimePerFrame");
#if !UNITY_WEBGL
            multiThreadGeneration = serializedObject.FindProperty("multiThreadGeneration");
#endif
            lowMemoryMode = serializedObject.FindProperty("lowMemoryMode");
            delayedInitialization = serializedObject.FindProperty("delayedInitialization");
            onlyRenderInFrustum = serializedObject.FindProperty("onlyRenderInFrustum");
            serverMode = serializedObject.FindProperty("serverMode");
            enableColliders = serializedObject.FindProperty("enableColliders");
            enableNavMesh = serializedObject.FindProperty("enableNavMesh");
            navMeshResolution = serializedObject.FindProperty("navMeshResolution");
            hideChunksInHierarchy = serializedObject.FindProperty("hideChunksInHierarchy");
            enableTrees = serializedObject.FindProperty("enableTrees");
            denseTrees = serializedObject.FindProperty("denseTrees");
            enableVegetation = serializedObject.FindProperty("enableVegetation");
            enableDetailGenerators = serializedObject.FindProperty("enableDetailGenerators");

            microVoxelsSnap = serializedObject.FindProperty("microVoxelsSnap");

            sun = serializedObject.FindProperty("sun");
            fogAmount = serializedObject.FindProperty("fogAmount");
            fogDistance = serializedObject.FindProperty("fogDistance");
            fogDistanceAuto = serializedObject.FindProperty("fogDistanceAuto");
            fogFallOff = serializedObject.FindProperty("fogFallOff");
            fogTint = serializedObject.FindProperty("fogTint");

            enableClouds = serializedObject.FindProperty("enableClouds");

            uiCanvasPrefab = serializedObject.FindProperty("UICanvasPrefab");
            inputControllerPC = serializedObject.FindProperty("inputControllerPCPrefab");
            inputControllerMobile = serializedObject.FindProperty("inputControllerMobilePrefab");
            crosshairPrefab = serializedObject.FindProperty("crosshairPrefab");
            crosshairTexture = serializedObject.FindProperty("crosshairTexture");

            enableStatusBar = serializedObject.FindProperty("enableStatusBar");
            statusBarBackgroundColor = serializedObject.FindProperty("statusBarBackgroundColor");

            layerParticles = serializedObject.FindProperty("layerParticles");
            particlePoolSize = serializedObject.FindProperty("particlePoolSize");
            layerVoxels = serializedObject.FindProperty("layerVoxels");
            layerClouds = serializedObject.FindProperty("layerClouds");

            defaultBuildSound = serializedObject.FindProperty("defaultBuildSound");
            defaultPickupSound = serializedObject.FindProperty("defaultPickupSound");
            defaultImpactSound = serializedObject.FindProperty("defaultImpactSound");
            defaultDestructionSound = serializedObject.FindProperty("defaultDestructionSound");
            defaultVoxel = serializedObject.FindProperty("defaultVoxel");
            defaultWaterVoxel = serializedObject.FindProperty("defaultWaterVoxel");

            enableFarChunksRendering = serializedObject.FindProperty("enableFarChunksRendering");
            farChunksShadows = serializedObject.FindProperty("farChunksShadows");
            farChunksShadowIntensity = serializedObject.FindProperty("farChunksShadowIntensity");
            farChunksWaterReflections = serializedObject.FindProperty("farChunksWaterReflections");
            farChunksWaterReflectionsIntensity = serializedObject.FindProperty("farChunksWaterReflectionsIntensity");
            farChunksWaterColorOverride = serializedObject.FindProperty("farChunksWaterColorOverride");
            farChunksWaterColor = serializedObject.FindProperty("farChunksWaterColor");
            farChunksShoreColor = serializedObject.FindProperty("farChunksShoreColor");
            farChunksDeepWater = serializedObject.FindProperty("farChunksDeepWater");

            sceneEditorAutomaticBackup = serializedObject.FindProperty("sceneEditorAutomaticBackup");

            env = (VoxelPlayEnvironment)target;
            if (!Application.isPlaying) {
                if (!env.initialized && env.gameObject.activeInHierarchy && env.world != null) {
                    env.InitAndLoadSaveGame();
                }
                env.WantRepaintInspector += Repaint;
            }

            expandWorldSettings = EditorPrefs.GetBool(VP_SECTION_WORLD_SETTINGS, expandWorldSettings);
            expandTerrainGeneratorSettings = EditorPrefs.GetBool(VP_SECTION_TERRAIN_SETTINGS, expandTerrainGeneratorSettings);
            expandSceneEditor = EditorPrefs.GetBool(VP_SECTION_SCENE_EDITOR, expandSceneEditor);
            expandQualitySection = EditorPrefs.GetBool(VP_SECTION_QUALITY, false);
            expandRenderingSection = EditorPrefs.GetBool(VP_SECTION_RENDERING, false);
            expandStatsSection = EditorPrefs.GetBool(VP_SECTION_STATS, false);
            expandVoxelGenerationSection = EditorPrefs.GetBool(VP_SECTION_GENERATION, false);
            expandSkySection = EditorPrefs.GetBool(VP_SECTION_SKY, false);
            expandSaveLoadSection = EditorPrefs.GetBool(VP_SECTION_SAVELOAD, false);
            expandInGameSection = EditorPrefs.GetBool(VP_SECTION_GAME_FEATURES, false);
            expandDefaultsSection = EditorPrefs.GetBool(VP_SECTION_DEFAULTS, false);
            expandAdvancedSection = EditorPrefs.GetBool(VP_SECTION_ADVANCED, true);

            enableCurvatureFromShader = "1".Equals(GetShaderOptionValue("VOXELPLAY_CURVATURE", "VPCommonVertexModifier.cginc"));
            curvatureAmount = GetShaderOptionValue("VOXELPLAY_CURVATURE_AMOUNT", "VPCommonVertexModifier.cginc");

            previewTouchUIinEditor = serializedObject.FindProperty("previewTouchUIinEditor");

            instancingCullingMode = serializedObject.FindProperty("instancingCullingMode");
            instancingCullingPadding = serializedObject.FindProperty("instancingCullingPadding");

            chunkSizeOptions = new string[] { "16", "32" };
            chunkSizeValues = new int[] { 16, 32 };
            chunkNewSize = VoxelPlayEnvironment.CHUNK_SIZE;

            microVoxelsSizeOptions = new string[] { "2", "4", "8", "16" };
            microVoxelsSizeValues = new int[] { 2, 4, 8, 16 };
            microVoxelsNewSize = MicroVoxels.COUNT_PER_AXIS;

            maxMaterialsPerChunk = VoxelPlayEnvironment.MAX_MATERIALS_PER_CHUNK;
            voxelPadding = VoxelPlayGreedyCommon.PADDING != 0;

            WorldEditorInit();
        }

        void OnDisable () {
            if (env != null) {
                env.VoxelHighlight(false);
                env.WantRepaintInspector -= this.Repaint;
            }

            WorldEditorDispose();

            EditorPrefs.SetBool(VP_SECTION_WORLD_SETTINGS, expandWorldSettings);
            EditorPrefs.SetBool(VP_SECTION_TERRAIN_SETTINGS, expandTerrainGeneratorSettings);
            EditorPrefs.SetBool(VP_SECTION_SCENE_EDITOR, expandSceneEditor);

            EditorPrefs.SetBool(VP_SECTION_QUALITY, expandQualitySection);
            EditorPrefs.SetBool(VP_SECTION_RENDERING, expandRenderingSection);
            EditorPrefs.SetBool(VP_SECTION_STATS, expandStatsSection);
            EditorPrefs.SetBool(VP_SECTION_GENERATION, expandVoxelGenerationSection);
            EditorPrefs.SetBool(VP_SECTION_SKY, expandSkySection);
            EditorPrefs.SetBool(VP_SECTION_SAVELOAD, expandSaveLoadSection);
            EditorPrefs.SetBool(VP_SECTION_GAME_FEATURES, expandInGameSection);
            EditorPrefs.SetBool(VP_SECTION_DEFAULTS, expandDefaultsSection);
            EditorPrefs.SetBool(VP_SECTION_ADVANCED, expandAdvancedSection);
        }

        void CollapseAllSections () {
            expandWorldSettings = false;
            expandTerrainGeneratorSettings = false;
            expandSceneEditor = false;
            expandQualitySection = false;
            expandRenderingSection = false;
            expandStatsSection = false;
            expandVoxelGenerationSection = false;
            expandSkySection = false;
            expandInGameSection = false;
            expandDefaultsSection = false;
        }

        void ToggleSection (ref bool section) {
            var state = !section;
            CollapseAllSections();
            section = state;
        }

        public override void OnInspectorGUI () {
            serializedObject.UpdateIfRequiredOrScript();
            if (boxStyle == null) {
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.padding = new RectOffset(15, 10, 5, 5);
            }
            if (titleLabelStyle == null) {
                titleLabelStyle = new GUIStyle(EditorStyles.label);
            }
            titleLabelStyle.normal.textColor = titleColor;
            titleLabelStyle.fontStyle = FontStyle.Bold;
            if (sectionHeaderStyle == null) {
                sectionHeaderStyle = new GUIStyle(EditorStyles.foldout);
            }
            sectionHeaderStyle.SetFoldoutColor();

            if (cookieIndex >= 0) {
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Help & Tutorials", titleLabelStyle);
                EditorGUILayout.HelpBox("To learn more about a property in this inspector move the mouse over the label for a quick description (tooltip).", MessageType.Info);
                if (GUILayout.Button("Online Documentation")) {
                    Application.OpenURL("https://kronnect.com/docs/voxel-play/");
                }
                if (GUILayout.Button("Tutorials")) {
                    Application.OpenURL("https://youtube.com/playlist?list=PLqzCcLYG3btgt5wsMqTf7ANjrwacdjfr8");
                }
                if (GUILayout.Button("Support Forum")) {
                    Application.OpenURL("https://kronnect.com/support");

                }
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Random Tip", titleLabelStyle);
                EditorGUILayout.HelpBox(VoxelPlayCookie.GetCookie(cookieIndex), MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("  ");
                ShowHelpButtons(true);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("General Settings", titleLabelStyle);
            if (cookieIndex < 0)
                ShowHelpButtons(false);
            EditorGUILayout.EndHorizontal();

            bool rebuildWorld = false;
            bool refreshChunks = false;
            bool reloadWorldTextures = false;
            bool updateSpecialFeaturesMacro = false;
            bool updateCurvatureMacro = false;

            // General settings
            bool isURPActive = GraphicsSettings.currentRenderPipeline != null;
            if (isURPActive != VoxelPlayEnvironment.supportsURP) {
                refreshChunks = true;
                updateSpecialFeaturesMacro = true;
            }

            // Use reflection to handle URP asset safely without direct type reference
            bool showURPDepthWarning = false;
            bool showURPMSAAWarning = false;
            bool showURPDeferredWarning = false;
            if (GraphicsSettings.currentRenderPipeline != null) {
                Type urpAssetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime", false);
                if (urpAssetType != null) {
                    object pipe = GraphicsSettings.currentRenderPipeline;
                    if (urpAssetType.IsInstanceOfType(pipe)) {
                        // Check if depth texture is supported
                        PropertyInfo depthTextureProperty = urpAssetType.GetProperty("supportsCameraDepthTexture");
                        if (depthTextureProperty != null) {
                            bool supportsDepthTexture = (bool)depthTextureProperty.GetValue(pipe);
                            if (!supportsDepthTexture) {
                                showURPDepthWarning = true;
                            }
                        }

                        // Check MSAA setting when realistic water is enabled
                        if (realisticWater.boolValue) {
                            PropertyInfo msaaSampleCountProperty = urpAssetType.GetProperty("msaaSampleCount");
                            if (msaaSampleCountProperty != null) {
                                int msaaSampleCount = (int)msaaSampleCountProperty.GetValue(pipe);
                                if (msaaSampleCount > 1) {
                                    showURPMSAAWarning = true;
                                }
                            }
                        }

                        // Check if renderer is set to Deferred/Deferred+ (not supported)
                        try {
                            int defaultIndex = 0;
                            FieldInfo defaultIndexField = urpAssetType.GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (defaultIndexField != null) {
                                defaultIndex = (int)defaultIndexField.GetValue(pipe);
                            }

                            FieldInfo rdlField = urpAssetType.GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (rdlField != null) {
                                Array rendererDataList = rdlField.GetValue(pipe) as Array;
                                if (rendererDataList != null && rendererDataList.Length > 0) {
                                    if (defaultIndex < 0 || defaultIndex >= rendererDataList.Length) defaultIndex = 0;
                                    object rendererData = rendererDataList.GetValue(defaultIndex);
                                    Type urdType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime", false);
                                    if (rendererData != null && urdType != null && urdType.IsInstanceOfType(rendererData)) {
                                        PropertyInfo usesDeferredLightingProp = urdType.GetProperty("usesDeferredLighting", BindingFlags.Instance | BindingFlags.Public);
                                        if (usesDeferredLightingProp != null) {
                                            bool usesDeferredLighting = (bool)usesDeferredLightingProp.GetValue(rendererData);
                                            if (usesDeferredLighting) {
                                                showURPDeferredWarning = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            if (showURPDepthWarning) {
                EditorGUILayout.HelpBox("Depth Texture option is required in Universal Rendering Pipeline asset!", MessageType.Error);
                if (GUILayout.Button("Go to Universal Rendering Pipeline Asset")) {
                    Selection.activeObject = GraphicsSettings.currentRenderPipeline;
                }
                EditorGUILayout.Separator();
                GUI.enabled = false;
            }

            if (showURPMSAAWarning) {
                EditorGUILayout.HelpBox("MSAA should be turned off when using realistic water option. Disable MSAA in URP asset.", MessageType.Warning);
                if (GUILayout.Button("Go to Universal Rendering Pipeline Asset")) {
                    Selection.activeObject = GraphicsSettings.currentRenderPipeline;
                }
                EditorGUILayout.Separator();
            }

            if (showURPDeferredWarning) {
                EditorGUILayout.HelpBox("URP Deferred rendering path is not supported by Voxel Play. Set the URP Renderer to Forward or Forward+.", MessageType.Error);
                if (GUILayout.Button("Go to Universal Rendering Pipeline Asset")) {
                    Selection.activeObject = GraphicsSettings.currentRenderPipeline;
                }
                EditorGUILayout.Separator();
            }

            CheckDepthPrimingMode();

            EditorGUILayout.BeginHorizontal();
            WorldDefinition wd = (WorldDefinition)world.objectReferenceValue;
            EditorGUILayout.PropertyField(world, new GUIContent("World", "The world definition asset. This asset contains the definition of biomes, voxels, items and other world-specific options."));
            if (wd != world.objectReferenceValue)
                rebuildWorld = true;
            if (GUILayout.Button("Create", GUILayout.Width(50))) {
                if (EditorUtility.DisplayDialog("Create World Definition", "Are you sure you want to create a new world definition? (this will create a new folder with the new world definition, terrain generator and a default biome?)", "Yes", "No")) {
                    CreateWorldDefinition();
                    GUIUtility.ExitGUI();
                    return;
                }
            }
            if (GUILayout.Button("Locate", GUILayout.Width(50))) {
                Selection.activeObject = world.objectReferenceValue;
            }
            EditorGUILayout.EndHorizontal();
            if (world.objectReferenceValue == null) {
                EditorGUILayout.HelpBox("Create or assign a World Definition asset.", MessageType.Warning);
            }

            GUIStyle leftAlignStyle = new GUIStyle(GUI.skin.button);
            leftAlignStyle.alignment = TextAnchor.MiddleLeft;
            leftAlignStyle.fixedHeight = leftAlignStyle.lineHeight + 6;

            if (world.objectReferenceValue != null) {
                if (GUILayout.Button(new GUIContent(" World Settings", Resources.Load("VoxelPlay/Icons/worldIcon") as Texture2D), leftAlignStyle)) {
                    ToggleSection(ref expandWorldSettings);
                }
                if (expandWorldSettings) {
                    if (cachedWorld != world.objectReferenceValue) {
                        cachedWorldEditor = null;
                    }
                    if (cachedWorldEditor == null) {
                        cachedWorld = (WorldDefinition)world.objectReferenceValue;
                        cachedWorldEditor = CreateEditor(world.objectReferenceValue);
                    }

                    // Drawing the world editor
                    EditorGUILayout.BeginVertical(boxStyle);
                    EditorGUI.BeginChangeCheck();
                    cachedWorldEditor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck()) {
                        env.UpdateMaterialProperties();
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Separator();
                }

                VoxelPlayTerrainGenerator terrainGenerator = (VoxelPlayTerrainGenerator)((WorldDefinition)world.objectReferenceValue).terrainGenerator;
                if (terrainGenerator != null) {
                    if (GUILayout.Button(new GUIContent(" Terrain Settings", Resources.Load("VoxelPlay/Icons/mountainsIcon") as Texture2D), leftAlignStyle)) {
                        expandTerrainGeneratorSettings = !expandTerrainGeneratorSettings;
                    }
                    if (expandTerrainGeneratorSettings) {
                        if (terrainGenerator != cachedTerrainGenerator) {
                            cachedTerrainGeneratorEditor = null;
                        }
                        if (cachedTerrainGeneratorEditor == null) {
                            cachedTerrainGenerator = terrainGenerator;
                            cachedTerrainGeneratorEditor = CreateEditor(terrainGenerator);
                        }

                        // Drawing the world editor
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.BeginVertical(boxStyle);
                        cachedTerrainGeneratorEditor.OnInspectorGUI();
                        EditorGUILayout.EndVertical();
                        if (EditorGUI.EndChangeCheck()) {
                            env.NotifyTerrainGeneratorConfigurationChanged();
                            VoxelPlayBiomeExplorer.requestRefresh = true;
                            InternalEditorUtility.RepaintAllViews();
                        }
                        EditorGUILayout.Separator();
                    }
                }

                string worldEditorHeaderLabel = WORLD_EDITOR_LABEL;
                if (renderInEditor.boolValue) {
                    if (pendingChanges) {
                        worldEditorHeaderLabel = WORLD_EDITOR_ENABLED_LABEL_UNSAVED;
                    } else {
                        worldEditorHeaderLabel = WORLD_EDITOR_ENABLED_LABEL;
                    }
                }
                if (GUILayout.Button(new GUIContent(worldEditorHeaderLabel, Resources.Load("VoxelPlay/Inspector/sceneViewEditor") as Texture2D), leftAlignStyle)) {
                    ToggleSection(ref expandSceneEditor);
                }
                if (expandSceneEditor) {

                    EditorGUILayout.BeginVertical(boxStyle);

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(renderInEditor, new GUIContent("Render In Editor", "Enable world rendering in Editor. If disabled, world will only be visible during play mode."));
                    if (EditorGUI.EndChangeCheck()) {
                        env.NotifyCameraMove();
                        if (!renderInEditor.boolValue && !Application.isPlaying) {
                            if (EditorUtility.DisplayDialog("Render In Editor", "Remove voxels from scene?", "Yes", "No")) {
                                env.DisposeAll();
                            }
                        }
                    }

                    if (!renderInEditor.boolValue) GUI.enabled = false;

                    EditorGUILayout.PropertyField(generateAroundCamera, new GUIContent("   Automatic", "Render the world around SceneView camera position as it moves."));

                    if (wd != world.objectReferenceValue) {
                        rebuildWorld = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(renderInEditorDetail, new GUIContent("   Render Detail", "Select the amount of detail to be rendered in Editor time."));
                    if (EditorGUI.EndChangeCheck()) {
                        rebuildWorld = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(editorChunkDistanceMultiplier, new GUIContent("   Distance Multiplier", "Multiplier for visible chunk distances when editing in Scene View. Higher values let you see further but use more memory. Default is 2."));
                    if (EditorGUI.EndChangeCheck()) {
                        rebuildWorld = true;
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.BeginVertical(boxStyle);

                    EditorGUI.BeginChangeCheck();
                    GUIContent[] options = new GUIContent[] { new GUIContent("File", "Load/Save/Export operations."), new GUIContent("Structs", "Tools to create simple structures."), new GUIContent("Terrain", "Tools to edit/customize the terrain."), new GUIContent("Paint", "Tools to place/remove/edit voxels in the world."), new GUIContent("Other", "Other world management tools.") };
                    GUIStyle style = new GUIStyle(GUI.skin.button);
                    style.fontStyle = FontStyle.Bold;
                    env.worldManagementSelectedTool = GUILayout.SelectionGrid(env.worldManagementSelectedTool, options, 5, style);
                    if (EditorGUI.EndChangeCheck()) {
                        selectedToolType = null;
                        // Refresh modified chunks list when File tab is selected
                        if (env.worldManagementSelectedTool == 0 && lastSelectedToolTab != 0) {
                            RefreshModifiedChunksList();
                        }
                        lastSelectedToolTab = env.worldManagementSelectedTool;
                    } else if (lastSelectedToolTab != env.worldManagementSelectedTool && env.worldManagementSelectedTool == 0) {
                        // Also refresh if File tab is already selected but wasn't tracked
                        RefreshModifiedChunksList();
                        lastSelectedToolTab = env.worldManagementSelectedTool;
                    }
                    GUI.enabled = true;

                    if (renderInEditor.boolValue) {

                        switch (env.worldManagementSelectedTool) {
                            case 0:
                                float half = EditorGUIUtility.currentViewWidth * 0.42f;
                                EditorGUILayout.BeginHorizontal();
                                if (GUILayout.Button("Toggle Chunks", GUILayout.Width(half))) {
                                    env.ChunksToggle();
                                    SceneView.RepaintAll();
                                }
                                if (GUILayout.Button("Delete Chunks", GUILayout.Width(half))) {
                                    renderInEditor.boolValue = false;
                                    WorldEditorDispose();
                                    env.DisposeAll();
                                }
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.BeginHorizontal();
                                if (GUILayout.Button("Reset World", GUILayout.Width(half))) {
                                    if (EditorUtility.DisplayDialog("Reset World", "This option will discard any modified chunk and create it again from the terrain generator. Continue?", "Yes", "No")) {
                                        renderInEditor.boolValue = true;
                                        serializedObject.ApplyModifiedProperties();
                                        WorldEditorDispose();
                                        env.ReloadWorld(keepWorldChanges: false);
                                        GUIUtility.ExitGUI();
                                        return;
                                    }
                                }
                                GUI.enabled = env.chunksCreated > 0;
                                if (GUILayout.Button("Export Chunks", GUILayout.Width(half))) {
                                    if (EditorUtility.DisplayDialog("Export Chunks", "Do you want to make chunks permanent in the scene and remove Voxel Play Environment manager?", "Ok", "Cancel")) {
                                        env.ChunksExport();
                                        EditorUtility.DisplayDialog("Export Chunks", "Chunks now available under 'Exported Chunks' node in hierarchy as regular gameobjects. Materials, textures and meshes are now part of the scene.\n\nThe 'ExportGlobalSettings' behaviour has been attached to 'Exported Chunks' root gameobject to keep global shader values.\nVoxel Play Environment has been removed.", "Ok");
                                        GUIUtility.ExitGUI();
                                        return;
                                    }
                                }
                                GUI.enabled = true;
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.BeginHorizontal();
                                if (GUILayout.Button("Load", GUILayout.Width(half))) {
                                    if (EditorUtility.DisplayDialog("Load World", "Discard any change and reload the entire world?", "Yes", "No")) {
                                        renderInEditor.boolValue = true;
                                        serializedObject.ApplyModifiedProperties();
                                        env.LoadGameBinary(true);
                                        GUIUtility.ExitGUI();
                                        return;
                                    }
                                }
                                GUIStyle saveButtonStyle = new GUIStyle(GUI.skin.button);
                                if (pendingChanges) {
                                    saveButtonStyle.normal.textColor = Color.yellow;
                                    saveButtonStyle.fontStyle = FontStyle.Bold;
                                }
                                if (GUILayout.Button("Save", saveButtonStyle, GUILayout.Width(half))) {
                                    if (EditorUtility.DisplayDialog("Save World?", "Do you want to save the current world to " + env.saveFilename + "?", "Yes", "No")) {
                                        SaveWorldInEditor();
                                    }
                                }
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.PropertyField(saveFilename, new GUIContent("Current File", "The name for the current saved world file."));
                                EditorGUILayout.PropertyField(sceneEditorAutomaticBackup, new GUIContent("Automatic Backup", "When enabled, a backup will be created before updating the savegame files."));
                                
                                EditorGUILayout.Space();
                                DrawModifiedChunksSection();
                                break;

                            case 1:
                                DrawTools(WorldEditorToolCategory.StructTool);
                                break;

                            case 2:
                                DrawTools(WorldEditorToolCategory.TerrainTool);
                                break;

                            case 3:
                                DrawTools(WorldEditorToolCategory.SculptTool);
                                break;

                            case 4:
                                if (env.cameraMain != null) {
                                    EditorGUILayout.LabelField("Main Cam Position", env.cameraMain.transform.position.ToString());
                                    EditorGUILayout.BeginHorizontal();
                                    env.sceneEditorCameraMainPosition = EditorGUILayout.Vector3Field("   New Position", env.sceneEditorCameraMainPosition);
                                    if (GUILayout.Button("Set", GUILayout.Width(40))) {
                                        env.MoveMainCameraTo(env.sceneEditorCameraMainPosition);
                                        EditorUtility.SetDirty(env.cameraMain);
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                                Camera sceneCam = null;
                                if (SceneView.lastActiveSceneView != null) {
                                    sceneCam = SceneView.lastActiveSceneView.camera;
                                }
                                if (sceneCam != null) {
                                    EditorGUILayout.LabelField("Scene Cam Pos", sceneCam.transform.position.ToString());
                                    EditorGUILayout.BeginHorizontal();
                                    env.sceneEditorCameraSceneViewPosition = EditorGUILayout.Vector3Field("   New Position", env.sceneEditorCameraSceneViewPosition);
                                    if (GUILayout.Button("Set", GUILayout.Width(40))) {
                                        SceneView.lastActiveSceneView.LookAt(env.sceneEditorCameraSceneViewPosition + new Vector3(50, 50, 50));
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                                EditorGUILayout.BeginHorizontal();
                                if (sceneCam != null) {
                                    if (GUILayout.Button("Scene Cam To Surface")) {
                                        Vector3 pos = SceneView.lastActiveSceneView.pivot;
                                        pos.y = env.GetTerrainHeight(pos, true);
                                        SceneView.lastActiveSceneView.LookAt(pos);
                                    }
                                }
                                if (env.cameraMain != null) {
                                    if (GUILayout.Button("Find Main Cam")) {
                                        Vector3 pos = env.cameraMain.transform.position + new Vector3(50, 50, 50);
                                        Vector3 fwd = (env.cameraMain.transform.position - pos).normalized;
                                        SceneView.lastActiveSceneView.LookAt(pos, Quaternion.LookRotation(fwd));
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.LabelField("Generate Area");
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(renderInEditorAreaCenter, new GUIContent("Center"));
                                EditorGUILayout.PropertyField(renderInEditorAreaSize, new GUIContent("Size"));
                                if (GUILayout.Button("Generate Chunks In Area")) {
                                    generateAroundCamera.boolValue = false;
                                    serializedObject.ApplyModifiedProperties();
                                    GenerateEditorArea();
                                    GUIUtility.ExitGUI();
                                }
                                EditorGUI.indentLevel--;
                                break;

                        }
                    }

                    EditorGUILayout.EndVertical();

                }
            }

            // Voxel Generation
            if (GUILayout.Button(new GUIContent(" Voxel Generation", Resources.Load("VoxelPlay/Inspector/voxelGeneration") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandVoxelGenerationSection);
            }
            if (expandVoxelGenerationSection) {
                EditorGUILayout.PropertyField(enableGeneration, new GUIContent("Enable Generation", "Enables/disables world/voxel generation updates."));
                EditorGUILayout.PropertyField(maxChunks, new GUIContent("Chunks Pool Size", "Number of total chunks allowed in memory."));
                EditorGUILayout.LabelField("   Recommended >=", env.maxChunksRecommended.ToString());
                EditorGUILayout.IntSlider(prewarmChunksInEditor, 1000, maxChunks.intValue, new GUIContent("   Prewarm In Editor", "Number of chunks that will be reserved during start in Unity Editor before game starts. In the final build, all chunks are reserved before game starts to provide a smooth gameplay experience."));
                EditorGUILayout.BeginHorizontal();
                chunkNewSize = EditorGUILayout.IntPopup("Chunk Size", chunkNewSize, chunkSizeOptions, chunkSizeValues);
                GUI.enabled = chunkNewSize != VoxelPlayEnvironment.CHUNK_SIZE;
                if (GUILayout.Button("Change", GUILayout.Width(80))) {
                    ChangeChunkSize();
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                microVoxelsNewSize = EditorGUILayout.IntPopup("MicroVoxels Size", microVoxelsNewSize, microVoxelsSizeOptions, microVoxelsSizeValues);
                GUI.enabled = microVoxelsNewSize != MicroVoxels.COUNT_PER_AXIS;
                if (GUILayout.Button("Change", GUILayout.Width(80))) {
                    ChangeMicroVoxelsSize();
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(microVoxelsSnap, new GUIContent("MicroVoxels Snap", "When enabled, microvoxels space will snap to the nearest microvoxel position."));


                EditorGUILayout.PropertyField(onlyRenderInFrustum, new GUIContent("Only Render In Frustum", "When enabled, only chunks inside the camera frustum will be rendered."));
#if UNITY_WEBGL
				GUI.enabled = false;
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField ("Multi Thread Generation", GUILayout.Width (EditorGUIUtility.labelWidth));
				EditorGUILayout.LabelField ("(Unsupported platform)");
				EditorGUILayout.EndHorizontal ();
				GUI.enabled = true;
#else
                EditorGUILayout.PropertyField(multiThreadGeneration, new GUIContent("Multi Thread Generation", "When enabled, uses a dedicated background thread for chunk generation (only in build, deactivated while running inside Unity Editor)."));
#endif
                EditorGUILayout.PropertyField(visibleChunksDistance, new GUIContent("Visible Chunk Distance", "Measured in number of chunks."));
                EditorGUILayout.PropertyField(visibleChunksVerticalDistance, new GUIContent("Visible Vertical Distance", "Maximum vertical render distance in chunks. Increase for elevated cameras. Higher values use more memory. Default is 8."));
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(adjustCameraFarClip, new GUIContent("Adjust Cam Far Clip", "Adjusts camera's far clipping plane to visible chunk distance automatically."));
                EditorGUILayout.PropertyField(distanceAnchor, new GUIContent("Distance Anchor", "Where the distance is computed from. Usually this is the camera (in first person view) or the character (in third person view)."));
                EditorGUILayout.PropertyField(unloadFarChunks, new GUIContent("Unload Far Chunks", "Disable or destroy chunk gameobject when it's out of visible distance. Enable/create it again when it enters the visible distance."));
                if (unloadFarChunks.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(unloadFarChunksMode, new GUIContent("Mode", "Select the action when a chunk is unloaded. 'Toggle visibility' will just hide/show the chunks based on the visible distance parameter. 'Destroy' will actually destroy and release memory of the chunk mesh as well as its collider and NavMesh (if present)."));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(unloadFarNavMesh, new GUIContent("Unload Far NavMesh", "Allows reusing a chunk NavMesh when it's out of visible distance. Note: NavMeshes are linked to chunks so when chunk pool is exhausted, NavMeshes will be reused automatically for new chunk requests. This option just releases the chunk NavMesh earlier when chunk is out of visible distance, without waiting for the pool to be depleted."));
                EditorGUI.indentLevel--;
                EditorGUILayout.PropertyField(forceChunkDistance, new GUIContent("Force Chunk Distance", "Distance measured in chunks that will be rendered completely before starting the game."));
                EditorGUILayout.PropertyField(maxCPUTimePerFrame, new GUIContent("Max CPU Time Per Frame", "Maximum milliseconds that can be used by the CPU per frame to generate the world."));
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableColliders, new GUIContent("Colliders", "Enables/disables collider generation for opaque voxels."));
                EditorGUILayout.PropertyField(enableNavMesh, new GUIContent("NavMesh", "Enables/disables NavMesh generation."));
                if (enableNavMesh.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(navMeshResolution, new GUIContent("Resolution", "Detail of the generated navMesh. Use a higher resolution if you need navMesh to be created on single voxels or Default every two voxels."));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(hideChunksInHierarchy, new GUIContent("Hide Chunks In Hierarchy", "Do not show chunks in hierarchy (this option has no effect in a build)"));
                EditorGUILayout.PropertyField(enableTrees, new GUIContent("Trees", "Enables/disables tree generation."));
                if (EditorGUI.EndChangeCheck()) {
                    rebuildWorld = true;
                }
                if (enableTrees.boolValue) {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(denseTrees, new GUIContent("   Dense Trees", "If enabled, disables adjacent voxel occlusion making tree leaves cutout denser."));
                    if (EditorGUI.EndChangeCheck())
                        refreshChunks = true;
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableVegetation, new GUIContent("Vegetation", "Enables/disables bush generation."));
                if (EditorGUI.EndChangeCheck())
                    rebuildWorld = true;
                EditorGUILayout.PropertyField(enableDetailGenerators, new GUIContent("Detail Generators", "Enables/disables world detail generators."));
                EditorGUILayout.PropertyField(particlePoolSize, new GUIContent("Particle Pool Size", "Maximum number of active particles, including recoverable voxels"));
                layerParticles.intValue = EditorGUILayout.LayerField(new GUIContent("Particles Layer", "The layer used for particles. Used to optimize physics and avoid particle collision between them."), layerParticles.intValue);
                layerVoxels.intValue = EditorGUILayout.LayerField(new GUIContent("Voxels Layer", "The layer used for voxels. Used to optimize physics and avoid voxels collision between them."), layerVoxels.intValue);
                layerClouds.intValue = EditorGUILayout.LayerField(new GUIContent("Clouds Layer", "The layer used for cloud voxels. Can be used to ignore cloud chunks if using a top-down camera or for other purposes."), layerClouds.intValue);
            }

            // Quality and effects
            if (GUILayout.Button(new GUIContent(" Shader Features", Resources.Load("VoxelPlay/Inspector/qualityAndEffects") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandQualitySection);
            }
            if (expandQualitySection) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Preset", GUILayout.Width(120));
                if (GUILayout.Button(new GUIContent("All Features", "Enables all engine visual features available for the active platform."))) {
                    globalIllumination.boolValue = true;
                    enableShadows.boolValue = true;
                    shadowsOnWater.boolValue = true;
                    enableSmoothLighting.boolValue = true;
                    enableFogSkyBlending.boolValue = true;
                    denseTrees.boolValue = true;
                    hqFiltering.boolValue = true;
                    usePixelLights.boolValue = true;
                    enableBevel.boolValue = true;
                    enableBrightPointLights.boolValue = true;
                    doubleSidedGlass.boolValue = true;
                    transparentBling.boolValue = true;
                    if (VoxelPlayFirstPersonController.instance != null) {
                        VoxelPlayFirstPersonController.instance.autoInvertColors = true;
                    }
                    rebuildWorld = true;
                }
                if (GUILayout.Button(new GUIContent("Medium", "Disables shadows to improve performance but keeps global illumination."))) {
                    globalIllumination.boolValue = true;
                    enableShadows.boolValue = false;
                    shadowsOnWater.boolValue = false;
                    enableSmoothLighting.boolValue = true;
                    enableFogSkyBlending.boolValue = true;
                    enableReliefMapping.boolValue = false;
                    usePixelLights.boolValue = true;
                    enableBevel.boolValue = false;
                    enableBrightPointLights.boolValue = false;
                    denseTrees.boolValue = true;
                    hqFiltering.boolValue = true;
                    doubleSidedGlass.boolValue = true;
                    transparentBling.boolValue = true;
                    if (VoxelPlayFirstPersonController.instance != null) {
                        VoxelPlayFirstPersonController.instance.autoInvertColors = true;
                    }
                    rebuildWorld = true;
                }
                if (GUILayout.Button(new GUIContent("Fastest", "Disables all effects to improve performance."))) {
                    globalIllumination.boolValue = false;
                    enableShadows.boolValue = false;
                    shadowsOnWater.boolValue = false;
                    enableSmoothLighting.boolValue = false;
                    obscuranceMode.intValue = (int)ObscuranceMode.Faster;
                    enableFogSkyBlending.boolValue = false;
                    enableReliefMapping.boolValue = false;
                    enableNormalMap.boolValue = false;
                    usePixelLights.boolValue = false;
                    enableBevel.boolValue = false;
                    enableBrightPointLights.boolValue = false;
                    denseTrees.boolValue = false;
                    hqFiltering.boolValue = false;
                    doubleSidedGlass.boolValue = false;
                    onlyRenderInFrustum.boolValue = true;
                    transparentBling.boolValue = false;
                    if (VoxelPlayFirstPersonController.instance != null) {
                        VoxelPlayFirstPersonController.instance.autoInvertColors = false;
                    }
                    if (visibleChunksDistance.intValue > 6) {
                        visibleChunksDistance.intValue = 6;
                    }
                    if (forceChunkDistance.intValue > 2) {
                        forceChunkDistance.intValue = 2;
                    }
                    if (maxChunks.intValue > 5000) {
                        maxChunks.intValue = 5000;
                    }
                    rebuildWorld = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(globalIllumination, new GUIContent("Global Illumination", "Enables Voxel Play's own lightmap computation. This option adds smooth shading and lighting in combination with Unity shadow system."));
                if (EditorGUI.EndChangeCheck())
                    refreshChunks = true;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableSmoothLighting, new GUIContent("Smooth Lighting", "Interpolates lighting between voxel vertices. Also includes ambient occlusion."));
                if (EditorGUI.EndChangeCheck())
                    refreshChunks = true;

                GUI.enabled = enableSmoothLighting.boolValue;
                int prevInt = obscuranceMode.intValue;
                EditorGUILayout.PropertyField(obscuranceMode, new GUIContent("Obscurance Mode", "Changes shader obscurance function. Requires smooth lighting."));
                if (obscuranceMode.intValue != prevInt) {
                    updateSpecialFeaturesMacro = true;
                }
                if (obscuranceMode.intValue == (int)ObscuranceMode.Custom) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(obscuranceIntensity, new GUIContent("Intensity", "AO intensity."));
                    EditorGUI.indentLevel--;
                }
                GUI.enabled = true;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(usePixelLights, new GUIContent("Per-Pixel Lighting", "If disabled, lighting will be calculated per-vertex."));
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                    reloadWorldTextures = true;
                    updateSpecialFeaturesMacro = true;
                }

                EditorGUILayout.PropertyField(ambientLight, new GUIContent("Ambient Light", "Minimum amount of light in the scene affecting the voxels."));

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableShadows, new GUIContent("Shadows", "Turns on/off shadow casting and receiving on voxels."));
                if (EditorGUI.EndChangeCheck()) {
                    rebuildWorld = true;
                }
                if (!enableShadows.boolValue) {
                    CheckMainLightShadows();
                }
                EditorGUI.BeginChangeCheck();
                if (!VoxelPlayEnvironment.supportsURP) {
                    EditorGUILayout.PropertyField(shadowsOnWater, new GUIContent("Shadows On Water", "Enables shadow receiving on water surface."));
                } else if (shadowsOnWater.boolValue) {
                    shadowsOnWater.boolValue = false;
                }
                EditorGUILayout.PropertyField(realisticWater, new GUIContent("Realistic Water", "Uses a realistic water shader."));
                if (EditorGUI.EndChangeCheck()) {
                    rebuildWorld = true;
                }

                EditorGUILayout.PropertyField(daylightShadowAtten, new GUIContent("Daylight Shadow Atten", "Shadow attenuation factor when Sun is high. Set this value to 0 to preserve standard shadow intensity. A value of 1 will make shadows disappear when Sun is on top. A middle value will make shadows more intense when Sun is low in the sky and more subtle when Sun is high."));
                EditorGUILayout.PropertyField(diffuseWrap, new GUIContent("Diffuse Wrap", "Diffuse wrap factor. Set this value to 0 to preserve standard diffuse wrap. A value of 1 will make diffuse wrap more intense."));

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableNormalMap, new GUIContent("Normal Mapping", "Enables use of normal maps."));
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                    reloadWorldTextures = true;
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableReliefMapping, new GUIContent("Relief Mapping", "Enables parallax occlusion/relief mapping."));
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                    reloadWorldTextures = true;
                }
                if (enableReliefMapping.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(reliefStrength, new GUIContent("Strength", "Strength of the parallax effect."));
                    EditorGUILayout.PropertyField(reliefMaxDistance, new GUIContent("Max Distance", "Maximum visible distance for the parallax effect."));
                    EditorGUILayout.PropertyField(reliefIterations, new GUIContent("Iterations", "Max number of ray-marching steps."));
                    EditorGUILayout.PropertyField(reliefIterationsBinarySearch, new GUIContent("Binary Search Iterations", "Max number of binary search iterations to precisely find the intersection point."));
                    EditorGUI.indentLevel--;
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enablePBR, new GUIContent("Physically Based Rendering", "Enables use of PBR maps (metallic, smoothness, occlusion)."));
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                    reloadWorldTextures = true;
                }

                EditorGUILayout.PropertyField(textureSize, new GUIContent("Texture Size", "Texture size should be a multiple of 2 (eg. 16, 32, 64, 128)"));
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableTinting, new GUIContent("Enable Tinting", "Enables individual voxel tint color."));
                EditorGUILayout.PropertyField(enableColoredShadows, new GUIContent("Colored Shadows", "When enabled, customize shadow tint color in the world definition."));
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                    updateSpecialFeaturesMacro = true;
                }

                EditorGUILayout.PropertyField(enableOutline, new GUIContent("Outline", "Enables outline effect on solid voxels."));
                if (enableOutline.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(outlineColor, new GUIContent("Color", "Outline color and alpha."));
                    EditorGUILayout.PropertyField(outlineThreshold, new GUIContent("Threshold", "Controls outline width."));
                    EditorGUI.indentLevel--;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableBevel, new GUIContent("Bevel", "Enables bevel effect by tweaking top face normals. Looks better from a third-person view."));
                if (EditorGUI.EndChangeCheck() && !Application.isPlaying) {
                    refreshChunks = true;
                    updateSpecialFeaturesMacro = true;
                }
                if (enableBevel.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(bevelWidth, new GUIContent("Width", "Controls bevel intensity."));
                    EditorGUI.indentLevel--;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(doubleSidedGlass, new GUIContent("Double Sided Glass", "Renders both sides of transparent voxels."));
                EditorGUILayout.PropertyField(transparentBling, new GUIContent("Transparent Bling", "Enables shining effect on transparent voxels."));
                if (EditorGUI.EndChangeCheck()) {
                    rebuildWorld = true;
                }

                EditorGUILayout.PropertyField(damageParticles);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableBrightPointLights, new GUIContent("Bright Point Lights", "Improves appearance of point lights."));
                if (enableBrightPointLights.boolValue) {
                    EditorGUI.indentLevel++;
                    if (VoxelPlayEnvironment.supportsURP) {
                        EditorGUILayout.PropertyField(enableURPNativeLights, new GUIContent("Enable URP Native Lights", "Adds support for native URP point and spot lights with shadows. Make sure additional lights and shadows are enabled in the URP asset used in Project Settings/Quality or Project Settings/Graphics."));
                    }
                }
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                    updateSpecialFeaturesMacro = true;
                }

                if (enableBrightPointLights.boolValue) {
                    EditorGUILayout.PropertyField(brightPointsMaxDistance, new GUIContent("Max Distance", "Max distance to render bright point lights."));
                    EditorGUI.indentLevel--;
                }

                GUI.enabled = !Application.isPlaying;
                EditorGUI.BeginChangeCheck();
                enableCurvatureFromShader = EditorGUILayout.Toggle(new GUIContent("Curvature", "Enables curvature vertex modifier in VoxelPlay shaders."), enableCurvatureFromShader);
                enableCurvature.boolValue = enableCurvatureFromShader;
                if (EditorGUI.EndChangeCheck()) {
                    updateCurvatureMacro = true;
                    rebuildWorld = true;
                }
                if (enableCurvatureFromShader) {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.indentLevel++;
                    curvatureAmount = EditorGUILayout.TextField(new GUIContent("Amount", "Vertex shift amount multiplier."), curvatureAmount);
                    if (GUILayout.Button("Update", GUILayout.Width(65))) {
                        updateCurvatureMacro = true;
                        rebuildWorld = true;
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndHorizontal();
                }
                GUI.enabled = true;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(seeThrough, new GUIContent("See Through", "Hides voxels between camera and desired target. This option is designed for third person perspective."));
                if (EditorGUI.EndChangeCheck()) {
                    updateSpecialFeaturesMacro = true;
                }
                if (seeThrough.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(seeThroughTarget, new GUIContent("Target", "The target gameobject. Usually this is the character controller or player gameobject."));
                    EditorGUILayout.PropertyField(seeThroughRadius, new GUIContent("Radius", "Radius of effect. No voxels will be visible within this distance to the target."));
                    EditorGUILayout.PropertyField(seeThroughHeightOffset, new GUIContent("Height Offset", "Voxels below target plus this height offset won't be hidden. This option avoids hiding the ground."));
                    EditorGUILayout.PropertyField(seeThroughAlpha, new GUIContent("Alpha", "The alpha value used for occluded voxels with see-through mode set to transparency."));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(useOriginShift, new GUIContent("Origin Shift", "Shift player to origin when its position passes beyond a threshold. This is called origin shift and is necessary to avoid floating point issues."));
                if (useOriginShift.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(originShiftDistanceThreshold, new GUIContent("Distance Threshold", "The distance at which the origin shift occurs."));
                    EditorGUI.indentLevel--;
                }
            }

            // Rendering
            if (GUILayout.Button(new GUIContent(" Rendering Options", Resources.Load("VoxelPlay/Inspector/renderingOptions") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandRenderingSection);
            }
            if (expandRenderingSection) {

                EditorGUI.BeginChangeCheck();
                GUI.enabled = SystemInfo.supportsComputeShaders;
                GUIContent computeGUIContent = new GUIContent("Compute Buffers", "Enables compute buffers for custom voxels. This option requires GPU capable of Shader Model 4.5 so it will restrict the amount of potential mobile devices that can run your game. Performance benefits vs regular GPU instancing in custom voxels may vary depending on platform, amount of voxels, etc. Do a benchmark before using this option.");
                if (!GUI.enabled) {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(computeGUIContent, GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUILayout.LabelField("(Unsupported platform or graphics API)");
                    EditorGUILayout.EndHorizontal();
                } else {
                    EditorGUILayout.PropertyField(useComputeBuffers, computeGUIContent);
                }
                if (EditorGUI.EndChangeCheck()) {
                    rebuildWorld = true;
                }
                GUI.enabled = true;

                EditorGUILayout.PropertyField(instancingCullingMode, new GUIContent("Instancing Culling Mode", "Aggressive is the default value: culls non visible voxels. Gentle allows some padding to keep shadows from invisible voxels. Disabled: renders all voxels, regardless of their positions vs camera."));
                if ((InstancingCullingMode)instancingCullingMode.intValue == InstancingCullingMode.Gentle) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(instancingCullingPadding);
                    EditorGUI.indentLevel--;
                }

                GUI.enabled = !Application.isPlaying;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(filterMode, new GUIContent("Texture Sampling", "Choose the texture sampling filter mode."));
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                    reloadWorldTextures = true;
                    updateSpecialFeaturesMacro = true;
                }

                GUI.enabled = true;
                if (filterMode.intValue == (int)FilterMode.Point) {
                    GUI.enabled = !enableReliefMapping.boolValue;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(hqFiltering, new GUIContent("HQ Point Filter", "Enables mipmapping and intergrated texel antialiasing."));
                    if (EditorGUI.EndChangeCheck()) {
                        refreshChunks = true;
                        reloadWorldTextures = true;
                    }
                    if (hqFiltering.boolValue) {
                        EditorGUI.indentLevel++;
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(mipMapBias, new GUIContent("MipMap Bias", "Increase to reduce texture blurring."));
                        if (EditorGUI.EndChangeCheck()) {
                            refreshChunks = true;
                            reloadWorldTextures = true;
                        }
                        EditorGUI.indentLevel--;
                    }
                    GUI.enabled = true;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(reflectionProbeUsage, new GUIContent("Reflection Probes", "Choose the reflection probe usage mode. When Physically Based Rendering is enabled, reflection probe usage is always enabled."));
                if (enablePBR.boolValue && reflectionProbeUsage.intValue == (int)ReflectionProbeUsage.Off) {
                    EditorGUILayout.HelpBox("When Physically Based Rendering is enabled, reflection probe usage is always enabled.", MessageType.Info);
                }
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                    rebuildWorld = true;
                }

                EditorGUILayout.LabelField("Gaps/White Pixels Removal Options");
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                voxelPadding = EditorGUILayout.Toggle(new GUIContent("Voxel Padding", "Enlarges voxels a bit to prevent gaps (white pixels) in adjacent edges due to greedy meshing."), voxelPadding);
                if (EditorGUI.EndChangeCheck()) {
                    ChangeVoxelPadding();
                }
                EditorGUILayout.PropertyField(usePostProcessing, new GUIContent("Post Processing", "Uses a custom post processing effect to detect and remove white pixels."));
                if (usePostProcessing.boolValue && !VoxelPlayPostProcessing.isActive) {
                    EditorGUILayout.HelpBox("Additional steps are required:\nIn built-in pipeline, Voxel Play Post Processing script must be added to your camera.\nIn URP, add the Voxel Play Post Processing render feature to the URP Universal Renderer.", MessageType.Warning);
                }
                if (voxelPadding && usePostProcessing.boolValue) {
                    EditorGUILayout.HelpBox("Only one method to remove white pixels should be used.", MessageType.Warning);
                }
                EditorGUI.indentLevel--;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableFarChunksRendering, new GUIContent("Far Chunks Rendering", "Enables rendering of far chunks."));
                if (EditorGUI.EndChangeCheck()) {
                    serializedObject.ApplyModifiedProperties();
                    VoxelPlayFarChunksRenderer.Dispose();
                    if (enableFarChunksRendering.boolValue) {
                        VoxelPlayFarChunksRenderer.Init(env);
                    } else {
                        env.UpdateMaterialProperties();
                    }
                    GUIUtility.ExitGUI();
                    return;
                }
                if (enableFarChunksRendering.boolValue) {
                    EditorGUI.indentLevel++;
                    if (!Application.isPlaying) {
                        EditorGUILayout.HelpBox("Far chunks rendering only works in playmode.", MessageType.Info);
                    }
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(farChunksShadows, new GUIContent("Shadows", "Enables shadows for far chunks."));
                    if (farChunksShadows.boolValue) {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(farChunksShadowIntensity, new GUIContent("Intensity", "Intensity of shadows for far chunks."));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(farChunksWaterReflections, new GUIContent("Water Reflections", "Enables water reflections for far chunks."));
                    if (farChunksWaterReflections.boolValue) {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(farChunksWaterReflectionsIntensity, new GUIContent("Intensity", "Intensity of shadows for far chunks."));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(farChunksWaterColorOverride, new GUIContent("Water Color Override", "Enables overriding the water color for far chunks."));
                    if (farChunksWaterColorOverride.boolValue) {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(farChunksWaterColor, new GUIContent("Color", "Color of water for far chunks."));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(farChunksShoreColor, new GUIContent("Shore Color", "Color of shore for far chunks."));
                    if (EditorGUI.EndChangeCheck()) {
                        serializedObject.ApplyModifiedProperties();
                        VoxelPlayFarChunksRenderer.requireUpdateMaterialProperties = true;
                    }
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(farChunksDeepWater, new GUIContent("Deep Water", "Trace below water level for far chunks."));
                    if (env.world != null && env.world.terrainGenerator != null && env.world.terrainGenerator.maxHeight > 255) {
                        EditorGUILayout.HelpBox("The maximum height of the terrain generator is set to a value higher than 255. The precision for the heightmap capture will increase from 8bit to 16bit. This will double the memory required by this feature and may also impact rendering performance.", MessageType.Info);
                    }
                    if (EditorGUI.EndChangeCheck()) {
                        serializedObject.ApplyModifiedProperties();
                        VoxelPlayFarChunksRenderer.Dispose();
                        VoxelPlayFarChunksRenderer.Init(env);
                        GUIUtility.ExitGUI();
                        return;
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(smartBiomeSurface, new GUIContent("Smart Biome Surface", "When enabled, underground voxels that become surface will be rendered as the biome surface voxel during meshing."));
                if (EditorGUI.EndChangeCheck()) {
                    refreshChunks = true;
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableBiomeMixing, new GUIContent("Biome Mixing", "Enables transitions between biomes by randomly mixing voxel types at biome borders."));
                if (EditorGUI.EndChangeCheck()) {
                    rebuildWorld = true;
                }
                if (enableBiomeMixing.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(biomeMixingSpread, new GUIContent("Mixing Spread", "Width of the mixing zone between biomes. Higher values produce wider transitions."));
                    if (EditorGUI.EndChangeCheck()) {
                        rebuildWorld = true;
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(placementAnimation, new GUIContent("Placement Animation", "Animation played before committing a voxel placement."));
                if (placementAnimation.enumValueIndex != 0) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(placementAnimDuration, new GUIContent("Duration", "Duration in seconds for the placement animation."));
                    if (placementAnimation.enumValueIndex == (int)VoxelPlayEnvironment.PlacementAnimationMode.Throw) {
                        EditorGUILayout.Slider(placementAnimThrowArcMagnitude, 0f, 0.2f, new GUIContent("Arc Magnitude", "Arc amplitude as a fraction of path length (0..0.2)."));
                    }
                    if (placementAnimation.enumValueIndex != (int)VoxelPlayEnvironment.PlacementAnimationMode.ElasticPop) {
                        EditorGUILayout.PropertyField(placementAnimSpin, new GUIContent("Spin", "Rotate the voxel during the animation."));
                        if (placementAnimSpin.boolValue) {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.Slider(placementAnimSpinTurns, 0f, 2f, new GUIContent("Turns", "Number of full rotations over the animation."));
                            EditorGUI.indentLevel--;
                        }
                    }
                    if (placementAnimation.enumValueIndex == (int)VoxelPlayEnvironment.PlacementAnimationMode.ElasticPop) {
                        EditorGUILayout.Slider(placementAnimElasticPopAmount, 0f, 0.4f, new GUIContent("Pop Amount", "Overshoot amplitude for the pop."));
                    }
                    EditorGUILayout.PropertyField(placementAnimFade, new GUIContent("Fade", "Animate alpha from 0 to 1 during placement (supported on opaque/cutout/alpha voxels)."));
                    EditorGUILayout.PropertyField(placementAnimUseGhostCollider, new GUIContent("Use Ghost Collider", "If enabled, spawns a temporary trigger collider so raycasts hit in-progress placements."));
                    EditorGUI.indentLevel--;
                }
            }

            // Sky Options
            if (GUILayout.Button(new GUIContent(" Sky Options", Resources.Load("VoxelPlay/Inspector/skySettings") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandSkySection);
            }
            if (expandSkySection) {
                EditorGUILayout.PropertyField(sun, new GUIContent("Sun", "Assigns the directional light used as the Sun."));
                EditorGUILayout.PropertyField(enableFogSkyBlending, new GUIContent("Enable Fog", "Enabled fog/sky blending."));
                if (enableFogSkyBlending.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(fogAmount, new GUIContent("Fog Height", "Amount of fog."));
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(fogDistanceAuto, new GUIContent("Auto Distance", "Adjust fog distance to match camera's far clipping plane or visible chunk distance if unload chunks is enabled (the lower distance)."));
                    if (env.cameraMain != null) {
                        EditorGUILayout.LabelField("(Currently: " + env.GetFogAutoDistance() + ")");
                    }
                    EditorGUILayout.EndHorizontal();
                    if (!fogDistanceAuto.boolValue) {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(fogDistance, new GUIContent("Fog Distance", "Fog's distance factor"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(fogFallOff, new GUIContent("Fog Fall Off", "Fog's fall off factor"));
                    EditorGUILayout.PropertyField(fogTint, new GUIContent("Fog Tint", "Fog's tint color"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(enableClouds, new GUIContent("Enable Clouds", "Clouds generation on/off"));
            }

            // Save/Load System
            if (GUILayout.Button(new GUIContent(" Save/Load System", Resources.Load("VoxelPlay/Inspector/saveLoadSettings") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandSaveLoadSection);
            }
            if (expandSaveLoadSection) {
                EditorGUILayout.PropertyField(loadSavedGame, new GUIContent("Load Saved Game At Start", "If Voxel Play should load a previously saved game at start up. Specify name of saved game in 'Save Filename' field."));
                EditorGUILayout.PropertyField(saveFilename, new GUIContent("Filename", "The current name for the saved game file. Used at runtime when pressing F3 to load or F4 to save. You can set a different save filename at runtime to support multiple save slots."));
                EditorGUILayout.PropertyField(restorePlayerPosition, new GUIContent("Restore Player Position", "If Voxel Play should restore the player's position from the saved game."));
                EditorGUILayout.PropertyField(enableDynamicLoad, new GUIContent("Enable Dynamic Load", "If enabled, regions will be loaded dynamically as needed instead of loading the entire world at start. This improves initial loading time and memory usage for large worlds."));
                EditorGUILayout.PropertyField(fallbackVoxelDefinition, new GUIContent("Fallback Voxel Definition", "The voxel definition to use when a voxel definition is missing from the saved game."));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Editor Saves", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(sceneEditorAutomaticBackup, new GUIContent("Automatic Backup", "When enabled, a backup will be created before updating the savegame files when using the world editor."));
            }

            if (GUILayout.Button(new GUIContent(" Optional Game Features", Resources.Load("VoxelPlay/Inspector/optionalGameFeatures") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandInGameSection);
            }
            if (expandInGameSection) {
                EditorGUILayout.PropertyField(enableLoadingPanel, new GUIContent("Loading Screen", "Shows a loading panel during start up while chunks are being reserved."));
                if (enableLoadingPanel.boolValue) {
                    EditorGUILayout.PropertyField(loadingText, new GUIContent("   Text", "Text to show while initializing the engine."));
                }
                EditorGUILayout.PropertyField(initialWaitTime, new GUIContent("Initial Wait Time", "Additional seconds to wait before loading screen is removed."));
                if (initialWaitTime.floatValue > 0) {
                    EditorGUILayout.PropertyField(initialWaitText, new GUIContent("   Text", "Text to show diring the additional wait time."));
                }
                GUI.enabled = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android || EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
                EditorGUILayout.PropertyField(previewTouchUIinEditor, new GUIContent("Preview Mobile UI in Editor", "Shows mobile UI in Editor when targeting a mobile platform."));
                GUI.enabled = true;
                EditorGUILayout.PropertyField(enableBuildMode, new GUIContent("Enable Build Mode", "Enables entering Build Mode by pressing key B. In build mode, all world items are available in the inventory in unlimited amount and anything can be destroyed with a single hit. Player is also indestructible."));
                if (enableBuildMode.boolValue) {
                    EditorGUILayout.PropertyField(buildMode, new GUIContent("   Build Mode ON", "Activates build mode."));
                }
                EditorGUILayout.PropertyField(enableConsole, new GUIContent("Enable Console", "Enables console system. Shows when pressing F1."));
                if (enableConsole.boolValue) {
                    EditorGUILayout.PropertyField(showConsole, new GUIContent("   Visible", "Toggles console visibility on/off. The console shows useful data for debugging purposes."));
                    EditorGUILayout.PropertyField(consoleBackgroundColor, new GUIContent("   Background Color"));
                }
                EditorGUILayout.PropertyField(enableStatusBar, new GUIContent("Enable Status Bar"));
                if (enableStatusBar.boolValue) {
                    EditorGUILayout.PropertyField(statusBarBackgroundColor, new GUIContent("   Status Bar Color"));
                }
                EditorGUILayout.PropertyField(enableInventory, new GUIContent("Enable Inventory", "Enables inventory UI when pressing Tab. Disable if you wish to provide your own interface."));
                EditorGUILayout.PropertyField(defaultPickupRadius, new GUIContent("Default Pickup Radius", "Distance at which the player attracts and collects dropped items. Items can override this with their own pickupRadius."));
                EditorGUILayout.PropertyField(enableDebugWindow, new GUIContent("Enable Debug Window", "Enables debug window toggling using F2."));
                EditorGUILayout.PropertyField(showFPS, new GUIContent("Show FPS", "Shows FPS on top/right screen corner."));
            }

            if (GUILayout.Button(new GUIContent(" Default Assets", Resources.Load("VoxelPlay/Inspector/defaultAssets") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandDefaultsSection);
            }
            if (expandDefaultsSection) {
                EditorGUILayout.PropertyField(defaultBuildSound, new GUIContent("Build Sound", "Default sound played when an item or voxel is placed in the scene."));
                EditorGUILayout.PropertyField(defaultPickupSound, new GUIContent("Pick Up Sound", "Default sound played when an item is collected."));
                EditorGUILayout.PropertyField(defaultImpactSound, new GUIContent("Impact Sound", "Default sound played when a voxel is hit."));
                EditorGUILayout.PropertyField(defaultDestructionSound, new GUIContent("Destruction Sound", "Default sound played when a voxel is destroyed."));
                EditorGUILayout.PropertyField(defaultVoxel, new GUIContent("Default Voxel", "Assumed voxel when the voxel definition is missing or placing colors directly on the positions."));
                EditorGUILayout.PropertyField(defaultWaterVoxel, new GUIContent("Default Water Voxel", "Default water voxel in case the terrain generator doesn't assign one."));

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(inputControllerPC, new GUIContent("Input Prefab (PC)", "The prefab that contains the input controller script for PC."));
                if (GUILayout.Button("Load Default", GUILayout.Width(120))) {
                    inputControllerPC.objectReferenceValue = Resources.Load<GameObject>("VoxelPlay/InputControllers/PC/Voxel Play PC Input Controller");
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(inputControllerMobile, new GUIContent("Input Prefab (Mobile)", "The prefab that contains the input controller script for mobile."));
                if (GUILayout.Button("Load Default", GUILayout.Width(120))) {
                    inputControllerMobile.objectReferenceValue = Resources.Load<GameObject>("VoxelPlay/InputControllers/Mobile/Voxel Play Mobile Input Controller");
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(uiCanvasPrefab, new GUIContent("UI Prefab", "The canvas prefab used for the game main interface. This interface has elements for inventory, selected item, crosshair and other information."));
                if (GUILayout.Button("Load Default", GUILayout.Width(120))) {
                    uiCanvasPrefab.objectReferenceValue = Resources.Load<GameObject>("VoxelPlay/UI/Voxel Play UI Canvas");
                }
                EditorGUILayout.EndHorizontal();

                if (uiCanvasPrefab.objectReferenceValue != null) {
                    EditorGUILayout.PropertyField(welcomeMessage, new GUIContent("Welcome Text", "Optional message shown when game starts"));
                    EditorGUILayout.PropertyField(welcomeMessageDuration, new GUIContent("Welcome Duration", "Duration for the welcome text"));
                }

                EditorGUILayout.PropertyField(crosshairPrefab, new GUIContent("Crosshair Prefab", "The prefab used for the crosshair."));
                EditorGUILayout.PropertyField(crosshairTexture, new GUIContent("Crosshair Texture", "The texture used for the crosshair."));
            }

            // Advanced section
            if (GUILayout.Button(new GUIContent(" Advanced", Resources.Load("VoxelPlay/Inspector/advancedSettings") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandAdvancedSection);
            }
            if (expandAdvancedSection) {
                EditorGUILayout.PropertyField(debugLevel);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serverMode, new GUIContent("Server Mode", "In server mode, Voxel Play doesn't render voxels to reduce memory usage and improve performance of system when running on an unattended server."));
                if (EditorGUI.EndChangeCheck() && serverMode.boolValue) {
                    lowMemoryMode.boolValue = true;
                }
                EditorGUILayout.PropertyField(lowMemoryMode, new GUIContent("Low Memory Mode", "When enabled, internal rendering buffers are not pre-allocated during start up. Memory allocation occur when needed only. Enable this option to reduce memory pressure warnings on mobile devices or on dedicated servers with low memory. Some memory allocation spike can occur when a buffer needs resizing."));
                EditorGUILayout.PropertyField(delayedInitialization, new GUIContent("Delayed Initialization", "When enabled, Voxel Play won't initialize until you call the Init() method."));
                EditorGUILayout.BeginHorizontal();
                maxMaterialsPerChunk = EditorGUILayout.IntField(new GUIContent("Max Materials Per Chunk", "The number of different materials that can be used in a single chunk. Please note that this number should be kept low to reduce memory usage and improve performance."), maxMaterialsPerChunk);
                GUI.enabled = maxMaterialsPerChunk != VoxelPlayEnvironment.MAX_MATERIALS_PER_CHUNK;
                if (GUILayout.Button("Change", GUILayout.Width(80))) {
                    ChangeMaxMaterialsPerChunk();
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            // Stats
            if (GUILayout.Button(new GUIContent(" Stats", Resources.Load("VoxelPlay/Inspector/Stats") as Texture2D), leftAlignStyle)) {
                ToggleSection(ref expandStatsSection);
            }
            if (expandStatsSection) {
                ShowProgressBar("Chunk Rendering: Pending (" + env.chunksInRenderQueueCount + ") / Drawn (" + env.chunksDrawn + ")", (env.chunksDrawn + 1f) / (env.chunksDrawn + env.chunksInRenderQueueCount + 1f));
                if (env.enableTrees) {
                    ShowProgressBar("Tree Creation: Pending (" + env.treesInCreationQueueCount + ") / Created (" + env.treesCreated + ")", (env.treesCreated + 1f) / (env.treesCreated + env.treesInCreationQueueCount + 1f));
                } else {
                    ShowProgressBar("Tree Creation: ---", 1f);
                }
                if (env.enableVegetation) {
                    ShowProgressBar("Bush Creation: Pending (" + env.vegetationInCreationQueueCount + ") / Created (" + env.vegetationCreated + ")", (env.vegetationCreated + 1f) / (env.vegetationCreated + env.vegetationInCreationQueueCount + 1f));
                } else {
                    ShowProgressBar("Bush Creation: ---", 1f);
                }
                EditorGUILayout.LabelField(new GUIContent("Total Chunks Created", "Increases when the chunk contents are generated which occurs when a chunk is created for the first time or when a chunk is reused and its contents replaced."), new GUIContent(env.chunksCreated.ToString()));
                EditorGUILayout.LabelField("Chunks Pool Usage", env.chunksUsed + " of " + maxChunks.intValue + " (" + (env.chunksUsed * 100f / env.maxChunks).ToString("F1") + "%)");
                EditorGUILayout.LabelField(new GUIContent("Total Voxels Created", "Number of voxels that contribute to mesh generation. Fully surrounded voxels are hidden and are not included."), new GUIContent(env.voxelsCreatedCount.ToString()));
            }

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Biome Map Explorer")) {
                VoxelPlayBiomeExplorer.ShowWindow();
            }
            if (GUILayout.Button("Import Models...")) {
                VoxelPlayImportTools.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            bool undoPerformed = Event.current != null && "UndoRedoPerformed".Equals(Event.current.commandName);
            if (serializedObject.ApplyModifiedProperties() || rebuildWorld || undoPerformed) {
                if (undoPerformed) {
                    VoxelPlayFarChunksRenderer.requireUpdateMaterialProperties = true;
                }
                if (updateSpecialFeaturesMacro) {
                    Debug.Log("Optimization: modifying scripts/shaders macros to reflect special feature change...");
                    env.UpdateSpecialFeaturesCodeMacro();
                    GUIUtility.ExitGUI();
                    return;
                }
                if (updateCurvatureMacro) {
                    UpdateCurvatureMacro();
                    GUIUtility.ExitGUI();
                    return;
                }
                if (env.gameObject.activeInHierarchy) {
                    if (Application.isPlaying || env.renderInEditor) {
                        if (rebuildWorld) {
                            rebuildWorld = false;
                            env.ReloadWorld();

                            // Check if scene camera is under terrain
                            if (!Application.isPlaying && env.renderInEditor && SceneView.lastActiveSceneView != null) {
                                Camera cam = SceneView.lastActiveSceneView.camera;
                                if (cam != null) {
                                    Vector3 camPos = SceneView.lastActiveSceneView.pivot;
                                    float h = env.GetTerrainHeight(camPos, true);
                                    if (camPos.y < h + 2) {
                                        camPos.y = h + 2;
                                    } else if (camPos.y > h + 100) {
                                        camPos.y = h + 50f;
                                    }
                                    SceneView.lastActiveSceneView.LookAt(camPos);
                                }
                            }
                        } else if (refreshChunks) {
                            refreshChunks = false;
                            env.Redraw(reloadWorldTextures);
                        }
                        env.UpdateMaterialProperties();
                    }
                }
                env.EnableEditorActivity();
            }
        }

        void ShowHelpButtons (bool showHideButton) {
            if (showHideButton) {
                if (GUILayout.Button("New Tip", GUILayout.Width(90))) {
                    cookieIndex++;
                }
                if (GUILayout.Button("Hide Help Section", GUILayout.Width(130))) {
                    cookieIndex = -1;
                    GUIUtility.ExitGUI();
                }
            } else if (GUILayout.Button("Help & Tutorials", GUILayout.Width(130))) {
                cookieIndex++;
            }
        }

        void FocusSceneView () {
            if (SceneView.sceneViews != null && SceneView.sceneViews.Count > 0) {
                SceneView sv = SceneView.sceneViews[0] as SceneView;
                if (sv != null && EditorWindow.focusedWindow != sv) {
                    sv.Focus();
                }
            }
        }

        void ShowProgressBar (string text, float progress) {
            Rect r = EditorGUILayout.BeginVertical();
            EditorGUI.ProgressBar(r, progress, text);
            GUILayout.Space(18);
            EditorGUILayout.EndVertical();
        }


        void CreateWorldDefinition () {
            // Create the base directory structure
            string worldsPath = "Assets/Voxel Play/Resources/Worlds";
            if (!Directory.Exists(worldsPath)) {
                Directory.CreateDirectory(worldsPath);
                AssetDatabase.Refresh();
            }

            // Generate a unique folder name using Unity's API
            string baseWorldName = "NewWorld";
            string newWorldPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(worldsPath, baseWorldName));
            string newWorldName = Path.GetFileName(newWorldPath);

            // Create the new world folder
            Directory.CreateDirectory(newWorldPath);

            // Create subdirectories
            string biomesPath = Path.Combine(newWorldPath, "Biomes");
            string generatorsPath = Path.Combine(newWorldPath, "Generators");
            string voxelsPath = Path.Combine(newWorldPath, "Voxels");
            Directory.CreateDirectory(biomesPath);
            Directory.CreateDirectory(generatorsPath);
            Directory.CreateDirectory(voxelsPath);

            AssetDatabase.Refresh();

            // Create a sample voxel definition
            VoxelDefinition sampleVoxel = Instantiate(env.defaultVoxel);
            sampleVoxel.name = sampleVoxel.title = "Sample Voxel";
            sampleVoxel.doNotSave = false;
            sampleVoxel.canBeCollected = true;

            // Assign white texture to all sides
            Texture2D whiteTexture = Resources.Load<Texture2D>("VoxelPlay/Textures/whiteVoxelTexture");
            sampleVoxel.textureTop = whiteTexture;
            sampleVoxel.textureSide = whiteTexture;
            sampleVoxel.textureBottom = whiteTexture;
            sampleVoxel.textureLeft = whiteTexture;
            sampleVoxel.textureRight = whiteTexture;
            sampleVoxel.textureForward = whiteTexture;
            sampleVoxel.textureSample = whiteTexture;
            sampleVoxel.icon = whiteTexture;

            // Save the voxel definition
            string voxelPath = Path.Combine(voxelsPath, "SampleVoxel.asset");
            AssetDatabase.CreateAsset(sampleVoxel, voxelPath);

            // Create a simple biome definition
            BiomeDefinition biome = CreateInstance<BiomeDefinition>();
            biome.name = "Biome";
            biome.voxelTop = sampleVoxel;
            biome.voxelDirt = sampleVoxel;
            biome.voxelLakeBed = sampleVoxel;

            BiomeZone zone = new BiomeZone();
            zone.altitudeMin = 0;
            zone.altitudeMax = 255;
            zone.moistureMin = 0;
            zone.moistureMax = 1;
            biome.zones = new BiomeZone[] { zone };
            string biomePath = Path.Combine(biomesPath, "Biome.asset");
            AssetDatabase.CreateAsset(biome, biomePath);

            // Create a terrain generator
            TerrainDefaultGenerator terrainGen = CreateInstance<TerrainDefaultGenerator>();
            terrainGen.name = "TerrainGenerator";
            terrainGen.maxHeight = 255;
            terrainGen.waterLevel = 0;

            // Add a single step to the terrain generator
            StepData step = new StepData {
                enabled = true,
                operation = TerrainStepType.Constant,
                description = "Flat terrain",
                param = 0.1f // For Constant operation, the value is stored in param
            };
            terrainGen.Steps = new StepData[] { step };

            string terrainGenPath = Path.Combine(generatorsPath, "TerrainGenerator.asset");
            AssetDatabase.CreateAsset(terrainGen, terrainGenPath);

            // Create the world definition and link everything
            WorldDefinition wd = CreateInstance<WorldDefinition>();
            wd.name = newWorldName;
            wd.seed = UnityEngine.Random.Range(1, 10000);
            wd.terrainGenerator = terrainGen;
            wd.biomes = new BiomeDefinition[] { biome };
            wd.infinite = false; // Make the world finite
            wd.extents = new Vector3(64, 128, 64); // Set world size to 64x64 (X and Z)
            wd.voxelDamageTextures = new Texture2D[5] {
                Resources.Load<Texture2D>("VoxelPlay/Defaults/FX/voxelDamage1"),
                Resources.Load<Texture2D>("VoxelPlay/Defaults/FX/voxelDamage2"),
                Resources.Load<Texture2D>("VoxelPlay/Defaults/FX/voxelDamage3"),
                Resources.Load<Texture2D>("VoxelPlay/Defaults/FX/voxelDamage4"),
                Resources.Load<Texture2D>("VoxelPlay/Defaults/FX/voxelDamage5"),
            };

            string worldDefPath = Path.Combine(newWorldPath, newWorldName + ".asset");
            AssetDatabase.CreateAsset(wd, worldDefPath);

            // Save all assets
            AssetDatabase.SaveAssets();

            // Assign the world definition to the environment
            world.objectReferenceValue = wd;
            renderInEditor.boolValue = true;
            serializedObject.ApplyModifiedProperties();

            // Ping the world definition in the Project window
            EditorGUIUtility.PingObject(wd);

            // Focus the SceneView camera on the world
            if (SceneView.lastActiveSceneView != null) {
                // Get the world root transform
                Transform worldRoot = env.worldRoot;
                if (worldRoot != null) {
                    // Position the scene view camera to look at the world center
                    Vector3 targetPosition = worldRoot.position;
                    SceneView.lastActiveSceneView.LookAt(
                        targetPosition,
                        SceneView.lastActiveSceneView.rotation,
                        wd.extents.magnitude * 0.5f
                    );
                    SceneView.lastActiveSceneView.Repaint();
                }
            }

            Debug.Log("Created new world definition at " + worldDefPath);
        }


        string GetShaderOptionValue (string option, string file) {
            string[] res = Directory.GetFiles(Application.dataPath, file, SearchOption.AllDirectories);
            string path = null;
            for (int k = 0; k < res.Length; k++) {
                if (res[k].Contains("Voxel Play")) {
                    path = res[k];
                    break;
                }
            }
            if (path == null) {
                Debug.LogError(file + " could not be found!");
                return "";
            }

            string[] code = File.ReadAllLines(path, System.Text.Encoding.UTF8);
            string searchToken = "#define " + option;
            for (int k = 0; k < code.Length; k++) {
                if (code[k].Contains(searchToken)) {
                    string[] values = code[k].Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length == 3) {
                        return values[2];
                    }
                    break;
                }
            }
            return "";
        }

        void SetShaderOptionValue (string option, string file, string value) {
            string[] res = Directory.GetFiles(Application.dataPath, file, SearchOption.AllDirectories);
            string path = null;
            for (int k = 0; k < res.Length; k++) {
                if (res[k].Contains("Voxel Play")) {
                    path = res[k];
                    break;
                }
            }
            if (path == null) {
                Debug.LogError(file + " could not be found!");
                return;
            }

            string[] code = File.ReadAllLines(path, System.Text.Encoding.UTF8);
            string searchToken = "#define " + option;
            for (int k = 0; k < code.Length; k++) {
                if (code[k].Contains(searchToken)) {
                    code[k] = "#define " + option + " " + value;
                    File.WriteAllLines(path, code, System.Text.Encoding.UTF8);
                    break;
                }
            }
        }

        public void UpdateCurvatureMacro () {
            env.SetShaderOptionValue("VOXELPLAY_CURVATURE", "VPCommonVertexModifier.cginc", enableCurvature.boolValue ? "1" : "0");
            env.SetShaderOptionValue("VOXELPLAY_CURVATURE_AMOUNT", "VPCommonVertexModifier.cginc", curvatureAmount);
            Debug.Log("Voxel Play shaders updated.");
            AssetDatabase.Refresh();
        }

        void CheckMainLightShadows () {
            Light[] lights = Misc.FindObjectsOfType<Light>();
            for (int k = 0; k < lights.Length; k++) {
                if (lights[k].isActiveAndEnabled && lights[k].shadows != LightShadows.None) {
                    EditorGUILayout.HelpBox("Light '" + lights[k].name + "' currently is configured to cast shadows. Consider disabling shadows on your lights as well to improve performance.", MessageType.Info);
                }
            }
        }

        void ChangeChunkSize () {
            if (!EditorUtility.DisplayDialog("Change Chunk Size", "Please note that saved games with different chunk sizes cannot be loaded.\nThe view distance and chunk pool size will be adjusted to reflect the new chunk size.\n\nDo you want to change the chunk size? (it won't modify any saved game).", "Yes", "No")) {
                return;
            }
            int newVisibleDistance = visibleChunksDistance.intValue * VoxelPlayEnvironment.CHUNK_SIZE / chunkNewSize;
            newVisibleDistance = Mathf.Clamp(newVisibleDistance, 1, 25);
            visibleChunksDistance.intValue = newVisibleDistance;
            maxChunks.intValue = env.maxChunksRecommended;
            serializedObject.ApplyModifiedProperties();
            env.UpdateChunkSizeInCode(chunkNewSize);
            Debug.Log("New chunk size updated.");
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
        }


        void ChangeMicroVoxelsSize () {
            if (!EditorUtility.DisplayDialog("Change Micro Voxels Size", "Please note that saved games with different microvoxels sizes cannot be loaded.\nDo you want to change the microvoxels size?", "Yes", "No")) {
                return;
            }
            env.UpdateMicroVoxelsSizeInCode(microVoxelsNewSize);
            Debug.Log("New microvoxels size updated.");
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
        }


        void ChangeMaxMaterialsPerChunk () {
            if (maxMaterialsPerChunk < 16) {
                EditorUtility.DisplayDialog("Change Max Materials Per Chunk", "Minimum material count is 16.", "Ok");
                return;
            }
            if (!EditorUtility.DisplayDialog("Change Max Materials Per Chunk", "Please note that increasing the number of materials per chunk increases memory usage and can affect performance..\n\nDo you want to change the maximum material count per chunk?", "Yes", "No")) {
                return;
            }
            env.UpdateMaxMaterialsPerChunk(maxMaterialsPerChunk);
            Debug.Log("Max Materials Per Chunk updated.");
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
        }



        void ChangeVoxelPadding () {
            env.UpdateVoxelPadding(voxelPadding);
            Debug.Log("Voxel Padding updated.");
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
        }


        void GenerateEditorArea () {
            if (!EditorUtility.DisplayDialog("Generate Chunks In Area", "Warning: chunks in area of size " + renderInEditorAreaSize.vector3Value + " with center at " + renderInEditorAreaCenter.vector3Value + " will be generated now.\n\nConfirm?", "Yes", "Cancel")) return;

            Vector3 sizeInChunks = renderInEditorAreaSize.vector3Value / VoxelPlayEnvironment.CHUNK_SIZE;
            int totalChunks = (int)(sizeInChunks.x * sizeInChunks.y * sizeInChunks.z);
            if (totalChunks > env.maxChunks) {
                EditorUtility.DisplayDialog("Max Chunks Exceeded!", "Total chunks to be generated (" + totalChunks + ") exceeds current pool size. Increase pool size or reduce size of area to be generated.", "Ok");
                return;
            }

            env.ReloadWorld(keepWorldChanges: false);
            env.ChunkCheckArea(renderInEditorAreaCenter.vector3Value, sizeInChunks / 2f, renderChunks: true);
            env.CompleteWork();
        }

        #region SRP utils

        void CheckDepthPrimingMode () {
            RenderPipelineAsset pipe = GraphicsSettings.currentRenderPipeline;
            if (pipe == null) return;
            // Check depth priming mode
            FieldInfo renderers = pipe.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (renderers == null) return;
            foreach (var renderer in (object[])renderers.GetValue(pipe)) {
                if (renderer == null) continue;
                FieldInfo depthPrimingModeField = renderer.GetType().GetField("m_DepthPrimingMode", BindingFlags.NonPublic | BindingFlags.Instance);
                int depthPrimingMode = -1;
                if (depthPrimingModeField != null) {
                    depthPrimingMode = (int)depthPrimingModeField.GetValue(renderer);
                }

                FieldInfo renderingModeField = renderer.GetType().GetField("m_RenderingMode", BindingFlags.NonPublic | BindingFlags.Instance);
                int renderingMode = -1;
                if (renderingModeField != null) {
                    renderingMode = (int)renderingModeField.GetValue(renderer);
                }

                if (depthPrimingMode > 0 && renderingMode != 1) {
                    EditorGUILayout.HelpBox("Depth Priming Mode in URP asset must be disabled.", MessageType.Warning);
                    if (GUILayout.Button("Show Pipeline Asset")) {
                        Selection.activeObject = (UnityEngine.Object)renderer;
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.Separator();
                }
            }
        }
        #endregion

    }

}
