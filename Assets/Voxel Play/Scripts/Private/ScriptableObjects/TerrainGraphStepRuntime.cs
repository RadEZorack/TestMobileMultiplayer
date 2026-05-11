using System;
using UnityEngine;

namespace VoxelPlay {

    public static class TerrainGraphStepRuntime {

        public static bool UsesImplicitFlowSentinel(TerrainStepType operation) {
            switch (operation) {
                case TerrainStepType.BeachMask:
                case TerrainStepType.Fill:
                    return true;
                default:
                    return false;
            }
        }

        public static void SanitizeInputReferences(StepData[] steps) {
            if (steps == null) return;

            int stepsLength = steps.Length;
            for (int i = 0; i < stepsLength; i++) {
                steps[i].inputIndex0 = SanitizeInputIndex(stepsLength, steps[i].inputIndex0);
                if (!UsesImplicitFlowSentinel(steps[i].operation)) {
                    steps[i].inputIndex1 = SanitizeInputIndex(stepsLength, steps[i].inputIndex1);
                }
            }
        }

        static int SanitizeInputIndex(int stepsLength, int index) {
            return index >= 0 && index < stepsLength ? index : -1;
        }

        static bool TryGetReferencedValue(StepData[] steps, int index, out float value) {
            if (steps != null && index >= 0 && index < steps.Length) {
                value = steps[index].value;
                return true;
            }
            value = 0f;
            return false;
        }

        public static void EvaluateSteps(StepData[] steps, double x, double z, float invMaxHeight, out bool allowBeach) {
            allowBeach = true;
            if (steps == null || steps.Length == 0) return;

            float value = 0f;
            int stepsLength = steps.Length;
            for (int k = 0; k < stepsLength; k++) {
                float nextValue = value;
                if (steps[k].enabled) {
                    switch (steps[k].operation) {
                        case TerrainStepType.SampleHeightMapTexture:
                        case TerrainStepType.SampleHeightMapUnityTerrain:
                            nextValue = NoiseTools.GetNoiseValueBilinear(steps[k].noiseValues, steps[k].noiseTextureSize,
                                x * steps[k].frecuency + steps[k].offset.x, z * steps[k].frecuency + steps[k].offset.y);
                            nextValue = nextValue * (steps[k].noiseRangeMax - steps[k].noiseRangeMin) + steps[k].noiseRangeMin;
                            break;
                        case TerrainStepType.SampleRidgeNoiseFromTexture:
                            nextValue = NoiseTools.GetNoiseValueBilinear(steps[k].noiseValues, steps[k].noiseTextureSize,
                                x * steps[k].frecuency + steps[k].offset.x, z * steps[k].frecuency + steps[k].offset.y, true);
                            nextValue = nextValue * (steps[k].noiseRangeMax - steps[k].noiseRangeMin) + steps[k].noiseRangeMin;
                            break;
                        case TerrainStepType.SampleHeightMapFractal:
                            // Legacy path: sample fractal octaves from the step's noise texture when one is supplied.
                            // Preserves terrain shape for generators authored before the graph-editor refactor.
                            if (steps[k].noiseValues != null && steps[k].noiseValues.Length > 0) {
                                float fractalFreq = steps[k].frecuency;
                                float fractalAmplitude = 1f;
                                float fractalAmplitudeTotal = 0f;
                                nextValue = 0f;
                                for (int j = 0; j < steps[k].octaves; j++) {
                                    float octaveValue = NoiseTools.GetNoiseValueBilinear(steps[k].noiseValues, steps[k].noiseTextureSize, x * fractalFreq, z * fractalFreq);
                                    nextValue += octaveValue * fractalAmplitude;
                                    fractalAmplitudeTotal += fractalAmplitude;
                                    fractalFreq *= steps[k].lacunarity;
                                    fractalAmplitude *= steps[k].persistence;
                                }
                                if (fractalAmplitudeTotal > 0f) nextValue /= fractalAmplitudeTotal;
                            } else {
                                nextValue = NoiseTools.GetFractalNoiseValue(x, z, steps[k].frecuency, steps[k].octaves, steps[k].persistence, steps[k].lacunarity);
                            }
                            nextValue = nextValue * (steps[k].noiseRangeMax - steps[k].noiseRangeMin) + steps[k].noiseRangeMin;
                            break;
                        case TerrainStepType.Shift:
                            nextValue += steps[k].param;
                            break;
                        case TerrainStepType.BeachMask:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float beachMask)) {
                                if (beachMask > steps[k].threshold) {
                                    allowBeach = false;
                                }
                            }
                            break;
                        case TerrainStepType.AddAndMultiply:
                            nextValue = (nextValue + steps[k].param) * steps[k].param2;
                            break;
                        case TerrainStepType.MultiplyAndAdd:
                            nextValue = (nextValue * steps[k].param) + steps[k].param2;
                            break;
                        case TerrainStepType.Exponential:
                            if (nextValue < 0f) {
                                nextValue = 0f;
                            }
                            nextValue = (float)Math.Pow(nextValue, steps[k].param);
                            break;
                        case TerrainStepType.Remap:
                            if (Mathf.Approximately(steps[k].min, steps[k].max)) {
                                nextValue = steps[k].threshold;
                            } else {
                                float t = (nextValue - steps[k].min) / (steps[k].max - steps[k].min);
                                nextValue = Mathf.LerpUnclamped(steps[k].threshold, steps[k].thresholdParam, t);
                            }
                            break;
                        case TerrainStepType.Abs:
                            nextValue = Mathf.Abs(nextValue);
                            break;
                        case TerrainStepType.Terraces: {
                                float terraced = NoiseTools.ApplyTerraces(nextValue, steps[k].octaves, steps[k].param);
                                nextValue = Mathf.Lerp(nextValue, terraced, Mathf.Clamp01(steps[k].param2));
                            }
                            break;
                        case TerrainStepType.Constant:
                            nextValue = steps[k].param;
                            break;
                        case TerrainStepType.Invert:
                            nextValue = 1f - nextValue;
                            break;
                        case TerrainStepType.Copy:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float copiedValue)) {
                                nextValue = copiedValue;
                            }
                            break;
                        case TerrainStepType.Random:
                            nextValue = WorldRand.GetValue(x, z);
                            break;
                        case TerrainStepType.BlendAdditive:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float blendA)
                                && TryGetReferencedValue(steps, steps[k].inputIndex1, out float blendB)) {
                                nextValue = blendA * steps[k].weight0 + blendB * steps[k].weight1;
                            }
                            break;
                        case TerrainStepType.BlendMultiply:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float multA)
                                && TryGetReferencedValue(steps, steps[k].inputIndex1, out float multB)) {
                                nextValue = multA * multB;
                            }
                            break;
                        case TerrainStepType.Min:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float minA)
                                && TryGetReferencedValue(steps, steps[k].inputIndex1, out float minB)) {
                                nextValue = Mathf.Min(minA, minB);
                            }
                            break;
                        case TerrainStepType.Max:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float maxA)
                                && TryGetReferencedValue(steps, steps[k].inputIndex1, out float maxB)) {
                                nextValue = Mathf.Max(maxA, maxB);
                            }
                            break;
                        case TerrainStepType.Subtract:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float subtractA)
                                && TryGetReferencedValue(steps, steps[k].inputIndex1, out float subtractB)) {
                                nextValue = subtractA - subtractB;
                            }
                            break;
                        case TerrainStepType.Divide:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float dividend)
                                && TryGetReferencedValue(steps, steps[k].inputIndex1, out float divisorSource)) {
                                nextValue = Mathf.Abs(divisorSource) > 0.000001f ? dividend / divisorSource : 0f;
                            }
                            break;
                        case TerrainStepType.Threshold:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float thresholdValue)) {
                                nextValue = thresholdValue >= steps[k].threshold
                                    ? thresholdValue + steps[k].thresholdShift
                                    : steps[k].thresholdParam;
                            }
                            break;
                        case TerrainStepType.Island:
                            float d = (float)Math.Sqrt(x * x + z * z);
                            d -= steps[k].param;
                            if (d > 0f) {
                                nextValue -= d * steps[k].param2 * invMaxHeight;
                            }
                            break;
                        case TerrainStepType.FlattenOrRaise:
                            if (nextValue >= steps[k].threshold) {
                                nextValue = (nextValue - steps[k].threshold) * steps[k].thresholdParam + steps[k].threshold;
                            }
                            break;
                        case TerrainStepType.Clamp:
                            if (nextValue < steps[k].min) {
                                nextValue = steps[k].min;
                            } else if (nextValue > steps[k].max) {
                                nextValue = steps[k].max;
                            }
                            break;
                        case TerrainStepType.Select:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float selectedValue)) {
                                nextValue = selectedValue < steps[k].min || selectedValue > steps[k].max
                                    ? steps[k].thresholdParam
                                    : selectedValue;
                            }
                            break;
                        case TerrainStepType.Fill:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float fillValue)) {
                                if (fillValue >= steps[k].min && fillValue <= steps[k].max) {
                                    nextValue = steps[k].thresholdParam;
                                }
                            }
                            break;
                        case TerrainStepType.Test:
                            if (TryGetReferencedValue(steps, steps[k].inputIndex0, out float testValue)) {
                                nextValue = testValue >= steps[k].min && testValue <= steps[k].max ? 1f : 0f;
                            }
                            break;
                    }
                }

                value = nextValue;
                steps[k].value = nextValue;
            }
        }
    }
}
