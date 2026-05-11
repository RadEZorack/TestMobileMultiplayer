using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public enum TerrainStepType {
        SampleHeightMapTexture = 0,
        SampleRidgeNoiseFromTexture = 1,
        SampleHeightMapFractal = 2,
        SampleHeightMapUnityTerrain = 3,
        Constant = 100,
        Copy = 101,
        Random = 102,
        Invert = 103,
        Shift = 104,
        BeachMask = 105,
        AddAndMultiply = 200,
        MultiplyAndAdd = 201,
        Exponential = 202,
        Threshold = 203,
        FlattenOrRaise = 204,
        Island = 205,
        Remap = 206,
        Abs = 207,
        Terraces = 208,
        BlendAdditive = 300,
        BlendMultiply = 301,
        Clamp = 302,
        Select = 303,
        Fill = 304,
        Test = 305,
        Min = 306,
        Max = 307,
        Subtract = 308,
        Divide = 309
    }

    [Serializable]
    public struct NodeLayout {
        public Vector2 position;
    }

    [Serializable]
    public struct TerrainGraphLayoutData {
        public Vector2 altitudeOutputPosition;
        public Vector2 moistureOutputPosition;
        public NodeLayout[] stepPositions;
    }

    [Serializable]
    public enum TerrainGraphEndpointKind {
        StepNode = 0,
        Reroute = 1,
        AltitudeOutput = 2,
        MoistureOutput = 3
    }

    [Serializable]
    public struct TerrainGraphRerouteData {
        public Vector2 position;
    }

    [Serializable]
    public struct TerrainGraphEditorEdgeData {
        public TerrainGraphEndpointKind sourceKind;
        public int sourceIndex;
        public TerrainGraphEndpointKind targetKind;
        public int targetIndex;
        public int targetPortIndex;
    }

    [Serializable]
    public struct TerrainGraphEditorStateData {
        public TerrainGraphRerouteData[] reroutes;
        public TerrainGraphEditorEdgeData[] edges;
    }

    [Serializable]
    public struct StepData {
        public bool enabled;
        public TerrainStepType operation;
        public string description;
        public Texture2D noiseTexture;
        public TerrainData terrainData;
        [Range(0.001f, 2f)]
        public float frecuency;
        public Vector2 offset;
        [Range(0, 1f)]
        public float noiseRangeMin;
        [Range(0, 1f)]
        public float noiseRangeMax;

        [Range(1, 8)]
        public int octaves;
        public float persistence;
        public float lacunarity;

        public int inputIndex0;
        public int inputIndex1;

        public float threshold, thresholdShift, thresholdParam;

        public float param, param2, param3;
        public float weight0, weight1;

        public float min, max;
        [HideInInspector]
        public uint editorHeightUnitMask;
        [HideInInspector]
        public uint editorHeightPercentMask;

        [HideInInspector, NonSerialized]
        public float[] noiseValues;
        [HideInInspector, NonSerialized]
        public int noiseTextureSize;
        [HideInInspector, NonSerialized]
        public float value;
        [HideInInspector, NonSerialized]
        public Texture2D lastTextureLoaded;
        [HideInInspector, NonSerialized]
        public TerrainData lastTerrainDataLoaded;
    }

    public partial class BiomeDefinition {
        [NonSerialized]
        public int biomeGeneration;
    }

    public partial interface ITerrainDefaultGenerator {
        StepData[] Steps { get; set; }
    }


    [CreateAssetMenu(menuName = "Voxel Play/Terrain Generators/Multi-Step Terrain Generator", fileName = "MultiStepTerrainGenerator", order = 101)]
    [HelpURL("https://kronnect.com/docs/voxel-play/")]
    public partial class TerrainDefaultGenerator : VoxelPlayTerrainGenerator, ITerrainDefaultGenerator {

        const string DEFAULT_MOISTURE_TEXTURE_RESOURCE_PATH = "Worlds/Earth/Noise/NoiseMoisture";

        [SerializeField]
        StepData[] steps;

        [HideInInspector]
        public NodeLayout[] nodePositions;

        [HideInInspector]
        public int terminalStepIndex = -1;

        [HideInInspector]
        public int moistureTerminalStepIndex = -1;

        [HideInInspector]
        public int graphVersion;

        [HideInInspector]
        public TerrainGraphLayoutData graphLayoutV2;

        [HideInInspector]
        public TerrainGraphEditorStateData graphEditorStateV2;

        [TextArea]
        public string hint = "The final value returned by the steps chain should be in 0..1 range. This value will be multiplied by the Max Height param to determine the terrain altitude.";

        public StepData[] Steps {
            get { return steps; }
            set { steps = value; }
        }

        [Range(0, 1f)]
        public float seaDepthMultiplier = 0.4f;
        [Range(0, 0.02f)]
        public float beachWidth = 0.001f;
        public VoxelDefinition waterVoxel;
        public VoxelDefinition shoreVoxel;
        [Tooltip("Used by terrain generator to set a hard limit in chunks at minimum height")]
        public VoxelDefinition bedrockVoxel;

        [Header("Underground")]
        public bool addOre;

        [Header("Moisture Parameters")]
        public Texture2D moisture;
        [Range(0, 1f)]
        public float moistureScale = 0.2f;

        [Header("Half-Step Surface (Microvoxels)")]
        [Tooltip("Enable half-voxel on terrain surface when fractional height < 0.5.")]
        public bool enableHalfStepSurface;
        MicroVoxels halfSurfaceVoxelTemplate;

        // Internal fields
        protected float[] moistureValues;
        protected int noiseMoistureTextureSize;
        protected float seaLevelAlignedWithInt, beachLevelAlignedWithInt;
        protected bool paintShore;
        protected HeightMapInfo[] heightChunkData;
        protected Texture2D lastMoistureTextureLoaded;
        protected int generation;
        [NonSerialized] int cachedAltitudeTerminalIndex = -1;
        [NonSerialized] int cachedMoistureTerminalIndex = -1;
        [NonSerialized] bool cachedUsesGraphMoisture;
        [NonSerialized] float cachedInvMaxHeight = 1f;

        public override void GetTerrainVoxelDefinitions (List<VoxelDefinition> vds) {
            if (shoreVoxel != null) vds.Add(shoreVoxel);
            if (bedrockVoxel != null) vds.Add(bedrockVoxel);
            if (env == null || env.world == null || env.world.biomes == null) return;
            foreach (var biome in world.biomes) {
                if (biome.voxelTop != null) vds.Add(biome.voxelTop);
                if (biome.voxelTopAdditional != null) {
                    foreach (var voxel in biome.voxelTopAdditional) {
                        if (voxel.voxelDefinition != null) {
                            vds.Add(voxel.voxelDefinition);
                        }
                    }
                }
                if (biome.voxelDirt != null) vds.Add(biome.voxelDirt);
                if (biome.voxelDirtAdditional != null) {
                    foreach (var voxel in biome.voxelDirtAdditional) {
                        if (voxel.voxelDefinition != null) {
                            vds.Add(voxel.voxelDefinition);
                        }
                    }
                }
                if (biome.voxelLakeBed != null) vds.Add(biome.voxelLakeBed);
                if (biome.voxelWater != null) vds.Add(biome.voxelWater);
            }
        }

        protected override void Init () {

            if (env.world != null && env.world.biomes != null) {
                for (int k = 0; k < env.world.biomes.Length; k++) {
                    BiomeDefinition biome = env.world.biomes[k];
                    if (biome != null) {
                        biome.Init();
                    }
                }
                if (env.world.defaultBiome != null) {
                    env.world.defaultBiome.Init();
                }
            }

            seaLevelAlignedWithInt = waterLevel / maxHeight;
            beachLevelAlignedWithInt = (waterLevel + 1f) / maxHeight;
            cachedInvMaxHeight = maxHeight != 0 ? 1f / maxHeight : 0f;
            if (steps != null) {
                for (int k = 0; k < steps.Length; k++) {
                    if (steps[k].noiseTexture != null) {
                        bool repeated = false;
                        for (int j = 0; j < k; j++) {
                            if (steps[k].noiseTexture == steps[j].noiseTexture) {
                                steps[k].noiseValues = steps[j].noiseValues;
                                steps[k].noiseTextureSize = steps[j].noiseTextureSize;
                                repeated = true;
                                break;
                            }
                        }
                        if (!repeated && (steps[k].noiseTextureSize == 0 || steps[k].noiseValues == null || steps[k].lastTextureLoaded == null || steps[k].noiseTexture != steps[k].lastTextureLoaded)) {
                            steps[k].lastTextureLoaded = steps[k].noiseTexture;
                            steps[k].noiseValues = NoiseTools.LoadNoiseTexture(steps[k].noiseTexture, out steps[k].noiseTextureSize);
                        }
                    } else if (steps[k].terrainData != null) {
                        bool repeated = false;
                        for (int j = 0; j < k; j++) {
                            if (steps[k].terrainData == steps[j].terrainData) {
                                steps[k].noiseValues = steps[j].noiseValues;
                                steps[k].noiseTextureSize = steps[j].noiseTextureSize;
                                repeated = true;
                                break;
                            }
                        }
                        if (!repeated && (steps[k].noiseTextureSize == 0 || steps[k].noiseValues == null || steps[k].lastTerrainDataLoaded == null || steps[k].terrainData != steps[k].lastTerrainDataLoaded)) {
                            steps[k].lastTerrainDataLoaded = steps[k].terrainData;
                            steps[k].noiseValues = NoiseTools.LoadHeightmapFromTerrainData(steps[k].terrainData, out steps[k].noiseTextureSize);
                        }
                    }
                }
            }
            TerrainGraphStepRuntime.SanitizeInputReferences(steps);
            cachedAltitudeTerminalIndex = ResolveAltitudeTerminalIndex();
            cachedMoistureTerminalIndex = ResolveMoistureTerminalIndex();
            cachedUsesGraphMoisture = cachedMoistureTerminalIndex >= 0;
            Texture2D resolvedMoistureTexture = GetResolvedLegacyMoistureTexture();
            if (resolvedMoistureTexture != null && (noiseMoistureTextureSize == 0 || moistureValues == null || lastMoistureTextureLoaded == null || lastMoistureTextureLoaded != resolvedMoistureTexture)) {
                lastMoistureTextureLoaded = resolvedMoistureTexture;
                moistureValues = NoiseTools.LoadNoiseTexture(resolvedMoistureTexture, out noiseMoistureTextureSize);
            }
            if (waterVoxel == null) {
                waterVoxel = env.defaultWaterVoxel;
            }
            env.currentWaterVoxelDefinition = waterVoxel;
            if (waterVoxel != null && waterVoxel.height == 0) {
                env.ShowError("Water voxel definition height is 0. It should be greater than 0. Pleae check the water voxel definition.");
            }

            paintShore = shoreVoxel != null;

            // Ensure voxels are available
            env.AddVoxelDefinitions(shoreVoxel, waterVoxel, bedrockVoxel);

            halfSurfaceVoxelTemplate = MicroVoxels.halfSurfaceVoxelTemplate;
        }

        public override void InvalidateRuntimeCaches () {
            base.InvalidateRuntimeCaches();
            cachedAltitudeTerminalIndex = -1;
            cachedMoistureTerminalIndex = -1;
            cachedUsesGraphMoisture = false;
            cachedInvMaxHeight = 1f;
        }

        /// <summary>
        /// Gets the altitude and moisture (in 0-1 range).
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="z">The z coordinate.</param>
        /// <param name="altitude">Altitude in 0-1 range.</param>
        /// <param name="moisture">Moisture in 0-1 range.</param>
        public override void GetHeightAndMoisture (double x, double z, out float altitude, out float moisture) {

            if (!isInitialized) {
                Initialize();
            }

            EvaluateSteps(x, z, out bool allowBeach);

            int altitudeTerminal = cachedAltitudeTerminalIndex;
            if (altitudeTerminal >= 0) {
                altitude = steps[altitudeTerminal].value;
            } else {
                altitude = -9999; // no terrain so make altitude very low so every chunk be considered above terrain for GI purposes
            }

            if (cachedUsesGraphMoisture) {
                moisture = steps[cachedMoistureTerminalIndex].value;
            } else {
                moisture = SampleLegacyMoisture(x, z);
            }

            // Remove any potential beach
            if (altitude < beachLevelAlignedWithInt && altitude >= seaLevelAlignedWithInt) {
                float depth = beachLevelAlignedWithInt - altitude;
                if (depth > beachWidth || !allowBeach) {
                    altitude = seaLevelAlignedWithInt - 0.0001f;
                }
            }

            // Adjusts sea depth
            if (altitude < seaLevelAlignedWithInt) {
                float depth = seaLevelAlignedWithInt - altitude;
                altitude = seaLevelAlignedWithInt - 0.0001f - depth * seaDepthMultiplier;
            }

        }

        void EvaluateSteps (double x, double z, out bool allowBeach) {
            TerrainGraphStepRuntime.EvaluateSteps(steps, x, z, cachedInvMaxHeight, out allowBeach);
        }

        bool IsUsableTerminalStep (int stepIndex) {
            return steps != null && stepIndex >= 0 && stepIndex < steps.Length && steps[stepIndex].enabled;
        }

        int ResolveAltitudeTerminalIndex () {
            if (steps == null || steps.Length == 0) return -1;
            if (IsUsableTerminalStep(terminalStepIndex)) return terminalStepIndex;
            if (graphVersion >= 2) return -1;
            for (int k = steps.Length - 1; k >= 0; k--) {
                if (steps[k].enabled) return k;
            }
            return -1;
        }

        int ResolveMoistureTerminalIndex () {
            if (steps == null || steps.Length == 0) return -1;
            if (IsUsableTerminalStep(moistureTerminalStepIndex)) return moistureTerminalStepIndex;
            return -1;
        }

        public Texture2D GetResolvedLegacyMoistureTexture () {
            if (moisture != null) return moisture;
            return Resources.Load<Texture2D>(DEFAULT_MOISTURE_TEXTURE_RESOURCE_PATH);
        }

        public float GetResolvedLegacyMoistureScale () {
            return moistureScale > 0 ? moistureScale : 0.2f;
        }

        float SampleLegacyMoisture (double x, double z) {
            if (moistureValues == null || noiseMoistureTextureSize <= 0) {
                return 0f;
            }
            float legacyMoistureScale = GetResolvedLegacyMoistureScale();
            return NoiseTools.GetNoiseValueBilinear(moistureValues, noiseMoistureTextureSize, x * legacyMoistureScale, z * legacyMoistureScale);
        }

        /// <summary>
        /// Paints the terrain inside the chunk defined by its central "position"
        /// </summary>
        /// <returns><c>true</c>, if terrain was painted, <c>false</c> otherwise.</returns>
		public override bool PaintChunk (VoxelChunk chunk) {

            Vector3d position = chunk.position;
            if (position.y + VoxelPlayEnvironment.CHUNK_HALF_SIZE < minHeight) {
                chunk.isAboveSurface = false;
                return false;
            }

            int bedrockRow = -1;
            bool usesBedrockVoxel = bedrockVoxel != null;
            if (position.y < minHeight + VoxelPlayEnvironment.CHUNK_HALF_SIZE) {
                bedrockRow = (int)(minHeight - (position.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE) + 1) * ONE_Y_ROW - 1;
            }
            position.x -= VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            position.y -= VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            position.z -= VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            Vector3d pos;

            int waterLevel = env.waterLevel;
            Voxel[] voxels = chunk.voxels;

            bool hasContent = false;
            bool isAboveSurface = false;
            generation++;
            env.GetHeightMapInfoFast(position.x, position.z, out heightChunkData);
            int shiftAmount = (int)Mathf.Log(VoxelPlayEnvironment.CHUNK_SIZE, 2);

            // iterate 256 slice of chunk (z/x plane = 16*16 positions)
            for (int arrayIndex = 0; arrayIndex < VoxelPlayEnvironment.ONE_Y_ROW; arrayIndex++) {
                float groundLevel = heightChunkData[arrayIndex].groundLevel;
                float surfaceLevel = waterLevel > groundLevel ? waterLevel : groundLevel;
                if (surfaceLevel < position.y) {
                    // position is above terrain or water
                    isAboveSurface = true;
                    continue;
                }
                BiomeDefinition biome = heightChunkData[arrayIndex].biome;
                if ((object)biome == null) {
                    biome = world.defaultBiome;
                    if ((object)biome == null)
                        continue;
                }
                VoxelDefinition biomeWaterVoxel = (object)biome.voxelWater != null ? biome.voxelWater : waterVoxel;

                int y = (int)(surfaceLevel - position.y);
                if (y >= VoxelPlayEnvironment.CHUNK_SIZE) {
                    y = VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
                }
                pos.y = position.y + y;
                pos.x = position.x + (arrayIndex & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE);
                pos.z = position.z + (arrayIndex >> shiftAmount);

                // Place voxels
                bool hasWater = false;
                int voxelIndex = y * ONE_Y_ROW + arrayIndex;

                if (pos.y > groundLevel) {

                    // water above terrain
                    if (pos.y == surfaceLevel) {
                        isAboveSurface = true;
                    }
                    while (pos.y > groundLevel && voxelIndex >= 0) {
                        voxels[voxelIndex].SetFastWater(biomeWaterVoxel);
                        voxelIndex -= ONE_Y_ROW;
                        pos.y--;
                        hasWater = true;
                    }

                    // Underwater vegetation
                    if (env.enableVegetation && biome.underwaterVegetationDensity > 0 && biome.underwaterVegetation.Length > 0 && pos.y == groundLevel) {
                        float rn = WorldRand.GetValue(pos);
                        if (rn < biome.underwaterVegetationDensity) {
                            if (voxelIndex >= VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE * ONE_Y_ROW) {
                                // request one vegetation voxel one position above which means the chunk above this one
                                Vector3d abovePos = pos;
                                abovePos.y++;
                                env.RequestVegetationCreation(abovePos, env.GetVegetation(biome.underwaterVegetation, rn / biome.underwaterVegetationDensity));
                            } else if (voxels[voxelIndex + ONE_Y_ROW].opaque < 15) {
                                // directly place a vegetation voxel above this voxel
                                chunk.SetVoxel(voxelIndex + ONE_Y_ROW, env.GetVegetation(biome.underwaterVegetation, rn / biome.underwaterVegetationDensity));
                                env.vegetationCreated++;
                            }
                        }
                    }

                } else if (pos.y == groundLevel) {
                    isAboveSurface = true;
                    if (voxels[voxelIndex].typeIndex == Voxel.EMPTY_TYPE_INDEX) {
                        if (paintShore && pos.y == waterLevel) {
                            // this is on the shore, place a shoreVoxel using chunk.SetVoxel which supports microvoxels
                            chunk.SetVoxel(voxelIndex, shoreVoxel);
                        } else {
                            // We're at the surface of the biome
                            // Check vegetation & tree probability
                            bool allowHalfStep = enableHalfStepSurface;
                            if (pos.y > waterLevel) {
                                float rn = WorldRand.GetValue(pos);
                                if (biome.treeDensity > 0 && rn < biome.treeDensity && biome.trees.Length > 0) {
                                    // request one tree at this position
                                    env.RequestTreeCreation(chunk, pos, env.GetTree(biome.trees, rn / biome.treeDensity));
                                    allowHalfStep = false;
                                } else if (biome.vegetationDensity > 0 && rn < biome.vegetationDensity && biome.vegetation.Length > 0) {
                                    if (voxelIndex >= VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE * ONE_Y_ROW) {
                                        // request one vegetation voxel one position above which means the chunk above this one
                                        Vector3d abovePos = pos;
                                        abovePos.y++;
                                        env.RequestVegetationCreation(abovePos, env.GetVegetation(biome.vegetation, rn / biome.vegetationDensity));
                                    } else {
                                        // directly place a vegetation voxel above this voxel
                                        if (env.enableVegetation) {
                                            chunk.SetVoxel(voxelIndex + ONE_Y_ROW, env.GetVegetation(biome.vegetation, rn / biome.vegetationDensity));
                                            env.vegetationCreated++;
                                        }
                                    }
                                }
                            }

                            // Draw the voxel top of the biome and also check for random vegetation and trees
                            VoxelDefinition topVoxel = biome.GetVoxelTop(pos);
                            chunk.SetVoxel(voxelIndex, topVoxel);  // using chunk.SetVoxel which supports microvoxels

                            // Optionally convert surface voxel into half-voxel using microvoxels
                            if (allowHalfStep) {
                                float frac = heightChunkData[arrayIndex].height - groundLevel;
                                if (frac < 0.5f) {
                                    chunk.SetMicroVoxels(voxelIndex, halfSurfaceVoxelTemplate);
                                }
                            }
                        }
                        voxelIndex -= ONE_Y_ROW;
                        pos.y--;
                    }
                }

                biome.biomeGeneration = generation;

                // fill hole with water
                int lastHoleIndex = -1;
                int firstHoleIndex = -1;
                while (voxelIndex > bedrockRow && voxels[voxelIndex].typeIndex == Voxel.HOLE_TYPE_INDEX && pos.y <= waterLevel) {
                    if (hasWater) {
                        voxels[voxelIndex].SetFastWater(biomeWaterVoxel);
                    }
                    lastHoleIndex = voxelIndex;
                    if (voxelIndex > firstHoleIndex) firstHoleIndex = voxelIndex;
                    voxelIndex -= ONE_Y_ROW;
                    pos.y--;
                }

                // Place lake/ocean bed
                if (voxelIndex > bedrockRow && voxels[voxelIndex].typeIndex == Voxel.EMPTY_TYPE_INDEX && voxelIndex + ONE_Y_ROW < VoxelPlayEnvironment.CHUNK_VOXEL_COUNT && voxels[voxelIndex + ONE_Y_ROW].hasWater) {
                    voxels[voxelIndex].SetFastOpaque(biome.voxelLakeBed);
                    voxelIndex -= ONE_Y_ROW;
                    pos.y--;
                }

                // Continue filling down
                for (; voxelIndex > bedrockRow; voxelIndex -= ONE_Y_ROW, pos.y--) {
                    if (voxels[voxelIndex].typeIndex == Voxel.EMPTY_TYPE_INDEX) { // avoid holes
                        VoxelDefinition dirtVoxel = biome.GetVoxelDirt(pos);
                        voxels[voxelIndex].SetFastOpaque(dirtVoxel);
                    } else if (voxels[voxelIndex].typeIndex == Voxel.HOLE_TYPE_INDEX) { // hole under water level -> fill with water 
                        lastHoleIndex = voxelIndex;
                        if (voxelIndex > firstHoleIndex) firstHoleIndex = voxelIndex;
                        if (hasWater && pos.y <= waterLevel) { // hole under water level -> fill with water
                            voxels[voxelIndex].SetFastWater(biomeWaterVoxel);
                        }
                    }
                }

                // Place bedrock
                if (voxelIndex >= 0 && bedrockRow >= 0 && usesBedrockVoxel) {
                    voxels[voxelIndex].SetFastOpaque(bedrockVoxel);
                }

                // Detail/vegetation in the ceiling of tunnels/caves: if there was a solid voxel on top, place vegetation
                if (biome.undergroundCeilingVegDensity > 0 && firstHoleIndex + ONE_Y_ROW < VoxelPlayEnvironment.CHUNK_VOXEL_COUNT && voxels[firstHoleIndex + ONE_Y_ROW].opaque == VoxelPlayEnvironment.FULL_OPAQUE) {
                    Vector3d placePos = pos;
                    placePos.y = position.y + firstHoleIndex / ONE_Y_ROW;
                    float rn = WorldRand.GetValue(placePos);
                    if (rn < biome.undergroundCeilingVegDensity && biome.undergroundCeilingVegetation.Length > 0) {
                        // request one vegetation voxel one position above which means the chunk above this one
                        env.RequestVegetationCreation(placePos, env.GetVegetation(biome.undergroundCeilingVegetation, rn / biome.undergroundCeilingVegDensity));
                    }
                }

                // Vegetation at base of tunnels/caves: if there was a hole on top, place vegetation
                if (lastHoleIndex >= ONE_Y_ROW && biome.undergroundVegDensity > 0 && voxels[lastHoleIndex - ONE_Y_ROW].opaque == VoxelPlayEnvironment.FULL_OPAQUE) {
                    Vector3d placePos = pos;
                    placePos.y = position.y + lastHoleIndex / ONE_Y_ROW;
                    float rn = WorldRand.GetValue(placePos);
                    if (rn < biome.undergroundVegDensity && biome.undergroundVegetation.Length > 0) {
                        // request one vegetation voxel one position above which means the chunk above this one
                        env.RequestVegetationCreation(placePos, env.GetVegetation(biome.undergroundVegetation, rn / biome.undergroundVegDensity));
                    }
                }

                hasContent = true;
            }

            // Spawn random ore
            if (addOre) {
                // Check if there's any ore in this chunk (randomly)
                float noiseValue = WorldRand.GetValue(chunk.position);
                int biomesLength = world.biomes.Length;
                for (int b = 0; b < biomesLength; b++) {
                    BiomeDefinition biome = world.biomes[b];
                    if (biome.biomeGeneration != generation)
                        continue;
                    int oresLength = biome.ores.Length;
                    for (int o = 0; o < oresLength; o++) {
                        if (biome.ores[o].ore == null)
                            continue;
                        if (biome.ores[o].probabilityMin <= noiseValue && biome.ores[o].probabilityMax >= noiseValue) {
                            // ore picked; determine the number of veins in this chunk
                            int veinsCount = biome.ores[o].veinsCountMin + (int)(WorldRand.GetValue() * (biome.ores[o].veinsCountMax - biome.ores[o].veinsCountMin + 1f));
                            for (int vein = 0; vein < veinsCount; vein++) {
                                Vector3d veinPos = chunk.position;
                                veinPos.x += vein;
                                // Determine random vein position in the chunk
                                Vector3 v = WorldRand.GetVector3(veinPos, VoxelPlayEnvironment.CHUNK_SIZE);
                                int px = (int)v.x;
                                int py = (int)v.y;
                                int pz = (int)v.z;
                                veinPos = env.GetVoxelPosition(veinPos, px, py, pz);
                                int oreIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
                                int veinSize = biome.ores[o].veinMinSize + (oreIndex % (biome.ores[o].veinMaxSize - biome.ores[o].veinMinSize + 1));
                                // span ore vein
                                SpawnOre(chunk, biome.ores[o].ore, veinPos, px, py, pz, veinSize, biome.ores[o].depthMin, biome.ores[o].depthMax);
                            }
                            break;
                        }
                    }
                }
            }

            // Finish, return
            chunk.isAboveSurface = isAboveSurface;
            return hasContent;
        }


        void SpawnOre (VoxelChunk chunk, VoxelDefinition oreDefinition, Vector3d veinPos, int px, int py, int pz, int veinSize, int minDepth, int maxDepth) {
            int voxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
            while (veinSize-- > 0 && voxelIndex >= 0 && voxelIndex < chunk.voxels.Length) {
                // Get height at position
                float groundLevel = heightChunkData[pz * VoxelPlayEnvironment.CHUNK_SIZE + px].groundLevel;
                int depth = (int)(groundLevel - veinPos.y);
                if (depth < minDepth || depth > maxDepth)
                    return;

                // Replace solid voxels with ore
                if (chunk.voxels[voxelIndex].opaque >= VoxelPlayEnvironment.FULL_OPAQUE) {
                    chunk.voxels[voxelIndex].SetFastOpaque(oreDefinition);
                }

                // Check if spawn continues
                Vector3d prevPos = veinPos;
                float v = WorldRand.GetValue(veinPos);
                int dir = (int)(v * 5);
                switch (dir) {
                    case 0: // down
                        veinPos.y--;
                        voxelIndex -= ONE_Y_ROW;
                        break;
                    case 1: // right
                        veinPos.x++;
                        voxelIndex++;
                        break;
                    case 2: // back
                        veinPos.z--;
                        voxelIndex -= ONE_Z_ROW;
                        break;
                    case 3: // left
                        veinPos.x--;
                        voxelIndex--;
                        break;
                    case 4: // forward
                        veinPos.z++;
                        voxelIndex += ONE_Z_ROW;
                        break;
                }
                if (veinPos.x == prevPos.x && veinPos.y == prevPos.y && veinPos.z == prevPos.z) {
                    veinPos.y--;
                    voxelIndex -= ONE_Y_ROW;
                }
            }
        }




    }

}
