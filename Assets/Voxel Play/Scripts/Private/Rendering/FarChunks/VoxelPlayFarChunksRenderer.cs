using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace VoxelPlay {

    [DefaultExecutionOrder(1000)]
    public class VoxelPlayFarChunksRenderer : MonoBehaviour {

        static class ShaderParams {
            public static int WaterLevel = Shader.PropertyToID("_WaterLevel");
            public static int WaterColor = Shader.PropertyToID("_WaterColor");
            public static int ShoreColor = Shader.PropertyToID("_ShoreColor");
            public static int SnapshotData = Shader.PropertyToID("_SnapshotData");
            public static int TerrainFarChunksTex = Shader.PropertyToID("_TerrainFarChunksTex");
            public static int CloudsTex = Shader.PropertyToID("_CloudsTex");
            public static int WaterTex = Shader.PropertyToID("_WaterTex");
            public static int TerrainMaxAltitude = Shader.PropertyToID("_TerrainMaxAltitude");
            public static int ShadowIntensity = Shader.PropertyToID("_ShadowIntensity");
            public static int WaterReflectionsIntensity = Shader.PropertyToID("_WaterReflectionsIntensity");
            public static int SpecularPower = Shader.PropertyToID("_SpecularPower");
            public static int SpecularIntensity = Shader.PropertyToID("_SpecularIntensity");

            public const string SKW_SHADOWS = "_SHADOWS";
            public const string SKW_WATER_REFLECTIONS = "_WATER_REFLECTIONS";
        }

        const int TEXTURE_SIZE = 4096;
        const int MIN_DISTANCE_UPDATE_SQR = 256 * 256;

        public static bool requireUpdateMaterialProperties;

        VoxelPlayEnvironment env;
        Texture2D terrainTex;
        Color32[] terrainData32;
        Color[] terrainDataHalf;
        Material mat;
        Vector3d lastSnapshotPosition;
        bool capturing;
        int worldExtents;
        bool requestTextureUpdate;
        Camera cam;
        Vector3 boundsMin; // in world space
        VoxelPlayTerrainGenerator tg;
        bool abort;
        Renderer quadRenderer;
        int chunkRange = 128;
        Texture2D noiseTex;
        float[] noiseValues;
        int noiseTexSize;
        bool usesHighRange;

        public static void Init (VoxelPlayEnvironment env) {
            if (!Application.isPlaying) return;
            if (!env.enableFarChunksRendering) return;
            GameObject go = Instantiate(Resources.Load("VoxelPlay/Prefabs/VoxelPlayFarChunksManager") as GameObject);
            go.name = "Voxel Play Far Chunks Renderer";
            go.transform.SetParent(env.transform, false);
        }

        public static void Dispose () {
            VoxelPlayFarChunksRenderer vf = Misc.FindObjectOfType<VoxelPlayFarChunksRenderer>(true);
            if (vf != null) {
                DestroyImmediate(vf.gameObject);
            }
        }

        void Start () {
            env = VoxelPlayEnvironment.instance;
            if (env.initialized) {
                Init();
            } else {
                env.OnInitialized += Init;
            }
        }

        private void OnValidate () {
            UpdateMaterialProperties();
        }

        private void Init () {
            if (noiseTex == null) {
                noiseTex = Resources.Load<Texture2D>("VoxelPlay/Textures/Noise");
            }
            if (noiseTex != null) {
                noiseValues = NoiseTools.LoadNoiseTexture(noiseTex, out noiseTexSize);
            } else {
                noiseValues = new float[1];
                noiseValues[0] = 1f;
                noiseTexSize = 1;
            }
            if (terrainTex != null) {
                DestroyImmediate(terrainTex);
            }
            usesHighRange = env.world != null && env.world.terrainGenerator != null && env.world.terrainGenerator.maxHeight > 255;
            terrainTex = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, usesHighRange ? TextureFormat.RGBAHalf : TextureFormat.RGBA32, mipChain: false);
            if (usesHighRange) {
                terrainDataHalf = new Color[TEXTURE_SIZE * TEXTURE_SIZE];
            } else {
                terrainData32 = new Color32[TEXTURE_SIZE * TEXTURE_SIZE];
            }
            terrainTex.filterMode = FilterMode.Bilinear;
            requestTextureUpdate = true;
            quadRenderer = GetComponent<Renderer>();
            lastSnapshotPosition.x = float.MinValue;

            mat = quadRenderer.material;
            quadRenderer.enabled = true;
            cam = env.cameraMain;
            chunkRange = TEXTURE_SIZE / (VoxelPlayEnvironment.CHUNK_SIZE * 2);
            worldExtents = VoxelPlayEnvironment.CHUNK_SIZE * chunkRange;

            env.UpdateMaterialProperties();

            // The terrain generators are not thread-safe so we instantiate a copy with same settings to be used in the background thread of this class
            tg = Instantiate(env.world.terrainGenerator);
            tg.Initialize();

            List<VoxelDefinition> vds = new List<VoxelDefinition>();
            tg.GetTerrainVoxelDefinitions(vds);
            foreach (VoxelDefinition vd in vds) {
                vd.ComputeFarChunksSampleColor();
            }
        }

        void OnDestroy () {
            abort = true;
            if (capturing) {
                System.Threading.Thread.Sleep(1000);
            }
            if (terrainTex != null) {
                DestroyImmediate(terrainTex);
            }
            if (mat != null) {
                DestroyImmediate(mat);
                mat = null;
            }
        }

        void LateUpdate () {

            if (!env.initialized) return;

            // Align the quad impostor for horizon to the camera
            AlignFarChunksQuad();

            // Update quad texture if we have data and submit to GPU
            if (requestTextureUpdate) {
                requestTextureUpdate = false;
                if (usesHighRange) {
                    terrainTex.SetPixels(terrainDataHalf);
                } else {
                    terrainTex.SetPixels32(terrainData32);
                }
                terrainTex.Apply();
                requireUpdateMaterialProperties = true;
            }

            if (requireUpdateMaterialProperties) {
                requireUpdateMaterialProperties = false;
                UpdateMaterialProperties();
            }

            // Needs new snapshot?
            if (capturing) return;
            Vector3d snapshotPosition = cam.transform.position;
            FastVector.Floor(ref snapshotPosition);
            if (FastVector.SqrDistanceXZ(ref snapshotPosition, ref lastSnapshotPosition) < MIN_DISTANCE_UPDATE_SQR) return;

            // Start a new capture
            capturing = true;
            lastSnapshotPosition = snapshotPosition;
            boundsMin = snapshotPosition - new Vector3d(worldExtents, 0, worldExtents);
            Capture();
        }

        void UpdateMaterialProperties () {
            if (mat == null) return;
            mat.SetTexture(ShaderParams.CloudsTex, noiseTex);
            mat.SetTexture(ShaderParams.WaterTex, env.currentWaterVoxelDefinition.textureTop != null ? env.currentWaterVoxelDefinition.textureTop : env.currentWaterVoxelDefinition.textureSide);
            mat.SetTexture(ShaderParams.TerrainFarChunksTex, terrainTex);
            mat.SetVector(ShaderParams.SnapshotData, new Vector4(boundsMin.x, boundsMin.z, TEXTURE_SIZE, env.visibleChunksDistance * VoxelPlayEnvironment.CHUNK_SIZE));
            mat.SetFloat(ShaderParams.WaterLevel, env.waterLevel);
            Color waterColor = env.farChunksWaterColor;
            if (!env.farChunksWaterColorOverride && env.currentWaterVoxelDefinition != null) {
                if (env.currentWaterVoxelDefinition.farChunksSampleColor.a == 0) {
                    env.currentWaterVoxelDefinition.ComputeFarChunksSampleColor();
                }
                waterColor = env.currentWaterVoxelDefinition.farChunksSampleColor;
            }
            mat.SetColor(ShaderParams.WaterColor, waterColor);
            mat.SetColor(ShaderParams.ShoreColor, env.farChunksShoreColor);
            mat.SetFloat(ShaderParams.TerrainMaxAltitude, tg.maxHeight + 1);
            if (env.farChunksShadows) {
                mat.EnableKeyword(ShaderParams.SKW_SHADOWS);
                mat.SetFloat(ShaderParams.ShadowIntensity, 1f - env.farChunksShadowIntensity);
            } else {
                mat.DisableKeyword(ShaderParams.SKW_SHADOWS);
            }
            if (env.farChunksWaterReflections) {
                mat.EnableKeyword(ShaderParams.SKW_WATER_REFLECTIONS);
                mat.SetFloat(ShaderParams.WaterReflectionsIntensity, env.farChunksWaterReflectionsIntensity);
            } else {
                mat.DisableKeyword(ShaderParams.SKW_WATER_REFLECTIONS);
            }
            mat.SetFloat(ShaderParams.SpecularPower, env.world.specularPower);
            mat.SetFloat(ShaderParams.SpecularIntensity, env.world.specularIntensity);
        }

        void AlignFarChunksQuad () {
            float dist = cam.farClipPlane * 0.9999f;
            transform.position = cam.transform.position + cam.transform.forward * dist;
            transform.forward = cam.transform.forward;
            float h = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * dist * 2f;
            transform.localScale = new Vector3(h * cam.aspect, h, 1f);
        }

        async void Capture () {

            await Task.Run(() => {
                int chunkAxis = chunkRange * 2;
                int numChunks = (int)Mathf.Pow(chunkAxis, 2);

                for (int i = 0; i < numChunks && !abort; i++) {
                    Vector3d curPos;
                    curPos.x = boundsMin.x + (i % chunkAxis) * VoxelPlayEnvironment.CHUNK_SIZE;
                    curPos.z = boundsMin.z + (i / chunkAxis) * VoxelPlayEnvironment.CHUNK_SIZE;
                    curPos.y = 0;
                    if (usesHighRange) {
                        SnapshotHighRange(curPos);
                    } else {
                        Snapshot32(curPos);
                    }
                }
            });

            requestTextureUpdate = true;
            capturing = false;
        }


        void Snapshot32 (Vector3d curPos) {

            Color32 color;
            Color32 waterColor = env.farChunksWaterColor;
            int offsetX = (int)(curPos.x - boundsMin.x);
            int offsetY = (int)(curPos.z - boundsMin.z);
            float waterLevelNorm = env.waterLevel / tg.maxHeight;
            byte waterLevelAltitude = env.farChunksDeepWater ? (byte)0 : (byte)(env.waterLevel - 1);
            for (int z = 0; z < VoxelPlayEnvironment.CHUNK_SIZE; z++) {
                int tindex = (z + offsetY) * TEXTURE_SIZE + offsetX;
                for (int x = 0; x < VoxelPlayEnvironment.CHUNK_SIZE; x++, tindex++) {
                    double wx = x + curPos.x;
                    double wz = z + curPos.z;
                    tg.GetHeightAndMoisture(wx, wz, out float altitude, out float moisture);
                    if (waterLevelAltitude > 0 && altitude < waterLevelNorm) {
                        waterColor.a = waterLevelAltitude;
                        terrainData32[tindex] = waterColor;
                        continue;
                    }
                    altitude *= tg.maxHeight;
                    BiomeDefinition biome;
                    if (env.enableBiomeMixing) {
                        env.GetBiomeWeights(altitude, moisture, out BiomeDefinition primary, out BiomeDefinition secondary, out float bw);
                        biome = primary;
                        if ((object)biome == null) continue;
                        color = biome.voxelTop.farChunksSampleColor;
                        if (bw > 0 && (object)secondary != null && (object)secondary.voxelTop != null) {
                            Color32 c2 = secondary.voxelTop.farChunksSampleColor;
                            color.r = (byte)(color.r + (c2.r - color.r) * bw);
                            color.g = (byte)(color.g + (c2.g - color.g) * bw);
                            color.b = (byte)(color.b + (c2.b - color.b) * bw);
                        }
                    } else {
                        biome = env.GetBiome(altitude, moisture);
                        if ((object)biome == null) continue;
                        color = biome.voxelTop.farChunksSampleColor;
                    }
                    color.a = (byte)altitude;
                    float rn = 0.85f + 0.30f * NoiseTools.GetNoiseValue(noiseValues, noiseTexSize, wx, wz);
                    if (rn > 1f) rn = 1f;
                    color.r = (byte)(color.r * rn);
                    color.g = (byte)(color.g * rn);
                    color.b = (byte)(color.b * rn);
                    terrainData32[tindex] = color;
                }
            }
        }

        void SnapshotHighRange (Vector3d curPos) {

            Color color;
            Color waterColor = env.farChunksWaterColor;
            int offsetX = (int)(curPos.x - boundsMin.x);
            int offsetY = (int)(curPos.z - boundsMin.z);
            float waterLevelNorm = env.waterLevel / tg.maxHeight;
            float waterLevelAltitude = env.farChunksDeepWater ? 0 : (env.waterLevel - 1) / (tg.maxHeight + 1);
            for (int z = 0; z < VoxelPlayEnvironment.CHUNK_SIZE; z++) {
                int tindex = (z + offsetY) * TEXTURE_SIZE + offsetX;
                for (int x = 0; x < VoxelPlayEnvironment.CHUNK_SIZE; x++, tindex++) {
                    double wx = x + curPos.x;
                    double wz = z + curPos.z;
                    tg.GetHeightAndMoisture(wx, wz, out float altitude, out float moisture);
                    if (waterLevelAltitude > 0 && altitude < waterLevelNorm) {
                        waterColor.a = waterLevelAltitude;
                        terrainDataHalf[tindex] = waterColor;
                        continue;
                    }
                    altitude *= tg.maxHeight;
                    BiomeDefinition biome;
                    if (env.enableBiomeMixing) {
                        env.GetBiomeWeights(altitude, moisture, out BiomeDefinition primary, out BiomeDefinition secondary, out float bw);
                        biome = primary;
                        if ((object)biome == null) continue;
                        color = biome.voxelTop.farChunksSampleColorHighRange;
                        if (bw > 0 && (object)secondary != null && (object)secondary.voxelTop != null) {
                            Color c2 = secondary.voxelTop.farChunksSampleColorHighRange;
                            color.r += (c2.r - color.r) * bw;
                            color.g += (c2.g - color.g) * bw;
                            color.b += (c2.b - color.b) * bw;
                        }
                    } else {
                        biome = env.GetBiome(altitude, moisture);
                        if ((object)biome == null) continue;
                        color = biome.voxelTop.farChunksSampleColorHighRange;
                    }
                    color.a = altitude;
                    float rn = 0.85f + 0.30f * NoiseTools.GetNoiseValue(noiseValues, noiseTexSize, wx, wz);
                    if (rn > 1f) rn = 1f;
                    color.r *= rn;
                    color.g *= rn;
                    color.b *= rn;
                    terrainDataHalf[tindex] = color;
                }
            }
        }
    }




    public partial class VoxelDefinition {
        [NonSerialized]
        public Color32 farChunksSampleColor;
        [NonSerialized]
        public Color farChunksSampleColorHighRange;


        public void ComputeFarChunksSampleColor () {
            if (textureIndexTop > 0 && textureArrayPacker != null) {
                Color32[] colors = textureArrayPacker.textures[textureIndexTop].colors;
                farChunksSampleColor = GetSampleTopColor(colors);
            } else if (textureIndexSide > 0 && textureArrayPacker != null) {
                Color32[] colors = textureArrayPacker.textures[textureIndexSide].colors;
                farChunksSampleColor = GetSampleTopColor(colors);
            } else if (textureSide != null) {
                Color32[] colors = textureSide.GetPixels32();
                farChunksSampleColor = GetSampleTopColor(colors);
            } else {
                farChunksSampleColor = sampleColor;
            }
            farChunksSampleColor.r = (byte)(farChunksSampleColor.r * tintColor.r / 255);
            farChunksSampleColor.g = (byte)(farChunksSampleColor.g * tintColor.g / 255);
            farChunksSampleColor.b = (byte)(farChunksSampleColor.b * tintColor.b / 255);
            farChunksSampleColorHighRange = farChunksSampleColor;
            farChunksSampleColorHighRange = farChunksSampleColorHighRange.linear;
        }

        Color32 GetSampleTopColor (Color32[] colors) {
            const int SAMPLE_COUNT = 16;
            int colorsLength = colors.Length;
            Color32 color = new Color32(0, 0, 0, 0);
            int colorIndex = 0;
            int colorStep = colorsLength / SAMPLE_COUNT;
            int r = 0, g = 0, b = 0, a = 0;
            for (int k = 0; k < SAMPLE_COUNT; k++, colorIndex += colorStep) {
                Color32 sample = colors[colorIndex];
                r += sample.r;
                g += sample.g;
                b += sample.b;
                a += sample.a;
            }
            color.r = (byte)(r / SAMPLE_COUNT);
            color.g = (byte)(g / SAMPLE_COUNT);
            color.b = (byte)(b / SAMPLE_COUNT);
            color.a = (byte)(a / SAMPLE_COUNT);
            return color;
        }

    }

}