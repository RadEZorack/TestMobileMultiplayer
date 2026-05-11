using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {
        public const byte FULL_OPAQUE = 15;
        public const byte HALF_OPAQUE = 7;
        public const byte FULL_DARK = 0;
        public const byte FULL_LIGHT = 15;

        [NonSerialized]
        public VoxelDefinition[] voxelDefinitions;
        public int voxelDefinitionsCount;

        [NonSerialized]
        public VoxelDefinition currentWaterVoxelDefinition;

        GameObject dynamicVoxelTemplate;
        GameObject defaultVoxelPrefab;

        readonly float[] collapsingOffsets = {
            0, 1, 0,
            -1, 1, -1,
            1, 1, 1,
            -1, 1, 1,
            1, 1, -1,
            1, 1, 0,
            0, 1, 1,
            -1, 1, 0,
            0, 1, -1,
            -1, 1, -1,
            1, 0, 1,
            -1, 0, 1,
            1, 0, -1,
            1, 0, 0,
            0, 0, 1,
            -1, 0, 0,
            0, 0, -1,
            -1, 0, -1
        };

        readonly int[] neighbourIndicesOrientationArrayForward = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        readonly int[] neighbourIndicesOrientationArrayRight = { 2, 5, 8, 1, 4, 7, 0, 3, 6 };
        readonly int[] neighbourIndicesOrientationArrayBack = { 8, 7, 6, 5, 4, 3, 2, 1, 0 };
        readonly int[] neighbourIndicesOrientationArrayLeft = { 6, 3, 0, 7, 4, 1, 8, 5, 2 };


        List<Vector3> tempVertices;
        List<Vector3> tempNormals;
        int[] tempIndices;
        int tempIndicesPos;
        List<Vector4> tempUVs;
        List<Color32> tempColors;

        void VoxelDestroyFast (VoxelChunk chunk, int voxelIndex, bool canSpreadWater) {

            if (captureEvents && OnVoxelBeforeDestroyed != null) {
                bool cancel = false;
                OnVoxelBeforeDestroyed(chunk, voxelIndex, ref cancel);
                if (cancel)
                    return;
            }

            // Ensure there's content on this position
            if (chunk.voxels[voxelIndex].typeIndex <= Voxel.HOLE_TYPE_INDEX)
                return;

            // Fetch voxel properties for replacement and collapse before clearing it
            VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            bool triggerCollapse = vd.triggerCollapse && !constructorMode && !buildMode;
            VoxelDefinition replaceType = vd.replacedBy;

            // Clears light (important: this needs to be called before clearing the voxel)
            // We reverse cast a full light from this position
            chunk.voxels[voxelIndex].torchLight = FULL_LIGHT;
            chunk.voxels[voxelIndex].light = FULL_LIGHT;
            ClearLightmapAtPosition(chunk, voxelIndex);

            // Clear voxel
            chunk.ClearVoxel(voxelIndex, effectiveGlobalIllumination ? (byte)0 : (byte)15);

            // Voxel replacement
            if ((object)replaceType != null) {
                chunk.SetVoxel(voxelIndex, replaceType);
            }

            ChunkRequestRefresh(chunk, false, true);
            SpreadLightmapAroundPosition(chunk, voxelIndex);

            // Force rebuild neighbour meshes if destroyed voxel is on a border
            RebuildNeighboursIfNeeded(chunk, voxelIndex);

            // Events
            RegisterChunkChanges(chunk);
            if (captureEvents && OnVoxelDestroyed != null) {
                OnVoxelDestroyed(chunk, voxelIndex);
            }

            // Check if it was surrounded by water. If it was, add water expander
            Vector3d voxelPosition = GetVoxelPosition(chunk, voxelIndex);
            if (canSpreadWater) {
                MakeSurroundingWaterExpand(voxelPosition);
            }

            // Check if voxels on top can fall
            if (triggerCollapse && world.collapseOnDestroy) {
                VoxelCollapse(voxelPosition, world.collapseAmount, null, world.consolidateDelay, world.destroyOnConsolidate, world.addParticlesOnDestroy, world.playSoundOnDestroy);
            }
        }

        void VoxelDestroyFastSingle (VoxelChunk chunk, int voxelIndex) {
            chunk.ClearVoxel(voxelIndex, effectiveGlobalIllumination ? (byte)0 : (byte)15);
            RegisterChunkChanges(chunk);
        }

        /// <summary>
        /// Puts a voxel in the given position. Takes care of informing neighbour chunks.
        /// </summary>
        /// <returns>Returns the affected chunk and voxel index</returns>
        /// <param name="position">Position.</param>
        /// <param name="placeMicroVoxels">If true and the voxel definition has microvoxels, place microvoxels.</param>
        bool VoxelPlaceFast (Vector3d position, VoxelDefinition voxelType, out VoxelChunk chunk, out int voxelIndex, Color32 tintColor = default, float amount = 1f, int rotation = 0, bool refresh = true, bool placeMicroVoxels = true, MicroVoxels microVoxels = null, bool slabMode = false) {

            if (tintColor.r == 0 && tintColor.g == 0 && tintColor.b == 0 && tintColor.a == 0) {
                tintColor = Misc.color32White;
            }
            if (!GetVoxelIndex(position, out chunk, out voxelIndex)) {
                return false;
            }

            if (!_bypassPlacementAnim && captureEvents && OnVoxelBeforePlace != null) {
                OnVoxelBeforePlace(position, chunk, voxelIndex, ref voxelType, ref tintColor);
                if (voxelType == null)
                    return false;
            }

            // Slab?
            if (slabMode) {
                double fy = position.y - Math.Floor(position.y);
                return MicroVoxelPlaceSlab(position, voxelType, tintColor, rotation, topHalf: fy > 0.5, refresh: refresh);
            }

            if (refresh) {
                // Clear lighting
                ClearLightmapAtPosition(chunk, voxelIndex);
            }

            // If it's water, add flood
            if (voxelType.spreads && canSpreadWater) {
                if (voxelType.renderType != RenderType.Water || chunk.voxels[voxelIndex].typeIndex <= Voxel.HOLE_TYPE_INDEX || voxelType.spreadReplaceThreshold > chunk.voxels[voxelIndex].opaque) {
                    chunk.SetVoxel(voxelIndex, voxelType, tintColor);
                }
                chunk.voxels[voxelIndex].SetWaterLevel((Mathf.CeilToInt(amount * voxelType.height)));
                AddWaterFloodInt(ref position, voxelType);
            } else {
                // Set voxel type preserving water level
                chunk.voxels[voxelIndex].Set(voxelType, tintColor);
            }

            // Apply rotation
            if (voxelType.allowsTextureRotation && voxelType.renderType.supportsTextureRotation()) {
                chunk.voxels[voxelIndex].SetTextureRotation(rotation);
            }

            // Set microvoxels
            if (placeMicroVoxels) {
                if (microVoxels == null) {
                    if (voxelType.usesMicroVoxels) {
                        microVoxels = voxelType.microVoxels;
                    }
                }
                if (microVoxels != null) {
                    chunk.SetMicroVoxels(voxelIndex, microVoxels);
                } else {
                    chunk.ClearMicroVoxels(voxelIndex);
                }
            }

            // Add light source
            if (voxelType.lightIntensity > 0) {
                chunk.AddLightSource(voxelIndex, voxelType.lightIntensity);
                SetTorchLightmap(chunk, voxelIndex, voxelType.lightIntensity);
            }

            if (captureEvents && OnVoxelAfterPlace != null) {
                OnVoxelAfterPlace(position, chunk, voxelIndex);
            }

            // Issue a chunk refresh
            if (refresh) {
                ChunkRequestRefresh(chunk, false, true);
                RebuildNeighboursIfNeeded(chunk, voxelIndex);
            }

            RegisterChunkChanges(chunk);
            return true;
        }


        /// <summary> 
        /// Internal method that puts a voxel in a given position. This method does not inform to neighbours. Only used by non-contiguous structures, like trees or vegetation.
        /// For terrain or large scale buildings, use VoxelPlaceFast.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="voxelType">Voxel type.</param>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        void VoxelSingleSet (Vector3d position, VoxelDefinition voxelType, out VoxelChunk chunk, out int voxelIndex, Color32 tintColor) {
            if (GetVoxelIndex(position, out chunk, out voxelIndex)) {
                if (captureEvents && OnVoxelBeforePlace != null) {
                    OnVoxelBeforePlace(position, chunk, voxelIndex, ref voxelType, ref tintColor);
                    if (voxelType == null)
                        return;
                }
                chunk.voxels[voxelIndex].Set(voxelType, tintColor);
                if (captureEvents && OnVoxelAfterPlace != null) {
                    OnVoxelAfterPlace(position, chunk, voxelIndex);
                }
            }
        }


        /// <summary>
        /// Converts a voxel into dynamic type
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        GameObject VoxelSetDynamic (VoxelChunk chunk, int voxelIndex, bool addRigidbody, float duration, bool destroyOnCollapse = false, bool addParticlesOnDestroy = true, bool playSoundOnDestroy = true) {
            if ((object)chunk == null || chunk.voxels[voxelIndex].isEmpty)
                return null;

            VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            if (!vd.renderType.supportsDynamic()) {
                return null;
            }

            VoxelPlaceholder placeholder = GetVoxelPlaceholder(chunk, voxelIndex, true);
            if (placeholder == null)
                return null;

            // Already dynamic?
            if (vd.isDynamic) return placeholder.gameObject;

            // Add rigid body (only if it's not a custom voxel with a rigidbody already)
            if (vd.renderType == RenderType.Custom && vd.prefabUsesRigidbody) {
                addRigidbody = false;
            }

            if (addRigidbody) {
                Rigidbody rb = placeholder.GetComponentInChildren<Rigidbody>();
                if (rb == null) {
                    // check it has a collider
                    if (!placeholder.GetComponentInChildren<Collider>()) {
                        if (placeholder.modelInstance != null) {
                            MeshCollider collider = placeholder.modelInstance.AddComponent<MeshCollider>();
                            collider.convex = true;
                        }
                    }
                    placeholder.rb = placeholder.gameObject.AddComponent<Rigidbody>();
                }
            }

            // If it's a custom model ignore it as it's already a gameobject
            if (vd.renderType == RenderType.Custom)
                return placeholder.gameObject;

            VoxelDefinition vdDyn = vd.dynamicDefinition;

            if (vdDyn == null) {
                // Setup and save voxel definition
                vd.dynamicDefinition = vdDyn = ScriptableObject.CreateInstance<VoxelDefinition>();
                vdDyn.name = vd.name + " (Dynamic)";
                vdDyn.isDynamic = true;
                vdDyn.doNotSave = true;
                vdDyn.staticDefinition = vd;
                vdDyn.renderType = RenderType.Custom;
                vdDyn.textureIndexBottom = vd.textureIndexBottom;
                vdDyn.textureIndexSide = vd.textureIndexSide;
                vdDyn.textureIndexTop = vd.textureIndexTop;
                vdDyn.textureThumbnailTop = vd.textureThumbnailTop;
                vdDyn.textureThumbnailSide = vd.textureThumbnailSide;
                vdDyn.textureThumbnailBottom = vd.textureThumbnailBottom;
                vdDyn.textureSideIndices = vd.textureSideIndices;
                vdDyn.scale = vd.scale;
                vdDyn.offset = vd.offset;
                vdDyn.offsetRandomRange = vd.offsetRandomRange;
                vdDyn.rotation = vd.rotation;
                vdDyn.rotationRandomY = vd.rotationRandomY;
                vdDyn.sampleColor = vd.sampleColor;
                vdDyn.promotesTo = vd.promotesTo;
                vdDyn.playerDamageDelay = vd.playerDamageDelay;
                vdDyn.playerDamage = vd.playerDamage;
                vdDyn.pickupSound = vd.pickupSound;
                vdDyn.landingSound = vd.landingSound;
                vdDyn.jumpSound = vd.jumpSound;
                vdDyn.impactSound = vd.impactSound;
                vdDyn.footfalls = vd.footfalls;
                vdDyn.destructionSound = vd.destructionSound;
                vdDyn.canBeCollected = vd.canBeCollected;
                vdDyn.dropProbability = vd.dropProbability;
                vdDyn.dropItem = GetItemDefinition(ItemCategory.Voxel, vd);
                vdDyn.dropItemLifeTime = vd.dropItemLifeTime;
                vdDyn.dropItemScale = vd.dropItemScale;
                vdDyn.buildSound = vd.buildSound;
                vdDyn.navigatable = vd.navigatable;
                vdDyn.windAnimation = false;
                vdDyn.textureSideIndices = null;
                vdDyn.isSolid = vd.isSolid;
                if (vd.renderType == RenderType.Custom) {
                    vdDyn.model = vd.prefab;
                }
            }

            // Always recompute dynamic model + opaque to avoid stale cache
            if (vd.renderType != RenderType.Custom) {
                bool hasMicroVoxels = IsMicroVoxelAtPosition(chunk, voxelIndex, out MicroVoxels mvForDynamic);
                if (hasMicroVoxels) {
                    vdDyn.model = MakeDynamicMicroVoxelsFromVoxel(chunk, voxelIndex, mvForDynamic);
                    chunk.ClearMicroVoxels(voxelIndex);
                } else {
                    vdDyn.model = MakeDynamicCubeFromVoxel(chunk, voxelIndex);
                }
            }

            // Ensure texture array indices/material linkage are up-to-date for the dynamic definition
            AddVoxelTextures(vdDyn);

            // Clear any vegetation on top if voxel can be moved (has a rigidbody) to avoid floating grass block
            if (placeholder.rb != null) {
                if (GetVoxelIndex(chunk, voxelIndex, 0, 1, 0, out VoxelChunk topChunk, out int topIndex)) {
                    if (topChunk.voxels[topIndex].hasContent) {
                        VoxelDefinition vdTop = voxelDefinitions[topChunk.voxels[topIndex].typeIndex];
                        if (vdTop.renderType == RenderType.CutoutCross && vdTop.destroyWithVoxelBelow) {
                            VoxelDestroyFast(topChunk, topIndex, true);
                        }
                    }
                }
            }
            Color32 color = chunk.voxels[voxelIndex].color;
            int textureRotation = chunk.voxels[voxelIndex].GetTextureRotation();
            chunk.voxels[voxelIndex].Set(vdDyn, color);
            chunk.voxels[voxelIndex].SetTextureRotation(textureRotation);
            chunk.SetNeedsColliderRebuild();

            if (duration > 0) {
                placeholder.SetAutoCancelDynamic(duration, destroyOnCollapse, addParticlesOnDestroy, playSoundOnDestroy);
            }

            // Refresh neighbours
            RebuildNeighboursIfNeeded(chunk, voxelIndex);

            return placeholder.gameObject;
        }

        /// <summary>
        /// Finds all voxels with "willCollapse" connected to a given position
        /// </summary>
        /// <returns>The crumbly voxel indices.</returns>
        /// <param name="position">Position.</param>
        /// <param name="voxelIndices">Results.</param>
        int GetCrumblyVoxelIndices (Vector3d position, int amount, List<VoxelIndex> voxelIndices) {
            voxelIndices.Clear();
            tempVoxelPositions.Clear();
            tempVoxelIndicesCrumbleCount = 0;
            GetCrumblyVoxelRecursive(new Vector3d(position.x, position.y + 1f, position.z), position, amount, voxelIndices);
            return tempVoxelIndicesCrumbleCount;
        }


        void GetCrumblyVoxelRecursive (Vector3d originalPosition, Vector3d position, int amount, List<VoxelIndex> voxelIndices) {
            if (tempVoxelIndicesCrumbleCount >= amount)
                return;
            int c = 0;
            VoxelIndex vi = new VoxelIndex();
            for (int k = 0; k < collapsingOffsets.Length; k += 3) {
                Vector3d pos = position;
                pos.x += collapsingOffsets[k];
                pos.y += collapsingOffsets[k + 1];
                pos.z += collapsingOffsets[k + 2];
                double dx = pos.x > originalPosition.x ? pos.x - originalPosition.x : originalPosition.x - pos.x;
                double dz = pos.z > originalPosition.z ? pos.z - originalPosition.z : originalPosition.z - pos.z;
                if (dx > 8 || dz > 8)
                    continue;
                if (!tempVoxelPositions.TryGetValue(pos, out _)) {
                    tempVoxelPositions[pos] = true;
                    if (GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, false) && chunk.voxels[voxelIndex].typeIndex > Voxel.HOLE_TYPE_INDEX) {
                        VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
                        if (vd.willCollapse && (vd.renderType == RenderType.Custom || chunk.voxels[voxelIndex].opaque >= 3)) {
                            vi.chunk = chunk;
                            vi.voxelIndex = voxelIndex;
                            vi.position = pos;
                            voxelIndices.Add(vi);
                            tempVoxelIndicesCrumbleCount++;
                            c++;
                            if (tempVoxelIndicesCrumbleCount >= amount)
                                break;
                        }
                    }
                }
            }
            int lastCount = tempVoxelIndicesCrumbleCount;
            for (int k = 1; k <= c; k++) {
                GetCrumblyVoxelRecursive(originalPosition, voxelIndices[lastCount - k].position, amount, voxelIndices);
            }
        }

        /// <summary>
        /// Returns the default voxel prefab (usually a cube; the prefab is located in Defaults folder)
        /// </summary>
        /// <returns>The default voxel prefab.</returns>
        GameObject GetDefaultVoxelPrefab () {
            if (defaultVoxelPrefab == null) {
                defaultVoxelPrefab = Resources.Load<GameObject>("VoxelPlay/Defaults/DefaultModel/Cube");
            }
            return defaultVoxelPrefab;
        }


        void InitTempVertices () {
            tempVertices = new List<Vector3>(36);
            tempNormals = new List<Vector3>(36);
            tempUVs = new List<Vector4>(36);
            tempIndices = new int[36];
            tempColors = new List<Color32>(36);

        }


        /// <summary>
        /// Creates a gameobject with geometry and materials based on the triangle renderer but lmited to one voxel
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        GameObject MakeCubeFromVoxel (VoxelChunk chunk, int voxelIndex) {
            VoxelDefinition type = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            Color32 tintColor = chunk.voxels[voxelIndex].color;

            Mesh mesh = null;
            if (type.dynamicMeshes == null) {
                type.dynamicMeshes = new Dictionary<Color, Mesh>();
            } else {
                type.dynamicMeshes.TryGetValue(tintColor, out mesh);
            }

            if (mesh == null) {
                // Create cube mesh procedurally
                tempVertices.Clear();
                tempNormals.Clear();
                tempUVs.Clear();
                tempColors.Clear();
                tempIndicesPos = 0;

                AddFace(Cube.faceVerticesBack, Cube.normalsBack, type.textureIndexSide, tintColor);
                AddFace(Cube.faceVerticesForward, Cube.normalsForward, type.textureIndexForward, tintColor);
                AddFace(Cube.faceVerticesLeft, Cube.normalsLeft, type.textureIndexLeft, tintColor);
                AddFace(Cube.faceVerticesRight, Cube.normalsRight, type.textureIndexRight, tintColor);
                AddFace(Cube.faceVerticesTop, Cube.normalsUp, type.textureIndexTop, tintColor);
                AddFace(Cube.faceVerticesBottom, Cube.normalsDown, type.textureIndexBottom, tintColor);

                mesh = new Mesh();
                mesh.SetVertices(tempVertices);
                mesh.SetUVs(0, tempUVs);
                mesh.SetNormals(tempNormals);
                if (enableTinting) {
                    mesh.SetColors(tempColors);
                }
                mesh.triangles = tempIndices;
                type.dynamicMeshes[tintColor] = mesh;
            }

            GameObject obj = new GameObject("Cube", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.position = GetVoxelPosition(chunk, voxelIndex);

            if (obj.TryGetComponent(out MeshFilter mf)) {
                mf.mesh = mesh;
            }

            if (obj.TryGetComponent(out MeshRenderer mr)) {
                Material mat = null;
                if (type.overrideMaterial) {
                    mat = type.overrideMaterialNonGeo;
                }
                if (mat == null) {
                    if (type.renderType == RenderType.Custom) {
                        mat = GetDynamicVoxelMaterialFromCustom(type);
                    } else {
                        mat = type.renderType == RenderType.Cutout ? matDynamicCutout : matDynamicOpaque;
                    }
                }
                mr.sharedMaterial = mat;
            }

            return obj;
        }

        /// <summary>
        /// Creates a gameobject with geometry and materials based on the triangle renderer but lmited to one voxel
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        GameObject MakeDynamicCubeFromVoxel (VoxelChunk chunk, int voxelIndex) {
            VoxelDefinition type = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            Color32 tintColor = chunk.voxels[voxelIndex].color;

            Mesh mesh = null;
            if (type.dynamicMeshes == null) {
                type.dynamicMeshes = new Dictionary<Color, Mesh>();
            } else {
                type.dynamicMeshes.TryGetValue(tintColor, out mesh);
            }

            if (mesh == null) {
                // Create cube mesh procedurally
                tempVertices.Clear();
                tempNormals.Clear();
                tempUVs.Clear();
                tempColors.Clear();
                tempIndicesPos = 0;

                AddFace(Cube.faceVerticesBack, Cube.normalsBack, type.textureIndexSide, tintColor);
                AddFace(Cube.faceVerticesForward, Cube.normalsForward, type.textureIndexForward, tintColor);
                AddFace(Cube.faceVerticesLeft, Cube.normalsLeft, type.textureIndexLeft, tintColor);
                AddFace(Cube.faceVerticesRight, Cube.normalsRight, type.textureIndexRight, tintColor);
                AddFace(Cube.faceVerticesTop, Cube.normalsUp, type.textureIndexTop, tintColor);
                AddFace(Cube.faceVerticesBottom, Cube.normalsDown, type.textureIndexBottom, tintColor);

                mesh = new Mesh();
                mesh.SetVertices(tempVertices);
                mesh.SetUVs(0, tempUVs);
                mesh.SetNormals(tempNormals);
                if (enableTinting) {
                    mesh.SetColors(tempColors);
                }
                mesh.triangles = tempIndices;
                type.dynamicMeshes[tintColor] = mesh;
            }

            if (dynamicVoxelTemplate == null) {
                dynamicVoxelTemplate = Resources.Load<GameObject>("VoxelPlay/Prefabs/DynamicVoxelTemplate");
            }
            GameObject obj = Instantiate(dynamicVoxelTemplate);
            obj.SetActive(false);

            if (obj.TryGetComponent(out MeshFilter mf)) {
                mf.mesh = mesh;
            }

            if (obj.TryGetComponent(out MeshRenderer mr)) {
                Material mat = null;
                if (type.overrideMaterial) {
                    mat = type.overrideMaterialNonGeo;
                }
                if (mat == null) {
                    if (type.renderType == RenderType.Custom) {
                        mat = GetDynamicVoxelMaterialFromCustom(type);
                    } else {
                        mat = type.renderType == RenderType.Cutout ? matDynamicCutout : matDynamicOpaque;
                    }
                }
                mr.sharedMaterial = mat;
            }

            obj.transform.SetParent(worldRoot, false);

            return obj;
        }

        GameObject MakeDynamicMicroVoxelsFromVoxel (VoxelChunk chunk, int voxelIndex, MicroVoxels mv) {
            VoxelDefinition type = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            int rotationIndex = chunk.voxels[voxelIndex].GetTextureRotation();

            // Ensure prototype exists
            if (mv.prototype == null || mv.needsMeshDataUpdate) {
                mv.needsMeshDataUpdate = false;
                ulong mvHash = mv.GetGridHashCode();
                if (!microVoxelsPrototypes.TryGetValue(mvHash, out mv.prototype)) {
                    var microMesher = new MeshingThreadMicroVoxels();
                    microMesher.UpdateMeshData(mv);
                    microVoxelsPrototypes[mvHash] = mv.prototype;
                }
            }

            // Build mesh via runtime-capable helper to ensure consistent UV.z encoding
            Mesh mesh = type.GetMicroVoxelsMesh(mv, rotationIndex, mv.secondaryType, encodeTextureIndex: true);
            if (mesh == null) {
                return MakeDynamicCubeFromVoxel(chunk, voxelIndex);
            }

            if (dynamicVoxelTemplate == null) {
                dynamicVoxelTemplate = Resources.Load<GameObject>("VoxelPlay/Prefabs/DynamicVoxelTemplate");
            }
            GameObject obj = Instantiate(dynamicVoxelTemplate);
            obj.SetActive(false);

            if (obj.TryGetComponent(out MeshFilter mf)) {
                mf.mesh = mesh;
            }

            if (obj.TryGetComponent(out MeshRenderer mr)) {
                Material mat = null;
                if (type.overrideMaterial) {
                    mat = type.overrideMaterialNonGeo;
                }
                if (mat == null) {
                    mat = type.renderType == RenderType.Cutout ? matDynamicCutout : matDynamicOpaque;
                }
                mr.sharedMaterial = mat;
            }

            obj.transform.SetParent(worldRoot, false);
            return obj;
        }



        Material GetDynamicVoxelMaterialFromCustom (VoxelDefinition template) {
            Material refMat = template.material;
            if (refMat == null) return null;
            bool useCutout = template.renderType == RenderType.Cutout;
            return GetDynamicVoxelMaterialFromCustom(refMat, useCutout);
        }

        Material GetDynamicVoxelMaterialFromCustom (Material refMat, bool useCutout) {
            Texture bumpMap = null;
            if (refMat.HasProperty(ShaderParams.BumpMap)) {
                bumpMap = refMat.GetTexture(ShaderParams.BumpMap);
                if (bumpMap != null && bumpMap.dimension != TextureDimension.Tex2D) bumpMap = null;
            }

            Material instancingMat;
            if (useCutout) {
                instancingMat = matDynamicCutoutNonArray;
            } else if (bumpMap != null) {
                instancingMat = matDynamicBumpMapNonArray;
            } else {
                instancingMat = matDynamicOpaqueNonArray;
            }
            instancingMat = Instantiate(instancingMat);
            instancingMat.DisableKeyword(SKW_VOXELPLAY_GPU_INSTANCING);
            if (refMat != null) {
                if (refMat.HasProperty(ShaderParams.Color) && instancingMat.HasProperty(ShaderParams.Color)) {
                    instancingMat.SetColor(ShaderParams.Color, refMat.GetColor(ShaderParams.Color));
                }
                if (refMat.HasProperty(ShaderParams.MainTex) && instancingMat.HasProperty(ShaderParams.MainTex)) {
                    Texture mainTex = refMat.GetTexture(ShaderParams.MainTex);
                    if (mainTex.dimension == TextureDimension.Tex2D) {
                        instancingMat.SetTexture(ShaderParams.MainTex, mainTex);
                    }
                }
                if (bumpMap != null && instancingMat.HasProperty(ShaderParams.BumpMap)) {
                    instancingMat.SetTexture(ShaderParams.BumpMap, bumpMap);
                }
            }
            return instancingMat;
        }

        void AddFace (Vector3[] faceVertices, Vector3[] normals, int textureIndex, Color32 tintColor) {
            int index = tempVertices.Count;
            tempVertices.AddRange(faceVertices);
            tempNormals.AddRange(normals);

            tempIndices[tempIndicesPos++] = index;
            tempIndices[tempIndicesPos++] = index + 1;
            tempIndices[tempIndicesPos++] = index + 3;
            tempIndices[tempIndicesPos++] = index + 3;
            tempIndices[tempIndicesPos++] = index + 2;
            tempIndices[tempIndicesPos++] = index + 0;

            Vector4 v4 = new Vector4(0, 0, textureIndex, 15f);
            tempUVs.Add(v4);
            v4.y = 1f;
            tempUVs.Add(v4);
            v4.x = 1f;
            v4.y = 0;
            tempUVs.Add(v4);
            v4.y = 1f;
            tempUVs.Add(v4);
            if (enableTinting) {
                tempColors.Add(tintColor);
                tempColors.Add(tintColor);
                tempColors.Add(tintColor);
                tempColors.Add(tintColor);
            }
        }

        /// <summary>
        /// Fetches 27 (9x3) surrounding voxels around a voxel position
        /// </summary>
        void GetVoxelNeighbourhood27 (VoxelChunk chunk, int voxelIndex, ref VoxelIndex[] voxelIndices, int[] neighbourIndicesOrientationArray) {
            const int ARRAY_SIZE = 3;
            const int SIZE_Y_ROW = ARRAY_SIZE * ARRAY_SIZE;
            const int SIZE_Z_ROW = ARRAY_SIZE;

            // fill center voxel
            int index = 1 * SIZE_Y_ROW + 1 * SIZE_Z_ROW + 1;
            voxelIndices[index].chunk = chunk;
            voxelIndices[index].voxelIndex = voxelIndex;

            Vector3 pos = GetVoxelPosition(chunk, voxelIndex);
            Vector3 otherPos;
            for (int offsetY = -1; offsetY <= 1; offsetY++) {
                for (int arrayIndex = 0, offsetZ = -1; offsetZ <= 1; offsetZ++) {
                    for (int offsetX = -1; offsetX <= 1; offsetX++, arrayIndex++) {
                        int entryIndex = neighbourIndicesOrientationArray[arrayIndex] + (offsetY + 1) * 9;
                        if (GetVoxelIndex(chunk, voxelIndex, offsetX, offsetY, offsetZ, out VoxelChunk otherChunk, out int otherVoxelIndex, createChunkIfNotExists: true)) {
                            voxelIndices[entryIndex].chunk = otherChunk;
                            otherPos.x = pos.x + offsetX;
                            otherPos.y = pos.y + offsetY;
                            otherPos.z = pos.z + offsetZ;
                            voxelIndices[entryIndex].position = otherPos;
                        } else {
                            voxelIndices[entryIndex].chunk = null;
                        }
                        voxelIndices[entryIndex].voxelIndex = otherVoxelIndex;
                    }
                }
            }
        }

        /// <summary>
        /// Fetches 9 surrounding voxels around voxel y position
        /// </summary>
        void GetVoxelNeighbourhood9 (VoxelChunk chunk, int voxelIndex, ref VoxelIndex[] voxelIndices, int[] neighbourIndicesOrientationArray) {
            const int ARRAY_SIZE = 3;
            const int SIZE_Z_ROW = ARRAY_SIZE;

            // fill center voxel
            int index = 1 * SIZE_Z_ROW + 1;
            voxelIndices[index].chunk = chunk;
            voxelIndices[index].voxelIndex = voxelIndex;

            Vector3 pos = GetVoxelPosition(chunk, voxelIndex);
            Vector3 otherPos = pos;
            for (int arrayIndex = 0, offsetZ = -1; offsetZ <= 1; offsetZ++) {
                for (int offsetX = -1; offsetX <= 1; offsetX++, arrayIndex++) {
                    int entryIndex = neighbourIndicesOrientationArray[arrayIndex];
                    if (GetVoxelIndex(chunk, voxelIndex, offsetX, 0, offsetZ, out VoxelChunk otherChunk, out int otherVoxelIndex, createChunkIfNotExists: true)) {
                        voxelIndices[entryIndex].chunk = otherChunk;
                        otherPos.x = pos.x + offsetX;
                        otherPos.z = pos.z + offsetZ;
                        voxelIndices[entryIndex].position = otherPos;
                    } else {
                        voxelIndices[entryIndex].chunk = null;
                    }
                    voxelIndices[entryIndex].voxelIndex = otherVoxelIndex;
                }
            }
        }

        /// <summary>
        /// Fetches horizontal surrounding voxels around a position plus top and bottom voxels
        /// </summary>
        void GetVoxelNeighbourhood11 (VoxelChunk chunk, int voxelIndex, ref VoxelIndex[] voxelIndices, int[] neighbourIndicesOrientationArray) {
            GetVoxelNeighbourhood9(chunk, voxelIndex, ref voxelIndices, neighbourIndicesOrientationArray);
            if (GetVoxelIndex(chunk, voxelIndex, 0, 1, 0, out VoxelChunk otherChunk, out int otherVoxelIndex, createChunkIfNotExists: true)) {
                voxelIndices[9].chunk = otherChunk;
                voxelIndices[9].voxelIndex = otherVoxelIndex;
                voxelIndices[9].position = voxelIndices[5].position;
                voxelIndices[9].position.y += 1f;
            }
            if (GetVoxelIndex(chunk, voxelIndex, 0, -1, 0, out otherChunk, out otherVoxelIndex, createChunkIfNotExists: true)) {
                voxelIndices[10].chunk = otherChunk;
                voxelIndices[10].voxelIndex = otherVoxelIndex;
                voxelIndices[10].position = voxelIndices[5].position;
                voxelIndices[10].position.y -= 1f;
            }
        }


        VoxelProperty VoxelGetProperty (VoxelChunk chunk, int voxelIndex, int propertyId) {
            if ((object)chunk == null || chunk.voxelsProperties == null) {
                return default;
            }
            if (!chunk.voxelsProperties.TryGetValue(voxelIndex, out FastHashSet<VoxelProperty> voxelProperties)) {
                return default;
            }
            voxelProperties.TryGetValue(propertyId, out VoxelProperty prop);
            return prop;
        }

    }

}
