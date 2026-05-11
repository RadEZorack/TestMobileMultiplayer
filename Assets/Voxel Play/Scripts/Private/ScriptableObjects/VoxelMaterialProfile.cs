using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    [CreateAssetMenu(menuName = "Voxel Play/Voxel Material Profile", fileName = "Voxel Material Profile", order = 102)]
    public class VoxelMaterialProfile : ScriptableObject {

        [Tooltip("The render type this profile was created for. Controls which texture slots are shown in the inspector.")]
        public RenderType sourceRenderType = RenderType.Opaque;

        // Material override
        [Tooltip("If a different material must be used to render this voxel type.")]
        public bool overrideMaterial;
        [Tooltip("Assign a custom material.")]
        public Material overrideMaterialNonGeo;
        [Tooltip("Enable if greedy meshing can be used for this override material.")]
        public bool overrideMaterialGreedyMeshing;
        [Tooltip("Textures are specified in the material itself.")]
        public bool texturesByMaterial;

        // Custom packing
        [Tooltip("Pack specified textures below in a different texture array with custom settings.")]
        public bool texturesCustomPacking;
        [Tooltip("Size of the textures.")]
        public int texturesPackingSize = 256;
        [Tooltip("Texture UV multiplier")]
        public float texturesPackingScale = 1;
        [Tooltip("Enables support for normal maps when using texture packing.")]
        public bool texturesPackingNormalMap;
        [Tooltip("Enables support for relief maps when using texture packing.")]
        public bool texturesPackingReliefMap;
        [Tooltip("Enables support for PBR maps when using texture packing.")]
        public bool texturesPackingPBRMap;

        // Top face
        [Tooltip("Texture of the voxel Top side")]
        public Texture2D textureTop;
        [Tooltip("Emission map")]
        public Texture2D textureTopEmission;
        [Tooltip("Emission strength multiplier for top face")]
        [Range(0f, 4f)]
        public float emissionStrengthTop = 1f;
        [Tooltip("Normal map")]
        public Texture2D textureTopNRM;
        [Tooltip("Displacement")]
        public Texture2D textureTopDISP;
        [Tooltip("Metallic")]
        public Texture2D textureTopMetallic;
        [Tooltip("Smoothness")]
        public Texture2D textureTopSmoothness;
        [Tooltip("Occlusion")]
        public Texture2D textureTopOcclusion;
        [Tooltip("Opacity Map")]
        public Texture2D textureTopOpacity;
        [Tooltip("Opacity strength multiplier for top face")]
        [Range(0f, 1f)]
        public float opacityStrengthTop = 1f;

        // Side/Back face
        [Tooltip("Texture for all voxel sides or Back texture if voxel has 6 textures")]
        public Texture2D textureSide;
        [Tooltip("Emission map")]
        public Texture2D textureSideEmission;
        [Tooltip("Emission strength multiplier for side face")]
        [Range(0f, 4f)]
        public float emissionStrengthSide = 1f;
        [Tooltip("Normal map")]
        public Texture2D textureSideNRM;
        [Tooltip("Displacement map")]
        public Texture2D textureSideDISP;
        [Tooltip("Metallic")]
        public Texture2D textureSideMetallic;
        [Tooltip("Smoothness")]
        public Texture2D textureSideSmoothness;
        [Tooltip("Occlusion")]
        public Texture2D textureSideOcclusion;
        [Tooltip("Opacity Map")]
        public Texture2D textureSideOpacity;
        [Tooltip("Opacity strength multiplier for side face")]
        [Range(0f, 1f)]
        public float opacityStrengthSide = 1f;

        // Right face
        [Tooltip("Texture for voxel's Right face")]
        public Texture2D textureRight;
        [Tooltip("Emission map")]
        public Texture2D textureRightEmission;
        [Tooltip("Emission strength multiplier for right face")]
        [Range(0f, 4f)]
        public float emissionStrengthRight = 1f;
        [Tooltip("Normal map")]
        public Texture2D textureRightNRM;
        [Tooltip("Displacement map")]
        public Texture2D textureRightDISP;
        [Tooltip("Metallic")]
        public Texture2D textureRightMetallic;
        [Tooltip("Smoothness")]
        public Texture2D textureRightSmoothness;
        [Tooltip("Occlusion")]
        public Texture2D textureRightOcclusion;
        [Tooltip("Opacity Map")]
        public Texture2D textureRightOpacity;
        [Tooltip("Opacity strength multiplier for right face")]
        [Range(0f, 1f)]
        public float opacityStrengthRight = 1f;

        // Forward face
        [Tooltip("Texture for voxel's Forward face")]
        public Texture2D textureForward;
        [Tooltip("Emission map")]
        public Texture2D textureForwardEmission;
        [Tooltip("Emission strength multiplier for forward face")]
        [Range(0f, 4f)]
        public float emissionStrengthForward = 1f;
        [Tooltip("Normal map")]
        public Texture2D textureForwardNRM;
        [Tooltip("Displacement map")]
        public Texture2D textureForwardDISP;
        [Tooltip("Metallic")]
        public Texture2D textureForwardMetallic;
        [Tooltip("Smoothness")]
        public Texture2D textureForwardSmoothness;
        [Tooltip("Occlusion")]
        public Texture2D textureForwardOcclusion;
        [Tooltip("Opacity Map")]
        public Texture2D textureForwardOpacity;
        [Tooltip("Opacity strength multiplier for forward face")]
        [Range(0f, 1f)]
        public float opacityStrengthForward = 1f;

        // Left face
        [Tooltip("Texture for voxel's Left face")]
        public Texture2D textureLeft;
        [Tooltip("Emission map")]
        public Texture2D textureLeftEmission;
        [Tooltip("Emission strength multiplier for left face")]
        [Range(0f, 4f)]
        public float emissionStrengthLeft = 1f;
        [Tooltip("Normal map")]
        public Texture2D textureLeftNRM;
        [Tooltip("Displacement map")]
        public Texture2D textureLeftDISP;
        [Tooltip("Metallic")]
        public Texture2D textureLeftMetallic;
        [Tooltip("Smoothness")]
        public Texture2D textureLeftSmoothness;
        [Tooltip("Occlusion")]
        public Texture2D textureLeftOcclusion;
        [Tooltip("Opacity Map")]
        public Texture2D textureLeftOpacity;
        [Tooltip("Opacity strength multiplier for left face")]
        [Range(0f, 1f)]
        public float opacityStrengthLeft = 1f;

        // Bottom face
        [Tooltip("Texture for the voxel Bottom side")]
        public Texture2D textureBottom;
        [Tooltip("Emission map")]
        public Texture2D textureBottomEmission;
        [Tooltip("Emission strength multiplier for bottom face")]
        [Range(0f, 4f)]
        public float emissionStrengthBottom = 1f;
        [Tooltip("Normal map")]
        public Texture2D textureBottomNRM;
        [Tooltip("Displacement map")]
        public Texture2D textureBottomDISP;
        [Tooltip("Metallic")]
        public Texture2D textureBottomMetallic;
        [Tooltip("Smoothness")]
        public Texture2D textureBottomSmoothness;
        [Tooltip("Occlusion")]
        public Texture2D textureBottomOcclusion;
        [Tooltip("Opacity Map")]
        public Texture2D textureBottomOpacity;
        [Tooltip("Opacity strength multiplier for bottom face")]
        [Range(0f, 1f)]
        public float opacityStrengthBottom = 1f;

        // PBR strength per face
        [Range(0f, 1f)] public float metallicStrengthTop;
        [Range(0f, 1f)] public float smoothnessStrengthTop = 0.06f;
        [Range(0f, 1f)] public float occlusionStrengthTop = 1f;

        [Range(0f, 1f)] public float metallicStrengthSide;
        [Range(0f, 1f)] public float smoothnessStrengthSide = 0.06f;
        [Range(0f, 1f)] public float occlusionStrengthSide = 1f;

        [Range(0f, 1f)] public float metallicStrengthRight;
        [Range(0f, 1f)] public float smoothnessStrengthRight = 0.06f;
        [Range(0f, 1f)] public float occlusionStrengthRight = 1f;

        [Range(0f, 1f)] public float metallicStrengthForward;
        [Range(0f, 1f)] public float smoothnessStrengthForward = 0.06f;
        [Range(0f, 1f)] public float occlusionStrengthForward = 1f;

        [Range(0f, 1f)] public float metallicStrengthLeft;
        [Range(0f, 1f)] public float smoothnessStrengthLeft = 0.06f;
        [Range(0f, 1f)] public float occlusionStrengthLeft = 1f;

        [Range(0f, 1f)] public float metallicStrengthBottom;
        [Range(0f, 1f)] public float smoothnessStrengthBottom = 0.06f;
        [Range(0f, 1f)] public float occlusionStrengthBottom = 1f;

        // Roughness mode per face
        public bool useRoughnessTop;
        public bool useRoughnessSide;
        public bool useRoughnessRight;
        public bool useRoughnessForward;
        public bool useRoughnessLeft;
        public bool useRoughnessBottom;

        // Normal and displacement strength per face
        [Range(0f, 1f)] public float normalStrengthTop = 1f;
        [Range(0f, 1f)] public float displacementStrengthTop = 1f;
        [Range(0f, 1f)] public float normalStrengthSide = 1f;
        [Range(0f, 1f)] public float displacementStrengthSide = 1f;
        [Range(0f, 1f)] public float normalStrengthRight = 1f;
        [Range(0f, 1f)] public float displacementStrengthRight = 1f;
        [Range(0f, 1f)] public float normalStrengthForward = 1f;
        [Range(0f, 1f)] public float displacementStrengthForward = 1f;
        [Range(0f, 1f)] public float normalStrengthLeft = 1f;
        [Range(0f, 1f)] public float displacementStrengthLeft = 1f;
        [Range(0f, 1f)] public float normalStrengthBottom = 1f;
        [Range(0f, 1f)] public float displacementStrengthBottom = 1f;

        // Color
        public Color32 tintColor = Misc.color32White;
        public bool tintGradient;
        public Gradient tintGradientColor = new Gradient();
        public VoxelDefinition.TintGradientMode tintGradientMode = VoxelDefinition.TintGradientMode.Random;
        [Range(0f, 1f)]
        public float tintGradientIntensity = 0.5f;
        [Range(0f, 0.5f)]
        public float tintGradientScale = 0.25f;
        [Range(0, 2f)]
        public float colorVariation = 0.5f;

        // Transparency
        [Range(0, 1f)]
        public float alpha = 1f;

        // Water
        public bool showFoam = true;

        // Animation
        public AnimationTextureSet[] animationTextures;
        [Range(1, 16)]
        public int animationSpeed = 4;

        // Sample
        [Tooltip("Texture used to sample colors for particle effects.")]
        public Texture2D textureSample;


        /// <summary>
        /// Copy all visual fields from this profile into a VoxelDefinition.
        /// Also applies textureSample fallback so linked VDs don't trigger the autofill side-effect.
        /// </summary>
        public void ApplyTo(VoxelDefinition vd) {
            // Material override
            vd.overrideMaterial = overrideMaterial;
            vd.overrideMaterialNonGeo = overrideMaterialNonGeo;
            vd.overrideMaterialGreedyMeshing = overrideMaterialGreedyMeshing;
            vd.texturesByMaterial = texturesByMaterial;

            // Custom packing
            vd.texturesCustomPacking = texturesCustomPacking;
            vd.texturesPackingSize = texturesPackingSize;
            vd.texturesPackingScale = texturesPackingScale;
            vd.texturesPackingNormalMap = texturesPackingNormalMap;
            vd.texturesPackingReliefMap = texturesPackingReliefMap;
            vd.texturesPackingPBRMap = texturesPackingPBRMap;

            // Top face
            vd.textureTop = textureTop;
            vd.textureTopEmission = textureTopEmission;
            vd.emissionStrengthTop = emissionStrengthTop;
            vd.textureTopNRM = textureTopNRM;
            vd.textureTopDISP = textureTopDISP;
            vd.textureTopMetallic = textureTopMetallic;
            vd.textureTopSmoothness = textureTopSmoothness;
            vd.textureTopOcclusion = textureTopOcclusion;
            vd.textureTopOpacity = textureTopOpacity;
            vd.opacityStrengthTop = opacityStrengthTop;

            // Side face
            vd.textureSide = textureSide;
            vd.textureSideEmission = textureSideEmission;
            vd.emissionStrengthSide = emissionStrengthSide;
            vd.textureSideNRM = textureSideNRM;
            vd.textureSideDISP = textureSideDISP;
            vd.textureSideMetallic = textureSideMetallic;
            vd.textureSideSmoothness = textureSideSmoothness;
            vd.textureSideOcclusion = textureSideOcclusion;
            vd.textureSideOpacity = textureSideOpacity;
            vd.opacityStrengthSide = opacityStrengthSide;

            // Right face
            vd.textureRight = textureRight;
            vd.textureRightEmission = textureRightEmission;
            vd.emissionStrengthRight = emissionStrengthRight;
            vd.textureRightNRM = textureRightNRM;
            vd.textureRightDISP = textureRightDISP;
            vd.textureRightMetallic = textureRightMetallic;
            vd.textureRightSmoothness = textureRightSmoothness;
            vd.textureRightOcclusion = textureRightOcclusion;
            vd.textureRightOpacity = textureRightOpacity;
            vd.opacityStrengthRight = opacityStrengthRight;

            // Forward face
            vd.textureForward = textureForward;
            vd.textureForwardEmission = textureForwardEmission;
            vd.emissionStrengthForward = emissionStrengthForward;
            vd.textureForwardNRM = textureForwardNRM;
            vd.textureForwardDISP = textureForwardDISP;
            vd.textureForwardMetallic = textureForwardMetallic;
            vd.textureForwardSmoothness = textureForwardSmoothness;
            vd.textureForwardOcclusion = textureForwardOcclusion;
            vd.textureForwardOpacity = textureForwardOpacity;
            vd.opacityStrengthForward = opacityStrengthForward;

            // Left face
            vd.textureLeft = textureLeft;
            vd.textureLeftEmission = textureLeftEmission;
            vd.emissionStrengthLeft = emissionStrengthLeft;
            vd.textureLeftNRM = textureLeftNRM;
            vd.textureLeftDISP = textureLeftDISP;
            vd.textureLeftMetallic = textureLeftMetallic;
            vd.textureLeftSmoothness = textureLeftSmoothness;
            vd.textureLeftOcclusion = textureLeftOcclusion;
            vd.textureLeftOpacity = textureLeftOpacity;
            vd.opacityStrengthLeft = opacityStrengthLeft;

            // Bottom face
            vd.textureBottom = textureBottom;
            vd.textureBottomEmission = textureBottomEmission;
            vd.emissionStrengthBottom = emissionStrengthBottom;
            vd.textureBottomNRM = textureBottomNRM;
            vd.textureBottomDISP = textureBottomDISP;
            vd.textureBottomMetallic = textureBottomMetallic;
            vd.textureBottomSmoothness = textureBottomSmoothness;
            vd.textureBottomOcclusion = textureBottomOcclusion;
            vd.textureBottomOpacity = textureBottomOpacity;
            vd.opacityStrengthBottom = opacityStrengthBottom;

            // PBR strength
            vd.metallicStrengthTop = metallicStrengthTop;
            vd.smoothnessStrengthTop = smoothnessStrengthTop;
            vd.occlusionStrengthTop = occlusionStrengthTop;
            vd.metallicStrengthSide = metallicStrengthSide;
            vd.smoothnessStrengthSide = smoothnessStrengthSide;
            vd.occlusionStrengthSide = occlusionStrengthSide;
            vd.metallicStrengthRight = metallicStrengthRight;
            vd.smoothnessStrengthRight = smoothnessStrengthRight;
            vd.occlusionStrengthRight = occlusionStrengthRight;
            vd.metallicStrengthForward = metallicStrengthForward;
            vd.smoothnessStrengthForward = smoothnessStrengthForward;
            vd.occlusionStrengthForward = occlusionStrengthForward;
            vd.metallicStrengthLeft = metallicStrengthLeft;
            vd.smoothnessStrengthLeft = smoothnessStrengthLeft;
            vd.occlusionStrengthLeft = occlusionStrengthLeft;
            vd.metallicStrengthBottom = metallicStrengthBottom;
            vd.smoothnessStrengthBottom = smoothnessStrengthBottom;
            vd.occlusionStrengthBottom = occlusionStrengthBottom;

            // Roughness mode
            vd.useRoughnessTop = useRoughnessTop;
            vd.useRoughnessSide = useRoughnessSide;
            vd.useRoughnessRight = useRoughnessRight;
            vd.useRoughnessForward = useRoughnessForward;
            vd.useRoughnessLeft = useRoughnessLeft;
            vd.useRoughnessBottom = useRoughnessBottom;

            // Normal and displacement strength
            vd.normalStrengthTop = normalStrengthTop;
            vd.displacementStrengthTop = displacementStrengthTop;
            vd.normalStrengthSide = normalStrengthSide;
            vd.displacementStrengthSide = displacementStrengthSide;
            vd.normalStrengthRight = normalStrengthRight;
            vd.displacementStrengthRight = displacementStrengthRight;
            vd.normalStrengthForward = normalStrengthForward;
            vd.displacementStrengthForward = displacementStrengthForward;
            vd.normalStrengthLeft = normalStrengthLeft;
            vd.displacementStrengthLeft = displacementStrengthLeft;
            vd.normalStrengthBottom = normalStrengthBottom;
            vd.displacementStrengthBottom = displacementStrengthBottom;

            // Color
            vd.tintColor = tintColor;
            vd.tintGradient = tintGradient;
            vd.tintGradientColor = tintGradientColor;
            vd.tintGradientMode = tintGradientMode;
            vd.tintGradientIntensity = tintGradientIntensity;
            vd.tintGradientScale = tintGradientScale;
            vd.colorVariation = colorVariation;

            // Transparency
            vd.alpha = alpha;

            // Water
            vd.showFoam = showFoam;

            // Animation
            vd.animationTextures = animationTextures;
            vd.animationSpeed = animationSpeed;

            // Sample - unconditional copy with fallback
            vd.textureSample = textureSample != null ? textureSample : (textureSide != null ? textureSide : textureTop);
        }

        /// <summary>
        /// Copy all visual fields from a VoxelDefinition into this profile.
        /// Also sets sourceRenderType from the VD.
        /// </summary>
        public void CopyFrom(VoxelDefinition vd) {
            sourceRenderType = vd.renderType;

            // Material override
            overrideMaterial = vd.overrideMaterial;
            overrideMaterialNonGeo = vd.overrideMaterialNonGeo;
            overrideMaterialGreedyMeshing = vd.overrideMaterialGreedyMeshing;
            texturesByMaterial = vd.texturesByMaterial;

            // Custom packing
            texturesCustomPacking = vd.texturesCustomPacking;
            texturesPackingSize = vd.texturesPackingSize;
            texturesPackingScale = vd.texturesPackingScale;
            texturesPackingNormalMap = vd.texturesPackingNormalMap;
            texturesPackingReliefMap = vd.texturesPackingReliefMap;
            texturesPackingPBRMap = vd.texturesPackingPBRMap;

            // Top face
            textureTop = vd.textureTop;
            textureTopEmission = vd.textureTopEmission;
            emissionStrengthTop = vd.emissionStrengthTop;
            textureTopNRM = vd.textureTopNRM;
            textureTopDISP = vd.textureTopDISP;
            textureTopMetallic = vd.textureTopMetallic;
            textureTopSmoothness = vd.textureTopSmoothness;
            textureTopOcclusion = vd.textureTopOcclusion;
            textureTopOpacity = vd.textureTopOpacity;
            opacityStrengthTop = vd.opacityStrengthTop;

            // Side face
            textureSide = vd.textureSide;
            textureSideEmission = vd.textureSideEmission;
            emissionStrengthSide = vd.emissionStrengthSide;
            textureSideNRM = vd.textureSideNRM;
            textureSideDISP = vd.textureSideDISP;
            textureSideMetallic = vd.textureSideMetallic;
            textureSideSmoothness = vd.textureSideSmoothness;
            textureSideOcclusion = vd.textureSideOcclusion;
            textureSideOpacity = vd.textureSideOpacity;
            opacityStrengthSide = vd.opacityStrengthSide;

            // Right face
            textureRight = vd.textureRight;
            textureRightEmission = vd.textureRightEmission;
            emissionStrengthRight = vd.emissionStrengthRight;
            textureRightNRM = vd.textureRightNRM;
            textureRightDISP = vd.textureRightDISP;
            textureRightMetallic = vd.textureRightMetallic;
            textureRightSmoothness = vd.textureRightSmoothness;
            textureRightOcclusion = vd.textureRightOcclusion;
            textureRightOpacity = vd.textureRightOpacity;
            opacityStrengthRight = vd.opacityStrengthRight;

            // Forward face
            textureForward = vd.textureForward;
            textureForwardEmission = vd.textureForwardEmission;
            emissionStrengthForward = vd.emissionStrengthForward;
            textureForwardNRM = vd.textureForwardNRM;
            textureForwardDISP = vd.textureForwardDISP;
            textureForwardMetallic = vd.textureForwardMetallic;
            textureForwardSmoothness = vd.textureForwardSmoothness;
            textureForwardOcclusion = vd.textureForwardOcclusion;
            textureForwardOpacity = vd.textureForwardOpacity;
            opacityStrengthForward = vd.opacityStrengthForward;

            // Left face
            textureLeft = vd.textureLeft;
            textureLeftEmission = vd.textureLeftEmission;
            emissionStrengthLeft = vd.emissionStrengthLeft;
            textureLeftNRM = vd.textureLeftNRM;
            textureLeftDISP = vd.textureLeftDISP;
            textureLeftMetallic = vd.textureLeftMetallic;
            textureLeftSmoothness = vd.textureLeftSmoothness;
            textureLeftOcclusion = vd.textureLeftOcclusion;
            textureLeftOpacity = vd.textureLeftOpacity;
            opacityStrengthLeft = vd.opacityStrengthLeft;

            // Bottom face
            textureBottom = vd.textureBottom;
            textureBottomEmission = vd.textureBottomEmission;
            emissionStrengthBottom = vd.emissionStrengthBottom;
            textureBottomNRM = vd.textureBottomNRM;
            textureBottomDISP = vd.textureBottomDISP;
            textureBottomMetallic = vd.textureBottomMetallic;
            textureBottomSmoothness = vd.textureBottomSmoothness;
            textureBottomOcclusion = vd.textureBottomOcclusion;
            textureBottomOpacity = vd.textureBottomOpacity;
            opacityStrengthBottom = vd.opacityStrengthBottom;

            // PBR strength
            metallicStrengthTop = vd.metallicStrengthTop;
            smoothnessStrengthTop = vd.smoothnessStrengthTop;
            occlusionStrengthTop = vd.occlusionStrengthTop;
            metallicStrengthSide = vd.metallicStrengthSide;
            smoothnessStrengthSide = vd.smoothnessStrengthSide;
            occlusionStrengthSide = vd.occlusionStrengthSide;
            metallicStrengthRight = vd.metallicStrengthRight;
            smoothnessStrengthRight = vd.smoothnessStrengthRight;
            occlusionStrengthRight = vd.occlusionStrengthRight;
            metallicStrengthForward = vd.metallicStrengthForward;
            smoothnessStrengthForward = vd.smoothnessStrengthForward;
            occlusionStrengthForward = vd.occlusionStrengthForward;
            metallicStrengthLeft = vd.metallicStrengthLeft;
            smoothnessStrengthLeft = vd.smoothnessStrengthLeft;
            occlusionStrengthLeft = vd.occlusionStrengthLeft;
            metallicStrengthBottom = vd.metallicStrengthBottom;
            smoothnessStrengthBottom = vd.smoothnessStrengthBottom;
            occlusionStrengthBottom = vd.occlusionStrengthBottom;

            // Roughness mode
            useRoughnessTop = vd.useRoughnessTop;
            useRoughnessSide = vd.useRoughnessSide;
            useRoughnessRight = vd.useRoughnessRight;
            useRoughnessForward = vd.useRoughnessForward;
            useRoughnessLeft = vd.useRoughnessLeft;
            useRoughnessBottom = vd.useRoughnessBottom;

            // Normal and displacement strength
            normalStrengthTop = vd.normalStrengthTop;
            displacementStrengthTop = vd.displacementStrengthTop;
            normalStrengthSide = vd.normalStrengthSide;
            displacementStrengthSide = vd.displacementStrengthSide;
            normalStrengthRight = vd.normalStrengthRight;
            displacementStrengthRight = vd.displacementStrengthRight;
            normalStrengthForward = vd.normalStrengthForward;
            displacementStrengthForward = vd.displacementStrengthForward;
            normalStrengthLeft = vd.normalStrengthLeft;
            displacementStrengthLeft = vd.displacementStrengthLeft;
            normalStrengthBottom = vd.normalStrengthBottom;
            displacementStrengthBottom = vd.displacementStrengthBottom;

            // Color
            tintColor = vd.tintColor;
            tintGradient = vd.tintGradient;
            tintGradientColor = vd.tintGradientColor;
            tintGradientMode = vd.tintGradientMode;
            tintGradientIntensity = vd.tintGradientIntensity;
            tintGradientScale = vd.tintGradientScale;
            colorVariation = vd.colorVariation;

            // Transparency
            alpha = vd.alpha;

            // Water
            showFoam = vd.showFoam;

            // Animation
            animationTextures = vd.animationTextures;
            animationSpeed = vd.animationSpeed;

            // Sample
            textureSample = vd.textureSample;
        }

        /// <summary>
        /// Applies this profile to all registered voxel definitions that use it (syncMode = Auto) and refreshes their textures.
        /// Call this at runtime after modifying profile properties from script.
        /// </summary>
        public void SyncAll () {
            var env = VoxelPlayEnvironment.instance;
            if (env == null) return;
            var list = new List<VoxelDefinition>();
            for (int k = 0; k < env.voxelDefinitionsCount; k++) {
                var vd = env.voxelDefinitions[k];
                if (vd != null && vd.syncMode == SyncMode.Auto && vd.materialProfile == this && !(vd.doNotSave && vd.hidden)) {
                    ApplyTo(vd);
                    list.Add(vd);
                }
            }
            if (list.Count > 0) {
                env.UpdateVoxelDefinitionTextures(list);
            }
        }

#if UNITY_EDITOR

        static bool _syncing;
        static readonly HashSet<VoxelMaterialProfile> _pendingSync = new HashSet<VoxelMaterialProfile>();

        void OnValidate() {
            _pendingSync.Add(this);
            UnityEditor.EditorApplication.delayCall -= ProcessPendingSync;
            UnityEditor.EditorApplication.delayCall += ProcessPendingSync;
        }

        static void ProcessPendingSync() {
            if (_syncing || _pendingSync.Count == 0) return;
            _syncing = true;
            try {
                var profiles = new HashSet<VoxelMaterialProfile>(_pendingSync);
                _pendingSync.Clear();

                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:VoxelDefinition");
                var toSync = new List<VoxelDefinition>();

                foreach (string guid in guids) {
                    var vd = UnityEditor.AssetDatabase.LoadAssetAtPath<VoxelDefinition>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (vd == null) continue;

                    if (vd.syncMode == SyncMode.Auto
                        && vd.materialProfile != null
                        && profiles.Contains(vd.materialProfile)) {
                        toSync.Add(vd);
                    }
                }

                if (toSync.Count > 0) {
                    var objects = new Object[toSync.Count];
                    for (int i = 0; i < toSync.Count; i++) objects[i] = toSync[i];
                    UnityEditor.Undo.RecordObjects(objects, "Sync Material Profile");
                    foreach (var vd in toSync) {
                        vd.materialProfile.ApplyTo(vd);
                        UnityEditor.EditorUtility.SetDirty(vd);
                    }
                    RefreshLiveVoxels(toSync);
                }
            } finally {
                _syncing = false;
            }
        }

        static void RefreshLiveVoxels(List<VoxelDefinition> vds) {
            var env = VoxelPlayEnvironment.instance;
            if (env == null || (!Application.isPlaying && !env.renderInEditor)) return;
            env.UpdateVoxelDefinitionTextures(vds);
        }

#endif

    }
}
