using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {
    
    public partial interface IVoxelPlayGreedyMesherLit {
        void FlushTriangles(List<Vector3> vertices, List<int> indices, List<Vector4> uv0, List<Vector3> normals, List<Color32> colors);
        void Clear();
    }
}