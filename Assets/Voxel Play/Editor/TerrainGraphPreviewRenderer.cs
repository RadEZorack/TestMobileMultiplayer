using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    static class TerrainGraphPreviewRenderer {

        const int MIN_RESOLUTION = 32;
        const int MAX_RESOLUTION = 512;

        public enum PreviewMode {
            Heightmap = 0,
            Biomes = 1,
            Moisture = 2,
            Hillshade = 3
        }

        static readonly Color deepWaterColor = new Color(0.05f, 0.16f, 0.34f);
        static readonly Color shallowWaterColor = new Color(0.12f, 0.42f, 0.72f);
        static readonly Color beachColor = new Color(0.78f, 0.74f, 0.52f);
        static readonly Color grassColor = new Color(0.23f, 0.52f, 0.24f);
        static readonly Color rockColor = new Color(0.5f, 0.48f, 0.43f);
        static readonly Color snowColor = new Color(0.95f, 0.95f, 0.95f);
        static readonly Color dryMoistureColor = new Color(0.72f, 0.55f, 0.26f);
        static readonly Color mediumMoistureColor = new Color(0.42f, 0.68f, 0.31f);
        static readonly Color wetMoistureColor = new Color(0.12f, 0.48f, 0.86f);
        static StepData[] cachedSourceSteps;
        static StepData[] cachedPreparedSteps;
        static Texture cachedLegacyMoistureTexture;
        static float[] cachedLegacyMoistureValues;
        static int cachedLegacyMoistureTextureSize;

        public struct PreviewSampleInfo {
            public double worldX;
            public double worldZ;
            public float altitudeNormalized;
            public float altitudeMeters;
            public float moisture;
            public BiomeDefinition biome;
        }

        public static Texture2D Render(TerrainDefaultGenerator generator, StepData[] serializedSteps, int altitudeTerminalStepIndex,
            int moistureTerminalStepIndex, WorldDefinition world, Rect areaXZ, int resolution, Texture2D previewTexture,
            PreviewMode mode, out string status) {

            status = "Preview unavailable.";
            resolution = Mathf.Clamp(resolution, MIN_RESOLUTION, MAX_RESOLUTION);

            if (generator == null) {
                return EnsureTexture(previewTexture, resolution);
            }
            if (areaXZ.width <= 0 || areaXZ.height <= 0) {
                status = "Preview area must be greater than 0.";
                return ClearTexture(EnsureTexture(previewTexture, resolution), new Color(0.12f, 0.12f, 0.12f));
            }

            var steps = PrepareSteps(serializedSteps ?? Array.Empty<StepData>());
            var texture = EnsureTexture(previewTexture, resolution);
            var colors = new Color32[resolution * resolution];
            var altitudeSamples = new float[resolution * resolution];
            var moistureSamples = new float[resolution * resolution];

            float minAltitude = float.MaxValue;
            float maxAltitude = float.MinValue;
            float minMoisture = float.MaxValue;
            float maxMoisture = float.MinValue;
            bool hasWater = generator.addWater && generator.maxHeight > 0f;
            float seaLevel = hasWater ? generator.waterLevel / generator.maxHeight : float.NegativeInfinity;
            bool hasMoistureOutput = moistureTerminalStepIndex >= 0 && moistureTerminalStepIndex < steps.Length;
            bool hasLegacyMoisture = !hasMoistureOutput && generator.GetResolvedLegacyMoistureTexture() != null;

            for (int py = 0; py < resolution; py++) {
                float tz = resolution > 1 ? py / (float)(resolution - 1) : 0f;
                double worldZ = areaXZ.yMin + areaXZ.height * tz;

                for (int px = 0; px < resolution; px++) {
                    int sampleIndex = py * resolution + px;
                    float tx = resolution > 1 ? px / (float)(resolution - 1) : 0f;
                    double worldX = areaXZ.xMin + areaXZ.width * tx;

                    EvaluateOutputs(generator, steps, altitudeTerminalStepIndex, moistureTerminalStepIndex, worldX, worldZ, out float altitude, out float moisture);
                    altitudeSamples[sampleIndex] = altitude;
                    moistureSamples[sampleIndex] = moisture;
                    if (altitude < minAltitude) minAltitude = altitude;
                    if (altitude > maxAltitude) maxAltitude = altitude;
                    if (moisture < minMoisture) minMoisture = moisture;
                    if (moisture > maxMoisture) maxMoisture = moisture;
                }
            }

            float sampleStepX = resolution > 1 ? areaXZ.width / (resolution - 1f) : 1f;
            float sampleStepZ = resolution > 1 ? areaXZ.height / (resolution - 1f) : 1f;
            for (int py = 0; py < resolution; py++) {
                for (int px = 0; px < resolution; px++) {
                    int sampleIndex = py * resolution + px;
                    float altitude = altitudeSamples[sampleIndex];
                    float moisture = moistureSamples[sampleIndex];
                    colors[sampleIndex] = (Color32)(mode switch {
                        PreviewMode.Biomes => ColorizeBiome(generator, world, altitude, moisture),
                        PreviewMode.Moisture => ColorizeMoisture(moisture),
                        PreviewMode.Hillshade => ColorizeHillshade(altitudeSamples, resolution, px, py, sampleStepX, sampleStepZ, generator.maxHeight, altitude, seaLevel, hasWater),
                        _ => ColorizeAltitude(altitude, seaLevel, hasWater)
                    });
                }
            }

            texture.SetPixels32(colors);
            texture.Apply(false, false);

            switch (mode) {
                case PreviewMode.Biomes:
                    if (altitudeTerminalStepIndex < 0) {
                        status = hasMoistureOutput || hasLegacyMoisture
                            ? "No altitude output connected. Biome preview uses baseline altitude 0."
                            : "No altitude or moisture output connected. Biome preview uses defaults.";
                    } else if (!hasMoistureOutput) {
                        status = hasLegacyMoisture
                            ? "Biome preview using legacy moisture texture."
                            : "Biome preview using default moisture 0.";
                    } else {
                        status = world != null
                            ? "Biome preview"
                            : "Biome preview (world not resolved)";
                    }
                    break;
                case PreviewMode.Moisture:
                    status = hasMoistureOutput
                        ? $"Moisture {minMoisture:0.###} .. {maxMoisture:0.###}"
                        : hasLegacyMoisture
                            ? $"Legacy moisture {minMoisture:0.###} .. {maxMoisture:0.###}"
                            : "No moisture output connected. Showing default moisture 0.";
                    break;
                case PreviewMode.Hillshade:
                    status = altitudeTerminalStepIndex < 0
                        ? "No altitude output connected. Hillshade uses baseline altitude 0m."
                        : $"Hillshade | Altitude {minAltitude * generator.maxHeight:0.0}m .. {maxAltitude * generator.maxHeight:0.0}m";
                    break;
                default:
                    status = altitudeTerminalStepIndex < 0
                        ? $"No altitude output connected. Baseline altitude 0m | Surface {minAltitude * generator.maxHeight:0.0}m"
                        : $"Altitude {minAltitude * generator.maxHeight:0.0}m .. {maxAltitude * generator.maxHeight:0.0}m";
                    break;
            }
            return texture;
        }

        public static bool TrySample(TerrainDefaultGenerator generator, StepData[] serializedSteps,
            int altitudeTerminalStepIndex, int moistureTerminalStepIndex, double worldX, double worldZ,
            WorldDefinition world, out PreviewSampleInfo sample) {

            sample = default;
            if (generator == null) {
                return false;
            }

            var steps = PrepareSteps(serializedSteps ?? Array.Empty<StepData>());
            EvaluateOutputs(generator, steps, altitudeTerminalStepIndex, moistureTerminalStepIndex, worldX, worldZ, out float altitude, out float moisture);

            altitude = Mathf.Clamp01(altitude);
            moisture = Mathf.Clamp01(moisture);

            sample.worldX = worldX;
            sample.worldZ = worldZ;
            sample.altitudeNormalized = altitude;
            sample.altitudeMeters = altitude * generator.maxHeight;
            sample.moisture = moisture;
            sample.biome = ResolveBiome(generator, world, sample.altitudeMeters, moisture);
            return true;
        }

        public static void Evaluate(TerrainDefaultGenerator generator, StepData[] serializedSteps,
            int altitudeTerminalStepIndex, int moistureTerminalStepIndex,
            double x, double z, out float altitude, out float moisture) {
            var steps = PrepareSteps(serializedSteps ?? Array.Empty<StepData>());
            EvaluateOutputs(generator, steps, altitudeTerminalStepIndex, moistureTerminalStepIndex, x, z, out altitude, out moisture);
        }

        static StepData[] PrepareSteps(StepData[] sourceSteps) {
            if (ReferenceEquals(sourceSteps, cachedSourceSteps) && cachedPreparedSteps != null) {
                return cachedPreparedSteps;
            }

            var steps = new StepData[sourceSteps.Length];
            Array.Copy(sourceSteps, steps, sourceSteps.Length);

            var textureCache = new Dictionary<Texture, float[]>();
            var textureSizes = new Dictionary<Texture, int>();
            var terrainCache = new Dictionary<TerrainData, float[]>();
            var terrainSizes = new Dictionary<TerrainData, int>();

            for (int i = 0; i < steps.Length; i++) {
                if (steps[i].operation != TerrainStepType.SampleHeightMapFractal && steps[i].noiseTexture != null) {
                    Texture tex = steps[i].noiseTexture;
                    if (!textureCache.TryGetValue(tex, out var noiseValues)) {
                        noiseValues = NoiseTools.LoadNoiseTexture(tex, out int textureSize);
                        textureCache[tex] = noiseValues;
                        textureSizes[tex] = textureSize;
                    }
                    steps[i].noiseValues = noiseValues;
                    steps[i].noiseTextureSize = textureSizes[tex];
                } else if (steps[i].terrainData != null) {
                    TerrainData terrain = steps[i].terrainData;
                    if (!terrainCache.TryGetValue(terrain, out var noiseValues)) {
                        noiseValues = NoiseTools.LoadHeightmapFromTerrainData(terrain, out int terrainSize);
                        terrainCache[terrain] = noiseValues;
                        terrainSizes[terrain] = terrainSize;
                    }
                    steps[i].noiseValues = noiseValues;
                    steps[i].noiseTextureSize = terrainSizes[terrain];
                }
            }
            TerrainGraphStepRuntime.SanitizeInputReferences(steps);

            cachedSourceSteps = sourceSteps;
            cachedPreparedSteps = steps;
            return steps;
        }

        static void EvaluateOutputs(TerrainDefaultGenerator generator, StepData[] steps, int altitudeTerminalStepIndex,
            int moistureTerminalStepIndex, double x, double z, out float altitude, out float moisture) {
            float invMaxHeight = generator.maxHeight > 0 ? 1f / generator.maxHeight : 0f;
            TerrainGraphStepRuntime.EvaluateSteps(steps, x, z, invMaxHeight, out bool allowBeach);

            altitude = altitudeTerminalStepIndex >= 0 && altitudeTerminalStepIndex < steps.Length
                ? steps[altitudeTerminalStepIndex].value
                : 0f;
            moisture = moistureTerminalStepIndex >= 0 && moistureTerminalStepIndex < steps.Length
                ? steps[moistureTerminalStepIndex].value
                : SampleLegacyMoisture(generator, x, z);

            if (generator.addWater && generator.maxHeight > 0f) {
                float seaLevelAligned = generator.waterLevel / generator.maxHeight;
                float beachLevelAligned = (generator.waterLevel + 1f) / generator.maxHeight;

                if (altitude < beachLevelAligned && altitude >= seaLevelAligned) {
                    float depth = beachLevelAligned - altitude;
                    if (depth > generator.beachWidth || !allowBeach) {
                        altitude = seaLevelAligned - 0.0001f;
                    }
                }

                if (altitude < seaLevelAligned) {
                    float depth = seaLevelAligned - altitude;
                    altitude = seaLevelAligned - 0.0001f - depth * generator.seaDepthMultiplier;
                }
            }
        }

        static float SampleLegacyMoisture(TerrainDefaultGenerator generator, double x, double z) {
            if (generator == null) {
                return 0f;
            }
            Texture2D moistureTexture = generator.GetResolvedLegacyMoistureTexture();
            if (moistureTexture == null) {
                return 0f;
            }
            if (!ReferenceEquals(cachedLegacyMoistureTexture, moistureTexture) || cachedLegacyMoistureValues == null || cachedLegacyMoistureTextureSize <= 0) {
                cachedLegacyMoistureTexture = moistureTexture;
                cachedLegacyMoistureValues = NoiseTools.LoadNoiseTexture(moistureTexture, out cachedLegacyMoistureTextureSize);
            }
            if (cachedLegacyMoistureValues == null || cachedLegacyMoistureTextureSize <= 0) {
                return 0f;
            }
            float moistureScale = generator.GetResolvedLegacyMoistureScale();
            return NoiseTools.GetNoiseValueBilinear(cachedLegacyMoistureValues, cachedLegacyMoistureTextureSize, x * moistureScale, z * moistureScale);
        }

        static BiomeDefinition ResolveBiome(TerrainDefaultGenerator generator, WorldDefinition world, float altitudeMeters, float moisture) {
            var env = VoxelPlayEnvironment.instance;
            if (env != null && env.world != null && env.world == world && env.world.terrainGenerator == generator) {
                return env.GetBiome(altitudeMeters, moisture);
            }

            if (world == null || world.biomes == null || world.biomes.Length == 0) {
                return world != null ? world.defaultBiome : null;
            }

            int biomesCount = world.biomes.Length;
            for (int i = 0; i < biomesCount; i++) {
                BiomeDefinition biome = world.biomes[i];
                if (biome == null || biome.zones == null) continue;
                int zonesCount = biome.zones.Length;
                for (int j = 0; j < zonesCount; j++) {
                    BiomeZone zone = biome.zones[j];
                    if (altitudeMeters >= zone.altitudeMin && altitudeMeters <= zone.altitudeMax
                        && moisture >= zone.moistureMin && moisture <= zone.moistureMax) {
                        return biome;
                    }
                }
            }

            return world.defaultBiome;
        }

        static Color ColorizeBiome(TerrainDefaultGenerator generator, WorldDefinition world, float altitudeNormalized, float moisture) {
            float altitudeMeters = altitudeNormalized * generator.maxHeight;
            BiomeDefinition biome = ResolveBiome(generator, world, altitudeMeters, moisture);
            if (biome == null) {
                return new Color(0.2f, 0.2f, 0.2f, 1f);
            }

            Color color = biome.biomeMapColor;
            if (color.a <= 0f) {
                color.a = 1f;
            }
            if (color.maxColorComponent <= 0f) {
                return new Color(0.35f, 0.35f, 0.35f, 1f);
            }
            return color;
        }

        static Color ColorizeAltitude(float altitude, float seaLevel, bool hasWater) {
            if (hasWater && altitude <= seaLevel) {
                float t = Mathf.InverseLerp(seaLevel - 0.25f, seaLevel, altitude);
                return Color.Lerp(deepWaterColor, shallowWaterColor, t);
            }

            float landStart = hasWater ? seaLevel : 0f;
            float landT = Mathf.InverseLerp(landStart, 1f, altitude);
            if (landT < 0.08f) {
                return Color.Lerp(beachColor, grassColor, landT / 0.08f);
            }
            if (landT < 0.65f) {
                return Color.Lerp(grassColor, rockColor, Mathf.InverseLerp(0.08f, 0.65f, landT));
            }
            return Color.Lerp(rockColor, snowColor, Mathf.InverseLerp(0.65f, 1f, landT));
        }

        static Color ColorizeMoisture(float moisture) {
            moisture = Mathf.Clamp01(moisture);
            if (moisture < 0.5f) {
                return Color.Lerp(dryMoistureColor, mediumMoistureColor, moisture / 0.5f);
            }
            return Color.Lerp(mediumMoistureColor, wetMoistureColor, (moisture - 0.5f) / 0.5f);
        }

        static Color ColorizeHillshade(float[] altitudeSamples, int resolution, int px, int py, float sampleStepX, float sampleStepZ,
            float maxHeight, float altitude, float seaLevel, bool hasWater) {
            const float ambient = 0.34f;
            const float lightStrength = 0.66f;
            const float exaggeration = 2.2f;

            float west = GetAltitudeMeters(altitudeSamples, resolution, px - 1, py, maxHeight);
            float east = GetAltitudeMeters(altitudeSamples, resolution, px + 1, py, maxHeight);
            float north = GetAltitudeMeters(altitudeSamples, resolution, px, py + 1, maxHeight);
            float south = GetAltitudeMeters(altitudeSamples, resolution, px, py - 1, maxHeight);

            float slopeX = sampleStepX > 0f ? (east - west) / (2f * sampleStepX) : 0f;
            float slopeZ = sampleStepZ > 0f ? (north - south) / (2f * sampleStepZ) : 0f;
            Vector3 normal = new Vector3(-slopeX * exaggeration, 1f, -slopeZ * exaggeration).normalized;
            Vector3 lightDir = new Vector3(-0.45f, 0.78f, 0.43f).normalized;
            float shade = ambient + lightStrength * Mathf.Clamp01(Vector3.Dot(normal, lightDir));

            Color baseColor = ColorizeAltitude(altitude, seaLevel, hasWater);
            Color shadowColor = Color.Lerp(baseColor, Color.black, 0.55f);
            Color lightColor = Color.Lerp(baseColor, Color.white, 0.24f);
            Color shadedColor = Color.Lerp(shadowColor, lightColor, shade);
            shadedColor.a = 1f;
            return shadedColor;
        }

        static float GetAltitudeMeters(float[] altitudeSamples, int resolution, int px, int py, float maxHeight) {
            if (resolution <= 0 || altitudeSamples == null || altitudeSamples.Length == 0) return 0f;

            px = Mathf.Clamp(px, 0, resolution - 1);
            py = Mathf.Clamp(py, 0, resolution - 1);
            return altitudeSamples[py * resolution + px] * maxHeight;
        }

        static Texture2D EnsureTexture(Texture2D texture, int resolution) {
            if (texture != null && texture.width == resolution && texture.height == resolution) {
                return texture;
            }
            if (texture != null) {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) {
                name = "TerrainGraphPreview",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }

        static Texture2D ClearTexture(Texture2D texture, Color color) {
            var colors = texture.GetPixels32();
            Color32 color32 = color;
            for (int k = 0; k < colors.Length; k++) {
                colors[k] = color32;
            }
            texture.SetPixels32(colors);
            texture.Apply(false, false);
            return texture;
        }
    }
}
