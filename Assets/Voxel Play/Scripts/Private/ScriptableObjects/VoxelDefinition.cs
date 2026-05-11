using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VoxelPlay {

    public enum RenderType : byte {
        Opaque = 0,
        Cutout = 1,
        Water = 2,
        CutoutCross = 3,
        Cloud = 4,
        Custom = 5,
        Opaque6tex = 6,
        Transp6tex = 7,
        Invisible = 8,
        OpaqueNoAO = 9,
        OpaqueAnimated = 10,
        Fluid = 11
    }

    public enum CustomVoxelMaterial {
        PrefabMaterial,
        VertexLit,
        Texture,
        TextureAlpha,
        TextureAlphaDoubleSided,
        TextureTriplanar,
        TextureCutout,
        TextureBumpMap,
        VertexLitAlpha
    }

    public enum SeeThroughMode {
        NotSupported = 0,
        Transparency = 1,
        ReplaceVoxel = 2,
        FullyInvisible = 3
    }

    public enum SyncMode { Local, Auto, Manual }

    public struct TextureRotationIndices {
        public int forward, right, back, left;
    }


    [CreateAssetMenu(menuName = "Voxel Play/Voxel Definition", fileName = "VoxelDefinition", order = 101)]
    [HelpURL("https://kronnect.com/docs/voxel-play/")]
    public partial class VoxelDefinition : ScriptableObject {

        [Tooltip("Name to show in the UI")]
        public string title;

        public VoxelMaterialProfile materialProfile;
        public SyncMode syncMode = SyncMode.Local;

        public RenderType renderType = RenderType.Opaque;

        [Tooltip("If a different material must be used to render this voxel type.")]
        public bool overrideMaterial;

        [Tooltip("Assign a custom material. Material must be derived from VP main materials for the appropriate render type.")]
        public Material overrideMaterialNonGeo;

        [Tooltip("Enable if greedy meshing can be used for this override material.")]
        public bool overrideMaterialGreedyMeshing;

        [Tooltip("Textures are specified in the material itself.")]
        public bool texturesByMaterial;

        [Tooltip("Pack specified textures below in a different texture array with custom settings. The shader must be compatible with texture arrays.")]
        public bool texturesCustomPacking;

        [Tooltip("Size of the textures.")]
        public int texturesPackingSize = 256;

        [Tooltip("Texture UV multiplier")]
        public float texturesPackingScale = 1;

        [Tooltip("Enables support for normal maps when using texture packing.")]
        public bool texturesPackingNormalMap;

        [Tooltip("Enables support for relief maps when using texture packing.")]
        public bool texturesPackingReliefMap;

        [Tooltip("Enables support for PBR maps (metallic, smoothness, occlusion) when using texture packing.")]
        public bool texturesPackingPBRMap;

        [NonSerialized]
        public TextureArrayPacker textureArrayPacker; // the texture array packer for this voxel

        [Tooltip("Enables world space UVs. Useful when the texture spreads over several voxels.")]
        public bool worldSpaceUVs;

        [Tooltip("The amount of light that's blocked by this voxel. Set this value to 15 to specify that this is a fully solid object that occludes other adjacent voxels. Custom voxels should have an opaque less than 15 or will be shown in black.")]
        [Range(0, 15)]
        public byte opaque;

        [Tooltip("If this voxel has an occluding face in this direction.")]
        public bool occludesTop, occludesBottom, occludesLeft, occludesRight, occludesForward, occludesBack;

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

        // Metallic and smoothness strength controls
        [Tooltip("Metallic strength multiplier for top face")]
        [Range(0f, 1f)]
        public float metallicStrengthTop;

        [Tooltip("Smoothness strength multiplier for top face")]
        [Range(0f, 1f)]
        public float smoothnessStrengthTop = 0.06f;

        [Tooltip("Ambient Occlusion strength multiplier for top face")]
        [Range(0f, 1f)]
        public float occlusionStrengthTop = 1f;

        [Tooltip("Metallic strength multiplier for side/back face")]
        [Range(0f, 1f)]
        public float metallicStrengthSide;

        [Tooltip("Smoothness strength multiplier for side/back face")]
        [Range(0f, 1f)]
        public float smoothnessStrengthSide = 0.06f;

        [Tooltip("Ambient Occlusion strength multiplier for side/back face")]
        [Range(0f, 1f)]
        public float occlusionStrengthSide = 1f;

        [Tooltip("Metallic strength multiplier for right face")]
        [Range(0f, 1f)]
        public float metallicStrengthRight;

        [Tooltip("Smoothness strength multiplier for right face")]
        [Range(0f, 1f)]
        public float smoothnessStrengthRight = 0.06f;

        [Tooltip("Ambient Occlusion strength multiplier for right face")]
        [Range(0f, 1f)]
        public float occlusionStrengthRight = 1f;

        [Tooltip("Metallic strength multiplier for forward face")]
        [Range(0f, 1f)]
        public float metallicStrengthForward;

        [Tooltip("Smoothness strength multiplier for forward face")]
        [Range(0f, 1f)]
        public float smoothnessStrengthForward = 0.06f;

        [Tooltip("Ambient Occlusion strength multiplier for forward face")]
        [Range(0f, 1f)]
        public float occlusionStrengthForward = 1f;

        [Tooltip("Metallic strength multiplier for left face")]
        [Range(0f, 1f)]
        public float metallicStrengthLeft;

        [Tooltip("Smoothness strength multiplier for left face")]
        [Range(0f, 1f)]
        public float smoothnessStrengthLeft = 0.06f;

        [Tooltip("Ambient Occlusion strength multiplier for left face")]
        [Range(0f, 1f)]
        public float occlusionStrengthLeft = 1f;

        [Tooltip("Metallic strength multiplier for bottom face")]
        [Range(0f, 1f)]
        public float metallicStrengthBottom;

        [Tooltip("Smoothness strength multiplier for bottom face")]
        [Range(0f, 1f)]
        public float smoothnessStrengthBottom = 0.06f;

        [Tooltip("Ambient Occlusion strength multiplier for bottom face")]
        [Range(0f, 1f)]
        public float occlusionStrengthBottom = 1f;

        // Roughness mode flags (false = smoothness, true = roughness)
        [Tooltip("If true, the top face texture is interpreted as roughness instead of smoothness")]
        public bool useRoughnessTop;

        [Tooltip("If true, the side/back face texture is interpreted as roughness instead of smoothness")]
        public bool useRoughnessSide;

        [Tooltip("If true, the right face texture is interpreted as roughness instead of smoothness")]
        public bool useRoughnessRight;

        [Tooltip("If true, the forward face texture is interpreted as roughness instead of smoothness")]
        public bool useRoughnessForward;

        [Tooltip("If true, the left face texture is interpreted as roughness instead of smoothness")]
        public bool useRoughnessLeft;

        [Tooltip("If true, the bottom face texture is interpreted as roughness instead of smoothness")]
        public bool useRoughnessBottom;

        // Normal and displacement strength controls
        [Tooltip("Normal map strength multiplier for top face")]
        [Range(0f, 1f)]
        public float normalStrengthTop = 1f;

        [Tooltip("Displacement map strength multiplier for top face")]
        [Range(0f, 1f)]
        public float displacementStrengthTop = 1f;

        [Tooltip("Normal map strength multiplier for side/back face")]
        [Range(0f, 1f)]
        public float normalStrengthSide = 1f;

        [Tooltip("Displacement map strength multiplier for side/back face")]
        [Range(0f, 1f)]
        public float displacementStrengthSide = 1f;

        [Tooltip("Normal map strength multiplier for right face")]
        [Range(0f, 1f)]
        public float normalStrengthRight = 1f;

        [Tooltip("Displacement map strength multiplier for right face")]
        [Range(0f, 1f)]
        public float displacementStrengthRight = 1f;

        [Tooltip("Normal map strength multiplier for forward face")]
        [Range(0f, 1f)]
        public float normalStrengthForward = 1f;

        [Tooltip("Displacement map strength multiplier for forward face")]
        [Range(0f, 1f)]
        public float displacementStrengthForward = 1f;

        [Tooltip("Normal map strength multiplier for left face")]
        [Range(0f, 1f)]
        public float normalStrengthLeft = 1f;

        [Tooltip("Displacement map strength multiplier for left face")]
        [Range(0f, 1f)]
        public float displacementStrengthLeft = 1f;

        [Tooltip("Normal map strength multiplier for bottom face")]
        [Range(0f, 1f)]
        public float normalStrengthBottom = 1f;

        [Tooltip("Displacement map strength multiplier for bottom face")]
        [Range(0f, 1f)]
        public float displacementStrengthBottom = 1f;

        public AnimationTextureSet[] animationTextures;

        [Tooltip("Number of frames in addition to the default textures")]
        [Range(1, 16)]
        public int animationSpeed = 4;

        [Tooltip("Default tint color")]
        public Color32 tintColor = Misc.color32White;

        public bool tintGradient;
        public Gradient tintGradientColor = new Gradient();
        public enum TintGradientMode { Random, Vertical, Horizontal }
        public TintGradientMode tintGradientMode = TintGradientMode.Random;
        [Range(0f, 1f)]
        public float tintGradientIntensity = 0.5f;
        [Range(0f, 0.5f)]
        public float tintGradientScale = 0.25f;

        [Tooltip("Grass/tree leaves color variation")]
        [Range(0, 2f)]
        public float colorVariation = 0.5f;

        [Tooltip("Minimum height for vegetation")]
        public float vegetationMinHeight = 1f;

        [Tooltip("Maximum height for vegetation")]
        public float vegetationMaxHeight = 1f;

        [Tooltip("Custom alpha for transparent voxels")]
        [Range(0, 1f)]
        public float alpha = 1f;

        [Tooltip("Compute surrounding lighting and AO and bake into vertex colors")]
        public bool computeLighting;

        [Range(0, 15), Tooltip("Amount of emitting light")]
        public byte lightIntensity;

        [Tooltip("Sound played when voxel is picked up")]
        public AudioClip pickupSound;

        [Tooltip("Sound produced when this voxel is placed in the scene")]
        public AudioClip buildSound;

        [Tooltip("Sounds of the footsteps")]
        public AudioClip[] footfalls;

        [Tooltip("Sound produced when player jumps from this voxel")]
        public AudioClip jumpSound;

        [Tooltip("Sound produced after jumping and landing over this voxel")]
        public AudioClip landingSound;

        [Tooltip("Voxel hit sound")]
        public AudioClip impactSound;

        [Tooltip("Voxel destruction sound")]
        public AudioClip destructionSound;

        [Range(0, 255)]
        [Tooltip("Resistance points. 0 means it cannot be hit. 255 means it's indestructible.")]
        public byte resistancePoints = 15;

        public bool showDamageCracks = true;

        // used for vegetation particles; extracted dynamically from texture
        [NonSerialized, HideInInspector]
        public Color sampleColor;

        /// <summary>
        /// If this voxel type can't be shown in the inventory.
        /// </summary>
        [Tooltip("If this voxel type can't be shown in the inventory.")]
        public bool hidden;

        /// <summary>
        /// If this voxel type can be collected by the user after breaking it.
        /// </summary>
        [Tooltip("If this voxel type can be collected by the user after breaking it.")]
        public bool canBeCollected = true;

        [Tooltip("Probability (0-1) of dropping an item when destroying this voxel.")]
        [Range(0, 1)]
        public float dropProbability = 1f;

        [Tooltip("The item dropped when this voxel is destroyed. If null, a default item of the same voxel type will be used.")]
        public ItemDefinition dropItem;

        [Tooltip("Drop item lifetime in seconds.")]
        public float dropItemLifeTime = 10f;

        [Tooltip("Drop item scale.")]
        public float dropItemScale = 0.25f;

        [Tooltip("Texture used for the inventory panel. If omitted, it will use the side texture.")]
        public Texture2D icon;

        [Tooltip("Texture used to sample colors for particle effects.")]
        public Texture2D textureSample;

        [Tooltip("Override prefab main texture. When enabled, the texture sample will be used as the main texture for this prefab.")]
        public bool overrideMainTexture;

        [Tooltip("Override prefab main texture offset.")]
        public Vector2 overrideMainTextureOffset;

        [Tooltip("If this voxel type can be included in NavMesh navigation. All opaque voxels are by default included in any generated NavMesh but you can exclude this voxel type by setting this property to false.")]
        public bool navigatable = true;

        [Tooltip("Render an additional cutout cross mesh to add denser effect (should be used only on tree leaves).")]
        public bool denseLeaves = true;

        [Tooltip("Cutout voxel types can be animated if this property is set to true.")]
        public bool windAnimation = true;

        [Tooltip("Only used by CutoutCross voxels. If true (default), this voxel is also destroyed when the voxel below it is destroyed (vegetation behavior). Set to false for cross voxels that should stay in place, like cobwebs or barbed wire.")]
        public bool destroyWithVoxelBelow = true;

        [Tooltip("If this voxel supports bevel effect.")]
        public bool supportsBevel = true;

        [Tooltip("The prefab to use when rendering this voxel. Make sure the prefab uses a valid material (you can copy one of the VP Model * materials provided with Voxel Play). Please check the documentation for details.")]
        public GameObject model;

        [Tooltip("Collider data will be generated for this prefab and merged with the chunk collider.")]
        public bool generateCollider;

        [Tooltip("NavMesh data will be generated for this prefab and merged with the chunk NavMesh.")]
        public bool generateNavMesh;

        /// <summary>
        /// This is either a reference to the model or an instanced model in case the voxel definition configuration wants to override the material
        /// </summary>
        [NonSerialized]
        public GameObject prefab;

        [Tooltip("The material to use when rendering the custom voxel. You can use the material provided by the prefab or use one of the optimized materials provided by Voxel Play.")]
        public CustomVoxelMaterial prefabMaterial = CustomVoxelMaterial.PrefabMaterial;

        [Tooltip("Use GPU instancing to render this voxel type.")]
        public bool gpuInstancing;

        [Tooltip("If this instanced voxel can cast shadows.")]
        public bool castShadows = true;

        [Tooltip("If the instanced voxel can receive shadows.")]
        public bool receiveShadows = true;

        [Tooltip("When GPU instancing is enabled, the rendering will be done in GPU but you can still force the creation of a gameobject which can hold colliders or custom scripts.")]
        public bool createGameObject;

        [Tooltip("Optional displacement of the voxel.")]
        public Vector3 offset;

        [Tooltip("If offset should be modified randomly to create variation")]
        public bool offsetRandom;

        [Tooltip("Random offset range in X/Y/Z")]
        public Vector3 offsetRandomRange;

        [Tooltip("If offset should be modified randomly to create variation for vegetation")]
        public bool offsetRandomVegetation = true;

        [Tooltip("Optional scale of the voxel. Note that this scale is multiplied by the gameobject transform scale.")]
        public Vector3 scale = Misc.vector3one;

        [Tooltip("Optional rotation of the voxel in degrees.")]
        public Vector3 rotation;

        [Tooltip("Random rotataion around Y-Axis")]
        public bool rotationRandomY;

        [Tooltip("Voxel definition to use when this voxel is placed twice on the same position.")]
        public VoxelDefinition promotesTo;

        [Tooltip("Voxel definition to use when this voxel is destroyed.")]
        public VoxelDefinition replacedBy;

        [Tooltip("If this voxel propagates (eg. water).")]
        public bool spreads;

        [Tooltip("If this voxel reduces its amount when it propagates (eg. sea water do not drain, acting like an infinite source of water but water in player's inventory should drain as it spreads)")]
        public bool drains = true;

        [Tooltip("Delay for propagation in seconds.")]
        public float spreadDelay;

        [Tooltip("Additional random delay for propagation.")]
        public float spreadDelayRandom;

        [Tooltip("Any voxel with opaque value less than this threshold will be replaced.")]
        [Range(0, 15)]
        public byte spreadReplaceThreshold;

        [ColorUsage(true)]
        public Color diveColor = new Color(0, 0.41f, 0.63f, 0.83f);

        [Range(0, 15), Tooltip("Default block level")]
        public byte height = 13;

        [Range(0, 255), Tooltip("Damage caused to the player.")]
        public byte playerDamage;

        [Range(0, 255), Tooltip("Time interval in seconds between damage caused to the player.")]
        public float playerDamageDelay = 2f;

        [Tooltip("If this voxel is destroyed, connected voxels on same level or upper levels with triggerCollapse flag will fall.")]
        public bool triggerCollapse;

        [Tooltip("If a nearby voxel is destroyed, this voxel will collapse and fall.")]
        public bool willCollapse;

        [Tooltip("Ignores raycast on this voxel type (ie. air voxels)")]
        public bool ignoresRayCast;

        [Tooltip("Forces generation of collider data for this voxel type.")]
        public bool generateColliders;

        [Tooltip("Optional displacement for the highlight box.")]
        public Vector3 highlightOffset;

        [Tooltip("If texture rotation is allowed for this voxel type.")]
        public bool allowsTextureRotation = true;

        [Tooltip("When placing this voxel, attach it to a wall.")]
        public bool placeOnWall;

        [Tooltip("When placing this voxel, orient it to the player facing direction. Only applies to voxels with 6 textures or custom voxel types.")]
        public bool placeFacingPlayer = true;

        [Tooltip("If entering this voxel raises a OnVoxelEnter event by FPS controllers.")]
        public bool triggerEnterEvent;

        [Tooltip("If walking over this voxel raises a OnVoxelWalk event by FPS controllers.")]
        public bool triggerWalkEvent;

        public bool showFoam = true;

        [Tooltip("This voxel can be turned invisible. Under certain circumstances, Voxel Play won't render certain voxels, for example to hide the roof of a building when the player enters it in third-person view.")]
        public SeeThroughMode seeThroughMode = SeeThroughMode.Transparency;

        [Tooltip("The voxel used to render when see-through effect occurs. This voxel can be a variation of this voxel with transparency of any other type of voxel.")]
        public VoxelDefinition seeThroughVoxel;

        public bool allowUpsideDownVoxel;
        public VoxelDefinition upsideDownVoxel;
        public bool isUpsideDown;

        /// ************************ Temporary/session-related data *****************************************
        /// <summary>
        /// Returns true is this voxel casues a collision
        /// </summary>
        [NonSerialized]
        public bool isSolid;

        // If stuff like pictures or carpets can be attached to this voxel
        public bool supportsDecorations => !placeOnWall && renderType != RenderType.CutoutCross && renderType != RenderType.Water && renderType != RenderType.Invisible && renderType != RenderType.Cloud;

        [NonSerialized]
        public int textureIndexSide, textureIndexTop, textureIndexBottom, textureIndexLeft, textureIndexForward, textureIndexRight;

        // indices for rotated textures
        [NonSerialized]
        public TextureRotationIndices[] textureSideIndices;

        // index in voxelDefinitions list when it's added
        [NonSerialized]
        public ushort index;

        public bool hasContent { get { return index > Voxel.HOLE_TYPE_INDEX; } }

        // The related dynamic voxel definition. This field is only set when a static voxel is converted to dynamic (only set once per type)
        [NonSerialized]
        public VoxelDefinition dynamicDefinition;

        // The related static voxel definition. Thsi field is set so when a dynamic voxel is converted back to static, it can know which static type belongs to
        [NonSerialized]
        public VoxelDefinition staticDefinition;

        // if this voxel definition is dynamic
        [NonSerialized]
        public bool isDynamic;

        // if this voxel is used for vegetation
        public bool isVegetation => renderType == RenderType.CutoutCross;

        // if this voxel is water or fluid
        public bool isLiquid => renderType == RenderType.Fluid || renderType == RenderType.Water;

        /// <summary>
        /// Returns true if this voxel is part of a tree model definition
        /// </summary>
        [NonSerialized]
        public bool isTree;

        // Used to cache dynamic voxel meshes
        [NonSerialized]
        public Dictionary<Color, Mesh> dynamicMeshes;

        // Used to texture dropped voxels
        [NonSerialized]
        public Texture2D textureThumbnailTop, textureThumbnailSide, textureThumbnailBottom;

        // Annotated if the thumbnail textures are instanced/scaled versions so they should be disposed properly
        [NonSerialized]
        public bool textureThumbnailTopInstanced, textureThumbnailSideInstanced, textureThumbnailBottomInstanced;

        // if this is the surface voxel of a biome, the dirt voxel definition of the same biome
        [NonSerialized]
        public VoxelDefinition biomeDirtCounterpart;

        // if this is the dirt voxel of a biome, the surface voxel definition of the same biome
        [NonSerialized]
        public VoxelDefinition biomeSurfaceCounterpart;

        // mesh used by the prefab
        [NonSerialized]
        Mesh _mesh;

        public Mesh mesh {
            get {
                if (_mesh == null && prefab != null) {
                    MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
                    if (mf != null)
                        _mesh = mf.sharedMesh;
                }
                return _mesh;
            }
        }

        [NonSerialized]
        public Color32[][] meshColors32; // for multi-part prefabs, a different color array per each object

        // material used by the prefab
        [NonSerialized]
        Material[] _materials;

        void FetchMaterials () {
            if (prefab == null) return;
            MeshRenderer mr = prefab.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
                _materials = mr.sharedMaterials;
        }

        /// <summary>
        /// Gets / set materials used from the prefab
        /// </summary>
        public Material[] materials {
            get {
                if (_materials.IsNullOrEmpty()) {
                    FetchMaterials();
                }
                return _materials;
            }
            set {
                _materials = value;
            }
        }

        /// <summary>
        /// Gets / set the first material used by the prefab
        /// </summary>
        public Material material {
            get {
                if (_materials.IsNullOrEmpty()) {
                    FetchMaterials();

                    if (_materials.IsNullOrEmpty()) {
                        return null;
                    }
                }
                return _materials[0];
            }

            set {
                if (_materials.IsNullOrEmpty()) {
                    _materials = new Material[1];
                }
                _materials[0] = value;
            }
        }

        // if the model has collider and the gameobject is being created.
        [NonSerialized]
        public bool prefabUsesCollider;


        Bounds _bakedBoxColliderBounds;

        // The computed collider prefab bounds
        public Bounds prefabColliderBounds {
            get {
                if (_bakedBoxColliderBounds.size.x != 0) {
                    return _bakedBoxColliderBounds;
                }
                return mesh.bounds;
            }
            set {
                _bakedBoxColliderBounds = value;
            }
        }

        // if the model has a rigid body.
        [NonSerialized]
        public bool prefabUsesRigidbody;


        /// <summary>
        /// Each material has an index for storing vertex indices in mesh job structs.
        /// </summary>
        [NonSerialized]
        public int materialBufferIndex;

        /// <summary>
        /// Convenient accessors for the greedymeshers corresponding to the rendering material associated to this voxel definitino
        /// </summary>
        [NonSerialized]
        public VoxelPlayGreedyMesherLit greedyMesherLit;
        [NonSerialized]
        public VoxelPlayGreedyMesherLitAO greedyMesherLitAO;


        public Quaternion GetRotation (Vector3d position) {
            Vector3 rot = rotation;
            if (rotationRandomY) {
                rot.y += WorldRand.GetValue(position) * 360f;
            }
            return Quaternion.Euler(rot);
        }

        public Vector3 GetOffset (Vector3d position) {
            Vector3 shiftedOffset = offset;
            if (offsetRandom) {
                shiftedOffset += WorldRand.GetVector3(position, offsetRandomRange, -0.5f);
            }
            return shiftedOffset;
        }

        /// <summary>
        /// The texture used for the inventory
        /// </summary>
        /// <value>The icon.</value>
        public Texture2D GetIcon () {
            if (icon != null)
                return icon;
            if (textureThumbnailSide != null) {
                return textureThumbnailSide;
            }
            return textureSide;
        }

        public Material GetOverrideMaterial () {
            if (!overrideMaterial)
                return null;
            return overrideMaterialNonGeo;
        }

        /// <summary>
        /// Temporary transparent voxel created for the see-through effect
        /// </summary>
        [NonSerialized]
        public int seeThroughVoxelTempTransp;

        /// <summary>
        /// If this voxel can be saved in a save game
        /// </summary>
        [NonSerialized]
        public bool doNotSave;

        /// <summary>
        /// Set at runtime. Combines denseLeves with global dense leaves setting.
        /// </summary>
        [NonSerialized]
        public bool usesDenseLeaves;


        // ********************************
        // Static methods *****************
        // ********************************

        /// <returns>true if the voxel is either null or represents the null voxel definition</returns>
        public static bool IsNull (VoxelDefinition vd) {
            return vd == null || vd.index == 0;
        }


        // ********************************
        // Events *************************
        // ********************************

        /// <summary>
        /// Clears temporary/session non-serializable fields
        /// </summary>
        public void Reset () {
            index = 0;
            dynamicDefinition = null;
            batchedIndex = -1;
            doNotSave = false;
            textureIndexBottom = textureIndexSide = textureIndexTop = 0;
            isDynamic = false;
            dynamicDefinition = null;
            staticDefinition = null;
            seeThroughVoxelTempTransp = 0;
            materialBufferIndex = 0;
            if (dynamicMeshes != null) {
                dynamicMeshes.Clear();
                dynamicMeshes = null;
            }
            _mesh = null;
            _materials = null;
        }
    }

    [Serializable]
    public struct AnimationTextureSet {
        public Texture2D textureTop;
        public Texture2D textureSide;
        public Texture2D textureBottom;
    }

    public static partial class RenderTypeExtensions {

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsNavigation (this RenderType o) {
            return o == RenderType.Opaque || o == RenderType.Cutout || o == RenderType.OpaqueNoAO || o == RenderType.Opaque6tex || o == RenderType.Transp6tex || o == RenderType.Invisible || o == RenderType.OpaqueAnimated;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsWindAnimation (this RenderType o) {
            return o == RenderType.Cutout || o == RenderType.CutoutCross;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsBevel (this RenderType o) {
            return o == RenderType.Opaque || o == RenderType.Opaque6tex;
        }

        public static int numberOfTextures (this RenderType o) {
            if (o == RenderType.Invisible)
                return 0;
            return (o == RenderType.Opaque6tex || o == RenderType.Transp6tex) ? 6 : 3;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsTextureRotation (this RenderType o) {
            // custom types can always be rotated
            return o == RenderType.Opaque6tex || o == RenderType.Transp6tex || o == RenderType.Custom || o == RenderType.Opaque || o == RenderType.Cutout;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsEmission (this RenderType o) {
            return o == RenderType.Opaque || o == RenderType.Opaque6tex || o == RenderType.OpaqueAnimated || o == RenderType.Water || o == RenderType.Transp6tex || o == RenderType.Fluid;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsDynamic (this RenderType o) {
            return o == RenderType.Opaque || o == RenderType.Opaque6tex || o == RenderType.OpaqueNoAO || o == RenderType.Cutout || o == RenderType.Custom || o == RenderType.Transp6tex;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsPBR (this RenderType o) {
            return o == RenderType.Opaque || o == RenderType.Opaque6tex || o == RenderType.OpaqueNoAO || o == RenderType.Cutout || o == RenderType.Transp6tex || o == RenderType.Fluid;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static byte hasContent (this RenderType o) {
            return o == RenderType.Invisible ? (byte)0 : (byte)1;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsAlphaSeeThrough (this RenderType o) {
            return o == RenderType.Opaque || o == RenderType.Cutout || o == RenderType.Opaque6tex || o == RenderType.OpaqueNoAO;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsTextureAnimation (this RenderType o) {
            return o == RenderType.OpaqueAnimated;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool supportsOptionalColliders (this RenderType o) {
            return o == RenderType.Cutout;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static bool isOpaque (this RenderType o) {
            return o == RenderType.Opaque || o == RenderType.Opaque6tex || o == RenderType.OpaqueAnimated || o == RenderType.OpaqueNoAO;
        }

        public static Material GetDefaultMaterial (this RenderType o, VoxelPlayEnvironment context) {
            string name;
            bool shadowsOnWater = context.shadowsOnWater && !VoxelPlayEnvironment.supportsURP;

            switch (o) {
                case RenderType.Opaque:
                case RenderType.Opaque6tex:
                    name = "VP Voxel Triangle Opaque";
                    break;
                case RenderType.OpaqueNoAO:
                    name = "VP Voxel Triangle Opaque No AO";
                    break;
                case RenderType.OpaqueAnimated:
                    name = "VP Voxel Triangle Opaque Animated";
                    break;
                case RenderType.Cutout:
                    name = "VP Voxel Triangle Cutout";
                    break;
                case RenderType.CutoutCross:
                    name = "VP Voxel Triangle Cutout Cross";
                    break;
                case RenderType.Water:
                    if (context.realisticWater) {
                        if (shadowsOnWater) {
                            name = "VP Voxel Triangle Water Realistic";
                        } else {
                            name = "VP Voxel Triangle Water Realistic No Shadows";
                        }
                    } else {
                        if (shadowsOnWater) {
                            name = "VP Voxel Triangle Water";
                        } else {
                            name = "VP Voxel Triangle Water No Shadows";
                        }
                    }
                    break;
                case RenderType.Transp6tex:
                    if (context.doubleSidedGlass) {
                        name = "VP Voxel Triangle Transp Double Sided";
                    } else {
                        name = "VP Voxel Triangle Transp";
                    }
                    break;
                case RenderType.Cloud:
                    name = "VP Voxel Triangle Cloud";
                    break;
                case RenderType.Fluid:
                    name = "VP Voxel Triangle Fluid";
                    break;
                case RenderType.Invisible:
                    return null;
                default:
                    Debug.LogError("Unknown Render type?");
                    return null;
            }
            return Resources.Load<Material>("VoxelPlay/Materials/" + name);
        }

    }
}