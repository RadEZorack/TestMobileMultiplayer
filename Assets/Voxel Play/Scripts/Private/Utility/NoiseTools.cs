using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

	public static partial class NoiseTools {

		/// <summary>
		/// Random seeded offsets to the terrain sampling. Used to provide different terrain outputs by translating the zero position.
		/// </summary>
		public static Vector3 seedOffset;

		/// <summary>
		/// Misc. useful functions for terrain generators
		/// </summary>
		/// <returns>The noise values from texture.</returns>
		/// <param name="tex">Tex.</param>
		/// <param name="textureSize">Texture size.</param>
		public static float[] LoadNoiseTexture(Texture tex, out int textureSize) {
			if (tex == null) {
				textureSize = 0;
				return null;
			}
			textureSize = tex.width;
            Color[] temp;
			if (tex is Texture2D) {
				Texture2D tex2D = (Texture2D)tex;
				temp = tex2D.GetPixels();
			} else if (tex is Texture3D) {
				Texture3D tex3D = (Texture3D)tex;
				temp = tex3D.GetPixels();
			} else return null;
				
			int count = temp.Length;
			float[] values = new float[count];
			for (int k = 0; k < temp.Length; k++) {
				values[k] = temp[k].r;
			}
			return values;
		}

        /// <summary>
        /// Misc. useful functions for terrain generators
        /// </summary>
        /// <returns>The values from the heightmap.</returns>
        public static float[] LoadHeightmapFromTerrainData(TerrainData td, out int size) {
            if (td == null) {
                size = 0;
                return null;
            }
            size = td.heightmapResolution;
            float[,] heightmap = td.GetHeights(0, 0, size, size);

            float[] values = new float[size * size];
            for (int i = 0; i < size; i++) {
                for (int j = 0; j < size; j++) {
                    values[i * size + j] = heightmap[i, j];
                }
            }
            return values;
        }


        /// <summary>
        /// Samples a 2D noise texture at a given world position (returns raw value)
        /// </summary>
        /// <returns>The noise value one sample.</returns>
        /// <param name="noiseArray">Noise array.</param>
        /// <param name="textureSize">Texture size.</param>
        /// <param name="x">The x coordinate.</param>
        /// <param name="z">The z coordinate.</param>
        public static float GetNoiseValue(float[] noiseArray, int textureSize, double x, double z, bool ridgeNoise = false) {

			if (textureSize == 0)
				return 0;

			double zz = z + textureSize * 10000 + seedOffset.z;
			double xx = x + textureSize * 10000 + seedOffset.x;
			int posZInt = (int)zz;
			int posXInt = (int)xx;

			// Texture array position (ensure indices are in [0, textureSize-1] even for negative coordinates)
			int ty0 = posZInt % textureSize;
			if (ty0 < 0) ty0 += textureSize;
			int tx0 = posXInt % textureSize;
			if (tx0 < 0) tx0 += textureSize;

			float value = noiseArray[ty0 * textureSize + tx0];
			if (ridgeNoise) {
				value = 0.5f - value;
				if (value < 0) {
					value = 2f * (0.5f + value);
				} else {
					value = 2f * (0.5f - value);
				}
			}
			return value;
		}

		/// <summary>
		/// Samples a 2D noise texture at given world position using bilinear filtering
		/// </summary>
		/// <returns>The noise value bilinear.</returns>
		/// <param name="noiseArray">Noise array.</param>
		/// <param name="textureSize">Texture size.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="z">The z coordinate.</param>
		public static float GetNoiseValueBilinear(float[] noiseArray, int textureSize, double x, double z, bool ridgeNoise = false) {

			if (textureSize == 0)
				return 0;

			double zz = z + textureSize * 10000 + seedOffset.z;
			double xx = x + textureSize * 10000 + seedOffset.x;
			int posZInt = (int)zz;
			int posXInt = (int)xx;
			float fy = (float)(zz - posZInt);
			float fx = (float)(xx - posXInt);

			// Texture array position (ensure indices are in [0, textureSize-1] even for negative coordinates)
			int ty0 = posZInt % textureSize;
			if (ty0 < 0) ty0 += textureSize;
			int tx0 = posXInt % textureSize;
			if (tx0 < 0) tx0 += textureSize;

			// Get noise for upper/left corner
			int ty, tx;
			ty = (ty0 == textureSize - 1) ? 0 : ty0 + 1;
			float noiseUL = noiseArray[ty * textureSize + tx0];
			// Get noise for upper/right corner
			tx = (tx0 == textureSize - 1) ? 0 : tx0 + 1;
			float noiseUR = noiseArray[ty * textureSize + tx];
			// Get noise for bottom/left corner
			float noiseBL = noiseArray[ty0 * textureSize + tx0];
			// Get noise for bottom/right corner
			float noiseBR = noiseArray[ty0 * textureSize + tx];

			// Bilinear interpolation
			float value =
				(1f - fx) * (fy * noiseUL + (1f - fy) * noiseBL) +
				fx * (fy * noiseUR + (1f - fy) * noiseBR);

			if (ridgeNoise) {
				value = 0.5f - value;
				if (value < 0) {
					value = 2f * (0.5f + value);
				} else {
					value = 2f * (0.5f - value);
				}
			}
			return value;
		}


		/// <summary>
		/// Samples a 3D noise texture at a given world position (returns raw value)
		/// </summary>
		/// <returns>The noise value one sample.</returns>
		/// <param name="noiseArray">Noise array.</param>
		/// <param name="textureSize">Texture size.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="z">The z coordinate.</param>
		public static float GetNoiseValue(float[] noiseArray, int textureSize, double x, double y, double z) {

			float f = GetNoiseValue (noiseArray, textureSize, x, z);
			float t = GetNoiseValue (noiseArray, textureSize, 1, y);

			f = f * t * 2.0f;
			if (f > 1f)
				f--;
			return f;

		}

        public static float GetFractalNoiseValue(double x, double z, float frequency, int octaves, float persistence, float lacunarity) {
            float currentFrequency = Mathf.Max(0.0001f, frequency);
            int octaveCount = Mathf.Max(1, octaves);
            float amplitude = 1f;
            float amplitudeTotal = 0f;
            float value = 0f;
            float sampleOffsetX = seedOffset.x + 10000f;
            float sampleOffsetZ = seedOffset.z + 10000f;
            float frequencyMultiplier = lacunarity > 0f ? lacunarity : 1f;

            for (int i = 0; i < octaveCount; i++) {
                float sampleX = (float)(x * currentFrequency) + sampleOffsetX;
                float sampleZ = (float)(z * currentFrequency) + sampleOffsetZ;
                float octaveValue = Mathf.PerlinNoise(sampleX, sampleZ);
                value += octaveValue * amplitude;
                amplitudeTotal += amplitude;
                currentFrequency *= frequencyMultiplier;
                amplitude *= persistence;
            }

            return amplitudeTotal > 0f ? value / amplitudeTotal : 0f;
        }

        public static float ApplyTerraces(float value, int terraceCount, float smoothness) {
            int clampedTerraceCount = Mathf.Max(2, terraceCount);
            float clampedSmoothness = Mathf.Clamp01(smoothness);
            float scaled = value * clampedTerraceCount;
            float lowerIndex = Mathf.Floor(scaled);
            float fraction = scaled - lowerIndex;
            float lower = lowerIndex / clampedTerraceCount;
            float upper = (lowerIndex + 1f) / clampedTerraceCount;

            if (clampedSmoothness <= 0f) {
                return lower;
            }

            float plateauEnd = 1f - clampedSmoothness;
            if (fraction <= plateauEnd) {
                return lower;
            }

            float t = (fraction - plateauEnd) / clampedSmoothness;
            t = t * t * (3f - 2f * t);
            return Mathf.LerpUnclamped(lower, upper, t);
        }


	}

}
