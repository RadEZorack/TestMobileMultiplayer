using UnityEngine;

namespace VoxelPlay.GPURendering {

	public partial interface IGPUInstancingRenderer {
		
		void ClearChunk (VoxelChunk chunk);

		void AddVoxel (VoxelChunk chunk, int voxelIndex, VoxelDefinition voxelDefinition, Vector3 position, Quaternion rotation, Vector3 scale);

		void Render (Vector3 observerPos, float visibleDistance, Vector3[] frustumPlanesNormals, float[] frustumPlanesDistances);

		void Refresh ();

		void Dispose ();
	}

}