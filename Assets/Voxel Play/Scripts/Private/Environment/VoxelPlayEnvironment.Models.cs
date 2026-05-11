using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        /// <summary>
        /// Ensures all voxel definitions/textures used in this model are registered
        /// </summary>
        void ModelRegisterVoxelDefinitions (ModelDefinition model) {
            if (model == null || model.bits == null) return;
            int bitsCount = model.bits.Length;
            for (int k = 0; k < bitsCount; k++) {
                VoxelDefinition vd = model.bits[k].voxelDefinition;
                if ((object)vd != null) {
                    AddVoxelTextures(vd);
                }
            }
        }

        IEnumerator ModelPlaceWithDuration (Vector3d position, ModelDefinition model, float buildDuration, int rotation = 0, float colorBrightness = 1f, bool fitTerrain = false, VoxelModelBuildEndEvent callback = null, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered, bool previewMode = false) {

            if (model == null) {
                yield break;
            }

            if (!previewMode && OnModelBuildStart != null) {
                OnModelBuildStart(model, position, out bool cancel);
                if (cancel) yield break;
            }
            int currentIndex = 0;
            int len = model.bits.Length - 1;
            float startTime = Time.time;
            float t = 0;
            WaitForEndOfFrame w = new WaitForEndOfFrame();
            Boundsd bounds = new Boundsd(position, Misc.vector3one);
            List<VoxelIndex> indices = BufferPool<VoxelIndex>.Get();

            while (t < 1f) {
                t = (Time.time - startTime) / buildDuration;
                if (t >= 1f) {
                    t = 1f;
                }
                int lastIndex = (int)(len * t);
                if (lastIndex >= currentIndex) {
                    ModelPlace(position, model, ref bounds, rotation, colorBrightness, fitTerrain, indices, currentIndex, lastIndex, alignment: alignment, previewMode: previewMode);
                    currentIndex = lastIndex + 1;
                }
                yield return w;
            }

            if (!previewMode) {
                ModelPlaceTorches(position, model, rotation, alignment);
                if (callback != null) {
                    callback(model, position);
                }
                if (OnModelBuildEnd != null) {
                    OnModelBuildEnd(model, position);
                }
                if (OnModelBuildEndWithIndices != null) {
                    OnModelBuildEndWithIndices(model, position, indices);
                }
            }
            BufferPool<VoxelIndex>.Release(indices);
        }

        bool ModelPlace (Vector3d position, ModelDefinition model, ref Boundsd bounds, int rotation = 0, float colorBrightness = 1f, bool fitTerrain = false, List<VoxelIndex> indices = null, int indexStart = -1, int indexEnd = -1, bool useUnpopulatedChunks = false, bool refreshChunks = true, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered, bool previewMode = false) {

            if (model == null)
                return false;

            if (indexStart < 0) {
                indexStart = 0;
            }
            if (indexEnd < 0) {
                indexEnd = model.bits.Length - 1;
            }
            if (model.fitToTerrain) {
                fitTerrain = true;
            }

            // ensure all voxel definitions are present
            for (int b = indexStart; b <= indexEnd; b++) {
                VoxelDefinition vd = model.bits[b].voxelDefinition;
                if (vd != null && vd.index == 0) {
                    AddVoxelDefinition(vd);
                }
            }

            bool indicesProvided = indices != null;
            if (indicesProvided && indexStart < 0 && indexEnd < 0) {
                indices.Clear();
            }
            VoxelIndex index = new VoxelIndex();
            Vector3d min = bounds.min;
            Vector3d max = bounds.max;

            List<VoxelChunk> updatedChunks = BufferPool<VoxelChunk>.Get();
            modificationTag++;

            Vector3d pos;
            int modelOneYRow = model.sizeZ * model.sizeX;
            int modelOneZRow = model.sizeX;

            int halfSizeZ, halfSizeX;
            if (alignment == ModelPlacementAlignment.Centered) {
                halfSizeZ = model.sizeZ / 2;
                halfSizeX = model.sizeX / 2;
            } else {
                halfSizeZ = halfSizeX = 0;
            }

            float rotationDegrees = Voxel.GetTextureRotationDegrees(rotation);
            Vector3 zeroPos = Quaternion.Euler(0, rotationDegrees, 0) * new Vector3(-halfSizeX, 0, -halfSizeZ);

            for (int b = indexStart; b <= indexEnd; b++) {
                int bitIndex = model.bits[b].voxelIndex;
                int py = bitIndex / modelOneYRow;
                int remy = bitIndex - py * modelOneYRow;
                int pz = remy / modelOneZRow;
                int px = remy - pz * modelOneZRow;

                float wx = zeroPos.x, wz = zeroPos.z;
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

                pos.x = position.x + model.offsetX + wx;
                pos.y = position.y + model.offsetY + py;
                pos.z = position.z + model.offsetZ + wz;

                if (useUnpopulatedChunks) {
                    GetChunkUnpopulated(pos);
                }
                if (GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex)) {
                    bool emptyVoxel = model.bits[b].isEmpty;
                    if (emptyVoxel) {
                        if (!previewMode) {
                            chunk.voxels[voxelIndex] = Voxel.Hole;
                        }
                        if (indicesProvided) {
                            index.chunk = chunk;
                            index.voxelIndex = voxelIndex;
                            index.position = pos;
                            indices.Add(index);
                        }
                    } else {
                        Color32 color = model.bits[b].finalColor;
                        if (colorBrightness != 1f) {
                            color.r = (byte)(color.r * colorBrightness);
                            color.g = (byte)(color.g * colorBrightness);
                            color.b = (byte)(color.b * colorBrightness);
                        }
                        VoxelDefinition vd = model.bits[b].voxelDefinition;
                        if (vd == null) {
                            vd = defaultVoxel;
                        }
                        if (!previewMode) {
                            chunk.SetVoxel(voxelIndex, vd, color);
                            float modelBitRotation = (model.bits[b].rotation + 360 + rotationDegrees) % 360;
                            if (modelBitRotation != 0) {
                                chunk.voxels[voxelIndex].SetTextureRotation(Voxel.GetTextureRotationFromDegrees(modelBitRotation));
                            }
                            MicroVoxels mv = model.bits[b].microVoxels;
                            if (mv != null) {
                                chunk.SetMicroVoxels(voxelIndex, mv);
                            } else if (vd.usesMicroVoxels) {
                                chunk.SetMicroVoxels(voxelIndex, vd.microVoxels);
                            }
                            // Restore voxel properties if any exist
                            if (model.bits[b].properties != null && model.bits[b].properties.Count > 0) {
                                foreach (var prop in model.bits[b].properties) {
                                    VoxelSetProperty(chunk, voxelIndex, prop.propertyId, new VoxelProperty {
                                        floatValue = prop.floatValue,
                                        stringValue = prop.stringValue
                                    });
                                }
                            }
                        }

                        // Add index
                        if (indicesProvided) {
                            index.chunk = chunk;
                            index.voxelIndex = voxelIndex;
                            index.position = pos;
                            indices.Add(index);
                        }
                        if (pos.x < min.x) {
                            min.x = pos.x;
                        }
                        if (pos.y < min.y) {
                            min.y = pos.y;
                        }
                        if (pos.z < min.z) {
                            min.z = pos.z;
                        }
                        if (pos.x > max.x) {
                            max.x = pos.x;
                        }
                        if (pos.y > max.y) {
                            max.y = pos.y;
                        }
                        if (pos.z > max.z) {
                            max.z = pos.z;
                        }

                        if (fitTerrain) {
                            // Fill beneath row 1
                            if (py == 0) {
                                Vector3d under = pos;
                                under.y -= 1;
                                float terrainAltitude = GetTerrainHeight(under);
                                for (int k = 0; k < 100; k++, under.y--) {
                                    if (under.y < terrainAltitude || !GetVoxelIndex(under, out VoxelChunk lowChunk, out int vindex) || lowChunk.voxels[vindex].opaque == FULL_OPAQUE) break;

                                    if (!previewMode) {
                                        lowChunk.voxels[vindex].Set(vd, color);
                                    }
                                    if (indicesProvided) {
                                        index.chunk = lowChunk;
                                        index.voxelIndex = vindex;
                                        index.position = under;
                                        indices.Add(index);
                                    }

                                    if (previewMode || lowChunk.SetModified(modificationTag)) {
                                        updatedChunks.Add(lowChunk);
                                    }

                                    if (!previewMode && !lowChunk.inqueue && !useUnpopulatedChunks) {
                                        ChunkRequestRefresh(lowChunk, true, true);
                                    }
                                }
                            }
                        }
                    }

                    // Prevent tree population
                    chunk.allowTrees = false;

                    // Stamp change
                    if (previewMode || chunk.SetModified(modificationTag)) {
                        updatedChunks.Add(chunk);
                    }

                    if (useUnpopulatedChunks) {
                        chunk.isDirty = true;
                    }

                    if (!previewMode && !chunk.inqueue && refreshChunks) {
                        ChunkRequestRefresh(chunk, true, true);
                    }

                    // Transfer properties from ModelBit to the placed voxel
                    if (!previewMode) {
                        var bit = model.bits[b];
                        if (bit.properties != null) {
                            foreach (var prop in bit.properties) {
                                VoxelSetProperty(chunk, voxelIndex, prop.propertyId, new VoxelProperty {
                                    stringValue = prop.stringValue,
                                    floatValue = prop.floatValue
                                });
                            }
                        }
                    }
                }
            }
            RegisterChunkChanges(updatedChunks);
            BufferPool<VoxelChunk>.Release(updatedChunks);

            FastVector.Floor(ref min);
            FastVector.Ceiling(ref max);
            bounds.SetMinMax(min, max);

            return true;
        }


        void ModelPlaceTorches (Vector3d position, ModelDefinition model, int rotation = 0, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered) {

            if (model == null || model.torches == null)
                return;
            FastVector.Floor(ref position);
            Vector3d pos;
            int modelOneYRow = model.sizeZ * model.sizeX;
            int modelOneZRow = model.sizeX;
            int halfSizeZ, halfSizeX;
            if (alignment == ModelPlacementAlignment.Centered) {
                halfSizeZ = model.sizeZ / 2;
                halfSizeX = model.sizeX / 2;
            } else {
                halfSizeZ = halfSizeX = 0;
            }

            float rotationDegrees = Voxel.GetTextureRotationDegrees(rotation);
            Vector3 zeroPos = Quaternion.Euler(0, rotationDegrees, 0) * new Vector3(-halfSizeX, 0, -halfSizeZ);

            // ensure all voxel definitions are present
            int tmp;
            for (int b = 0; b < model.torches.Length; b++) {
                int bitIndex = model.torches[b].voxelIndex;
                int py = bitIndex / modelOneYRow;
                int remy = bitIndex - py * modelOneYRow;
                int pz = remy / modelOneZRow;
                int px = remy - pz * modelOneZRow;
                Vector3 normal = model.torches[b].normal;
                float wx = zeroPos.x, wz = zeroPos.z;
                switch (rotation) {
                    case 1:
                        wx += pz;
                        wz -= px;
                        tmp = (int)normal.x;
                        normal.x = normal.z;
                        normal.z = -tmp;
                        break;
                    case 2:
                        wx -= px;
                        wz -= pz;
                        normal.x = -normal.x;
                        normal.z = -normal.z;
                        break;
                    case 3:
                        wx -= pz;
                        wz += px;
                        tmp = (int)normal.x;
                        normal.x = normal.z;
                        normal.z = tmp;
                        break;
                    default:
                        wx += px;
                        wz += pz;
                        break;
                }

                pos.x = position.x + model.offsetX + wx;
                pos.y = position.y + model.offsetY + py;
                pos.z = position.z + model.offsetZ + wz;
                pos -= normal;

                VoxelHitInfo hitInfo = new VoxelHitInfo();
                if (GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex)) {
                    hitInfo.chunk = chunk;
                    hitInfo.voxelIndex = voxelIndex;
                    hitInfo.normal = normal;
                    hitInfo.voxelCenter = pos + Misc.vector3half;
                    TorchAttach(hitInfo, model.torches[b].itemDefinition, false);
                }
            }
        }

    }



}
