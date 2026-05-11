using System.Runtime.CompilerServices;
using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        struct LightmapAddNode {
            public VoxelChunk chunk;
            public int voxelIndex;
        }

        struct LightmapRemovalNode {
            public VoxelChunk chunk;
            public int voxelIndex;
            public int light;
        }

        bool effectiveGlobalIllumination {
            [MethodImpl(256)]
            get {
#if UNITY_EDITOR
                if (!applicationIsPlaying)
                    return false;
#endif
                return globalIllumination;
            }
        }


        void InitLightmap() {
            sunLightmapRemovalQueue = new FastList<LightmapRemovalNode>(128);
            sunLightmapSpreadQueue = new FastList<LightmapAddNode>(128);
            torchLightmapRemovalQueue = new FastList<LightmapRemovalNode>(128);
            torchLightmapSpreadQueue = new FastList<LightmapAddNode>(128);
            if (!effectiveGlobalIllumination) {
                Voxel.Hole.light = FULL_LIGHT;
            }
        }

        /// <summary>
        /// Computes light propagation. Only Sun light. Other light sources like torches are handled in the shader itself.
        /// </summary>e
        void ComputeLightmap(VoxelChunk chunk) {
            chunk.lightmapIsClear = false;
            chunk.needsLightmapRebuild = false;

            if (!effectiveGlobalIllumination) {
                return;
            }

            int lightSourcesCount = chunk.lightSources != null ? chunk.lightSources.Count : 0;
            for (int k = 0; k < lightSourcesCount; k++) {
                LightSource ls = chunk.lightSources[k];
                SetTorchLightmap(chunk, ls.voxelIndex, ls.lightIntensity);
            }
            ComputeSunLightmap(chunk);
            ComputeTorchLightmap(chunk);
        }

        /// <summary>
        /// Computes quick lightmap changes
        /// </summary>
        void ProcessLightmapUpdates() {
            if (!effectiveGlobalIllumination) {
                return;
            }

            ProcessSunLightmapRemoval();
            ProcessSunLightmapSpread();
            ProcessTorchLightmapRemoval();
            ProcessTorchLightmapSpread();
        }

        /// <summary>
        /// Clear sun & torch lightmap at position
        /// </summary>
        void ClearLightmapAtPosition(VoxelChunk chunk, int voxelIndex) {
            ClearSunLightmap(chunk, voxelIndex);
            ClearTorchLightmap(chunk, voxelIndex);
        }

        /// <summary>
        /// Spreads lightmap when one voxel is destroyed
        /// </summary>
        void SpreadLightmapAroundPosition(VoxelChunk chunk, int voxelIndex) {
            SpreadSunLightmapAroundVoxel(chunk, voxelIndex);
            SpreadTorchLightmapAroundVoxel(chunk, voxelIndex);
        }

    }



}

