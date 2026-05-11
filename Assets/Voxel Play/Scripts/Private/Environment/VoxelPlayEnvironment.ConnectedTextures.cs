using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        void FindConnectedTextures() {

            // Find connected textures
            ConnectedTexture[] ctt = Resources.LoadAll<ConnectedTexture>(world.resourcesLocation);
            int cttCount = ctt.Length;
            LogMessage($"{cttCount} connected textures rules found.");
            for (int k = 0; k < cttCount; k++) {
                ConnectedTexture ct = ctt[k];
                if (ct == null) continue;
                if (ct.voxelDefinition == null) {
                    LogMessage($"Connected texture {k + 1} / {cttCount} for {ct.name} ignored. Missing voxel definition.");
                    continue;
                }
                ct.voxelDefinition.connectedTextures = ct;
            }
        }


        void LoadConnectedTextures(VoxelDefinition vd) {

            ConnectedTexture ct = vd.connectedTextures;
            if (ct == null) {
                return;
            }

            LogMessage($"Connected texture for {vd.name} loaded. Adding {ct.config.Length} textures.");
            for (int j = 0; j < ct.config.Length; j++) {
                ct.config[j].textureIndex = vd.textureArrayPacker.AddTexture(ct.config[j].texture, texNRM: ct.config[j].normalMap, ignoreAlpha: vd.renderType.isOpaque());
            }
        }

        void InitConnectedTextures() {
            for (int k = 0; k < voxelDefinitionsCount; k++) {
                VoxelDefinition vd = voxelDefinitions[k];
                if (vd != null && vd.connectedTextures != null) {
                    vd.connectedTextures.Init();
                }
            }
        }
    }

}
