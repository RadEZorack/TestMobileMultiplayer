using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        const string CHUNKS_ROOT = "Chunks Root";
        const string CHUNKS_EXPORT_ROOT = "Exported Chunks";

        // Optimization support
        VoxelChunk lastChunkFetch;
        int lastChunkFetchX, lastChunkFetchY, lastChunkFetchZ;
        readonly object lockLastChunkFetch = new object();

        #region Chunk functions

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        int GetChunkHash (int chunkX, int chunkY, int chunkZ) {
            int x00 = WORLD_SIZE_DEPTH * WORLD_SIZE_HEIGHT * (chunkX + WORLD_SIZE_WIDTH);
            int y00 = WORLD_SIZE_DEPTH * (chunkY + WORLD_SIZE_HEIGHT);
            return x00 + y00 + chunkZ;
        }

        /// <summary>
        /// Gets the chunk if exits or create it if forceCreation is set to true.
        /// </summary>
        /// <returns><c>true</c>, if chunk fast was gotten, <c>false</c> otherwise.</returns>
        /// <param name="chunkX">Chunk x.</param>
        /// <param name="chunkY">Chunk y.</param>
        /// <param name="chunkZ">Chunk z.</param>
        /// <param name="createIfNotAvailable">If set to <c>true</c> force creation if chunk doesn't exist.</param>
		bool GetChunkFast (int chunkX, int chunkY, int chunkZ, out VoxelChunk chunk, bool createIfNotAvailable = false) {
            lock (lockLastChunkFetch) {
                if (lastChunkFetchX == chunkX && lastChunkFetchY == chunkY && lastChunkFetchZ == chunkZ && (object)lastChunkFetch != null) {
                    chunk = lastChunkFetch;
                    return true;
                }
            }
            int hash = GetChunkHash(chunkX, chunkY, chunkZ);
            STAGE = 501;
            bool exists = cachedChunks.TryGetValue(hash, out CachedChunk cachedChunk);
            chunk = exists ? cachedChunk.chunk : null;

            if (createIfNotAvailable) {
                if (!exists) {
                    STAGE = 502;
                    // not yet created, create it
                    chunk = CreateChunk(hash, chunkX, chunkY, chunkZ, createEmptyChunk: false);
                    exists = true;
                }
                if ((object)chunk == null) { // chunk is really empty, create it with empty space
                    STAGE = 503;
                    chunk = CreateChunk(hash, chunkX, chunkY, chunkZ, createEmptyChunk: true);
                }
            }
            STAGE = 0;
            if (exists) {
                lock (lockLastChunkFetch) {
                    lastChunkFetchX = chunkX;
                    lastChunkFetchY = chunkY;
                    lastChunkFetchZ = chunkZ;
                    lastChunkFetch = chunk;
                }
                return (object)chunk != null;
            }
            chunk = null;
            return false;
        }


        bool GetChunkOrCreate (Vector3d position, out VoxelChunk chunk) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int x, out int y, out int z);
            return GetChunkFast(x, y, z, out chunk, createIfNotAvailable: true);
        }


        bool GetChunkOrCreate (int chunkX, int chunkY, int chunkZ, out VoxelChunk chunk) {
            return GetChunkFast(chunkX, chunkY, chunkZ, out chunk, createIfNotAvailable: true);
        }

        VoxelChunk GetChunkIfExists (int hash) {
            if (cachedChunks.TryGetValue(hash, out CachedChunk cachedChunk)) {
                return cachedChunk.chunk;
            }
            return null;
        }


        /// <summary>
        /// Creates the chunk.
        /// </summary>
        /// <returns>The chunk.</returns>
        /// <param name="hash">Hash.</param>
        /// <param name="chunkX">Chunk x.</param>
        /// <param name="chunkY">Chunk y.</param>
        /// <param name="chunkZ">Chunk z.</param>
        /// <param name="createEmptyChunk">If set to <c>true</c> create empty chunk.</param>
        /// <param name="complete">If set to <c>true</c> detail generators will fire as well as OnChunkCreated event. Chunk will be marked as populated and a refresh will be triggered if within view distance.</param>
        VoxelChunk CreateChunk (int hash, int chunkX, int chunkY, int chunkZ, bool createEmptyChunk, bool complete = true) {

            // Try to load chunk dynamically if enabled and we have a saved game loaded
            if (enableDynamicLoad && saveFileSupportsDynamicLoad && !createEmptyChunk) {
                if (TryLoadRegionDynamically(chunkX, chunkZ)) {
                    // if region is loaded, then the requested chunk should have loaded before so return false
                    if (GetChunkFast(chunkX, chunkY, chunkZ, out VoxelChunk savedChunk, createIfNotAvailable: false)) {
                        return savedChunk;
                    }
                }
            }

            VoxelChunk chunk = null;
            bool chunkHasContents = false;

            STAGE = 101;
            Vector3d position;
            position.x = chunkX * CHUNK_SIZE + CHUNK_HALF_SIZE;
            position.y = chunkY * CHUNK_SIZE + CHUNK_HALF_SIZE;
            position.z = chunkZ * CHUNK_SIZE + CHUNK_HALF_SIZE;

            STAGE = 102;
            // Create entry in the dictionary
            if (!cachedChunks.TryGetValue(hash, out CachedChunk cachedChunk)) {
                cachedChunk = new CachedChunk();
                cachedChunks[hash] = cachedChunk;
            }

            STAGE = 103;
            if ((object)cachedChunk.chunk == null) {
                // Fetch a new entry in the chunks pool
                if (chunksPoolFetchNew) {
                    chunksPoolFetchNew = false;
                    FetchNewChunkIndex(position);
                }
                chunk = chunksPool[chunksPoolCurrentIndex];
            } else {
                chunk = cachedChunk.chunk;
            }

            // Paint voxels
            chunk.position = position;

            STAGE = 104;
            if (createEmptyChunk) {
                if (OnChunkBeforeCreate != null) {
                    // allows a external function to fill the contents of this new chunk
                    OnChunkBeforeCreate(position, out chunkHasContents, chunk, out bool isAboveSurface);
                    chunk.isAboveSurface = isAboveSurface;
                } else {
                    chunk.isAboveSurface = CheckIfChunkAboveTerrain(position);
                }
            } else {
                if (world.infinite || (position.x >= world.center.x - world.extents.x && position.x <= world.center.x + world.extents.x && position.y >= world.center.y - world.extents.y && position.y <= world.center.y + world.extents.y && position.z >= world.center.z - world.extents.z && position.z <= world.center.z + world.extents.z)) {
                    if (OnChunkBeforeCreate != null) {
                        // allows a external function to fill the contents of this new chunk
                        OnChunkBeforeCreate(position, out chunkHasContents, chunk, out bool isAboveSurface);
                        chunk.isAboveSurface = isAboveSurface;
                    }
                    if (!chunkHasContents) {
                        if (!chunk.isCloud && world.terrainGenerator != null && world.terrainGenerator.enabled) {
                            chunkHasContents = world.terrainGenerator.PaintChunk(chunk);
                        }
                        chunk.isAboveSurface |= !chunkHasContents;
                    }
                }
            }

            STAGE = 105;

            if (chunkHasContents || createEmptyChunk) {

                chunk.ComputeNeighbours();

                if (effectiveGlobalIllumination) {
                    // Ensure that the chunk lightmap is clear; the chunk lightmap might be computed early if the chunk was obtained using GetChunkUnpopulated method
                    // We ensure it remains cleared and is rebuilt when terrain paints this chunk
                    if (chunk.isDirty) {
                        chunk.isDirty = false;
                        chunk.ClearLightmap(FULL_DARK);
                    }
                    // rebuild lightmap for chunks got with GetChunkUnpopulated (ie. from a saved game file)
                    chunk.needsLightmapRebuild = true;
                } else {
                    // lit chunk if not global illumination
                    chunk.ClearLightmap(FULL_LIGHT);
                }

                chunksPoolFetchNew = true;
                chunksCreated++;

                cachedChunk.chunk = chunk;

                if (complete) {
                    chunk.isPopulated = true;

                    // rebuild lightmap as this chunk is fully populated and has been modified by a detail generator
                    chunk.needsLightmapRebuild = true;

                    // Check for detail generators
                    bool useDetailGenerators = worldHasDetailGenerators && enableDetailGenerators;
#if UNITY_EDITOR
                    if (renderInEditorDetail == EditorRenderDetail.StandardNoDetailGenerators && !applicationIsPlaying) {
                        useDetailGenerators = false;
                    }
#endif
                    if (useDetailGenerators) {
                        bool prevCaptureEvents = captureEvents;
                        captureEvents = false;
                        // detail generators shouldn't trigger events for performance reasons. Also a detail generator works same on all clients in multiplayer environment
                        // so no need to propagate these changes as every client will execute the same logic.
                        int detailGeneratorsCount = world.detailGenerators.Length;
                        for (int d = 0; d < detailGeneratorsCount; d++) {
                            VoxelPlayDetailGenerator gen = world.detailGenerators[d];
                            if (gen.enabled) {
                                if (gen.allowNestedExecutions || !gen.busy) {
                                    if (OnChunkBeforeDetailGeneration != null) {
                                        OnChunkBeforeDetailGeneration(chunk, gen, out bool cancelDetailGeneration);
                                        if (cancelDetailGeneration) continue;
                                    }
                                    gen.busy = true;
                                    gen.AddDetail(chunk);
                                }
                            }
                            gen.busy = false;
                        }
                        captureEvents = prevCaptureEvents;
                    }

                    if (chunkHasContents) {
                        // if chunk is near camera, request a render refresh
                        bool sendRefresh = (chunkX >= visible_xmin && chunkX <= visible_xmax && chunkZ >= visible_zmin && chunkZ <= visible_zmax && chunkY >= visible_ymin && chunkY <= visible_ymax);
                        if (sendRefresh) {
                            ChunkRequestRefresh(chunk, clearLightmap: false, refreshMesh: true);
                        }
                    } else {
                        chunk.renderState = ChunkRenderState.RenderingComplete;
                    }

                    if (OnChunkAfterCreate != null) {
                        OnChunkAfterCreate(chunk);
                    }

                    if ((object)chunk.right != null && chunk.right.missingLeftChunk) {
                        chunk.right.missingLeftChunk = false;
                        ChunkRequestRefresh(chunk.right, clearLightmap: false, refreshMesh: true);
                    }
                    if ((object)chunk.left != null && chunk.left.missingRightChunk) {
                        chunk.left.missingRightChunk = false;
                        ChunkRequestRefresh(chunk.left, clearLightmap: false, refreshMesh: true);
                    }
                    if ((object)chunk.forward != null && chunk.forward.missingBackChunk) {
                        chunk.forward.missingBackChunk = false;
                        ChunkRequestRefresh(chunk.forward, clearLightmap: false, refreshMesh: true);
                    }
                    if ((object)chunk.back != null && chunk.back.missingForwardChunk) {
                        chunk.back.missingForwardChunk = false;
                        ChunkRequestRefresh(chunk.back, clearLightmap: false, refreshMesh: true);
                    }
                    if ((object)chunk.top != null && chunk.top.missingBottomChunk) {
                        chunk.top.missingBottomChunk = false;
                        ChunkRequestRefresh(chunk.top, clearLightmap: false, refreshMesh: true);
                    }
                    if ((object)chunk.bottom != null && chunk.bottom.missingTopChunk) {
                        chunk.bottom.missingTopChunk = false;
                        ChunkRequestRefresh(chunk.bottom, clearLightmap: false, refreshMesh: true);
                    }
                }

                STAGE = 0;
                return chunk;
            }
            chunk.renderState = ChunkRenderState.RenderingComplete;
            STAGE = 0;
            return null;
        }

        bool CheckIfChunkAboveTerrain (Vector3d position) {
            
            position.y += CHUNK_HALF_SIZE - 1;
            
            double xMin = position.x - CHUNK_HALF_SIZE;
            double zMin = position.z - CHUNK_HALF_SIZE;
            GetHeightMapInfoFast(xMin, zMin, out HeightMapInfo[] heights);
            float posYf = (float)position.y;
            float waterLvl = waterLevel;
            for (int i = 0; i < ONE_Y_ROW; i++) {
                float surfaceLevel = heights[i].height;
                if (surfaceLevel < waterLvl) surfaceLevel = waterLvl;
                if (posYf >= surfaceLevel) {
                    return true;
                }
            }
            return false;
        }


        void RefreshNeighbourhood (VoxelChunk chunk, bool forceMeshRefresh = false, bool clearLightMap = true, bool excludeCenterChunk = false, bool ignoreFrustum = false) {
            if ((object)chunk == null)
                return;

            FastMath.FloorToInt(chunk.position.x / CHUNK_SIZE, chunk.position.y / CHUNK_SIZE, chunk.position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);

            for (int y = -1; y <= 1; y++) {
                for (int z = -1; z <= 1; z++) {
                    for (int x = -1; x <= 1; x++) {
                        if (excludeCenterChunk && y == 0 && z == 0 && x == 0) continue;
                        GetChunkFast(chunkX + x, chunkY + y, chunkZ + z, out VoxelChunk neighbour);
                        if ((object)neighbour != null) {
                            ChunkRequestRefresh(neighbour, clearLightMap, forceMeshRefresh, ignoreFrustum);
                        }
                    }
                }
            }
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        void RebuildNeighboursIfNeeded (VoxelChunk chunk, int voxelIndex) {
            int bx = voxelIndex & VOXELINDEX_X_EDGE_BITWISE;
            int bz = voxelIndex & VOXELINDEX_Z_EDGE_BITWISE;
            int by = voxelIndex & VOXELINDEX_Y_EDGE_BITWISE;

            if (bx == 0)
                ChunkRequestRefresh(chunk.left, clearLightmap: false, refreshMesh: true);
            else if (bx == VOXELINDEX_X_EDGE_BITWISE)
                ChunkRequestRefresh(chunk.right, clearLightmap: false, refreshMesh: true);

            if (by == 0)
                ChunkRequestRefresh(chunk.bottom, clearLightmap: false, refreshMesh: true);
            else if (by == VOXELINDEX_Y_EDGE_BITWISE)
                ChunkRequestRefresh(chunk.top, clearLightmap: false, refreshMesh: true);

            if (bz == 0)
                ChunkRequestRefresh(chunk.back, clearLightmap: false, refreshMesh: true);
            else if (bz == VOXELINDEX_Z_EDGE_BITWISE)
                ChunkRequestRefresh(chunk.forward, clearLightmap: false, refreshMesh: true);
        }


        void RebuildNeighbours (VoxelChunk chunk) {

            if ((object)chunk.left != null) {
                ChunkRequestRefresh(chunk.left, clearLightmap: false, refreshMesh: true);
            }
            if ((object)chunk.right != null) {
                ChunkRequestRefresh(chunk.right, clearLightmap: false, refreshMesh: true);
            }
            if ((object)chunk.top != null) {
                ChunkRequestRefresh(chunk.top, clearLightmap: false, refreshMesh: true);
            }
            if ((object)chunk.bottom != null) {
                ChunkRequestRefresh(chunk.bottom, clearLightmap: false, refreshMesh: true);
            }
            if ((object)chunk.forward != null) {
                ChunkRequestRefresh(chunk.forward, clearLightmap: false, refreshMesh: true);
            }
            if ((object)chunk.back != null) {
                ChunkRequestRefresh(chunk.back, clearLightmap: false, refreshMesh: true);
            }
        }


        /// <summary>
        /// Clears a chunk
        /// </summary>
        void ChunkClearFast (VoxelChunk chunk) {
            chunk.ClearVoxels(noLightValue);
        }

        public void ChunksExportAll () {
            if (cachedChunks == null) {
                return;
            }
            GameObject exportRoot = GameObject.Find(CHUNKS_EXPORT_ROOT);
            if (exportRoot != null) {
                DestroyImmediate(exportRoot);
            }
            exportRoot = new GameObject(CHUNKS_EXPORT_ROOT);
            exportRoot.transform.position = Misc.vector3zero;

            ExportGlobalSettings settings = exportRoot.AddComponent<ExportGlobalSettings>();
            settings.lightPosBuffer = Shader.GetGlobalVectorArray(VoxelPlay.GPULighting.VoxelPlayLightManager.ShaderParams.GlobalLightPositionsArray);
            settings.lightColorBuffer = Shader.GetGlobalVectorArray(VoxelPlay.GPULighting.VoxelPlayLightManager.ShaderParams.GlobalLightColorsArray);
            settings.lightCount = Shader.GetGlobalInt(VoxelPlay.GPULighting.VoxelPlayLightManager.ShaderParams.GlobalLightCount);
            settings.emissionIntensity = Shader.GetGlobalFloat(ShaderParams.VPEmissionIntensity);
            settings.skyTint = Shader.GetGlobalColor(ShaderParams.VPSkyTint);
            settings.groundColor = Shader.GetGlobalColor(ShaderParams.VPGroundColor);
            settings.fogTint = Shader.GetGlobalColor(ShaderParams.VPFogTint);
            settings.fogData = Shader.GetGlobalVector(ShaderParams.VPFogData);
            settings.fogAmount = Shader.GetGlobalFloat(ShaderParams.VPFogAmount);
            settings.exposure = Shader.GetGlobalFloat(ShaderParams.VPExposure);
            settings.ambientLight = Shader.GetGlobalFloat(ShaderParams.VPAmbientLight);
            settings.diffuseWrap = Shader.GetGlobalVector(ShaderParams.VPDiffuseWrap);
            settings.daylightShadowAtten = Shader.GetGlobalFloat(ShaderParams.VPDaylightShadowAtten);
            settings.enableFog = Shader.IsKeywordEnabled(SKW_VOXELPLAY_GLOBAL_USE_FOG);

            foreach (KeyValuePair<int, CachedChunk> kv in cachedChunks) {
                if (kv.Value == null)
                    continue;
                VoxelChunk chunk = kv.Value.chunk;
                if ((object)chunk == null)
                    continue;
                if (chunk.mf.sharedMesh != null) {
                    chunk.gameObject.hideFlags = 0;
                    chunk.mf.sharedMesh.hideFlags = 0;
                    if (chunk.mc != null && chunk.mc.sharedMesh != null) {
                        chunk.mc.sharedMesh.hideFlags = 0;
                    }
                    chunk.transform.SetParent(exportRoot.transform, true);

                    // Replace chunk in pool so it's not reused or destroyed
                    if (chunk.poolIndex >= 0 && chunk.poolIndex < chunksPool.Length) {
                        VoxelChunk newChunk = CreateChunkPoolEntry();
                        newChunk.poolIndex = chunk.poolIndex;
                        chunksPool[chunk.poolIndex] = newChunk;
                        chunk.poolIndex = -1;
                    }
                }
            }
            cachedChunks.Clear();

#if UNITY_EDITOR
            // Mark scene as modified
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
#endif

        }

        #endregion

    }



}
