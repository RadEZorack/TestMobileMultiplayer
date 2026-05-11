using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    /// <summary>
    /// Holds SerializedProperty references for all material profile fields.
    /// Works with both VoxelDefinition and VoxelMaterialProfile since field names match.
    /// </summary>
    public class MaterialFieldCache {
        // Material override
        public SerializedProperty overrideMaterial, overrideMaterialNonGeo, overrideMaterialGreedyMeshing, texturesByMaterial;

        // Custom packing
        public SerializedProperty texturesCustomPacking, texturesPackingSize, texturesPackingScale;
        public SerializedProperty texturesPackingNormalMap, texturesPackingReliefMap, texturesPackingPBRMap;

        // Textures per face
        public SerializedProperty textureTop, textureTopEmission, textureTopNRM, textureTopDISP;
        public SerializedProperty textureTopMetallic, textureTopSmoothness, textureTopOcclusion, textureTopOpacity;
        public SerializedProperty textureSide, textureSideEmission, textureSideNRM, textureSideDISP;
        public SerializedProperty textureSideMetallic, textureSideSmoothness, textureSideOcclusion, textureSideOpacity;
        public SerializedProperty textureRight, textureRightEmission, textureRightNRM, textureRightDISP;
        public SerializedProperty textureRightMetallic, textureRightSmoothness, textureRightOcclusion, textureRightOpacity;
        public SerializedProperty textureForward, textureForwardEmission, textureForwardNRM, textureForwardDISP;
        public SerializedProperty textureForwardMetallic, textureForwardSmoothness, textureForwardOcclusion, textureForwardOpacity;
        public SerializedProperty textureLeft, textureLeftEmission, textureLeftNRM, textureLeftDISP;
        public SerializedProperty textureLeftMetallic, textureLeftSmoothness, textureLeftOcclusion, textureLeftOpacity;
        public SerializedProperty textureBottom, textureBottomEmission, textureBottomNRM, textureBottomDISP;
        public SerializedProperty textureBottomMetallic, textureBottomSmoothness, textureBottomOcclusion, textureBottomOpacity;

        // Emission strength
        public SerializedProperty emissionStrengthTop, emissionStrengthSide, emissionStrengthRight;
        public SerializedProperty emissionStrengthForward, emissionStrengthLeft, emissionStrengthBottom;

        // Opacity strength
        public SerializedProperty opacityStrengthTop, opacityStrengthSide, opacityStrengthRight;
        public SerializedProperty opacityStrengthForward, opacityStrengthLeft, opacityStrengthBottom;

        // PBR strength
        public SerializedProperty metallicStrengthTop, smoothnessStrengthTop, occlusionStrengthTop;
        public SerializedProperty metallicStrengthSide, smoothnessStrengthSide, occlusionStrengthSide;
        public SerializedProperty metallicStrengthRight, smoothnessStrengthRight, occlusionStrengthRight;
        public SerializedProperty metallicStrengthForward, smoothnessStrengthForward, occlusionStrengthForward;
        public SerializedProperty metallicStrengthLeft, smoothnessStrengthLeft, occlusionStrengthLeft;
        public SerializedProperty metallicStrengthBottom, smoothnessStrengthBottom, occlusionStrengthBottom;

        // Normal/displacement strength
        public SerializedProperty normalStrengthTop, displacementStrengthTop;
        public SerializedProperty normalStrengthSide, displacementStrengthSide;
        public SerializedProperty normalStrengthRight, displacementStrengthRight;
        public SerializedProperty normalStrengthForward, displacementStrengthForward;
        public SerializedProperty normalStrengthLeft, displacementStrengthLeft;
        public SerializedProperty normalStrengthBottom, displacementStrengthBottom;

        // Roughness mode
        public SerializedProperty useRoughnessTop, useRoughnessSide, useRoughnessRight;
        public SerializedProperty useRoughnessForward, useRoughnessLeft, useRoughnessBottom;

        // Color
        public SerializedProperty tintColor, colorVariation;
        public SerializedProperty tintGradient, tintGradientColor, tintGradientMode, tintGradientIntensity, tintGradientScale;

        // Transparency
        public SerializedProperty alpha;

        // Water
        public SerializedProperty showFoam;

        // Animation
        public SerializedProperty animationTextures, animationSpeed;

        // Sample
        public SerializedProperty textureSample;

        public void Init(SerializedObject so) {
            overrideMaterial = so.FindProperty("overrideMaterial");
            overrideMaterialNonGeo = so.FindProperty("overrideMaterialNonGeo");
            overrideMaterialGreedyMeshing = so.FindProperty("overrideMaterialGreedyMeshing");
            texturesByMaterial = so.FindProperty("texturesByMaterial");

            texturesCustomPacking = so.FindProperty("texturesCustomPacking");
            texturesPackingSize = so.FindProperty("texturesPackingSize");
            texturesPackingScale = so.FindProperty("texturesPackingScale");
            texturesPackingNormalMap = so.FindProperty("texturesPackingNormalMap");
            texturesPackingReliefMap = so.FindProperty("texturesPackingReliefMap");
            texturesPackingPBRMap = so.FindProperty("texturesPackingPBRMap");

            textureTop = so.FindProperty("textureTop");
            textureTopEmission = so.FindProperty("textureTopEmission");
            textureTopNRM = so.FindProperty("textureTopNRM");
            textureTopDISP = so.FindProperty("textureTopDISP");
            textureTopMetallic = so.FindProperty("textureTopMetallic");
            textureTopSmoothness = so.FindProperty("textureTopSmoothness");
            textureTopOcclusion = so.FindProperty("textureTopOcclusion");
            textureTopOpacity = so.FindProperty("textureTopOpacity");

            textureSide = so.FindProperty("textureSide");
            textureSideEmission = so.FindProperty("textureSideEmission");
            textureSideNRM = so.FindProperty("textureSideNRM");
            textureSideDISP = so.FindProperty("textureSideDISP");
            textureSideMetallic = so.FindProperty("textureSideMetallic");
            textureSideSmoothness = so.FindProperty("textureSideSmoothness");
            textureSideOcclusion = so.FindProperty("textureSideOcclusion");
            textureSideOpacity = so.FindProperty("textureSideOpacity");

            textureRight = so.FindProperty("textureRight");
            textureRightEmission = so.FindProperty("textureRightEmission");
            textureRightNRM = so.FindProperty("textureRightNRM");
            textureRightDISP = so.FindProperty("textureRightDISP");
            textureRightMetallic = so.FindProperty("textureRightMetallic");
            textureRightSmoothness = so.FindProperty("textureRightSmoothness");
            textureRightOcclusion = so.FindProperty("textureRightOcclusion");
            textureRightOpacity = so.FindProperty("textureRightOpacity");

            textureForward = so.FindProperty("textureForward");
            textureForwardEmission = so.FindProperty("textureForwardEmission");
            textureForwardNRM = so.FindProperty("textureForwardNRM");
            textureForwardDISP = so.FindProperty("textureForwardDISP");
            textureForwardMetallic = so.FindProperty("textureForwardMetallic");
            textureForwardSmoothness = so.FindProperty("textureForwardSmoothness");
            textureForwardOcclusion = so.FindProperty("textureForwardOcclusion");
            textureForwardOpacity = so.FindProperty("textureForwardOpacity");

            textureLeft = so.FindProperty("textureLeft");
            textureLeftEmission = so.FindProperty("textureLeftEmission");
            textureLeftNRM = so.FindProperty("textureLeftNRM");
            textureLeftDISP = so.FindProperty("textureLeftDISP");
            textureLeftMetallic = so.FindProperty("textureLeftMetallic");
            textureLeftSmoothness = so.FindProperty("textureLeftSmoothness");
            textureLeftOcclusion = so.FindProperty("textureLeftOcclusion");
            textureLeftOpacity = so.FindProperty("textureLeftOpacity");

            textureBottom = so.FindProperty("textureBottom");
            textureBottomEmission = so.FindProperty("textureBottomEmission");
            textureBottomNRM = so.FindProperty("textureBottomNRM");
            textureBottomDISP = so.FindProperty("textureBottomDISP");
            textureBottomMetallic = so.FindProperty("textureBottomMetallic");
            textureBottomSmoothness = so.FindProperty("textureBottomSmoothness");
            textureBottomOcclusion = so.FindProperty("textureBottomOcclusion");
            textureBottomOpacity = so.FindProperty("textureBottomOpacity");

            emissionStrengthTop = so.FindProperty("emissionStrengthTop");
            emissionStrengthSide = so.FindProperty("emissionStrengthSide");
            emissionStrengthRight = so.FindProperty("emissionStrengthRight");
            emissionStrengthForward = so.FindProperty("emissionStrengthForward");
            emissionStrengthLeft = so.FindProperty("emissionStrengthLeft");
            emissionStrengthBottom = so.FindProperty("emissionStrengthBottom");

            opacityStrengthTop = so.FindProperty("opacityStrengthTop");
            opacityStrengthSide = so.FindProperty("opacityStrengthSide");
            opacityStrengthRight = so.FindProperty("opacityStrengthRight");
            opacityStrengthForward = so.FindProperty("opacityStrengthForward");
            opacityStrengthLeft = so.FindProperty("opacityStrengthLeft");
            opacityStrengthBottom = so.FindProperty("opacityStrengthBottom");

            metallicStrengthTop = so.FindProperty("metallicStrengthTop");
            smoothnessStrengthTop = so.FindProperty("smoothnessStrengthTop");
            occlusionStrengthTop = so.FindProperty("occlusionStrengthTop");
            metallicStrengthSide = so.FindProperty("metallicStrengthSide");
            smoothnessStrengthSide = so.FindProperty("smoothnessStrengthSide");
            occlusionStrengthSide = so.FindProperty("occlusionStrengthSide");
            metallicStrengthRight = so.FindProperty("metallicStrengthRight");
            smoothnessStrengthRight = so.FindProperty("smoothnessStrengthRight");
            occlusionStrengthRight = so.FindProperty("occlusionStrengthRight");
            metallicStrengthForward = so.FindProperty("metallicStrengthForward");
            smoothnessStrengthForward = so.FindProperty("smoothnessStrengthForward");
            occlusionStrengthForward = so.FindProperty("occlusionStrengthForward");
            metallicStrengthLeft = so.FindProperty("metallicStrengthLeft");
            smoothnessStrengthLeft = so.FindProperty("smoothnessStrengthLeft");
            occlusionStrengthLeft = so.FindProperty("occlusionStrengthLeft");
            metallicStrengthBottom = so.FindProperty("metallicStrengthBottom");
            smoothnessStrengthBottom = so.FindProperty("smoothnessStrengthBottom");
            occlusionStrengthBottom = so.FindProperty("occlusionStrengthBottom");

            normalStrengthTop = so.FindProperty("normalStrengthTop");
            displacementStrengthTop = so.FindProperty("displacementStrengthTop");
            normalStrengthSide = so.FindProperty("normalStrengthSide");
            displacementStrengthSide = so.FindProperty("displacementStrengthSide");
            normalStrengthRight = so.FindProperty("normalStrengthRight");
            displacementStrengthRight = so.FindProperty("displacementStrengthRight");
            normalStrengthForward = so.FindProperty("normalStrengthForward");
            displacementStrengthForward = so.FindProperty("displacementStrengthForward");
            normalStrengthLeft = so.FindProperty("normalStrengthLeft");
            displacementStrengthLeft = so.FindProperty("displacementStrengthLeft");
            normalStrengthBottom = so.FindProperty("normalStrengthBottom");
            displacementStrengthBottom = so.FindProperty("displacementStrengthBottom");

            useRoughnessTop = so.FindProperty("useRoughnessTop");
            useRoughnessSide = so.FindProperty("useRoughnessSide");
            useRoughnessRight = so.FindProperty("useRoughnessRight");
            useRoughnessForward = so.FindProperty("useRoughnessForward");
            useRoughnessLeft = so.FindProperty("useRoughnessLeft");
            useRoughnessBottom = so.FindProperty("useRoughnessBottom");

            tintColor = so.FindProperty("tintColor");
            colorVariation = so.FindProperty("colorVariation");
            tintGradient = so.FindProperty("tintGradient");
            tintGradientColor = so.FindProperty("tintGradientColor");
            tintGradientMode = so.FindProperty("tintGradientMode");
            tintGradientIntensity = so.FindProperty("tintGradientIntensity");
            tintGradientScale = so.FindProperty("tintGradientScale");
            alpha = so.FindProperty("alpha");
            showFoam = so.FindProperty("showFoam");

            animationTextures = so.FindProperty("animationTextures");
            animationSpeed = so.FindProperty("animationSpeed");

            textureSample = so.FindProperty("textureSample");
        }
    }

    /// <summary>
    /// Shared helper for drawing material profile fields. Used by both VoxelDefinitionEditor and VoxelMaterialProfileEditor.
    /// </summary>
    public static class VoxelMaterialDrawer {

        /// <summary>
        /// Draws material override and custom packing sections.
        /// </summary>
        public static void DrawMaterialOverrideSection(MaterialFieldCache f, VoxelPlayEnvironment env, RenderType rt) {
            EditorGUILayout.PropertyField(f.overrideMaterial);
            if (f.overrideMaterial.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Material shader must be compatible with the original VP shader.\nCheck the online documentation for more details.", MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(f.overrideMaterialNonGeo, new GUIContent("Material", "Overriding material."));
                if (env != null && GUILayout.Button("Locate Original")) {
                    Material mat = rt.GetDefaultMaterial(env);
                    if (mat != null) {
                        EditorGUIUtility.PingObject(mat);
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(f.texturesByMaterial);
                if (f.texturesByMaterial.boolValue) {
                    EditorGUILayout.HelpBox("Shaders with name 'Voxel Play/Voxels/Override Examples/*** are examples you can use or duplicate.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(f.overrideMaterialGreedyMeshing, new GUIContent("Greedy Meshing", "Enables greedy meshing when using this override material."));
                EditorGUI.indentLevel--;
            }
            if (!f.overrideMaterial.boolValue || !f.texturesByMaterial.boolValue) {
                EditorGUILayout.PropertyField(f.texturesCustomPacking, new GUIContent("Custom Packing", "Specifies custom texture packing settings for this voxel definition, including custom texture size, normal map and relief mapping support."));
                if (f.texturesCustomPacking.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox("Custom texture packing allows you to use textures of different sizes for this voxel definition (and custom normal/relief map even if regular voxels do not use them). All textures provided below will be packed in a different texture array. Voxel Play will reuse the same texture array for textures with same size, uv scale and normal/relief mapping settings. Try to use a less different combinations as possible across all voxel definitions.", MessageType.Info);
                    EditorGUILayout.PropertyField(f.texturesPackingSize, new GUIContent("Texture Size", "Texture size for each individual texture used by this material."));
                    EditorGUILayout.PropertyField(f.texturesPackingScale, new GUIContent("UV Scale", "UV multiplier."));
                    EditorGUILayout.PropertyField(f.texturesPackingNormalMap, new GUIContent("Enable Normal Map", "Enables or disables normal map effect for this material."));
                    EditorGUILayout.PropertyField(f.texturesPackingReliefMap, new GUIContent("Enable Relief Map", "Enables or disables relief mapping effect for this material."));
                    EditorGUILayout.PropertyField(f.texturesPackingPBRMap, new GUIContent("Enable PBR", "Enables or disables PBR maps for this material."));
                    EditorGUILayout.HelpBox("Custom texture packing provides more control over texture size and other features but all textures will be packed into a separate texture array which can result in more draw calls.", MessageType.Info);
                    EditorGUI.indentLevel--;
                }
            }
        }

        /// <summary>
        /// Draws texture fields and color/material fields for the given render type.
        /// </summary>
        public static void DrawTextureAndColorFields(MaterialFieldCache f, VoxelPlayEnvironment env, RenderType rt, bool readOnly = false, System.Action onBeforeTextures = null) {
            bool prevEnabled = GUI.enabled;
            if (readOnly) GUI.enabled = false;

            bool showTextureFields = !f.overrideMaterial.boolValue || !f.texturesByMaterial.boolValue;
            bool requiresAlpha = rt == RenderType.Transp6tex || rt == RenderType.Cutout || rt == RenderType.CutoutCross || rt == RenderType.Water || rt == RenderType.Fluid;

            if (showTextureFields) {
                onBeforeTextures?.Invoke();
            }

            switch (rt) {
                case RenderType.OpaqueNoAO:
                case RenderType.Opaque:
                case RenderType.OpaqueAnimated:
                case RenderType.Cloud:
                case RenderType.Cutout:
                case RenderType.Fluid:
                case RenderType.Water:
                    if (showTextureFields) {
                        DrawFaceTexture3Tex(f, env, rt, requiresAlpha);
                    }
                    if (rt == RenderType.Water || rt == RenderType.Fluid) {
                        EditorGUILayout.PropertyField(f.showFoam);
                    } else if (rt != RenderType.Cutout) {
                        TextureField(f, env, f.textureSample, false, "Texture Sample", "Texture that represents the object colors. Used for sampling particle colors and inventory.");
                        DrawTintColor(f, env);
                    }
                    break;
                case RenderType.Opaque6tex:
                case RenderType.Transp6tex:
                    if (showTextureFields) {
                        DrawFaceTexture6Tex(f, env, rt, requiresAlpha);
                    }
                    TextureField(f, env, f.textureSample, false, "Texture Sample", "Texture that represents the object colors. Used for sampling particle colors and inventory.");
                    DrawTintColor(f, env);
                    if (rt == RenderType.Transp6tex || rt == RenderType.Fluid) {
                        EditorGUILayout.PropertyField(f.alpha, new GUIContent("Alpha", "Custom alpha for transparent voxels. Texture alpha value is multiplied by this factor."));
                    }
                    break;
                case RenderType.CutoutCross:
                    if (showTextureFields) {
                        TextureField(f, env, f.textureSide, requiresAlpha, "Texture");
                    }
                    break;
            }

            if (rt == RenderType.Cutout || rt == RenderType.CutoutCross) {
                TextureField(f, env, f.textureSample, false, "Texture Sample", "Texture that represents the object colors. Used for sampling particle colors and inventory.");
                DrawTintColor(f, env);
                EditorGUILayout.PropertyField(f.colorVariation);
            } else if (rt == RenderType.OpaqueAnimated) {
                EditorGUILayout.PropertyField(f.animationTextures, new GUIContent("Additional Textures"), true);
                EditorGUILayout.PropertyField(f.animationSpeed, new GUIContent("Speed"));
            }

            if (readOnly) GUI.enabled = prevEnabled;
        }


        static void DrawFaceTexture3Tex(MaterialFieldCache f, VoxelPlayEnvironment env, RenderType rt, bool requiresAlpha) {
            TextureField(f, env, f.textureTop, requiresAlpha);
            EditorGUI.indentLevel++;
            TextureFieldWithStrength(f, env, f.textureTopNRM, f.normalStrengthTop, false, "Normal Map");
            CheckNormalMapFeature(f, env, f.textureTopNRM);
            TextureFieldWithStrength(f, env, f.textureTopDISP, f.displacementStrengthTop, false, "Displacement Map");
            CheckReliefMapFeature(f, env, f.textureTopDISP);
            if (rt.supportsPBR()) {
                TextureFieldWithStrength(f, env, f.textureTopMetallic, f.metallicStrengthTop, false, "Metallic");
                SmoothnessField(f, env, f.textureTopSmoothness, f.smoothnessStrengthTop, f.useRoughnessTop, false, "Smoothness");
                TextureFieldWithStrength(f, env, f.textureTopOcclusion, f.occlusionStrengthTop, false, "Occlusion");
                CheckPBRFeature(f, env, f.textureTopMetallic, f.textureTopSmoothness, f.textureTopOcclusion);
            }
            if (rt.supportsEmission()) {
                TextureFieldWithStrength(f, env, f.textureTopEmission, f.emissionStrengthTop, false, "Emission");
            }
            EditorGUI.indentLevel--;

            TextureField(f, env, f.textureSide, requiresAlpha);
            if (f.textureSide.objectReferenceValue != f.textureTop.objectReferenceValue) {
                EditorGUI.indentLevel++;
                TextureFieldWithStrength(f, env, f.textureSideNRM, f.normalStrengthSide, false, "Normal Map");
                CheckNormalMapFeature(f, env, f.textureSideNRM);
                TextureFieldWithStrength(f, env, f.textureSideDISP, f.displacementStrengthSide, false, "Displacement Map");
                CheckReliefMapFeature(f, env, f.textureSideDISP);
                if (rt.supportsPBR()) {
                    TextureFieldWithStrength(f, env, f.textureSideMetallic, f.metallicStrengthSide, false, "Metallic");
                    SmoothnessField(f, env, f.textureSideSmoothness, f.smoothnessStrengthSide, f.useRoughnessSide, false, "Smoothness");
                    TextureFieldWithStrength(f, env, f.textureSideOcclusion, f.occlusionStrengthSide, false, "Occlusion");
                    CheckPBRFeature(f, env, f.textureSideMetallic, f.textureSideSmoothness, f.textureSideOcclusion);
                }
                if (rt.supportsEmission()) {
                    TextureFieldWithStrength(f, env, f.textureSideEmission, f.emissionStrengthSide, false, "Emission");
                }
                EditorGUI.indentLevel--;
            }

            TextureField(f, env, f.textureBottom, requiresAlpha);
            if (f.textureBottom.objectReferenceValue != f.textureTop.objectReferenceValue) {
                EditorGUI.indentLevel++;
                TextureFieldWithStrength(f, env, f.textureBottomNRM, f.normalStrengthBottom, false, "Normal Map");
                CheckNormalMapFeature(f, env, f.textureBottomNRM);
                TextureFieldWithStrength(f, env, f.textureBottomDISP, f.displacementStrengthBottom, false, "Displacement Map");
                CheckReliefMapFeature(f, env, f.textureBottomDISP);
                if (rt.supportsPBR()) {
                    TextureFieldWithStrength(f, env, f.textureBottomMetallic, f.metallicStrengthBottom, false, "Metallic");
                    SmoothnessField(f, env, f.textureBottomSmoothness, f.smoothnessStrengthBottom, f.useRoughnessBottom, false, "Smoothness");
                    TextureFieldWithStrength(f, env, f.textureBottomOcclusion, f.occlusionStrengthBottom, false, "Occlusion");
                    CheckPBRFeature(f, env, f.textureBottomMetallic, f.textureBottomSmoothness, f.textureBottomOcclusion);
                }
                if (rt.supportsEmission()) {
                    TextureFieldWithStrength(f, env, f.textureBottomEmission, f.emissionStrengthBottom, false, "Emission");
                }
                EditorGUI.indentLevel--;
            }
        }


        static void DrawFaceWithSubfields6Tex(MaterialFieldCache f, VoxelPlayEnvironment env, RenderType rt, bool requiresAlpha,
            SerializedProperty texture, SerializedProperty compareWith,
            SerializedProperty opacityTex, SerializedProperty opacityStrength,
            SerializedProperty nrm, SerializedProperty normalStr,
            SerializedProperty disp, SerializedProperty dispStr,
            SerializedProperty metallic, SerializedProperty metallicStr,
            SerializedProperty smoothness, SerializedProperty smoothnessStr, SerializedProperty useRoughness,
            SerializedProperty occlusion, SerializedProperty occlusionStr,
            SerializedProperty emission, SerializedProperty emissionStr,
            string label = null) {

            TextureField(f, env, texture, requiresAlpha, label);
            if (compareWith == null || texture.objectReferenceValue != compareWith.objectReferenceValue) {
                EditorGUI.indentLevel++;
                TextureFieldWithStrength(f, env, opacityTex, opacityStrength, false, "Opacity Map");
                TextureFieldWithStrength(f, env, nrm, normalStr, false, "Normal Map");
                CheckNormalMapFeature(f, env, nrm);
                TextureFieldWithStrength(f, env, disp, dispStr, false, "Displacement Map");
                CheckReliefMapFeature(f, env, disp);
                if (rt.supportsPBR()) {
                    TextureFieldWithStrength(f, env, metallic, metallicStr, false, "Metallic");
                    SmoothnessField(f, env, smoothness, smoothnessStr, useRoughness, false, "Smoothness");
                    TextureFieldWithStrength(f, env, occlusion, occlusionStr, false, "Occlusion");
                    CheckPBRFeature(f, env, metallic, smoothness, occlusion);
                }
                if (rt.supportsEmission()) {
                    TextureFieldWithStrength(f, env, emission, emissionStr, false, "Emission");
                }
                EditorGUI.indentLevel--;
            }
        }

        static void DrawFaceTexture6Tex(MaterialFieldCache f, VoxelPlayEnvironment env, RenderType rt, bool requiresAlpha) {
            // Top
            DrawFaceWithSubfields6Tex(f, env, rt, requiresAlpha,
                f.textureTop, null,
                f.textureTopOpacity, f.opacityStrengthTop,
                f.textureTopNRM, f.normalStrengthTop,
                f.textureTopDISP, f.displacementStrengthTop,
                f.textureTopMetallic, f.metallicStrengthTop,
                f.textureTopSmoothness, f.smoothnessStrengthTop, f.useRoughnessTop,
                f.textureTopOcclusion, f.occlusionStrengthTop,
                f.textureTopEmission, f.emissionStrengthTop);

            // Bottom
            DrawFaceWithSubfields6Tex(f, env, rt, requiresAlpha,
                f.textureBottom, f.textureTop,
                f.textureBottomOpacity, f.opacityStrengthBottom,
                f.textureBottomNRM, f.normalStrengthBottom,
                f.textureBottomDISP, f.displacementStrengthBottom,
                f.textureBottomMetallic, f.metallicStrengthBottom,
                f.textureBottomSmoothness, f.smoothnessStrengthBottom, f.useRoughnessBottom,
                f.textureBottomOcclusion, f.occlusionStrengthBottom,
                f.textureBottomEmission, f.emissionStrengthBottom);

            // Back (Side)
            DrawFaceWithSubfields6Tex(f, env, rt, requiresAlpha,
                f.textureSide, f.textureTop,
                f.textureSideOpacity, f.opacityStrengthSide,
                f.textureSideNRM, f.normalStrengthSide,
                f.textureSideDISP, f.displacementStrengthSide,
                f.textureSideMetallic, f.metallicStrengthSide,
                f.textureSideSmoothness, f.smoothnessStrengthSide, f.useRoughnessSide,
                f.textureSideOcclusion, f.occlusionStrengthSide,
                f.textureSideEmission, f.emissionStrengthSide,
                "Texture Back");

            // Right
            DrawFaceWithSubfields6Tex(f, env, rt, requiresAlpha,
                f.textureRight, f.textureTop,
                f.textureRightOpacity, f.opacityStrengthRight,
                f.textureRightNRM, f.normalStrengthRight,
                f.textureRightDISP, f.displacementStrengthRight,
                f.textureRightMetallic, f.metallicStrengthRight,
                f.textureRightSmoothness, f.smoothnessStrengthRight, f.useRoughnessRight,
                f.textureRightOcclusion, f.occlusionStrengthRight,
                f.textureRightEmission, f.emissionStrengthRight,
                "Texture Right");

            // Forward
            DrawFaceWithSubfields6Tex(f, env, rt, requiresAlpha,
                f.textureForward, f.textureTop,
                f.textureForwardOpacity, f.opacityStrengthForward,
                f.textureForwardNRM, f.normalStrengthForward,
                f.textureForwardDISP, f.displacementStrengthForward,
                f.textureForwardMetallic, f.metallicStrengthForward,
                f.textureForwardSmoothness, f.smoothnessStrengthForward, f.useRoughnessForward,
                f.textureForwardOcclusion, f.occlusionStrengthForward,
                f.textureForwardEmission, f.emissionStrengthForward,
                "Texture Forward");

            // Left
            DrawFaceWithSubfields6Tex(f, env, rt, requiresAlpha,
                f.textureLeft, f.textureTop,
                f.textureLeftOpacity, f.opacityStrengthLeft,
                f.textureLeftNRM, f.normalStrengthLeft,
                f.textureLeftDISP, f.displacementStrengthLeft,
                f.textureLeftMetallic, f.metallicStrengthLeft,
                f.textureLeftSmoothness, f.smoothnessStrengthLeft, f.useRoughnessLeft,
                f.textureLeftOcclusion, f.occlusionStrengthLeft,
                f.textureLeftEmission, f.emissionStrengthLeft,
                "Texture Left");
        }


        static void DrawTintColor(MaterialFieldCache f, VoxelPlayEnvironment env) {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(f.tintColor);
            if (EditorGUI.EndChangeCheck()) {
                CheckTintColorFeature(f, env);
            }
            if (f.tintGradient != null) {
                EditorGUILayout.PropertyField(f.tintGradient, new GUIContent("Tint Gradient", "Apply noise-based color variation using a gradient"));
                if (f.tintGradient.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(f.tintGradientColor, new GUIContent("Gradient"));
                    EditorGUILayout.PropertyField(f.tintGradientMode, new GUIContent("Mode"));
                    EditorGUILayout.PropertyField(f.tintGradientIntensity, new GUIContent("Intensity", "How much of the gradient range is applied. 0 = no variation, 1 = full gradient range."));
                    EditorGUILayout.PropertyField(f.tintGradientScale, new GUIContent("Scale", "Spatial frequency of the noise pattern. Higher values create smaller, more frequent patches."));
                    EditorGUI.indentLevel--;
                }
            }
        }


        // Helper methods

        public static void TextureField(MaterialFieldCache f, VoxelPlayEnvironment env, SerializedProperty texture, bool requireAlphaTexture, string label = null, string tooltip = null) {
            EditorGUI.BeginChangeCheck();
            if (label != null) {
                EditorGUILayout.PropertyField(texture, new GUIContent(label, tooltip));
            } else {
                EditorGUILayout.PropertyField(texture);
            }
            if (EditorGUI.EndChangeCheck()) {
                if (texture.objectReferenceValue != null) {
                    Texture textureAsset = (Texture)texture.objectReferenceValue;
                    VoxelPlayEditorCommons.CheckAlbedoImportSettings(textureAsset, requireAlphaTexture, false);
                }
            }
            CheckTextureSize(f, env, texture);
        }

        public static void TextureFieldWithStrength(MaterialFieldCache f, VoxelPlayEnvironment env, SerializedProperty texture, SerializedProperty strength, bool requireAlphaTexture, string label) {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(texture, new GUIContent(label), GUILayout.MinWidth(200));
            if (EditorGUI.EndChangeCheck()) {
                if (texture.objectReferenceValue != null) {
                    Texture textureAsset = (Texture)texture.objectReferenceValue;
                    VoxelPlayEditorCommons.CheckAlbedoImportSettings(textureAsset, requireAlphaTexture, false);
                }
            }
            strength.floatValue = EditorGUILayout.FloatField(strength.floatValue, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            CheckTextureSize(f, env, texture);
        }

        public static void SmoothnessField(MaterialFieldCache f, VoxelPlayEnvironment env, SerializedProperty texture, SerializedProperty strength, SerializedProperty useRoughness, bool requireAlphaTexture, string label) {
            EditorGUILayout.BeginHorizontal();
            string[] options = new string[] { "Smoothness", "Roughness" };
            int selectedIndex = useRoughness.boolValue ? 1 : 0;
            selectedIndex = EditorGUILayout.Popup(selectedIndex, options, GUILayout.Width(EditorGUIUtility.labelWidth - 16));
            useRoughness.boolValue = selectedIndex == 1;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(texture, GUIContent.none, GUILayout.MinWidth(110));
            if (EditorGUI.EndChangeCheck()) {
                if (texture.objectReferenceValue != null) {
                    Texture textureAsset = (Texture)texture.objectReferenceValue;
                    VoxelPlayEditorCommons.CheckAlbedoImportSettings(textureAsset, requireAlphaTexture, false);
                }
            }
            strength.floatValue = EditorGUILayout.FloatField(strength.floatValue, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            CheckTextureSize(f, env, texture);
        }

        public static void CheckTextureSize(MaterialFieldCache f, VoxelPlayEnvironment env, SerializedProperty texture) {
            if (texture.objectReferenceValue == null) return;
            Texture tex = texture.objectReferenceValue as Texture;
            if (tex == null) return;

            int expectedSize = 0;
            if (f.texturesCustomPacking.boolValue) {
                expectedSize = f.texturesPackingSize.intValue;
            } else if (env != null) {
                expectedSize = env.textureSize;
            }

            if (expectedSize > 0 && (tex.width != expectedSize || tex.height != expectedSize)) {
                EditorGUILayout.HelpBox("Texture resolution (" + tex.width + "x" + tex.height + ") is different from expected (" + expectedSize + "x" + expectedSize + ") and it will be automatically scaled when loaded.", MessageType.Warning);
            }
        }

        public static void CheckTintColorFeature(MaterialFieldCache f, VoxelPlayEnvironment env) {
            if (f.tintColor.colorValue.r < 1f || f.tintColor.colorValue.g < 1f || f.tintColor.colorValue.b < 1f) {
                if (env != null && !env.enableTinting) {
                    EditorGUILayout.HelpBox("Tint Color shader feature is disabled in Voxel Play Environment component.", MessageType.Warning);
                }
            }
        }

        public static void CheckNormalMapFeature(MaterialFieldCache f, VoxelPlayEnvironment env, SerializedProperty textureProperty) {
            if (textureProperty.objectReferenceValue == null) return;
            if (f.texturesCustomPacking.boolValue) {
                if (!f.texturesPackingNormalMap.boolValue) {
                    EditorGUILayout.HelpBox("Normal Map is disabled for this voxel definition (check Custom Packing settings).", MessageType.Warning);
                }
            } else if (env != null && !env.enableNormalMap) {
                EditorGUILayout.HelpBox("Normal Map shader feature is disabled in Voxel Play Environment component.", MessageType.Warning);
            }
        }

        public static void CheckReliefMapFeature(MaterialFieldCache f, VoxelPlayEnvironment env, SerializedProperty textureProperty) {
            if (textureProperty.objectReferenceValue == null) return;
            if (f.texturesCustomPacking.boolValue) {
                if (!f.texturesPackingReliefMap.boolValue) {
                    EditorGUILayout.HelpBox("Relief Mapping is disabled for this voxel definition (check Custom Packing settings).", MessageType.Warning);
                }
            } else if (env != null && !env.enableReliefMapping) {
                EditorGUILayout.HelpBox("Relief Mapping (displacement) shader feature is disabled in Voxel Play Environment component.", MessageType.Warning);
            }
        }

        public static void CheckPBRFeature(MaterialFieldCache f, VoxelPlayEnvironment env, SerializedProperty metallicTexture, SerializedProperty smoothnessTexture, SerializedProperty occlusionTexture) {
            if (metallicTexture.objectReferenceValue == null && smoothnessTexture.objectReferenceValue == null && occlusionTexture.objectReferenceValue == null) return;
            if (f.texturesCustomPacking.boolValue) {
                if (!f.texturesPackingPBRMap.boolValue) {
                    EditorGUILayout.HelpBox("PBR is disabled for this voxel definition (check Custom Packing settings).", MessageType.Warning);
                }
            } else if (env != null && !env.enablePBR) {
                EditorGUILayout.HelpBox("PBR shader feature is disabled in Voxel Play Environment component.", MessageType.Warning);
            }
        }


        // Render type compatibility helpers

        public static bool IsProfileSupported(RenderType rt) {
            return rt != RenderType.Custom && rt != RenderType.Invisible;
        }

        public static bool AreCompatibleRenderTypes(RenderType a, RenderType b) {
            return GetCompatibilityGroup(a) == GetCompatibilityGroup(b);
        }

        static int GetCompatibilityGroup(RenderType rt) {
            switch (rt) {
                case RenderType.Opaque:
                case RenderType.OpaqueNoAO:
                case RenderType.OpaqueAnimated:
                    return 1;
                case RenderType.Opaque6tex:
                case RenderType.Transp6tex:
                    return 2;
                case RenderType.Cutout:
                case RenderType.CutoutCross:
                    return 3;
                case RenderType.Water:
                case RenderType.Fluid:
                    return 4;
                default:
                    return 0;
            }
        }
    }
}
