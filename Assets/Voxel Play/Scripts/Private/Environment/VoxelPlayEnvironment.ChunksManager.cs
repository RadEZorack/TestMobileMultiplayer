using System;
using System.Collections.Generic;
using UnityEngine;


namespace VoxelPlay {

    public struct HeightMapInfo {
        public float moisture;
        public float height;
        public readonly int groundLevel => FastMath.FloorToInt(height);
        public BiomeDefinition biome;
        public BiomeDefinition biomeSecondary;
        public float biomeMixingWeight;
    }

    public class Octree {
        public Vector3d center;
        public int extents;
        public Octree parent;
        public Octree[] children;
        public int exploredChildren;
        public bool explored;

        public Octree (Octree parent, Vector3d center, int extents) {
            this.parent = parent;
            this.center = center;
            this.extents = extents;
        }

        public void Explode () {
            children = new Octree[8];
            int half = extents / 2;
            children[0] = new Octree(this, new Vector3d(center.x - half, center.y + half, center.z + half), half);
            children[1] = new Octree(this, new Vector3d(center.x + half, center.y + half, center.z + half), half);
            children[2] = new Octree(this, new Vector3d(center.x - half, center.y + half, center.z - half), half);
            children[3] = new Octree(this, new Vector3d(center.x + half, center.y + half, center.z - half), half);
            children[4] = new Octree(this, new Vector3d(center.x - half, center.y - half, center.z + half), half);
            children[5] = new Octree(this, new Vector3d(center.x + half, center.y - half, center.z + half), half);
            children[6] = new Octree(this, new Vector3d(center.x - half, center.y - half, center.z - half), half);
            children[7] = new Octree(this, new Vector3d(center.x + half, center.y - half, center.z - half), half);
        }
    }


    public partial class VoxelPlayEnvironment : MonoBehaviour {

        struct LinkedChunk {
            public VoxelChunk chunk;
            public int prev, next;
            public bool used;
        }

        const int WORLD_SIZE_WIDTH = 10000;
        const int WORLD_SIZE_DEPTH = 10000;
        const int WORLD_SIZE_HEIGHT = 1000;

        const int NO_WATER = -1000000;

        // Chunk Creation
        [NonSerialized]
        public int waterLevel;

        public bool hasWater {
            get { return waterLevel > NO_WATER; }
            set { if (value == false) waterLevel = NO_WATER; }
        }

        int lastChunkX, lastChunkY, lastChunkZ;
        Dictionary<Vector3d, Octree> octreeRoots;
        int octreeSize;
        int chunkRequestLast;
        Vector3[] frustumCorners;
        const int CHUNKS_CREATION_BUFFER_SIZE = 15000;
        FastHashSet<CachedChunk> cachedChunks;
        int visible_xmin, visible_xmax, visible_ymin, visible_ymax, visible_zmin, visible_zmax;
        int meshingIdle;

        // Chunk Rendering
        const int CHUNKS_RENDER_BUFFER_SIZE = 20000;
        LinkedChunk[] linkedChunks;
        Transform chunksRoot;
        int chunkQueueRoot;
        Octree[] chunkRequests;
        GameObject chunkPlaceholderPrefab;
        int frustumCheckIteration;
        FastList<VoxelChunk> updatedChunks;
        float forceChunkSqrDistance;
        int uploadingMeshingThreadIndex;

        // Heightmap
        HeightMapCache heightMapCache;

        // Materials & other
        double lastChunkDistanceSqr;
        Plane[] frustumPlanes;
        Vector3[] frustumPlanesNormals;
        float[] frustumPlanesDistances;

        // Detail generation
        bool worldHasDetailGenerators;
        bool needDetailGeneratorInvoke;

        #region Terrain engine initialization

        void InitHeightMap () {
            if (heightMapCache == null) {
                heightMapCache = new HeightMapCache();
            } else {
                heightMapCache.Clear();
            }
        }

        void InitBiomes () {
            if (world == null)
                return;

            if (world.defaultBiome == null && (world.biomes == null || world.biomes.Length == 0)) {
                // provide a fallback / default biome when starting
                world.defaultBiome = Resources.Load<BiomeDefinition>("VoxelPlay/Defaults/DefaultBiome");
            }

            if (world.defaultBiome != null) {
                world.defaultBiome.ValidateSettings(showDebugWarnings: false);
            }

            if (world.biomes == null)
                return;

            for (int b = 0; b < world.biomes.Length; b++) {
                BiomeDefinition biome = world.biomes[b];
                if (biome != null) {
                    biome.ValidateSettings(showDebugWarnings: false);
                    if (biome.voxelTop == null) {
                        LogMessage("Biome " + biome.name + " has no voxel top defined. Assigning a default voxel.");
                        biome.voxelTop = defaultVoxel;
                    }
                    if (biome.voxelDirt == null) {
                        LogMessage("Biome " + biome.name + " has no voxel dirt defined. Assigning a default voxel.");
                        biome.voxelDirt = defaultVoxel;
                    }
                }
            }

            SetBiomeDefaultColors(false);
        }

        public void NotifyTerrainGeneratorConfigurationChanged () {
            InitHeightMap();
            InitBiomes();
            if (world != null && world.terrainGenerator != null) {
                world.terrainGenerator.Initialize();
            }
        }

        void InitChunkManager () {

            if (chunksPool == null || chunksPool.Length != maxChunks) {
                chunksPool = new VoxelChunk[maxChunks];
            }

            cachedChunks = new FastHashSet<CachedChunk>(CHUNKS_CREATION_BUFFER_SIZE);
            chunkRequests = new Octree[CHUNKS_CREATION_BUFFER_SIZE];
            chunkRequestLast = -1;
            linkedChunks = new LinkedChunk[CHUNKS_RENDER_BUFFER_SIZE];
            chunkQueueRoot = 0;

            frustumPlanes = new Plane[6];
            frustumPlanesDistances = new float[6];
            frustumPlanesNormals = new Vector3[6];
            frustumCorners = new Vector3[4];

            InitLightmap();
            InitOctrees();
            InitHeightMap();
            InitBiomes();

            NoiseTools.seedOffset = WorldRand.GetVector3(Vector3d.zero, 1024);
            frustumCheckIteration = 1;
            forceChunkSqrDistance = (forceChunkDistance * CHUNK_SIZE) * (forceChunkDistance * CHUNK_SIZE);
            if (updatedChunks == null) {
                updatedChunks = new FastList<VoxelChunk>(50);
            } else {
                updatedChunks.Clear();
            }

            if (world != null) {
                if (world.terrainGenerator == null) {
                    world.terrainGenerator = Resources.Load<NullTerrainGenerator>("VoxelPlay/Defaults/NullTerrainGenerator");
                }
                world.terrainGenerator.Initialize();

                worldHasDetailGenerators = world.detailGenerators != null && world.detailGenerators.Length > 0;
                if (worldHasDetailGenerators) {
                    for (int d = 0; d < world.detailGenerators.Length; d++) {
                        if (world.detailGenerators[d] == null) {
                            Debug.LogWarning("Voxel Play: one world detail generator is missing!");
                            world.detailGenerators = Misc.PackArray<VoxelPlayDetailGenerator>(world.detailGenerators);
                            d--;
                            continue;
                        }
                        world.detailGenerators[d].detailGeneratorIndex = d;
                        world.detailGenerators[d].Init();
                    }
                }
            }

            ClearStats();

            // Prepare scene root object
            if (chunkPlaceholderPrefab == null) {
                chunkPlaceholderPrefab = Resources.Load<GameObject>("VoxelPlay/Prefabs/ChunkGEO");
            }
            if (chunksRoot == null) {
                chunksRoot = transform.Find(CHUNKS_ROOT);
                if (chunksRoot != null) {
                    DestroyImmediate(chunksRoot.gameObject);
                }
                do {
                    chunksRoot = worldRoot.Find(CHUNKS_ROOT);
                    if (chunksRoot != null) {
                        DestroyImmediate(chunksRoot.gameObject);
                    }
                } while (chunksRoot != null);
            }
            if (chunksRoot == null) {
                GameObject cr = new GameObject(CHUNKS_ROOT);
                cr.hideFlags = HideFlags.DontSave;
                chunksRoot = cr.transform;
                chunksRoot.hierarchyCapacity = 20000;
                chunksRoot.SetParent(worldRoot, false);
            }
            chunksRoot.gameObject.SetActive(true);
        }

        /// <summary>
        /// Destroys all chunks and internal engine structures - should be used only during shutdown
        /// </summary>
        public void DisposeAll (bool removeVoxelPlayEnvironment = false) {

            StopGenerationThreads();
            VoxelPlayFarChunksRenderer.Dispose();
            DisposeRenderer();
            DestroyNavMesh();
            TextureTools.Dispose();
            VoxelPlayBehaviour.Dispose();
            VoxelHighlightDispose();
            OriginShiftDispose();

            // Destroy chunks
            if (cachedChunks != null) {
                cachedChunks.Clear();
                cachedChunks = null;
            }
            if (chunksPool != null) {
                foreach (VoxelChunk chunk in chunksPool) {
                    if (chunk == null) continue;
                    if (chunk.mf != null) {
                        Mesh mfMesh = chunk.mf.sharedMesh;
                        Misc.DestroyImmediateAndNullify(ref mfMesh);
                        chunk.mf.sharedMesh = null;
                    }
                    if (chunk.mc != null) {
                        Mesh mcMesh = chunk.mc.sharedMesh;
                        Misc.DestroyImmediateAndNullify(ref mcMesh);
                        chunk.mc.sharedMesh = null;
                    }
                    Mesh navMesh = chunk.navMesh;
                    Misc.DestroyImmediateAndNullify(ref navMesh);
                    chunk.navMesh = null;
                    DestroyImmediate(chunk.gameObject);
                }
                chunksPool = null;
            }
            
            ClearLoadingState();
            chunkRequestLast = -1;
            chunksPoolCurrentIndex = -1;
            chunksPoolLoadIndex = 0;
            chunksPoolFetchNew = true;
            lastChunkFetch = null;

            if (!keepTexturesOnDestroy) {
                DisposeTextures();
            }

            DisposeMicroVoxels();

            if (worldRoot != null) {
                while (worldRoot.childCount > 0) {
                    DestroyImmediate(worldRoot.GetChild(0).gameObject);
                }
            }
            DestroyParticles();

            lastChunkX = int.MaxValue;

            // Clear heightmap
            if (heightMapCache != null) {
                heightMapCache.Clear();
                heightMapCache = null;
            }

            // Clear render queue
            chunkQueueRoot = 0;
            lastChunkDistanceSqr = 0;

            chunkRequests = null;
            linkedChunks = null;
            frustumPlanes = null;
            frustumPlanesNormals = null;
            frustumPlanesDistances = null;
            frustumCorners = null;

            if (octreeRoots != null) {
                octreeRoots.Clear();
                octreeRoots = null;
            }

            if (updatedChunks != null) {
                updatedChunks.Clear();
                updatedChunks = null;
            }

            ClearStats();

            // reset voxel definitions state values
            if (voxelDefinitions != null) {
                for (int k = 0; k < voxelDefinitions.Length; k++) {
                    VoxelDefinition vd = voxelDefinitions[k];
                    if (vd != null) {
                        vd.Reset();
                    }
                }
            }

            treeRequests = null;
            vegetationRequests = null;
            sunLightmapSpreadQueue = null;
            sunLightmapRemovalQueue = null;
            torchLightmapSpreadQueue = null;
            torchLightmapRemovalQueue = null;
            waterFloodSources = null;

            initialized = false;

            if (removeVoxelPlayEnvironment) {
                VoxelPlayUI.Dispose();
                DestroyImmediate(gameObject);
            }

            if (worldRoot != null) DestroyImmediate(worldRoot.gameObject);
            worldRoot = null;
            cloudsRoot = null;
            chunksRoot = null;

            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        void ClearStats () {
            chunksCreated = 0;
            chunksUsed = 0;
            chunksInRenderQueueCount = 0;
            chunksDrawn = 0;
            voxelsCreatedCount = 0;
            treesInCreationQueueCount = 0;
            treesCreated = 0;
            vegetationInCreationQueueCount = 0;
            vegetationCreated = 0;
        }

        #endregion


        #region Chunk generation

        void InitOctrees () {
            if (octreeRoots == null) {
                octreeRoots = new Dictionary<Vector3d, Octree>();
            } else {
                octreeRoots.Clear();
            }
            octreeSize = effectiveChunksXZDistance * CHUNK_SIZE * 2;
            float l2 = Mathf.Log(octreeSize, 2);
            octreeSize = (int)Mathf.Pow(2, Mathf.Ceil(l2));
#if UNITY_EDITOR
            _lastOctreeEffectiveDistance = effectiveChunksXZDistance;
#endif
        }

#if UNITY_EDITOR
        int _lastOctreeEffectiveDistance;
#endif

        void CheckChunksInRange (long endTime) {

            if (cachedChunks == null)
                return;

#if UNITY_EDITOR
            int currentEffective = effectiveChunksXZDistance;
            if (currentEffective != _lastOctreeEffectiveDistance) {
                _lastOctreeEffectiveDistance = currentEffective;
                InitOctrees();
            }
#endif

            FastMath.FloorToInt(currentAnchorPos.x / CHUNK_SIZE, currentAnchorPos.y / CHUNK_SIZE, currentAnchorPos.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            int chunkDeltaX = chunkX - lastChunkX;
            int chunkDeltaY = chunkY - lastChunkY;
            int chunkDeltaZ = chunkZ - lastChunkZ;

            bool deltaXZChanged = chunkDeltaX != 0 || chunkDeltaZ != 0;
            bool deltaXYZChanged = deltaXZChanged || chunkDeltaY != 0;
            bool checkOnlyBorders = !deltaXYZChanged;

            lastChunkX = chunkX;
            lastChunkY = chunkY;
            lastChunkZ = chunkZ;

            if (deltaXYZChanged) {

                if (deltaXZChanged) {
                    TriggerFarChunksUnloadCheck();
                }

                if (!constructorMode) {
                    // Anchor has entered a new chunk
                    if (OnPlayerEnterChunk != null) {
                        OnPlayerEnterChunk();
                    }

                    needDetailGeneratorInvoke = true;

                    instancedRenderer.Refresh();
                }
            }

            // Inform detail generators
            bool useDetailGenerators = worldHasDetailGenerators && enableDetailGenerators;
#if UNITY_EDITOR
            if (renderInEditorDetail == EditorRenderDetail.StandardNoDetailGenerators && !applicationIsPlaying) {
                useDetailGenerators = false;
            }
#endif
            if (needDetailGeneratorInvoke && useDetailGenerators) {
                needDetailGeneratorInvoke = false;
                for (int k = 0; k < world.detailGenerators.Length; k++) {
                    if (world.detailGenerators[k].enabled) {
                        if (world.detailGenerators[k].ExploreArea(currentAnchorPos, checkOnlyBorders, endTime)) {
                            needDetailGeneratorInvoke = true;
                        }
                    }
                }
            }

            // Check new chunks to render
            if (checkOnlyBorders) {
                int chunksXZDistance = effectiveChunksXZDistance;
                int chunksYDistance = effectiveChunksYDistance;
                visible_xmin = chunkX - chunksXZDistance;
                visible_xmax = chunkX + chunksXZDistance;
                visible_zmin = chunkZ - chunksXZDistance;
                visible_zmax = chunkZ + chunksXZDistance;
                visible_ymin = chunkY - chunksYDistance;
                visible_ymax = chunkY + chunksYDistance;
                CheckNewChunksInFrustum(endTime);
            } else {
                visible_xmin = chunkX - forceChunkDistance;
                visible_xmax = chunkX + forceChunkDistance;
                visible_zmin = chunkZ - forceChunkDistance;
                visible_zmax = chunkZ + forceChunkDistance;
                visible_ymin = chunkY - forceChunkDistance;
                visible_ymax = chunkY + forceChunkDistance;
                CheckNewNearChunks();
                shouldCheckChunksInFrustum = true;
            }
        }

        /// <summary>
        /// Check chunks around player position up to forceChunkDistance distance
        /// </summary>
        void CheckNewNearChunks () {
            for (int x = visible_xmin; x <= visible_xmax; x++) {
                int x00 = WORLD_SIZE_DEPTH * WORLD_SIZE_HEIGHT * (x + WORLD_SIZE_WIDTH);
                for (int y = visible_ymin; y <= visible_ymax; y++) {
                    int y00 = WORLD_SIZE_DEPTH * (y + WORLD_SIZE_HEIGHT);
                    int h00 = x00 + y00;
                    for (int z = visible_zmin; z <= visible_zmax; z++) {
                        int hash = h00 + z;
                        if (cachedChunks.TryGetValue(hash, out CachedChunk cachedChunk)) {
                            VoxelChunk chunk = cachedChunk.chunk;
                            if ((object)chunk == null)
                                continue;
                            if (chunk.isPopulated) {
                                // If this chunk has been created but not rendered yet, request it
                                if (chunk.renderState != ChunkRenderState.RenderingComplete || !chunk.mr.enabled) {
                                    if (chunk.inqueue) {
                                        chunk.needsMeshRebuild = true;
                                    } else {
                                        ChunkRequestRefresh(chunk, false, true);
                                    }
                                }
                                continue;
                            }
                        }
                        CreateChunk(hash, x, y, z, false);
                    }
                }
            }
        }

        /// <summary>
        /// Checks remaining voxels inside the frustum to be created
        /// </summary>
        void CheckNewChunksInFrustum (long endTime) {

            if (frustumCorners == null)
                return;

            if (shouldCheckChunksInFrustum) {
                shouldCheckChunksInFrustum = false;

                // Get octree roots around camera pos
                int half = octreeSize / 2;
                double cx = Math.Floor(currentAnchorPos.x / octreeSize) * octreeSize + half;
                double cy = Math.Floor(currentAnchorPos.y / octreeSize) * octreeSize + half;
                double cz = Math.Floor(currentAnchorPos.z / octreeSize) * octreeSize + half;

                chunkRequestLast = -1;

                Vector3d center;
                for (int y = -1; y <= 1; y++) {
                    center.y = cy + y * octreeSize;
                    for (int z = -1; z <= 1; z++) {
                        center.z = cz + z * octreeSize;
                        for (int x = -1; x <= 1; x++) {
                            center.x = cx + x * octreeSize;
                            if (!octreeRoots.TryGetValue(center, out Octree octreeRoot)) {
                                octreeRoot = new Octree(null, center, half);
                                octreeRoots[center] = octreeRoot;
                            }
                            if (!octreeRoot.explored)
                                PushCuboid(octreeRoot);
                        }
                    }
                }
            }

            long elapsed;
            do {
                if (chunkRequestLast < 0)
                    return;
                Octree current = chunkRequests[chunkRequestLast--];
                CheckCuboidVisibility(current);

                elapsed = stopWatch.ElapsedMilliseconds;
            } while (elapsed < endTime);

        }

        void PushCuboid (Octree cuboid) {

            if (chunkRequestLast >= chunkRequests.Length - 1) {
                ShowMessage("Chunk creation buffer exhausted. Delaying creation...");
                return;
            }

            // Compare distances from last cuboid
            if (chunkRequestLast >= 0) {
                Octree prev = chunkRequests[chunkRequestLast];
                double distCuboid = (cuboid.center.x - currentAnchorPos.x) * (cuboid.center.x - currentAnchorPos.x) + (cuboid.center.y - currentAnchorPos.y) * (cuboid.center.y - currentAnchorPos.y);
                double distPrev = (prev.center.x - currentAnchorPos.x) * (prev.center.x - currentAnchorPos.x) + (prev.center.y - currentAnchorPos.y) * (prev.center.y - currentAnchorPos.y);
                if (distPrev < distCuboid) {
                    chunkRequests[chunkRequestLast] = cuboid;
                    cuboid = prev;
                }
            }
            chunkRequests[++chunkRequestLast] = cuboid;
        }

        void CheckCuboidVisibility (Octree cuboid) {

            if (cuboid.exploredChildren >= 8)
                return;

            int effectiveXZDistance = effectiveChunksXZDistance;
            int effectiveYDistance = effectiveChunksYDistance;

            Vector3d cuboidCenter = cuboid.center;
            double dx = cuboidCenter.x - currentAnchorPos.x;
            if (dx < 0)
                dx = -dx; // simple abs()
            dx -= cuboid.extents;
            dx /= CHUNK_SIZE;
            if (dx > effectiveXZDistance) {
                return;
            }
            double dy = cuboidCenter.y - currentAnchorPos.y;
            if (dy < 0)
                dy = -dy;
            dy -= cuboid.extents;
            dy /= CHUNK_SIZE;
            if (dy > effectiveYDistance) {
                return;
            }
            double dz = cuboidCenter.z - currentAnchorPos.z;
            if (dz < 0)
                dz = -dz;
            dz -= cuboid.extents;
            dz /= CHUNK_SIZE;
            if (dz > effectiveXZDistance) {
                return;
            }

            Vector3d cuboidMin, cuboidMax;
            cuboidMin.x = cuboidCenter.x - cuboid.extents;
            cuboidMin.y = cuboidCenter.y - cuboid.extents;
            cuboidMin.z = cuboidCenter.z - cuboid.extents;
            cuboidMax.x = cuboidCenter.x + cuboid.extents;
            cuboidMax.y = cuboidCenter.y + cuboid.extents;
            cuboidMax.z = cuboidCenter.z + cuboid.extents;

            bool inFrustum;
            if (onlyRenderInFrustum) {
                inFrustum = GeometryUtilityNonAlloc.TestPlanesAABB(frustumPlanesNormals, frustumPlanesDistances, ref cuboidMin, ref cuboidMax);
            } else {
                inFrustum = true;
            }
            if (inFrustum) {
                int cuboidSize = cuboid.extents * 2;
                if (cuboidSize > CHUNK_SIZE) {
                    if (cuboid.children == null)
                        cuboid.Explode();
                    for (int k = 0; k < 8; k++) {
                        if (!cuboid.children[k].explored) {
                            PushCuboid(cuboid.children[k]);
                        }
                    }
                    return;
                }

                // cuboid is less than a chunk - use that chunk
                FastMath.FloorToInt(cuboidCenter.x / CHUNK_SIZE, cuboidCenter.y / CHUNK_SIZE, cuboidCenter.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
                int hash = GetChunkHash(chunkX, chunkY, chunkZ);
                if (cachedChunks.TryGetValue(hash, out CachedChunk cachedChunk)) {
                    cachedChunk.octree = cuboid;
                    cuboid.parent.exploredChildren++;
                    cuboid.explored = true;
                    VoxelChunk chunk = cachedChunk.chunk;
                    if ((object)chunk == null)
                        return;
                    if (chunk.isPopulated) {
                        // If this chunk has been created but not rendered yet, request it
                        if (chunk.renderState != ChunkRenderState.RenderingComplete || !chunk.mr.enabled) {
                            if (chunk.inqueue) {
                                chunk.needsMeshRebuild = true;
                            } else {
                                ChunkRequestRefresh(chunk, false, true);
                            }
                        }
                        return;
                    }
                }

                CreateChunk(hash, chunkX, chunkY, chunkZ, false, true);
            }

        }

        readonly static HeightMapInfo nullHeightMapInfo = new HeightMapInfo { height = float.MinValue };


        /// <summary>
        /// Gets heightmap info on a given position from the terrain generator directly (does not use any cached value)
        /// </summary>
        /// <returns>The height map info.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="z">The z coordinate.</param>
        public void GetHeightMapInfo (double x, double z, out int groundLevel, out float moisture, out BiomeDefinition biome) {
            VoxelPlayTerrainGenerator tg = world.terrainGenerator;
            if (!tg.usesHeightAndMoisture) {
                groundLevel = 0;
                moisture = 0;
                biome = null;
                return;
            }
            tg.GetHeightAndMoisture(x, z, out float altitude, out moisture);
            if (altitude > 1f)
                altitude = 1f;
            else if (altitude < 0f)
                altitude = 0f;
            if (moisture > 1f)
                moisture = 1f;
            else if (moisture < 0f)
                moisture = 0f;
            altitude *= tg.maxHeight;
            groundLevel = (int)altitude;
            if (enableBiomeMixing) {
                GetBiomeWeights(altitude, moisture, out biome, out _, out _);
            } else {
                biome = GetBiome(altitude, moisture);
            }
        }


        /// <summary>
        /// Gets heightmap info on a given position. This method only works at runtime. Use GetTerainInfo() in Editor.
        /// </summary>
        /// <param name="x">Any world space x coordinate.</param> 
        /// <param name="z">Any world space z coordinate.</param>
        /// <returns>The height map info.</returns>
        public HeightMapInfo GetHeightMapInfoFast (double x, double z) {
            GetHeightMapInfoFast(x, z, out HeightMapInfo[] heights, out int heightsIndex);
            return heights[heightsIndex];
        }

        /// <summary>
        /// Gets heightmap info for all positions in a chunk
        /// </summary>
        /// <returns>The height map info.</returns>
        /// <param name="x">Any world space x coordinate.</param> 
        /// <param name="z">Any world space z coordinate.</param>
        /// <param name="heights">An array of HeightMapInfo values corresponding to the chunk.</param>
        public void GetHeightMapInfoFast (double x, double z, out HeightMapInfo[] heights) {

            VoxelPlayTerrainGenerator tg = world.terrainGenerator;
            if (!tg.usesHeightAndMoisture) {
                heights = new HeightMapInfo[ONE_Y_ROW];
                return;
            }

            if (heightMapCache.TryGetValue(x, z, out heights, out _, out int chunkXMin, out int chunkZMin)) return;

            int index = 0;
            bool mixing = enableBiomeMixing;
            for (int zz = 0; zz < CHUNK_SIZE; zz++) {
                for (int xx = 0; xx < CHUNK_SIZE; xx++, index++) {
                    tg.GetHeightAndMoisture(chunkXMin + xx, chunkZMin + zz, out float altitude, out float moisture);
                    if (altitude > 1f)
                        altitude = 1f;
                    else if (altitude < 0f)
                        altitude = 0f;
                    if (moisture > 1f)
                        moisture = 1f;
                    else if (moisture < 0f)
                        moisture = 0f;
                    float height = altitude * tg.maxHeight;
                    heights[index].height = height;
                    heights[index].moisture = moisture;
                    if (mixing) {
                        GetBiomeWeights(height, moisture, out BiomeDefinition primary, out BiomeDefinition secondary, out float bw);
                        heights[index].biomeMixingWeight = bw;
                        if (bw > 0 && (object)secondary != null && WorldRand.GetValue(chunkXMin + xx, chunkZMin + zz) < bw) {
                            heights[index].biome = secondary;
                            heights[index].biomeSecondary = primary;
                        } else {
                            heights[index].biome = primary;
                            heights[index].biomeSecondary = secondary;
                        }
                    } else {
                        heights[index].biome = GetBiome(height, moisture);
                    }
                }
            }

        }

        /// <summary>
        /// Gets heightmap info for 16x16 positions in a chunk
        /// </summary>
        /// <returns>The height map info.</returns>
        /// <param name="x">Any world space x coordinate.</param>
        /// <param name="z">Any world space z coordinate.</param>
        /// <param name="heights">An array of HeightMapInfo values.</param>
        /// <param name="heightsIndex">The index of the first HeightMapInfo value in the array.</param>
        public void GetHeightMapInfoFast (double x, double z, out HeightMapInfo[] heights, out int heightsIndex) {

            if (heightMapCache.TryGetValue(x, z, out heights, out heightsIndex, out int chunkXMin, out int chunkZMin)) return;

            VoxelPlayTerrainGenerator tg = world.terrainGenerator;
            int index = 0;
            bool mixing = enableBiomeMixing;
            for (int zz = 0; zz < CHUNK_SIZE; zz++) {
                for (int xx = 0; xx < CHUNK_SIZE; xx++, index++) {
                    tg.GetHeightAndMoisture(chunkXMin + xx, chunkZMin + zz, out float altitude, out float moisture);
                    if (altitude > 1f)
                        altitude = 1f;
                    else if (altitude < 0f)
                        altitude = 0f;
                    if (moisture > 1f)
                        moisture = 1f;
                    else if (moisture < 0f)
                        moisture = 0f;
                    float height = altitude * tg.maxHeight;
                    heights[index].height = height;
                    heights[index].moisture = moisture;
                    if (mixing) {
                        GetBiomeWeights(height, moisture, out BiomeDefinition primary, out BiomeDefinition secondary, out float bw);
                        heights[index].biomeMixingWeight = bw;
                        if (bw > 0 && (object)secondary != null && WorldRand.GetValue(chunkXMin + xx, chunkZMin + zz) < bw) {
                            heights[index].biome = secondary;
                            heights[index].biomeSecondary = primary;
                        } else {
                            heights[index].biome = primary;
                            heights[index].biomeSecondary = secondary;
                        }
                    } else {
                        heights[index].biome = GetBiome(height, moisture);
                    }
                }
            }
        }


        /// <summary>
        /// Gets heightmap info for 16x16 positions in a chunk
        /// </summary>
        /// <returns>The height map info obtained by sampling the current world contents. This alternate method to GetHeightMapInfoFast is slower but more accurate.</returns>
        /// <param name="x">The x coordinate of the left-most position of the chunk.</param>
        /// <param name="z">The z coordinate of the back-most position of the chunk.</param>
        /// <param name="heights">An array of HeightMapInfo values.</param>
        public void GetHeightMapInfoReal (double x, double z, out HeightMapInfo[] heights) {

            GetHeightMapInfoFast(x, z, out heights);
            Vector3d pos = new Vector3d(x, 0, z);
            int index = 0;
            for (int zz = 0; zz < CHUNK_SIZE; zz++) {
                pos.z = z + zz;
                for (int xx = 0; xx < CHUNK_SIZE; xx++, index++) {
                    pos.x = x + xx;
                    heights[index].height = GetHeight(pos, FULL_OPAQUE);
                }
            }
        }

        /// <summary>
        /// Gets heightmap info for 16x16 positions in a chunk using current world contents, allowing a filter of voxel definitions.
        /// </summary>
        /// <param name="x">The x coordinate of the left-most position of the chunk.</param>
        /// <param name="z">The z coordinate of the back-most position of the chunk.</param>
        /// <param name="heights">An array of HeightMapInfo values.</param>
        /// <param name="allowedVoxelDefinitions">If provided, only these voxel type indices will be considered for the height (others are ignored).</param>
        public void GetHeightMapInfoReal (double x, double z, out HeightMapInfo[] heights, HashSet<int> allowedVoxelDefinitions) {

            GetHeightMapInfoFast(x, z, out heights);
            Vector3d pos = new Vector3d(x, 0, z);
            int index = 0;
            for (int zz = 0; zz < CHUNK_SIZE; zz++) {
                pos.z = z + zz;
                for (int xx = 0; xx < CHUNK_SIZE; xx++, index++) {
                    pos.x = x + xx;
                    heights[index].height = GetHeight(pos, FULL_OPAQUE, allowedVoxelDefinitions);
                }
            }
        }

        /// <summary>
        /// Removes cached height data for a chunk given x, z position
        /// </summary>
        /// <param name="x"></param>
        /// <param name="z"></param>
        void ResetHeightMapCache (double x, double z) {
            heightMapCache.Remove(x, z);
        }

        public void InvalidateTerrainCaches (VoxelPlayTerrainGenerator terrainGenerator = null) {
            if (terrainGenerator == null) {
                terrainGenerator = world != null ? world.terrainGenerator : null;
            }
            if (terrainGenerator != null) {
                terrainGenerator.InvalidateRuntimeCaches();
            }
            if (heightMapCache != null) {
                heightMapCache.Clear();
            }
        }


        #endregion

        #region Chunk rendering queue manager

        void ChunkRequestRefresh (List<VoxelChunk> chunks, bool clearLightmap, bool refreshMesh, bool ignoreFrustum = false) {
            int count = chunks.Count;
            for (int k = 0; k < count; k++) {
                ChunkRequestRefresh(chunks[k], clearLightmap, refreshMesh, ignoreFrustum);
            }
        }

        void ChunkRequestRefresh (VoxelChunk chunk, bool clearLightmap, bool refreshMesh, bool ignoreFrustum = false) {

            if ((object)chunk == null)
                return;

            if (refreshMesh) {
                chunk.needsMeshRebuild = true;
            }

            if (ignoreFrustum) {
                chunk.ignoreFrustum = true;
            }

            if (clearLightmap) {
                chunk.ClearLightmap(noLightValue);
            }

            if (chunk.inqueue && chunkQueueRoot > 0) {
                return;
            }

            // Get free linked chunk for rendering
            int newRoot = chunkQueueRoot;
            int linkedChunksLength = linkedChunks.Length;
            for (int k = 1; k < linkedChunksLength; k++) {
                newRoot++;
                if (newRoot >= linkedChunksLength)
                    newRoot = 1;
                if (!linkedChunks[newRoot].used) {
                    chunk.inqueue = true;
                    linkedChunks[newRoot].chunk = chunk;
                    linkedChunks[newRoot].next = chunkQueueRoot;
                    linkedChunks[newRoot].used = true;
                    if (chunkQueueRoot > 0) {
                        linkedChunks[chunkQueueRoot].prev = newRoot;
                    }
                    chunkQueueRoot = newRoot;
                    chunksInRenderQueueCount++;
                    return;
                }
            }

            ShowMessage("Out of space in linkedChunks buffer.");
        }


        void ChunkRequestRefresh (Boundsd bounds, bool clearLightmap, bool refreshMesh, bool ignoreFrustum = false) {
            Vector3d position;
            Vector3d min = bounds.min;
            int xmin, ymin, zmin;
            FastMath.FloorToInt(min.x / CHUNK_SIZE, min.y / CHUNK_SIZE, min.z / CHUNK_SIZE, out xmin, out ymin, out zmin);
            xmin *= CHUNK_SIZE;
            ymin *= CHUNK_SIZE;
            zmin *= CHUNK_SIZE;
            Vector3d max = bounds.max;
            int xmax, ymax, zmax;
            FastMath.FloorToInt(max.x / CHUNK_SIZE, max.y / CHUNK_SIZE, max.z / CHUNK_SIZE, out xmax, out ymax, out zmax);
            xmax *= CHUNK_SIZE;
            ymax *= CHUNK_SIZE;
            zmax *= CHUNK_SIZE;

            for (int y = ymax; y >= ymin; y -= CHUNK_SIZE) {
                position.y = y;
                for (int z = zmin; z <= zmax; z += CHUNK_SIZE) {
                    position.z = z;
                    for (int x = xmin; x <= xmax; x += CHUNK_SIZE) {
                        position.x = x;
                        VoxelChunk chunk;
                        if (GetChunk(position, out chunk, true)) {
                            ChunkRequestRefresh(chunk, clearLightmap, refreshMesh, ignoreFrustum);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Monitors the render queue.
        /// </summary>
        void CheckRenderChunkQueue (long endTime) {

            frustumCheckIteration++;
            int frameCount = Time.frameCount;

            long elapsed;
            do {
                VoxelChunk chunk;
                chunk = PopNearestChunk(frustumCheckIteration, lastChunkDistanceSqr + CHUNK_SIZE * CHUNK_SIZE, forceChunkSqrDistance, out lastChunkDistanceSqr);
                if ((object)chunk == null) {
                    lastChunkDistanceSqr = 0;
                    return;
                }
                chunksInRenderQueueCount--;
                if (chunk.inqueue) {
                    chunk.inqueue = false;
                    if (chunk.needsLightmapRebuild) {
                        ComputeLightmap(chunk);
                        RebuildNeighbours(chunk);
                    }
                    ProcessLightmapUpdates();
                    if (chunk.needsMeshRebuild && chunk.renderingFrame != frameCount) {
                        if (chunk.renderState == ChunkRenderState.Pending) {
                            chunk.renderState = ChunkRenderState.RenderingRequested;
                        }
                        chunk.renderingFrame = frameCount;
                        updatedChunks.Add(chunk);
                    }
                }
                elapsed = stopWatch.ElapsedMilliseconds;
            } while (lastChunkDistanceSqr < forceChunkSqrDistance || elapsed < endTime);
        }

        VoxelChunk PopNearestChunk (int frustumCheckIteration, double acceptedDistanceSqr, float forceChunkSqrDistance, out double chunkDist) {

            chunkDist = 1e8f;
            int nearestNode = 0;
            double minDist = double.MaxValue;
            int node = chunkQueueRoot;
            while (node > 0) {
                VoxelChunk chunk = linkedChunks[node].chunk;
                double cdx = chunk.position.x - currentAnchorPos.x;
                double cdz = chunk.position.z - currentAnchorPos.z;
                double dist = cdx * cdx + cdz * cdz;

                // if chunk is within forced visible distance, return it immediately
                if (dist <= forceChunkSqrDistance) {
                    chunkDist = dist;
                    nearestNode = node;
                    break;
                }

                // otherwise, prioritize by distance and frustum
                if (frustumCheckIteration != chunk.frustumCheckIteration) {
                    chunk.frustumCheckIteration = frustumCheckIteration;
                    Vector3d boundsMin;
                    boundsMin.x = chunk.position.x - CHUNK_HALF_SIZE;
                    boundsMin.y = chunk.position.y - CHUNK_HALF_SIZE;
                    boundsMin.z = chunk.position.z - CHUNK_HALF_SIZE;
                    Vector3d boundsMax;
                    boundsMax.x = chunk.position.x + CHUNK_HALF_SIZE;
                    boundsMax.y = chunk.position.y + CHUNK_HALF_SIZE;
                    boundsMax.z = chunk.position.z + CHUNK_HALF_SIZE;
                    chunk.visibleInFrustum = GeometryUtilityNonAlloc.TestPlanesAABB(frustumPlanesNormals, frustumPlanesDistances, ref boundsMin, ref boundsMax);
                }

                double frontDist = dist;
                if (!chunk.visibleInFrustum && !chunk.ignoreFrustum) {
                    bool inDistance = frontDist <= acceptedDistanceSqr;
                    if (onlyRenderInFrustum && !inDistance) {
                        node = linkedChunks[node].next;
                        continue;
                    }
                    frontDist += 1000000000;
                }
                if (frontDist < minDist) {
                    minDist = frontDist;
                    chunkDist = dist;
                    nearestNode = node;
                    if (frontDist < acceptedDistanceSqr) {
                        break;
                    }
                }
                node = linkedChunks[node].next;
            }
            if (nearestNode > 0) {
                if (nearestNode == chunkQueueRoot) {
                    chunkQueueRoot = linkedChunks[nearestNode].next;
                    linkedChunks[chunkQueueRoot].prev = 0;
                } else {
                    if (linkedChunks[nearestNode].prev > 0) {
                        linkedChunks[linkedChunks[nearestNode].prev].next = linkedChunks[nearestNode].next;
                    }
                    if (linkedChunks[nearestNode].next > 0) {
                        linkedChunks[linkedChunks[nearestNode].next].prev = linkedChunks[nearestNode].prev;
                    }
                }
                linkedChunks[nearestNode].used = false;
                VoxelChunk chunk = linkedChunks[nearestNode].chunk;
                return chunk;
            }
            return null;
        }


        void RemoveFromRenderQueue (VoxelChunk chunk) {
            int node = chunkQueueRoot;
            while (node > 0) {
                if (linkedChunks[node].chunk.poolIndex == chunk.poolIndex) {
                    if (node == chunkQueueRoot) {
                        chunkQueueRoot = linkedChunks[node].next;
                        linkedChunks[chunkQueueRoot].prev = 0;
                    } else {
                        if (linkedChunks[node].prev > 0) {
                            linkedChunks[linkedChunks[node].prev].next = linkedChunks[node].next;
                        }
                        if (linkedChunks[node].next > 0) {
                            linkedChunks[linkedChunks[node].next].prev = linkedChunks[node].prev;
                        }
                    }
                    linkedChunks[node].used = false;
                    return;
                }
                node = linkedChunks[node].next;
            }
        }

        void UpdateChunkRR (VoxelChunk chunk) {
            if ((object)chunk == null)
                return;

            int chunkX, chunkY, chunkZ;
            FastMath.FloorToInt(chunk.position.x / CHUNK_SIZE, chunk.position.y / CHUNK_SIZE, chunk.position.z / CHUNK_SIZE, out chunkX, out chunkY, out chunkZ);

            VoxelChunk neighbour;
            List<VoxelChunk> tempChunks = BufferPool<VoxelChunk>.Get();

            for (int y = -1; y <= 1; y++) {
                for (int z = -1; z <= 1; z++) {
                    for (int x = -1; x <= 1; x++) {
                        if (y != 0 || z != 0 || x != 0) {
                            GetChunkFast(chunkX + x, chunkY + y, chunkZ + z, out neighbour);
                            if ((object)neighbour != null) {
                                tempChunks.Add(neighbour);
                            }
                        }
                    }
                }
            }
            tempChunks.Add(chunk);

            // Clear & update lightmaps in a fast way if possible (if any of the chunks are pending of updates, this will just issue a lightmap refresh instead of processing the lightmap immediately to avoid conflicts with meshing threads)
            int count = tempChunks.Count;
            for (int k = 0; k < count; k++) {
                VoxelChunk theChunk = tempChunks[k];
                if (theChunk.inqueue) {
                    ChunkRequestRefresh(theChunk, true, true);
                } else {
                    tempChunks[k].ClearLightmap(noLightValue);
                }
            }
            for (int k = 0; k < count; k++) {
                VoxelChunk theChunk = tempChunks[k];
                if (!theChunk.inqueue) {
                    ComputeLightmap(tempChunks[k]);
                    updatedChunks.Add(tempChunks[k]);
                }
            }
            BufferPool<VoxelChunk>.Release(tempChunks);
            ProcessLightmapUpdates();
        }

        void UpdateMeshAndNotifyChunkChanges (float endTime) {
            meshingIdle++;
            if (meshingThreads != null) {
                int threadsCount = meshingThreads.Length;
                for (int k = 0; k < threadsCount; k++) {
                    UpdateMeshAndNotifyChunkChanges(meshingThreads[uploadingMeshingThreadIndex], endTime);
                    uploadingMeshingThreadIndex = (uploadingMeshingThreadIndex + 1) % threadsCount;
                    if (stopWatch.ElapsedMilliseconds >= endTime) {
                        return;
                    }
                }
            }
        }

        void UpdateMeshAndNotifyChunkChanges (MeshingThread thread, float endTime) {
            // The background thread generation is working... process whatever is ready to be uploaded to the GPU
            bool finishedUploading = false;
            int meshJobsLength = thread.meshJobs.Length;
            for (int k = 0; k < 100; k++) {
                lock (thread.indicesUpdating) {
                    if (thread.meshJobMeshUploadIndex == thread.meshJobMeshDataGenerationReadyIndex) {
                        finishedUploading = true;
                        break;
                    }

                    ++thread.meshJobMeshUploadIndex;
                    if (thread.meshJobMeshUploadIndex >= meshJobsLength) {
                        thread.meshJobMeshUploadIndex = 0;
                    }
                }

                VoxelChunk chunk = thread.meshJobs[thread.meshJobMeshUploadIndex].chunk;
                chunk.needsMeshRebuild = false;
                UploadMeshData(thread, thread.meshJobMeshUploadIndex);

                meshingIdle = 0;

                if (stopWatch.ElapsedMilliseconds >= endTime) {
                    break;
                }
            }

            if (finishedUploading) {
                int updatedChunksCount = updatedChunks.count;
                if (updatedChunksCount > 0) {
                    // Send marked chunks to background thread generation
                    for (int k = 0; k < updatedChunksCount; k++) {
                        VoxelChunk chunk = updatedChunks.values[k];
                        if (chunk.inqueue) {
                            chunk.needsMeshRebuild = true; // delay mesh update till next frame due to new chunk or nearby chunks lightmap changes or modifications
                        } else {
                            meshingIdle = 0;
                            if (chunk.TryGetComponent(out Transform t)) {
                                t.position = chunk.position;
                            }
                            if (!CreateChunkMeshJob(chunk)) {
                                // can't create more mesh jobs - remove processed requests from the update list and exit
                                updatedChunks.RemoveFirst(k);
                                return;
                            }
                        }
                    }
                    updatedChunks.Clear();
                }
            }
        }

        #endregion

        #region Detail

        void DoDetailWork (long endTime) {
            if (constructorMode)
                return;
            if (worldHasDetailGenerators && enableDetailGenerators) {
                for (int k = 0; k < world.detailGenerators.Length; k++) {
                    if (world.detailGenerators[k].enabled) {
                        world.detailGenerators[k].DoWork(endTime);
                    }
                }
            }
        }


        #endregion
    }



}
