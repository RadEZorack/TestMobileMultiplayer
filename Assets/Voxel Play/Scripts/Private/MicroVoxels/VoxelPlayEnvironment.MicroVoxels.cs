using Math = System.Math;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        internal readonly Dictionary<ulong, MicroVoxelsPrototype> microVoxelsPrototypes = new Dictionary<ulong, MicroVoxelsPrototype>();

        Dictionary<ulong, MicroVoxels> _microVoxelTemplates;

        void InitMicroVoxels () {
            microVoxelsPrototypes.Clear();
        }

        void DisposeMicroVoxels () {
            _microVoxelTemplates = null;
        }

        public void MicroVoxelSetSecondaryType (Vector3d position, VoxelDefinition secondary) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return;
            MicroVoxelSetSecondaryType(chunk, voxelIndex, secondary);
        }

        public void MicroVoxelClearSecondaryType (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return;
            MicroVoxelClearSecondaryType(chunk, voxelIndex);
        }

        public void MicroVoxelSetSecondaryType (VoxelChunk chunk, int voxelIndex, VoxelDefinition secondary) {
            if ((object)chunk == null) return;
            if (chunk.microVoxels != null && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv)) {
                if (mv.secondaryType == secondary) return;
                if (mv.isShared) {
                    mv = mv.Clone();
                    chunk.microVoxels[voxelIndex] = mv;
                }
                mv.secondaryType = secondary;
                ChunkRequestRefresh(chunk, false, true);
                RegisterChunkChanges(chunk);
            }
        }

        public void MicroVoxelClearSecondaryType (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null) return;
            if (chunk.microVoxels != null && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv)) {
                if (mv.secondaryType == null) return;
                mv.secondaryType = null;
                ChunkRequestRefresh(chunk, false, true);
                RegisterChunkChanges(chunk);
            }
        }

        public void MicroVoxelSetLayout (Vector3d position, MicroVoxelLayout layout) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return;
            MicroVoxelSetLayout(chunk, voxelIndex, layout);
        }

        public MicroVoxelLayout MicroVoxelGetLayout (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return MicroVoxelLayout.Default;
            return MicroVoxelGetLayout(chunk, voxelIndex);
        }

        public void MicroVoxelSetLayout (VoxelChunk chunk, int voxelIndex, MicroVoxelLayout layout) {
            if ((object)chunk == null) return;
            if (chunk.microVoxels != null && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv)) {
                if (mv.layout == layout) return;
                if (mv.isShared) {
                    mv = mv.Clone();
                    chunk.microVoxels[voxelIndex] = mv;
                }
                mv.layout = layout;
                ChunkRequestRefresh(chunk, false, true);
                RegisterChunkChanges(chunk);
            }
        }

        public MicroVoxelLayout MicroVoxelGetLayout (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null) return MicroVoxelLayout.Default;
            if (chunk.microVoxels != null && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv)) return mv.layout;
            return MicroVoxelLayout.Default;
        }

        // Helper used by chunk code without introducing circular dependencies
        public static MicroVoxelLayout GetMicroVoxelLayoutForChunkVoxel (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null || chunk.microVoxels == null) return MicroVoxelLayout.Default;
            if (!chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv)) return MicroVoxelLayout.Default;
            return mv.layout;
        }

        void DamageMicroVoxelFast (ref VoxelHitInfo hitInfo, int size, float probability, bool addParticles = true) {
            if (MicroVoxelDestroy(ref hitInfo, size, probability) && addParticles) {
                Vector3 camForward = cameraMain.transform.forward;
                int voxelLight = GetVoxelLightPacked(hitInfo.voxelCenter - camForward);
                AddParticlesAtHitPoint(Random.Range(1, size), camForward, false, hitInfo, voxelLight, 0.1f);
            }
        }

        /// <summary>
        /// Destroys a microvoxel at a given position
        /// </summary>
        public bool MicroVoxelDestroy (Vector3d position) {

            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) return false;

            VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            if (!vd.supportsMicroVoxels) return false;

            // Undo rotation
            int rotationIndex = chunk.voxels[voxelIndex].GetTextureRotation();
            int microVoxelIndex = GetMicroVoxelIndex(position, rotationIndex);

            MicroVoxels mv = GetOrCreateMicroVoxels(chunk, voxelIndex, defaultFilled: true);
            mv.SetUnoccupied(microVoxelIndex);
            if (mv.isEmpty) {
                VoxelDestroyFast(chunk, voxelIndex, false);
                return true;
            }
            ChunkRequestRefresh(chunk, false, true);

            byte newOpaque = mv.GetOpaqueProportional();
            if (newOpaque != chunk.voxels[voxelIndex].opaque) {
                chunk.voxels[voxelIndex].opaque = newOpaque;
                chunk.voxelSignature = -1;
                SpreadLightmapAroundPosition(chunk, voxelIndex);
            }

            RebuildNeighboursIfNeeded(chunk, voxelIndex);

            RegisterChunkChanges(chunk);

            return true;
        }


        /// <summary>
        /// Destroys microvoxels inside a volume defined by a position and size
        /// </summary>
        /// <param name="probability">the probability to remove a microvoxel inside the volume</param>
        /// <returns></returns>
        public bool MicroVoxelDestroy (ref VoxelHitInfo hitInfo, int size, float probability = 1f) {

            size = Mathf.Max(size, 1);
            if (size == 1) {
                return MicroVoxelDestroy(hitInfo.center);
            }

            Boundsd bounds = GetMicroVoxelBounds(ref hitInfo, size);
            Vector3d minBox = bounds.min;
            Vector3d microPos = minBox;

            List<VoxelIndex> updatedVoxels = BufferPool<VoxelIndex>.Get();
            VoxelIndex vi = new VoxelIndex();

            for (int y = 0; y < size; y++) {
                microPos.y = minBox.y + y * MicroVoxels.SIZE;
                for (int z = 0; z < size; z++) {
                    microPos.z = minBox.z + z * MicroVoxels.SIZE;
                    for (int x = 0; x < size; x++) {
                        microPos.x = minBox.x + x * MicroVoxels.SIZE;

                        if (probability < 1f && UnityEngine.Random.value > probability) continue;

                        if (!GetVoxelIndex(microPos, out VoxelChunk chunk, out int voxelIndex, false)) continue;

                        VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
                        if (!vd.supportsMicroVoxels) continue;

                        // Undo rotation
                        int rotationIndex = chunk.voxels[voxelIndex].GetTextureRotation();
                        int microVoxelIndex = GetMicroVoxelIndex(microPos, rotationIndex);

                        MicroVoxels mv = GetOrCreateMicroVoxels(chunk, voxelIndex, defaultFilled: true);
                        if (mv.SetUnoccupied(microVoxelIndex)) {
                            vi.chunk = chunk;
                            vi.voxelIndex = voxelIndex;
                            if (!updatedVoxels.Contains(vi)) {
                                updatedVoxels.Add(vi);
                            }
                            if (mv.isEmpty) {
                                VoxelDestroyFast(chunk, voxelIndex, false);
                            } else {
                                byte newOpaque = mv.GetOpaqueProportional();
                                if (newOpaque != chunk.voxels[voxelIndex].opaque) {
                                    chunk.voxels[voxelIndex].opaque = newOpaque;
                                    chunk.voxelSignature = -1;
                                }
                            }
                        }
                    }
                }
            }

            if (updatedVoxels.Count == 0) {
                BufferPool<VoxelIndex>.Release(updatedVoxels);
                return false;
            }

            foreach (var v in updatedVoxels) {
                ChunkRequestRefresh(v.chunk, false, true);
                SpreadLightmapAroundPosition(v.chunk, v.voxelIndex);

                RebuildNeighboursIfNeeded(v.chunk, v.voxelIndex);

                RegisterChunkChanges(v.chunk);
            }

            BufferPool<VoxelIndex>.Release(updatedVoxels);

            return true;
        }



        /// <summary>
        /// Places a microvoxel on the given position
        /// </summary>
        /// <param name="voxelType">the voxel type to use for the microvoxels in case the position doesn't contain a voxel</param>
        public bool MicroVoxelPlace (Vector3d position, VoxelDefinition voxelType, Color tintColor = default, int rotation = 0) {

            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) return false;

            if (!_bypassPlacementAnim && captureEvents && OnVoxelBeforePlace != null) {
                Color32 tintColor32 = tintColor;
                OnVoxelBeforePlace(position, chunk, voxelIndex, ref voxelType, ref tintColor32);
                tintColor = tintColor32;
                if (voxelType == null) return false;
            }

            // Check if placement animation should be used for single microvoxel
            if (!_bypassPlacementAnim && placementAnimation != PlacementAnimationMode.None && applicationIsPlaying && !serverMode) {
                EnsurePlacementAnimator();
                if (_placementAnimator != null && _placementAnimator.enabled) {
                    var req = new PlacementRequest {
                        position = position,
                        voxelType = voxelType,
                        tintColor = tintColor,
                        amount = 1f,
                        rotation = rotation,
                        refresh = true,
                        placeMicroVoxels = true,
                        microVoxels = null,  // Will be handled as single microvoxel placement
                        slabMode = false,
                        playSound = false,
                        isSingleMicroVoxel = true,  // flag for single microvoxel
                        originalHitPoint = position,  // For direct position placement, use the position as hit point
                        originalVoxelCenter = GetVoxelPosition(position),
                        placementNormalWS = _nextPlacementAnimNormalWS.HasValue ? _nextPlacementAnimNormalWS.Value : Vector3.up
                    };
                    return _placementAnimator.Schedule(req);
                }
            }

            // If the voxel is empty, place a voxel of the given type
            VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            if (chunk.voxels[voxelIndex].isEmpty || vd.isVegetation || vd.isLiquid) {
                if (tintColor == default) {
                    tintColor = Misc.colorWhite;
                }
                int waterLevel = chunk.voxels[voxelIndex].GetWaterLevel();
                bool prevBypass = _bypassPlacementAnim; _bypassPlacementAnim = true;
                VoxelPlace(position, voxelType, playSound: false, tintColor, rotation, refresh: false, placeMicroVoxels: false);
                _bypassPlacementAnim = prevBypass;
                chunk.voxels[voxelIndex].SetWaterLevel(waterLevel);
                vd = voxelType;
            }

            // Check if the voxel supports micro voxels
            if (vd == null || vd.supportsMicroVoxels) {
                // Undo rotation
                rotation = chunk.voxels[voxelIndex].GetTextureRotation();
                int microVoxelIndex = GetMicroVoxelIndex(position, rotation);

                MicroVoxels mv = GetOrCreateMicroVoxels(chunk, voxelIndex, defaultFilled: false);
                mv.SetOccupied(microVoxelIndex);
                chunk.voxelSignature = -1;

                byte prevOpaque = chunk.voxels[voxelIndex].opaque;
                if (mv.isFull) {
                    chunk.ClearMicroVoxels(voxelIndex);
                    chunk.voxels[voxelIndex].opaque = chunk.voxels[voxelIndex].type.opaque;
                    chunk.voxelSignature = -1;
                } else {
                    byte newOpaque = mv.GetOpaqueProportional();
                    if (newOpaque != chunk.voxels[voxelIndex].opaque) {
                        chunk.voxels[voxelIndex].opaque = newOpaque;
                        chunk.voxelSignature = -1;
                    }
                    // Handle proper microvoxel layout when y > size / 2
                    if (microVoxelIndex >= MicroVoxels.COUNT_PER_VOXEL_HALF && voxelType != vd && mv.layout != MicroVoxelLayout.Slabs) {
                        mv.layout = MicroVoxelLayout.Slabs;
                        mv.secondaryType = voxelType;
                    }
                }
                if (prevOpaque != chunk.voxels[voxelIndex].opaque) {
                    ClearLightmapAtPosition(chunk, voxelIndex);
                }
            }

            ChunkRequestRefresh(chunk, false, true);

            RebuildNeighboursIfNeeded(chunk, voxelIndex);

            RegisterChunkChanges(chunk);

            return true;
        }

        /// <summary>
        /// Places a microvoxel on the given position
        /// </summary>
        /// <param name="voxelType">the voxel type to use for the microvoxels in case the position doesn't contain a voxel</param>
        /// <param name="topHalf">if true, the microvoxel will be placed on the top half of the voxel, otherwise it will be placed on the bottom half</param>
        /// <param name="rotation">the rotation of the microvoxel (0,1=90,2=180,3=270)</param>
        /// <param name="topHalf">if true, the microvoxel will be placed on the top half of the voxel, otherwise it will be placed on the bottom half</param>
        /// <param name="replace">if true, the microvoxels will be replaced with the new type, otherwise the microvoxels will be added</param>
        /// <param name="refresh">if true, the chunk will be refreshed</param>
        public bool MicroVoxelPlaceSlab (Vector3d position, VoxelDefinition voxelType, Color tintColor = default, int rotation = 0, bool topHalf = false, bool replace = false, bool refresh = true) {

            if (voxelType == null || !voxelType.supportsMicroVoxels) return false;

            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) return false;

            // If the voxel is empty, place a voxel of the given type
            VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            if (chunk.voxels[voxelIndex].isEmpty || vd.isLiquid) {
                if (tintColor == default) {
                    tintColor = Misc.colorWhite;
                }
                int waterLevel = chunk.voxels[voxelIndex].GetWaterLevel();
                bool prevBypass = _bypassPlacementAnim; _bypassPlacementAnim = true;
                VoxelPlace(position, voxelType, playSound: false, tintColor, rotation, refresh: false, placeMicroVoxels: false);
                _bypassPlacementAnim = prevBypass;
                chunk.voxels[voxelIndex].SetWaterLevel(waterLevel);
                vd = voxelType;
            }

            if (vd == null || vd.supportsMicroVoxels) {
                rotation = chunk.voxels[voxelIndex].GetTextureRotation();
                byte prevOpaque = chunk.voxels[voxelIndex].opaque;
                MicroVoxels mv = null;

                if (topHalf) {
                    if (replace) {
                        if (voxelType == vd) {
                            mv = MicroVoxels.topHalfVoxelTemplate;
                        } else {
                            mv = GetOrCreateMicroVoxels(chunk, voxelIndex, MicroVoxels.topHalfVoxelTemplate);
                            mv.secondaryType = voxelType;
                            mv.layout = MicroVoxelLayout.Slabs;
                        }
                    } else {
                        mv = GetOrCreateMicroVoxels(chunk, voxelIndex, defaultFilled: false);
                        for (int i = MicroVoxels.COUNT_PER_VOXEL_HALF; i < MicroVoxels.COUNT_PER_VOXEL; i++) {
                            mv.SetOccupied(i);
                        }
                        mv.secondaryType = voxelType != vd ? voxelType : null;
                        mv.layout = mv.secondaryType != null ? MicroVoxelLayout.Slabs : MicroVoxelLayout.Default;
                    }
                } else {
                    chunk.voxels[voxelIndex].typeIndex = voxelType.index;
                    if (replace) {
                        mv = voxelType.biomeDirtCounterpart != null ? MicroVoxels.halfSurfaceVoxelTemplate : MicroVoxels.bottomHalfDefaultVoxelTemplate;
                    } else {
                        mv = GetOrCreateMicroVoxels(chunk, voxelIndex, defaultFilled: false);
                        for (int i = 0; i < MicroVoxels.COUNT_PER_VOXEL_HALF; i++) {
                            mv.SetOccupied(i);
                        }
                        mv.layout = voxelType.biomeDirtCounterpart != null ? MicroVoxelLayout.TopCap : MicroVoxelLayout.Default;
                    }
                }

                if (mv == null) return false;

                chunk.SetMicroVoxels(voxelIndex, mv);
                chunk.voxelSignature = -1;
                byte newOpaque = chunk.voxels[voxelIndex].opaque;
                if (prevOpaque != newOpaque && refresh) {
                    ClearLightmapAtPosition(chunk, voxelIndex);
                    SpreadLightmapAroundPosition(chunk, voxelIndex);
                }
            }


            if (captureEvents && OnVoxelAfterPlace != null) {
                OnVoxelAfterPlace(position, chunk, voxelIndex);
            }

            if (refresh) {
                ChunkRequestRefresh(chunk, false, true);

                RebuildNeighboursIfNeeded(chunk, voxelIndex);
            }

            RegisterChunkChanges(chunk);



            return true;
        }


        /// <summary>
        /// Removes a slab (top or bottom) from the given voxel position
        /// </summary>
        /// <param name="position">the position of the voxel to remove the slab from</param>
        /// <param name="topHalf">if true, the top half of the slab will be removed, otherwise the bottom half will be removed</param>
        /// <returns></returns>
        public bool MicroVoxelDestroySlab (Vector3d position, bool topHalf) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) return false;

            Voxel voxel = chunk.voxels[voxelIndex];
            VoxelDefinition vd = voxel.type;
            if (vd == null || !vd.supportsMicroVoxels) return false;

            // Ensure microvoxel data container exists; if absent, start from filled microvoxels to carve out the slab
            MicroVoxels mv = GetOrCreateMicroVoxels(chunk, voxelIndex, defaultFilled: true);

            int startIndex = topHalf ? MicroVoxels.COUNT_PER_VOXEL_HALF : 0;
            int endIndex = topHalf ? MicroVoxels.COUNT_PER_VOXEL : MicroVoxels.COUNT_PER_VOXEL_HALF;
            bool anyChange = false;
            for (int i = startIndex; i < endIndex; i++) {
                if (mv.SetUnoccupied(i)) anyChange = true;
            }

            if (!anyChange) return false;

            chunk.voxelSignature = -1;
            byte prevOpaque = chunk.voxels[voxelIndex].opaque;

            if (mv.isEmpty) {
                VoxelDestroyFast(chunk, voxelIndex, false);
                return true;
            } else {
                if (topHalf) {
                    // Only bottom slab remains
                    mv.secondaryType = null;
                    mv.layout = MicroVoxelLayout.TopCap;
                } else {
                    // Only top slab remains
                    mv.layout = mv.secondaryType != null ? MicroVoxelLayout.Slabs : MicroVoxelLayout.Default;
                }
                byte newOpaque = mv.GetOpaqueProportional();
                if (newOpaque != chunk.voxels[voxelIndex].opaque) {
                    chunk.voxels[voxelIndex].opaque = newOpaque;
                    chunk.voxelSignature = -1;
                }
            }

            if (prevOpaque != chunk.voxels[voxelIndex].opaque) {
                ClearLightmapAtPosition(chunk, voxelIndex);
                SpreadLightmapAroundPosition(chunk, voxelIndex);
            }

            ChunkRequestRefresh(chunk, false, true);
            RebuildNeighboursIfNeeded(chunk, voxelIndex);
            RegisterChunkChanges(chunk);

            return true;
        }

        /// <summary>
        /// Places microvoxels on a volume defined by a position and size
        /// </summary>
        /// <param name="position"></param>
        /// <param name="size">Microvoxels amount per axis</param>
        /// <param name="voxelType">the voxel type to use for the microvoxels in case the position doesn't contain a voxel</param>
        /// <param name="probability">the probability for placing a microvoxel in each position of the volume</param>
        public bool MicroVoxelPlace (ref VoxelHitInfo hitInfo, int size, VoxelDefinition voxelType, float probability = 1f, Color tintColor = default, int rotation = 0) {

            size = Mathf.Max(size, 1);

            if (captureEvents && OnVoxelBeforePlace != null && !_bypassPlacementAnim) {
                Vector3d position = size == 1 ? (hitInfo.voxelCenter + hitInfo.normal * MicroVoxels.SIZE) : (hitInfo.voxelCenter + hitInfo.normal * MicroVoxels.SIZE * size);
                if (GetVoxelIndex(position, out VoxelChunk eventChunk, out int eventVoxelIndex)) {
                    Color32 tintColor32 = tintColor;
                    OnVoxelBeforePlace(position, eventChunk, eventVoxelIndex, ref voxelType, ref tintColor32);
                    tintColor = tintColor32;
                    if (voxelType == null) return false;
                }
            }

            // For microvoxel placement with animation (any size)
            if (!_bypassPlacementAnim && placementAnimation != PlacementAnimationMode.None && applicationIsPlaying && !serverMode) {
                EnsurePlacementAnimator();
                if (_placementAnimator != null && _placementAnimator.enabled) {
                    Vector3d animMicroPos;
                    if (size == 1) {
                        animMicroPos = hitInfo.voxelCenter + hitInfo.normal * MicroVoxels.SIZE;
                    } else {
                        // For multi-microvoxel placement, calculate the center of the placement volume
                        Boundsd animBounds = GetMicroVoxelBounds(ref hitInfo, size);
                        Vector3d animMinBox = animBounds.min;
                        animMinBox += hitInfo.normal * MicroVoxels.SIZE * size;
                        // Center of the placement volume
                        animMicroPos = animMinBox + new Vector3d(size * MicroVoxels.SIZE * 0.5, size * MicroVoxels.SIZE * 0.5, size * MicroVoxels.SIZE * 0.5);
                    }

                    var req = new PlacementRequest {
                        position = animMicroPos,
                        voxelType = voxelType,
                        tintColor = tintColor,
                        amount = 1f,
                        rotation = rotation,
                        refresh = true,
                        placeMicroVoxels = true,
                        microVoxels = null,
                        slabMode = false,
                        playSound = false,
                        isSingleMicroVoxel = size == 1,
                        microVoxelSize = size,
                        originalHitPoint = hitInfo.point,
                        originalVoxelCenter = hitInfo.voxelCenter,
                        placementNormalWS = hitInfo.normal
                    };
                    return _placementAnimator.Schedule(req);
                }
            }

            if (size == 1) {
                return MicroVoxelPlace(hitInfo.voxelCenter + hitInfo.normal * MicroVoxels.SIZE, voxelType, tintColor, rotation);
            }

            if (tintColor == default) {
                tintColor = Misc.colorWhite;
            }

            Boundsd bounds = GetMicroVoxelBounds(ref hitInfo, size);
            Vector3d minBox = bounds.min;
            minBox += hitInfo.normal * MicroVoxels.SIZE * size;
            Vector3d microPos = minBox;

            List<VoxelIndex> updatedVoxels = BufferPool<VoxelIndex>.Get();
            VoxelIndex vi = new VoxelIndex();

            for (int y = 0; y < size; y++) {
                microPos.y = minBox.y + y * MicroVoxels.SIZE;
                for (int z = 0; z < size; z++) {
                    microPos.z = minBox.z + z * MicroVoxels.SIZE;
                    for (int x = 0; x < size; x++) {
                        microPos.x = minBox.x + x * MicroVoxels.SIZE;

                        if (!GetVoxelIndex(microPos, out VoxelChunk chunk, out int voxelIndex)) continue;

                        // If the voxel is empty, place a voxel of the given type
                        VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
                        if (chunk.voxels[voxelIndex].isEmpty || vd.isVegetation || vd.isLiquid) {
                            int waterLevel = chunk.voxels[voxelIndex].GetWaterLevel();
                            bool prevBypass = _bypassPlacementAnim; _bypassPlacementAnim = true;
                            VoxelPlace(microPos, voxelType, playSound: false, tintColor, rotation, refresh: false, placeMicroVoxels: false);
                            _bypassPlacementAnim = prevBypass;
                            chunk.voxels[voxelIndex].SetWaterLevel(waterLevel);
                            vd = voxelType;

                            vi.chunk = chunk;
                            vi.voxelIndex = voxelIndex;
                            if (!updatedVoxels.Contains(vi)) {
                                updatedVoxels.Add(vi);
                            }
                        } else {
                            if (!IsMicroVoxelAtPosition(chunk, voxelIndex)) continue;
                        }

                        if (vd == null || !vd.supportsMicroVoxels) continue;

                        MicroVoxels mv = GetOrCreateMicroVoxels(chunk, voxelIndex, defaultFilled: false);

                        // Undo rotation
                        rotation = chunk.voxels[voxelIndex].GetTextureRotation();
                        int microVoxelIndex = GetMicroVoxelIndex(microPos, rotation);

                        if (mv.SetOccupied(microVoxelIndex)) {

                            vi.chunk = chunk;
                            vi.voxelIndex = voxelIndex;
                            if (!updatedVoxels.Contains(vi)) {
                                updatedVoxels.Add(vi);
                            }

                            if (mv.isFull) {
                                chunk.ClearMicroVoxels(voxelIndex);
                                chunk.voxels[voxelIndex].opaque = chunk.voxels[voxelIndex].type.opaque;
                                chunk.voxelSignature = -1;
                            } else {
                                byte newOpaque = mv.GetOpaqueProportional();
                                if (chunk.voxels[voxelIndex].opaque != newOpaque) {
                                    chunk.voxels[voxelIndex].opaque = newOpaque;
                                    chunk.voxelSignature = -1;
                                }
                                // Handle proper microvoxel layout when y > size / 2
                                if (microVoxelIndex >= MicroVoxels.COUNT_PER_VOXEL_HALF && voxelType != vd && mv.layout != MicroVoxelLayout.Slabs) {
                                    mv.layout = MicroVoxelLayout.Slabs;
                                    mv.secondaryType = voxelType;
                                }

                            }
                        }
                    }
                }
            }

            if (updatedVoxels.Count == 0) {
                BufferPool<VoxelIndex>.Release(updatedVoxels);
                return false;
            }

            foreach (var v in updatedVoxels) {
                v.chunk.voxelSignature = -1;
                ChunkRequestRefresh(v.chunk, false, true);
                ClearLightmapAtPosition(v.chunk, v.voxelIndex);

                RebuildNeighboursIfNeeded(v.chunk, v.voxelIndex);

                RegisterChunkChanges(v.chunk);
            }

            BufferPool<VoxelIndex>.Release(updatedVoxels);

            return true;
        }


        /// <summary>
        /// Gets or create a microvoxel data container in the chunk with empty or filled microvoxels
        /// </summary>
        MicroVoxels GetOrCreateMicroVoxels (VoxelChunk chunk, int voxelIndex, bool defaultFilled = false) {

            if (chunk.microVoxels == null) {
                chunk.microVoxels = new Dictionary<int, MicroVoxels>();
            }
            if (!chunk.usesMicroVoxels) {
                chunk.usesMicroVoxels = true;
                chunk.voxelSignature = -1;
            }

            if (!chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv)) {
                mv = new MicroVoxels();
                chunk.microVoxels[voxelIndex] = mv;
                if (defaultFilled) {
                    mv.Fill();
                }
                return mv;
            }

            if (mv.isShared) {
                mv = mv.Clone();
                chunk.microVoxels[voxelIndex] = mv;
            }

            return mv;
        }



        /// <summary>
        /// Gets of create a microvoxel data container in the chunk with the given microvoxel data
        /// </summary>
        MicroVoxels GetOrCreateMicroVoxels (VoxelChunk chunk, int voxelIndex, MicroVoxels mv) {

            if (chunk.microVoxels == null) {
                chunk.microVoxels = new Dictionary<int, MicroVoxels>();
            }
            if (!chunk.usesMicroVoxels) {
                chunk.usesMicroVoxels = true;
                chunk.voxelSignature = -1;
            }

            if (!chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels existingMv)) {
                MicroVoxels newMv = mv.isShared ? mv : mv.Clone();
                if (mv.isShared) {
                    chunk.microVoxels[voxelIndex] = mv;
                    return mv;
                } else {
                    chunk.microVoxels[voxelIndex] = newMv;
                    return newMv;
                }
            }

            if (existingMv.isShared) {
                existingMv = existingMv.Clone();
                chunk.microVoxels[voxelIndex] = existingMv;
            }
            existingMv.CopyFrom(mv);

            return existingMv;
        }

        /// <summary>
        /// Returns the index inside the voxel that corresponds to the microvoxel at the given world position considering the rotation index of the voxel
        /// </summary>
        int GetMicroVoxelIndex (Vector3d position, int rotation) {
            GetMicroVoxelCoordinates(position, rotation, out int px, out int py, out int pz);
            return px + pz * MicroVoxels.COUNT_PER_AXIS + py * MicroVoxels.COUNT_PER_FACE;
        }


        /// <summary>
        /// Returns the index inside the voxel that corresponds to the microvoxel at the given world position
        /// </summary>
        int GetMicroVoxelIndex (int px, int py, int pz) {
            return px + pz * MicroVoxels.COUNT_PER_AXIS + py * MicroVoxels.COUNT_PER_FACE;
        }

        /// <summary>
        /// Returns the index inside the voxel that corresponds to the microvoxel at the given world position
        /// </summary>
        int GetMicroVoxelIndex (int px, int py, int pz, int rotation = 0) {
            switch (rotation) {
                case 1: // 90 degrees CCW
                    (px, pz) = (MicroVoxels.COUNT_PER_AXIS - 1 - pz, px);
                    break;
                case 2: // 180 degrees
                    px = MicroVoxels.COUNT_PER_AXIS - 1 - px;
                    pz = MicroVoxels.COUNT_PER_AXIS - 1 - pz;
                    break;
                case 3: // 270 degrees CCW
                    (px, pz) = (pz, MicroVoxels.COUNT_PER_AXIS - 1 - px);
                    break;
            }
            int index = px + pz * MicroVoxels.COUNT_PER_AXIS + py * MicroVoxels.COUNT_PER_FACE;
            return index;
        }

        /// <summary>
        /// Returns the coordinates inside the voxel that corresponds to the microvoxel
        /// </summary>
        void GetMicroVoxelCoordinates (int index, out int px, out int py, out int pz) {
            px = index & MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
            py = index / MicroVoxels.COUNT_PER_FACE;
            pz = (index / MicroVoxels.COUNT_PER_AXIS) & MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
        }

        /// <summary>
        /// Returns the coordinates inside the voxel that corresponds to the microvoxel at the given world position
        /// </summary>
        void GetMicroVoxelCoordinates (Vector3d position, int rotation, out int px, out int py, out int pz) {
            Vector3d iposition = position;
            FastVector.Floor(ref iposition);

            double fracX = position.x - iposition.x;
            double fracZ = position.z - iposition.z;
            double fracY = position.y - iposition.y;

            int cellIndexX = (int)(fracX * MicroVoxels.COUNT_PER_AXIS);
            int cellIndexZ = (int)(fracZ * MicroVoxels.COUNT_PER_AXIS);

            switch (rotation) {
                case 1: // 90 degrees CCW
                    (cellIndexX, cellIndexZ) = (MicroVoxels.COUNT_PER_AXIS - 1 - cellIndexZ, cellIndexX);
                    break;
                case 2: // 180 degrees
                    cellIndexX = MicroVoxels.COUNT_PER_AXIS - 1 - cellIndexX;
                    cellIndexZ = MicroVoxels.COUNT_PER_AXIS - 1 - cellIndexZ;
                    break;
                case 3: // 270 degrees CCW
                    (cellIndexX, cellIndexZ) = (cellIndexZ, MicroVoxels.COUNT_PER_AXIS - 1 - cellIndexX);
                    break;
            }

            px = cellIndexX;
            pz = cellIndexZ;
            py = (int)(fracY * MicroVoxels.COUNT_PER_AXIS);
        }

        /// <summary>
        /// Returns the center of the microvoxel at a given voxel position
        /// </summary>
        Vector3d GetMicroVoxelPosition (Vector3d position, int px, int py, int pz) {
            FastVector.Floor(ref position);
            position.x += (px + 0.5f) * MicroVoxels.SIZE;
            position.y += (py + 0.5f) * MicroVoxels.SIZE;
            position.z += (pz + 0.5f) * MicroVoxels.SIZE;
            return position;
        }

        /// <summary>
        /// Returns the center of the microvoxel at a given voxel position
        /// </summary>
        Vector3d GetMicroVoxelPosition (Vector3d position) {
            position.x = (Math.Floor(position.x / MicroVoxels.SIZE) + 0.5) * MicroVoxels.SIZE;
            position.y = (Math.Floor(position.y / MicroVoxels.SIZE) + 0.5) * MicroVoxels.SIZE;
            position.z = (Math.Floor(position.z / MicroVoxels.SIZE) + 0.5) * MicroVoxels.SIZE;
            return position;
        }

        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains microvoxels
        /// </summary>
        public bool IsMicroVoxelAtPosition (ref VoxelHitInfo hitInfo) {
            Vector3d position = hitInfo.voxelCenter + hitInfo.normal * MicroVoxels.SIZE;
            return IsMicroVoxelAtPosition(position);
        }

        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains microvoxels
        /// </summary>
        public bool IsMicroVoxelAtPosition (ref VoxelHitInfo hitInfo, out MicroVoxels mv) {
            return IsMicroVoxelAtPosition(hitInfo.chunk, hitInfo.voxelIndex, out mv);
        }

        /// <summary>
        /// Returns true if the voxel at a position in world space contains microvoxels
        /// </summary>
        public bool IsMicroVoxelAtPosition (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return false;
            return IsMicroVoxelAtPosition(chunk, voxelIndex);
        }
        /// <summary>
        /// Returns true if the voxel at a position in world space contains microvoxels
        /// </summary>
        public bool IsMicroVoxelAtPosition (Vector3d position, out MicroVoxels mv) {
            mv = null;
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return false;
            return IsMicroVoxelAtPosition(chunk, voxelIndex, out mv);
        }
        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains microvoxels
        /// </summary>
        public bool IsMicroVoxelAtPosition (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null) return false;
            if (!chunk.usesMicroVoxels) return false;
            return chunk.microVoxels.ContainsKey(voxelIndex);
        }

        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains microvoxels
        /// </summary>
        public bool IsMicroVoxelAtPosition (VoxelChunk chunk, int voxelIndex, out MicroVoxels mv) {
            mv = null;
            if ((object)chunk == null) return false;
            if (!chunk.usesMicroVoxels) return false;
            return chunk.microVoxels.TryGetValue(voxelIndex, out mv);
        }

        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains a bottom half filled microvoxel
        /// </summary>
        public bool IsBottomHalfAtPosition (VoxelChunk chunk, int voxelIndex) {
            if (!IsMicroVoxelAtPosition(chunk, voxelIndex, out MicroVoxels mv)) return false;
            return mv.IsBottomHalf();
        }

        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains a bottom half filled microvoxel
        /// </summary>
        public bool IsBottomHalfAtPosition (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return false;
            if (!IsMicroVoxelAtPosition(chunk, voxelIndex, out MicroVoxels mv)) return false;
            return mv.IsBottomHalf();
        }

        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains a top half filled microvoxel
        /// </summary>
        public bool IsTopHalfAtPosition (VoxelChunk chunk, int voxelIndex) {
            if (!IsMicroVoxelAtPosition(chunk, voxelIndex, out MicroVoxels mv)) return false;
            return mv.IsTopHalf();
        }

        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains a top half filled microvoxel
        /// </summary>
        public bool IsTopHalfAtPosition (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return false;
            return IsTopHalfAtPosition(chunk, voxelIndex);
        }

        /// <summary>
        /// Returns true if the voxel at a chunk and voxelIndex contains a bottom or top half filled microvoxel
        /// </summary>
        public bool IsHalfVoxelAtPosition (VoxelChunk chunk, int voxelIndex) {
            return IsBottomHalfAtPosition(chunk, voxelIndex) || IsTopHalfAtPosition(chunk, voxelIndex);
        }

        /// <summary>
        /// Returns true if the voxel at a position in world space contains a bottom or top half filled microvoxel
        /// </summary>
        public bool IsHalfVoxelAtPosition (Vector3d position) {
            return IsBottomHalfAtPosition(position) || IsTopHalfAtPosition(position);
        }


        public MicroVoxels GetMicroVoxels (VoxelChunk chunk, int voxelIndex) {
            MicroVoxels mv = null;
            if (chunk != null && chunk.usesMicroVoxels) {
                chunk.microVoxels.TryGetValue(voxelIndex, out mv);
            }
            return mv;
        }

        public MicroVoxels GetMicroVoxels (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) return null;
            return GetMicroVoxels(chunk, voxelIndex);
        }

        // Deduplicate identical microvoxel shapes within a chunk (used after loading v21 saves)
        internal void InternMicroVoxels (VoxelChunk chunk) {
            if ((object)chunk == null || !chunk.usesMicroVoxels || chunk.microVoxels == null || chunk.microVoxels.Count == 0) return;
            var map = new Dictionary<(ulong, int), MicroVoxels>();
            var keys = BufferPool<int>.Get();
            keys.AddRange(chunk.microVoxels.Keys);
            int n = keys.Count;
            for (int i = 0; i < n; i++) {
                int voxelIndex = keys[i];
                if (!chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels mv) || mv == null) continue;
                int sec = mv.secondaryType != null ? mv.secondaryType.index : -1;
                ulong h = mv.GetGridHashCode();
                var key = (h, sec);
                if (!map.TryGetValue(key, out MicroVoxels shared)) {
                    map[key] = mv;
                    mv.isShared = true;
                } else {
                    chunk.microVoxels[voxelIndex] = shared;
                }
                byte opaque = chunk.microVoxels[voxelIndex].GetOpaqueProportional();
                if (chunk.voxels[voxelIndex].opaque != opaque) chunk.voxels[voxelIndex].opaque = opaque;
            }
            BufferPool<int>.Release(keys);
        }

        public Boundsd GetMicroVoxelBounds (ref VoxelHitInfo hitInfo, int microVoxelSize, float padding = 0) {
            Vector3d v1 = hitInfo.center;
            float size = MicroVoxels.SIZE * microVoxelSize;
            Vector3 size3 = new Vector3(size, size, size);
            Vector3d min, max;
            if (microVoxelsSnap) {
                min = v1;
                min.x = Math.Floor(min.x / size) * size;
                min.y = Math.Floor(min.y / size) * size;
                min.z = Math.Floor(min.z / size) * size;
            } else {
                Vector3d midPoint = v1 - hitInfo.normal * (0.5f * MicroVoxels.SIZE * (microVoxelSize - 1));
                min = midPoint - size3 * 0.5f;
            }
            min = GetMicroVoxelPosition(min);
            min -= MicroVoxels.SIZE_3D * 0.5;
            max = min + size3;
            size3.x += padding;
            size3.y += padding;
            size3.z += padding;
            return new Boundsd((min + max) * 0.5, size3);
        }

        /// <summary>
        /// Creates or updates a MicroVoxels object based on the occupancy array.
        /// If shared is true, returns a shared cached template (or builds and caches it) via mv.
        /// If shared is false, fills mv without using cache and marks it non-shared.
        /// </summary>
        public bool CreateMicroVoxels (bool[,,] occupancy, ref MicroVoxels mv, bool shared = true) {
            int size = occupancy.GetLength(0);
            if (size != MicroVoxels.COUNT_PER_AXIS || size != occupancy.GetLength(1) || size != occupancy.GetLength(2)) {
                ShowError("CreateMicroVoxels: invalid occupancy array size: " + size + ". Expected: " + MicroVoxels.COUNT_PER_AXIS);
                return false;
            }
            if (shared) {
                if (_microVoxelTemplates == null) _microVoxelTemplates = new Dictionary<ulong, MicroVoxels>();
                MicroVoxels builder = mv ?? new MicroVoxels();
                builder.Clear();
                for (int y = 0; y < size; y++) {
                    for (int z = 0; z < size; z++) {
                        for (int x = 0; x < size; x++) {
                            if (occupancy[y, z, x]) builder.SetOccupied(x, y, z);
                        }
                    }
                }
                ulong occHash = builder.GetOccupancyHashCode();
                if (_microVoxelTemplates.TryGetValue(occHash, out MicroVoxels cached)) { mv = cached; return true; }
                builder.isShared = true;
                _microVoxelTemplates[occHash] = builder;
                mv = builder;
                return true;
            }

            if (mv == null) mv = new MicroVoxels();
            mv.Clear();
            for (int y = 0; y < size; y++) {
                for (int z = 0; z < size; z++) {
                    for (int x = 0; x < size; x++) {
                        if (occupancy[y, z, x]) mv.SetOccupied(x, y, z);
                    }
                }
            }
            mv.isShared = false;
            return true;

        }

        /// <summary>
		/// Creates (or retrieves from cache) a MicroVoxels vertical span filled between bottomY (inclusive) and topY (inclusive).
        /// If shared is true, uses a dictionary keyed by occupancy hash to cache and reuse identical shapes and assigns a shared instance to mv.
        /// If shared is false, fills mv without using cache and marks it non-shared.
        /// </summary>
		public bool CreateMicroVoxels (int bottomY, int topY, ref MicroVoxels mv, bool shared = true) {
            int n = MicroVoxels.COUNT_PER_AXIS;
            if (bottomY < 0) bottomY = 0;
            if (topY >= n) topY = n - 1;
            if (topY < bottomY) return false;
            if (shared && _microVoxelTemplates == null) _microVoxelTemplates = new Dictionary<ulong, MicroVoxels>();

            // Build shape into a builder
            MicroVoxels builder = (shared ? null : mv);
            if (builder == null || builder.isShared) builder = new MicroVoxels();
            builder.Clear();
            int bitsPerLayer = MicroVoxels.COUNT_PER_FACE;
            for (int y = bottomY; y <= topY; y++) {
                int startBit = y * bitsPerLayer;
                int endBit = startBit + bitsPerLayer - 1;
                while (startBit <= endBit) {
                    int uIndex = startBit >> 6;
                    int bitOffset = startBit & 63;
                    int remainingBitsInWord = 64 - bitOffset;
                    int remainingBitsInLayer = endBit - startBit + 1;
                    int bitsThisWord = remainingBitsInLayer < remainingBitsInWord ? remainingBitsInLayer : remainingBitsInWord;
                    ulong mask = bitsThisWord >= 64 ? ulong.MaxValue : ((1UL << bitsThisWord) - 1UL) << bitOffset;
                    builder.gridData[uIndex] |= mask;
                    startBit += bitsThisWord;
                }
            }
            builder.count = bitsPerLayer * (topY - bottomY + 1);
            builder.layout = MicroVoxelLayout.Default;
            builder.secondaryType = null;
            builder.needsMeshDataUpdate = true;

            if (shared) {
                // Compute occupancy hash and return cached if exists
                ulong occHash = builder.GetOccupancyHashCode();
                if (_microVoxelTemplates.TryGetValue(occHash, out MicroVoxels cached)) { mv = cached; return true; }
                builder.isShared = true;
                _microVoxelTemplates[occHash] = builder;
                mv = builder;
                return true;
            }

            builder.isShared = false;
            mv = builder;
            return true;
        }


    }

}