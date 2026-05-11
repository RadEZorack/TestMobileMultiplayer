//#define DEBUG_BATCHES

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelPlay.GPURendering.InstancingIndirect {

    public class GPUInstancingIndirectRenderer : IGPUInstancingRenderer {
        const float CELL_SIZE = 128;

        Material[] defaultInstancingMaterial;
        FastIndexedList<VoxelChunk, InstancedChunk> instancedChunks;
        FastIndexedList<Vector3, BatchedCell> cells;
        VoxelPlayEnvironment env;
        bool rebuild;
        uint[] args;

        public GPUInstancingIndirectRenderer(VoxelPlayEnvironment env) {
            this.env = env;
            defaultInstancingMaterial = new Material[1];
            defaultInstancingMaterial[0] = Resources.Load<Material>("VoxelPlay/Materials/VP Indirect VertexLit");
            if (!SystemInfo.supportsComputeShaders) {
                Debug.LogError("Current platform does not support compute buffers. Switch off 'Compute Buffers' option in Voxel Play Environment inspector.");
            }
            instancedChunks = new FastIndexedList<VoxelChunk, InstancedChunk>();
            cells = new FastIndexedList<Vector3, BatchedCell>();
            args = new uint[] { 0, 0, 0, 0, 0 };
        }

        public void Dispose() {
            if (cells == null)
                return;
            for (int k = 0; k <= cells.lastIndex; k++) {
                BatchedCell cell = cells.values[k];
                if (cell != null) {
                    cell.DisposeBuffers();
                }
            }
        }

        public void Refresh() {
            rebuild = true;
        }


        public void ClearChunk(VoxelChunk chunk) {
            InstancedChunk instancedChunk;
            if (instancedChunks.TryGetValue(chunk, out instancedChunk)) {
                if (!instancedChunk.batchedCell.rebuild) {
                    instancedChunk.batchedCell.rebuild = true;
                }
                instancedChunk.Clear();
            }
        }


        BatchedCell GetBatchedCell(VoxelChunk chunk) {
            Vector3 pos = chunk.position;
            int cellX, cellY, cellZ;
            FastMath.FloorToInt(pos.x / CELL_SIZE, pos.y / CELL_SIZE, pos.z / CELL_SIZE, out cellX, out cellY, out cellZ);
            pos.x = cellX;
            pos.y = cellY;
            pos.z = cellZ;
            BatchedCell cell;
            if (!cells.TryGetValue(pos, out cell)) {
                cell = new BatchedCell(pos, CELL_SIZE);
                cells.Add(pos, cell);
            }
            return cell;
        }

        public void AddVoxel(VoxelChunk chunk, int voxelIndex, VoxelDefinition voxelDefinition, Vector3 position, Quaternion rotation, Vector3 scale) {

            // Add chunk to cell rendering lists
            InstancedChunk instancedChunk;
            if (!instancedChunks.TryGetValue(chunk, out instancedChunk)) {
                BatchedCell batchedCell = GetBatchedCell(chunk);
                instancedChunk = new InstancedChunk(chunk, batchedCell);
                instancedChunks.Add(chunk, instancedChunk);
                batchedCell.instancedChunks.Add(instancedChunk);
            }

            // Ensure there're batches for this voxel definition in its cell
            InstancedVoxel instancedVoxel = new InstancedVoxel();
            BatchedCell cell = instancedChunk.batchedCell;
            BatchedMesh batchedMesh;
            if (!cell.batchedMeshes.TryGetValue(voxelDefinition, out batchedMesh)) {
                batchedMesh = new BatchedMesh(voxelDefinition);
                Material[] materials = voxelDefinition.materials;
                if (materials.IsNullOrEmpty()) {
                    materials = defaultInstancingMaterial;
                }
                int materialsCount = materials.Length;
                for (int k = 0; k < materialsCount; k++) {
                    materials[k].EnableKeyword(VoxelPlayEnvironment.SKW_VOXELPLAY_GPU_INSTANCING);
                }
                batchedMesh.materials = materials;
                cell.batchedMeshes.Add(voxelDefinition, batchedMesh);
            }

            // Add voxel to the rendering lists
            instancedVoxel.batchedMesh = batchedMesh;
            instancedVoxel.voxelDefinition = voxelDefinition;
            instancedVoxel.meshSize = voxelDefinition.mesh.bounds.size;
            // only uniform scale is supported in indirect rendering (for optimization purposes)
            instancedVoxel.position.x = position.x; instancedVoxel.position.y = position.y; instancedVoxel.position.z = position.z; instancedVoxel.position.w = scale.x;
            instancedVoxel.rotation.x = rotation.x; instancedVoxel.rotation.y = rotation.y; instancedVoxel.rotation.z = rotation.z; instancedVoxel.rotation.w = rotation.w;
            instancedVoxel.color = chunk.voxels[voxelIndex].color;
            instancedVoxel.packedLight = chunk.voxels[voxelIndex].packedLight;
            instancedChunk.instancedVoxels.Add(instancedVoxel);

            // Mark cell to be rebuilt
            cell.rebuild = true;
        }


        /// <summary>
        /// Creates a copy of the array and instantiate its elements
        /// </summary>
        Material[] InstantiateMaterials(Material[] array) {
            if (array == null) return null;

            int length = array.Length;
            Material[] copy = new Material[length];
            for (int k = 0; k < length; k++) {
                if (array[k] != null) {
                    copy[k] = UnityEngine.Object.Instantiate(array[k]);
                }
            }
            return copy;
        }

        void RebuildCellRenderingLists(BatchedCell cell, Vector3 observerPos, float visibleDistance) {
            // rebuild batch lists to be used in the rendering loop
            cell.ClearBatches();

            float cullDistance = (visibleDistance * VoxelPlayEnvironment.CHUNK_SIZE) * (visibleDistance * VoxelPlayEnvironment.CHUNK_SIZE);

            for (int j = 0; j < cell.instancedChunks.count; j++) {
                InstancedChunk instancedChunk = cell.instancedChunks.values[j];
                if (instancedChunk == null)
                    continue;

                // check if chunk is in area
                Vector3 chunkCenter = instancedChunk.chunk.position;
                if (!instancedChunk.chunk.ignoreFrustum && FastVector.SqrDistance(ref chunkCenter, ref observerPos) > cullDistance)
                    continue;

                // add instances to batch
                InstancedVoxel[] voxels = instancedChunk.instancedVoxels.values;
                for (int i = 0; i < instancedChunk.instancedVoxels.count; i++) {
                    BatchedMesh batchedMesh = voxels[i].batchedMesh;

                    Batch batch = batchedMesh.lastBatch;
                    if (batch == null || batch.instancesCount >= Batch.MAX_INSTANCES) {
                        batch = batchedMesh.batches.FetchDirty();
                        if (batch == null) {
                            batch = new Batch();
                            batch.instancedMaterials = InstantiateMaterials(batchedMesh.materials);
                            if (batchedMesh.voxelDefinition.rotationRandomY || batchedMesh.voxelDefinition.rotation != Misc.vector3zero) {
                                int materialsCount = batch.instancedMaterials.Length;
                                for (int k = 0; k < materialsCount; k++) {
                                    batch.instancedMaterials[k].EnableKeyword(VoxelPlayEnvironment.SKW_VOXELPLAY_USE_ROTATION);
                                }
                                batch.usesRotation = true;
                            }
                            batchedMesh.batches.ReplaceLast(batch);
                        }
                        batchedMesh.lastBatch = batch;
                        batch.Init();
                    }
                    int pos = batch.instancesCount++;
                    batch.positions[pos] = voxels[i].position;
                    batch.rotations[pos].x = voxels[i].rotation.x;
                    batch.rotations[pos].y = voxels[i].rotation.y;
                    batch.rotations[pos].z = voxels[i].rotation.z;
                    batch.rotations[pos].w = voxels[i].rotation.w;
                    if (!batch.usesRotation) {
                        // check if this voxel as custom rotations in which case we need to enable rotation feature on the material
                        if (voxels[i].rotation.x != 0 || voxels[i].rotation.y != 0 || voxels[i].rotation.z != 0) {
                            int materialsCount = batch.instancedMaterials.Length;
                            for (int k = 0; k < materialsCount; k++) {
                                batch.instancedMaterials[k].EnableKeyword(VoxelPlayEnvironment.SKW_VOXELPLAY_USE_ROTATION);
                            }
                            batch.usesRotation = true;
                        }
                    }
                    batch.colorsAndLight[pos].x = voxels[i].color.r / 255f;
                    batch.colorsAndLight[pos].y = voxels[i].color.g / 255f;
                    batch.colorsAndLight[pos].z = voxels[i].color.b / 255f;
                    batch.colorsAndLight[pos].w = voxels[i].packedLight;
                    batch.UpdateBounds(voxels[i].position, voxels[i].meshSize);
                }
            }

            for (int i = 0; i <= cell.batchedMeshes.lastIndex; i++) {
                BatchedMesh batchedMesh = cell.batchedMeshes.values[i];
                if (batchedMesh == null)
                    continue;
                for (int j = 0; j < batchedMesh.batches.count; j++) {
                    Batch batch = batchedMesh.batches.values[j];
                    batch.ComputeBounds();

                    Mesh mesh = batchedMesh.voxelDefinition.mesh;
                    int subMeshCount = mesh.subMeshCount;

                    int materialsCount = batch.instancedMaterials.Length;

                    // Upload data buffers once per batch
                    batch.positionsBuffer.SetData(batch.positions);
                    batch.colorsAndLightBuffer.SetData(batch.colorsAndLight);
                    batch.rotationsBuffer.SetData(batch.rotations);

                    batch.EnsureArgsBuffers(subMeshCount);

                    for (int subMesh = 0; subMesh < subMeshCount; subMesh++) {
                        Material instancedMaterial = subMesh < materialsCount ? batch.instancedMaterials[subMesh] : batch.instancedMaterials[materialsCount - 1];
                        // Bind buffers to material
                        instancedMaterial.SetBuffer(ShaderParams.PositionsArray, batch.positionsBuffer);
                        instancedMaterial.SetBuffer(ShaderParams.ColorsAndLightArray, batch.colorsAndLightBuffer);
                        instancedMaterial.SetBuffer(ShaderParams.RotationsArray, batch.rotationsBuffer);
                        // Set per-submesh buffer args
                        args[0] = mesh.GetIndexCount(subMesh);
                        args[1] = (uint)batch.instancesCount;
                        args[2] = mesh.GetIndexStart(subMesh);
                        args[3] = 0;
                        batch.argsBuffers[subMesh].SetData(args);
                    }
                }
            }

        }

        public void Render(Vector3 observerPos, float visibleDistance, Vector3[] frustumPlanesNormals, float[] frustumPlanesDistances) {
#if DEBUG_BATCHES
			int batches = 0;
			int instancesCount = 0;
#endif

            int frameCount = Time.frameCount;

            for (int k = 0; k <= cells.lastIndex; k++) {
                BatchedCell cell = cells.values[k];
                if (cell == null)
                    continue;
                if (env.instancingCullingMode != InstancingCullingMode.Disabled && !GeometryUtilityNonAlloc.TestPlanesAABB(frustumPlanesNormals, frustumPlanesDistances, ref cell.boundsMin, ref cell.boundsMax, env.instancingCullingPadding))
                    continue;

                if (cell.rebuild || rebuild) {
                    if (!Application.isPlaying || frameCount - cell.lastRebuildFrame > 10) {
                        cell.lastRebuildFrame = frameCount;
                        RebuildCellRenderingLists(cell, observerPos, visibleDistance);
                        cell.rebuild = false;
                        rebuild = false;
                    }
                }

                for (int j = 0; j <= cell.batchedMeshes.lastIndex; j++) {
                    BatchedMesh batchedMesh = cell.batchedMeshes.values[j];
                    if (batchedMesh == null)
                        continue;
                    VoxelDefinition vd = batchedMesh.voxelDefinition;
                    Mesh mesh = vd.mesh;
                    int subMeshCount = mesh.subMeshCount;
                    ShadowCastingMode shadowCastingMode = (vd.castShadows && env.enableShadows) ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    bool receiveShadows = vd.receiveShadows && env.enableShadows;
                    for (int i = 0; i < batchedMesh.batches.count; i++) {
                        Batch batch = batchedMesh.batches.values[i];
                        if (env.instancingCullingMode != InstancingCullingMode.Disabled && GeometryUtilityNonAlloc.TestPlanesAABB(frustumPlanesNormals, frustumPlanesDistances, ref batch.boundsMin, ref batch.boundsMax, env.instancingCullingPadding)) {
                            for (int subMesh = 0; subMesh < subMeshCount; subMesh++) {
                                Material instancedMaterial = subMesh < batch.instancedMaterials.Length ? batch.instancedMaterials[subMesh] : batch.instancedMaterials[0];
                                Graphics.DrawMeshInstancedIndirect(mesh, subMesh, instancedMaterial, batch.bounds, batch.argsBuffers[subMesh], 0, null, shadowCastingMode, receiveShadows, env.layerVoxels);
#if DEBUG_BATCHES
							batches++;
							instancesCount += batch.instancesCount;
#endif
                            }
                        }
                    }
                }
            }
#if DEBUG_BATCHES
			Debug.Log ("Batches: " + batches + " Instances: " + instancesCount);
#endif
        }


    }
}
