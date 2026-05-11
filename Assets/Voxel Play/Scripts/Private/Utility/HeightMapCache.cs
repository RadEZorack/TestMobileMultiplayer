namespace VoxelPlay {

    public class HeightMapCache {

        readonly FastHashSet<HeightMapInfo[]> sectorsDict;

        public HeightMapCache () {
            sectorsDict = new FastHashSet<HeightMapInfo[]>(16);
        }

        public void Clear () {
            int count = sectorsDict.Count;
            if (count > 0) {
                var entries = sectorsDict.entries;
                for (int i = 0; i < entries.Length; i++) {
                    if (entries[i].key >= 0) {
                        entries[i].value = null;
                    }
                }
            }
            sectorsDict.Clear();
        }

        public bool TryGetValue (double x, double z, out HeightMapInfo[] heights, out int heightIndex, out int chunkXMin, out int chunkZMin) {
            FastMath.FloorToInt(x / VoxelPlayEnvironment.CHUNK_SIZE, z / VoxelPlayEnvironment.CHUNK_SIZE, out chunkXMin, out chunkZMin);
            int key = ((chunkZMin + 1024) << 16) + chunkXMin + 1024;
            chunkXMin *= VoxelPlayEnvironment.CHUNK_SIZE;
            chunkZMin *= VoxelPlayEnvironment.CHUNK_SIZE;
            int px = (int)(x - chunkXMin);
            int pz = (int)(z - chunkZMin);
            heightIndex = pz * VoxelPlayEnvironment.ONE_Z_ROW + px;

            if (sectorsDict.TryGetValue(key, out heights)) return true;

            heights = new HeightMapInfo[VoxelPlayEnvironment.ONE_Y_ROW];
            sectorsDict.Add(key, heights);

            return false;
        }

        public void Remove (double x, double z) {
            FastMath.FloorToInt(x / VoxelPlayEnvironment.CHUNK_SIZE, z / VoxelPlayEnvironment.CHUNK_SIZE, out int chunkX, out int chunkZ);
            int key = ((chunkZ + 1024) << 16) + chunkX + 1024;
            sectorsDict.Remove(key);
        }

    }

}