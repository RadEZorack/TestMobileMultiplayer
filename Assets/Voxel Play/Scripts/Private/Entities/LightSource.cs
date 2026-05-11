using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay
{

	/// <summary>
    /// Data for a light source inside a specific chunk (see chunk.lightSources)
    /// </summary>
	public class LightSource
	{
		// The gameobject used for this light (if any, ie. a torch model)
		public GameObject gameObject;

		// The voxel index where the light source is located
		public int voxelIndex;

		// Location of voxel to which this light source is attached. For instance, a torch is always attached to a solid voxel.
		// The hitInfo contains data about that solid voxel location, not the torch location. This data includes also the normal to the surface of the voxel.
		public VoxelHitInfo hitInfo;

		// The item definition of this light source (ie. a torch item)
		public ItemDefinition itemDefinition;

		// The light intensity (0-15)
        public byte lightIntensity = 15;
	}

}