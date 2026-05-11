using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoxelPlay {

    public enum VoxelPlaySkybox {
        UserDefined = 0,
        Earth = 1,
        Space = 2,
        EarthSimplified = 3,
        EarthNightCubemap = 4,
        EarthDayNightCubemap = 5
    }


    [CreateAssetMenu(menuName = "Voxel Play/World Definition", fileName = "WorldDefinition", order = 103)]
    [HelpURL("https://kronnect.com/docs/voxel-play/")]
    public partial class WorldDefinition : ScriptableObject {
        public int seed;

        public VoxelPlayTerrainGenerator terrainGenerator;
        [Tooltip("Generate infinite world.")]
        public bool infinite = true;
        [Tooltip("Center of the world bounds. Used together with extents to define the valid area for chunk generation and character spawning.")]
        public Vector3 center = Vector3.zero;
        [Tooltip("The world extents (half of size) around center. Extents must be a multiple of chunk size (16). For example, if extents X = 1024 the world will be generated between center.x-1024 and center.x+1024.")]
        public Vector3 extents = new Vector3(1024, 1024, 1024);
        public VoxelPlayDetailGenerator[] detailGenerators;

        public BiomeDefinition[] biomes;
        [Tooltip("Default biome used if no biome matches altitude/moisture at a given position. Optional.")]
        public BiomeDefinition defaultBiome;
        [Tooltip("Global wind speed (only for grass)")]
        [Range(0, 16)]
        public float grassWindSpeed = 1;
        [Tooltip("Global wind speed (only for trees)")]
        [Range(0, 16)]
        public float treeWindSpeed = 1;

        [Header("Sky")]
        public VoxelPlaySkybox skyboxDesktop = VoxelPlaySkybox.Earth;
        public VoxelPlaySkybox skyboxMobile = VoxelPlaySkybox.EarthSimplified;
        public Texture skyboxDayCubemap, skyboxNightCubemap;

        [Range(-10, 10)]
        public float dayCycleSpeed = 1f;

        public bool setTimeAndAzimuth;

        [Range(0, 24)]
        public float timeOfDay;

        [Range(0, 360)]
        public float azimuth = 15f;

        [Range(0, 2f)]
        public float exposure = 1f;

        [Tooltip("Used to create clouds")]
        public VoxelDefinition cloudVoxel;

        [Range(0, 255)]
        public int cloudCoverage = 110;

        [Range(0, 1024)]
        public int cloudAltitude = 150;

        public Color skyTint = new Color(0.52f, 0.5f, 1f);

        public Color groundColor = new Color(0.369f, 0.349f, 0.341f);

        [ColorUsage(showAlpha: false)]
        [Tooltip("Requires enabling the option Colored Shadows in Voxel Play Environment")]
        public Color shadowTintColor;

        [Header("Lighting")]
        [Tooltip("Range multiplier for the point lights")]
        public float lightScattering = 0.01f;
        [Tooltip("Intensity multiplier for the point lights")]
        public float lightIntensityMultiplier = 2f;
        [Tooltip("If enabled, Sun light will decrease vertically when crossing underground chunks.")]
        public bool lightSunAttenUnderground = true;
        [Tooltip("The speed at which the sun light attenuates underground")]
        [Range(1, 5)] public int lightSunAttenuation = 1;
        [Tooltip("The speed at which the torch light attenuates across distance")]
        [Range(1, 5)] public int lightTorchAttenuation = 1;

        [Header("Water Properties")]
        public Color underWaterFogColor = new Color(0.118f, 0.247f, 0.455f, 0.235f);
        [Range(0, 3)]
        public float waveAmplitude = 1f;

        [Tooltip("Animation speed of the non-realistic water")]
        public float waveAnimationSpeed = 1.0f;
        public float specularIntensity = 2f;
        public float specularPower = 64;
        [Tooltip("If water is affected by gravity or spreads in build mode")]
        public bool waterSpreadsInBuildMode;
        public Color foamColor = Color.white;

        [Header("Realistic Water")]
        public Color waterColor = new Color(0.231f, 0.455f, 0.82f, 0.31f); // (0.26f, 0.46f, 0.76f);
        [Range(0f, 1f)]
        public float waveScale = 0.1f;
        public float waveSpeed = 0.4f;
        public float refractionDistortion = 0.08f;
        [Range(0f, 1f)]
        public float fresnel = 0.9f;
        public float normalStrength = 2f;
        [Range(0.49f, 0.555f)]
        public float oceanWaveThreshold = 0.512f;
        [Range(0, 100)]
        public float oceanWaveIntensity = 12f;

        [Header("FX")]
        [Tooltip("Duration for the emission animation on certain materials")]
        public float emissionAnimationSpeed = 0.5f;
        public float emissionMinIntensity = 0.5f;
        public float emissionMaxIntensity = 1.2f;

        [Tooltip("Duration for the voxel damage cracks")]
        public float damageDuration = 3f;
        public Texture2D[] voxelDamageTextures;

        [Tooltip("Add particles when the voxel consolidates after collapsing")]
        public bool addParticlesOnDestroy;

        public GameObject damageParticle;

        [Range(0, 0.2f)]
        public float minParticleSize = 0.04f;
        [Range(0, 0.2f)]
        public float maxParticleSize = 0.1f;

        [Tooltip("Play a sound when the voxel consolidates after collapsing")]
        public bool playSoundOnDestroy;

        public float gravity = -9.8f;

        [Tooltip("When set to true, voxel types with 'Trigger Collapse' will fall along nearby voxels marked with 'Will Collapse' flag")]
        public bool collapseOnDestroy = true;

        [Tooltip("The maximum number of voxels that can fall at the same time")]
        public int collapseAmount = 50;

        [Tooltip("Delay for consolidating collapsed voxels into normal voxels. A value of zero keeps dynamic voxels in the scene. Note that consolidation takes place when chunk is not in frustum to avoid visual glitches.")]
        public int consolidateDelay = 5;

        [Tooltip("Destroy the voxel when it consolidates after collapsing")]
        public bool destroyOnConsolidate;

        [Tooltip("Resolution used for the downscaled textures of the original voxels used in the dropped recoverable small voxels. Set this to 0 to use the same voxel textures.")]
        public int dropVoxelTextureResolution = 64;

        [Header("Additional Objects")]
        public VoxelDefinition[] moreVoxels;
        public ItemDefinition[] items;

        [HideInInspector]
        public string resourcesLocation;


        void OnEnable () {
            if (biomes == null) {
                biomes = new BiomeDefinition[0];
            }
            UpdateResourcesLocation();
        }

        public void UpdateResourcesLocation () {
#if UNITY_EDITOR
            try {
                string location = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
                int i = location.IndexOf("Resources/");
                if (i > 0) {
                    location = location.Substring(i + 10);
                    if (!string.Equals(resourcesLocation, location)) {
                        resourcesLocation = location;
                        EditorUtility.SetDirty(this);
                    }
                }
            }
            catch {
            }
#endif
            if (string.IsNullOrEmpty(resourcesLocation)) {
                resourcesLocation = "Worlds/" + name;
            }
        }

        void OnValidate () {
            lightScattering = Mathf.Max(lightScattering, 0);
            lightIntensityMultiplier = Mathf.Max(lightIntensityMultiplier, 0);
            specularIntensity = Mathf.Max(specularIntensity, 0);
            specularPower = Mathf.Max(specularPower, 0);
            normalStrength = Mathf.Max(normalStrength, 0);
            emissionAnimationSpeed = Mathf.Max(emissionAnimationSpeed, 0);
            emissionMinIntensity = Mathf.Max(emissionMinIntensity, 0);
            consolidateDelay = Mathf.Max(consolidateDelay, 0);
            dropVoxelTextureResolution = Mathf.Max(dropVoxelTextureResolution, 0);

            VoxelPlayEnvironment env = VoxelPlayEnvironment.instance;
            if (env != null && this == env.world) {
                if (setTimeAndAzimuth) {
                    env.SetTimeOfDay(timeOfDay, azimuth);
                }
                env.UpdateMaterialProperties();
                if (terrainGenerator != null && !terrainGenerator.isInitialized) {
                    terrainGenerator.Initialize();
                }
            }
        }

        public VoxelPlayDetailGenerator GetGenerator<T> () {
            if (detailGenerators == null) return default;
            for (int k = 0; k < detailGenerators.Length; k++) {
                if (detailGenerators[k] is T) {
                    return detailGenerators[k];
                }
            }
            return default;
        }

        /// <summary>
        /// Adds a new voxel definition to the world definition.
        /// </summary>
        public void AddVoxelDefinition (VoxelDefinition vd) {
            if (moreVoxels == null) {
                moreVoxels = new VoxelDefinition[1];
                moreVoxels[0] = vd;
                return;
            }
            List<VoxelDefinition> list = new List<VoxelDefinition>(moreVoxels);
            if (!list.Contains(vd)) {
                list.Add(vd);
                moreVoxels = list.ToArray();
            }
        }



    }

}