using UnityEngine;
using UnityEditor;


namespace VoxelPlay {

    [CustomEditor(typeof(VoxelDefinition), isFallback = true)]
    [CanEditMultipleObjects]
    public class VoxelDefinitionEditor : Editor {

        protected SerializedProperty title, renderType, opaque;
        protected SerializedProperty occludesTop, occludesBottom, occludesForward, occludesBack, occludesLeft, occludesRight;
        protected SerializedProperty overrideMaterial, overrideMaterialNonGeo, overrideMaterialGreedyMeshing, texturesByMaterial;
        protected SerializedProperty texturesCustomPacking, texturesPackingSize, texturesPackingScale, texturesPackingNormalMap, texturesPackingReliefMap, texturesPackingPBRMap;
        protected SerializedProperty textureTop, textureTopEmission, textureTopNRM, textureTopDISP, textureTopMetallic, textureTopSmoothness, textureTopOcclusion, textureTopOpacity;
        protected SerializedProperty textureSide, textureSideEmission, textureSideNRM, textureSideDISP, textureSideMetallic, textureSideSmoothness, textureSideOcclusion, textureSideOpacity;
        protected SerializedProperty textureRight, textureRightEmission, textureRightNRM, textureRightDISP, textureRightMetallic, textureRightSmoothness, textureRightOcclusion, textureRightOpacity;
        protected SerializedProperty textureForward, textureForwardEmission, textureForwardNRM, textureForwardDISP, textureForwardMetallic, textureForwardSmoothness, textureForwardOcclusion, textureForwardOpacity;
        protected SerializedProperty textureLeft, textureLeftEmission, textureLeftNRM, textureLeftDISP, textureLeftMetallic, textureLeftSmoothness, textureLeftOcclusion, textureLeftOpacity;
        protected SerializedProperty textureBottom, textureBottomEmission, textureBottomNRM, textureBottomDISP, textureBottomMetallic, textureBottomSmoothness, textureBottomOcclusion, textureBottomOpacity;
        protected SerializedProperty metallicStrengthTop, smoothnessStrengthTop, occlusionStrengthTop;
        protected SerializedProperty metallicStrengthSide, smoothnessStrengthSide, occlusionStrengthSide;
        protected SerializedProperty metallicStrengthRight, smoothnessStrengthRight, occlusionStrengthRight;
        protected SerializedProperty metallicStrengthForward, smoothnessStrengthForward, occlusionStrengthForward;
        protected SerializedProperty metallicStrengthLeft, smoothnessStrengthLeft, occlusionStrengthLeft;
        protected SerializedProperty metallicStrengthBottom, smoothnessStrengthBottom, occlusionStrengthBottom;
        protected SerializedProperty normalStrengthTop, displacementStrengthTop;
        protected SerializedProperty normalStrengthSide, displacementStrengthSide;
        protected SerializedProperty normalStrengthRight, displacementStrengthRight;
        protected SerializedProperty normalStrengthForward, displacementStrengthForward;
        protected SerializedProperty normalStrengthLeft, displacementStrengthLeft;
        protected SerializedProperty normalStrengthBottom, displacementStrengthBottom;
        protected SerializedProperty useRoughnessTop, useRoughnessSide, useRoughnessRight, useRoughnessForward, useRoughnessLeft, useRoughnessBottom;
        protected SerializedProperty showFoam, tintColor, colorVariation, alpha;
        protected SerializedProperty vegetationMinHeight, vegetationMaxHeight;
        protected SerializedProperty pickupSound, buildSound, footfalls, jumpSound, landingSound, impactSound, destructionSound;
        protected SerializedProperty showDamageCracks, resistancePoints, canBeCollected, dropProbability, hidden, dropItem, dropItemLifeTime, dropItemScale;
        protected SerializedProperty icon, textureSample, overrideMainTexture, overrideMainTextureOffset, triggerCollapse, willCollapse, navigatable, denseLeaves, windAnimation, destroyWithVoxelBelow;
        protected SerializedProperty model, prefabMaterial, gpuInstancing, castShadows, receiveShadows, createGameObject, generateCollider, generateNavMesh;
        protected SerializedProperty offset, offsetRandom, offsetRandomRange, offsetRandomVegetation, scale, rotation, rotationRandomY, promotesTo, replacedBy;
        protected SerializedProperty spreads, drains, spreadDelay, spreadDelayRandom, spreadReplaceThreshold, supportsBevel, diveColor, height;
        protected SerializedProperty playerDamage, playerDamageDelay, ignoresRayCast, highlightOffset;
        protected SerializedProperty placeOnWall, placeFacingPlayer, allowsTextureRotation;
        protected SerializedProperty triggerEnterEvent, triggerWalkEvent;
        protected SerializedProperty seeThroughMode, seeThroughVoxel;
        protected SerializedProperty animationSpeed, animationTextures;
        protected SerializedProperty generateColliders, lightIntensity, computeLighting;
        protected SerializedProperty allowUpsideDownVoxel, upsideDownVoxel, isUpsideDown;
        protected SerializedProperty emissionStrengthTop, emissionStrengthSide, emissionStrengthRight, emissionStrengthForward, emissionStrengthLeft, emissionStrengthBottom;
        protected SerializedProperty opacityStrengthTop, opacityStrengthSide, opacityStrengthRight, opacityStrengthForward, opacityStrengthLeft, opacityStrengthBottom;

        protected SerializedProperty materialProfile, syncMode;
        protected MaterialFieldCache materialFields;
        static bool materialProfileExpanded = true;

        static bool soundEffectsExpanded = true;
        static bool specialEventsExpanded;
        static bool inventoryExpanded = true;
        static bool placementExpanded = true;
        static bool otherAttributesExpanded = true;
        static bool microVoxelToolsExpanded = true;
        static bool microVoxelCustomHeightMode;
        static bool microVoxelBottomSlabMode;
        static bool microVoxelTopSlabMode;
        static int microVoxelHeight = MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
        GUIContent microVoxelIconClear;
        GUIContent microVoxelIconBottomSlab;
        GUIContent microVoxelIconTopSlab;
        GUIContent microVoxelIconCustomHeight;

        protected readonly GUIContent[] renderTypesNames = {
            new GUIContent ("Opaque 3 Textures (no ambient occlusion)"),
            new GUIContent ("Opaque 3 Textures (with ambient occlusion)"),
            new GUIContent ("Opaque 3 Textures (with ambient occlusion and animation)"),
            new GUIContent ("Opaque 6 Textures (with ambient occlusion)"),
            new GUIContent ("Transparent 6 textures"),
            new GUIContent ("Cutout"),
            new GUIContent ("Fluid"),
            new GUIContent ("Water"),
            new GUIContent ("Cloud (scaled x4, no AO, no collider)"),
            new GUIContent ("Vegetation (Cutout Cross)"),
            new GUIContent ("Custom (prefab)"),
            new GUIContent ("Invisible"),
        };

        protected readonly int[] renderTypesValues = {
            (int)RenderType.OpaqueNoAO,
            (int)RenderType.Opaque,
            (int)RenderType.OpaqueAnimated,
            (int)RenderType.Opaque6tex,
            (int)RenderType.Transp6tex,
            (int)RenderType.Cutout,
            (int)RenderType.Fluid,
            (int)RenderType.Water,
            (int)RenderType.Cloud,
            (int)RenderType.CutoutCross,
            (int)RenderType.Custom,
            (int)RenderType.Invisible,
        };

        protected Color titleColor;
        protected static GUIStyle titleLabelStyle;
        protected static GUIStyle sectionHeaderStyle;
        protected VoxelPlayEnvironment _env;
        protected Editor meshPreviewEditor;
        protected bool gpuInstancingMaterialSupportChecked;
        protected bool showGPUInstancingWarning;
        protected Material prefabCachedMaterial;

        protected virtual void OnEnable () {
            titleColor = EditorGUIUtility.isProSkin ? new Color(0.52f, 0.66f, 0.9f) : new Color(0.12f, 0.16f, 0.4f);

            title = serializedObject.FindProperty("title");
            renderType = serializedObject.FindProperty("renderType");
            overrideMaterial = serializedObject.FindProperty("overrideMaterial");
            overrideMaterialNonGeo = serializedObject.FindProperty("overrideMaterialNonGeo");
            overrideMaterialGreedyMeshing = serializedObject.FindProperty("overrideMaterialGreedyMeshing");
            texturesByMaterial = serializedObject.FindProperty("texturesByMaterial");

            texturesCustomPacking = serializedObject.FindProperty("texturesCustomPacking");
            texturesPackingSize = serializedObject.FindProperty("texturesPackingSize");
            texturesPackingScale = serializedObject.FindProperty("texturesPackingScale");
            texturesPackingNormalMap = serializedObject.FindProperty("texturesPackingNormalMap");
            texturesPackingReliefMap = serializedObject.FindProperty("texturesPackingReliefMap");
            texturesPackingPBRMap = serializedObject.FindProperty("texturesPackingPBRMap");

            opaque = serializedObject.FindProperty("opaque");
            occludesTop = serializedObject.FindProperty("occludesTop");
            occludesBottom = serializedObject.FindProperty("occludesBottom");
            occludesForward = serializedObject.FindProperty("occludesForward");
            occludesBack = serializedObject.FindProperty("occludesBack");
            occludesLeft = serializedObject.FindProperty("occludesLeft");
            occludesRight = serializedObject.FindProperty("occludesRight");

            textureTop = serializedObject.FindProperty("textureTop");
            textureTopEmission = serializedObject.FindProperty("textureTopEmission");
            textureTopNRM = serializedObject.FindProperty("textureTopNRM");
            textureTopDISP = serializedObject.FindProperty("textureTopDISP");
            textureTopMetallic = serializedObject.FindProperty("textureTopMetallic");
            textureTopSmoothness = serializedObject.FindProperty("textureTopSmoothness");
            textureTopOcclusion = serializedObject.FindProperty("textureTopOcclusion");
            textureTopOpacity = serializedObject.FindProperty("textureTopOpacity");
            textureSide = serializedObject.FindProperty("textureSide");
            textureSideEmission = serializedObject.FindProperty("textureSideEmission");
            textureSideNRM = serializedObject.FindProperty("textureSideNRM");
            textureSideDISP = serializedObject.FindProperty("textureSideDISP");
            textureSideMetallic = serializedObject.FindProperty("textureSideMetallic");
            textureSideSmoothness = serializedObject.FindProperty("textureSideSmoothness");
            textureSideOcclusion = serializedObject.FindProperty("textureSideOcclusion");
            textureSideOpacity = serializedObject.FindProperty("textureSideOpacity");
            textureRight = serializedObject.FindProperty("textureRight");
            textureRightEmission = serializedObject.FindProperty("textureRightEmission");
            textureRightNRM = serializedObject.FindProperty("textureRightNRM");
            textureRightDISP = serializedObject.FindProperty("textureRightDISP");
            textureRightMetallic = serializedObject.FindProperty("textureRightMetallic");
            textureRightSmoothness = serializedObject.FindProperty("textureRightSmoothness");
            textureRightOcclusion = serializedObject.FindProperty("textureRightOcclusion");
            textureRightOpacity = serializedObject.FindProperty("textureRightOpacity");
            textureForward = serializedObject.FindProperty("textureForward");
            textureForwardEmission = serializedObject.FindProperty("textureForwardEmission");
            textureForwardNRM = serializedObject.FindProperty("textureForwardNRM");
            textureForwardDISP = serializedObject.FindProperty("textureForwardDISP");
            textureForwardMetallic = serializedObject.FindProperty("textureForwardMetallic");
            textureForwardSmoothness = serializedObject.FindProperty("textureForwardSmoothness");
            textureForwardOcclusion = serializedObject.FindProperty("textureForwardOcclusion");
            textureForwardOpacity = serializedObject.FindProperty("textureForwardOpacity");
            textureLeft = serializedObject.FindProperty("textureLeft");
            textureLeftEmission = serializedObject.FindProperty("textureLeftEmission");
            textureLeftNRM = serializedObject.FindProperty("textureLeftNRM");
            textureLeftDISP = serializedObject.FindProperty("textureLeftDISP");
            textureLeftMetallic = serializedObject.FindProperty("textureLeftMetallic");
            textureLeftSmoothness = serializedObject.FindProperty("textureLeftSmoothness");
            textureLeftOcclusion = serializedObject.FindProperty("textureLeftOcclusion");
            textureLeftOpacity = serializedObject.FindProperty("textureLeftOpacity");
            textureBottom = serializedObject.FindProperty("textureBottom");
            textureBottomEmission = serializedObject.FindProperty("textureBottomEmission");
            textureBottomNRM = serializedObject.FindProperty("textureBottomNRM");
            textureBottomDISP = serializedObject.FindProperty("textureBottomDISP");
            textureBottomMetallic = serializedObject.FindProperty("textureBottomMetallic");
            textureBottomSmoothness = serializedObject.FindProperty("textureBottomSmoothness");
            textureBottomOcclusion = serializedObject.FindProperty("textureBottomOcclusion");
            textureBottomOpacity = serializedObject.FindProperty("textureBottomOpacity");

            opacityStrengthTop = serializedObject.FindProperty("opacityStrengthTop");
            opacityStrengthSide = serializedObject.FindProperty("opacityStrengthSide");
            opacityStrengthRight = serializedObject.FindProperty("opacityStrengthRight");
            opacityStrengthForward = serializedObject.FindProperty("opacityStrengthForward");
            opacityStrengthLeft = serializedObject.FindProperty("opacityStrengthLeft");
            opacityStrengthBottom = serializedObject.FindProperty("opacityStrengthBottom");

            showFoam = serializedObject.FindProperty("showFoam");
            tintColor = serializedObject.FindProperty("tintColor");
            colorVariation = serializedObject.FindProperty("colorVariation");
            alpha = serializedObject.FindProperty("alpha");
            vegetationMinHeight = serializedObject.FindProperty("vegetationMinHeight");
            vegetationMaxHeight = serializedObject.FindProperty("vegetationMaxHeight");

            pickupSound = serializedObject.FindProperty("pickupSound");
            buildSound = serializedObject.FindProperty("buildSound");
            footfalls = serializedObject.FindProperty("footfalls");
            jumpSound = serializedObject.FindProperty("jumpSound");
            landingSound = serializedObject.FindProperty("landingSound");
            impactSound = serializedObject.FindProperty("impactSound");
            destructionSound = serializedObject.FindProperty("destructionSound");
            resistancePoints = serializedObject.FindProperty("resistancePoints");
            showDamageCracks = serializedObject.FindProperty("showDamageCracks");

            canBeCollected = serializedObject.FindProperty("canBeCollected");
            dropProbability = serializedObject.FindProperty("dropProbability");
            hidden = serializedObject.FindProperty("hidden");
            dropItem = serializedObject.FindProperty("dropItem");
            dropItemLifeTime = serializedObject.FindProperty("dropItemLifeTime");
            dropItemScale = serializedObject.FindProperty("dropItemScale");
            icon = serializedObject.FindProperty("icon");
            textureSample = serializedObject.FindProperty("textureSample");
            overrideMainTexture = serializedObject.FindProperty("overrideMainTexture");
            overrideMainTextureOffset = serializedObject.FindProperty("overrideMainTextureOffset");
            navigatable = serializedObject.FindProperty("navigatable");
            denseLeaves = serializedObject.FindProperty("denseLeaves");
            windAnimation = serializedObject.FindProperty("windAnimation");
            destroyWithVoxelBelow = serializedObject.FindProperty("destroyWithVoxelBelow");
            model = serializedObject.FindProperty("model");
            prefabMaterial = serializedObject.FindProperty("prefabMaterial");
            gpuInstancing = serializedObject.FindProperty("gpuInstancing");
            castShadows = serializedObject.FindProperty("castShadows");
            receiveShadows = serializedObject.FindProperty("receiveShadows");
            createGameObject = serializedObject.FindProperty("createGameObject");
            generateCollider = serializedObject.FindProperty("generateCollider");
            generateNavMesh = serializedObject.FindProperty("generateNavMesh");
            offset = serializedObject.FindProperty("offset");
            offsetRandom = serializedObject.FindProperty("offsetRandom");
            offsetRandomRange = serializedObject.FindProperty("offsetRandomRange");
            offsetRandomVegetation = serializedObject.FindProperty("offsetRandomVegetation");
            scale = serializedObject.FindProperty("scale");
            rotation = serializedObject.FindProperty("rotation");
            rotationRandomY = serializedObject.FindProperty("rotationRandomY");
            promotesTo = serializedObject.FindProperty("promotesTo");
            replacedBy = serializedObject.FindProperty("replacedBy");
            triggerCollapse = serializedObject.FindProperty("triggerCollapse");
            willCollapse = serializedObject.FindProperty("willCollapse");

            spreads = serializedObject.FindProperty("spreads");
            drains = serializedObject.FindProperty("drains");
            spreadDelay = serializedObject.FindProperty("spreadDelay");
            spreadDelayRandom = serializedObject.FindProperty("spreadDelayRandom");
            spreadReplaceThreshold = serializedObject.FindProperty("spreadReplaceThreshold");

            supportsBevel = serializedObject.FindProperty("supportsBevel");
            diveColor = serializedObject.FindProperty("diveColor");
            height = serializedObject.FindProperty("height");

            playerDamage = serializedObject.FindProperty("playerDamage");
            playerDamageDelay = serializedObject.FindProperty("playerDamageDelay");
            ignoresRayCast = serializedObject.FindProperty("ignoresRayCast");
            generateColliders = serializedObject.FindProperty("generateColliders");
            highlightOffset = serializedObject.FindProperty("highlightOffset");

            allowsTextureRotation = serializedObject.FindProperty("allowsTextureRotation");
            placeOnWall = serializedObject.FindProperty("placeOnWall");
            placeFacingPlayer = serializedObject.FindProperty("placeFacingPlayer");

            triggerEnterEvent = serializedObject.FindProperty("triggerEnterEvent");
            triggerWalkEvent = serializedObject.FindProperty("triggerWalkEvent");

            animationSpeed = serializedObject.FindProperty("animationSpeed");
            animationTextures = serializedObject.FindProperty("animationTextures");

            seeThroughMode = serializedObject.FindProperty("seeThroughMode");
            seeThroughVoxel = serializedObject.FindProperty("seeThroughVoxel");

            lightIntensity = serializedObject.FindProperty("lightIntensity");
            computeLighting = serializedObject.FindProperty("computeLighting");

            allowUpsideDownVoxel = serializedObject.FindProperty("allowUpsideDownVoxel");
            upsideDownVoxel = serializedObject.FindProperty("upsideDownVoxel");
            isUpsideDown = serializedObject.FindProperty("isUpsideDown");

            emissionStrengthTop = serializedObject.FindProperty("emissionStrengthTop");
            emissionStrengthSide = serializedObject.FindProperty("emissionStrengthSide");
            emissionStrengthRight = serializedObject.FindProperty("emissionStrengthRight");
            emissionStrengthForward = serializedObject.FindProperty("emissionStrengthForward");
            emissionStrengthLeft = serializedObject.FindProperty("emissionStrengthLeft");
            emissionStrengthBottom = serializedObject.FindProperty("emissionStrengthBottom");

            metallicStrengthTop = serializedObject.FindProperty("metallicStrengthTop");
            smoothnessStrengthTop = serializedObject.FindProperty("smoothnessStrengthTop");
            occlusionStrengthTop = serializedObject.FindProperty("occlusionStrengthTop");
            metallicStrengthSide = serializedObject.FindProperty("metallicStrengthSide");
            smoothnessStrengthSide = serializedObject.FindProperty("smoothnessStrengthSide");
            occlusionStrengthSide = serializedObject.FindProperty("occlusionStrengthSide");
            metallicStrengthRight = serializedObject.FindProperty("metallicStrengthRight");
            smoothnessStrengthRight = serializedObject.FindProperty("smoothnessStrengthRight");
            occlusionStrengthRight = serializedObject.FindProperty("occlusionStrengthRight");
            metallicStrengthForward = serializedObject.FindProperty("metallicStrengthForward");
            smoothnessStrengthForward = serializedObject.FindProperty("smoothnessStrengthForward");
            occlusionStrengthForward = serializedObject.FindProperty("occlusionStrengthForward");
            metallicStrengthLeft = serializedObject.FindProperty("metallicStrengthLeft");
            smoothnessStrengthLeft = serializedObject.FindProperty("smoothnessStrengthLeft");
            occlusionStrengthLeft = serializedObject.FindProperty("occlusionStrengthLeft");
            metallicStrengthBottom = serializedObject.FindProperty("metallicStrengthBottom");
            smoothnessStrengthBottom = serializedObject.FindProperty("smoothnessStrengthBottom");
            occlusionStrengthBottom = serializedObject.FindProperty("occlusionStrengthBottom");

            normalStrengthTop = serializedObject.FindProperty("normalStrengthTop");
            displacementStrengthTop = serializedObject.FindProperty("displacementStrengthTop");
            normalStrengthSide = serializedObject.FindProperty("normalStrengthSide");
            displacementStrengthSide = serializedObject.FindProperty("displacementStrengthSide");
            normalStrengthRight = serializedObject.FindProperty("normalStrengthRight");
            displacementStrengthRight = serializedObject.FindProperty("displacementStrengthRight");
            normalStrengthForward = serializedObject.FindProperty("normalStrengthForward");
            displacementStrengthForward = serializedObject.FindProperty("displacementStrengthForward");
            normalStrengthLeft = serializedObject.FindProperty("normalStrengthLeft");
            displacementStrengthLeft = serializedObject.FindProperty("displacementStrengthLeft");
            normalStrengthBottom = serializedObject.FindProperty("normalStrengthBottom");
            displacementStrengthBottom = serializedObject.FindProperty("displacementStrengthBottom");

            useRoughnessTop = serializedObject.FindProperty("useRoughnessTop");
            useRoughnessSide = serializedObject.FindProperty("useRoughnessSide");
            useRoughnessRight = serializedObject.FindProperty("useRoughnessRight");
            useRoughnessForward = serializedObject.FindProperty("useRoughnessForward");
            useRoughnessLeft = serializedObject.FindProperty("useRoughnessLeft");
            useRoughnessBottom = serializedObject.FindProperty("useRoughnessBottom");

            materialProfile = serializedObject.FindProperty("materialProfile");
            syncMode = serializedObject.FindProperty("syncMode");

            materialFields = new MaterialFieldCache();
            materialFields.Init(serializedObject);

            SetupMicroVoxelIcons();

            if (target is VoxelDefinition vdTarget) {
                SyncCustomHeightWithData(vdTarget.microVoxels);
            }
        }

        protected virtual void OnDestroy () {
            try {
                if (meshPreviewEditor != null) {
                    DestroyImmediate(meshPreviewEditor);
                }
            }
            catch { }
        }

        public override void OnInspectorGUI () {

            serializedObject.UpdateIfRequiredOrScript();
            if (titleLabelStyle == null) {
                titleLabelStyle = new GUIStyle(EditorStyles.label);
            }
            titleLabelStyle.normal.textColor = titleColor;
            titleLabelStyle.fontStyle = FontStyle.Bold;
            if (sectionHeaderStyle == null) {
                sectionHeaderStyle = new GUIStyle(GUI.skin.button);
                sectionHeaderStyle.alignment = TextAnchor.MiddleLeft;
                sectionHeaderStyle.fontStyle = FontStyle.Bold;
                sectionHeaderStyle.fixedHeight = EditorGUIUtility.singleLineHeight + 6f;
            }

            EditorGUILayout.Separator();
            GUILayout.Label(new GUIContent(" Rendering", EditorGUIUtility.IconContent("d_Mesh Icon")?.image), sectionHeaderStyle);
            EditorGUILayout.IntPopup(renderType, renderTypesNames, renderTypesValues);
            RenderType rt = (RenderType)renderType.intValue;
            EditorGUILayout.HelpBox(GetRenderTypeDescription(rt), MessageType.Info);

            // ---- Material Profile Section ----
            bool isAutoSync = false;
            DrawMaterialProfileSection(rt, ref isAutoSync);

            // ---- renderType compatibility check when Auto ----
            if (isAutoSync && !VoxelMaterialDrawer.IsProfileSupported(rt)) {
                Undo.RecordObjects(targets, "Switch Material Profile to Manual");
                foreach (Object obj in targets) {
                    var vd = obj as VoxelDefinition;
                    if (vd != null && vd.syncMode == SyncMode.Auto) {
                        vd.syncMode = SyncMode.Manual;
                        EditorUtility.SetDirty(vd);
                    }
                }
                syncMode.intValue = (int)SyncMode.Manual;
                isAutoSync = false;
                EditorGUILayout.HelpBox("Sync mode switched to Manual because " + rt + " is not compatible with material profiles.", MessageType.Info);
            }

            // ---- Material Fields (non-Custom, non-Invisible) ----
            if (rt != RenderType.Custom && rt != RenderType.Invisible) {

                // When auto-sync, wrap material fields in a collapsible box
                if (isAutoSync) {
                    materialProfileExpanded = EditorGUILayout.Foldout(materialProfileExpanded, "Material Profile Properties", true);
                    if (materialProfileExpanded) {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    }
                }

                if (!isAutoSync || materialProfileExpanded) {
                    bool prevEnabled = GUI.enabled;
                    if (isAutoSync) GUI.enabled = false;
                    VoxelMaterialDrawer.DrawMaterialOverrideSection(materialFields, env, rt);

                    // textureSample autofill (skip when Auto)
                    if (!isAutoSync && textureSample.objectReferenceValue == null && textureSide.objectReferenceValue != null) {
                        textureSample.objectReferenceValue = textureSide.objectReferenceValue;
                    }

                    // CutoutCross: offsetRandomVegetation before textures (not part of material profile, always editable)
                    if (rt == RenderType.CutoutCross) {
                        if (isAutoSync) GUI.enabled = prevEnabled;
                        EditorGUILayout.PropertyField(offsetRandomVegetation, new GUIContent("Random Offset", "If offset should be modified randomly to create variation for vegetation"));
                        if (isAutoSync) GUI.enabled = false;
                    }

                    bool showTextureFields = !overrideMaterial.boolValue || !texturesByMaterial.boolValue;
                    System.Action autopickAction = (!isAutoSync && showTextureFields) ? () => {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("Autopick Textures", "Automatically find suitable textures and assign them based on their filenames."), GUILayout.MaxWidth(160f))) {
                            Undo.RecordObjects(targets, "Autopick Textures");
                            AutoPickTextures();
                        }
                        EditorGUILayout.EndHorizontal();
                    } : (System.Action)null;
                    VoxelMaterialDrawer.DrawTextureAndColorFields(materialFields, env, rt, isAutoSync, onBeforeTextures: autopickAction);
                    if (isAutoSync) GUI.enabled = prevEnabled;
                }

                if (isAutoSync && materialProfileExpanded) {
                    EditorGUILayout.EndVertical();
                }

                // Vegetation height variation (not part of material profile)
                if (rt == RenderType.Cutout || rt == RenderType.CutoutCross) {
                    EditorGUILayout.BeginHorizontal();
                    float min = vegetationMinHeight.floatValue;
                    float max = vegetationMaxHeight.floatValue;
                    EditorGUILayout.MinMaxSlider(new GUIContent("Height Variation"), ref min, ref max, 0f, 5f);
                    vegetationMinHeight.floatValue = min;
                    vegetationMaxHeight.floatValue = max;
                    GUILayout.Label(vegetationMinHeight.floatValue.ToString("F2") + " - " + vegetationMaxHeight.floatValue.ToString("F2"));
                    EditorGUILayout.EndHorizontal();
                }
            }

            // ---- Custom render type (not profile-managed) ----
            if (rt == RenderType.Custom) {
                EditorGUILayout.PropertyField(model, new GUIContent("Prefab", "Assign a prefab. Make sure your prefab uses a valid material (you can copy one of the VP Model * materials provided with Voxel Play). Please check the documentation for details."));
                EditorGUILayout.PropertyField(prefabMaterial, new GUIContent("Material", "The material to use when rendering the custom voxel. You can use the material provided by the prefab or use one of the optimized materials provided by Voxel Play."));
                if (prefabMaterial.intValue != (int)CustomVoxelMaterial.PrefabMaterial) {
                    EditorGUILayout.HelpBox("Existing material will be replaced at runtime by this one - previous material color, texture and normal maps will be used.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(offset);
                EditorGUILayout.PropertyField(offsetRandom);
                if (offsetRandom.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(offsetRandomRange, new GUIContent("Offset Range", "Scale applied to random on each axis."));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(scale);
                EditorGUILayout.PropertyField(rotation);
                EditorGUILayout.PropertyField(rotationRandomY);
                EditorGUILayout.PropertyField(overrideMainTexture);
                if (overrideMainTexture.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(overrideMainTextureOffset, new GUIContent("Texture Offset", "Offsets the overridden texture"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(opaque, new GUIContent("Opaque", "Set this value to 15 to specify that this is a fully solid object that occludes other adjacent voxels. A lower value let light pass through and reduces it by this amount. 0 = fully transparent."));
                if (opaque.intValue == VoxelPlayEnvironment.FULL_OPAQUE) {
                    occludesTop.boolValue = occludesBottom.boolValue = occludesLeft.boolValue = occludesRight.boolValue = occludesForward.boolValue = occludesBack.boolValue = true;
                }
                if (opaque.intValue > 0) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(occludesTop);
                    EditorGUILayout.PropertyField(occludesBottom);
                    EditorGUILayout.PropertyField(occludesLeft);
                    EditorGUILayout.PropertyField(occludesRight);
                    EditorGUILayout.PropertyField(occludesForward);
                    EditorGUILayout.PropertyField(occludesBack);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(gpuInstancing, new GUIContent("GPU Instancing", "Uses GPU instancing to render the model."));
                if (allowUpsideDownVoxel.boolValue) {
                    EditorGUILayout.HelpBox("GPU instancing is not compatible with 'Allow Upside Down' option.", MessageType.Info);
                }
                if (gpuInstancing.boolValue) {
                    CheckGPUInstancingMaterialSupport();
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(castShadows, new GUIContent("Cast Shadows"));
                    EditorGUILayout.PropertyField(receiveShadows, new GUIContent("Receive Shadows", "If this instanced voxel can cast shadows."));
                    EditorGUILayout.PropertyField(createGameObject, new GUIContent("Create GameObject"));
                    EditorGUILayout.PropertyField(generateCollider, new GUIContent("Generate Collider"));
                    if (createGameObject.boolValue && generateCollider.boolValue) {
                        GameObject o = (GameObject)model.objectReferenceValue;
                        if (o != null && o.GetComponentInChildren<Collider>() != null) {
                            EditorGUILayout.HelpBox("A collider has been found in the prefab. Consider removing it when using the option 'Generate Collider'", MessageType.Warning);
                        }
                    }
                    GUI.enabled = generateCollider.boolValue;
                    EditorGUILayout.PropertyField(generateNavMesh, new GUIContent("Generate NavMesh"));
                    GUI.enabled = true;
                    EditorGUI.indentLevel--;
                } else {
                    EditorGUILayout.PropertyField(computeLighting);
                }
            }

            EditorGUILayout.Separator();

            VoxelDefinition voxelDefinition = (VoxelDefinition)target;
            DrawMicroVoxelTools(voxelDefinition);
            EditorGUILayout.Space();

            if (DrawSectionHeader("Sound Effects", "AudioSource Icon", ref soundEffectsExpanded)) {
                EditorGUILayout.PropertyField(pickupSound);
                EditorGUILayout.PropertyField(buildSound);
                if (rt != RenderType.Cutout && rt != RenderType.CutoutCross && rt != RenderType.Water && rt != RenderType.Fluid) {
                    EditorGUILayout.PropertyField(footfalls, true);
                }
                EditorGUILayout.PropertyField(jumpSound);
                EditorGUILayout.PropertyField(landingSound);
                EditorGUILayout.PropertyField(impactSound);
                EditorGUILayout.PropertyField(destructionSound);
            }

            EditorGUILayout.Separator();
            if (DrawSectionHeader("Special Events", "d_FilterByType", ref specialEventsExpanded)) {
                EditorGUILayout.PropertyField(triggerWalkEvent);
                EditorGUILayout.PropertyField(triggerEnterEvent);
            }

            EditorGUILayout.Separator();
            if (DrawSectionHeader("Inventory", "d_Package Manager@2x", ref inventoryExpanded)) {
                EditorGUILayout.PropertyField(title);
                EditorGUILayout.PropertyField(hidden);
                if (!hidden.boolValue) {
                    EditorGUILayout.PropertyField(icon);
                    EditorGUILayout.PropertyField(canBeCollected);
                    if (canBeCollected.boolValue) {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(dropProbability);
                        EditorGUILayout.PropertyField(dropItem);
                        EditorGUILayout.PropertyField(dropItemLifeTime);
                        EditorGUILayout.PropertyField(dropItemScale);
                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.Separator();
            if (DrawSectionHeader("Placement", "d_Grid.MoveTool@2x", ref placementExpanded)) {

            if (rt.supportsTextureRotation()) {
                if (rt == RenderType.Custom) {
                    EditorGUILayout.PropertyField(placeOnWall);
                }
                if (!placeOnWall.boolValue) {
                    EditorGUILayout.PropertyField(allowsTextureRotation, new GUIContent("Can Rotate", "Allows texture/object rotation by using VoxelRotateTextures or VoxelRotate (for custom voxel) and similar methods."));
                    GUI.enabled = allowsTextureRotation.boolValue;
                    EditorGUILayout.PropertyField(placeFacingPlayer);
                    GUI.enabled = true;
                }
            }
            EditorGUILayout.PropertyField(promotesTo);

            EditorGUILayout.PropertyField(replacedBy);

            if (rt == RenderType.Custom && !placeOnWall.boolValue) {
                EditorGUILayout.PropertyField(allowUpsideDownVoxel, new GUIContent("Allow Upside Down", "Allows the voxel to be placed upside down."));
                if (allowUpsideDownVoxel.boolValue) {
                    if (gpuInstancing.boolValue) {
                        gpuInstancing.boolValue = false;
                    }
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(isUpsideDown);
                    if (isUpsideDown.boolValue) {
                        EditorGUILayout.PropertyField(upsideDownVoxel, new GUIContent("Normal Voxel"));
                    } else {
                        EditorGUILayout.PropertyField(upsideDownVoxel, new GUIContent("Upside Down Voxel"));

                        if (GUILayout.Button("Create Upside Down Voxel")) {
                            string oldpath = AssetDatabase.GetAssetPath(target);
                            string newPath = oldpath.Replace(".asset", "_Upside_Down.asset");
                            AssetDatabase.CopyAsset(oldpath, newPath);
                            VoxelDefinition newDefinition = AssetDatabase.LoadAssetAtPath(newPath, typeof(VoxelDefinition)) as VoxelDefinition;

                            upsideDownVoxel.objectReferenceValue = newDefinition;
                            isUpsideDown.boolValue = false;

                            newDefinition.promotesTo = promotesTo.objectReferenceValue as VoxelDefinition;
                            newDefinition.dropItem = dropItem.objectReferenceValue as ItemDefinition;
                            newDefinition.allowsTextureRotation = allowsTextureRotation.boolValue;
                            newDefinition.placeFacingPlayer = placeFacingPlayer.boolValue;
                            newDefinition.dropItemLifeTime = dropItemLifeTime.floatValue;
                            newDefinition.upsideDownVoxel = target as VoxelDefinition;
                            newDefinition.canBeCollected = canBeCollected.boolValue;
                            newDefinition.dropProbability = dropProbability.floatValue;
                            newDefinition.dropItemScale = dropItemScale.floatValue;
                            newDefinition.isUpsideDown = true;
                            newDefinition.rotation.z = -180f;
                            newDefinition.hidden = true;
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            } // end Placement

            EditorGUILayout.Separator();
            if (DrawSectionHeader("Other Attributes", "d_Settings@2x", ref otherAttributesExpanded)) {

            if (rt.supportsNavigation()) {
                EditorGUILayout.PropertyField(navigatable);
            }

            if (rt == RenderType.Cutout) {
                EditorGUILayout.PropertyField(denseLeaves);
            }

            if (rt.supportsWindAnimation()) {
                EditorGUILayout.PropertyField(windAnimation);
            }

            if (rt == RenderType.CutoutCross) {
                EditorGUILayout.PropertyField(destroyWithVoxelBelow);
            }

            if (rt.supportsBevel()) {
                EditorGUILayout.PropertyField(supportsBevel);
            }

            if (rt == RenderType.Water || rt == RenderType.Fluid) {
                EditorGUILayout.PropertyField(height);
                EditorGUILayout.PropertyField(diveColor);
                EditorGUILayout.PropertyField(spreads);
                if (spreads.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spreadDelay, new GUIContent("Delay"));
                    EditorGUILayout.PropertyField(spreadDelayRandom, new GUIContent("Randomness"));
                    EditorGUILayout.PropertyField(spreadReplaceThreshold, new GUIContent("Replace Threshold"));
                    EditorGUILayout.PropertyField(drains);
                    EditorGUI.indentLevel--;
                }
            }
            if (rt != RenderType.Invisible) {
                EditorGUILayout.PropertyField(lightIntensity, new GUIContent("Voxel Light Emission", "Amount of light that this voxel emit. This emission value is added to the voxel lighting."));
            }
            if (rt != RenderType.CutoutCross && rt != RenderType.Water && rt != RenderType.Fluid && rt != RenderType.Invisible && rt != RenderType.Cloud) {
                EditorGUILayout.PropertyField(triggerCollapse);
                EditorGUILayout.PropertyField(willCollapse);
            }

            EditorGUILayout.PropertyField(resistancePoints);
            EditorGUILayout.PropertyField(showDamageCracks);

            EditorGUILayout.PropertyField(playerDamage);
            GUI.enabled = playerDamage.intValue > 0;
            EditorGUILayout.PropertyField(playerDamageDelay);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(ignoresRayCast);
            if (!ignoresRayCast.boolValue) {
                EditorGUILayout.PropertyField(highlightOffset);
            }

            if (rt.supportsOptionalColliders()) {
                EditorGUILayout.PropertyField(generateColliders);
            }

            EditorGUILayout.PropertyField(seeThroughMode);
            if (seeThroughMode.intValue == (int)SeeThroughMode.ReplaceVoxel) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(seeThroughVoxel, new GUIContent("Replace By", "The voxel used to render when see-through effect occurs. This voxel can be a variation of this voxel with transparency of any other type of voxel."));
                EditorGUI.indentLevel--;
            } else if (seeThroughMode.intValue == (int)SeeThroughMode.Transparency && !rt.supportsAlphaSeeThrough()) {
                EditorGUILayout.HelpBox("This render type doesn't support alpha-based seethrough mode.", MessageType.Warning);
            }
            } // end Other Attributes

            if (serializedObject.ApplyModifiedProperties()) {
                CheckAnimationTexturesImportSettings();

                // Handle runtime and editor-time updates
                VoxelPlayEnvironment liveEnv = VoxelPlayEnvironment.instance;
                if (liveEnv != null && (Application.isPlaying || liveEnv.renderInEditor)) {
                    HandleLiveUpdate();
                }
            }

            // Handle MicroVoxelsDefinition object picker events
            HandleMicroVoxelObjectPicker();
        }


        // ---- Material Profile UI ----

        protected virtual void DrawMaterialProfileSection(RenderType rt, ref bool isAutoSync) {
            EditorGUILayout.Separator();

            bool profileSupported = VoxelMaterialDrawer.IsProfileSupported(rt);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(materialProfile, new GUIContent("Material Profile"));
            if (profileSupported && GUILayout.Button(new GUIContent("Create", "Create a new Material Profile from the current visual properties."), GUILayout.Width(54f))) {
                ExtractProfile(rt);
            }
            EditorGUILayout.EndHorizontal();
            bool profileChanged = EditorGUI.EndChangeCheck();

            // Block assigning new profiles on Custom/Invisible, but allow clearing
            if (!profileSupported && profileChanged && materialProfile.objectReferenceValue != null) {
                materialProfile.objectReferenceValue = null;
                profileChanged = false;
            }

            VoxelMaterialProfile profile = materialProfile.objectReferenceValue as VoxelMaterialProfile;
            SyncMode mode = (SyncMode)syncMode.intValue;

            // State normalization
            if (profileChanged) {
                if (profile != null && mode == SyncMode.Local) {
                    // User just assigned a profile - switch to Auto and sync
                    Undo.RecordObjects(targets, "Assign Material Profile");
                    syncMode.intValue = (int)SyncMode.Auto;
                    mode = SyncMode.Auto;
                    serializedObject.ApplyModifiedProperties();
                    foreach (Object obj in targets) {
                        var vd = obj as VoxelDefinition;
                        if (vd != null && vd.materialProfile != null) {
                            vd.materialProfile.ApplyTo(vd);
                            EditorUtility.SetDirty(vd);
                        }
                    }
                    serializedObject.UpdateIfRequiredOrScript();
                } else if (profile == null && mode != SyncMode.Local) {
                    // Profile was cleared - switch to Local
                    syncMode.intValue = (int)SyncMode.Local;
                    mode = SyncMode.Local;
                }
            }

            // Auto-heal: non-Local but profile is null
            if (mode != SyncMode.Local && profile == null) {
                syncMode.intValue = (int)SyncMode.Local;
                mode = SyncMode.Local;
            }

            if (profile != null) {
                // Render type compatibility warning
                if (profileSupported && !VoxelMaterialDrawer.AreCompatibleRenderTypes(rt, profile.sourceRenderType)) {
                    EditorGUILayout.HelpBox("This profile was created for " + profile.sourceRenderType + ". Some texture slots may not apply to " + rt + ".", MessageType.Warning);
                }

                // Sync Mode popup
                EditorGUI.BeginChangeCheck();
                GUIContent[] modeOptions = {
                    new GUIContent("Auto", "Visual properties are automatically synced from the profile. Fields are read-only."),
                    new GUIContent("Manual", "Visual properties are local and editable. Use Sync Now to copy values from the profile.")
                };
                int currentMode = mode == SyncMode.Auto ? 0 : 1;
                currentMode = EditorGUILayout.Popup(new GUIContent("Sync Mode", "Auto: properties stay in sync with the profile (read-only). Manual: properties are editable, sync on demand with Sync Now."), currentMode, modeOptions);
                if (EditorGUI.EndChangeCheck()) {
                    SyncMode newMode = currentMode == 0 ? SyncMode.Auto : SyncMode.Manual;
                    if (newMode != mode) {
                        if (newMode == SyncMode.Auto) {
                            // Manual -> Auto: sync now and lock
                            Undo.RecordObjects(targets, "Set Sync Mode Auto");
                            serializedObject.ApplyModifiedProperties();
                            foreach (Object obj in targets) {
                                var vd = obj as VoxelDefinition;
                                if (vd != null && vd.materialProfile != null) {
                                    vd.materialProfile.ApplyTo(vd);
                                    EditorUtility.SetDirty(vd);
                                }
                            }
                            serializedObject.UpdateIfRequiredOrScript();
                        }
                        // Manual -> Auto syncs; Auto -> Manual does NOT sync
                        syncMode.intValue = (int)newMode;
                        mode = newMode;
                    }
                }

                // Sync Now button (only in Manual mode)
                if (mode == SyncMode.Manual) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Sync Now", "Copy all visual properties from the Material Profile into this voxel definition."), GUILayout.MaxWidth(130f))) {
                        Undo.RecordObjects(targets, "Sync Material Profile");
                        serializedObject.ApplyModifiedProperties();
                        foreach (Object obj in targets) {
                            var vd = obj as VoxelDefinition;
                            if (vd != null && vd.materialProfile != null) {
                                vd.materialProfile.ApplyTo(vd);
                                EditorUtility.SetDirty(vd);
                            }
                        }
                        serializedObject.UpdateIfRequiredOrScript();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (!profileSupported) {
                EditorGUILayout.HelpBox("Material profiles are not supported for Custom/Invisible render types.", MessageType.Warning);
            }

            // Apply to Selection (only when multiple VDs selected with a profile)
            if (profileSupported && profile != null && targets.Length > 1) {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Apply to Selection", GUILayout.MaxWidth(130f))) {
                    ApplyProfileToSelection(profile);
                }
                EditorGUILayout.EndHorizontal();
            }

            isAutoSync = mode == SyncMode.Auto && profile != null;
        }

        void ExtractProfile(RenderType rt) {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Material Profile",
                "MaterialProfile",
                "asset",
                "Choose a location to save the Material Profile"
            );
            if (string.IsNullOrEmpty(path)) return;

            VoxelMaterialProfile profile = ScriptableObject.CreateInstance<VoxelMaterialProfile>();
            VoxelDefinition vd = (VoxelDefinition)target;
            profile.CopyFrom(vd);

            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            // Link the VD to the new profile
            Undo.RecordObjects(targets, "Extract Material Profile");
            foreach (Object obj in targets) {
                var targetVd = obj as VoxelDefinition;
                if (targetVd != null) {
                    targetVd.materialProfile = profile;
                    targetVd.syncMode = SyncMode.Auto;
                    EditorUtility.SetDirty(targetVd);
                }
            }
            // Refresh serialized object to pick up direct field changes
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUIUtility.PingObject(profile);
        }

        void ApplyProfileToSelection(VoxelMaterialProfile profile) {
            int applied = 0;
            int skipped = 0;

            Undo.RecordObjects(targets, "Apply Profile to Selection");
            foreach (Object obj in targets) {
                var vd = obj as VoxelDefinition;
                if (vd == null) continue;

                if (!VoxelMaterialDrawer.IsProfileSupported(vd.renderType)) {
                    skipped++;
                    continue;
                }

                profile.ApplyTo(vd);
                vd.materialProfile = profile;
                vd.syncMode = SyncMode.Auto;
                EditorUtility.SetDirty(vd);
                applied++;
            }

            string message = "Applied to " + applied + " voxel definitions.";
            if (skipped > 0) {
                message += " Skipped " + skipped + " (incompatible render type: Custom/Invisible).";
            }
            EditorUtility.DisplayDialog("Apply Profile to Selection", message, "OK");
        }


        static bool DrawSectionHeader(string label, string iconName, ref bool expanded) {
            GUIContent content;
            Texture icon = EditorGUIUtility.IconContent(iconName)?.image;
            if (icon != null) {
                content = new GUIContent(" " + label, icon);
            } else {
                content = new GUIContent(" " + label);
            }
            if (GUILayout.Button(content, sectionHeaderStyle)) {
                expanded = !expanded;
            }
            return expanded;
        }


        // ---- MicroVoxel Methods ----

        void DrawMicroVoxelTools (VoxelDefinition vd) {
            if (!vd.supportsMicroVoxels) return;

            if (vd.usesMicroVoxels && !microVoxelToolsExpanded) {
                microVoxelToolsExpanded = true;
            }

            if (GUILayout.Button(new GUIContent(" Microvoxels", EditorGUIUtility.IconContent("Prefab Icon").image), sectionHeaderStyle)) {
                microVoxelToolsExpanded = !microVoxelToolsExpanded;
            }
            if (!microVoxelToolsExpanded) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (!vd.supportsMicroVoxels) {
                EditorGUILayout.HelpBox("This voxel type does not support microvoxels.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (vd.usesMicroVoxels) {
                EditorGUILayout.BeginHorizontal();
                DrawMicroVoxelPreview(vd);
                DrawMicroVoxelLayerPreview(vd.microVoxels);
                EditorGUILayout.EndHorizontal();
                DrawMicroVoxelToolRibbon(vd);
            } else {
                EditorGUILayout.BeginHorizontal();
                if (microVoxelToolsExpanded) {
                    DrawMicroVoxelPreview(vd);
                }
                EditorGUILayout.EndHorizontal();
                if (microVoxelToolsExpanded) {
                    DrawMicroVoxelToolRibbon(vd);
                }
            }

            EditorGUILayout.EndVertical();
        }

        void ApplyMicroVoxelTemplate (VoxelDefinition vd, MicroVoxels template, string undoTitle) {
            if (template == null) return;
            ApplyMicroVoxelChange(vd, undoTitle, target => {
                MicroVoxels clone = template.Clone();
                clone.needsMeshDataUpdate = true;
                AssignMicroVoxels(target, clone);
            });
        }

        void ApplyMicroVoxelHeight (VoxelDefinition vd, int layerCount) {
            int clampedLayers = Mathf.Clamp(layerCount, 1, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE);
            MicroVoxels mv = CreateMicroVoxels(vd, true);
            for (int y = 0; y < clampedLayers; y++) {
                int layerStart = y * MicroVoxels.COUNT_PER_FACE;
                int layerEnd = layerStart + MicroVoxels.COUNT_PER_FACE;
                for (int index = layerStart; index < layerEnd; index++) {
                    mv.SetOccupied(index);
                }
            }
            mv.layout = MicroVoxelLayout.Default;
            AssignMicroVoxels(vd, mv);
        }

        void ApplyMicroVoxelChange (VoxelDefinition vd, string undoTitle, System.Action<VoxelDefinition> action) {
            if (vd == null) return;
            Undo.RecordObject(vd, undoTitle);
            action(vd);
            if (meshPreviewEditor != null) {
                DestroyImmediate(meshPreviewEditor);
                meshPreviewEditor = null;
            }
            EditorUtility.SetDirty(vd);
            serializedObject.UpdateIfRequiredOrScript();
            Repaint();
            SceneView.RepaintAll();
        }

        static MicroVoxels CreateMicroVoxels (VoxelDefinition vd, bool clear) {
            MicroVoxels mv = vd.microVoxels;
            if (mv == null) {
                mv = new MicroVoxels();
            } else if (mv.isShared) {
                mv = mv.Clone();
            }
            if (clear) {
                mv.Clear();
            }
            mv.isShared = true;
            mv.needsMeshDataUpdate = true;
            return mv;
        }

        static void AssignMicroVoxels (VoxelDefinition vd, MicroVoxels data) {
            if (data != null) {
                if (data.isEmpty || data.isFullSingleMaterial) {
                    data = null;
                } else {
                    data.isShared = true;
                    data.needsMeshDataUpdate = true;
                }
            }
            vd.microVoxels = data;
            vd.microVoxelsPreviewMesh = null;
        }

        void SetupMicroVoxelIcons () {
            microVoxelIconClear = CreateMicroVoxelIcon("TreeEditor.Trash", "Clr", "Clear microvoxels");
            microVoxelIconBottomSlab = CreateIconFromResource("VoxelPlay/Inspector/bottomSlab", "Bot", "Fill bottom slab");
            microVoxelIconTopSlab = CreateIconFromResource("VoxelPlay/Inspector/topSlab", "Top", "Fill top slab");
            microVoxelIconCustomHeight = CreateMicroVoxelIcon("Animation.AddEvent", "H", "Custom height fill");
        }

        static GUIContent CreateIconFromResource (string resourcePath, string fallbackText, string tooltip) {
            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null) {
                return new GUIContent(tex, tooltip);
            }
            return new GUIContent(fallbackText, tooltip);
        }

        void DrawMicroVoxelLayerPreview (MicroVoxels mv) {
            Rect rect = GUILayoutUtility.GetRect(48f, 130f, GUILayout.ExpandWidth(false));
            if (Event.current.type == EventType.Repaint) {
                Color bg = EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.9f, 0.9f, 0.9f);
                EditorGUI.DrawRect(rect, bg);
                float layerHeight = rect.height / MicroVoxels.COUNT_PER_AXIS;
                for (int layer = 0; layer < MicroVoxels.COUNT_PER_AXIS; layer++) {
                    Rect layerRect = new Rect(rect.x + 2f, rect.y + rect.height - (layer + 1) * layerHeight + 1f, rect.width - 4f, layerHeight - 2f);
                    float fill = GetLayerFillRatio(mv, layer);
                    Color fillColor = Color.Lerp(new Color(0.25f, 0.25f, 0.25f), new Color(0.2f, 0.7f, 1f), fill);
                    EditorGUI.DrawRect(layerRect, fillColor);
                }
                GUIStyle topStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
                GUIStyle bottomStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerCenter };
                GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 12f), "Top", topStyle);
                GUI.Label(new Rect(rect.x, rect.yMax - 14f, rect.width, 12f), "Bottom", bottomStyle);
            }
        }

        static float GetLayerFillRatio (MicroVoxels mv, int y) {
            if (mv == null) return 0f;
            int start = y * MicroVoxels.COUNT_PER_FACE;
            int filled = 0;
            for (int i = 0; i < MicroVoxels.COUNT_PER_FACE; i++) {
                int idx = start + i;
                int ulongIndex = idx / 64;
                int bit = idx % 64;
                if ((mv.gridData[ulongIndex] & (1UL << bit)) != 0) filled++;
            }
            return (float)filled / MicroVoxels.COUNT_PER_FACE;
        }

        static int GetMicroVoxelHeightFromData (MicroVoxels mv) {
            if (mv == null || mv.gridData == null || mv.count == 0) return MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
            for (int y = MicroVoxels.COUNT_PER_AXIS - 1; y >= 0; y--) {
                int layerStart = y * MicroVoxels.COUNT_PER_FACE;
                int layerEnd = layerStart + MicroVoxels.COUNT_PER_FACE;
                for (int index = layerStart; index < layerEnd; index++) {
                    int ulongIndex = index >> 6;
                    int bitIndex = index & 63;
                    if ((mv.gridData[ulongIndex] & (1UL << bitIndex)) != 0) {
                        return Mathf.Clamp(y + 1, 1, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE);
                    }
                }
            }
            return MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
        }

        static void SyncCustomHeightWithData (MicroVoxels mv) {
            microVoxelHeight = Mathf.Clamp(GetMicroVoxelHeightFromData(mv), 1, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE);
        }

        void DrawMicroVoxelToolRibbon (VoxelDefinition vd) {
            MicroVoxels current = vd.microVoxels;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.enabled = current != null;
            if (GUILayout.Button(microVoxelIconClear, EditorStyles.toolbarButton, GUILayout.Width(32f))) {
                ApplyMicroVoxelChange(vd, "Clear MicroVoxels", target => AssignMicroVoxels(target, null));
            }
            GUI.enabled = true;
            bool newBottom = GUILayout.Toggle(microVoxelBottomSlabMode, microVoxelIconBottomSlab, EditorStyles.toolbarButton, GUILayout.Width(32f));
            if (newBottom != microVoxelBottomSlabMode) {
                microVoxelBottomSlabMode = newBottom;
                if (microVoxelBottomSlabMode) {
                    microVoxelTopSlabMode = false;
                    microVoxelCustomHeightMode = false;
                    ApplyMicroVoxelTemplate(vd, MicroVoxels.bottomHalfDefaultVoxelTemplate, "Bottom Slab MicroVoxels");
                }
            }
            bool newTop = GUILayout.Toggle(microVoxelTopSlabMode, microVoxelIconTopSlab, EditorStyles.toolbarButton, GUILayout.Width(32f));
            if (newTop != microVoxelTopSlabMode) {
                microVoxelTopSlabMode = newTop;
                if (microVoxelTopSlabMode) {
                    microVoxelBottomSlabMode = false;
                    microVoxelCustomHeightMode = false;
                    ApplyMicroVoxelTemplate(vd, MicroVoxels.topHalfVoxelTemplate, "Top Slab MicroVoxels");
                }
            }
            bool newCustom = GUILayout.Toggle(microVoxelCustomHeightMode, microVoxelIconCustomHeight, EditorStyles.toolbarButton, GUILayout.Width(32f));
            if (newCustom != microVoxelCustomHeightMode) {
                microVoxelCustomHeightMode = newCustom;
                if (microVoxelCustomHeightMode) {
                    microVoxelBottomSlabMode = false;
                    microVoxelTopSlabMode = false;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (microVoxelCustomHeightMode) {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                microVoxelHeight = Mathf.Clamp(microVoxelHeight, 1, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE);
                microVoxelHeight = EditorGUILayout.IntSlider("Custom Height", microVoxelHeight, 1, MicroVoxels.COUNT_PER_AXIS_MINUS_ONE);
                bool removingFullHeight = microVoxelHeight >= MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
                if (GUILayout.Button("Apply", GUILayout.Width(70f))) {
                    if (removingFullHeight) {
                        bool confirm = EditorUtility.DisplayDialog(
                            "Remove Microvoxels",
                            "Full-height microvoxels equal to not using microvoxels. Apply will remove them. Continue?",
                            "Remove",
                            "Cancel"
                        );
                        if (confirm) {
                            ApplyMicroVoxelChange(vd, "Clear MicroVoxels", target => AssignMicroVoxels(target, null));
                        }
                    } else {
                        ApplyMicroVoxelChange(vd, "Apply MicroVoxel Height", target => ApplyMicroVoxelHeight(target, microVoxelHeight));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            // Load/Save buttons for MicroVoxelsDefinition assets
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            // Save button - enabled when voxel definition has microvoxels
            GUI.enabled = current != null && !current.isEmpty;
            if (GUILayout.Button(new GUIContent("Save...", "Save microvoxels to a MicroVoxelsDefinition asset"), EditorStyles.miniButton)) {
                EditorGUILayout.EndHorizontal();
                SaveMicroVoxelsToAsset(vd);
                GUIUtility.ExitGUI();
                return;
            }
            GUI.enabled = true;

            // Load button - always enabled
            if (GUILayout.Button(new GUIContent("Load...", "Load microvoxels from a MicroVoxelsDefinition asset"), EditorStyles.miniButton)) {
                EditorGUILayout.EndHorizontal();
                LoadMicroVoxelsFromAsset(vd);
                GUIUtility.ExitGUI();
                return;
            }

            EditorGUILayout.EndHorizontal();
        }

        void SaveMicroVoxelsToAsset(VoxelDefinition vd) {
            if (vd.microVoxels == null || vd.microVoxels.isEmpty) {
                EditorUtility.DisplayDialog("Save MicroVoxels", "No microvoxels data to save.", "Ok");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Save MicroVoxels Definition",
                "MicroVoxelsDefinition",
                "asset",
                "Choose a location to save the MicroVoxels Definition asset"
            );

            if (string.IsNullOrEmpty(path)) return;

            MicroVoxelsDefinition newMvd = ScriptableObject.CreateInstance<MicroVoxelsDefinition>();
            newMvd.microVoxels = vd.microVoxels.Clone();
            newMvd.microVoxels.isShared = true;

            AssetDatabase.CreateAsset(newMvd, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(newMvd);
        }

        void LoadMicroVoxelsFromAsset(VoxelDefinition vd) {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.ShowObjectPicker<MicroVoxelsDefinition>(null, false, "", controlID);
            microVoxelPickerControlID = controlID;
            microVoxelPickerTargetVd = vd;
        }

        static int microVoxelPickerControlID;
        static VoxelDefinition microVoxelPickerTargetVd;

        void HandleMicroVoxelObjectPicker() {
            if (Event.current.commandName == "ObjectSelectorClosed" && EditorGUIUtility.GetObjectPickerControlID() == microVoxelPickerControlID) {
                MicroVoxelsDefinition selectedMvd = EditorGUIUtility.GetObjectPickerObject() as MicroVoxelsDefinition;
                if (selectedMvd != null && microVoxelPickerTargetVd != null) {
                    ApplyMicroVoxelChange(microVoxelPickerTargetVd, "Load MicroVoxels", target => {
                        MicroVoxels clone = selectedMvd.GetMicroVoxelsClone();
                        if (clone != null) {
                            clone.isShared = true;
                            clone.needsMeshDataUpdate = true;
                            AssignMicroVoxels(target, clone);
                        }
                    });
                }
                microVoxelPickerControlID = 0;
                microVoxelPickerTargetVd = null;
            }
        }

        static GUIContent CreateMicroVoxelIcon (string iconName, string fallbackText, string tooltip) {
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            if (content == null || content.image == null) {
                content = new GUIContent(fallbackText, tooltip);
            } else {
                content = new GUIContent(content) {
                    tooltip = tooltip
                };
            }
            return content;
        }


        void DrawMicroVoxelPreview (VoxelDefinition vd) {
            if (!vd.usesMicroVoxels) return;
            Mesh mesh = vd.GetMicroVoxelsMesh();
            if (mesh == null) return;
            if (meshPreviewEditor == null || meshPreviewEditor.target != mesh) {
                if (meshPreviewEditor != null) {
                    DestroyImmediate(meshPreviewEditor);
                }
                meshPreviewEditor = CreateEditor(mesh);
            }
            Rect previewRect = GUILayoutUtility.GetRect(120f, 150f, GUILayout.ExpandWidth(true));
            bool skipPreviewEvent = Event.current != null && Event.current.type == EventType.ScrollWheel && !previewRect.Contains(Event.current.mousePosition);
            if (!skipPreviewEvent) {
                meshPreviewEditor.OnInteractivePreviewGUI(previewRect, GUIStyle.none);
            }
        }

        /// <summary>
        /// Handle live updates of voxel definitions (both runtime and editor rendering)
        /// </summary>
        protected virtual void HandleLiveUpdate () {
            VoxelPlayEnvironment liveEnv = VoxelPlayEnvironment.instance;
            if (liveEnv == null) return;

            bool needsRedraw = false;

            // Update voxel definitions
            foreach (Object obj in targets) {
                VoxelDefinition vd = obj as VoxelDefinition;
                if (vd != null) {
                    // For new voxel definitions (index <= 0), add them
                    if (vd.index <= 0) {
                        liveEnv.AddVoxelDefinition(vd);
                        needsRedraw = true;
                    }
                    // For existing voxel definitions, update their textures and properties
                    else if (vd.renderType != RenderType.Custom && vd.renderType != RenderType.Invisible) {
                        // Force texture array update to include new metallic/smoothness values
                        liveEnv.UpdateVoxelDefinitionTextures(vd);
                        needsRedraw = true;
                    }
                }
            }

            // Redraw all chunks to apply the changes
            if (needsRedraw) {
                liveEnv.Redraw(reloadWorldTextures: false);
            }
        }

        protected virtual void CheckTextureSize (SerializedProperty texture) {
            VoxelMaterialDrawer.CheckTextureSize(materialFields, env, texture);
        }

        protected virtual void TextureField (SerializedProperty texture, bool requireAlphaTexture, string label = null, string tooltip = null) {
            VoxelMaterialDrawer.TextureField(materialFields, env, texture, requireAlphaTexture, label, tooltip);
        }

        protected VoxelPlayEnvironment env {
            get {
                if (_env == null) {
                    _env = VoxelPlayEnvironment.instance;
                }
                return _env;
            }
        }

        protected virtual void CheckTintColorFeature () {
            VoxelMaterialDrawer.CheckTintColorFeature(materialFields, env);
        }

        protected virtual void CheckNormalMapFeature (SerializedProperty textureProperty) {
            VoxelMaterialDrawer.CheckNormalMapFeature(materialFields, env, textureProperty);
        }

        protected virtual void CheckReliefMapFeature (SerializedProperty textureProperty) {
            VoxelMaterialDrawer.CheckReliefMapFeature(materialFields, env, textureProperty);
        }

        protected virtual void CheckPBRFeature (SerializedProperty metallicTexture, SerializedProperty smoothnessTexture, SerializedProperty occlusionTexture) {
            VoxelMaterialDrawer.CheckPBRFeature(materialFields, env, metallicTexture, smoothnessTexture, occlusionTexture);
        }

        protected virtual void TextureFieldWithStrength (SerializedProperty texture, SerializedProperty strength, bool requireAlphaTexture, string label) {
            VoxelMaterialDrawer.TextureFieldWithStrength(materialFields, env, texture, strength, requireAlphaTexture, label);
        }

        protected virtual void SmoothnessField (SerializedProperty texture, SerializedProperty strength, SerializedProperty useRoughness, bool requireAlphaTexture, string label) {
            VoxelMaterialDrawer.SmoothnessField(materialFields, env, texture, strength, useRoughness, requireAlphaTexture, label);
        }

        protected virtual void LocateOriginal (RenderType renderType) {
            Material mat = renderType.GetDefaultMaterial(env);
            if (mat != null) {
                EditorGUIUtility.PingObject(mat);
            } else {
                Debug.LogError("Default material not found.");
            }
        }

        protected virtual string GetRenderTypeDescription (RenderType rt) {
            switch (rt) {
                case RenderType.Opaque: return "A fully opaque cubic voxel which doesn't allow light to pass through. Supports 3 textures: top, bottom and a third texture for all 6 sides.";
                case RenderType.OpaqueAnimated: return "A fully opaque cubic voxel which doesn't allow light to pass through. Supports 3 textures with animation.";
                case RenderType.Opaque6tex: return "A fully opaque cubic voxel which doesn't allow light to pass through. Supports 6 textures: one texture per cube face.";
                case RenderType.OpaqueNoAO: return "A fully opaque cubic voxel which doesn't allow light to pass through. Does not support ambient occlusion nor global illumination (doesn't get dark).";
                case RenderType.Cloud: return "A render type specific for rendering clouds.";
                case RenderType.Transp6tex: return "A transparent cubic voxel. Supports 6 textures: one texture per cube face. The alpha value of the texture determines the level of transparency.";
                case RenderType.Water: return "Reserved for water rendering.";
                case RenderType.Cutout: return "A cutout cubic voxel. Mostly used for tree leaves and voxels with holes.";
                case RenderType.CutoutCross: return "Reserved for vegetation rendering. Uses two quads to render bushes.";
                case RenderType.Invisible: return "Does not render anything but can generate collider.";
                case RenderType.Custom: return "Used for custom shapes like objects, half-blocks, stylish trees or vegetation, etc. The material used by the prefab will be used for rendering (check the online documentation about custom voxels for valid materials).";
                case RenderType.Fluid: return "A transparent, animated voxel that supports PBR textures, opacity maps, and spreading behavior.";
                default:
                    return "Unknown render type!";
            }
        }

        protected virtual void CheckAnimationTexturesImportSettings () {
            VoxelDefinition vd = (VoxelDefinition)target;
            if (vd.animationTextures == null) return;
            for (int k = 0; k < vd.animationTextures.Length; k++) {
                VoxelPlayEditorCommons.CheckAlbedoImportSettings(vd.animationTextures[k].textureTop, false, false);
                VoxelPlayEditorCommons.CheckAlbedoImportSettings(vd.animationTextures[k].textureSide, false, false);
                VoxelPlayEditorCommons.CheckAlbedoImportSettings(vd.animationTextures[k].textureBottom, false, false);
            }
        }

        public override Texture2D RenderStaticPreview (string assetPath, Object[] subAssets, int width, int height) {
            VoxelDefinition vd = (VoxelDefinition)target;

            if (vd == null || (vd.icon == null && vd.textureSide == null))
                return null;

            Texture source = vd.icon != null ? vd.icon : vd.textureSide;
            if (source == null) return null;

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default);
            Graphics.Blit(source, rt);

            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return tex;
        }

        protected virtual void CheckGPUInstancingMaterialSupport () {
            if (!gpuInstancing.boolValue) return;

            if (!gpuInstancingMaterialSupportChecked) {
                gpuInstancingMaterialSupportChecked = true;
                VoxelDefinition vd = (VoxelDefinition)target;
                if (vd.model != null) {
                    GameObject prefab = vd.prefab != null ? vd.prefab : vd.model;
                    if (prefab != null) {
                        Renderer renderer = prefab.GetComponent<Renderer>();
                        if (renderer != null && renderer.sharedMaterial != null) {
                            prefabCachedMaterial = renderer.sharedMaterial;
                            if (!prefabCachedMaterial.enableInstancing) {
                                showGPUInstancingWarning = true;
                            }
                        }
                    }
                }
            }

            if (showGPUInstancingWarning && prefabCachedMaterial != null) {
                EditorGUILayout.HelpBox("The material associated with the prefab does not have GPU Instancing enabled. Enable it in the material settings.", MessageType.Warning);
                if (GUILayout.Button("Open Material")) {
                    Selection.activeObject = prefabCachedMaterial;
                    EditorGUIUtility.PingObject(prefabCachedMaterial);
                    GUIUtility.ExitGUI();
                }
            }
        }

        protected virtual void AutoPickTextures () {
            VoxelDefinition vd = (VoxelDefinition)target;
            string path = AssetDatabase.GetAssetPath(vd);
            if (string.IsNullOrEmpty(path)) return;

            string dir = System.IO.Path.GetDirectoryName(path);
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });

            var textures = new System.Collections.Generic.List<(Texture2D texture, string name)>();
            foreach (string guid in guids) {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture != null) {
                    textures.Add((texture, System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLower()));
                }
            }

            RenderType rt = (RenderType)renderType.intValue;
            bool requiresAlphaTextureForColor = rt == RenderType.Transp6tex || rt == RenderType.Cutout || rt == RenderType.CutoutCross || rt == RenderType.Water || rt == RenderType.Fluid;

            var typeKeywords = new System.Collections.Generic.Dictionary<string, string[]>() {
                { "color", new[] { "_color", "_albedo", "_diffuse", "_col", "_al", "_diff", "_d", "_basecolor" } },
                { "nrm", new[] { "_normal", "_norm", "_nrm", "_n", "_ddn" } },
                { "disp", new[] { "_displacement", "_displace", "_disp", "_height", "_h" } },
                { "metallic", new[] { "_metallic", "_metal", "_m" } },
                { "smoothness", new[] { "_smoothness", "_gloss", "_specular", "_spec", "_g", "_s", "_roughness" } },
                { "emission", new[] { "_emission", "_emissive", "_e", "_glow" } },
                { "occlusion", new[] { "_occlusion", "_ao" } },
                { "opacity", new[] { "_opacity", "_alpha" } }
            };

            var slotKeywords = new System.Collections.Generic.Dictionary<string, string[]>() {
                { "Top", new[] { "_top", "_up" } },
                { "Bottom", new[] { "_bottom", "_down" } },
                { "Side", new[] { "_side", "_sides", "_back" } },
                { "Right", new[] { "_right" } },
                { "Forward", new[] { "_forward", "_front" } },
                { "Left", new[] { "_left" } },
            };

            var propNames = new System.Collections.Generic.Dictionary<string, string>() {
                { "Top_color", "textureTop" }, { "Top_nrm", "textureTopNRM" }, { "Top_disp", "textureTopDISP" }, { "Top_metallic", "textureTopMetallic" }, { "Top_smoothness", "textureTopSmoothness" }, { "Top_emission", "textureTopEmission" }, { "Top_occlusion", "textureTopOcclusion" }, { "Top_opacity", "textureTopOpacity" },
                { "Side_color", "textureSide" }, { "Side_nrm", "textureSideNRM" }, { "Side_disp", "textureSideDISP" }, { "Side_metallic", "textureSideMetallic" }, { "Side_smoothness", "textureSideSmoothness" }, { "Side_emission", "textureSideEmission" }, { "Side_occlusion", "textureSideOcclusion" }, { "Side_opacity", "textureSideOpacity" },
                { "Bottom_color", "textureBottom" }, { "Bottom_nrm", "textureBottomNRM" }, { "Bottom_disp", "textureBottomDISP" }, { "Bottom_metallic", "textureBottomMetallic" }, { "Bottom_smoothness", "textureBottomSmoothness" }, { "Bottom_emission", "textureBottomEmission" }, { "Bottom_occlusion", "textureBottomOcclusion" }, { "Bottom_opacity", "textureBottomOpacity" },
                { "Right_color", "textureRight" }, { "Right_nrm", "textureRightNRM" }, { "Right_disp", "textureRightDISP" }, { "Right_metallic", "textureRightMetallic" }, { "Right_smoothness", "textureRightSmoothness" }, { "Right_emission", "textureRightEmission" }, { "Right_occlusion", "textureRightOcclusion" }, { "Right_opacity", "textureRightOpacity" },
                { "Forward_color", "textureForward" }, { "Forward_nrm", "textureForwardNRM" }, { "Forward_disp", "textureForwardDISP" }, { "Forward_metallic", "textureForwardMetallic" }, { "Forward_smoothness", "textureForwardSmoothness" }, { "Forward_emission", "textureForwardEmission" }, { "Forward_occlusion", "textureForwardOcclusion" }, { "Forward_opacity", "textureForwardOpacity" },
                { "Left_color", "textureLeft" }, { "Left_nrm", "textureLeftNRM" }, { "Left_disp", "textureLeftDISP" }, { "Left_metallic", "textureLeftMetallic" }, { "Left_smoothness", "textureLeftSmoothness" }, { "Left_emission", "textureLeftEmission" }, { "Left_occlusion", "textureLeftOcclusion" }, { "Left_opacity", "textureLeftOpacity" }
            };

            var genericTextures = new System.Collections.Generic.Dictionary<string, Texture2D>();
            foreach (var typeEntry in typeKeywords) {
                Texture2D genericTexture = FindBestMatch(textures, typeEntry.Value, null);
                genericTextures[typeEntry.Key] = genericTexture;
            }

            foreach (var slotEntry in slotKeywords) {
                string slotName = slotEntry.Key;
                string[] slotKeys = slotEntry.Value;

                foreach (var typeEntry in typeKeywords) {
                    string typeName = typeEntry.Key;
                    string[] typeKeys = typeEntry.Value;

                    Texture2D specificTexture = FindBestMatch(textures, typeKeys, slotKeys);
                    Texture2D textureToAssign = specificTexture != null ? specificTexture : genericTextures[typeName];

                    if (textureToAssign != null) {
                        string propKey = slotName + "_" + typeName;
                        if (propNames.ContainsKey(propKey)) {
                            string propName = propNames[propKey];
                            serializedObject.FindProperty(propName).objectReferenceValue = textureToAssign;
                            bool isColorTexture = typeName == "color";
                            VoxelPlayEditorCommons.CheckAlbedoImportSettings(textureToAssign, isColorTexture && requiresAlphaTextureForColor, false);
                        }
                    }
                }
            }

            // Fallback for color textures
            Texture2D topColorTexture = serializedObject.FindProperty("textureTop").objectReferenceValue as Texture2D;
            if (topColorTexture != null) {
                string[] colorProps = { "textureSide", "textureBottom", "textureRight", "textureForward", "textureLeft" };
                foreach (string prop in colorProps) {
                    if (serializedObject.FindProperty(prop).objectReferenceValue == null) {
                        serializedObject.FindProperty(prop).objectReferenceValue = topColorTexture;
                        VoxelPlayEditorCommons.CheckAlbedoImportSettings(topColorTexture, requiresAlphaTextureForColor, false);
                    }
                }
            }

            if (textureSample.objectReferenceValue == null) {
                if (textureSide.objectReferenceValue != null) {
                    textureSample.objectReferenceValue = textureSide.objectReferenceValue;
                } else if (textureTop.objectReferenceValue != null) {
                    textureSample.objectReferenceValue = textureTop.objectReferenceValue;
                }
            }
        }

        Texture2D FindBestMatch (System.Collections.Generic.List<(Texture2D texture, string name)> textures, string[] typeKeywords, string[] slotKeywords) {
            foreach (var typeKey in typeKeywords) {
                if (slotKeywords != null) {
                    foreach (var slotKey in slotKeywords) {
                        foreach (var tex in textures) {
                            if (tex.name.EndsWith(typeKey) && tex.name.Contains(slotKey)) {
                                return tex.texture;
                            }
                        }
                    }
                } else {
                    foreach (var tex in textures) {
                        if (tex.name.EndsWith(typeKey)) {
                            return tex.texture;
                        }
                    }
                }
            }
            return null;
        }
    }
}
