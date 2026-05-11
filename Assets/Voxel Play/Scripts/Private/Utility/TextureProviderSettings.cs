namespace VoxelPlay {

    public struct TextureProviderSettings {
        public int textureSize;
        public float textureScale;
        public bool enableNormalMap;
        public bool enableReliefMap;
        public bool enablePBRMap;
        public bool useMipmapping;
        public float mipMapBias;
        public bool enableSmoothLighting;

        public static bool operator ==(TextureProviderSettings s1, TextureProviderSettings s2) {
            return s1.textureScale == s2.textureScale && s1.textureSize == s2.textureSize && s1.enableNormalMap == s2.enableNormalMap && s1.enableReliefMap == s2.enableReliefMap && s1.enablePBRMap == s2.enablePBRMap && s1.useMipmapping == s2.useMipmapping && s1.enableSmoothLighting == s2.enableSmoothLighting && s1.mipMapBias == s2.mipMapBias;
        }
        public static bool operator !=(TextureProviderSettings s1, TextureProviderSettings s2) {
            return s1.textureScale != s2.textureScale || s1.textureSize != s2.textureSize || s1.enableNormalMap != s2.enableNormalMap || s1.enableReliefMap != s2.enableReliefMap || s1.enablePBRMap != s2.enablePBRMap || s1.useMipmapping != s2.useMipmapping || s1.enableSmoothLighting != s2.enableSmoothLighting || s1.mipMapBias != s2.mipMapBias;
        }

        public override bool Equals(object obj) {
            return obj is TextureProviderSettings other && this == other;
        }

        public override int GetHashCode() {
            return System.HashCode.Combine(textureSize, textureScale, enableNormalMap, enableReliefMap, enablePBRMap, useMipmapping, mipMapBias, enableSmoothLighting);
        }

        public static TextureProviderSettings Create(int textureSize, float textureScale, bool enableNormalMap, bool enableReliefMap, bool enablePBRMap, VoxelPlayEnvironment env) {
            bool useMipmapping = false, enableSmoothLighting = false;
            float mipMapBias = 0;
            if (env != null) {
                useMipmapping = env.hqFiltering;
                enableSmoothLighting = env.enableSmoothLighting;
                mipMapBias = env.mipMapBias;
            }
            return new TextureProviderSettings {
                textureSize = textureSize, textureScale = textureScale, enableNormalMap = enableNormalMap, enableReliefMap = enableReliefMap, enablePBRMap = enablePBRMap,
                useMipmapping = useMipmapping, mipMapBias = mipMapBias, enableSmoothLighting = enableSmoothLighting
            };
        }

    }

}