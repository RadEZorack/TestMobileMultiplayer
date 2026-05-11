using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelDefinition : ScriptableObject {

        const string MICROVOXELS_MESH_NAME = "Microvoxels";

        /// <summary>
        /// Optional data that represents microvoxels shape. This is only used when calling VoxelPlace methods.
        /// </summary>
        public MicroVoxels microVoxels;

        public bool usesMicroVoxels => supportsMicroVoxels && microVoxels != null && !microVoxels.isEmpty;

        // If the voxel definition supports microvoxels
        public bool supportsMicroVoxels => renderType.supportsMicroVoxels() && !placeOnWall;

        [NonSerialized]
        public Mesh microVoxelsPreviewMesh;

#if UNITY_EDITOR
        [NonSerialized]
        public GameObject microVoxelsPreviewGO;
#endif

        ulong microVoxelsPreviewHash;

        void OnEnable() {
            if (microVoxels != null) microVoxels.isShared = true;
        }

        public Mesh GetMicroVoxelsMesh () {

            if (microVoxels == null) return null;

            ulong currentHash = microVoxels.GetGridHashCode();
            if (microVoxelsPreviewMesh != null) {
                if (currentHash == microVoxelsPreviewHash) return microVoxelsPreviewMesh;
            }

            microVoxelsPreviewHash = currentHash;

            // Reuse the parameterized builder without encoding texture index for preview/editor
            Mesh mesh = GetMicroVoxelsMesh(microVoxels, 0, null, false);
            if (mesh == null) return null;

            microVoxelsPreviewMesh = mesh;
            microVoxelsPreviewMesh.name = MICROVOXELS_MESH_NAME;
            return microVoxelsPreviewMesh;
        }

        public Mesh GetMicroVoxelsMesh (MicroVoxels mv, int rotationIndex, VoxelDefinition secondaryType, bool encodeTextureIndex) {
            if (mv == null) return null;

            // Ensure prototype exists
            MeshingThreadMicroVoxels mesher = new MeshingThreadMicroVoxels();
            if (mv.prototype == null || mv.needsMeshDataUpdate) {
                mv.needsMeshDataUpdate = false;
                mesher.UpdateMeshData(mv);
            }
            MicroVoxelsPrototype proto = mv.prototype;
            if (proto == null) return null;

            const int VP_FLAG_LOCAL_UV = 1 << 23;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector4> uvs = new List<Vector4>();
            List<int> triangles = new List<int>();
            int vertexOffset = 0;

            // Primary texture indices (rotated for sides)
            int texBack = textureSideIndices != null ? textureSideIndices[rotationIndex].back : textureIndexSide;
            int texRight = textureSideIndices != null ? textureSideIndices[rotationIndex].right : textureIndexSide;
            int texForward = textureSideIndices != null ? textureSideIndices[rotationIndex].forward : textureIndexSide;
            int texLeft = textureSideIndices != null ? textureSideIndices[rotationIndex].left : textureIndexSide;
            int texTop = textureIndexTop;
            int texBottom = textureIndexBottom;

            // Secondary texture indices
            int texBackB = texBack, texRightB = texRight, texForwardB = texForward, texLeftB = texLeft, texTopB = texTop, texBottomB = texBottom;
            if ((object)secondaryType != null && secondaryType.textureSideIndices != null) {
                texBackB = secondaryType.textureSideIndices[rotationIndex].back;
                texRightB = secondaryType.textureSideIndices[rotationIndex].right;
                texForwardB = secondaryType.textureSideIndices[rotationIndex].forward;
                texLeftB = secondaryType.textureSideIndices[rotationIndex].left;
                texTopB = secondaryType.textureIndexTop;
                texBottomB = secondaryType.textureIndexBottom;
            }

            void AppendSide(int sideIndexForVertices, int texA, int texB, bool isSide) {
                var verts = proto.sidesVertexData[sideIndexForVertices].vertices;
                var uv2 = proto.sidesVertexData[sideIndexForVertices].uvs;
                int sideVertexCount = verts.Count;
                for (int i = 0; i < sideVertexCount; i += 4) {
                    // Decide texture for this quad based on layout
                    int currentTex = texA;
                    bool useLocal = false;
                    if (encodeTextureIndex) {
                        float quadY = (verts[i].y + verts[i + 1].y + verts[i + 2].y + verts[i + 3].y) * 0.25f;
                        if (mv.layout == MicroVoxelLayout.Slabs) {
                            if (quadY > 0.02f) { currentTex = texB; useLocal = true; }
                        } else if (mv.layout == MicroVoxelLayout.TopCap && isSide) {
                            if (quadY < 0f) { currentTex = texB; useLocal = true; }
                        }
                    }

                    // Add quad vertices
                    for (int k = 0; k < 4; k++) {
                        vertices.Add(verts[i + k]);
                        Vector2 u = uv2[i + k];
                        Vector4 uv;
                        uv.x = u.x;
                        uv.y = u.y;
                        if (encodeTextureIndex) {
                            bool useLocalUV = isSide ? useLocal : true;
                            if (useLocalUV) {
                                if (mv.layout == MicroVoxelLayout.TopCap && isSide) {
                                    uv.y += 0.5f;
                                }
                                uv.z = currentTex | VP_FLAG_LOCAL_UV;
                            } else {
                                uv.z = currentTex;
                            }
                            uv.w = 15f;
                        } else {
                            uv.z = 0f; uv.w = 0f;
                        }
                        uvs.Add(uv);
                    }

                    // Triangles
                    triangles.Add(vertexOffset + 0);
                    triangles.Add(vertexOffset + 1);
                    triangles.Add(vertexOffset + 3);
                    triangles.Add(vertexOffset + 3);
                    triangles.Add(vertexOffset + 2);
                    triangles.Add(vertexOffset + 0);
                    vertexOffset += 4;
                }
            }

            // Sides with rotation-applied vertex data selection
            AppendSide(((int)Cube.Side.Back + rotationIndex) % 4, texBack, texBackB, true);
            AppendSide(((int)Cube.Side.Right + rotationIndex) % 4, texRight, texRightB, true);
            AppendSide(((int)Cube.Side.Forward + rotationIndex) % 4, texForward, texForwardB, true);
            AppendSide(((int)Cube.Side.Left + rotationIndex) % 4, texLeft, texLeftB, true);
            // Top & Bottom
            AppendSide((int)Cube.Side.Top, texTop, texTopB, false);
            AppendSide((int)Cube.Side.Bottom, texBottom, texBottomB, false);

            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.name = MICROVOXELS_MESH_NAME;
            return mesh;
        }

    }

}