using System;
using UnityEngine;

namespace VoxelPlay {


    [CreateAssetMenu(menuName = "Voxel Play/Biome Definition", fileName = "BiomeDefinition", order = 100)]
    [HelpURL("https://kronnect.com/docs/voxel-play/")]
    public partial class BiomeDefinition : ScriptableObject {

        public BiomeZone[] zones;

        // Used by biome map explorer
        [NonSerialized]
        public int biomeMapOccurrences;

        /// <summary>
        /// If this biome is visible in the biome explorer
        /// </summary>
        public bool showInBiomeMap = true;

        public Color biomeMapColor;

        [NonSerialized]
        public Color biomeMapColorTemp;

        [Tooltip("Voxel definition used for voxels that are on the surface in this biome.")]
        public VoxelDefinition voxelTop;
        public BiomeSurfaceVoxel[] voxelTopAdditional;

        [Tooltip("Voxel definition used for underground voxels in this biome.")]
        public VoxelDefinition voxelDirt;
        public BiomeUndergroundVoxel[] voxelDirtAdditional;

        [Tooltip("Optional voxel to be used at the bottom of ocean/lakes. If not assigned, the Voxel Dirt will be used instead.")]
        public VoxelDefinition voxelLakeBed;

        [Tooltip("Optional water voxel for this biome. If not assigned, the terrain generator default water is used.")]
        public VoxelDefinition voxelWater;

        public BiomeOre[] ores;

        [Range(0, 0.05f)]
        public float treeDensity = 0.02f;
        public BiomeTree[] trees;

        [Range(0, 1)]
        public float vegetationDensity = 0.05f;
        public BiomeVegetation[] vegetation;

        [Range(0, 1)]
        public float underwaterVegetationDensity = 0.05f;
        public BiomeVegetation[] underwaterVegetation;

        [Range(0, 1)]
        public float undergroundVegDensity = 0.05f;
        public BiomeVegetation[] undergroundVegetation;

        [Range(0, 1)]
        public float undergroundCeilingVegDensity = 0.05f;
        public BiomeVegetation[] undergroundCeilingVegetation;


        const int MINIMUM_ALTITUDE = -500;
        const int MAXIMUM_ALTITUDE = 500;



        VoxelDefinition[] allTopVoxels;
        VoxelDefinition[][] allDirtVoxels;

        private void Awake () {
            ValidateSettings(true);
        }

        private void OnValidate () {
            ValidateSettings(false);
        }

        /// <summary>
        /// Initialization function called by Terrain Generator
        /// </summary>
        public void Init () {
            if (voxelLakeBed == null) {
                voxelLakeBed = voxelDirt;
            }
            if (undergroundVegetation == null) {
                undergroundVegetation = new BiomeVegetation[0];
            }
            if (undergroundCeilingVegetation == null) {
                undergroundCeilingVegetation = new BiomeVegetation[0];
            }
			VoxelPlayEnvironment env = VoxelPlayEnvironment.instance;
			if (env != null) {
				for (int v = 0; v < undergroundVegetation.Length; v++) {
					if (undergroundVegetation[v].vegetation == null) {
						undergroundVegetation[v].vegetation = env.defaultVoxel;
					} else {
						env.AddVoxelDefinition(undergroundVegetation[v].vegetation);
					}
				}
				for (int v = 0; v < undergroundCeilingVegetation.Length; v++) {
					if (undergroundCeilingVegetation[v].vegetation == null) {
						undergroundCeilingVegetation[v].vegetation = env.defaultVoxel;
					} else {
						env.AddVoxelDefinition(undergroundCeilingVegetation[v].vegetation);
					}
				}
				// Centralized registration of all voxel definitions used by this biome
				RegisterVoxelDefinitions(env);
			}
			if (trees != null) {
                int treeCount = trees.Length;
                for (int t = 0; t < treeCount; t++) {
                    BiomeTree tree = trees[t];
                    ModelDefinition treeModel = tree.tree;
                    if (treeModel != null) {
                        int bitCount = treeModel.bits.Length;
                        for (int v = 0; v < bitCount; v++) {
                            VoxelDefinition vd = treeModel.bits[v].voxelDefinition;
							if (vd != null) {
								vd.isTree = true;
								if (env != null) env.AddVoxelDefinition(vd);
							}
                        }
                    }
                }
            }

            // Consolidate all top/dirt voxel definitions in a single array
            DistributeSurfaceVoxels(voxelTop, voxelTopAdditional, ref allTopVoxels);
            DistributeUndergroundVoxels(voxelDirt, voxelDirtAdditional, ref allDirtVoxels);
        }

		public void RegisterVoxelDefinitions (VoxelPlayEnvironment env) {
			if (env == null) return;
			ForEachVoxelDefinition(env.AddVoxelDefinition);
		}

		public void ForEachVoxelDefinition (Action<VoxelDefinition> action) {
			if (action == null) return;
			// main surface/dirt/lake bed
			if (voxelTop != null) action(voxelTop);
			if (voxelDirt != null) action(voxelDirt);
			if (voxelLakeBed != null) action(voxelLakeBed);
			if (voxelWater != null) action(voxelWater);
			// additions
			if (voxelTopAdditional != null) {
				for (int i = 0; i < voxelTopAdditional.Length; i++) {
					VoxelDefinition vd = voxelTopAdditional[i].voxelDefinition;
					if (vd != null) action(vd);
				}
			}
			if (voxelDirtAdditional != null) {
				for (int i = 0; i < voxelDirtAdditional.Length; i++) {
					VoxelDefinition vd = voxelDirtAdditional[i].voxelDefinition;
					if (vd != null) action(vd);
				}
			}
			// vegetation
			if (vegetation != null) {
				for (int i = 0; i < vegetation.Length; i++) {
					VoxelDefinition vd = vegetation[i].vegetation;
					if (vd != null) action(vd);
				}
			}
			if (underwaterVegetation != null) {
				for (int i = 0; i < underwaterVegetation.Length; i++) {
					VoxelDefinition vd = underwaterVegetation[i].vegetation;
					if (vd != null) action(vd);
				}
			}
			if (undergroundVegetation != null) {
				for (int i = 0; i < undergroundVegetation.Length; i++) {
					VoxelDefinition vd = undergroundVegetation[i].vegetation;
					if (vd != null) action(vd);
				}
			}
			if (undergroundCeilingVegetation != null) {
				for (int i = 0; i < undergroundCeilingVegetation.Length; i++) {
					VoxelDefinition vd = undergroundCeilingVegetation[i].vegetation;
					if (vd != null) action(vd);
				}
			}
			// trees bits
			if (trees != null) {
				for (int t = 0; t < trees.Length; t++) {
					ModelDefinition tree = trees[t].tree;
					if (tree == null || tree.bits == null) continue;
					for (int b = 0; b < tree.bits.Length; b++) {
						VoxelDefinition vd = tree.bits[b].voxelDefinition;
						if (vd != null) action(vd);
					}
				}
			}
			// ores
			if (ores != null) {
				for (int i = 0; i < ores.Length; i++) {
					VoxelDefinition vd = ores[i].ore;
					if (vd != null) action(vd);
				}
			}
		}

        public void ValidateSettings (bool showDebugWarnings) {

            if (ores == null) {
                ores = new BiomeOre[0];
            }
            if (trees == null) {
                trees = new BiomeTree[0];
            }
            if (vegetation == null) {
                vegetation = new BiomeVegetation[0];
            }
            if (underwaterVegetation == null) {
                underwaterVegetation = new BiomeVegetation[0];
            }

            if (voxelTop != null) {
                if (voxelTop.biomeDirtCounterpart == null) {
                    voxelTop.biomeDirtCounterpart = voxelDirt;
                }
            }
            if (voxelDirt != null) {
                if (voxelDirt.biomeSurfaceCounterpart == null) {
                    voxelDirt.biomeSurfaceCounterpart = voxelTop;
                }
            }

            if (voxelTopAdditional != null && voxelDirtAdditional != null) {
                int voxelTopAdditionalLength = voxelTopAdditional.Length;
                for (int i = 0; i < voxelTopAdditionalLength; i++) {
                    BiomeSurfaceVoxel voxel = voxelTopAdditional[i];
                    if (voxel.voxelDefinition == null) continue;
                    if (voxel.voxelDefinition.biomeDirtCounterpart != null) continue;
                    if (voxelDirtAdditional.Length > i) {
                        voxel.voxelDefinition.biomeDirtCounterpart = voxelDirtAdditional[i].voxelDefinition;
                    } else {
                        voxel.voxelDefinition.biomeDirtCounterpart = voxelDirt;
                    }
                    if (voxel.voxelDefinition.biomeSurfaceCounterpart != null) continue;
                    if (voxelTopAdditional.Length > i) {
                        voxel.voxelDefinition.biomeSurfaceCounterpart = voxelTopAdditional[i].voxelDefinition;
                    } else {
                        voxel.voxelDefinition.biomeSurfaceCounterpart = voxelTop;
                    }
                }
            }

            VoxelPlayEnvironment env = VoxelPlayEnvironment.instance;
            if (env == null) return;

            if (zones != null) {
                for (int z = 0; z < zones.Length; z++) {
                    BiomeZone zone = zones[z];
                    zone.biome = this;
                    if (zone.altitudeMin == 0 && zone.altitudeMax == 0) {
                        if (showDebugWarnings) Debug.LogWarning("Biome " + name + " has no minimum/maximum altitude defined. Assigning a default value of 255 to maximum altitude.", this);
                        zone.altitudeMax = 255;
                    }
                    if (zone.moistureMin == 0 && zone.moistureMax == 0) {
                        if (showDebugWarnings) Debug.LogWarning("Biome " + name + " has no minimum/maximum moisture defined. Assigning a default value of 1 to maximum moisture.", this);
                        zone.moistureMax = 1;
                    }
                    zones[z] = zone;
                }
            }

            if (vegetation != null) {
                for (int v = 0; v < vegetation.Length; v++) {
                    if (vegetation[v].vegetation == null) {
                        vegetation[v].vegetation = env.defaultVoxel;
                    }
                }
            }

            if (underwaterVegetation != null) {
                for (int v = 0; v < underwaterVegetation.Length; v++) {
                    if (underwaterVegetation[v].vegetation == null) {
                        underwaterVegetation[v].vegetation = env.defaultVoxel;
                    }
                }
            }
        }


        /// <summary>
        /// For optimization purposes this function precomputes random values and distributes voxels in an array
        /// </summary>
        void DistributeSurfaceVoxels (VoxelDefinition mainVoxel, BiomeSurfaceVoxel[] additionalVoxels, ref VoxelDefinition[] voxelsArray) {

            VoxelPlayEnvironment env = VoxelPlayEnvironment.instance;
            if (env != null) {
                if (mainVoxel == null) {
                    mainVoxel = env.defaultVoxel;
                }
                // Ensure textures are added to the engine
                if (additionalVoxels != null) {
                    for (int k = 0; k < additionalVoxels.Length; k++) {
                        env.AddVoxelDefinition(additionalVoxels[k].voxelDefinition);
                    }
                }
            }

            voxelsArray = new VoxelDefinition[100];
            float acumProb = 0;
            int currentIndex = 0;
            if (additionalVoxels != null) {
                for (int k = 0; k < additionalVoxels.Length; k++) {
                    if (additionalVoxels[k].voxelDefinition == null)
                        continue;
                    acumProb += additionalVoxels[k].probability;
                    acumProb = Mathf.Clamp01(acumProb);
                    int nextProb = (int)(acumProb * 100);
                    if (currentIndex < nextProb) {
                        VoxelDefinition vd = additionalVoxels[k].voxelDefinition;
                        do {
                            voxelsArray[currentIndex++] = vd;
                        } while (currentIndex < nextProb);
                    }
                }
            }
            while (currentIndex < 100) {
                voxelsArray[currentIndex++] = mainVoxel;
            }
        }

        /// <summary>
        /// For optimization purposes this function precomputes random values and distributes voxels in an array
        /// </summary>
        void DistributeUndergroundVoxels (VoxelDefinition mainVoxel, BiomeUndergroundVoxel[] additionalVoxels, ref VoxelDefinition[][] voxelsArray) {

            VoxelPlayEnvironment env = VoxelPlayEnvironment.instance;
            if (env != null) {
                if (mainVoxel == null) {
                    mainVoxel = env.defaultVoxel;
                }
                // Ensure textures are added to the engine
                if (additionalVoxels != null) {
                    for (int k = 0; k < additionalVoxels.Length; k++) {
                        env.AddVoxelDefinition(additionalVoxels[k].voxelDefinition);
                    }
                }
            }

            voxelsArray = new VoxelDefinition[1000][];
            for (int altitude = MINIMUM_ALTITUDE; altitude < MAXIMUM_ALTITUDE; altitude++) {
                VoxelDefinition[] voxelsThisAltitude = new VoxelDefinition[100];
                int altitudeIndex = altitude - MINIMUM_ALTITUDE;
                voxelsArray[altitudeIndex] = voxelsThisAltitude;
                float acumProb = 0;
                int currentIndex = 0;
                if (additionalVoxels != null) {
                    for (int k = 0; k < additionalVoxels.Length; k++) {
                        if (additionalVoxels[k].voxelDefinition == null)
                            continue;
                        if (additionalVoxels[k].altitudeMin != 0 && additionalVoxels[k].altitudeMin > altitude)
                            continue;
                        if (additionalVoxels[k].altitudeMax != 0 && additionalVoxels[k].altitudeMax < altitude)
                            continue;

                        acumProb += additionalVoxels[k].probability;
                        acumProb = Mathf.Clamp01(acumProb);
                        int nextProb = (int)(acumProb * 100);
                        if (currentIndex < nextProb) {
                            VoxelDefinition vd = additionalVoxels[k].voxelDefinition;
                            do {
                                voxelsThisAltitude[currentIndex++] = vd;
                            } while (currentIndex < nextProb);
                        }
                    }
                }
                while (currentIndex < 100) {
                    voxelsThisAltitude[currentIndex++] = mainVoxel;
                }
            }
        }

        /// <summary>
        /// Returns a random voxel for the surface at the given position
        /// </summary>
        public VoxelDefinition GetVoxelTop (Vector3 position) {
            float rand = WorldRand.GetValue(position);
            int index = (int)(rand * 100);
            return allTopVoxels[index];
        }

        /// <summary>
        /// Returns a random voxel for the underground at the given position
        /// </summary>
        public VoxelDefinition GetVoxelDirt (Vector3 position) {
            position.y -= MINIMUM_ALTITUDE;
            float rand = WorldRand.GetValue(position);
            int altitude;
            if (position.y < 0) {
                altitude = 0;
            } else if (position.y > 999) {
                altitude = 999;
            } else {
                altitude = (int)position.y;
            }
            int index = (int)(rand * 100);
            VoxelDefinition vd = allDirtVoxels[altitude][index];
            return vd;
        }

    }


    [Serializable]
    public struct BiomeZone {
        public float altitudeMin;
        public float altitudeMax;

        [Range(0, 1f)]
        public float moistureMin;
        [Range(0, 1f)]
        public float moistureMax;

        [NonSerialized]
        public BiomeDefinition biome;
    }

    [Serializable]
    public partial class BiomeSurfaceVoxel {
        public VoxelDefinition voxelDefinition;
        [Range(0, 1)]
        public float probability;
    }

    [Serializable]
    public partial class BiomeUndergroundVoxel {
        public VoxelDefinition voxelDefinition;
        [Range(0, 1)]
        public float probability;
        public int altitudeMin, altitudeMax;
    }


    [Serializable]
    public struct BiomeTree {
        public ModelDefinition tree;
        public float probability;
    }

    [Serializable]
    public struct BiomeVegetation {
        public VoxelDefinition vegetation;
        public float probability;
    }

    [Serializable]
    public struct BiomeOre {
        public VoxelDefinition ore;
        [Range(0, 1)]
        [Tooltip("Per chunk minimum probability. This min probability should start at the max value of any previous ore so all probabilities stack up.")]
        public float probabilityMin;
        [Range(0, 1)]
        [Tooltip("Per chunk maximum probability")]
        public float probabilityMax;
        [Tooltip("Min depth from surface")]
        public int depthMin;
        [Tooltip("Max depth from surface. Required.")]
        public int depthMax;
        [Tooltip("Min size of vein")]
        public int veinMinSize;
        [Tooltip("Max size of vein")]
        public int veinMaxSize;
        [Tooltip("Per chunk minimum number of veins")]
        public int veinsCountMin;
        [Tooltip("Per chunk maximum number of veins")]
        public int veinsCountMax;
    }

}