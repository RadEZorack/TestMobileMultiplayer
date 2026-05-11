//#define USES_SEE_THROUGH
#define USES_BRIGHT_POINT_LIGHTS
#define USES_URP
//#define USES_URP_NATIVE_LIGHTS
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VoxelPlay.GPURendering;
using VoxelPlay.GPURendering.Instancing;
using VoxelPlay.GPURendering.InstancingIndirect;
using VoxelPlay.GPULighting;
using UnityEngine.Rendering;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        public static bool supportsSeeThrough {
            get {
#if USES_SEE_THROUGH
                return true;
#else
                return false;
#endif
            }
        }

        public static bool supportsBrightPointLights {
            get {
#if USES_BRIGHT_POINT_LIGHTS
                return true;
#else
                return false;
#endif
            }
        }

        public static bool supportsURP {
            get {
#if USES_URP
                return true;
#else
                return false;
#endif
            }
        }

        public static bool supportsURPNativeLights {
            get {
#if USES_URP_NATIVE_LIGHTS && USES_BRIGHT_POINT_LIGHTS
                return true;
#else
                return false;
#endif
            }
        }

        public bool isServerMode {
            get {
                return serverMode && Application.isPlaying;
            }
        }


        public struct RenderingMaterialDescriptor {
            public Material templateMaterial;
            public TextureArrayPacker textureProvider;
        }

        public struct RenderingMaterial {
            public RenderingMaterialDescriptor descriptor;
            public Material material;
            public VoxelPlayGreedyMesherLitAO greedyMesherLitAO;
            public VoxelPlayGreedyMesherLit greedyMesherLit;
        }


        public const int MESH_JOBS_TOTAL_POOL_SIZE_PC = 2000;
        public const int MESH_JOBS_TOTAL_POOL_SIZE_MOBILE = 128;
        public const int MAX_MATERIALS_PER_CHUNK = 16;

        /* cube coords
		
		7+------+6
		/.   3 /|
		2+------+ |
		|4.....|.+5
		|/     |/
		0+------+1
		
		*/
        public const int INDICES_BUFFER_OPAQUE = 0;
        public const int INDICES_BUFFER_CUTXSS = 1;
        public const int INDICES_BUFFER_CUTOUT = 2;
        public const int INDICES_BUFFER_WATER = 3;
        public const int INDICES_BUFFER_TRANSP = 4;
        public const int INDICES_BUFFER_CLOUD = 5;
        public const int INDICES_BUFFER_OPANIM = 6;
        public const int INDICES_BUFFER_OPNOAO = 7;


        // Unconclusive neighbours
        const byte CHUNK_TOP = 1;
        const byte CHUNK_BOTTOM = 2;
        const byte CHUNK_LEFT = 4;
        const byte CHUNK_RIGHT = 8;
        const byte CHUNK_BACK = 16;
        const byte CHUNK_FORWARD = 32;

        // Chunk Rendering
        GameObject voxelPlaceholderPrefab;

        readonly struct BakedMesh : IEquatable<BakedMesh> {
            public readonly Mesh mesh;
            public readonly Color32 tintColor;

            public BakedMesh (Mesh mesh, Color32 tintColor) {
                this.mesh = mesh;
                this.tintColor = tintColor;
            }

            public bool Equals (BakedMesh other) {
                return ReferenceEquals(mesh, other.mesh) &&
                    tintColor.r == other.tintColor.r &&
                    tintColor.g == other.tintColor.g &&
                    tintColor.b == other.tintColor.b &&
                    tintColor.a == other.tintColor.a;
            }

            public override bool Equals (object obj) {
                return obj is BakedMesh other && Equals(other);
            }

            public override int GetHashCode () {
                unchecked {
                    int hash = 17;
                    hash = hash * 23 + mesh.GetHashCode();
                    hash = hash * 23 + tintColor.GetHashCode();
                    return hash;
                }
            }
        }

        readonly struct BakedMeshWithGather : IEquatable<BakedMeshWithGather> {
            public readonly Mesh mesh;
            public readonly int lightHash;

            public BakedMeshWithGather (Mesh mesh, int lightHash) {
                this.mesh = mesh;
                this.lightHash = lightHash;
            }

            public bool Equals (BakedMeshWithGather other) {
                return ReferenceEquals(mesh, other.mesh) && lightHash == other.lightHash;
            }

            public override bool Equals (object obj) {
                return obj is BakedMeshWithGather other && Equals(other);
            }

            public override int GetHashCode () {
                unchecked {
                    int hash = 17;
                    hash = hash * 23 + mesh.GetHashCode();
                    hash = hash * 23 + lightHash;
                    return hash;
                }
            }
        }

        bool effectiveMultithreadGeneration;

        [NonSerialized]
        public VirtualVoxel[] virtualChunk;

        [NonSerialized]
        public Voxel[] emptyChunkUnderground, emptyChunkAboveTerrain;

        [NonSerialized]
        public RenderingMaterial[] renderingMaterials;
        FastHashSet<Material[]> materialsDict;
        List<Color32> modelMeshColors;
        List<Vector3> modelMeshVertices;
        Material matDynamicCutout, matDynamicOpaque, matDynamicAlpha;
        Material matDynamicCutoutNonArray, matDynamicOpaqueNonArray, matDynamicBumpMapNonArray;
        readonly Dictionary<BakedMesh, Mesh> bakedMeshes = new Dictionary<BakedMesh, Mesh>();
        readonly Dictionary<BakedMeshWithGather, Mesh> bakedMeshesWithGather = new Dictionary<BakedMeshWithGather, Mesh>();
        Color32[] surroundingColors;

        // Each material has an index power of 2 which is combined with other materials to create a multi-material chunk mesh
        Dictionary<RenderingMaterialDescriptor, int> materialIndices;
        int lastRenderingMaterialIndex;

        // Multi-thread support
        MeshingThread[] meshingThreads;
        bool generationThreadsRunning;
        readonly object seeThroughLock = new object();

        // Instancing
        IGPUInstancingRenderer instancedRenderer;

        // Predefined bounds
        static Bounds boundsCloud = new Bounds(Misc.vector3zero, new Vector3(10000, 10000, 10000));
        static Bounds boundsWithCurvature = new Bounds(Misc.vector3zero, new Vector3(CHUNK_SIZE, CHUNK_SIZE + 32, CHUNK_SIZE));
        static Bounds boundsChunk = new Bounds(Misc.vector3zero, new Vector3(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE));

        #region Renderer initialization

        void InitRenderer () {

            // Init rendering-related stuff
            voxelPlaceholderPrefab = Resources.Load<GameObject>("VoxelPlay/Defaults/VoxelPlaceholder");

            // Triangle opaque and cutout are always loaded because dynamic voxels requires them
            matDynamicOpaque = Instantiate(Resources.Load<Material>("VoxelPlay/Materials/VP Voxel Dynamic Opaque"));
            matDynamicCutout = Instantiate(Resources.Load<Material>("VoxelPlay/Materials/VP Voxel Dynamic Cutout"));
            matDynamicAlpha = Instantiate(Resources.Load<Material>("VoxelPlay/Materials/VP Voxel Dynamic Alpha"));
            matDynamicOpaqueNonArray = Resources.Load<Material>("VoxelPlay/Materials/VP Model Texture");
            matDynamicBumpMapNonArray = Resources.Load<Material>("VoxelPlay/Materials/VP Model Texture BumpMap");
            matDynamicCutoutNonArray = Resources.Load<Material>("VoxelPlay/Materials/VP Model Texture Cutout");

            InitRenderingMaterials();

            if (surroundingColors == null || surroundingColors.Length < 27) {
                surroundingColors = new Color32[27];
            }
            if (modelMeshColors == null) {
                modelMeshColors = new List<Color32>(24);
            }
            if (modelMeshVertices == null) {
                modelMeshVertices = new List<Vector3>(24);
            }

            Voxel.Empty.light = noLightValue;

            InitTempVertices();
            InitSeeThrough();
            InitMeshingThreads();

            if (useComputeBuffers) {
                instancedRenderer = new GPUInstancingIndirectRenderer(this);
            } else {
                instancedRenderer = new GPUInstancingRenderer(this);
            }

Camera cam = currentCamera;
if (cam != null) {
            VoxelPlayLightManager lightManager = cam.GetComponent<VoxelPlayLightManager>();
            if (lightManager == null) {
                cam.gameObject.AddComponent<VoxelPlayLightManager>();
            } else {
                lightManager.enabled = true;
            }

            if (realisticWater) {
                cam.depthTextureMode |= DepthTextureMode.Depth;
                cam.forceIntoRenderTexture = true;
            }
}

            StartGenerationThreads();

            if (isServerMode) {
                Debug.Log("Voxel Play server mode enabled -- voxels won't be rendered.");
            }
        }

        int RegisterRenderingMaterial (Material templateMat, RenderType rt, TextureArrayPacker provider, bool forceNewRegistration = false) {

            if (templateMat == null) return 0;

            RenderingMaterialDescriptor desc = new RenderingMaterialDescriptor { templateMaterial = templateMat, textureProvider = provider };
            if (!forceNewRegistration && materialIndices.TryGetValue(desc, out int materialIndex)) return materialIndex; // already registered

            if (lastRenderingMaterialIndex < renderingMaterials.Length - 1) {
                lastRenderingMaterialIndex++;
                Material mat = Instantiate(templateMat);

                VoxelPlayGreedyMesherLitAO greedyMesherLitAO = null;
                VoxelPlayGreedyMesherLit greedyMesherLit = null;
                switch (rt) {
                    case RenderType.Opaque:
                    case RenderType.Opaque6tex:
                    case RenderType.OpaqueAnimated:
                        greedyMesherLitAO = new VoxelPlayGreedyMesherLitAO();
                        greedyMesherLit = new VoxelPlayGreedyMesherLit();
                        break;
                    case RenderType.OpaqueNoAO:
                        greedyMesherLit = new VoxelPlayGreedyMesherLit();
                        break;
                    case RenderType.Cutout:
                        greedyMesherLit = new VoxelPlayGreedyMesherLit();
                        break;
                    case RenderType.Cloud:
                        greedyMesherLit = new VoxelPlayGreedyMesherLit();
                        break;
                }

                renderingMaterials[lastRenderingMaterialIndex] = new RenderingMaterial { descriptor = desc, material = mat, greedyMesherLitAO = greedyMesherLitAO, greedyMesherLit = greedyMesherLit };
                materialIndices[desc] = lastRenderingMaterialIndex;
            } else {
                Debug.LogError("Too many override materials. Max materials supported = " + MAX_MATERIALS_PER_CHUNK);
            }

            return lastRenderingMaterialIndex;
        }

        int RegisterRenderingMaterialNoTextureArray (Material templateMat) {

            if (templateMat == null) return 0;

            RenderingMaterialDescriptor desc = new RenderingMaterialDescriptor { templateMaterial = templateMat };
            if (materialIndices.TryGetValue(desc, out int materialIndex)) return materialIndex; // already registered

            if (lastRenderingMaterialIndex < renderingMaterials.Length - 1) {
                lastRenderingMaterialIndex++;
                Material mat = Instantiate(templateMat);
                renderingMaterials[lastRenderingMaterialIndex] = new RenderingMaterial { descriptor = desc, material = mat };
                materialIndices[desc] = lastRenderingMaterialIndex;
            } else {
                Debug.LogError("Too many override materials. Max materials supported = " + MAX_MATERIALS_PER_CHUNK);
            }

            return lastRenderingMaterialIndex;
        }


        void DisposeRenderer () {
            Misc.DestroyImmediateAndNullify(ref matDynamicOpaque);
            Misc.DestroyImmediateAndNullify(ref matDynamicCutout);
            Misc.DestroyImmediateAndNullify(ref matDynamicAlpha);
            if (renderingMaterials != null) {
                for (int k = 0; k < renderingMaterials.Length; k++) {
                    Material m = renderingMaterials[k].material;
                    Misc.DestroyImmediateAndNullify(ref m);
                    renderingMaterials[k].material = null;
                    if (renderingMaterials[k].greedyMesherLitAO != null) {
                        renderingMaterials[k].greedyMesherLitAO.Dispose();
                    }
                    renderingMaterials[k].greedyMesherLitAO = null;
                    if (renderingMaterials[k].greedyMesherLit != null) {
                        renderingMaterials[k].greedyMesherLit.Dispose();
                    }
                    renderingMaterials[k].greedyMesherLit = null;
                }
            }
            if (instancedRenderer != null) {
                instancedRenderer.Dispose();
            }
            if (meshingThreads != null) {
                for (int k = 0; k < meshingThreads.Length; k++) {
                    if (meshingThreads[k] != null) {
                        meshingThreads[k].Clear();
                    }
                }
                meshingThreads = null;
            }
            if (bakedMeshes != null) {
                foreach (Mesh mesh in bakedMeshes.Values) {
                    if (mesh != null) DestroyImmediate(mesh);
                }
                bakedMeshes.Clear();
            }
            if (bakedMeshesWithGather != null) {
                foreach (Mesh mesh in bakedMeshesWithGather.Values) {
                    if (mesh != null) DestroyImmediate(mesh);
                }
                bakedMeshesWithGather.Clear();
            }

            emptyChunkUnderground = null;
            emptyChunkAboveTerrain = null;
            virtualChunk = null;
        }


        void InitMeshingThreads () {
            InitVirtualChunk();
            int maxThreads = effectiveMultithreadGeneration ? SystemInfo.processorCount - 1 : 1;
            if (maxThreads < 1) maxThreads = 1;
            meshingThreads = new MeshingThread[maxThreads];
            int poolSize = (isMobilePlatform ? MESH_JOBS_TOTAL_POOL_SIZE_MOBILE : MESH_JOBS_TOTAL_POOL_SIZE_PC) / maxThreads;
            for (int k = 0; k < meshingThreads.Length; k++) {
                meshingThreads[k] = new MeshingThreadTriangle();
                meshingThreads[k].Init(k, poolSize, this);
            }
        }

        void StartGenerationThreads () {
            if (effectiveMultithreadGeneration) {
                generationThreadsRunning = true;
                for (int k = 0; k < meshingThreads.Length; k++) {
                    MeshingThread thread = meshingThreads[k];
                    thread.waitEvent = new AutoResetEvent(false);
                    thread.meshGenerationThread = new Thread(() => GenerateChunkMeshDataInBackgroundThread(thread));
                    thread.meshGenerationThread.Start();
                }
            }
        }

        void StopGenerationThreads () {
            generationThreadsRunning = false;
            if (meshingThreads == null) return;

            // Signal all threads to stop
            for (int k = 0; k < meshingThreads.Length; k++) {
                MeshingThread meshingThread = meshingThreads[k];
                if (meshingThread != null && meshingThread.meshGenerationThread != null) {
                    meshingThread.waitEvent.Set();
                }
            }

            // Wait for all threads to terminate with a reasonable timeout
            const int MAX_WAIT_MS = 5000; // 5 seconds total timeout
            const int SLEEP_INTERVAL_MS = 10;
            const int MAX_ITERATIONS = MAX_WAIT_MS / SLEEP_INTERVAL_MS;

            for (int t = 0; t < meshingThreads.Length; t++) {
                MeshingThread meshingThread = meshingThreads[t];
                if (meshingThread != null && meshingThread.meshGenerationThread != null) {
                    // Wait for thread to terminate
                    for (int k = 0; k < MAX_ITERATIONS; k++) {
                        if (!meshingThread.meshGenerationThread.IsAlive) {
                            break;
                        }
                        Thread.Sleep(SLEEP_INTERVAL_MS);
                    }

                    // If thread is still alive after timeout, try to abort it (not recommended but as a last resort)
                    if (meshingThread.meshGenerationThread.IsAlive) {
                        try {
                            meshingThread.meshGenerationThread.Abort();
                            Debug.LogWarning("Voxel Play: Had to abort a meshing thread that didn't terminate gracefully.");
                        }
                        catch (System.Exception ex) {
                            Debug.LogError("Voxel Play: Failed to abort meshing thread: " + ex.Message);
                        }
                    }

                    // Dispose of the wait event
                    try {
                        meshingThread.waitEvent.Close();
                        meshingThread.waitEvent.Dispose();
                    }
                    catch (System.Exception ex) {
                        Debug.LogError("Voxel Play: Error disposing thread wait event: " + ex.Message);
                    }

                    // Clear thread resources
                    meshingThread.Clear();

                    // Clear references
                    meshingThread.waitEvent = null;
                    meshingThread.meshGenerationThread = null;
                }
            }
        }


        void InitVirtualChunk () {
            emptyChunkUnderground = new Voxel[CHUNK_VOXEL_COUNT];
            emptyChunkAboveTerrain = new Voxel[CHUNK_VOXEL_COUNT];
            for (int k = 0; k < emptyChunkAboveTerrain.Length; k++) {
                emptyChunkAboveTerrain[k].light = FULL_LIGHT;
                emptyChunkUnderground[k].typeIndex = Voxel.DEFAULT_VOXEL_TYPE_INDEX;
                emptyChunkUnderground[k].opaque = FULL_OPAQUE;
            }

            virtualChunk = new VirtualVoxel[CHUNK_SIZE_PLUS_2 * CHUNK_SIZE_PLUS_2 * CHUNK_SIZE_PLUS_2];

            int index = 0;
            for (int y = 0; y < CHUNK_SIZE_PLUS_2; y++) {
                for (int z = 0; z < CHUNK_SIZE_PLUS_2; z++) {
                    for (int x = 0; x < CHUNK_SIZE_PLUS_2; x++, index++) {
                        int vy = 1, vz = 1, vx = 1;
                        if (y == 0) {
                            vy = 0;
                        } else if (y == CHUNK_SIZE + 1) {
                            vy = 2;
                        }
                        if (z == 0) {
                            vz = 0;
                        } else if (z == CHUNK_SIZE + 1) {
                            vz = 2;
                        }
                        if (x == 0) {
                            vx = 0;
                        } else if (x == CHUNK_SIZE + 1) {
                            vx = 2;
                        }
                        virtualChunk[index].chunk9Index = vy * 9 + vz * 3 + vx;
                        int py = (y + CHUNK_SIZE_MINUS_ONE) % CHUNK_SIZE;
                        int pz = (z + CHUNK_SIZE_MINUS_ONE) % CHUNK_SIZE;
                        int px = (x + CHUNK_SIZE_MINUS_ONE) % CHUNK_SIZE;
                        virtualChunk[index].voxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
                    }
                }
            }
        }

        #endregion


        #region Rendering


        public void UpdateMaterialProperties () {

            NotifyCameraMove();
            UpdateAmbientProperties();
            UpdateLightProperties();

            if (renderingMaterials == null || renderingMaterials.Length == 0)
                return;

            if (enableFogSkyBlending) {
                Shader.EnableKeyword(SKW_VOXELPLAY_GLOBAL_USE_FOG);
            } else {
                Shader.DisableKeyword(SKW_VOXELPLAY_GLOBAL_USE_FOG);
            }

            UpdateRenderingMaterialsProperties();
            UpdateOutlinePropertiesMat(matDynamicOpaque);
            UpdateNormalMapPropertiesMat(matDynamicOpaque, this.enableNormalMap);
            UpdateParallaxPropertiesMat(matDynamicOpaque, this.enableReliefMapping);
            UpdatePBRPropertiesMat(matDynamicOpaque, this.enablePBR);
            UpdatePixelLightsPropertiesMat(matDynamicOpaque);

            UpdateOutlinePropertiesMat(matDynamicCutout);
            UpdateNormalMapPropertiesMat(matDynamicCutout, this.enableNormalMap);
            UpdateParallaxPropertiesMat(matDynamicCutout, this.enableReliefMapping);
            UpdatePBRPropertiesMat(matDynamicCutout, this.enablePBR);
            UpdatePixelLightsPropertiesMat(matDynamicCutout);

            UpdateOutlinePropertiesMat(matDynamicAlpha);
            UpdateNormalMapPropertiesMat(matDynamicAlpha, this.enableNormalMap);
            UpdateParallaxPropertiesMat(matDynamicAlpha, this.enableReliefMapping);
            UpdatePBRPropertiesMat(matDynamicAlpha, this.enablePBR);
            UpdatePixelLightsPropertiesMat(matDynamicAlpha);

            UpdateWaterMat();

            if (OnSettingsChanged != null) {
                OnSettingsChanged();
            }
        }

        void UpdateRenderingMaterialsProperties () {
            for (int k = 0; k < renderingMaterials.Length; k++) {
                Material mat = renderingMaterials[k].material;
                if (mat != null) {
                    bool enableNormalMap = this.enableNormalMap;
                    bool enableReliefMap = this.enableReliefMapping;
                    bool enablePBR = this.enablePBR;
                    TextureArrayPacker provider = renderingMaterials[k].descriptor.textureProvider;
                    if (provider != null) {
                        enableNormalMap = provider.settings.enableNormalMap;
                        enableReliefMap = provider.settings.enableReliefMap;
                        enablePBR = provider.settings.enablePBRMap;
                    }
                    if (provider != null) {
                        float textureScale = provider.settings.textureScale;
                        if (textureScale == 0) textureScale = 1f;
                        if (textureScale != 1f) {
                            textureScale = 1f / Mathf.Pow(2, textureScale - 1);
                            mat.SetTextureScale(ShaderParams.MainTex, new Vector2(textureScale, textureScale));
                        }
                    }
                    ToggleMaterialKeyword(mat, SKW_VOXELPLAY_TRANSP_BLING, transparentBling);
                    ToggleMaterialKeyword(mat, SKW_VOXELPLAY_AA_TEXELS, hqFiltering && !enableReliefMap);

                    UpdateOutlinePropertiesMat(mat);
                    UpdateNormalMapPropertiesMat(mat, enableNormalMap);
                    UpdateParallaxPropertiesMat(mat, enableReliefMap);
                    UpdatePBRPropertiesMat(mat, enablePBR);
                    UpdatePixelLightsPropertiesMat(mat);
                }
            }
        }

        void ToggleMaterialKeyword (Material mat, string keyword, bool enabled) {
            if (enabled && !mat.IsKeywordEnabled(keyword)) {
                mat.EnableKeyword(keyword);
            } else if (!enabled && mat.IsKeywordEnabled(keyword)) {
                mat.DisableKeyword(keyword);
            }
        }

        public float GetFogAutoDistance () {
            float thisFogDistance = cameraMain != null ? cameraMain.farClipPlane : 1000f;
            if (unloadFarChunks && !enableFarChunksRendering) {
                float unloadDistance = CHUNK_SIZE * visibleChunksDistance;
                if (thisFogDistance > unloadDistance) {
                    thisFogDistance = unloadDistance;
                }
            }
            return Mathf.Max(1f, thisFogDistance);
        }

        void UpdateAmbientProperties () {

            if (world == null)
                return;

            if (cameraMain != null && adjustCameraFarClip && distanceAnchor == cameraMain.transform) {
                cameraMain.farClipPlane = visibleChunksDistance * CHUNK_SIZE;
            }

            float thisFogDistance;
            if (fogDistanceAuto) {
                thisFogDistance = GetFogAutoDistance();
            } else {
                thisFogDistance = Mathf.Max(1f, fogDistance);
            }
            float thisFogStart = thisFogDistance * fogFallOff;
            Vector3 fogData = new Vector3(thisFogStart * thisFogStart, Mathf.Max(0.0001f, thisFogDistance * thisFogDistance - thisFogStart * thisFogStart), 0);
            Shader.SetGlobalVector(ShaderParams.VPFogData, fogData);

            // Global sky & global uniforms
            Shader.SetGlobalColor(ShaderParams.VPSkyTint, world.skyTint);
            Shader.SetGlobalColor(ShaderParams.VPGroundColor, world.groundColor);
            Shader.SetGlobalColor(ShaderParams.VPFogTint, fogTint);
            Shader.SetGlobalFloat(ShaderParams.VPFogAmount, fogAmount);
            Shader.SetGlobalFloat(ShaderParams.VPExposure, world.exposure);
            Shader.SetGlobalFloat(ShaderParams.VPAmbientLight, ambientLight);
            Shader.SetGlobalVector(ShaderParams.VPDiffuseWrap, new Vector4(diffuseWrap, 1.0f / (1.0f + diffuseWrap), 0, 0));
            Shader.SetGlobalFloat(ShaderParams.VPDaylightShadowAtten, daylightShadowAtten);
            Shader.SetGlobalFloat(ShaderParams.VPGrassWindSpeed, world.grassWindSpeed * 0.01f);
            Shader.SetGlobalFloat(ShaderParams.VPTreeWindSpeed, world.treeWindSpeed * 0.005f);
            Shader.SetGlobalFloat(ShaderParams.VPObscuranceIntensity, 1.0001f + obscuranceIntensity);
            Shader.SetGlobalColor(ShaderParams.VPShadowTintColor, world.shadowTintColor);
            Shader.SetGlobalFloat(ShaderParams.VPLightIntensity, sun != null ? Mathf.Max(sun.intensity, 0.001f) : 1f);
            Shader.SetGlobalFloat(ShaderParams.VPWaterDepthClip, enableFarChunksRendering ? 0f : 1f);

            // Update skybox material
            VoxelPlaySkybox worldSkybox = isMobilePlatform ? world.skyboxMobile : world.skyboxDesktop;

            if (worldSkybox != VoxelPlaySkybox.UserDefined) {
                if (skyboxMaterial != RenderSettings.skybox || RenderSettings.skybox == null) {
                    switch (worldSkybox) {
                        case VoxelPlaySkybox.Earth:
                            if (skyboxEarth == null) {
                                skyboxEarth = Resources.Load<Material>("VoxelPlay/Materials/VP Skybox Earth");
                            }
                            skyboxMaterial = skyboxEarth;
                            break;
                        case VoxelPlaySkybox.EarthSimplified:
                            if (skyboxEarthSimplified == null) {
                                skyboxEarthSimplified = Resources.Load<Material>("VoxelPlay/Materials/VP Skybox Earth Simplified");
                            }
                            skyboxMaterial = skyboxEarthSimplified;
                            break;
                        case VoxelPlaySkybox.Space:
                            if (skyboxSpace == null) {
                                skyboxSpace = Resources.Load<Material>("VoxelPlay/Materials/VP Skybox Space");
                            }
                            skyboxMaterial = skyboxSpace;
                            break;
                        case VoxelPlaySkybox.EarthNightCubemap:
                            if (skyboxEarthNightCube == null) {
                                skyboxEarthNightCube = Resources.Load<Material>("VoxelPlay/Materials/VP Skybox Earth Night Cubemap");
                            }
                            if (world.skyboxNightCubemap != null) {
                                skyboxEarthNightCube.SetTexture(ShaderParams.NightTex, world.skyboxNightCubemap);
                            }
                            skyboxMaterial = skyboxEarthNightCube;
                            break;
                        case VoxelPlaySkybox.EarthDayNightCubemap:
                            if (skyboxEarthDayNightCube == null) {
                                skyboxEarthDayNightCube = Resources.Load<Material>("VoxelPlay/Materials/VP Skybox Earth Day Night Cubemap");
                            }
                            if (world.skyboxDayCubemap != null)
                                skyboxEarthDayNightCube.SetTexture(ShaderParams.DayTex, world.skyboxDayCubemap);
                            if (world.skyboxNightCubemap != null)
                                skyboxEarthDayNightCube.SetTexture(ShaderParams.NightTex, world.skyboxNightCubemap);
                            skyboxMaterial = skyboxEarthDayNightCube;
                            break;
                    }
                    if (sun != null) {
                        skyboxMaterial.SetColor(ShaderParams.SunLightColor, sun.color);
                    }
                    RenderSettings.skybox = skyboxMaterial;
                }
            }

        }

        void UpdateLightProperties () {
            float maxLightDistance = brightPointsMaxDistance * brightPointsMaxDistance;
            Shader.SetGlobalFloat(ShaderParams.VPLightMaxDistSqr, maxLightDistance);
        }

        void UpdateWaterMat () {
            // Update realistic water properties
            Material waterMat = renderingMaterials[INDICES_BUFFER_WATER].material;
            if (waterMat == null) return;
            waterMat.SetColor(ShaderParams.WaterColor, world.waterColor);
            waterMat.SetFloat(ShaderParams.WaveAmplitude, world.waveAmplitude);
            waterMat.SetColor(ShaderParams.UnderWaterFogColor, world.underWaterFogColor);
            waterMat.SetFloat(ShaderParams.SpecularIntensity, world.specularIntensity);
            waterMat.SetFloat(ShaderParams.SpecularPower, world.specularPower);
            waterMat.SetColor(ShaderParams.FoamColor, world.foamColor);
            if (realisticWater) {
                waterMat.SetFloat(ShaderParams.WaveScale, world.waveScale * world.waveAmplitude);
                waterMat.SetFloat(ShaderParams.WaveSpeed, world.waveSpeed * world.waveAmplitude);
                waterMat.SetFloat(ShaderParams.RefractionDistortion, world.refractionDistortion * world.waveAmplitude);
                waterMat.SetFloat(ShaderParams.Fresnel, 1f - world.fresnel);
                waterMat.SetFloat(ShaderParams.NormalStrength, world.normalStrength * world.waveAmplitude);
                waterMat.SetVector(ShaderParams.OceanWave, new Vector3(world.oceanWaveThreshold, world.oceanWaveIntensity, 0));
            } else {
                waterMat.SetFloat(ShaderParams.WaveSpeed, world.waveAnimationSpeed);
            }
        }

        void UpdateOutlinePropertiesMat (Material mat) {
            if (mat == null) return;
            if (enableOutline) {
                mat.EnableKeyword(SKW_VOXELPLAY_USE_OUTLINE);
                mat.SetColor(ShaderParams.OutlineColor, outlineColor);
                mat.SetFloat(ShaderParams.OutlineThreshold, outlineThreshold * 10f);
            } else {
                mat.DisableKeyword(SKW_VOXELPLAY_USE_OUTLINE);
            }
        }

        void UpdateParallaxPropertiesMat (Material mat, bool enableReliefMap) {
            if (mat == null) return;
            if (enableReliefMap) {
                mat.EnableKeyword(SKW_VOXELPLAY_USE_PARALLAX);
                mat.SetFloat(ShaderParams.ParallaxStrength, reliefStrength);
                mat.SetFloat(ShaderParams.ParallaxMaxDistanceSqr, reliefMaxDistance * reliefMaxDistance);
                mat.SetInt(ShaderParams.ParallaxIterations, reliefIterations);
                mat.SetInt(ShaderParams.ParallaxIterationsBinarySearch, reliefIterationsBinarySearch);
            } else {
                mat.DisableKeyword(SKW_VOXELPLAY_USE_PARALLAX);
            }
        }

        void UpdateNormalMapPropertiesMat (Material mat, bool enableNormalMap) {
            if (mat == null) return;
            if (enableNormalMap) {
                mat.EnableKeyword(SKW_VOXELPLAY_USE_NORMAL);
            } else {
                mat.DisableKeyword(SKW_VOXELPLAY_USE_NORMAL);
            }
        }

        void UpdatePBRPropertiesMat (Material mat, bool enablePBR) {
            if (mat == null) return;
            if (enablePBR) {
                mat.EnableKeyword(SKW_VOXELPLAY_USE_PBR);
            } else {
                mat.DisableKeyword(SKW_VOXELPLAY_USE_PBR);
            }
        }

        void UpdatePixelLightsPropertiesMat (Material mat) {
            if (mat == null) return;
            if (usePixelLights) {
                mat.EnableKeyword(SKW_VOXELPLAY_USE_PIXEL_LIGHTS);
            } else {
                mat.DisableKeyword(SKW_VOXELPLAY_USE_PIXEL_LIGHTS);
            }
        }

        bool CreateChunkMeshJob (VoxelChunk chunk) {
            int threadId = chunk.poolIndex % meshingThreads.Length;
            return meshingThreads[threadId].CreateChunkMeshJob(chunk, generationThreadsRunning);
        }

        void GenerateChunkMeshDataInBackgroundThread (MeshingThread thread) {
            try {
                while (generationThreadsRunning) {
                    bool idle;
                    lock (thread.indicesUpdating) {
                        idle = thread.meshJobMeshDataGenerationIndex == thread.meshJobMeshLastIndex;
                    }
                    if (idle) {
                        thread.waitEvent.WaitOne();
                        continue;
                    }
                    GenerateChunkMeshDataOneJob(thread);
                    lock (thread.indicesUpdating) {
                        thread.meshJobMeshDataGenerationReadyIndex = thread.meshJobMeshDataGenerationIndex;
                    }
                }
            }
            catch (Exception ex) {
                ShowExceptionMessage(ex);
            }
        }



        void GenerateChunkMeshDataInMainThread (long endTime) {

            long elapsed;
            MeshingThread thread = meshingThreads[0];
            do {
                if (thread.meshJobMeshDataGenerationIndex == thread.meshJobMeshLastIndex)
                    return;
                GenerateChunkMeshDataOneJob(thread);
                thread.meshJobMeshDataGenerationReadyIndex = thread.meshJobMeshDataGenerationIndex;
                elapsed = stopWatch.ElapsedMilliseconds;
            } while (elapsed < endTime);
        }


        void GenerateChunkMeshDataOneJob (MeshingThread thread) {
            lock (thread.indicesUpdating) {
                thread.meshJobMeshDataGenerationIndex++;
                if (thread.meshJobMeshDataGenerationIndex >= thread.meshJobs.Length) {
                    thread.meshJobMeshDataGenerationIndex = 0;
                }
            }

            VoxelChunk chunk = thread.meshJobs[thread.meshJobMeshDataGenerationIndex].chunk;
            Voxel[][] chunk9 = thread.chunk9;
            chunk9[13] = chunk.voxels;
            Voxel[] emptyChunk = chunk.isAboveSurface ? emptyChunkAboveTerrain : emptyChunkUnderground;
            FastMath.FloorToInt(chunk.position.x / CHUNK_SIZE, chunk.position.y / CHUNK_SIZE, chunk.position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);

            VoxelChunk[] neighbourChunks = thread.neighbourChunks;
            neighbourChunks[13] = chunk;
            for (int c = 0, y = -1; y <= 1; y++) {
                int yy = chunkY + y;
                for (int z = -1; z <= 1; z++) {
                    int zz = chunkZ + z;
                    for (int x = -1; x <= 1; x++, c++) {
                        if (y == 0 && z == 0 && x == 0)
                            continue;
                        int xx = chunkX + x;
                        if (GetChunkFast(xx, yy, zz, out VoxelChunk neighbour, false) && (neighbour.isPopulated || neighbour.isRendered)) {
                            chunk9[c] = neighbour.voxels;
                        } else {
                            chunk9[c] = emptyChunk;
                        }
                        neighbourChunks[c] = neighbour;
                    }
                }
            }

            lock (seeThroughLock) {
                // Hide voxels marked as hidden
                for (int c = 0; c < neighbourChunks.Length; c++) {
                    ToggleHiddenVoxels(neighbourChunks[c], false);
                }
                thread.GenerateMeshData();
                // Reactivate hidden voxels
                for (int c = 0; c < neighbourChunks.Length; c++) {
                    ToggleHiddenVoxels(neighbourChunks[c], true);
                }
            }
        }

        void UploadMeshData (MeshingThread thread, int jobIndex) {
            MeshJobData[] meshJobs = thread.meshJobs;
            VoxelChunk chunk = meshJobs[jobIndex].chunk;

            // Update collider?
            if (enableColliders && meshJobs[jobIndex].needsColliderRebuild) {
                meshJobs[jobIndex].needsColliderRebuild = false;
                int colliderVerticesCount = meshJobs[jobIndex].colliderVertices.Count;
                Mesh colliderMesh = chunk.mc.sharedMesh;
#if UNITY_EDITOR
                if (renderInEditorDetail != EditorRenderDetail.StandardPlusColliders && !applicationIsPlaying) {
                    colliderVerticesCount = 0;
                }
#endif
                if (colliderVerticesCount == 0) {
                    chunk.mc.enabled = false;
                } else {
                    if (colliderMesh == null) {
                        colliderMesh = new Mesh();
                    } else {
                        colliderMesh.Clear();
                    }
                    SetMeshVertices(colliderMesh, meshJobs[jobIndex].colliderVertices);
                    colliderMesh.SetTriangles(meshJobs[jobIndex].colliderIndices, 0);
                    chunk.mc.sharedMesh = colliderMesh;
                    chunk.mc.enabled = true;
                }

                // Update navmesh
                if (enableNavMesh) {
                    int navMeshVerticesCount = meshJobs[jobIndex].navMeshVertices.Count;
                    Mesh navMesh = chunk.navMesh;
                    bool chunkHasNavMesh = navMesh != null;
                    if (navMeshVerticesCount > 0 || chunkHasNavMesh) {
                        if (navMeshVerticesCount == 0) {
                            ReleaseChunkNavMesh(chunk);
                        } else {
                            // if we have new navmesh data or it's cleared but chunk has old navmesh, update
                            if (chunkHasNavMesh) {
                                navMesh.Clear();
                            } else {
                                navMesh = new Mesh();
                            }
                            SetMeshVertices(navMesh, meshJobs[jobIndex].navMeshVertices);
                            navMesh.SetTriangles(meshJobs[jobIndex].navMeshIndices, 0);
                            chunk.navMesh = navMesh;
                            AddChunkNavMesh(chunk);
                        }
                    }
                }
            }

            // Refresh highlight
            RefreshVoxelHighlight();

            // Empty chunk or server mode? Exit now
            if (meshJobs[jobIndex].totalVoxels == 0 || isServerMode) {


                if (chunk.mf.sharedMesh != null) {
                    chunk.mf.sharedMesh.Clear(false);
                }
                // Remove any existing custom voxel
                if (chunk.totalVisibleVoxelsCount != 0) {
                    ClearCustomVoxels(chunk);
                    chunk.totalVisibleVoxelsCount = 0;

                    if (OnChunkRender != null) {
                        OnChunkRender(chunk);
                    }
                } else {
                    // custom voxels still need to be rendered in server mode as they may contain server logic, colliders, etc.
                    RenderModelsInChunk(chunk, meshJobs[jobIndex].mivs);
                    chunk.totalVisibleVoxelsCount = meshJobs[jobIndex].totalVoxels;
                }

                chunk.renderState = ChunkRenderState.RenderingComplete;
                return;
            }

            // Otherwise, create or update mesh
            Mesh mesh = chunk.mf.sharedMesh;
#if !UNITY_EDITOR
            if (isMobilePlatform) {
                Misc.DestroyImmediateAndNullify(ref mesh);
                chunk.mf.sharedMesh = null;
                mesh = new Mesh(); // on mobile will be released mesh data upon uploading to the GPU so the mesh is no longer readable; need to recreate it everytime the chunk is rendered
                chunksDrawn++;
            } else {
                if (mesh == null) {
                    mesh = new Mesh();
                    chunksDrawn++;
                } else {
                    mesh.Clear();
                }
            }
#else
            if (mesh == null) {
                mesh = new Mesh();
                chunksDrawn++;
            } else {
                voxelsCreatedCount -= chunk.totalVisibleVoxelsCount;
                mesh.Clear();
            }
#endif
            chunk.totalVisibleVoxelsCount = meshJobs[jobIndex].totalVoxels;
            voxelsCreatedCount += chunk.totalVisibleVoxelsCount;

            // Assign materials and submeshes
            mesh.subMeshCount = meshJobs[jobIndex].subMeshCount;
            if (mesh.subMeshCount > 0) {

                // Vertices
                SetMeshVertices(mesh, meshJobs[jobIndex].vertices);

                // UVs, normals, colors
                mesh.SetUVs(0, meshJobs[jobIndex].uv0);
                mesh.SetNormals(meshJobs[jobIndex].normals);
                if (enableTinting) {
                    mesh.SetColors(meshJobs[jobIndex].colors);
                }

                int subMeshIndex = -1;
                int matIndex = 0;

                for (int k = 0; k < MAX_MATERIALS_PER_CHUNK; k++) {
                    if (meshJobs[jobIndex].indexBuffers[k].Count > 0) {
                        subMeshIndex++;
                        mesh.SetTriangles(meshJobs[jobIndex].indexBuffers[k], subMeshIndex, false);
                        matIndex += 1 << k;
                    }
                }

                // Compute material array
                if (!materialsDict.TryGetValue(matIndex, out Material[] matArray)) {
                    matArray = new Material[mesh.subMeshCount];
                    for (int k = 0, j = 0; k < MAX_MATERIALS_PER_CHUNK; k++) {
                        if (meshJobs[jobIndex].indexBuffers[k].Count > 0) {
                            matArray[j++] = renderingMaterials[k].material;
                        }
                    }
                    materialsDict[matIndex] = matArray;
                }
                chunk.mr.sharedMaterials = matArray;

                if (chunk.isCloud) {
                    mesh.bounds = boundsCloud;
                } else if (enableCurvature) {
                    mesh.bounds = boundsWithCurvature;
                } else {
                    mesh.bounds = boundsChunk;
                }

                chunk.mf.sharedMesh = mesh;

                ReflectionProbeUsage probeUsage = reflectionProbeUsage;
                if (enablePBR && probeUsage == ReflectionProbeUsage.Off) {
                    probeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                }
                if (probeUsage != ReflectionProbeUsage.Off) {
                    mesh.RecalculateTangents();
                    chunk.mr.reflectionProbeUsage = probeUsage;
                }

#if !UNITY_EDITOR
                if (isMobilePlatform) {
                    mesh.UploadMeshData(true);
                }
#endif
                if (!chunk.mr.enabled) {
                    chunk.mr.enabled = true;
                }
            }

            RenderModelsInChunk(chunk, meshJobs[jobIndex].mivs);

            if (chunk.renderState != ChunkRenderState.RenderingComplete) {
                chunk.renderState = ChunkRenderState.RenderingComplete;
                if (OnChunkAfterFirstRender != null) {
                    OnChunkAfterFirstRender(chunk);
                }
            }

            if (OnChunkRender != null) {
                OnChunkRender(chunk);
            }

            shouldUpdateParticlesLighting = true;
        }

        void BakeMeshLighting (VoxelDefinition voxelDefinition, Color32 tintColor, VoxelPlaceholder placeholder) {

            int partsCount = placeholder.parts.Length;
            for (int p = 0; p < partsCount; p++) {
                BakeMeshPartLighting(voxelDefinition, tintColor, placeholder, p);
            }
        }


        void BakeMeshPartLighting (VoxelDefinition voxelDefinition, Color32 tintColor, VoxelPlaceholder placeholder, int partIndex) {

            BakedMesh bm = new BakedMesh(placeholder.parts[partIndex].originalMesh, tintColor);

            if (bakedMeshes.TryGetValue(bm, out Mesh mesh)) {
                placeholder.parts[partIndex].meshFilter.sharedMesh = mesh;
                return;
            }

            Mesh meshWithColors = Instantiate(bm.mesh);

            Color32[] originalColors32 = voxelDefinition.meshColors32[partIndex];
            modelMeshColors.Clear();
            int vertexCount = meshWithColors.vertexCount;
            if (originalColors32 == null || originalColors32.Length == 0) {
                for (int c = 0; c < vertexCount; c++) {
                    modelMeshColors.Add(tintColor);
                }
            } else {
                for (int c = 0; c < vertexCount; c++) {
                    Color32 color = tintColor.MultiplyRGB(originalColors32[c]);
                    modelMeshColors.Add(color);
                }
            }
            meshWithColors.SetColors(modelMeshColors);

            bakedMeshes[bm] = meshWithColors;

            placeholder.parts[partIndex].meshFilter.sharedMesh = meshWithColors;
        }

        void SetMeshVertices (Mesh mesh, List<Vector3> vertices) {

            IndexFormat indexFormat = mesh.indexFormat;
            IndexFormat desiredFormat = vertices.Count > 65530 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            if (indexFormat != desiredFormat) {
                int subMeshCount = mesh.subMeshCount;
                mesh.indexFormat = desiredFormat;
                mesh.subMeshCount = subMeshCount; // subMeshCount is reset when changing index format
            }
            mesh.SetVertices(vertices);

        }

        void BakeMeshLightingWithGather (VoxelDefinition voxelDefinition, Color32 tintColor, VoxelPlaceholder placeholder) {

            int partsCount = placeholder.parts.Length;
            for (int p = 0; p < partsCount; p++) {
                BakeMeshPartLightingWithGather(voxelDefinition, tintColor, placeholder, p);
            }
        }


        void BakeMeshPartLightingWithGather (VoxelDefinition voxelDefinition, Color32 tintColor, VoxelPlaceholder placeholder, int partIndex) {

            Vector3d worldPos = placeholder.parts[partIndex].meshFilter.transform.position;
            FastVector.Middling(ref worldPos);

            int i = 0;
            Vector3d pos = worldPos;
            for (int y = -1; y <= 1; y++) {
                pos.y = worldPos.y + y;
                for (int z = -1; z <= 1; z++) {
                    pos.z = worldPos.z + z;
                    for (int x = -1; x <= 1; x++) {
                        pos.x = worldPos.x + x;
                        if (GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex)) {
                            tintColor.a = (byte)(chunk.voxels[voxelIndex].light + (chunk.voxels[voxelIndex].torchLight << 4));
                        } else {
                            tintColor.a = 15;
                        }
                        surroundingColors[i++] = tintColor;
                    }
                }
            }

            Color32[] originalColors32 = voxelDefinition.meshColors32[partIndex];
            placeholder.parts[partIndex].originalMesh.GetVertices(modelMeshVertices);
            modelMeshColors.Clear();
            int vertexCount = placeholder.parts[partIndex].originalMesh.vertexCount;
            Quaternion rot = placeholder.transform.rotation;
            bool hasMeshColors = originalColors32 != null && originalColors32.Length > 0;
            int hash = 27;
            unchecked {
                for (int c = 0; c < vertexCount; c++) {
                    Vector3 v = modelMeshVertices[c];
                    v = (rot * v).normalized;
                    float vx = v.x + 1f;
                    float vy = v.y + 1f;
                    float vz = v.z + 1f;
                    int ix = (int)vx;
                    int iy = (int)vy;
                    int iz = (int)vz;
                    if (ix > 1) ix = 1;
                    if (iy > 1) iy = 1;
                    if (iz > 1) iz = 1;
                    float ly0z0x0 = surroundingColors[(iy + 0) * 9 + (iz + 0) * 3 + (ix + 0)].a;
                    float ly0z0x1 = surroundingColors[(iy + 0) * 9 + (iz + 0) * 3 + (ix + 1)].a;
                    float ly0z1x0 = surroundingColors[(iy + 0) * 9 + (iz + 1) * 3 + (ix + 0)].a;
                    float ly0z1x1 = surroundingColors[(iy + 0) * 9 + (iz + 1) * 3 + (ix + 1)].a;
                    float ly1z0x0 = surroundingColors[(iy + 1) * 9 + (iz + 0) * 3 + (ix + 0)].a;
                    float ly1z0x1 = surroundingColors[(iy + 1) * 9 + (iz + 0) * 3 + (ix + 1)].a;
                    float ly1z1x0 = surroundingColors[(iy + 1) * 9 + (iz + 1) * 3 + (ix + 0)].a;
                    float ly1z1x1 = surroundingColors[(iy + 1) * 9 + (iz + 1) * 3 + (ix + 1)].a;
                    float fx = vx - ix;
                    float fy = vy - iy;
                    float fz = vz - iz;
                    float light = ly0z0x0 * (1f - fy) * (1f - fz) * (1f - fx) + ly0z0x1 * (1f - fy) * (1f - fz) * fx +
                    ly0z1x0 * (1f - fy) * fz * (1f - fx) + ly0z1x1 * (1f - fy) * fz * fx +
                    ly1z0x0 * fy * (1f - fz) * (1f - fx) + ly1z0x1 * fy * (1f - fz) * fx +
                    ly1z1x0 * fy * fz * (1f - fx) + ly1z1x1 * fy * fz * fx;
                    light *= 1.4f;
                    if (light > 15) light = 15;
                    tintColor.a = (byte)light;
                    Color32 color = hasMeshColors ? tintColor.MultiplyRGB(originalColors32[c]) : tintColor;
                    modelMeshColors.Add(color);

                    hash = (13 * hash) + color.r;
                    hash = (13 * hash) + color.g;
                    hash = (13 * hash) + color.b;
                    hash = (13 * hash) + color.a;
                }
            }

            BakedMeshWithGather bm = new BakedMeshWithGather(placeholder.parts[partIndex].originalMesh, hash);

            if (bakedMeshesWithGather.TryGetValue(bm, out Mesh mesh)) {
                placeholder.parts[partIndex].meshFilter.sharedMesh = mesh;
                return;
            }

            Mesh meshWithColors = Instantiate(bm.mesh);
            meshWithColors.SetColors(modelMeshColors);

            bakedMeshesWithGather[bm] = meshWithColors;
            placeholder.parts[partIndex].meshFilter.sharedMesh = meshWithColors;
        }


        void ClearCustomVoxels (VoxelChunk chunk) {

            instancedRenderer.ClearChunk(chunk);

            // deactivate all models in this chunk
            // we need to iterate the placeholders list entirely to address the case when the voxel is not using GPU instancing. In this case the gameobject renderer needs to be disabled 
            // and we need to do this way because mivs won't contain the custom voxel since it may be temporarily converted to a transparent voxels due to see-through effect
            if (chunk.placeholders != null) {
                int count = chunk.placeholders.Count;
                for (int k = 0; k < count; k++) {
                    if (chunk.placeholders.entries[k].key >= 0) {
                        VoxelPlaceholder placeHolder = chunk.placeholders.entries[k].value;
                        if (placeHolder != null) {
                            placeHolder.ToggleRenderers(false);
                        }
                    }
                }
            }
        }

        readonly List<MeshFilter> tmpMeshFilters = new List<MeshFilter>();
        void RenderModelsInChunk (VoxelChunk chunk, FastList<MivEntry> voxelIndices) {

            ClearCustomVoxels(chunk);

            Quaternion rotation = Misc.quaternionZero;
            Vector3 position;

            for (int k = 0; k < voxelIndices.count; k++) {
                int voxelIndex = voxelIndices.values[k].voxelindex;

                if (chunk.voxels[voxelIndex].typeIndex <= 0) {
                    continue;
                }

                if (VoxelIsHidden(chunk, voxelIndex)) {
                    continue;
                }

                VoxelDefinition voxelDefinition = voxelIndices.values[k].renderingVoxelDefinition;

                bool createGO = voxelDefinition.createGameObject || !voxelDefinition.gpuInstancing;

                if (createGO) {
                    VoxelPlaceholder placeholder = GetVoxelPlaceholder(chunk, voxelIndex, voxelDefinition, true);
                    bool createModel = true;

                    position = placeholder.transform.position;

                    GameObject prefab = voxelDefinition.prefab;
                    if (placeholder.modelInstance != null) {
                        if (placeholder.modelTemplate != prefab) {
                            DestroyImmediate(placeholder.modelInstance);
                            placeholder.ResetLastMivTintColors();
                        } else {
                            createModel = false;
                        }
                    }

                    if (createModel || placeholder.modelInstance == null) {
                        if (prefab == null) continue;

                        placeholder.modelTemplate = prefab;
                        placeholder.modelInstance = Instantiate(prefab, placeholder.transform, false);
                        Transform tModel = placeholder.modelInstance.transform;
                        tModel.localScale = voxelDefinition.scale;
                        placeholder.modelInstance.name = "DynamicVoxelInstance";

                        // Note: placeHolder.modelInstance layer must be different from layerVoxels to allow dynamic voxels collide with terrain. So don't set its layer to layer voxels
                        placeholder.modelMeshRenderers = placeholder.modelInstance.GetComponentsInChildren<MeshRenderer>();
                        if (voxelDefinition.gpuInstancing) {
                            placeholder.ToggleRenderers(false);
                        } else {
                            placeholder.modelInstance.GetComponentsInChildren(true, tmpMeshFilters);
                            int partsCount = tmpMeshFilters.Count;
                            placeholder.parts = new VoxelPlaceholder.ModelParts[partsCount];
                            if (voxelDefinition.meshColors32 == null || voxelDefinition.meshColors32.Length != partsCount) {
                                voxelDefinition.meshColors32 = new Color32[partsCount][];
                            }
                            for (int p = 0; p < partsCount; p++) {
                                MeshFilter mf = tmpMeshFilters[p];
                                placeholder.parts[p].lastMivTintColor = Misc.color32White;
                                placeholder.parts[p].meshFilter = mf;
                                Mesh originalMesh = mf.sharedMesh;
                                placeholder.parts[p].originalMesh = originalMesh;
                                if (voxelDefinition.meshColors32[p] == null && originalMesh != null) {
                                    voxelDefinition.meshColors32[p] = originalMesh.colors32;
                                }
                            }
                        }
                    } else if (!voxelDefinition.gpuInstancing) {
                        placeholder.ToggleRenderers(true);
                    }

                    if (voxelDefinition.gpuInstancing) {
                        rotation = placeholder.transform.localRotation;
                    } else {
                        // Adjust lighting
                        if ((effectiveGlobalIllumination || chunk.voxels[voxelIndex].isColored) && placeholder.parts != null) {
                            int partCount = placeholder.modelMeshRenderers.Length;
                            // Update mesh colors
                            for (int p = 0; p < partCount; p++) {
                                MeshFilter mf = placeholder.parts[p].meshFilter;
                                if (mf != null) {
                                    Mesh mesh = placeholder.parts[p].originalMesh;
                                    if (mesh != null) {
                                        Color32 tintColor = chunk.voxels[voxelIndex].color;
                                        if (voxelDefinition.computeLighting) {
                                            BakeMeshLightingWithGather(voxelDefinition, tintColor, placeholder);
                                        } else {
                                            tintColor.a = (byte)(chunk.voxels[voxelIndex].light + (chunk.voxels[voxelIndex].torchLight << 4));
                                            Color32 lastMivTintColor = placeholder.parts[p].lastMivTintColor;
                                            if (lastMivTintColor.a != tintColor.a || lastMivTintColor.r != tintColor.r || lastMivTintColor.g != tintColor.g || lastMivTintColor.b != tintColor.b) {
                                                BakeMeshLighting(voxelDefinition, tintColor, placeholder);
                                                placeholder.parts[p].lastMivTintColor = tintColor;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (!placeholder.modelInstance.gameObject.activeSelf) {
                        placeholder.modelInstance.gameObject.SetActive(true);
                    }
                } else {
                    // pure gpu instancing, no gameobject

                    Vector3d voxelPosition = GetVoxelPosition(chunk, voxelIndex);

                    rotation = voxelDefinition.GetRotation(voxelPosition); // deterministic rotation by position

                    // User rotation (ignore if rendering voxel is driven by a connected voxel rule)
                    if (chunk.voxels[voxelIndex].typeIndex == voxelDefinition.index) {
                        float rot = chunk.voxels[voxelIndex].GetTextureRotationDegrees();
                        if (rot != 0) {
                            rotation *= Quaternion.Euler(0, rot, 0);
                        }
                    }

                    // Custom position
                    voxelPosition += rotation * voxelDefinition.GetOffset(voxelPosition);

                    position = voxelPosition;
                }

                if (voxelDefinition.gpuInstancing) {
                    instancedRenderer.AddVoxel(chunk, voxelIndex, voxelDefinition, position, rotation, voxelDefinition.scale);
                }

            }
        }

        #endregion

    }



}
