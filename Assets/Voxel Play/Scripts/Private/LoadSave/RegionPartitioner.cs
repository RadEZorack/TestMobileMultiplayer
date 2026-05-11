using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace VoxelPlay {

    public class RegionPartitioner {

        public const int REGION_SIZE = 256;
        
        const int REGION_HASH_WIDTH = 32768;
        const int REGION_HASH_DEPTH = 32768;
        Dictionary<(int, int), Region> regions = new Dictionary<(int, int), Region>();

        public RegionPartitioner (List<VoxelChunk> chunks) {

            foreach (var chunk in chunks) {

                if (chunk == null || !chunk.modified)
                    continue;

                var (regionX, regionZ) = GetChunkRegionXZ(chunk.position.x, chunk.position.z);

                var key = (regionX, regionZ);
                if (!regions.ContainsKey(key)) {
                    regions[key] = new Region(regionX, regionZ);
                }

                regions[key].AddChunk(chunk);
            }
        }

        public int Count => regions.Count;

        public IEnumerable<Region> GetRegions () {
            return regions.Values;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static (int, int) GetChunkRegionXZ (double chunkX, double chunkZ) {
            int regionX = FastMath.FloorToInt(chunkX / REGION_SIZE);
            int regionZ = FastMath.FloorToInt(chunkZ / REGION_SIZE);
            return (regionX, regionZ);
        }

        [MethodImpl(256)]
        public static (int, int) GetRegionXZFromChunkXZ (int chunkX, int chunkZ) {
            double rx = (double)chunkX * VoxelPlayEnvironment.CHUNK_SIZE / REGION_SIZE;
            double rz = (double)chunkZ * VoxelPlayEnvironment.CHUNK_SIZE / REGION_SIZE;
            int regionX = FastMath.FloorToInt(rx);
            int regionZ = FastMath.FloorToInt(rz);
            return (regionX, regionZ);
        }

        [MethodImpl(256)]
        public static string GetRegionStringIdFromChunkXZ (int chunkX, int chunkZ) {
            var (regionX, regionZ) = GetRegionXZFromChunkXZ(chunkX, chunkZ);
            return GetStringId(regionX, regionZ);
        }

        [MethodImpl(256)]
        public static int GetChunkRegionId (int chunkX, int chunkZ) {
            var (regionX, regionZ) = GetRegionXZFromChunkXZ(chunkX, chunkZ);
            return GetRegionId(regionX, regionZ);
        }

        public static string GetStringId (int regionX, int regionZ) {
            return $"{regionX}_{regionZ}";
        }

        [MethodImpl(256)]
        public static int GetRegionId (int regionX, int regionZ) {
            int x = regionX + REGION_HASH_WIDTH;
            int z = regionZ + REGION_HASH_DEPTH;
            return (x << 16) | (z & 0xFFFF);
        }

        [MethodImpl(256)]
        public static string GetStringIdFromRegionId (int regionId) {
            int x = (regionId >> 16) - REGION_HASH_WIDTH;
            int z = (regionId & 0xFFFF) - REGION_HASH_DEPTH;
            return GetStringId(x, z);
        }

        [MethodImpl(256)]
        public static bool TryGetRegionIdFromString (string regionStringId, out int regionId) {
            regionId = 0;
            if (string.IsNullOrEmpty(regionStringId)) return false;
            int sep = regionStringId.IndexOf('_');
            if (sep <= 0 || sep >= regionStringId.Length - 1) return false;
            string sx = regionStringId.Substring(0, sep);
            string sz = regionStringId.Substring(sep + 1);
            if (!int.TryParse(sx, out int rx)) return false;
            if (!int.TryParse(sz, out int rz)) return false;
            regionId = GetRegionId(rx, rz);
            return true;
        }
    }

}