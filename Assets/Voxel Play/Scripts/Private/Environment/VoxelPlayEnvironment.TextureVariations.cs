using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        void FindTextureVariations() {

            // Add texture variations
            TextureVariations[] tvv = Resources.LoadAll<TextureVariations>(world.resourcesLocation);
            int tvCount = tvv.Length;
            LogMessage(tvCount + " texture variations found.");
            for (int k = 0; k < tvCount; k++) {
                TextureVariations tv = tvv[k];
                if (tv == null) continue;
                VoxelDefinition vd = tv.voxelDefinition;
                if (tv.voxelDefinition == null) {
                    LogMessage($"Texture variation {k + 1} / {tvCount} for {tv.name} ignore. Missing voxel definition.");
                    continue;
                }
                if (vd.textureVariations == null) {
                    vd.textureVariations = new List<TextureVariations>();
                }
                vd.textureVariations.Add(tv);
            }
        }

        void LoadTextureVariations(VoxelDefinition vd) {
            if (vd.textureVariations == null) return;
            foreach (var textureVariation in vd.textureVariations) {
                var config = textureVariation.config;
                if (config == null || config.Length == 0) continue;
                LogMessage($"Texture variation for {vd.name} loaded. Adding {config.Length} textures.");
                for (int i = 0; i < config.Length; i++) {
                    var tex = config[i].texture;
                    if (tex == null) continue;
                    config[i].textureIndex = vd.textureArrayPacker.AddTexture(tex, texNRM: config[i].normalMap, ignoreAlpha: vd.renderType.isOpaque());
                }
                textureVariation.Init();
            }
        }

    }


}
