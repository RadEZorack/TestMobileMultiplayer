using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelPlay {

    public delegate bool TreeBeforeCreateEvent (Vector3d position, ModelDefinition model);
    public delegate void TreeAfterCreateEvent (Vector3d position, ModelDefinition model, List<VoxelIndex> indices);


    public partial class VoxelPlayEnvironment : MonoBehaviour {

        struct TreeRequest {
            public VoxelChunk chunk;
            public Vector3d chunkOriginalPosition;
            public Vector3d rootPosition;
            public ModelDefinition tree;
        }

        const int TREES_CREATION_BUFFER_SIZE = 20000;

        TreeRequest[] treeRequests;
        int treeRequestLast, treeRequestFirst;
        HashSet<VoxelChunk> treeChunkRefreshRequests;

        void InitTrees () {
            if (treeRequests == null || treeRequests.Length != TREES_CREATION_BUFFER_SIZE) {
                treeRequests = new TreeRequest[TREES_CREATION_BUFFER_SIZE];
            }
            treeRequestLast = -1;
            treeRequestFirst = -1;
            if (treeChunkRefreshRequests == null) {
                treeChunkRefreshRequests = new HashSet<VoxelChunk>();
            } else {
                treeChunkRefreshRequests.Clear();
            }
        }


        public ModelDefinition GetTree (BiomeTree[] trees, float random) {
            float acumProb = 0;
            int index = 0;
            int treesLength = trees.Length;
            for (int t = 0; t < treesLength; t++) {
                acumProb += trees[t].probability;
                if (random < acumProb) {
                    index = t;
                    break;
                }
            }
            return trees[index].tree;
        }

        /// <summary>
        /// Requests the tree creation.
        /// </summary>
        /// <param name="position">Position.</param>
        public void RequestTreeCreation (VoxelChunk chunk, Vector3d position, ModelDefinition treeModel) {
            if (treeModel == null || !enableTrees)
                return;

            treeRequestLast++;
            if (treeRequestLast >= treeRequests.Length) {
                treeRequestLast = 0;
            }
            if (treeRequestLast != treeRequestFirst) {
                treeRequests[treeRequestLast].chunk = chunk;
                treeRequests[treeRequestLast].chunkOriginalPosition = chunk.position;
                treeRequests[treeRequestLast].rootPosition = position;
                treeRequests[treeRequestLast].tree = treeModel; // trees[index].tree;
                treesInCreationQueueCount++;
            } else {
                ShowMessage("New trees request buffer exhausted.");
            }
        }

        /// <summary>
        /// Monitors queue of new trees requests. This function calls CreateTree to create the tree data and pushes a chunk refresh.
        /// </summary>
        void CheckTreeRequests (float endTime) {
            for (int k = 0; k < 10000; k++) {
                if (treeRequestFirst == treeRequestLast)
                    return;
                treeRequestFirst++;
                if (treeRequestFirst >= treeRequests.Length) {
                    treeRequestFirst = 0;
                }
                treesInCreationQueueCount--;
                VoxelChunk chunk = treeRequests[treeRequestFirst].chunk;
                if ((object)chunk != null && chunk.allowTrees && chunk.position == treeRequests[treeRequestFirst].chunkOriginalPosition) {
                    TreePlace(treeRequests[treeRequestFirst].rootPosition, treeRequests[treeRequestFirst].tree);
                    long elapsed = stopWatch.ElapsedMilliseconds;
                    if (elapsed >= endTime)
                        break;
                }
            }
        }

        /// <summary>
        /// Places a tree in the world.
        /// </summary>
        /// <param name="position">The position of the tree.</param>
        /// <param name="tree">The tree model to place.</param>
        /// <param name="modifiedChunks">The list of modified chunks.</param>
        /// <param name="canPlantOnModifiedChunks">Whether to allow planting on modified chunks.</param>
        /// <param name="previewMode">If true, the method won't modify the world but will return the affected chunks in modifiedChunks list (which can't be null).</param>
        public void TreePlace (Vector3d position, ModelDefinition tree, List<VoxelChunk> modifiedChunks = null, bool canPlantOnModifiedChunks = false, bool previewMode = false) {

            if ((object)tree == null) {
                return;
            }

            if (!previewMode) {
                if (OnTreeBeforeCreate != null) {
                    if (!OnTreeBeforeCreate(position, tree)) {
                        return;
                    }
                }
            }

            int rotation = tree.treeRandomRotation ? WorldRand.Range(0, 4, position, 1) : 0;
            Vector3d pos;
            treeChunkRefreshRequests.Clear();
            VoxelChunk lastChunk = null;
            int modelOneYRow = tree.sizeZ * tree.sizeX;
            int modelOneZRow = tree.sizeX;
            int halfSizeX = tree.sizeX / 2;
            int halfSizeZ = tree.sizeZ / 2;

            VoxelIndex index = new VoxelIndex();
            bool informIndices = false;
            List<VoxelIndex> tempVoxelIndices = BufferPool<VoxelIndex>.Get();
            if (OnTreeAfterCreate != null) {
                informIndices = true;
            }

            float rotationDegrees = Voxel.GetTextureRotationDegrees(rotation);
            Vector3 zeroPos = Quaternion.Euler(0, rotationDegrees, 0) * new Vector3(-halfSizeX, 0, -halfSizeZ);

            for (int b = 0; b < tree.bits.Length; b++) {

                int bitIndex = tree.bits[b].voxelIndex;
                int py = bitIndex / modelOneYRow;
                int remy = bitIndex - py * modelOneYRow;
                int pz = remy / modelOneZRow;
                int px = remy - pz * modelOneZRow;
                float wx = zeroPos.x, wz = zeroPos.z;

                // Random rotation
                switch (rotation) {
                    case 1:
                        wx += pz;
                        wz -= px;
                        break;
                    case 2:
                        wx -= px;
                        wz -= pz;
                        break;
                    case 3:
                        wx -= pz;
                        wz += px;
                        break;
                    default:
                        wx += px;
                        wz += pz;
                        break;
                }

                pos.x = position.x + tree.offsetX + wx;
                pos.y = position.y + tree.offsetY + py;
                pos.z = position.z + tree.offsetZ + wz;

                if (GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex)) {
                    // do not generate new trees on saved chunk or positions where an existing solidi voxel exists
                    if ((canPlantOnModifiedChunks || !chunk.modified) && (chunk.voxels[voxelIndex].opaque < FULL_OPAQUE || voxelDefinitions[chunk.voxels[voxelIndex].typeIndex].renderType == RenderType.CutoutCross)) {
                        VoxelDefinition treeVoxel = tree.bits[b].voxelDefinition;
                        if (treeVoxel == null) {
                            treeVoxel = defaultVoxel;
                        }
                        if (!previewMode) {
                            chunk.SetVoxel(voxelIndex, treeVoxel, tree.bits[b].finalColor);
                            if (tree.bits[b].microVoxels != null) {
                                chunk.SetMicroVoxels(voxelIndex, tree.bits[b].microVoxels);
                            }
                        }
                        if (informIndices) {
                            index.chunk = chunk;
                            index.voxelIndex = voxelIndex;
                            index.position = pos;
                            tempVoxelIndices.Add(index);
                        }
                        if (py == tree.yMin) {
                            pos.y--;
                            if (tree.fitToTerrain) {
                                Vector3d under = pos;
                                float terrainAltitude = GetTerrainHeight(under);
                                for (int k = 0; k < 100; k++, under.y--) {
                                    GetVoxelIndex(under, out VoxelChunk bottomChunk, out int vindex);
                                    if (under.y < terrainAltitude || (object)bottomChunk == null || bottomChunk.voxels[vindex].opaque == FULL_OPAQUE) {
                                        break;
                                    }
                                    if (!previewMode) {
                                        bottomChunk.SetVoxel(vindex, treeVoxel, tree.bits[b].finalColor);
                                        if (tree.bits[b].microVoxels != null) {
                                            bottomChunk.SetMicroVoxels(vindex, tree.bits[b].microVoxels);
                                        }
                                    }
                                    if (informIndices) {
                                        index.chunk = bottomChunk;
                                        index.voxelIndex = vindex;
                                        index.position = pos;
                                        tempVoxelIndices.Add(index);
                                    }
                                    treeChunkRefreshRequests.Add(bottomChunk);
                                }
                            } else {
                                // fills one voxel beneath with tree voxel to avoid the issue of having some trees floating on some edges/corners
                                if (voxelIndex >= ONE_Y_ROW) {
                                    if (chunk.voxels[voxelIndex - ONE_Y_ROW].typeIndex <= Voxel.HOLE_TYPE_INDEX) {
                                        if (!previewMode) {
                                            chunk.SetVoxel(voxelIndex - ONE_Y_ROW, treeVoxel, tree.bits[b].finalColor);
                                            if (tree.bits[b].microVoxels != null) {
                                                chunk.SetMicroVoxels(voxelIndex - ONE_Y_ROW, tree.bits[b].microVoxels);
                                            }
                                        }
                                        if (informIndices) {
                                            index.chunk = chunk;
                                            index.voxelIndex = voxelIndex - ONE_Y_ROW;
                                            index.position = pos;
                                            tempVoxelIndices.Add(index);
                                        }
                                    } else {
                                        if (!previewMode) {
                                            if (chunk.ClearMicroVoxels(voxelIndex - ONE_Y_ROW)) {
                                                treeChunkRefreshRequests.Add(chunk);
                                            }
                                        }
                                    }
                                } else {
                                    VoxelChunk bottomChunk = chunk.bottom;
                                    if ((object)bottomChunk != null && !bottomChunk.modified) {
                                        int bottomIndex = voxelIndex + (CHUNK_SIZE - 1) * ONE_Y_ROW;
                                        if (bottomChunk.voxels[bottomIndex].typeIndex <= Voxel.HOLE_TYPE_INDEX) {
                                            if (!previewMode) {
                                                bottomChunk.SetVoxel(bottomIndex, treeVoxel, tree.bits[b].finalColor);
                                                if (tree.bits[b].microVoxels != null) {
                                                    bottomChunk.SetMicroVoxels(bottomIndex, tree.bits[b].microVoxels);
                                                }
                                            }
                                            if (informIndices) {
                                                index.chunk = bottomChunk;
                                                index.voxelIndex = bottomIndex;
                                                index.position = pos;
                                                tempVoxelIndices.Add(index);
                                            }
                                            if (!treeChunkRefreshRequests.Contains(bottomChunk)) {
                                                treeChunkRefreshRequests.Add(bottomChunk);
                                            }
                                        } else {
                                            if (!previewMode) {
                                                if (bottomChunk.ClearMicroVoxels(bottomIndex)) {
                                                    if (!treeChunkRefreshRequests.Contains(bottomChunk)) {
                                                        treeChunkRefreshRequests.Add(bottomChunk);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (chunk != lastChunk) {
                            lastChunk = chunk;
                            if (tree.exclusiveTree) chunk.allowTrees = false;
                            if (!chunk.inqueue) {
                                treeChunkRefreshRequests.Add(chunk);
                            }
                        }
                    }
                }
            }
            treesCreated++;

            if (!previewMode) {
                ModelPlaceTorches(position, tree, rotation);

                if (informIndices) {
                    OnTreeAfterCreate(position, tree, tempVoxelIndices);
                }

                foreach (VoxelChunk chunk in treeChunkRefreshRequests) {
                    ChunkRequestRefresh(chunk, true, true);
                }
            }

            if (modifiedChunks != null) {
                modifiedChunks.AddRange(treeChunkRefreshRequests);
            }

            BufferPool<VoxelIndex>.Release(tempVoxelIndices);

        }

    }



}
