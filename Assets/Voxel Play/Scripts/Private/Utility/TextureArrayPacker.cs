using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public class TextureArrayPacker {

        public struct WorldTexture {
            public Color32[] colors;
            public Color32[] normalsAndElevation;
            public Color32[] pbr;
            public Color32[] emission;
        }

        /// <summary>
        /// Settings for the texture provider
        /// </summary>
        public TextureProviderSettings settings;

        /// <summary>
        /// Texture array containing all textures
        /// </summary>
        public Texture2DArray textureArray;

        /// <summary>
        /// Texture array containing material properties
        /// </summary>
        public Texture2D materialPropsTexture;

        /// <summary>
        /// Gradient LUT texture (64 x textureCount, RGBA32)
        /// </summary>
        public Texture2D gradientLUTTexture;

        /// <summary>
        /// List containing all textures added
        /// </summary>
        public List<WorldTexture> textures;

        /// <summary>
        /// Number of registered textures
        /// </summary>
        public int texturesCount => textures != null ? textures.Count : 0;

        /// <summary>
        /// Maximum textures allowed in a single texture array
        /// </summary>
        public int maxTextures => _maxTextures;

        /// <summary>
        /// Memory size of the texture array
        /// </summary>
        public long memorySize => _memorySize;

        /// <summary>
        /// Returns true if this texture packer doesn't accept more textures
        /// </summary>
        public bool HasAvailableTextureSlices (int required) {
            return texturesCount + required <= maxTextures;
        }

        public bool containsValidTextureArray => !textureArrayDirty && textureArray != null && materialPropsTexture != null;

        /// <summary>
        /// Dictionary for fast texture search
        /// </summary>
        Dictionary<int, int> texturesDict;

        Color32[] defaultColorMap;
        Texture2D defaultTransparentTexture;
        Color32[] materialPropsData;
        Color32[][] gradientLUTRows;
        bool textureArrayDirty;

        readonly int _maxTextures;
        readonly long _memorySize;
        const long TextureArrayBytesLimitD3D12 = 2L * 1024 * 1024 * 1024;
        const long TextureArrayBytesLimitVulkan = 2L * 1024 * 1024 * 1024;
        const long TextureArrayBytesLimitMetal = 2L * 1024 * 1024 * 1024;
        const long TextureArrayBytesLimitD3D11 = 2L * 1024 * 1024 * 1024;
        const long TextureArrayBytesLimitOpenGL = 512L * 1024 * 1024;
        const long TextureArrayBytesLimitDefault = 512L * 1024 * 1024;

        static readonly byte[] linearToGammaLut;

        static TextureArrayPacker () {
            linearToGammaLut = new byte[256];
            for (int i = 0; i < 256; i++) {
                linearToGammaLut[i] = (byte)(255 * Mathf.LinearToGammaSpace(i / 255f));
            }
        }

        public TextureArrayPacker (TextureProviderSettings settings) {
            this.settings = settings;
            ComputeTextureArrayLimits(out _maxTextures, out _memorySize);
            Clear();
        }

        void ComputeTextureArrayLimits (out int maxTextures, out long memorySize) {
            long systemMaxTextureArrayMemorySize = GetSystemMaxTextureArrayMemorySize();
            long bytesPerSliceWithMips = ComputeBytesPerSliceWithMips(settings);
            long maxTexturesByMemory = bytesPerSliceWithMips <= 0 ? 1 : systemMaxTextureArrayMemorySize / bytesPerSliceWithMips;
            if (maxTexturesByMemory < 1) {
                maxTexturesByMemory = 1;
            }
            maxTextures = (int)Math.Min(SystemInfo.maxTextureArraySlices, maxTexturesByMemory);
            memorySize = maxTextures * bytesPerSliceWithMips;
        }

        static long GetSystemMaxTextureArrayMemorySize () {
            switch (SystemInfo.graphicsDeviceType) {
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D12:
                    return TextureArrayBytesLimitD3D12;
                case UnityEngine.Rendering.GraphicsDeviceType.Vulkan:
                    return TextureArrayBytesLimitVulkan;
                case UnityEngine.Rendering.GraphicsDeviceType.Metal:
                    return TextureArrayBytesLimitMetal;
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:
                    return TextureArrayBytesLimitD3D11;
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore:
				#if !UNITY_6000_0_OR_NEWER
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3:
				#endif
                    return TextureArrayBytesLimitOpenGL;
                default:
                    return TextureArrayBytesLimitDefault;
            }
        }

        static long ComputeBytesPerSliceWithMips (TextureProviderSettings settings) {
            int size = settings.textureSize;
            if (size <= 0) size = 1;

            double mipFactor = 1.0;
            if (settings.useMipmapping) {
                int mipCount = 1 + Mathf.FloorToInt(Mathf.Log(size, 2f));
                mipFactor = (1.0 - Math.Pow(0.25, mipCount)) / 0.75;
            }

            const int textureArrayBytesPerPixel = 4;
            double bytesPerSlice = (double)size * size * textureArrayBytesPerPixel;
            return (long)Math.Ceiling(bytesPerSlice * mipFactor);
        }

        public void SetTintGradientParam (int textureIndex, byte encodedValue) {
            if (textureIndex < 0 || textureIndex >= materialPropsData.Length) return;
            if (materialPropsData[textureIndex].a != 0) return; // first-wins
            materialPropsData[textureIndex].a = encodedValue;
            textureArrayDirty = true;
        }

        public void SetTintGradientLUT (int textureIndex, Color32[] row64) {
            if (gradientLUTRows == null || gradientLUTRows.Length <= textureIndex) {
                int newSize = maxTextures;
                var old = gradientLUTRows;
                gradientLUTRows = new Color32[newSize][];
                if (old != null) Array.Copy(old, gradientLUTRows, old.Length);
            }
            if (gradientLUTRows[textureIndex] != null) return; // first-wins
            gradientLUTRows[textureIndex] = row64;
            textureArrayDirty = true;
        }

        public void Clear () {
            if (textures == null) {
                textures = new List<WorldTexture>();
            } else {
                textures.Clear();
            }
            if (texturesDict == null) {
                texturesDict = new Dictionary<int, int>();
            } else {
                texturesDict.Clear();
            }
            if (materialPropsData == null) {
                materialPropsData = new Color32[maxTextures];
            } else {
                System.Array.Fill(materialPropsData, Misc.color32Transparent);
            }
            Misc.DestroyImmediateAndNullify(ref textureArray);
            Misc.DestroyImmediateAndNullify(ref materialPropsTexture);
            Misc.DestroyImmediateAndNullify(ref gradientLUTTexture);
            Misc.DestroyImmediateAndNullify(ref defaultTransparentTexture);
            gradientLUTRows = null;
            textureArrayDirty = true;
        }

        Color32 GetGammaColor32 (byte r, byte g, byte b, byte a) {
            Color32 color = new Color32(r, g, b, a);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) {
                color.r = linearToGammaLut[color.r];
                color.g = linearToGammaLut[color.g];
                color.b = linearToGammaLut[color.b];
                color.a = linearToGammaLut[color.a];
            }
            return color;
        }

        public Texture2D GetDefaultTransparentTexture () {
            if (defaultTransparentTexture == null) {
                defaultTransparentTexture = new Texture2D(settings.textureSize, settings.textureSize, TextureFormat.ARGB32, false);
                defaultTransparentTexture.name = "DefaultTransparentTexture";
                Color32[] colors = new Color32[settings.textureSize * settings.textureSize];
                defaultTransparentTexture.SetPixels32(colors);
                defaultTransparentTexture.Apply();
            }
            return defaultTransparentTexture;
        }

        int GetTextureHash (Texture2D tex) {
            int textureHash = tex.GetHashCode();
            // if (texNRM != null) { // changed: now, the albedo texture alone will determine if a side or bottom texture collection is same
            //     textureHash ^= (texNRM.GetHashCode() << 2);
            // }
            return textureHash;
        }

        /// <summary>
        /// Returns the index in the texture list and the full index (index in the list + some flags specifying existence of normal/displacement maps)
        /// </summary>
        public int AddTexture (Texture2D texAlbedo, Texture2D texEmission = null, Texture2D texNRM = null, Texture2D texDISP = null, Texture2D texMetallic = null, Texture2D texSmoothness = null, Texture2D texOcclusion = null, Texture2D texOpacity = null, bool avoidRepetitions = true, bool ignoreAlpha = false, float metallicStrength = 0f, float smoothnessStrength = 0.06f, float occlusionStrength = 1f, float normalStrength = 1f, float displacementStrength = 1f, float emissionStrength = 1f, float opacityStrength = 1f, bool isRoughness = false) {

            if (texAlbedo == null) return 0;

            int index;
            if (avoidRepetitions) {
                int textureHash = GetTextureHash(texAlbedo); //, texNRM);
                if (texturesDict.TryGetValue(textureHash, out index)) {
                    return index;
                }
                index = textures.Count;
                texturesDict[textureHash] = index;
            } else {
                index = textures.Count;
            }

            // Add entry to dictionary
            if (ignoreAlpha) {
                texOpacity = null;
            }

            // Albedo + Opacity
            WorldTexture wt = new WorldTexture {
                colors = CombineAlbedoAndOpacity(texAlbedo, ignoreAlpha, texOpacity, opacityStrength)
            };
            textures.Add(wt);

            Color32 props = Misc.color32Transparent;
            byte sliceCount = 1;

            // Normal + Elevation Map
            bool useNormalsOrRelief = settings.enableNormalMap || settings.enableReliefMap;
            if (useNormalsOrRelief && (texNRM != null || texDISP != null)) {
                props.r = sliceCount;
                WorldTexture wextra = new WorldTexture {
                    normalsAndElevation = CombineNormalsAndElevation(texNRM, texDISP, normalStrength, displacementStrength)
                };
                textures.Add(wextra);
                sliceCount++;
            }

            // Metallic/Smoothness Map
            if (settings.enablePBRMap && (texMetallic != null || texSmoothness != null || texOcclusion != null)) {
                props.g = sliceCount;
                WorldTexture wextra = new WorldTexture {
                    pbr = CombinePBR(texMetallic, texSmoothness, texOcclusion, metallicStrength, smoothnessStrength, occlusionStrength, isRoughness)
                };
                textures.Add(wextra);
                sliceCount++;
            }

            // Emission
            if (texEmission != null) {
                props.b = sliceCount;
                WorldTexture wemission = new WorldTexture {
                    emission = ProcessEmission(texEmission, emissionStrength)
                };
                textures.Add(wemission);
            }

            materialPropsData[index] = props;
            textureArrayDirty = true;

            return index;
        }

        Color32[] CombineAlbedoAndOpacity (Texture2D albedoMap, bool ignoreAlpha, Texture2D opacityMap, float opacityStrength) {
            Color32[] albedoColors;
            if (albedoMap == null) {
                return GetDefaultColorMap();
            }

            bool usesOpacityMap = opacityMap != null;
            if (albedoMap.width != settings.textureSize || albedoMap.height != settings.textureSize) {
#if UNITY_EDITOR
                VoxelPlayEnvironment.instance.LogMessage($"Scaling albedo map {albedoMap.name} to {settings.textureSize}x{settings.textureSize}");
#endif
                albedoColors = TextureTools.ScaleTextureColors(albedoMap, settings.textureSize, settings.textureSize, FilterMode.Point, canUseCache: true);
            } else {
                albedoColors = albedoMap.GetPixels32();
            }
            int len = albedoColors.Length;

            if (usesOpacityMap) {
                Color32[] opacityColors;
                if (opacityMap.width != settings.textureSize || opacityMap.height != settings.textureSize) {
#if UNITY_EDITOR
                    VoxelPlayEnvironment.instance.LogMessage($"Scaling opacity map {opacityMap.name} to {settings.textureSize}x{settings.textureSize}");
#endif
                    opacityColors = TextureTools.ScaleTextureColors(opacityMap, settings.textureSize, settings.textureSize, FilterMode.Point, canUseCache: false);
                } else {
                    opacityColors = opacityMap.GetPixels32();
                }
                for (int k = 0; k < len; k++) {
                    float opacity = opacityColors[k].r * opacityStrength;
                    if (opacity > 255) opacity = 255;
                    albedoColors[k].a = (byte)opacity;
                }
            } else if (ignoreAlpha) {
                for (int k = 0; k < len; k++) {
                    albedoColors[k].a = 255;
                }
            }
            return albedoColors;
        }

        Color32[] ProcessEmission(Texture2D emissionMap, float emissionStrength) {
            Color32[] emissionColors;
            if (emissionMap.width != settings.textureSize || emissionMap.height != settings.textureSize) {
#if UNITY_EDITOR
                VoxelPlayEnvironment.instance.LogMessage($"Scaling emission map {emissionMap.name} to {settings.textureSize}x{settings.textureSize}");
#endif
                emissionColors = TextureTools.ScaleTextureColors(emissionMap, settings.textureSize, settings.textureSize, FilterMode.Point, canUseCache: false);
            } else {
                emissionColors = emissionMap.GetPixels32();
            }
            int len = emissionColors.Length;
            byte alpha = (byte)(emissionStrength / 8f * 255f);
            for (int k = 0; k < len; k++) {
                emissionColors[k].a = alpha;
            }
            return emissionColors;
        }

        Color32[] CombineNormalsAndElevation (Texture2D normalMap, Texture2D elevationMap, float normalStrength, float displacementStrength) {
            Color32[] normals;
            if (normalMap == null) {
                normals = new Color32[settings.textureSize * settings.textureSize];
                normals.Fill(new Color32(128, 128, 255, 0));
            } else {
                if (normalMap.width != settings.textureSize || normalMap.height != settings.textureSize) {
#if UNITY_EDITOR
                    VoxelPlayEnvironment.instance.LogMessage($"Scaling normal map {normalMap.name} to {settings.textureSize}x{settings.textureSize}");
#endif
                    normals = TextureTools.ScaleTextureColors(normalMap, settings.textureSize, settings.textureSize, FilterMode.Point, canUseCache: false);
                } else {
                    normals = normalMap.GetPixels32();
                }
            }

            Color32[] elevations;
            if (elevationMap == null) {
                elevations = GetDefaultColorMap(); // r = 0 elevation in this default map
            } else {
                if (elevationMap.width != settings.textureSize || elevationMap.height != settings.textureSize) {
#if UNITY_EDITOR
                    VoxelPlayEnvironment.instance.LogMessage($"Scaling elevation map {elevationMap.name} to {settings.textureSize}x{settings.textureSize}");
#endif
                    elevations = TextureTools.ScaleTextureColors(elevationMap, settings.textureSize, settings.textureSize, FilterMode.Point, canUseCache: false);
                } else {
                    elevations = elevationMap.GetPixels32();
                }
            }

            int normalMapColorsLength = normals.Length;
            if (normalStrength == 1f && displacementStrength == 1f) {
                // detect dxt compression (has r = 255)
                if (Application.isMobilePlatform || normals[0].r != 255) {
                    // no displacement, no strength, no dxt compression
                    // copy elevation into alpha channel of normal map to save 1 texture slot in texture array and optimize cache
                    for (int k = 0; k < normalMapColorsLength; k++) {
                        normals[k].a = elevations[k].r;
                    }
                } else {
                    for (int k = 0; k < normalMapColorsLength; k++) {
                        // in dxt5nrm format, r is stored in the alpha channel so we move it back to r
                        normals[k].r = normals[k].a;
                        // reconstruct z (blue) from x & y
                        float x = (normals[k].r / 255f) * 2f - 1f;
                        float y = (normals[k].g / 255f) * 2f - 1f;
                        float t = 1f - (x * x + y * y);
                        if (t < 0) t = 0;
                        t = (float)System.Math.Sqrt(t);
                        normals[k].b = (byte)((t * 0.5f + 0.5) * 255);
                        // copy elevation into alpha channel of normal map to save 1 texture slot in texture array and optimize cache
                        normals[k].a = elevations[k].r;
                    }
                }
            } else {
                // detect dxt compression (has r = 255)
                if (Application.isMobilePlatform || normals[0].r != 255) {
                    // copy elevation into alpha channel of normal map to save 1 texture slot in texture array and optimize cache
                    for (int k = 0; k < normalMapColorsLength; k++) {
                        // Apply normal strength by interpolating between default normal (0, 0, 1) and the normal map
                        float x = (normals[k].r / 255f) * 2f - 1f;
                        float y = (normals[k].g / 255f) * 2f - 1f;
                        x *= normalStrength;
                        y *= normalStrength;
                        if (x < -1f) x = -1f; else if (x > 1f) x = 1f;
                        if (y < -1f) y = -1f; else if (y > 1f) y = 1f;
                        normals[k].r = (byte)((x * 0.5f + 0.5f) * 255);
                        normals[k].g = (byte)((y * 0.5f + 0.5f) * 255);

                        // Apply displacement strength
                        float disp = elevations[k].r * displacementStrength;
                        if (disp > 255) disp = 255;
                        normals[k].a = (byte)disp;
                    }
                } else {
                    for (int k = 0; k < normalMapColorsLength; k++) {
                        // in dxt5nrm format, r is stored in the alpha channel so we move it back to r
                        normals[k].r = normals[k].a;
                        // reconstruct z (blue) from x & y
                        float x = (normals[k].r / 255f) * 2f - 1f;
                        float y = (normals[k].g / 255f) * 2f - 1f;
                        // Apply normal strength
                        x *= normalStrength;
                        y *= normalStrength;
                        if (x < -1f) x = -1f; else if (x > 1f) x = 1f;
                        if (y < -1f) y = -1f; else if (y > 1f) y = 1f;
                        float t = 1f - (x * x + y * y);
                        if (t < 0) t = 0;
                        t = (float)System.Math.Sqrt(t);
                        normals[k].r = (byte)((x * 0.5f + 0.5f) * 255);
                        normals[k].g = (byte)((y * 0.5f + 0.5f) * 255);
                        normals[k].b = (byte)((t * 0.5f + 0.5) * 255);
                        // copy elevation into alpha channel of normal map to save 1 texture slot in texture array and optimize cache
                        float disp = elevations[k].r * displacementStrength;
                        if (disp > 255) disp = 255;
                        normals[k].a = (byte)disp;
                    }
                }
            }

            // Normal & heightmaps are usually stored in linear so we need to convert them to gamma
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) {
                for (int k = 0; k < normalMapColorsLength; k++) {
                    normals[k].r = linearToGammaLut[normals[k].r];
                    normals[k].g = linearToGammaLut[normals[k].g];
                    normals[k].b = linearToGammaLut[normals[k].b];
                    normals[k].a = linearToGammaLut[normals[k].a];
                }
            }

            return normals;
        }


        Color32[] CombinePBR (Texture2D metallicMap, Texture2D smoothnessMap, Texture2D occlusionMap, float metallicStrength, float smoothnessStrength, float occlusionStrength, bool isRoughness) {
            Color32[] metallicMapColors, smoothnessMapColors, occlusionMapColors;
            if (metallicMap == null) {
                metallicMapColors = new Color32[settings.textureSize * settings.textureSize];
                metallicMapColors.Fill(new Color32(0, 0, 0, 0));
            } else if (metallicMap.width != settings.textureSize || metallicMap.height != settings.textureSize) {
#if UNITY_EDITOR
                VoxelPlayEnvironment.instance.LogMessage($"Scaling metallic map {metallicMap.name} to {settings.textureSize}x{settings.textureSize}");
#endif
                metallicMapColors = TextureTools.ScaleTextureColors(metallicMap, settings.textureSize, settings.textureSize, FilterMode.Point, canUseCache: false);
            } else {
                metallicMapColors = metallicMap.GetPixels32();
            }

            int mapColorsLength = metallicMapColors.Length;

            bool hasSmoothnessMap = smoothnessMap != null;
            if (hasSmoothnessMap) {
                if (smoothnessMap.width != settings.textureSize || smoothnessMap.height != settings.textureSize) {
#if UNITY_EDITOR
                    VoxelPlayEnvironment.instance.LogMessage($"Scaling smoothness map {smoothnessMap.name} to {settings.textureSize}x{settings.textureSize}");
#endif
                    smoothnessMapColors = TextureTools.ScaleTextureColors(smoothnessMap, settings.textureSize, settings.textureSize, FilterMode.Point, canUseCache: false);
                } else {
                    smoothnessMapColors = smoothnessMap.GetPixels32();
                }
            } else {
                smoothnessMapColors = null;
            }

            bool hasOcclusionMap = occlusionMap != null;
            if (hasOcclusionMap) {
                if (occlusionMap.width != settings.textureSize || occlusionMap.height != settings.textureSize) {
#if UNITY_EDITOR
                    VoxelPlayEnvironment.instance.LogMessage($"Scaling occlusion map {occlusionMap.name} to {settings.textureSize}x{settings.textureSize}");
#endif
                    occlusionMapColors = TextureTools.ScaleTextureColors(occlusionMap, settings.textureSize, settings.textureSize, FilterMode.Point, canUseCache: false);
                } else {
                    occlusionMapColors = occlusionMap.GetPixels32();
                }
            } else {
                occlusionMapColors = null;
            }

            for (int k = 0; k < mapColorsLength; k++) {
                // Apply strength multipliers
                float metallicValue = (metallicMap != null ? metallicMapColors[k].r : 0) * metallicStrength;
                if (metallicValue > 255) metallicValue = 255;
                metallicMapColors[k].r = (byte)metallicValue;

                float smoothnessValue = smoothnessMapColors[k].r * smoothnessStrength;
                if (smoothnessValue > 255) smoothnessValue = 255; else if (smoothnessValue < 0) smoothnessValue = 0;
                if (isRoughness) smoothnessValue = 255 - smoothnessValue;
                metallicMapColors[k].g = (byte)smoothnessValue;

                float occlusionValue = (hasOcclusionMap ? occlusionMapColors[k].r : 255) * occlusionStrength;
                if (occlusionValue > 255) occlusionValue = 255;
                metallicMapColors[k].b = (byte)occlusionValue;
            }

            return metallicMapColors;
        }

        Color32[] GetDefaultColorMap () {
            int len = settings.textureSize * settings.textureSize;
            if (defaultColorMap != null && defaultColorMap.Length == len) {
                return defaultColorMap;
            }
            defaultColorMap = new Color32[len];
            return defaultColorMap;
        }


        void ForgetTexture (Texture2D tex, Texture2D texNRM, Texture2D texDISP, Texture2D texMetallic, Texture2D texSmoothness, Texture2D texOcclusion, Texture2D texEmission, Texture2D texOpacity) {
            if (tex == null) return;
            // remove texture from dictionary
            int textureHash = GetTextureHash(tex); //, texNRM);
            texturesDict.Remove(textureHash);
            // release scaled texture caches
            TextureTools.ReleaseCache(tex);
            TextureTools.ReleaseCache(texNRM);
            TextureTools.ReleaseCache(texDISP);
            TextureTools.ReleaseCache(texMetallic);
            TextureTools.ReleaseCache(texSmoothness);
            TextureTools.ReleaseCache(texOcclusion);
            TextureTools.ReleaseCache(texEmission);
            TextureTools.ReleaseCache(texOpacity);
        }

        public void ForgetTextures (VoxelDefinition vd) {
            if (vd == null) return;
            ForgetTexture(vd.textureTop, vd.textureTopNRM, vd.textureTopDISP, vd.textureTopMetallic, vd.textureTopSmoothness, vd.textureTopOcclusion, vd.textureTopEmission, vd.textureTopOpacity);
            ForgetTexture(vd.textureSide, vd.textureSideNRM, vd.textureSideDISP, vd.textureSideMetallic, vd.textureSideSmoothness, vd.textureSideOcclusion, vd.textureSideEmission, vd.textureSideOpacity);
            ForgetTexture(vd.textureBottom, vd.textureBottomNRM, vd.textureBottomDISP, vd.textureBottomMetallic, vd.textureBottomSmoothness, vd.textureBottomOcclusion, vd.textureBottomEmission, vd.textureBottomOpacity);
            ForgetTexture(vd.textureForward, vd.textureForwardNRM, vd.textureForwardDISP, vd.textureForwardMetallic, vd.textureForwardSmoothness, vd.textureForwardOcclusion, vd.textureForwardEmission, vd.textureForwardOpacity);
            ForgetTexture(vd.textureLeft, vd.textureLeftNRM, vd.textureLeftDISP, vd.textureLeftMetallic, vd.textureLeftSmoothness, vd.textureLeftOcclusion, vd.textureLeftEmission, vd.textureLeftOpacity);
            ForgetTexture(vd.textureRight, vd.textureRightNRM, vd.textureRightDISP, vd.textureRightMetallic, vd.textureRightSmoothness, vd.textureRightOcclusion, vd.textureRightEmission, vd.textureRightOpacity);
        }

        public void CreateTextureArray () {
            int textureCount = this.texturesCount;
            if (textureCount == 0) {
                textureArrayDirty = false;
                return;
            }
            if (!textureArrayDirty && textureArray != null && materialPropsTexture != null && materialPropsTexture.width == textureCount) {
                return;
            }

            Misc.DestroyImmediateAndNullify(ref textureArray);

            textureArray = new Texture2DArray(settings.textureSize, settings.textureSize, textureCount, TextureFormat.ARGB32, settings.useMipmapping);
            if (settings.enableReliefMap || !settings.enableSmoothLighting) {
                textureArray.wrapMode = TextureWrapMode.Repeat;
            } else {
                textureArray.wrapMode = TextureWrapMode.Clamp;
            }
            textureArray.filterMode = settings.useMipmapping ? FilterMode.Bilinear : FilterMode.Point;
            textureArray.mipMapBias = -settings.mipMapBias;

            for (int k = 0; k < textureCount; k++) {
                WorldTexture wt = textures[k];
                if (wt.colors != null) {
                    textureArray.SetPixels32(wt.colors, k);
                } else if (wt.normalsAndElevation != null) {
                    textureArray.SetPixels32(wt.normalsAndElevation, k);
                } else if (wt.pbr != null) {
                    textureArray.SetPixels32(wt.pbr, k);
                } else if (wt.emission != null) {
                    textureArray.SetPixels32(wt.emission, k);
                }
            }
            textureArray.Apply(settings.useMipmapping, true);

            // Create material props texture
            if (materialPropsTexture != null && materialPropsTexture.width != textureCount) {
                UnityEngine.Object.DestroyImmediate(materialPropsTexture);
                materialPropsTexture = null;
            }
            if (materialPropsTexture == null) {
                materialPropsTexture = new Texture2D(textureCount, 1, TextureFormat.RGBA32, false, true);
                materialPropsTexture.filterMode = FilterMode.Point;
                materialPropsTexture.wrapMode = TextureWrapMode.Clamp;
            }
            materialPropsTexture.SetPixels32(0, 0, textureCount, 1, materialPropsData, 0);
            materialPropsTexture.Apply(false);

            // Create gradient LUT texture
            if (gradientLUTTexture != null && gradientLUTTexture.height != textureCount) {
                UnityEngine.Object.DestroyImmediate(gradientLUTTexture);
                gradientLUTTexture = null;
            }
            if (gradientLUTTexture == null) {
                gradientLUTTexture = new Texture2D(64, textureCount, TextureFormat.RGBA32, false, true);
                gradientLUTTexture.filterMode = FilterMode.Bilinear;
                gradientLUTTexture.wrapMode = TextureWrapMode.Clamp;
            }
            Color32 white = new Color32(255, 255, 255, 255);
            Color32[] whiteRow = null;
            for (int i = 0; i < textureCount; i++) {
                Color32[] row = (gradientLUTRows != null && i < gradientLUTRows.Length && gradientLUTRows[i] != null)
                    ? gradientLUTRows[i]
                    : null;
                if (row == null) {
                    if (whiteRow == null) {
                        whiteRow = new Color32[64];
                        for (int j = 0; j < 64; j++) whiteRow[j] = white;
                    }
                    row = whiteRow;
                }
                gradientLUTTexture.SetPixels32(0, i, 64, 1, row);
            }
            gradientLUTTexture.Apply(false, true);

            textureArrayDirty = false;
        }
    }
}