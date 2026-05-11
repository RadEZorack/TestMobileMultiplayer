#ifndef VOXELPLAY_COMMON_INPUT
#define VOXELPLAY_COMMON_INPUT

#ifndef NON_ARRAY_TEXTURE
    UNITY_DECLARE_TEX2DARRAY(_MainTex); 
#else
    sampler _MainTex;
#endif

CBUFFER_START(UnityPerMaterial)

#ifndef SURFACE_SHADER
    float4 _MainTex_ST;
#endif

float4 _MainTex_TexelSize;
fixed _CutOff;
int _VoxelLight;

#ifdef NON_ARRAY_TEXTURE
    fixed4 _Color;
#endif

#ifdef SUBTLE_SELF_SHADOWS
    fixed _CustomDaylightShadowAtten;
    #define _VPDaylightShadowAtten _CustomDaylightShadowAtten
#endif

#if defined(IS_WATER) || defined(IS_REALISTIC_WATER)
    float _WaveSpeed;
#endif

#ifdef IS_WATER
    float _WaveAmplitude;
    half _SpecularPower, _SpecularIntensity;
    half4 _FoamColor;
#endif

#ifdef IS_REALISTIC_WATER
    float _WaveScale, _RefractionDistortion, _Fresnel;
    half _NormalStrength;
    half4 _WaterColor, _UnderWaterFogColor;
    half3 _OceanWave;
#endif

float _VPParallaxStrength, _VPParallaxMaxDistanceSqr;
int _VPParallaxIterations, _VPParallaxIterationsBinarySearch;    

float4 _VPMatProps_TexelSize;

CBUFFER_END

#endif // VOXELPLAY_COMMON_INPUT

