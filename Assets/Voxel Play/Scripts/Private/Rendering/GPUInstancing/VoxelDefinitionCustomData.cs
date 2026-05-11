using System;

namespace VoxelPlay {

	public partial class VoxelDefinition {
		
		// look-up index for batched mesh array in GPU instancing
		[NonSerialized]
		public int batchedIndex = -1;

	}

}
