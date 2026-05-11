#ifndef VOXELPLAY_REALISTIC_WATER
#define VOXELPLAY_REALISTIC_WATER

#define FOAM_SIZE 0.4

sampler2D _WaterBackgroundTexture;
sampler2D _ReflectiveColor;
sampler2D _FoamTex;
sampler2D _FoamGradient;

inline half3 GetWaterNormal(float2 uv) {
    // realistic water must use trilinear mapping to avoid noise
    return UnpackNormal(_BumpMap.Sample(sampler_Trilinear_Repeat, uv));
}


#endif // VOXELPLAY_REALISTIC_WATER

