using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VoxelPlay {

    public class MeshingThreadMicroVoxels {

        struct Cuboid {
            public Vector3 min, max;
            public bool deleted;
            public byte layoutGroup;
        }
        Cuboid[] cuboids;

        struct Face {
            public Vector3 center;
            public Vector3 size;
            public Vector3[] vertices;
            public Cube.Side side;

            public Face (Vector3 center, Vector3 size, Vector3[] vertices, Cube.Side side) {
                this.center = center;
                this.size = size;
                this.vertices = vertices;
                this.side = side;
            }

            public static bool operator == (Face f1, Face f2) {
                return f1.size == f2.size && f1.center == f2.center;
            }

            public static bool operator != (Face f1, Face f2) {
                return f1.size != f2.size || f1.center != f2.center;
            }

            public override bool Equals (object obj) {
                if (obj == null || !(obj is Face))
                    return false;
                Face other = (Face)obj;
                return size == other.size && center == other.center;
            }

            public override int GetHashCode () {
                unchecked {
                    int hash = 23;
                    hash = hash * 31 + center.GetHashCode();
                    hash = hash * 31 + size.GetHashCode();
                    return hash;
                }
            }
        }
        readonly HashSet<Face> faces = new HashSet<Face>();

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        void Encapsulate (int k, int j) {
            if (cuboids[j].min.x < cuboids[k].min.x) cuboids[k].min.x = cuboids[j].min.x;
            else if (cuboids[j].max.x > cuboids[k].max.x) cuboids[k].max.x = cuboids[j].max.x;
            if (cuboids[j].min.y < cuboids[k].min.y) cuboids[k].min.y = cuboids[j].min.y;
            else if (cuboids[j].max.y > cuboids[k].max.y) cuboids[k].max.y = cuboids[j].max.y;
            if (cuboids[j].min.z < cuboids[k].min.z) cuboids[k].min.z = cuboids[j].min.z;
            else if (cuboids[j].max.z > cuboids[k].max.z) cuboids[k].max.z = cuboids[j].max.z;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        void RemoveDuplicateOrAddFace (HashSet<Face> faces, Face face) {
            if (!faces.Remove(face)) {
                faces.Add(face);
            }
        }

        public void UpdateMeshData (MicroVoxels mv) {

            if (cuboids == null) {
                cuboids = new Cuboid[MicroVoxels.COUNT_PER_VOXEL];
            }
            Cuboid cuboid = new Cuboid();
            int cuboidsCount = 0;

            for (int index = 0; index < MicroVoxels.COUNT_PER_VOXEL; index++) {
                if (mv.IsOccupied(index)) {
                    int px = index & MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
                    int py = index / MicroVoxels.COUNT_PER_FACE;
                    int pz = (index / MicroVoxels.COUNT_PER_AXIS) & MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
                    cuboid.min.x = px - MicroVoxels.COUNT_PER_AXIS_HALF;
                    cuboid.min.y = py - MicroVoxels.COUNT_PER_AXIS_HALF;
                    cuboid.min.z = pz - MicroVoxels.COUNT_PER_AXIS_HALF;
                    cuboid.max.x = cuboid.min.x + 1;
                    cuboid.max.y = cuboid.min.y + 1;
                    cuboid.max.z = cuboid.min.z + 1;

                    if (mv.layout == MicroVoxelLayout.Slabs && py >= MicroVoxels.COUNT_PER_AXIS_HALF) {
                        cuboid.layoutGroup = 1;
                    }

                    cuboids[cuboidsCount++] = cuboid;
                }
            }

            // Optimization 1: Fusion adjacent cuboids
            bool repeat = true;
            while (repeat) {
                repeat = false;
                for (int k = 0; k < cuboidsCount; k++) {
                    if (cuboids[k].deleted)
                        continue;
                    Vector3 f1min = cuboids[k].min;
                    Vector3 f1max = cuboids[k].max;
                    for (int j = k + 1; j < cuboidsCount; j++) {
                        if (cuboids[j].deleted)
                            continue;
                        bool touching = false;
                        Vector3 f2min = cuboids[j].min;
                        Vector3 f2max = cuboids[j].max;
                        // Touching back or forward faces?
                        if (f1min.x == f2min.x && f1max.x == f2max.x && f1min.y == f2min.y && f1max.y == f2max.y) {
                            touching = f1min.z == f2max.z || f1max.z == f2min.z;
                            // ... left or right faces?
                        } else if (f1min.z == f2min.z && f1max.z == f2max.z && f1min.y == f2min.y && f1max.y == f2max.y) {
                            touching = f1min.x == f2max.x || f1max.x == f2min.x;
                            // ... top or bottom faces?
                        } else if (f1min.x == f2min.x && f1max.x == f2max.x && f1min.z == f2min.z && f1max.z == f2max.z) {
                            touching = f1min.y == f2max.y || f1max.y == f2min.y;
                        }
                        if (touching && cuboids[k].layoutGroup == cuboids[j].layoutGroup) {
                            Encapsulate(k, j);
                            cuboids[j].deleted = true;
                            repeat = true;
                            break;
                        }
                    }
                }
            }

            // Pack non-deleted cuboids
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < cuboidsCount; readIndex++) {
                if (!cuboids[readIndex].deleted) {
                    if (writeIndex != readIndex) {
                        cuboids[writeIndex] = cuboids[readIndex];
                    }
                    writeIndex++;
                }
            }
            cuboidsCount = writeIndex;

            // Fragment cuboids into faces and remove duplicates
            faces.Clear();

            for (int k = 0; k < cuboidsCount; k++) {
                Vector3 min = cuboids[k].min;
                Vector3 max = cuboids[k].max;
                Vector3 size = max - min;
                Face top = new Face(new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f), new Vector3(size.x, 0, size.z), Cube.faceVerticesTop, Cube.Side.Top);
                RemoveDuplicateOrAddFace(faces, top);
                Face bottom = new Face(new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f), new Vector3(size.x, 0, size.z), Cube.faceVerticesBottom, Cube.Side.Bottom);
                RemoveDuplicateOrAddFace(faces, bottom);
                Face left = new Face(new Vector3(min.x, (min.y + max.y) * 0.5f, (min.z + max.z) * 0.5f), new Vector3(0, size.y, size.z), Cube.faceVerticesLeft, Cube.Side.Left);
                RemoveDuplicateOrAddFace(faces, left);
                Face right = new Face(new Vector3(max.x, (min.y + max.y) * 0.5f, (min.z + max.z) * 0.5f), new Vector3(0, size.y, size.z), Cube.faceVerticesRight, Cube.Side.Right);
                RemoveDuplicateOrAddFace(faces, right);
                Face back = new Face(new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, min.z), new Vector3(size.x, size.y, 0), Cube.faceVerticesBack, Cube.Side.Back);
                RemoveDuplicateOrAddFace(faces, back);
                Face forward = new Face(new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.z), new Vector3(size.x, size.y, 0), Cube.faceVerticesForward, Cube.Side.Forward);
                RemoveDuplicateOrAddFace(faces, forward);
            }

            // Create geometry & uv mapping
            // Check if prototype already exists to preserve face masks
            MicroVoxelsPrototype prototype = mv.prototype;
            if (prototype == null) {
                prototype = mv.prototype = new MicroVoxelsPrototype();
            } else {
                // Clear existing vertex data but preserve face hashes
                for (int k = 0; k < 6; k++) {
                    prototype.sidesVertexData[k].vertices.Clear();
                    prototype.sidesVertexData[k].uvs.Clear();
                }
                prototype.bounds = Misc.bounds1;
            }
            int estimatedVerticesPerSide = faces.Count * 4;
            for (int k = 0; k < 6; k++) {
                var vertexList = prototype.sidesVertexData[k].vertices;
                var uvList = prototype.sidesVertexData[k].uvs;

                // Preallocate capacity (lists are already cleared above if reusing prototype)
                if (vertexList.Capacity < estimatedVerticesPerSide) {
                    vertexList.Capacity = estimatedVerticesPerSide;
                }
                if (uvList.Capacity < estimatedVerticesPerSide) {
                    uvList.Capacity = estimatedVerticesPerSide;
                }
            }

            float whitePixelsGapExt = VoxelPlayGreedyCommon.PADDING2;
            foreach (Face face in faces) {
                Vector3 faceVertex;
                Vector2 faceUV;
                int sideIndex = (int)face.side;
                for (int j = 0; j < 4; j++) {
                    faceVertex.x = (face.center.x + face.vertices[j].x * face.size.x) / MicroVoxels.COUNT_PER_AXIS;
                    faceVertex.y = (face.center.y + face.vertices[j].y * face.size.y) / MicroVoxels.COUNT_PER_AXIS;
                    faceVertex.z = (face.center.z + face.vertices[j].z * face.size.z) / MicroVoxels.COUNT_PER_AXIS;
                    switch (sideIndex) {
                        case (int)Cube.Side.Top:
                            faceUV.x = faceVertex.x + 0.5f;
                            faceUV.y = faceVertex.z + 0.5f;
                            break;
                        case (int)Cube.Side.Bottom:
                            faceUV.x = faceVertex.x + 0.5f;
                            faceUV.y = 1f - (faceVertex.z + 0.5f);
                            break;
                        case (int)Cube.Side.Forward:
                            faceUV.x = 1f - (faceVertex.x + 0.5f);
                            faceUV.y = faceVertex.y + 0.5f;
                            break;
                        case (int)Cube.Side.Back:
                            faceUV.x = faceVertex.x + 0.5f;
                            faceUV.y = faceVertex.y + 0.5f;
                            break;
                        case (int)Cube.Side.Left:
                            faceUV.x = 1f - (faceVertex.z + 0.5f);
                            faceUV.y = faceVertex.y + 0.5f;
                            break;
                        //case (int)Cube.Side.Right:
                        default:
                            faceUV.x = faceVertex.z + 0.5f;
                            faceUV.y = faceVertex.y + 0.5f;
                            break;
                    }
                    prototype.sidesVertexData[sideIndex].uvs.Add(faceUV);

                    faceVertex.x += face.vertices[j].x * whitePixelsGapExt;
                    faceVertex.y += face.vertices[j].y * whitePixelsGapExt;
                    faceVertex.z += face.vertices[j].z * whitePixelsGapExt;
                    prototype.sidesVertexData[sideIndex].vertices.Add(faceVertex);

                }
            }

            // Calculate normalized bounds by iterating prototype vertices (fast, reflects actual rendered geometry)
            {
                Vector3 min = Misc.vector3half;
                Vector3 max = Misc.vector3minusHalf;
                for (int s = 0; s < 6; s++) {
                    var verts = prototype.sidesVertexData[s].vertices;
                    int vcount = verts.Count;
                    if (vcount == 0) continue;
                    for (int i = 1; i < vcount; i++) {
                        Vector3 v = verts[i];
                        if (v.x < min.x) min.x = v.x; if (v.x > max.x) max.x = v.x;
                        if (v.y < min.y) min.y = v.y; if (v.y > max.y) max.y = v.y;
                        if (v.z < min.z) min.z = v.z; if (v.z > max.z) max.z = v.z;
                    }
                }
                prototype.bounds.size = max - min;
                prototype.bounds.center = (min + max) * 0.5f;
            }

            // Calculate and cache face hashes for performance optimization
            for (int sideIndex = 0; sideIndex < 6; sideIndex++) {
                prototype.faceHashes[sideIndex] = ComputeFaceHash(mv, (Cube.Side)sideIndex);
            }
        }

        /// <summary>
        /// Compute rotation-invariant hash by using canonical coordinate indexing
        /// </summary>
        ulong ComputeFaceHash (MicroVoxels mv, Cube.Side side) {
            unchecked {
                ulong hash = 17;
                int baseIndex;
                int strideX, strideY;

                // Precompute face-specific parameters to avoid switch in inner loop
                switch (side) {
                    case Cube.Side.Back: // -Z face (z = 0)
                        baseIndex = 0;
                        strideX = 1;
                        strideY = MicroVoxels.COUNT_PER_FACE;
                        break;
                    case Cube.Side.Forward: // +Z face (z = last)
                        baseIndex = MicroVoxels.COUNT_PER_AXIS_MINUS_ONE * MicroVoxels.COUNT_PER_AXIS;
                        strideX = 1;
                        strideY = MicroVoxels.COUNT_PER_FACE;
                        break;
                    case Cube.Side.Left: // -X face (x = 0)
                        baseIndex = 0;
                        strideX = MicroVoxels.COUNT_PER_AXIS;
                        strideY = MicroVoxels.COUNT_PER_FACE;
                        break;
                    case Cube.Side.Right: // +X face (x = last)
                        baseIndex = MicroVoxels.COUNT_PER_AXIS_MINUS_ONE;
                        strideX = MicroVoxels.COUNT_PER_AXIS;
                        strideY = MicroVoxels.COUNT_PER_FACE;
                        break;
                    case Cube.Side.Bottom: // -Y face (y = 0)
                        baseIndex = 0;
                        strideX = 1;
                        strideY = MicroVoxels.COUNT_PER_AXIS;
                        break;
                    case Cube.Side.Top: // +Y face (y = last)
                        baseIndex = MicroVoxels.COUNT_PER_AXIS_MINUS_ONE * MicroVoxels.COUNT_PER_FACE;
                        strideX = 1;
                        strideY = MicroVoxels.COUNT_PER_AXIS;
                        break;
                    default:
                        return 0;
                }

                for (int canonicalIndex = 0; canonicalIndex < MicroVoxels.COUNT_PER_FACE; canonicalIndex++) {
                    int x = canonicalIndex % MicroVoxels.COUNT_PER_AXIS;
                    int y = canonicalIndex / MicroVoxels.COUNT_PER_AXIS;

                    // Compute 3D index directly: base + x*strideX + y*strideY
                    int index = baseIndex + x * strideX + y * strideY;
                    int ulongIndex = index / 64; // BITS_PER_ULONG = 64
                    int bitPosition = index % 64;
                    ulong bitValue = ((mv.gridData[ulongIndex] & (1UL << bitPosition)) != 0) ? 1UL : 0UL;

                    hash = hash * 31 + bitValue;
                    hash ^= hash >> 32;
                }

                return hash;
            }
        }
    }
}
