using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public delegate int TextureVariationsDelegate(int defaultTextureIndex, Vector3d position, int seed);


    [Serializable]
    public struct TextureVariationConfig {
        [Range(0, 1)]
        public float probability;
        public Texture2D texture;
        public Texture2D normalMap;

        [NonSerialized]
        public int textureIndex;

        [NonSerialized]
        public float probStart, probEnd;
    }

    [CreateAssetMenu(menuName = "Voxel Play/Texture Variations", fileName = "TextureVariations", order = 132)]
    public class TextureVariations : ScriptableObject {

        public enum Side {
            AnySide,
            Top,
            Bottom,
            Forward,
            Back,
            Left,
            Right
        }

        [Tooltip("The voxel definition to which this configuration applies")]
        public VoxelDefinition voxelDefinition;

        [Tooltip("The side of the voxel to which these rules apply")]
        public Side side = Side.AnySide;

        public TextureVariationConfig[] config;

        int[] results;

        public void Init() {
            if (voxelDefinition == null || config == null) return;
            if (side == Side.Top || side == Side.AnySide) voxelDefinition.customTextureVariationsProviderTop = ResolveTexture;
            if (side == Side.Bottom || side == Side.AnySide) voxelDefinition.customTextureVariationsProviderBottom = ResolveTexture;
            if (side == Side.Back || side == Side.AnySide) voxelDefinition.customTextureVariationsProviderBack = ResolveTexture;
            if (side == Side.Forward || side == Side.AnySide) voxelDefinition.customTextureVariationsProviderForward = ResolveTexture;
            if (side == Side.Left || side == Side.AnySide) voxelDefinition.customTextureVariationsProviderLeft = ResolveTexture;
            if (side == Side.Right || side == Side.AnySide) voxelDefinition.customTextureVariationsProviderRight = ResolveTexture;

            ComputeMatchesMatrix();
        }

        private void OnValidate() {
            ComputeMatchesMatrix();
        }


        void ComputeMatchesMatrix() {

            if (config == null) return;

            int configLength = config.Length;

            float sumProbs = 0;
            for (int k = 0; k < configLength; k++) {
                sumProbs += config[k].probability;
            }
            if (sumProbs <= 0) return;

            float probBase = 0;
            for (int k = 0; k < configLength; k++) {
                config[k].probStart = probBase;
                probBase += config[k].probability / sumProbs;
                config[k].probEnd = probBase;
            }

            if (results == null || results.Length != 100) {
                results = new int[100];
            }

            for (int k = 0; k < 100; k++) {
                float prob = k / 100f;
                for (int j = 0; j < configLength; j++) {
                    if (prob >= config[j].probStart && prob < config[j].probEnd) {
                        results[k] = j;
                        break;
                    }
                }
            }

        }

        public int ResolveTexture(int defaultTextureIndex, Vector3d position, int iteration) {
            int r = WorldRand.Range(0, 100, position);
            int j = (results[r] + iteration) % config.Length;
            return config[j].textureIndex;
        }

    }

    public partial class VoxelDefinition : ScriptableObject {
        [NonSerialized] public TextureVariationsDelegate customTextureVariationsProviderBack;
        [NonSerialized] public TextureVariationsDelegate customTextureVariationsProviderForward;
        [NonSerialized] public TextureVariationsDelegate customTextureVariationsProviderTop;
        [NonSerialized] public TextureVariationsDelegate customTextureVariationsProviderBottom;
        [NonSerialized] public TextureVariationsDelegate customTextureVariationsProviderLeft;
        [NonSerialized] public TextureVariationsDelegate customTextureVariationsProviderRight;
        [NonSerialized] public List<TextureVariations> textureVariations;
    }

}

