//#define USES_TINTING
//#define USES_BEVEL
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace VoxelPlay {

    public class MeshingThreadTriangle : MeshingThread {

        const int V_ONE_Y_ROW = VoxelPlayEnvironment.CHUNK_SIZE_PLUS_2 * VoxelPlayEnvironment.CHUNK_SIZE_PLUS_2;
        const int V_ONE_Z_ROW = VoxelPlayEnvironment.CHUNK_SIZE_PLUS_2;
        float aoBase;
        readonly MeshingThreadMicroVoxels microVoxelMesher = new MeshingThreadMicroVoxels();

        const int VP_FLAG_LOCAL_UV = 1 << 23; // flag bit in uv.z to tell shader to use provided uv.xy

        public override void Init (int threadId, int poolSize, VoxelPlayEnvironment env) {
            base.Init(threadId, poolSize, env);
        }


        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        float ComputeVertexLight (int voxelLight, int side1, int side2, int corner) {
            int light = (side1 | side2) == 0 ? voxelLight : voxelLight + side1 + side2 + corner;
            return ((light >> 2) & 0xFFC00) + (light & 0x1FF) * aoBase;
        }


        /// <summary>
        /// Generates chunk mesh. Also computes lightmap if needed.
        /// </summary>
        public override void GenerateMeshData () {
            int jobIndex = meshJobMeshDataGenerationIndex;

            List<int>[] indexBuffers = meshJobs[jobIndex].indexBuffers;
            int indexBuffersLength = indexBuffers.Length;
            for (int j = 0; j < indexBuffersLength; j++) {
                indexBuffers[j].Clear();
            }

            VoxelChunk chunk = meshJobs[jobIndex].chunk;
            tempChunkVertices = meshJobs[jobIndex].vertices;
            tempChunkUV0 = meshJobs[jobIndex].uv0;
            tempChunkColors32 = meshJobs[jobIndex].colors;
            tempChunkNormals = meshJobs[jobIndex].normals;
            meshColliderVertices = meshJobs[jobIndex].colliderVertices;
            meshColliderIndices = meshJobs[jobIndex].colliderIndices;
            navMeshVertices = meshJobs[jobIndex].navMeshVertices;
            navMeshIndices = meshJobs[jobIndex].navMeshIndices;
            FastList<MivEntry> mivs = meshJobs[jobIndex].mivs;

            tempChunkVertices.Clear();
            tempChunkUV0.Clear();
            tempChunkColors32.Clear();
            tempChunkNormals.Clear();
            mivs.Clear();

            if (enableColliders) {
                meshColliderIndices.Clear();
                meshColliderVertices.Clear();
                if (enableNavMesh) {
                    navMeshIndices.Clear();
                    navMeshVertices.Clear();
                }
            }
            Color32 tintColor = Misc.color32White;
            MicroVoxelsPrototype.SideVertexData[] microVoxelsVertexData = null;

            int chunkVoxelCount = 0;
            Vector3 pos = Misc.vector3zero;

            List<int> cutoutCrossBuffer = indexBuffers[VoxelPlayEnvironment.INDICES_BUFFER_CUTXSS];

            Voxel[] voxels = chunk.voxels;

            int voxelSignature = 1;
            int voxelIndex = 0;
            for (int y = 0; y < VoxelPlayEnvironment.CHUNK_SIZE; y++) {
                int vy = (y + 1) * VoxelPlayEnvironment.CHUNK_SIZE_PLUS_2 * VoxelPlayEnvironment.CHUNK_SIZE_PLUS_2;
                for (int z = 0; z < VoxelPlayEnvironment.CHUNK_SIZE; z++) {
                    int vyz = vy + (z + 1) * VoxelPlayEnvironment.CHUNK_SIZE_PLUS_2;
                    for (int x = 0; x < VoxelPlayEnvironment.CHUNK_SIZE; x++, voxelIndex++) {
                        if (voxels[voxelIndex].typeIndex <= Voxel.HOLE_TYPE_INDEX)
                            continue;

                        // If voxel is surrounded by material, don't render
                        int vxyz = vyz + x + 1;

                        int vindex = vxyz - 1;
                        Voxel[] chunk_middle_middle_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int middle_middle_left = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz + 1;
                        Voxel[] chunk_middle_middle_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int middle_middle_right = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz + V_ONE_Y_ROW;
                        Voxel[] chunk_top_middle_middle = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_middle_middle = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz - V_ONE_Y_ROW;
                        Voxel[] chunk_bottom_middle_middle = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_middle_middle = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz + V_ONE_Z_ROW;
                        Voxel[] chunk_middle_forward_middle = chunk9[virtualChunk[vindex].chunk9Index];
                        int middle_forward_middle = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz - V_ONE_Z_ROW;
                        Voxel[] chunk_middle_back_middle = chunk9[virtualChunk[vindex].chunk9Index];
                        int middle_back_middle = virtualChunk[vindex].voxelIndex;

                        // If voxel is surrounded by material, don't render
                        int v1b = chunk_middle_back_middle[middle_back_middle].opaque;
                        int v1f = chunk_middle_forward_middle[middle_forward_middle].opaque;
                        int v1u = chunk_top_middle_middle[top_middle_middle].opaque;
                        int v1d = chunk_bottom_middle_middle[bottom_middle_middle].opaque;
                        int v1l = chunk_middle_middle_left[middle_middle_left].opaque;
                        int v1r = chunk_middle_middle_right[middle_middle_right].opaque;

                        if (chunk.usesMicroVoxels) {
                            // Augment opaque checks with microvoxel full-face coverage flags
                            if (v1b < FULL_OPAQUE && chunk.microVoxels.TryGetValue(middle_back_middle, out MicroVoxels mvBack) && mvBack.count >= MicroVoxels.COUNT_PER_FACE) {
                                if (env.voxelDefinitions[chunk_middle_back_middle[middle_back_middle].typeIndex].renderType.isOpaque()) {
                                    EnsurePrototypeExists(mvBack);
                                    int neighborRotation = chunk_middle_back_middle[middle_back_middle].GetTextureRotation();
                                    Cube.Side adjustedFace = (Cube.Side)(((int)Cube.Side.Forward + neighborRotation) % 4);
                                    if (mvBack.IsFaceFullyCovered(adjustedFace)) v1b = FULL_OPAQUE; // neighbour forward fully covered (adjusted for rotation)
                                }
                            }
                            if (v1f < FULL_OPAQUE && chunk.microVoxels.TryGetValue(middle_forward_middle, out MicroVoxels mvFwd) && mvFwd.count >= MicroVoxels.COUNT_PER_FACE) {
                                if (env.voxelDefinitions[chunk_middle_forward_middle[middle_forward_middle].typeIndex].renderType.isOpaque()) {
                                    EnsurePrototypeExists(mvFwd);
                                    int neighborRotation = chunk_middle_forward_middle[middle_forward_middle].GetTextureRotation();
                                    Cube.Side adjustedFace = (Cube.Side)(((int)Cube.Side.Back + neighborRotation) % 4);
                                    if (mvFwd.IsFaceFullyCovered(adjustedFace)) v1f = FULL_OPAQUE; // neighbour back fully covered (adjusted for rotation)
                                }
                            }
                            if (v1l < FULL_OPAQUE && chunk.microVoxels.TryGetValue(middle_middle_left, out MicroVoxels mvLeft) && mvLeft.count >= MicroVoxels.COUNT_PER_FACE) {
                                if (env.voxelDefinitions[chunk_middle_middle_left[middle_middle_left].typeIndex].renderType.isOpaque()) {
                                    EnsurePrototypeExists(mvLeft);
                                    int neighborRotation = chunk_middle_middle_left[middle_middle_left].GetTextureRotation();
                                    Cube.Side adjustedFace = (Cube.Side)(((int)Cube.Side.Right + neighborRotation) % 4);
                                    if (mvLeft.IsFaceFullyCovered(adjustedFace)) v1l = FULL_OPAQUE; // neighbour right fully covered (adjusted for rotation)
                                }
                            }
                            if (v1r < FULL_OPAQUE && chunk.microVoxels.TryGetValue(middle_middle_right, out MicroVoxels mvRight) && mvRight.count >= MicroVoxels.COUNT_PER_FACE) {
                                if (env.voxelDefinitions[chunk_middle_middle_right[middle_middle_right].typeIndex].renderType.isOpaque()) {
                                    EnsurePrototypeExists(mvRight);
                                    int neighborRotation = chunk_middle_middle_right[middle_middle_right].GetTextureRotation();
                                    Cube.Side adjustedFace = (Cube.Side)(((int)Cube.Side.Left + neighborRotation) % 4);
                                    if (mvRight.IsFaceFullyCovered(adjustedFace)) v1r = FULL_OPAQUE; // neighbour left fully covered (adjusted for rotation)
                                }
                            }
                            if (v1u < FULL_OPAQUE && chunk.microVoxels.TryGetValue(top_middle_middle, out MicroVoxels mvTop) && mvTop.count >= MicroVoxels.COUNT_PER_FACE) {
                                if (env.voxelDefinitions[chunk_top_middle_middle[top_middle_middle].typeIndex].renderType.isOpaque()) {
                                    EnsurePrototypeExists(mvTop);
                                    if (mvTop.IsFaceFullyCovered(Cube.Side.Bottom)) v1u = FULL_OPAQUE; // neighbour bottom fully covered
                                }
                            }
                            if (v1d < FULL_OPAQUE && chunk.microVoxels.TryGetValue(bottom_middle_middle, out MicroVoxels mvBottom) && mvBottom.count >= MicroVoxels.COUNT_PER_FACE) {
                                if (env.voxelDefinitions[chunk_bottom_middle_middle[bottom_middle_middle].typeIndex].renderType.isOpaque()) {
                                    EnsurePrototypeExists(mvBottom);
                                    if (mvBottom.IsFaceFullyCovered(Cube.Side.Top)) v1d = FULL_OPAQUE; // neighbour top fully covered
                                }
                            }
                        }

                        int surroundingOpaque = v1u + v1f + v1b + v1l + v1r + v1d;
                        if (surroundingOpaque == 90) // 90 = 15 * 6
                            continue;

                        // top
                        vindex = vxyz + V_ONE_Y_ROW + V_ONE_Z_ROW - 1;
                        Voxel[] chunk_top_forward_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_forward_left = virtualChunk[vindex].voxelIndex;

                        vindex++;
                        Voxel[] chunk_top_forward_middle = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_forward_middle = virtualChunk[vindex].voxelIndex;

                        vindex++;
                        Voxel[] chunk_top_forward_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_forward_right = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz + V_ONE_Y_ROW - 1;
                        Voxel[] chunk_top_middle_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_middle_left = virtualChunk[vindex].voxelIndex;

                        vindex += 2;
                        Voxel[] chunk_top_middle_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_middle_right = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz + V_ONE_Y_ROW - V_ONE_Z_ROW - 1;
                        Voxel[] chunk_top_back_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_back_left = virtualChunk[vindex].voxelIndex;

                        vindex++;
                        Voxel[] chunk_top_back_middle = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_back_middle = virtualChunk[vindex].voxelIndex;

                        vindex++;
                        Voxel[] chunk_top_back_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int top_back_right = virtualChunk[vindex].voxelIndex;

                        // middle
                        vindex = vxyz + V_ONE_Z_ROW - 1;
                        Voxel[] chunk_middle_forward_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int middle_forward_left = virtualChunk[vindex].voxelIndex;

                        vindex += 2;
                        Voxel[] chunk_middle_forward_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int middle_forward_right = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz - V_ONE_Z_ROW - 1;
                        Voxel[] chunk_middle_back_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int middle_back_left = virtualChunk[vindex].voxelIndex;

                        vindex += 2;
                        Voxel[] chunk_middle_back_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int middle_back_right = virtualChunk[vindex].voxelIndex;

                        // bottom
                        vindex = vxyz - V_ONE_Y_ROW + V_ONE_Z_ROW - 1;
                        Voxel[] chunk_bottom_forward_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_forward_left = virtualChunk[vindex].voxelIndex;

                        vindex++;
                        Voxel[] chunk_bottom_forward_middle = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_forward_middle = virtualChunk[vindex].voxelIndex;

                        vindex++;
                        Voxel[] chunk_bottom_forward_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_forward_right = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz - V_ONE_Y_ROW - 1;
                        Voxel[] chunk_bottom_middle_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_middle_left = virtualChunk[vindex].voxelIndex;

                        vindex += 2;
                        Voxel[] chunk_bottom_middle_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_middle_right = virtualChunk[vindex].voxelIndex;

                        vindex = vxyz - V_ONE_Y_ROW - V_ONE_Z_ROW - 1;
                        Voxel[] chunk_bottom_back_left = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_back_left = virtualChunk[vindex].voxelIndex;

                        vindex++;
                        Voxel[] chunk_bottom_back_middle = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_back_middle = virtualChunk[vindex].voxelIndex;

                        vindex++;
                        Voxel[] chunk_bottom_back_right = chunk9[virtualChunk[vindex].chunk9Index];
                        int bottom_back_right = virtualChunk[vindex].voxelIndex;


                        pos.x = x - VoxelPlayEnvironment.CHUNK_HALF_SIZE + 0.5f;
                        pos.y = y - VoxelPlayEnvironment.CHUNK_HALF_SIZE + 0.5f;
                        pos.z = z - VoxelPlayEnvironment.CHUNK_HALF_SIZE + 0.5f;

                        int typeIndex = voxels[voxelIndex].typeIndex;
                        VoxelDefinition type = env.voxelDefinitions[typeIndex];

                        MicroVoxels microVoxels = null;
                        bool useMicroVoxels = chunk.usesMicroVoxels && chunk.microVoxels.TryGetValue(voxelIndex, out microVoxels);
                        MicroVoxelLayout microLayout = MicroVoxelLayout.Default;
                        VoxelDefinition typeSecondary = type;
                        if (useMicroVoxels) {
                            typeSecondary = microVoxels.secondaryType;
                            microLayout = microVoxels.layout;
                        }

                        // connected voxels
                        if (type.customVoxelDefinitionForRendering != null) {
                            type = type.customVoxelDefinitionForRendering(chunk.position + pos, type, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_top_back_left[top_back_left].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_back_right[top_back_right].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_forward_left[top_forward_left].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_forward_right[top_forward_right].typeIndex, chunk_bottom_back_left[bottom_back_left].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_back_right[bottom_back_right].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_forward_left[bottom_forward_left].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_forward_right[bottom_forward_right].typeIndex);
                            if (type == null) continue;

                            byte opaque = type.opaque;
                            if (type.usesMicroVoxels) {
                                microVoxels = type.microVoxels;
                                useMicroVoxels = true;
                                opaque = microVoxels.GetOpaqueProportional();
                            }

                            if (voxels[voxelIndex].opaque != opaque) {
                                voxels[voxelIndex].opaque = opaque; //   updates voxel opaque value based on the replacement voxel definition
                                chunk.needsMeshRebuild = true; // needs a second pass
                            }

                            if (x == 0 && type.customVoxelDefinitionForRenderingRequiresLeftChunk && ((object)chunk.left == null || !chunk.left.isPopulated)) {
                                chunk.missingLeftChunk = true;
                            }
                            if (x == VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE && type.customVoxelDefinitionForRenderingRequiresRightChunk && ((object)chunk.right == null || !chunk.right.isPopulated)) {
                                chunk.missingRightChunk = true;
                            }
                            if (z == 0 && type.customVoxelDefinitionForRenderingRequiresBackChunk && ((object)chunk.back == null || !chunk.back.isPopulated)) {
                                chunk.missingBackChunk = true;
                            }
                            if (z == VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE && type.customVoxelDefinitionForRenderingRequiresForwardChunk && ((object)chunk.forward == null || !chunk.forward.isPopulated)) {
                                chunk.missingForwardChunk = true;
                            }
                            if (y == 0 && type.customVoxelDefinitionForRenderingRequiresBottomChunk && ((object)chunk.bottom == null || !chunk.bottom.isPopulated)) {
                                chunk.missingBottomChunk = true;
                            }
                            if (y == VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE && type.customVoxelDefinitionForRenderingRequiresTopChunk && ((object)chunk.top == null || !chunk.top.isPopulated)) {
                                chunk.missingTopChunk = true;
                            }
                        }

                        if (useMicroVoxels) {
                            EnsurePrototypeExists(microVoxels);
                            microVoxelsVertexData = microVoxels.prototype?.sidesVertexData;
                        }

                        chunkVoxelCount++;
                        voxelSignature += typeIndex * voxelIndex + surroundingOpaque;
                        List<int> indices = indexBuffers[type.materialBufferIndex];

#if USES_TINTING
                        tintColor.r = voxels[voxelIndex].red;
                        tintColor.g = voxels[voxelIndex].green;
                        tintColor.b = voxels[voxelIndex].blue;
                        faceColors[0].r = faceColors[1].r = faceColors[2].r = faceColors[3].r = tintColor.r;
                        faceColors[0].g = faceColors[1].g = faceColors[2].g = faceColors[3].g = tintColor.g;
                        faceColors[0].b = faceColors[1].b = faceColors[2].b = faceColors[3].b = tintColor.b;
#endif

                        int waterLevel = voxels[voxelIndex].GetWaterLevel();
                        if (waterLevel > 0) {
                            VoxelDefinition typeWater;
                            if (type.isLiquid) {
                                typeWater = type;
                            } else {
                                typeWater = env.currentWaterVoxelDefinition;
                            }
                            List<int> indicesWater = indexBuffers[typeWater.materialBufferIndex];

                            int foam = 0;
                            const int NOFLOW = 1 << 8; // vertical flow

                            int light = (voxels[voxelIndex].light << 13) + (voxels[voxelIndex].torchLight << 17);
                            int flow = NOFLOW;

                            // Get corners heights
                            int hf = chunk_middle_forward_middle[middle_forward_middle].GetWaterLevel();
                            int hb = chunk_middle_back_middle[middle_back_middle].GetWaterLevel();
                            int hr = chunk_middle_middle_right[middle_middle_right].GetWaterLevel();
                            int hl = chunk_middle_middle_left[middle_middle_left].GetWaterLevel();
                            int th = chunk_top_middle_middle[top_middle_middle].GetWaterLevel();
                            int wh = voxels[voxelIndex].GetWaterLevel();

                            int corner_height_fr, corner_height_br, corner_height_fl, corner_height_bl;
                            int hfr = 0, hbr = 0, hbl = 0, hfl = 0;
                            // If there's water on top, full size
                            if (th > 0) {
                                corner_height_fr = corner_height_br = corner_height_fl = corner_height_bl = 15;
                            } else {
                                hfr = corner_height_fr = chunk_middle_forward_right[middle_forward_right].GetWaterLevel();
                                hbr = corner_height_br = chunk_middle_back_right[middle_back_right].GetWaterLevel();
                                hbl = corner_height_bl = chunk_middle_back_left[middle_back_left].GetWaterLevel();
                                hfl = corner_height_fl = chunk_middle_forward_left[middle_forward_left].GetWaterLevel();

                                int tf = chunk_top_forward_middle[top_forward_middle].GetWaterLevel();
                                int tfr = chunk_top_forward_right[top_forward_right].GetWaterLevel();
                                int tr = chunk_top_middle_right[top_middle_right].GetWaterLevel();
                                int tbr = chunk_top_back_right[top_back_right].GetWaterLevel();
                                int tb = chunk_top_back_middle[top_back_middle].GetWaterLevel();
                                int tbl = chunk_top_back_left[top_back_left].GetWaterLevel();
                                int tl = chunk_top_middle_left[top_middle_left].GetWaterLevel();
                                int tfl = chunk_top_forward_left[top_forward_left].GetWaterLevel();

                                // forward right corner
                                if (tf * hf + tfr * corner_height_fr + tr * hr > 0) {
                                    corner_height_fr = 15;
                                } else {
                                    corner_height_fr = wh > corner_height_fr ? wh : corner_height_fr;
                                    if (hf > corner_height_fr)
                                        corner_height_fr = hf;
                                    if (hr > corner_height_fr)
                                        corner_height_fr = hr;
                                }
                                // bottom right corner
                                if (tr * hr + tbr * corner_height_br + tb * hb > 0) {
                                    corner_height_br = 15;
                                } else {
                                    corner_height_br = wh > corner_height_br ? wh : corner_height_br;
                                    if (hr > corner_height_br)
                                        corner_height_br = hr;
                                    if (hb > corner_height_br)
                                        corner_height_br = hb;
                                }
                                // bottom left corner
                                if (tb * hb + tbl * corner_height_bl + tl * hl > 0) {
                                    corner_height_bl = 15;
                                } else {
                                    corner_height_bl = wh > corner_height_bl ? wh : corner_height_bl;
                                    if (hb > corner_height_bl)
                                        corner_height_bl = hb;
                                    if (hl > corner_height_bl)
                                        corner_height_bl = hl;
                                }
                                // forward left corner
                                if (tl * hl + tfl * corner_height_fl + tf * hf > 0) {
                                    corner_height_fl = 15;
                                } else {
                                    corner_height_fl = wh > corner_height_fl ? wh : corner_height_fl;
                                    if (hl > corner_height_fl)
                                        corner_height_fl = hl;
                                    if (hf > corner_height_fl)
                                        corner_height_fl = hf;
                                }

                                // flow
                                int fx = corner_height_fr + corner_height_br - corner_height_fl - corner_height_bl;
                                if (fx < 0)
                                    flow = 2 << 10;
                                else if (fx == 0)
                                    flow = 1 << 10;
                                else
                                    flow = 0;

                                int fz = corner_height_fl + corner_height_fr - corner_height_bl - corner_height_br;
                                if (fz > 0)
                                    flow += 2 << 8;
                                else if (fz == 0)
                                    flow += 1 << 8;
                            }
                            pos.y -= 0.5f;

                            // back face
                            if (hb == 0) {
                                if (v1b == FULL_OPAQUE) {
                                    foam = 1;
                                } else {
                                    int textureIndex = typeWater.textureIndexSide;
                                    int resolvedTextureIndex = typeWater.textureIndexSide;
                                    if (typeWater.customTextureProviderBack != null) {
                                        resolvedTextureIndex = typeWater.customTextureProviderBack(textureIndex,
                                            chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex,
                                            chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                            chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex);
                                    }
                                    if (typeWater.customTextureVariationsProviderBack != null && resolvedTextureIndex == textureIndex) {
                                        resolvedTextureIndex = typeWater.customTextureVariationsProviderBack(textureIndex, chunk.position + pos, 0);
                                    }
                                    AddFaceWater(Cube.faceVerticesBack, Cube.normalsBack, pos, indicesWater, resolvedTextureIndex, light + NOFLOW, 0, corner_height_bl, 0, corner_height_br);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                }
                            }

                            // forward face
                            if (hf == 0) {
                                if (v1f == FULL_OPAQUE) {
                                    foam |= 2;
                                } else {
                                    int textureIndex = typeWater.textureIndexSide;
                                    int resolvedTextureIndex = textureIndex;
                                    if (typeWater.customTextureProviderForward != null) {
                                        resolvedTextureIndex = typeWater.customTextureProviderForward(textureIndex,
                                        chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex,
                                        chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                        chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex);
                                    }
                                    if (typeWater.customTextureVariationsProviderForward != null && resolvedTextureIndex == textureIndex) {
                                        resolvedTextureIndex = typeWater.customTextureVariationsProviderForward(textureIndex, chunk.position + pos, 0);
                                    }
                                    AddFaceWater(Cube.faceVerticesForward, Cube.normalsForward, pos, indicesWater, resolvedTextureIndex, light + NOFLOW, 0, corner_height_fr, 0, corner_height_fl);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                }
                            }

                            // left face
                            if (hl == 0) {
                                if (v1l == FULL_OPAQUE) {
                                    foam |= 4;
                                } else {
                                    int textureIndex = typeWater.textureIndexSide;
                                    int resolvedTextureIndex = textureIndex;
                                    if (typeWater.customTextureProviderLeft != null) {
                                        resolvedTextureIndex = typeWater.customTextureProviderLeft(textureIndex,
                                            chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex,
                                            chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex,
                                            chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex);
                                    }
                                    if (typeWater.customTextureVariationsProviderLeft != null && resolvedTextureIndex == textureIndex) {
                                        resolvedTextureIndex = typeWater.customTextureVariationsProviderLeft(textureIndex, chunk.position + pos, 0);
                                    }
                                    AddFaceWater(Cube.faceVerticesLeft, Cube.normalsLeft, pos, indicesWater, resolvedTextureIndex, light + NOFLOW, 0, corner_height_fl, 0, corner_height_bl);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                }
                            }

                            // right face
                            if (hr == 0) {
                                if (v1r == FULL_OPAQUE) {
                                    foam |= 8;
                                } else {
                                    int textureIndex = typeWater.textureIndexSide;
                                    int resolvedTextureIndex = textureIndex;
                                    if (typeWater.customTextureProviderRight != null) {
                                        resolvedTextureIndex = typeWater.customTextureProviderRight(textureIndex,
                                            chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex,
                                            chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex,
                                            chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex);
                                    }
                                    if (typeWater.customTextureVariationsProviderRight != null && resolvedTextureIndex == textureIndex) {
                                        resolvedTextureIndex = typeWater.customTextureVariationsProviderRight(textureIndex, chunk.position + pos, 0);
                                    }
                                    AddFaceWater(Cube.faceVerticesRight, Cube.normalsRight, pos, indicesWater, resolvedTextureIndex, light + NOFLOW, 0, corner_height_br, 0, corner_height_fr);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                }
                            }

                            // top (hide only if water level is full or voxel on top is water)
                            if (chunk_top_middle_middle[top_middle_middle].typeIndex <= Voxel.HOLE_TYPE_INDEX || (wh < 15 && th == 0)) {
                                if (type.showFoam) {
                                    // corner foam
                                    if (hbl == 0) {
                                        if (chunk_middle_back_left[middle_back_left].typeIndex > Voxel.HOLE_TYPE_INDEX) foam |= 1 << 4;
                                    }
                                    if (hfl == 0) {
                                        if (chunk_middle_forward_left[middle_forward_left].typeIndex > Voxel.HOLE_TYPE_INDEX) foam |= 1 << 5;
                                    }
                                    if (hfr == 0) {
                                        if (chunk_middle_forward_right[middle_forward_right].typeIndex > Voxel.HOLE_TYPE_INDEX) foam |= 1 << 6;
                                    }
                                    if (hbr == 0) {
                                        if (chunk_middle_back_right[middle_back_right].typeIndex > Voxel.HOLE_TYPE_INDEX) foam |= 1 << 7;
                                    }
                                } else {
                                    foam = 0;
                                }
                                int textureIndex = typeWater.textureIndexTop;
                                int resolvedTextureIndex = textureIndex;
                                if (typeWater.customTextureProviderTop != null) {
                                    resolvedTextureIndex = typeWater.customTextureProviderTop(textureIndex,
                                        chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex,
                                        chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                        chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex);
                                }
                                if (typeWater.customTextureVariationsProviderTop != null && resolvedTextureIndex == textureIndex) {
                                    resolvedTextureIndex = typeWater.customTextureVariationsProviderTop(textureIndex, chunk.position + pos, 0);
                                }
                                AddFaceWater(Cube.faceVerticesTop, Cube.normalsUp, pos, indicesWater, resolvedTextureIndex, light + foam + flow, corner_height_bl, corner_height_fl, corner_height_br, corner_height_fr);
                                AddFaceWater(Cube.faceVerticesTopFlipped, Cube.normalsUp, pos, indicesWater, resolvedTextureIndex, light + flow, corner_height_fl, corner_height_bl, corner_height_fr, corner_height_br);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
                                        tempChunkColors32.AddRange(faceColors);
#endif
                            }

                            // bottom
                            if (chunk_bottom_middle_middle[bottom_middle_middle].typeIndex <= Voxel.HOLE_TYPE_INDEX) {
                                int textureIndex = typeWater.textureIndexBottom;
                                int resolvedTextureIndex = textureIndex;
                                if (typeWater.customTextureProviderBottom != null) {
                                    resolvedTextureIndex = typeWater.customTextureProviderBottom(textureIndex,
                                           chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex,
                                            chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                            chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex);
                                }
                                if (typeWater.customTextureVariationsProviderBottom != null && resolvedTextureIndex == textureIndex) {
                                    resolvedTextureIndex = typeWater.customTextureVariationsProviderBottom(textureIndex, chunk.position + pos, 0);
                                }
                                AddFaceWater(Cube.faceVerticesBottom, Cube.normalsDown, pos, indicesWater, resolvedTextureIndex, light + NOFLOW, 0, 0, 0, 0);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                            }

                            pos.y += 0.5f;

                        }

                        switch (type.renderType) {
                            case RenderType.Water:
                            case RenderType.Fluid:
                                break;
                            case RenderType.CutoutCross: {
                                    float random = WorldRand.GetValue(pos.x, pos.z);
                                    float colorVariation = 1f + (random - 0.45f) * type.colorVariation;
                                    float light = voxels[voxelIndex].GetPackedLight(colorVariation);
                                    int texData = type.textureIndexSide;
                                    if (type.windAnimation) {
                                        texData |= 65536;
                                    }
                                    float height = WorldRand.Range(type.vegetationMinHeight, type.vegetationMaxHeight, pos);

                                    // Check if voxel below is a half voxel, then position the vegetation on top of it
                                    int vindexBelow = vxyz - V_ONE_Y_ROW;
                                    int chunkIndexBelow = virtualChunk[vindexBelow].chunk9Index;
                                    VoxelChunk chunkBelow = neighbourChunks[chunkIndexBelow];
                                    if ((object)chunkBelow != null && chunkBelow.usesMicroVoxels) {
                                        int voxelIndexBelow = virtualChunk[vindexBelow].voxelIndex;
                                        if (chunkBelow.microVoxels.TryGetValue(voxelIndexBelow, out MicroVoxels mv) && mv.IsBottomHalf()) {
                                            pos.y -= 0.5f;
                                        }
                                    }

                                    AddFaceVegetation(faceVerticesCross1, pos, indices, texData, light, height, type.offsetRandomVegetation);
                                    AddFaceVegetation(faceVerticesCross2, pos, indices, texData, light, height, type.offsetRandomVegetation);
#if USES_TINTING
                                    tempChunkColors32.AddRange(faceColors);
                                    tempChunkColors32.AddRange(faceColors);
#endif
                                }
                                break;
                            case RenderType.Cloud: {
                                    // back face
                                    VoxelPlayGreedyMesherLit greedyClouds = type.greedyMesherLit;
                                    if (v1b < FULL_OPAQUE) {
                                        greedyClouds.AddQuad(FaceDirection.Back, x, y, z, tintColor, 1f, type.textureIndexSide);
                                    }
                                    // forward face
                                    if (v1f < FULL_OPAQUE) {
                                        greedyClouds.AddQuad(FaceDirection.Forward, x, y, z, tintColor, 1f, type.textureIndexSide);
                                    }
                                    // left face
                                    if (v1l < FULL_OPAQUE) {
                                        greedyClouds.AddQuad(FaceDirection.Left, z, y, x, tintColor, 1f, type.textureIndexSide);
                                    }
                                    // right face
                                    if (v1r < FULL_OPAQUE) {
                                        greedyClouds.AddQuad(FaceDirection.Right, z, y, x, tintColor, 1f, type.textureIndexSide);
                                    }
                                    // top face
                                    if (v1u < FULL_OPAQUE) {
                                        greedyClouds.AddQuad(FaceDirection.Top, x, z, y, tintColor, 1f, type.textureIndexTop);
                                    }
                                    // bottom face
                                    if (v1d < FULL_OPAQUE) {
                                        greedyClouds.AddQuad(FaceDirection.Bottom, x, z, y, tintColor, 1f, type.textureIndexBottom);
                                    }
                                }
                                break;
                            case RenderType.OpaqueNoAO: {
                                    int lu = chunk_top_middle_middle[top_middle_middle].packedLight;
                                    int ll = chunk_middle_middle_left[middle_middle_left].packedLight;
                                    int lf = chunk_middle_forward_middle[middle_forward_middle].packedLight;
                                    int lr = chunk_middle_middle_right[middle_middle_right].packedLight;
                                    int lb = chunk_middle_back_middle[middle_back_middle].packedLight;
                                    int ld = chunk_bottom_middle_middle[bottom_middle_middle].packedLight;

                                    bool addCollider = enableColliders && voxels[voxelIndex].opaque > 5;
                                    int rotationIndex = voxels[voxelIndex].GetTextureRotation();

                                    VoxelPlayGreedyMesherLit greedyOpaqueNoAO = type.greedyMesherLit;

                                    // back face
                                    if (v1b < FULL_OPAQUE) {
                                        int textureIndex = type.textureSideIndices[rotationIndex].back;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderBack != null) {
                                            resolvedTextureIndex = type.customTextureProviderBack(textureIndex,
                                            chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex,
                                            chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                            chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderBack != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderBack(textureIndex, chunk.position + pos, rotationIndex);
                                        }
                                        greedyOpaqueNoAO.AddQuad(FaceDirection.Back, x, y, z, tintColor, lb, resolvedTextureIndex);
                                        if (addCollider) {
                                            greedyCollider.AddQuad(FaceDirection.Back, x, y, z);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Back, x, y, z);
                                            }
                                        }
                                    }
                                    // forward face
                                    if (v1f < FULL_OPAQUE) {
                                        int textureIndex = type.textureSideIndices[rotationIndex].forward;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderForward != null) {
                                            resolvedTextureIndex = type.customTextureProviderForward(textureIndex,
                                            chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex,
                                            chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                            chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderForward != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderForward(textureIndex, chunk.position + pos, rotationIndex);
                                        }
                                        greedyOpaqueNoAO.AddQuad(FaceDirection.Forward, x, y, z, tintColor, lf, resolvedTextureIndex);
                                        if (addCollider) {
                                            greedyCollider.AddQuad(FaceDirection.Forward, x, y, z);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Forward, x, y, z);
                                            }
                                        }
                                    }
                                    // left face
                                    if (v1l < FULL_OPAQUE) {
                                        int textureIndex = type.textureSideIndices[rotationIndex].left;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderLeft != null) {
                                            resolvedTextureIndex = type.customTextureProviderLeft(textureIndex,
                                            chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex,
                                            chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex,
                                            chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderLeft != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderLeft(textureIndex, chunk.position + pos, rotationIndex);
                                        }
                                        greedyOpaqueNoAO.AddQuad(FaceDirection.Left, z, y, x, tintColor, ll, resolvedTextureIndex);
                                        if (addCollider) {
                                            greedyCollider.AddQuad(FaceDirection.Left, z, y, x);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Left, z, y, x);
                                            }
                                        }
                                    }
                                    // right face
                                    if (v1r < FULL_OPAQUE) {
                                        int textureIndex = type.textureSideIndices[rotationIndex].right;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderRight != null) {
                                            resolvedTextureIndex = type.customTextureProviderRight(textureIndex,
                                            chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex,
                                            chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex,
                                            chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderRight != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderRight(textureIndex, chunk.position + pos, rotationIndex);
                                        }
                                        greedyOpaqueNoAO.AddQuad(FaceDirection.Right, z, y, x, tintColor, lr, resolvedTextureIndex);
                                        if (addCollider) {
                                            greedyCollider.AddQuad(FaceDirection.Right, z, y, x);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Right, z, y, x);
                                            }
                                        }
                                    }
                                    // top face
                                    if (v1u < FULL_OPAQUE) {
                                        int textureIndex = type.textureIndexTop;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderTop != null) {
                                            resolvedTextureIndex = type.customTextureProviderTop(textureIndex,
                                            chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex,
                                            chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                            chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderTop != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderTop(textureIndex, chunk.position + pos, rotationIndex);
                                        }
                                        greedyOpaqueNoAO.AddQuad(FaceDirection.Top, x, z, y, tintColor, lu, resolvedTextureIndex);
                                        if (addCollider) {
                                            greedyCollider.AddQuad(FaceDirection.Top, x, z, y);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Top, x, z, y);
                                            }
                                        }
                                    }
                                    // bottom face
                                    if (v1d < FULL_OPAQUE) {
                                        int textureIndex = type.textureIndexBottom;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderBottom != null) {
                                            resolvedTextureIndex = type.customTextureProviderBottom(textureIndex,
                                            chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex,
                                            chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                            chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderBottom != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderBottom(textureIndex, chunk.position + pos, rotationIndex);
                                        }
                                        greedyOpaqueNoAO.AddQuad(FaceDirection.Bottom, x, z, y, tintColor, ld, resolvedTextureIndex);
                                        if (addCollider) {
                                            greedyCollider.AddQuad(FaceDirection.Bottom, x, z, y);
                                        }
                                    }
                                }
                                break;
                            case RenderType.Transp6tex: {
                                    int rotationIndex = voxels[voxelIndex].GetTextureRotation();
                                    float light = voxels[voxelIndex].packedLight;

                                    // back face
                                    if (v1b != FULL_OPAQUE && chunk_middle_back_middle[middle_back_middle].typeIndex != typeIndex) {
                                        int textureIndex = type.textureSideIndices[rotationIndex].back;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderBack != null) {
                                            resolvedTextureIndex = type.customTextureProviderBack(textureIndex,
                                            chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex,
                                            chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                            chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderBack != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderBack(textureIndex, chunk.position + pos, rotationIndex);
                                        }

                                        AddFaceTransparent(Cube.faceVerticesBack, Cube.normalsBack, pos, indices, resolvedTextureIndex, light, type.alpha);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Back, x, y, z);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Back, x, y, z);
                                            }
                                        }
                                    }

                                    // forward
                                    if (v1f != FULL_OPAQUE && chunk_middle_forward_middle[middle_forward_middle].typeIndex != typeIndex) {
                                        int textureIndex = type.textureSideIndices[rotationIndex].forward;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderForward != null) {
                                            resolvedTextureIndex = type.customTextureProviderForward(textureIndex,
                                             chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex,
                                            chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                            chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderForward != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderForward(textureIndex, chunk.position + pos, rotationIndex);
                                        }
                                        AddFaceTransparent(Cube.faceVerticesForward, Cube.normalsForward, pos, indices, resolvedTextureIndex, light, type.alpha);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Forward, x, y, z);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Forward, x, y, z);
                                            }
                                        }
                                    }

                                    // top
                                    if (v1u != FULL_OPAQUE && chunk_top_middle_middle[top_middle_middle].typeIndex != typeIndex) {
                                        int textureIndex = type.textureIndexTop;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderTop != null) {
                                            resolvedTextureIndex = type.customTextureProviderTop(textureIndex,
                                            chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex,
                                            chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                            chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderTop != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderTop(textureIndex, chunk.position + pos, rotationIndex);
                                        }

                                        AddFaceTransparent(Cube.faceVerticesTop, Cube.normalsUp, pos, indices, resolvedTextureIndex, light, type.alpha);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Top, x, z, y);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Top, x, z, y);
                                            }
                                        }
                                    }

                                    // bottom
                                    if (v1d != FULL_OPAQUE && chunk_bottom_middle_middle[bottom_middle_middle].typeIndex != typeIndex) {
                                        int textureIndex = type.textureIndexBottom;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderBottom != null) {
                                            resolvedTextureIndex = type.customTextureProviderBottom(textureIndex,
                                           chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex,
                                            chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                            chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderBottom != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderBottom(textureIndex, chunk.position + pos, rotationIndex);
                                        }

                                        AddFaceTransparent(Cube.faceVerticesBottom, Cube.normalsDown, pos, indices, resolvedTextureIndex, light, type.alpha);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Bottom, x, z, y);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Bottom, x, z, y);
                                            }
                                        }
                                    }

                                    // left
                                    if (v1l != FULL_OPAQUE && chunk_middle_middle_left[middle_middle_left].typeIndex != typeIndex) {
                                        int textureIndex = type.textureSideIndices[rotationIndex].left;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderLeft != null) {
                                            resolvedTextureIndex = type.customTextureProviderLeft(textureIndex,
                                            chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex,
                                            chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex,
                                            chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderLeft != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderLeft(textureIndex, chunk.position + pos, rotationIndex);
                                        }

                                        AddFaceTransparent(Cube.faceVerticesLeft, Cube.normalsLeft, pos, indices, resolvedTextureIndex, light, type.alpha);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Left, z, y, x);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Left, z, y, x);
                                            }
                                        }
                                    }
                                    // right
                                    if (v1r != FULL_OPAQUE && chunk_middle_middle_right[middle_middle_right].typeIndex != typeIndex) {
                                        int textureIndex = type.textureSideIndices[rotationIndex].right;
                                        int resolvedTextureIndex = textureIndex;
                                        if (type.customTextureProviderRight != null) {
                                            resolvedTextureIndex = type.customTextureProviderRight(textureIndex,
                                            chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex,
                                            chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex,
                                            chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex);
                                        }
                                        if (type.customTextureVariationsProviderRight != null && resolvedTextureIndex == textureIndex) {
                                            resolvedTextureIndex = type.customTextureVariationsProviderRight(textureIndex, chunk.position + pos, rotationIndex);
                                        }

                                        AddFaceTransparent(Cube.faceVerticesRight, Cube.normalsRight, pos, indices, resolvedTextureIndex, light, type.alpha);
#if USES_TINTING
                                        tempChunkColors32.AddRange(faceColors);
#endif
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Right, z, y, x);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Right, z, y, x);
                                            }
                                        }
                                    }
                                }
                                break;
                            default: //case RenderType.Custom:
                                {
                                    // check if voxel is surrounded
                                    bool s1b = env.voxelDefinitions[chunk_middle_back_middle[middle_back_middle].typeIndex].occludesForward;
                                    bool s1f = env.voxelDefinitions[chunk_middle_forward_middle[middle_forward_middle].typeIndex].occludesBack;
                                    bool s1u = env.voxelDefinitions[chunk_top_middle_middle[top_middle_middle].typeIndex].occludesBottom;
                                    bool s1d = env.voxelDefinitions[chunk_bottom_middle_middle[bottom_middle_middle].typeIndex].occludesTop;
                                    bool s1l = env.voxelDefinitions[chunk_middle_middle_left[middle_middle_left].typeIndex].occludesRight;
                                    bool s1r = env.voxelDefinitions[chunk_middle_middle_right[middle_middle_right].typeIndex].occludesLeft;
                                    if (s1b && s1f && s1u && s1d && s1l && s1r) {
                                        chunkVoxelCount--;
                                        continue;
                                    }

                                    mivs.Add(new MivEntry { voxelindex = voxelIndex, renderingVoxelDefinition = type });

                                    bool addCollider = type.generateCollider && enableColliders;

                                    if (addCollider) {
                                        bool addNavMesh = enableNavMesh && type.generateNavMesh;

                                        // back face
                                        if (v1b < FULL_OPAQUE) {
                                            greedyCollider.AddQuad(FaceDirection.Back, x, y, z);
                                            if (addNavMesh) {
                                                greedyNavMesh.AddQuad(FaceDirection.Back, x, y, z);
                                            }
                                        }
                                        // forward face
                                        if (v1f < FULL_OPAQUE) {
                                            greedyCollider.AddQuad(FaceDirection.Forward, x, y, z);
                                            if (addNavMesh) {
                                                greedyNavMesh.AddQuad(FaceDirection.Forward, x, y, z);
                                            }
                                        }
                                        // left face
                                        if (v1l < FULL_OPAQUE) {
                                            greedyCollider.AddQuad(FaceDirection.Left, z, y, x);
                                            if (addNavMesh) {
                                                greedyNavMesh.AddQuad(FaceDirection.Left, z, y, x);
                                            }
                                        }
                                        // right face
                                        if (v1r < FULL_OPAQUE) {
                                            greedyCollider.AddQuad(FaceDirection.Right, z, y, x);
                                            if (addNavMesh) {
                                                greedyNavMesh.AddQuad(FaceDirection.Right, z, y, x);
                                            }
                                        }
                                        // top face
                                        if (v1u < FULL_OPAQUE) {
                                            greedyCollider.AddQuad(FaceDirection.Top, x, z, y);
                                            if (addNavMesh) {
                                                greedyNavMesh.AddQuad(FaceDirection.Top, x, z, y);
                                            }
                                        }
                                        // bottom face
                                        if (v1d < FULL_OPAQUE) {
                                            greedyCollider.AddQuad(FaceDirection.Bottom, x, z, y);
                                        }
                                    }

                                    break;
                                }
                            case RenderType.Invisible: {
                                    // back face
                                    if (v1b < FULL_OPAQUE) {
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Back, x, y, z);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Back, x, y, z);
                                            }
                                        }
                                    }
                                    // forward face
                                    if (v1f < FULL_OPAQUE) {
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Forward, x, y, z);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Forward, x, y, z);
                                            }
                                        }
                                    }
                                    // left face
                                    if (v1l < FULL_OPAQUE) {
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Left, z, y, x);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Left, z, y, x);
                                            }
                                        }
                                    }
                                    // right face
                                    if (v1r < FULL_OPAQUE) {
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Right, z, y, x);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Right, z, y, x);
                                            }
                                        }
                                    }
                                    // top face
                                    if (v1u < FULL_OPAQUE) {
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Top, x, z, y);
                                            if (enableNavMesh && type.navigatable) {
                                                greedyNavMesh.AddQuad(FaceDirection.Top, x, z, y);
                                            }
                                        }
                                    }
                                    // bottom face
                                    if (v1d < FULL_OPAQUE) {
                                        if (enableColliders) {
                                            greedyCollider.AddQuad(FaceDirection.Bottom, x, z, y);
                                        }
                                    }
                                    break;
                                }
                            case RenderType.Cutout: {

                                    if (allowAO || type.overrideMaterial || type.texturesCustomPacking) {
                                        // Cutout with AO
                                        int lu = chunk_top_middle_middle[top_middle_middle].packedLight;
                                        int ll = chunk_middle_middle_left[middle_middle_left].packedLight;
                                        int lf = chunk_middle_forward_middle[middle_forward_middle].packedLight;
                                        int lr = chunk_middle_middle_right[middle_middle_right].packedLight;
                                        int lb = chunk_middle_back_middle[middle_back_middle].packedLight;
                                        int ld = chunk_bottom_middle_middle[bottom_middle_middle].packedLight;

                                        int v2r = chunk_top_middle_right[top_middle_right].packedLight;
                                        int v2br = chunk_top_back_right[top_back_right].packedLight;
                                        int v2b = chunk_top_back_middle[top_back_middle].packedLight;
                                        int v2bl = chunk_top_back_left[top_back_left].packedLight;
                                        int v2l = chunk_top_middle_left[top_middle_left].packedLight;
                                        int v2fl = chunk_top_forward_left[top_forward_left].packedLight;
                                        int v2f = chunk_top_forward_middle[top_forward_middle].packedLight;
                                        int v2fr = chunk_top_forward_right[top_forward_right].packedLight;

                                        int v1fr = chunk_middle_forward_right[middle_forward_right].packedLight;
                                        int v1br = chunk_middle_back_right[middle_back_right].packedLight;
                                        int v1bl = chunk_middle_back_left[middle_back_left].packedLight;
                                        int v1fl = chunk_middle_forward_left[middle_forward_left].packedLight;

                                        int v0r = chunk_bottom_middle_right[bottom_middle_right].packedLight;
                                        int v0br = chunk_bottom_back_right[bottom_back_right].packedLight;
                                        int v0b = chunk_bottom_back_middle[bottom_back_middle].packedLight;
                                        int v0bl = chunk_bottom_back_left[bottom_back_left].packedLight;
                                        int v0l = chunk_bottom_middle_left[bottom_middle_left].packedLight;
                                        int v0fl = chunk_bottom_forward_left[bottom_forward_left].packedLight;
                                        int v0f = chunk_bottom_forward_middle[bottom_forward_middle].packedLight;
                                        int v0fr = chunk_bottom_forward_right[bottom_forward_right].packedLight;

                                        float l0, l1, l2, l3;

                                        aoBase = 1f / 4f; // 4 light factors per vertex
                                        bool addCollider = enableColliders & type.generateColliders;
                                        float random = WorldRand.GetValue(pos);
                                        float colorVariation = 1f - (random - 0.45f) * type.colorVariation;
                                        aoBase *= colorVariation;
                                        int extraData = type.windAnimation ? 65536 : 0;
                                        if (type.usesDenseLeaves) {
                                            float light = voxels[voxelIndex].GetPackedLight(colorVariation);
                                            int texData = type.textureIndexTop | extraData;
                                            int fr = (int)(random * 16);
                                            AddFaceDenseLeaves(faceVerticesCrossLeaves1[fr], pos, cutoutCrossBuffer, texData, light, random);
                                            AddFaceDenseLeaves(faceVerticesCrossLeaves2[fr], pos, cutoutCrossBuffer, texData, light, random);
#if USES_TINTING
                                            tempChunkColors32.AddRange(faceColors);
                                            tempChunkColors32.AddRange(faceColors);
#endif

                                        }
                                        int rotationIndex = voxels[voxelIndex].GetTextureRotation();

                                        // back face
                                        if (v1b < FULL_OPAQUE) {
                                            // Vertex 0 (from the cube representation)
                                            l0 = ComputeVertexLight(lb, v0b, v1bl, v0bl);
                                            // Vertex 2
                                            l1 = ComputeVertexLight(lb, v2b, v1bl, v2bl);
                                            // Vertex 1
                                            l2 = ComputeVertexLight(lb, v0b, v1br, v0br);
                                            // Vertex 3
                                            l3 = ComputeVertexLight(lb, v2b, v1br, v2br);

                                            int textureIndex = type.textureSideIndices[rotationIndex].back;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderBack != null) {
                                                resolvedTextureIndex = type.customTextureProviderBack(textureIndex,
                                                chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex,
                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderBack != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderBack(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            AddFaceWithAO(Cube.faceVerticesBack, Cube.normalsBack, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                            tempChunkColors32.AddRange(faceColors);
#endif
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Back, x, y, z);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Back, x, y, z);
                                                }
                                            }
                                        }
                                        // forward face
                                        if (v1f < FULL_OPAQUE) {
                                            // Vertex 5
                                            l0 = ComputeVertexLight(lf, v0f, v1fr, v0fr);
                                            // Vertex 6
                                            l1 = ComputeVertexLight(lf, v2f, v1fr, v2fr);
                                            // Vertex 4
                                            l2 = ComputeVertexLight(lf, v0f, v1fl, v0fl);
                                            // Vertex 7
                                            l3 = ComputeVertexLight(lf, v2f, v1fl, v2fl);

                                            int textureIndex = type.textureSideIndices[rotationIndex].forward;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderForward != null) {
                                                resolvedTextureIndex = type.customTextureProviderForward(textureIndex,
                                                chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex,
                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderForward != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderForward(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            AddFaceWithAO(Cube.faceVerticesForward, Cube.normalsForward, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                            tempChunkColors32.AddRange(faceColors);
#endif
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Forward, x, y, z);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Forward, x, y, z);
                                                }
                                            }
                                        }
                                        // left face
                                        if (v1l < FULL_OPAQUE) {
                                            // Vertex 4
                                            l0 = ComputeVertexLight(ll, v0l, v1fl, v0fl);
                                            // Vertex 7
                                            l1 = ComputeVertexLight(ll, v2l, v1fl, v2fl);
                                            // Vertex 0
                                            l2 = ComputeVertexLight(ll, v0l, v1bl, v0bl);
                                            // Vertex 2
                                            l3 = ComputeVertexLight(ll, v2l, v1bl, v2bl);

                                            int textureIndex = type.textureSideIndices[rotationIndex].left;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderLeft != null) {
                                                resolvedTextureIndex = type.customTextureProviderLeft(textureIndex,
                                                chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex,
                                                chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex,
                                                chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderLeft != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderLeft(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            AddFaceWithAO(Cube.faceVerticesLeft, Cube.normalsLeft, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                            tempChunkColors32.AddRange(faceColors);
#endif
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Left, z, y, x);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Left, z, y, x);
                                                }
                                            }
                                        }
                                        // right face
                                        if (v1r < FULL_OPAQUE) {
                                            // Vertex 1
                                            l0 = ComputeVertexLight(lr, v0r, v1br, v0br);
                                            // Vertex 3
                                            l1 = ComputeVertexLight(lr, v2r, v1br, v2br);
                                            // Vertex 5
                                            l2 = ComputeVertexLight(lr, v0r, v1fr, v0fr);
                                            // Vertex 6
                                            l3 = ComputeVertexLight(lr, v2r, v1fr, v2fr);

                                            int textureIndex = type.textureSideIndices[rotationIndex].right;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderRight != null) {
                                                resolvedTextureIndex = type.customTextureProviderRight(textureIndex,
                                                chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex,
                                                chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex,
                                                chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderRight != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderRight(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            AddFaceWithAO(Cube.faceVerticesRight, Cube.normalsRight, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);

#if USES_TINTING
                                            tempChunkColors32.AddRange(faceColors);
#endif
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Right, z, y, x);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Right, z, y, x);
                                                }
                                            }
                                        }
                                        // top face
                                        if (v1u < FULL_OPAQUE) {
                                            // Top face
                                            // Vertex 2
                                            l0 = ComputeVertexLight(lu, v2b, v2l, v2bl);
                                            // Vertex 7
                                            l1 = ComputeVertexLight(lu, v2l, v2f, v2fl);
                                            // Vvertex 3
                                            l2 = ComputeVertexLight(lu, v2b, v2r, v2br);
                                            // Vertex 6
                                            l3 = ComputeVertexLight(lu, v2r, v2f, v2fr);

                                            int textureIndex = type.textureIndexTop;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderTop != null) {
                                                resolvedTextureIndex = type.customTextureProviderTop(textureIndex,
                                                chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex,
                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderTop != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderTop(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            AddFaceWithAO(Cube.faceVerticesTop, Cube.normalsUp, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);

#if USES_TINTING
                                            tempChunkColors32.AddRange(faceColors);
#endif
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Top, x, z, y);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Top, x, z, y);
                                                }
                                            }
                                        }
                                        // bottom face
                                        if (v1d < FULL_OPAQUE) {
                                            // Vertex 1
                                            l0 = ComputeVertexLight(ld, v0b, v0r, v0br);
                                            // Vertex 5
                                            l1 = ComputeVertexLight(ld, v0f, v0r, v0fr);
                                            // Vertex 0
                                            l2 = ComputeVertexLight(ld, v0b, v0l, v0bl);
                                            // Vertex 4
                                            l3 = ComputeVertexLight(ld, v0f, v0l, v0fl);

                                            int textureIndex = type.textureIndexBottom;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderBottom != null) {
                                                resolvedTextureIndex = type.customTextureProviderBottom(textureIndex,
                                               chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex,
                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderBottom != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderBottom(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            AddFaceWithAO(Cube.faceVerticesBottom, Cube.normalsDown, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);

#if USES_TINTING
                                            tempChunkColors32.AddRange(faceColors);
#endif
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Bottom, x, z, y);
                                                // no NavMesh for bottom faces
                                            }
                                        }
                                    } else {
                                        // Cutout without AO
                                        bool addCollider = enableColliders & type.generateColliders;
                                        int extraData = type.windAnimation ? 65536 : 0;
                                        float random = WorldRand.GetValue(pos);
                                        float colorVariation = 1f + (random - 0.45f) * type.colorVariation;

                                        int topFaceGI = chunk_top_middle_middle[top_middle_middle].GetPackedLight(colorVariation);
                                        int leftFaceGI = chunk_middle_middle_left[middle_middle_left].GetPackedLight(colorVariation);
                                        int frontFaceGI = chunk_middle_forward_middle[middle_forward_middle].GetPackedLight(colorVariation);
                                        int rightFaceGI = chunk_middle_middle_right[middle_middle_right].GetPackedLight(colorVariation);
                                        int backFaceGI = chunk_middle_back_middle[middle_back_middle].GetPackedLight(colorVariation);
                                        int bottomFaceGI = chunk_bottom_middle_middle[bottom_middle_middle].GetPackedLight(colorVariation);

                                        VoxelPlayGreedyMesherLit greedyCutoutNoAO = type.greedyMesherLit;

                                        if (type.usesDenseLeaves) {
                                            float light = voxels[voxelIndex].GetPackedLight(colorVariation);
                                            int texData = type.textureIndexTop | extraData;
                                            int fr = (int)(random * 16);
                                            AddFaceDenseLeaves(faceVerticesCrossLeaves1[fr], pos, cutoutCrossBuffer, texData, light, random);
                                            AddFaceDenseLeaves(faceVerticesCrossLeaves2[fr], pos, cutoutCrossBuffer, texData, light, random);
#if USES_TINTING
                                            tempChunkColors32.AddRange(faceColors);
                                            tempChunkColors32.AddRange(faceColors);
#endif
                                        }
                                        int rotationIndex = voxels[voxelIndex].GetTextureRotation();

                                        // back face
                                        if (v1b < FULL_OPAQUE) {
                                            int textureIndex = type.textureSideIndices[rotationIndex].back;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderBack != null) {
                                                resolvedTextureIndex = type.customTextureProviderBack(textureIndex,
                                                chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex,
                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderBack != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderBack(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyCutoutNoAO.AddQuad(FaceDirection.Back, x, y, z, tintColor, backFaceGI, resolvedTextureIndex + extraData);
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Back, x, y, z);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Back, x, y, z);
                                                }
                                            }
                                        }
                                        // forward face
                                        if (v1f < FULL_OPAQUE) {
                                            int textureIndex = type.textureSideIndices[rotationIndex].forward;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderForward != null) {
                                                resolvedTextureIndex = type.customTextureProviderForward(textureIndex,
                                                 chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex,
                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderForward != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderForward(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyCutoutNoAO.AddQuad(FaceDirection.Forward, x, y, z, tintColor, frontFaceGI, resolvedTextureIndex + extraData);
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Forward, x, y, z);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Forward, x, y, z);
                                                }
                                            }
                                        }
                                        // left face
                                        if (v1l < FULL_OPAQUE) {
                                            int textureIndex = type.textureSideIndices[rotationIndex].left;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderLeft != null) {
                                                resolvedTextureIndex = type.customTextureProviderLeft(textureIndex,
                                                chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex,
                                                chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex,
                                                chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderLeft != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderLeft(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyCutoutNoAO.AddQuad(FaceDirection.Left, z, y, x, tintColor, leftFaceGI, resolvedTextureIndex + extraData);
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Left, z, y, x);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Left, z, y, x);
                                                }
                                            }
                                        }
                                        // right face
                                        if (v1r < FULL_OPAQUE) {
                                            int textureIndex = type.textureSideIndices[rotationIndex].right;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderRight != null) {
                                                resolvedTextureIndex = type.customTextureProviderRight(textureIndex,
                                                chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex,
                                                chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex,
                                                chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderRight != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderRight(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyCutoutNoAO.AddQuad(FaceDirection.Right, z, y, x, tintColor, rightFaceGI, resolvedTextureIndex + extraData);
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Right, z, y, x);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Right, z, y, x);
                                                }
                                            }
                                        }
                                        // top face
                                        if (v1u < FULL_OPAQUE) {
                                            int textureIndex = type.textureIndexTop;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderTop != null) {
                                                resolvedTextureIndex = type.customTextureProviderTop(textureIndex,
                                                chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex,
                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderTop != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderTop(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyCutoutNoAO.AddQuad(FaceDirection.Top, x, z, y, tintColor, topFaceGI, resolvedTextureIndex + extraData);
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Top, x, z, y);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Top, x, z, y);
                                                }
                                            }
                                        }
                                        // bottom face
                                        if (v1d < FULL_OPAQUE) {
                                            int textureIndex = type.textureIndexBottom;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderBottom != null) {
                                                resolvedTextureIndex = type.customTextureProviderBottom(textureIndex,
                                               chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex,
                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderBottom != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderBottom(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyCutoutNoAO.AddQuad(FaceDirection.Bottom, x, z, y, tintColor, bottomFaceGI, resolvedTextureIndex + extraData);
                                            if (addCollider) {
                                                greedyCollider.AddQuad(FaceDirection.Bottom, x, z, y);
                                            }
                                        }
                                    }
                                }
                                break;
                            case RenderType.Opaque:
                            case RenderType.OpaqueAnimated:
                            case RenderType.Opaque6tex: {
                                    // exception: smart biome surface rendering based on voxel above
                                    if (env.smartBiomeSurface) {
                                        // If this is a surface voxel (has dirt counterpart)
                                        if ((object)type.biomeDirtCounterpart != null) {
                                            VoxelDefinition voxelAboveType = env.voxelDefinitions[chunk_top_middle_middle[top_middle_middle].typeIndex];
                                            bool voxelAboveIsSolid = voxelAboveType.renderType.isOpaque();
                                            if (voxelAboveIsSolid) {
                                                type = type.biomeDirtCounterpart;
                                            }
                                        }
                                        // If this is a dirt voxel (has surface counterpart)
                                        else if ((object)type.biomeSurfaceCounterpart != null) {
                                            VoxelDefinition voxelAboveType = env.voxelDefinitions[chunk_top_middle_middle[top_middle_middle].typeIndex];
                                            bool voxelAboveIsSolid = voxelAboveType.renderType.isOpaque();
                                            if (!voxelAboveIsSolid) {
                                                type = type.biomeSurfaceCounterpart;
                                            }
                                        }
                                    }

                                    int lu = chunk_top_middle_middle[top_middle_middle].packedLight;
                                    int ll = chunk_middle_middle_left[middle_middle_left].packedLight;
                                    int lf = chunk_middle_forward_middle[middle_forward_middle].packedLight;
                                    int lr = chunk_middle_middle_right[middle_middle_right].packedLight;
                                    int lb = chunk_middle_back_middle[middle_back_middle].packedLight;
                                    int ld = chunk_bottom_middle_middle[bottom_middle_middle].packedLight;
                                    int rotationIndex = voxels[voxelIndex].GetTextureRotation();

                                    int extraData = 0;
                                    if (type.renderType == RenderType.OpaqueAnimated) {
                                        extraData = (type.animationSpeed << 18) | ((type.animationTextures.Length + 1) << 14);
                                    }

                                    bool doNotPackVertices = (type.overrideMaterial && !type.overrideMaterialGreedyMeshing) || type.texturesCustomPacking || env.enableCurvature || type.renderType.supportsTextureAnimation();

                                    if (allowAO || doNotPackVertices) {

                                        VoxelPlayGreedyMesherLitAO greedyOpaque = type.greedyMesherLitAO;

                                        // Opaque / Cutout with AO
                                        int v2r = chunk_top_middle_right[top_middle_right].packedLight;
                                        int v2br = chunk_top_back_right[top_back_right].packedLight;
                                        int v2b = chunk_top_back_middle[top_back_middle].packedLight;
                                        int v2bl = chunk_top_back_left[top_back_left].packedLight;
                                        int v2l = chunk_top_middle_left[top_middle_left].packedLight;
                                        int v2fl = chunk_top_forward_left[top_forward_left].packedLight;
                                        int v2f = chunk_top_forward_middle[top_forward_middle].packedLight;
                                        int v2fr = chunk_top_forward_right[top_forward_right].packedLight;

                                        int v1fr = chunk_middle_forward_right[middle_forward_right].packedLight;
                                        int v1br = chunk_middle_back_right[middle_back_right].packedLight;
                                        int v1bl = chunk_middle_back_left[middle_back_left].packedLight;
                                        int v1fl = chunk_middle_forward_left[middle_forward_left].packedLight;

                                        int v0r = chunk_bottom_middle_right[bottom_middle_right].packedLight;
                                        int v0br = chunk_bottom_back_right[bottom_back_right].packedLight;
                                        int v0b = chunk_bottom_back_middle[bottom_back_middle].packedLight;
                                        int v0bl = chunk_bottom_back_left[bottom_back_left].packedLight;
                                        int v0l = chunk_bottom_middle_left[bottom_middle_left].packedLight;
                                        int v0fl = chunk_bottom_forward_left[bottom_forward_left].packedLight;
                                        int v0f = chunk_bottom_forward_middle[bottom_forward_middle].packedLight;
                                        int v0fr = chunk_bottom_forward_right[bottom_forward_right].packedLight;

                                        float l0, l1, l2, l3;

                                        aoBase = 1f / 4f; // 4 light factors per vertex

                                        // back face
                                        if (v1b < FULL_OPAQUE || useMicroVoxels) {
                                            // Vertex 0 (from the cube representatino)
                                            l0 = ComputeVertexLight(lb, v0b, v1bl, v0bl);
                                            // Vertex 2
                                            l1 = ComputeVertexLight(lb, v2b, v1bl, v2bl);
                                            // Vertex 1
                                            l2 = ComputeVertexLight(lb, v0b, v1br, v0br);
                                            // Vertex 3
                                            l3 = ComputeVertexLight(lb, v2b, v1br, v2br);
                                            int textureIndex = type.textureSideIndices[rotationIndex].back;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderBack != null) {
                                                resolvedTextureIndex = type.customTextureProviderBack(textureIndex,
                                                chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex,
                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderBack != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderBack(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            if (useMicroVoxels) {
                                                int sideIndex = ((int)Cube.Side.Back + rotationIndex) % 4;
                                                // Check if we can skip geometry generation due to perfect occlusion by adjacent voxel
                                                VoxelChunk adjacentChunkBack = neighbourChunks[virtualChunk[vxyz - V_ONE_Z_ROW].chunk9Index];
                                                int adjacentRotation = chunk_middle_back_middle[middle_back_middle].GetTextureRotation();
                                                if (adjacentChunkBack == null || ShouldTreatNeighbourAsOpaque(microVoxels, Cube.Side.Back, rotationIndex, adjacentChunkBack, middle_back_middle, Cube.Side.Forward, adjacentRotation, ref v1b)) {
                                                    int resolvedSecondaryTextureIndex = resolvedTextureIndex;
                                                    if (typeSecondary != null) {
                                                        int texB = typeSecondary.textureSideIndices[rotationIndex].back;
                                                        resolvedSecondaryTextureIndex = texB;
                                                        if (typeSecondary.customTextureProviderBack != null) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureProviderBack(texB,
                                                                chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex,
                                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                                chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex);
                                                        }
                                                        if (typeSecondary.customTextureVariationsProviderBack != null && resolvedSecondaryTextureIndex == texB) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureVariationsProviderBack(texB, chunk.position + pos, rotationIndex);
                                                        }
                                                    }
                                                    AddMicroVoxelGeometry(microVoxelsVertexData[sideIndex].vertices, microVoxelsVertexData[sideIndex].uvs, Cube.normalsBack, pos, indices, resolvedTextureIndex + extraData, resolvedSecondaryTextureIndex + extraData, microLayout, l0, l1, l2, l3, enableColliders, enableNavMesh && type.navigatable, rotationIndex, tintColor, v1b, v1f, v1l, v1r, v1u, v1d);
                                                }
                                            } else {
                                                if (doNotPackVertices) {
                                                    AddFaceWithAO(Cube.faceVerticesBack, Cube.normalsBack, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                                tempChunkColors32.AddRange(faceColors);
#endif
                                                } else {
                                                    greedyOpaque.AddQuad(FaceDirection.Back, x, y, z, tintColor, l0, l1, l2, l3, resolvedTextureIndex + extraData);
                                                }
                                                if (enableColliders) {
                                                    greedyCollider.AddQuad(FaceDirection.Back, x, y, z);
                                                    if (enableNavMesh && type.navigatable) {
                                                        greedyNavMesh.AddQuad(FaceDirection.Back, x, y, z);
                                                    }
                                                }
                                            }
                                        }
                                        // forward face
                                        if (v1f < FULL_OPAQUE || useMicroVoxels) {
                                            // Vertex 5
                                            l0 = ComputeVertexLight(lf, v0f, v1fr, v0fr);
                                            // Vertex 6
                                            l1 = ComputeVertexLight(lf, v2f, v1fr, v2fr);
                                            // Vertex 4
                                            l2 = ComputeVertexLight(lf, v0f, v1fl, v0fl);
                                            // Vertex 7
                                            l3 = ComputeVertexLight(lf, v2f, v1fl, v2fl);

                                            int textureIndex = type.textureSideIndices[rotationIndex].forward;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderForward != null) {
                                                resolvedTextureIndex = type.customTextureProviderForward(textureIndex,
                                                 chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex,
                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderForward != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderForward(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            if (useMicroVoxels) {
                                                int sideIndex = ((int)Cube.Side.Forward + rotationIndex) % 4;
                                                // Check if we can skip geometry generation due to perfect occlusion by adjacent voxel
                                                VoxelChunk adjacentChunkForward = neighbourChunks[virtualChunk[vxyz + V_ONE_Z_ROW].chunk9Index];
                                                int adjacentRotation = chunk_middle_forward_middle[middle_forward_middle].GetTextureRotation();
                                                if (adjacentChunkForward == null || ShouldTreatNeighbourAsOpaque(microVoxels, Cube.Side.Forward, rotationIndex, adjacentChunkForward, middle_forward_middle, Cube.Side.Back, adjacentRotation, ref v1f)) {
                                                    int resolvedSecondaryTextureIndex = resolvedTextureIndex;
                                                    if (typeSecondary != null) {
                                                        int texB = typeSecondary.textureSideIndices[rotationIndex].forward;
                                                        resolvedSecondaryTextureIndex = texB;
                                                        if (typeSecondary.customTextureProviderForward != null) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureProviderForward(texB,
                                                                chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex,
                                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                                chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex);
                                                        }
                                                        if (typeSecondary.customTextureVariationsProviderForward != null && resolvedSecondaryTextureIndex == texB) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureVariationsProviderForward(texB, chunk.position + pos, rotationIndex);
                                                        }
                                                    }
                                                    AddMicroVoxelGeometry(microVoxelsVertexData[sideIndex].vertices, microVoxelsVertexData[sideIndex].uvs, Cube.normalsForward, pos, indices, resolvedTextureIndex + extraData, resolvedSecondaryTextureIndex + extraData, microLayout, l0, l1, l2, l3, enableColliders, enableNavMesh && type.navigatable, rotationIndex, tintColor, v1b, v1f, v1l, v1r, v1u, v1d);
                                                }
                                            } else {
                                                if (doNotPackVertices) {
                                                    AddFaceWithAO(Cube.faceVerticesForward, Cube.normalsForward, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                                tempChunkColors32.AddRange(faceColors);
#endif
                                                } else {
                                                    greedyOpaque.AddQuad(FaceDirection.Forward, x, y, z, tintColor, l0, l1, l2, l3, resolvedTextureIndex + extraData);
                                                }
                                                if (enableColliders) {
                                                    greedyCollider.AddQuad(FaceDirection.Forward, x, y, z);
                                                    if (enableNavMesh && type.navigatable) {
                                                        greedyNavMesh.AddQuad(FaceDirection.Forward, x, y, z);
                                                    }
                                                }
                                            }
                                        }
                                        // left face
                                        if (v1l < FULL_OPAQUE || useMicroVoxels) {
                                            // Vertex 4
                                            l0 = ComputeVertexLight(ll, v0l, v1fl, v0fl);
                                            // Vertex 7
                                            l1 = ComputeVertexLight(ll, v2l, v1fl, v2fl);
                                            // Vertex 0
                                            l2 = ComputeVertexLight(ll, v0l, v1bl, v0bl);
                                            // Vertex 2
                                            l3 = ComputeVertexLight(ll, v2l, v1bl, v2bl);

                                            int textureIndex = type.textureSideIndices[rotationIndex].left;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderLeft != null) {
                                                resolvedTextureIndex = type.customTextureProviderLeft(textureIndex,
                                                chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex,
                                                chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex,
                                                chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderLeft != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderLeft(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            if (useMicroVoxels) {
                                                int sideIndex = ((int)Cube.Side.Left + rotationIndex) % 4;
                                                // Check if we can skip geometry generation due to perfect occlusion by adjacent voxel
                                                VoxelChunk adjacentChunkLeft = neighbourChunks[virtualChunk[vxyz - 1].chunk9Index];
                                                int adjacentRotation = chunk_middle_middle_left[middle_middle_left].GetTextureRotation();
                                                if (adjacentChunkLeft == null || ShouldTreatNeighbourAsOpaque(microVoxels, Cube.Side.Left, rotationIndex, adjacentChunkLeft, middle_middle_left, Cube.Side.Right, adjacentRotation, ref v1l)) {
                                                    int resolvedSecondaryTextureIndex = resolvedTextureIndex;
                                                    if (typeSecondary != null) {
                                                        int texB = typeSecondary.textureSideIndices[rotationIndex].left;
                                                        resolvedSecondaryTextureIndex = texB;
                                                        if (typeSecondary.customTextureProviderLeft != null) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureProviderLeft(texB,
                                                                chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex,
                                                                chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex,
                                                                chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex);
                                                        }
                                                        if (typeSecondary.customTextureVariationsProviderLeft != null && resolvedSecondaryTextureIndex == texB) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureVariationsProviderLeft(texB, chunk.position + pos, rotationIndex);
                                                        }
                                                    }
                                                    AddMicroVoxelGeometry(microVoxelsVertexData[sideIndex].vertices, microVoxelsVertexData[sideIndex].uvs, Cube.normalsLeft, pos, indices, resolvedTextureIndex + extraData, resolvedSecondaryTextureIndex + extraData, microLayout, l0, l1, l2, l3, enableColliders, enableNavMesh && type.navigatable, rotationIndex, tintColor, v1b, v1f, v1l, v1r, v1u, v1d);
                                                }
                                            } else {
                                                if (doNotPackVertices) {
                                                    AddFaceWithAO(Cube.faceVerticesLeft, Cube.normalsLeft, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                                tempChunkColors32.AddRange(faceColors);
#endif
                                                } else {
                                                    greedyOpaque.AddQuad(FaceDirection.Left, z, y, x, tintColor, l0, l1, l2, l3, resolvedTextureIndex + extraData);
                                                }
                                                if (enableColliders) {
                                                    greedyCollider.AddQuad(FaceDirection.Left, z, y, x);
                                                    if (enableNavMesh && type.navigatable) {
                                                        greedyNavMesh.AddQuad(FaceDirection.Left, z, y, x);
                                                    }
                                                }
                                            }
                                        }
                                        // right face
                                        if (v1r < FULL_OPAQUE || useMicroVoxels) {
                                            // Vertex 1
                                            l0 = ComputeVertexLight(lr, v0r, v1br, v0br);
                                            // Vertex 3
                                            l1 = ComputeVertexLight(lr, v2r, v1br, v2br);
                                            // Vertex 5
                                            l2 = ComputeVertexLight(lr, v0r, v1fr, v0fr);
                                            // Vertex 6
                                            l3 = ComputeVertexLight(lr, v2r, v1fr, v2fr);

                                            int textureIndex = type.textureSideIndices[rotationIndex].right;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderRight != null) {
                                                resolvedTextureIndex = type.customTextureProviderRight(textureIndex,
                                                chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex,
                                                chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex,
                                                chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderRight != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderRight(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            if (useMicroVoxels) {
                                                int sideIndex = ((int)Cube.Side.Right + rotationIndex) % 4;
                                                // Check if we can skip geometry generation due to perfect occlusion by adjacent voxel
                                                VoxelChunk adjacentChunkRight = neighbourChunks[virtualChunk[vxyz + 1].chunk9Index];
                                                int adjacentRotation = chunk_middle_middle_right[middle_middle_right].GetTextureRotation();
                                                if (adjacentChunkRight == null || ShouldTreatNeighbourAsOpaque(microVoxels, Cube.Side.Right, rotationIndex, adjacentChunkRight, middle_middle_right, Cube.Side.Left, adjacentRotation, ref v1r)) {
                                                    int resolvedSecondaryTextureIndex = resolvedTextureIndex;
                                                    if (typeSecondary != null) {
                                                        int texB = typeSecondary.textureSideIndices[rotationIndex].right;
                                                        resolvedSecondaryTextureIndex = texB;
                                                        if (typeSecondary.customTextureProviderRight != null) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureProviderRight(texB,
                                                                chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex,
                                                                chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex,
                                                                chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex);
                                                        }
                                                        if (typeSecondary.customTextureVariationsProviderRight != null && resolvedSecondaryTextureIndex == texB) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureVariationsProviderRight(texB, chunk.position + pos, rotationIndex);
                                                        }
                                                    }
                                                    AddMicroVoxelGeometry(microVoxelsVertexData[sideIndex].vertices, microVoxelsVertexData[sideIndex].uvs, Cube.normalsRight, pos, indices, resolvedTextureIndex + extraData, resolvedSecondaryTextureIndex + extraData, microLayout, l0, l1, l2, l3, enableColliders, enableNavMesh && type.navigatable, rotationIndex, tintColor, v1b, v1f, v1l, v1r, v1u, v1d);
                                                }
                                            } else {
                                                if (doNotPackVertices) {
                                                    AddFaceWithAO(Cube.faceVerticesRight, Cube.normalsRight, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                                tempChunkColors32.AddRange(faceColors);
#endif
                                                } else {
                                                    greedyOpaque.AddQuad(FaceDirection.Right, z, y, x, tintColor, l0, l1, l2, l3, resolvedTextureIndex + extraData);
                                                }

                                                if (enableColliders) {
                                                    greedyCollider.AddQuad(FaceDirection.Right, z, y, x);
                                                    if (enableNavMesh && type.navigatable) {
                                                        greedyNavMesh.AddQuad(FaceDirection.Right, z, y, x);
                                                    }
                                                }
                                            }
                                        }
                                        // top face
                                        if (v1u < FULL_OPAQUE || useMicroVoxels) {
                                            // Top face
                                            // Vertex 2
                                            l0 = ComputeVertexLight(lu, v2b, v2l, v2bl);
                                            // Vertex 7
                                            l1 = ComputeVertexLight(lu, v2l, v2f, v2fl);
                                            // Vvertex 3
                                            l2 = ComputeVertexLight(lu, v2b, v2r, v2br);
                                            // Vertex 6
                                            l3 = ComputeVertexLight(lu, v2r, v2f, v2fr);

                                            int textureIndex = type.textureIndexTop;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderTop != null) {
                                                resolvedTextureIndex = type.customTextureProviderTop(textureIndex,
                                                chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex,
                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderTop != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderTop(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            // bevel data
#if USES_BEVEL
                                        if (v1u < FULL_OPAQUE && type.supportsBevel) {
                                            const int LEFT_EDGE_IS_BEVELED = 1 << 14;
                                            const int RIGHT_EDGE_IS_BEVELED = 1 << 15;
                                            const int FORWARD_EDGE_IS_BEVELED = 1 << 16;
                                            const int BACK_EDGE_IS_BEVELED = 1 << 17;
                                            if (v1l < FULL_OPAQUE && chunk_top_middle_left [top_middle_left].opaque < FULL_OPAQUE) extraData += LEFT_EDGE_IS_BEVELED;
                                            if (v1b < FULL_OPAQUE && chunk_top_back_middle [top_back_middle].opaque < FULL_OPAQUE) extraData += BACK_EDGE_IS_BEVELED;
                                            if (v1r < FULL_OPAQUE && chunk_top_middle_right [top_middle_right].opaque < FULL_OPAQUE) extraData += RIGHT_EDGE_IS_BEVELED;
                                            if (v1f < FULL_OPAQUE && chunk_top_forward_middle [top_forward_middle].opaque < FULL_OPAQUE) extraData += FORWARD_EDGE_IS_BEVELED;
                                        }
#endif

                                            if (useMicroVoxels) {
                                                // Check if we can skip geometry generation due to perfect occlusion by adjacent voxel
                                                VoxelChunk adjacentChunkTop = neighbourChunks[virtualChunk[vxyz + V_ONE_Y_ROW].chunk9Index];
                                                int adjacentRotation = chunk_top_middle_middle[top_middle_middle].GetTextureRotation();
                                                if (adjacentChunkTop == null || ShouldTreatNeighbourAsOpaque(microVoxels, Cube.Side.Top, rotationIndex, adjacentChunkTop, top_middle_middle, Cube.Side.Bottom, adjacentRotation, ref v1u)) {
                                                    int resolvedSecondaryTextureIndex = resolvedTextureIndex;
                                                    if (typeSecondary != null) {
                                                        int texB = typeSecondary.textureIndexTop;
                                                        resolvedSecondaryTextureIndex = texB;
                                                        if (typeSecondary.customTextureProviderTop != null) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureProviderTop(texB,
                                                                chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex,
                                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                                chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex);
                                                        }
                                                        if (typeSecondary.customTextureVariationsProviderTop != null && resolvedSecondaryTextureIndex == texB) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureVariationsProviderTop(texB, chunk.position + pos, rotationIndex);
                                                        }
                                                    }
                                                    AddMicroVoxelGeometry(microVoxelsVertexData[(int)Cube.Side.Top].vertices, microVoxelsVertexData[(int)Cube.Side.Top].uvs, Cube.normalsUp, pos, indices, resolvedTextureIndex + extraData, resolvedSecondaryTextureIndex + extraData, microLayout, l0, l1, l2, l3, enableColliders, enableNavMesh && type.navigatable, rotationIndex, tintColor, v1b, v1f, v1l, v1r, v1u, v1d);
                                                }
                                            } else {
                                                if (doNotPackVertices) {
                                                    AddFaceWithAO(Cube.faceVerticesTop, Cube.normalsUp, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                                tempChunkColors32.AddRange(faceColors);
#endif
                                                } else {
                                                    greedyOpaque.AddQuad(FaceDirection.Top, x, z, y, tintColor, l0, l1, l2, l3, resolvedTextureIndex + extraData);
                                                }

                                                if (enableColliders) {
                                                    greedyCollider.AddQuad(FaceDirection.Top, x, z, y);
                                                    if (enableNavMesh && type.navigatable) {
                                                        greedyNavMesh.AddQuad(FaceDirection.Top, x, z, y);
                                                    }
                                                }
                                            }
                                        }
                                        // bottom face
                                        if (v1d < FULL_OPAQUE || useMicroVoxels) {
                                            // Vertex 1
                                            l0 = ComputeVertexLight(ld, v0b, v0r, v0br);
                                            // Vertex 5
                                            l1 = ComputeVertexLight(ld, v0f, v0r, v0fr);
                                            // Vertex 0
                                            l2 = ComputeVertexLight(ld, v0b, v0l, v0bl);
                                            // Vertex 4
                                            l3 = ComputeVertexLight(ld, v0f, v0l, v0fl);

                                            int textureIndex = type.textureIndexBottom;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderBottom != null) {
                                                resolvedTextureIndex = type.customTextureProviderBottom(textureIndex,
                                               chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex,
                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderBottom != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderBottom(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            if (useMicroVoxels) {
                                                // Check if we can skip geometry generation due to perfect occlusion by adjacent voxel
                                                VoxelChunk adjacentChunkBottom = neighbourChunks[virtualChunk[vxyz - V_ONE_Y_ROW].chunk9Index];
                                                int adjacentRotation = chunk_bottom_middle_middle[bottom_middle_middle].GetTextureRotation();
                                                if (adjacentChunkBottom == null || ShouldTreatNeighbourAsOpaque(microVoxels, Cube.Side.Bottom, rotationIndex, adjacentChunkBottom, bottom_middle_middle, Cube.Side.Top, adjacentRotation, ref v1d)) {
                                                    int resolvedSecondaryTextureIndex = resolvedTextureIndex;
                                                    if (typeSecondary != null) {
                                                        int texB = typeSecondary.textureIndexBottom;
                                                        resolvedSecondaryTextureIndex = texB;
                                                        if (typeSecondary.customTextureProviderBottom != null) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureProviderBottom(texB,
                                                                chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex,
                                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                                chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex);
                                                        }
                                                        if (typeSecondary.customTextureVariationsProviderBottom != null && resolvedSecondaryTextureIndex == texB) {
                                                            resolvedSecondaryTextureIndex = typeSecondary.customTextureVariationsProviderBottom(texB, chunk.position + pos, rotationIndex);
                                                        }
                                                    }
                                                    AddMicroVoxelGeometry(microVoxelsVertexData[(int)Cube.Side.Bottom].vertices, microVoxelsVertexData[(int)Cube.Side.Bottom].uvs, Cube.normalsDown, pos, indices, resolvedTextureIndex + extraData, resolvedSecondaryTextureIndex + extraData, microLayout, l0, l1, l2, l3, enableColliders, false, rotationIndex, tintColor, v1b, v1f, v1l, v1r, v1u, v1d);
                                                }
                                            } else {
                                                if (doNotPackVertices) {
                                                    AddFaceWithAO(Cube.faceVerticesBottom, Cube.normalsDown, pos, indices, resolvedTextureIndex + extraData, l0, l1, l2, l3);
#if USES_TINTING
                                                tempChunkColors32.AddRange(faceColors);
#endif
                                                } else {
                                                    greedyOpaque.AddQuad(FaceDirection.Bottom, x, z, y, tintColor, l0, l1, l2, l3, resolvedTextureIndex + extraData);
                                                }

                                                if (enableColliders) {
                                                    greedyCollider.AddQuad(FaceDirection.Bottom, x, z, y);
                                                    // no NavMesh for bottom faces
                                                }
                                            }
                                        }
                                    } else {
                                        // Opaque without AO
                                        float aoBase = 1f;
                                        VoxelPlayGreedyMesherLit greedyOpaqueNoAO = type.greedyMesherLit;

                                        // back face
                                        if (v1b < FULL_OPAQUE) {
                                            float backFaceGI = lb * aoBase;

                                            int textureIndex = type.textureSideIndices[rotationIndex].back;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderBack != null) {
                                                resolvedTextureIndex = type.customTextureProviderBack(textureIndex,
                                                chunk_top_middle_left[top_middle_left].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_right[top_middle_right].typeIndex,
                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                chunk_bottom_middle_left[bottom_middle_left].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_right[bottom_middle_right].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderBack != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderBack(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyOpaqueNoAO.AddQuad(FaceDirection.Back, x, y, z, tintColor, backFaceGI, resolvedTextureIndex + extraData);
                                            if (enableColliders) {
                                                greedyCollider.AddQuad(FaceDirection.Back, x, y, z);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Back, x, y, z);
                                                }
                                            }
                                        }
                                        // forward face
                                        if (v1f < FULL_OPAQUE) {
                                            float frontFaceGI = lf * aoBase;

                                            int textureIndex = type.textureSideIndices[rotationIndex].forward;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderForward != null) {
                                                resolvedTextureIndex = type.customTextureProviderForward(textureIndex,
                                                 chunk_top_middle_right[top_middle_right].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_middle_left[top_middle_left].typeIndex,
                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                chunk_bottom_middle_right[bottom_middle_right].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_middle_left[bottom_middle_left].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderForward != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderForward(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyOpaqueNoAO.AddQuad(FaceDirection.Forward, x, y, z, tintColor, frontFaceGI, resolvedTextureIndex + extraData);
                                            if (enableColliders) {
                                                greedyCollider.AddQuad(FaceDirection.Forward, x, y, z);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Forward, x, y, z);
                                                }
                                            }
                                        }
                                        // left face
                                        if (v1l < FULL_OPAQUE) {
                                            float leftFaceGI = ll * aoBase;

                                            int textureIndex = type.textureSideIndices[rotationIndex].left;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderLeft != null) {
                                                resolvedTextureIndex = type.customTextureProviderLeft(textureIndex,
                                                chunk_top_forward_middle[top_forward_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_back_middle[top_back_middle].typeIndex,
                                                chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex,
                                                chunk_bottom_forward_middle[bottom_forward_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_back_middle[bottom_back_middle].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderLeft != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderLeft(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyOpaqueNoAO.AddQuad(FaceDirection.Left, z, y, x, tintColor, leftFaceGI, resolvedTextureIndex + extraData);
                                            if (enableColliders) {
                                                greedyCollider.AddQuad(FaceDirection.Left, z, y, x);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Left, z, y, x);
                                                }
                                            }
                                        }
                                        // right face
                                        if (v1r < FULL_OPAQUE) {
                                            float rightFaceGI = lr * aoBase;

                                            int textureIndex = type.textureSideIndices[rotationIndex].right;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderRight != null) {
                                                resolvedTextureIndex = type.customTextureProviderRight(textureIndex,
                                                chunk_top_back_middle[top_back_middle].typeIndex, chunk_top_middle_middle[top_middle_middle].typeIndex, chunk_top_forward_middle[top_forward_middle].typeIndex,
                                                chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex,
                                                chunk_bottom_back_middle[bottom_back_middle].typeIndex, chunk_bottom_middle_middle[bottom_middle_middle].typeIndex, chunk_bottom_forward_middle[bottom_forward_middle].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderRight != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderRight(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyOpaqueNoAO.AddQuad(FaceDirection.Right, z, y, x, tintColor, rightFaceGI, resolvedTextureIndex + extraData);
                                            if (enableColliders) {
                                                greedyCollider.AddQuad(FaceDirection.Right, z, y, x);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Right, z, y, x);
                                                }
                                            }
                                        }
                                        // top face
                                        if (v1u < FULL_OPAQUE) {
                                            // Top face
                                            float topFaceGI = lu * aoBase;

                                            int textureIndex = type.textureIndexTop;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderTop != null) {
                                                resolvedTextureIndex = type.customTextureProviderTop(textureIndex,
                                                chunk_middle_forward_left[middle_forward_left].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_right[middle_forward_right].typeIndex,
                                                chunk_middle_middle_left[middle_middle_left].typeIndex, chunk_middle_middle_right[middle_middle_right].typeIndex,
                                                chunk_middle_back_left[middle_back_left].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_right[middle_back_right].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderTop != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderTop(textureIndex, chunk.position + pos, rotationIndex);
                                            }
#if USES_BEVEL
                                        if (v1u < FULL_OPAQUE) {
                                            const int LEFT_EDGE_IS_BEVELED = 1 << 18;
                                            const int RIGHT_EDGE_IS_BEVELED = 1 << 19;
                                            const int FORWARD_EDGE_IS_BEVELED = 1 << 20;
                                            const int BACK_EDGE_IS_BEVELED = 1 << 21;
                                            if (v1l < FULL_OPAQUE && chunk_top_middle_left [top_middle_left].opaque < FULL_OPAQUE) extraData += LEFT_EDGE_IS_BEVELED;
                                            if (v1b < FULL_OPAQUE && chunk_top_back_middle [top_back_middle].opaque < FULL_OPAQUE) extraData += BACK_EDGE_IS_BEVELED;
                                            if (v1r < FULL_OPAQUE && chunk_top_middle_right [top_middle_right].opaque < FULL_OPAQUE) extraData += RIGHT_EDGE_IS_BEVELED;
                                            if (v1f < FULL_OPAQUE && chunk_top_forward_middle [top_forward_middle].opaque < FULL_OPAQUE) extraData += FORWARD_EDGE_IS_BEVELED;
                                        }
                                        AddFaceWithAO (Cube.faceVerticesTop, Cube.normalsUp, pos, indices, resolvedTextureIndex + extraData, topFaceGI, topFaceGI, topFaceGI, topFaceGI);
#else
                                            greedyOpaqueNoAO.AddQuad(FaceDirection.Top, x, z, y, tintColor, topFaceGI, resolvedTextureIndex + extraData);
#endif

                                            if (enableColliders) {
                                                greedyCollider.AddQuad(FaceDirection.Top, x, z, y);
                                                if (enableNavMesh && type.navigatable) {
                                                    greedyNavMesh.AddQuad(FaceDirection.Top, x, z, y);
                                                }
                                            }
                                        }
                                        // bottom face
                                        if (v1d < FULL_OPAQUE) {
                                            float bottomFaceGI = ld * aoBase;

                                            int textureIndex = type.textureIndexBottom;
                                            int resolvedTextureIndex = textureIndex;
                                            if (type.customTextureProviderBottom != null) {
                                                resolvedTextureIndex = type.customTextureProviderBottom(textureIndex,
                                                chunk_middle_forward_right[middle_forward_right].typeIndex, chunk_middle_forward_middle[middle_forward_middle].typeIndex, chunk_middle_forward_left[middle_forward_left].typeIndex,
                                                chunk_middle_middle_right[middle_middle_right].typeIndex, chunk_middle_middle_left[middle_middle_left].typeIndex,
                                                chunk_middle_back_right[middle_back_right].typeIndex, chunk_middle_back_middle[middle_back_middle].typeIndex, chunk_middle_back_left[middle_back_left].typeIndex);
                                            }
                                            if (type.customTextureVariationsProviderBottom != null && resolvedTextureIndex == textureIndex) {
                                                resolvedTextureIndex = type.customTextureVariationsProviderBottom(textureIndex, chunk.position + pos, rotationIndex);
                                            }

                                            greedyOpaqueNoAO.AddQuad(FaceDirection.Bottom, x, z, y, tintColor, bottomFaceGI, resolvedTextureIndex + extraData);
                                            if (enableColliders) {
                                                greedyCollider.AddQuad(FaceDirection.Bottom, x, z, y);
                                            }
                                        }
                                    }
                                    //}
                                }
                                break;
                        }
                    }
                }
            }

            meshJobs[jobIndex].chunk = chunk;
            meshJobs[jobIndex].totalVoxels = chunkVoxelCount;

            if (voxelSignature != chunk.voxelSignature) {
                chunk.voxelSignature = voxelSignature;
                meshJobs[jobIndex].needsColliderRebuild = true;
            }

            if (chunkVoxelCount == 0) {
                return;
            }

            if (enableColliders) {
                if (meshJobs[jobIndex].needsColliderRebuild) {
                    greedyCollider.FlushTriangles(meshColliderVertices, meshColliderIndices);
                    if (enableNavMesh) {
                        greedyNavMesh.FlushTriangles(navMeshVertices, navMeshIndices);
                    }
                } else {
                    greedyCollider.Clear();
                    if (enableNavMesh) {
                        greedyNavMesh.Clear();
                    }
                }
            }

            // Flush greedy meshers and annotate submesh count
            int subMeshCount = 0;
            for (int k = 0; k < VoxelPlayEnvironment.MAX_MATERIALS_PER_CHUNK; k++) {
                if (env.renderingMaterials[k].greedyMesherLitAO != null) {
                    env.renderingMaterials[k].greedyMesherLitAO.FlushTriangles(tempChunkVertices, indexBuffers[k], tempChunkUV0, tempChunkNormals, tempChunkColors32);
                }
                if (env.renderingMaterials[k].greedyMesherLit != null) {
                    env.renderingMaterials[k].greedyMesherLit.FlushTriangles(tempChunkVertices, indexBuffers[k], tempChunkUV0, tempChunkNormals, tempChunkColors32);
                }
                if (indexBuffers[k].Count > 0) {
                    subMeshCount++;
                }
            }

            meshJobs[jobIndex].subMeshCount = subMeshCount;

            meshJobs[jobIndex].mivs = mivs;
        }

        /// <summary>
        /// Checks if microvoxel geometry generation should be skipped for a face due to perfect occlusion by adjacent voxel
        /// </summary>
        bool ShouldTreatNeighbourAsOpaque (MicroVoxels mv, Cube.Side currentFace, int rotationIndex, VoxelChunk adjacentChunk, int adjacentVoxelIndex, Cube.Side adjacentFace, int adjacentRotation, ref int opaque) {
            if ((object)adjacentChunk == null || !adjacentChunk.usesMicroVoxels) return true;

            if (!adjacentChunk.microVoxels.TryGetValue(adjacentVoxelIndex, out MicroVoxels adjacentMV)) return true;
            if (MicroVoxels.CompareFaces(mv, adjacentMV, currentFace, adjacentFace, rotationIndex, adjacentRotation)) {
                opaque = FULL_OPAQUE;
            }
            return true;
        }

        void AddMicroVoxelGeometry (List<Vector3> vertices, List<Vector2> uvs, Vector3[] normals, Vector3 pos, List<int> indices, int textureIndexA, int textureIndexB, MicroVoxelLayout layout, float w0, float w1, float w2, float w3, bool addColliderData, bool addNavMeshData, int rotationIndex, Color32 tintColor, int v1b, int v1f, int v1l, int v1r, int v1u, int v1d) {
            Vector4 uv;

            int index = tempChunkVertices.Count;
            int meshVerticesIndex = meshColliderVertices.Count;
            int navMeshVerticesIndex = addNavMeshData ? navMeshVertices.Count : 0;
            int verticesCount = vertices.Count;
            int verticesAdded = 0;
            Vector4 rotationFactors = Cube.rotatedVertexFactors[rotationIndex];
            Vector3 normal = normals[0];

            // Inline per-quad decision (no precomputation arrays)
            bool isSide = Mathf.Abs(normals[0].y) < 0.5f;
            int currentTexId = textureIndexA;
            bool currentUseLocal = false;

            for (int v = 0; v < verticesCount; v++) {
                Vector3 vertPos = vertices[v];
                float x = vertPos.x, z = vertPos.z;
                vertPos.x = x * rotationFactors.x + z * rotationFactors.y;
                vertPos.z = x * rotationFactors.z + z * rotationFactors.w;

                // face occlusion
                if (normal.x < -0.5f) {
                    if (v1l == FULL_OPAQUE && vertPos.x < -0.499) continue;
                } else if (normal.x > 0.5f) {
                    if (v1r == FULL_OPAQUE && vertPos.x > 0.499) continue;
                } else if (normal.z < -0.5f) {
                    if (v1b == FULL_OPAQUE && vertPos.z < -0.499) continue;
                } else if (normal.z > 0.5f) {
                    if (v1f == FULL_OPAQUE && vertPos.z > 0.499) continue;
                } else if (normal.y < -0.5f) {
                    if (v1d == FULL_OPAQUE && vertPos.y < -0.499) continue;
                } else if (normal.y > 0.5f) {
                    if (v1u == FULL_OPAQUE && vertPos.y > 0.499) continue;
                }

                // Recompute decision at the start of each quad (v%4==0)
                if ((v & 3) == 0) {
                    int texIdx = textureIndexA;
                    bool useLocal = false;
                    float quadY = (vertices[v].y + vertices[v + 1].y + vertices[v + 2].y + vertices[v + 3].y) * 0.25f;
                    if (layout == MicroVoxelLayout.Slabs) {
                        if (quadY > 0.02f) {
                            texIdx = textureIndexB;
                            useLocal = true;
                        }
                    } else if (layout == MicroVoxelLayout.TopCap && isSide) {
                        if (quadY < 0f) {
                            texIdx = textureIndexB;
                            useLocal = true;
                        }
                    }
                    currentTexId = texIdx;
                    currentUseLocal = useLocal;
                }

                vertPos.x += pos.x;
                vertPos.z += pos.z;
                vertPos.y += pos.y;

                verticesAdded++;
                tempChunkVertices.Add(vertPos);

                uv = uvs[v];
                bool useLocalUV = isSide ? currentUseLocal : true;
                if (useLocalUV) {
                    if (layout == MicroVoxelLayout.TopCap && isSide) {
                        uv.y += 0.5f;
                    }
                    uv.z = currentTexId | VP_FLAG_LOCAL_UV;
                } else {
                    uv.z = currentTexId;
                }
                uv.w = GetInterpolatedUVLight(w0, w1, w2, w3, uv.x, uv.y);
                tempChunkUV0.Add(uv);
                if (addColliderData) {
                    meshColliderVertices.Add(vertPos);
                    if (addNavMeshData) {
                        navMeshVertices.Add(vertPos);
                    }
                }
#if USES_TINTING
                    tempChunkColors32.Add(tintColor);
#endif
            }

            for (int k = 0; k < verticesAdded; k += 4) {
                tempChunkNormals.AddRange(normals);
                w0 = tempChunkUV0[index].w;
                w1 = tempChunkUV0[index + 1].w;
                w2 = tempChunkUV0[index + 2].w;
                w3 = tempChunkUV0[index + 3].w;
                if (w0 + w3 > w1 + w2) {
                    indices.Add(index);
                    indices.Add(index + 1);
                    indices.Add(index + 3);
                    indices.Add(index + 3);
                    indices.Add(index + 2);
                    indices.Add(index);
                    if (addColliderData) {
                        meshColliderIndices.Add(meshVerticesIndex);
                        meshColliderIndices.Add(meshVerticesIndex + 1);
                        meshColliderIndices.Add(meshVerticesIndex + 3);
                        meshColliderIndices.Add(meshVerticesIndex + 3);
                        meshColliderIndices.Add(meshVerticesIndex + 2);
                        meshColliderIndices.Add(meshVerticesIndex);
                        if (addNavMeshData) {
                            navMeshIndices.Add(navMeshVerticesIndex);
                            navMeshIndices.Add(navMeshVerticesIndex + 1);
                            navMeshIndices.Add(navMeshVerticesIndex + 3);
                            navMeshIndices.Add(navMeshVerticesIndex + 3);
                            navMeshIndices.Add(navMeshVerticesIndex + 2);
                            navMeshIndices.Add(navMeshVerticesIndex);
                        }
                    }
                } else {
                    indices.Add(index);
                    indices.Add(index + 1);
                    indices.Add(index + 2);
                    indices.Add(index + 3);
                    indices.Add(index + 2);
                    indices.Add(index + 1);
                    if (addColliderData) {
                        meshColliderIndices.Add(meshVerticesIndex);
                        meshColliderIndices.Add(meshVerticesIndex + 1);
                        meshColliderIndices.Add(meshVerticesIndex + 2);
                        meshColliderIndices.Add(meshVerticesIndex + 3);
                        meshColliderIndices.Add(meshVerticesIndex + 2);
                        meshColliderIndices.Add(meshVerticesIndex + 1);
                        if (addNavMeshData) {
                            navMeshIndices.Add(navMeshVerticesIndex);
                            navMeshIndices.Add(navMeshVerticesIndex + 1);
                            navMeshIndices.Add(navMeshVerticesIndex + 2);
                            navMeshIndices.Add(navMeshVerticesIndex + 3);
                            navMeshIndices.Add(navMeshVerticesIndex + 2);
                            navMeshIndices.Add(navMeshVerticesIndex + 1);
                        }
                    }
                }
                index += 4;
                meshVerticesIndex += 4;
                navMeshVerticesIndex += 4;
            }
        }

        float GetInterpolatedUVLight (float w0, float w1, float w2, float w3, float u, float v) {
            float s0 = w0 % 512;
            float s1 = w1 % 512;
            float s2 = w2 % 512;
            float s3 = w3 % 512;
            float t0 = (int)w0 >> 10;
            float t1 = (int)w1 >> 10;
            float t2 = (int)w2 >> 10;
            float t3 = (int)w3 >> 10;

            // interpolate torch light
            float t_left = (1 - v) * t0 + v * t1;
            float t_right = (1 - v) * t2 + v * t3;
            float t = (1 - u) * t_left + u * t_right;
            t = (int)t << 10;

            // interpolate sun light
            float s_left = (1 - v) * s0 + v * s1;
            float s_right = (1 - v) * s2 + v * s3;
            float s = (1 - u) * s_left + u * s_right;

            return t + s;
        }

        void AddFaceWithAO (Vector3[] faceVertices, Vector3[] normals, Vector3 pos, List<int> indices, int textureIndex, float w0, float w1, float w2, float w3) {
            int index = tempChunkVertices.Count;
            for (int v = 0; v < 4; v++) {
                Vector3 vertPos = faceVertices[v];
                vertPos.x += pos.x;
                vertPos.y += pos.y;
                vertPos.z += pos.z;
                tempChunkVertices.Add(vertPos);
            }
            tempChunkNormals.AddRange(normals);

            // Flip triangle so AO looks good at all corners
            if (w0 + w3 > w1 + w2) {
                indices.Add(index);
                indices.Add(index + 1);
                indices.Add(index + 3);
                indices.Add(index + 3);
                indices.Add(index + 2);
                indices.Add(index);
            } else {
                indices.Add(index);
                indices.Add(index + 1);
                indices.Add(index + 2);
                indices.Add(index + 3);
                indices.Add(index + 2);
                indices.Add(index + 1);
            }

            Vector4 v4;
            v4.x = 0;
            v4.y = 0;
            v4.z = textureIndex;
            v4.w = w0;
            tempChunkUV0.Add(v4);
            v4.y = 1f;
            v4.w = w1;
            tempChunkUV0.Add(v4);
            v4.x = 1f;
            v4.y = 0;
            v4.w = w2;
            tempChunkUV0.Add(v4);
            v4.y = 1f;
            v4.w = w3;
            tempChunkUV0.Add(v4);
        }

        void AddFaceWater (Vector3[] faceVertices, Vector3[] normals, Vector3 pos, List<int> indices, int textureIndex, int w, int h0, int h1, int h2, int h3) {
            int index = tempChunkVertices.Count;
            Vector3 vertPos;
            // vertices
            vertPos.x = faceVertices[0].x + pos.x;
            vertPos.y = h0 / 15f + pos.y;
            vertPos.z = faceVertices[0].z + pos.z;
            tempChunkVertices.Add(vertPos);
            vertPos.x = faceVertices[1].x + pos.x;
            vertPos.y = h1 / 15f + pos.y;
            vertPos.z = faceVertices[1].z + pos.z;
            tempChunkVertices.Add(vertPos);
            vertPos.x = faceVertices[2].x + pos.x;
            vertPos.y = h2 / 15f + pos.y;
            vertPos.z = faceVertices[2].z + pos.z;
            tempChunkVertices.Add(vertPos);
            vertPos.x = faceVertices[3].x + pos.x;
            vertPos.y = h3 / 15f + pos.y;
            vertPos.z = faceVertices[3].z + pos.z;
            tempChunkVertices.Add(vertPos);
            tempChunkNormals.AddRange(normals);

            // indices
            indices.Add(index);
            indices.Add(index + 1);
            indices.Add(index + 2);
            indices.Add(index + 3);
            indices.Add(index + 2);
            indices.Add(index + 1);
            Vector4 v4 = new Vector4(0f, 0f, textureIndex, w);
            tempChunkUV0.Add(v4);
            v4.y = 1f;
            tempChunkUV0.Add(v4);
            v4.x = 1f;
            v4.y = 0f;
            tempChunkUV0.Add(v4);
            v4.y = 1f;
            tempChunkUV0.Add(v4);
        }

        void AddFaceVegetation (Vector3[] faceVertices, Vector3 pos, List<int> indices, int textureIndex, float w, float height, bool randomOffset) {
            int index = tempChunkVertices.Count;

            // Add random displacement and elevation
            Vector3 aux;
            if (randomOffset) {
                aux = pos;
                float random = WorldRand.GetValue(aux.x, aux.z);
                pos.x += random * 0.5f - 0.25f;
                aux.x += 1f;
                random = WorldRand.GetValue(aux);
                pos.z += random * 0.5f - 0.25f;
                pos.y -= random * 0.1f;
            }
            for (int v = 0; v < 4; v++) {
                aux.x = faceVertices[v].x + pos.x;
                float h = faceVertices[v].y;
                if (h > 0) h *= height;
                aux.y = h + pos.y;
                aux.z = faceVertices[v].z + pos.z;
                tempChunkVertices.Add(aux);
                tempChunkNormals.Add(Misc.vector3zero);
            }
            indices.Add(index);
            indices.Add(index + 1);
            indices.Add(index + 2);
            indices.Add(index + 3);
            indices.Add(index + 2);
            indices.Add(index + 1);
            Vector4 v4 = new Vector4(0, 0, textureIndex, w);
            tempChunkUV0.Add(v4);
            v4.y = 1f;
            tempChunkUV0.Add(v4);
            v4.x = 1f;
            v4.y = 0f;
            tempChunkUV0.Add(v4);
            v4.y = 1f;
            tempChunkUV0.Add(v4);
        }


        void AddFaceDenseLeaves (Vector3[] faceVertices, Vector3 pos, List<int> indices, int textureIndex, float w, float random) {
            int index = tempChunkVertices.Count;

            // Add random displacement and elevation
            pos.x += random * 0.5f - 0.25f;
            pos.z += random * 0.5f - 0.25f;
            pos.y += random * 0.5f - 0.25f;
            for (int v = 0; v < 4; v++) {
                Vector3 aux = faceVertices[v];
                aux.x += pos.x;
                aux.y += pos.y;
                aux.z += pos.z;
                tempChunkVertices.Add(aux);
                tempChunkNormals.Add(Misc.vector3zero);
            }
            indices.Add(index);
            indices.Add(index + 1);
            indices.Add(index + 2);
            indices.Add(index + 3);
            indices.Add(index + 2);
            indices.Add(index + 1);
            Vector4 v4 = new Vector4(0, 0, textureIndex, w);
            tempChunkUV0.Add(v4);
            v4.y = 1f;
            tempChunkUV0.Add(v4);
            v4.x = 1f;
            v4.y = 0f;
            tempChunkUV0.Add(v4);
            v4.y = 1f;
            tempChunkUV0.Add(v4);
        }

        void AddFaceTransparent (Vector3[] faceVertices, Vector3[] normals, Vector3 pos, List<int> indices, int textureIndex, float light, float alpha) {
            int index = tempChunkVertices.Count;
            for (int v = 0; v < 4; v++) {
                Vector3 vertPos = faceVertices[v];
                vertPos.x += pos.x;
                vertPos.y += pos.y;
                vertPos.z += pos.z;
                tempChunkVertices.Add(vertPos);
            }
            tempChunkNormals.AddRange(normals);


            indices.Add(index);
            indices.Add(index + 1);
            indices.Add(index + 3);
            indices.Add(index + 3);
            indices.Add(index + 2);
            indices.Add(index + 0);

            Vector4 v4 = new Vector4(0, alpha, textureIndex, light);
            tempChunkUV0.Add(v4);
            v4.x = 1f;
            tempChunkUV0.Add(v4);
            v4.x = 2f;
            tempChunkUV0.Add(v4);
            v4.x = 3f;
            tempChunkUV0.Add(v4);
        }

        /// <summary>
        /// Ensures that the prototype exists for the given MicroVoxels object.
        /// This is needed before accessing vertex data or calling IsFaceFullyCovered.
        /// </summary>
        void EnsurePrototypeExists (MicroVoxels mv) {
            if (mv.prototype == null || mv.needsMeshDataUpdate) {
                if (mv.needsMeshDataUpdate) {
                    mv.needsMeshDataUpdate = false;
                }
                ulong mvHash = mv.GetGridHashCode();
                if (!env.microVoxelsPrototypes.TryGetValue(mvHash, out mv.prototype)) {
                    microVoxelMesher.UpdateMeshData(mv);
                    env.microVoxelsPrototypes[mvHash] = mv.prototype;
                }
            }
        }

    }
}
