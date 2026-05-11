#ifndef VOXELPLAY_COMMON
#define VOXELPLAY_COMMON

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
#define SampleSceneDepthProj(grabPos) SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, grabPos)
#define SampleSceneDepthX(uv) SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv)
#define LinearEyeDepthX(x) LinearEyeDepth(x)
#define Linear01DepthX(x) Linear01Depth(x)

#if defined(NO_SELF_SHADOWS)
    #define VOXELPLAY_LIGHT_ATTENUATION(i) max(0, i.light.x + _WorldSpaceLightPos0.y * _VPDaylightShadowAtten * i.light.z)
    #define UNITY_SHADOW_ATTEN(i) 1.0
#else
    #if defined(USE_SOFT_SHADOWS)
        #define VOXELPLAY_SHADOW_ATTENUATION(i) min(1, SHADOW_ATTENUATION(i) + 0.25 + max(0, LinearEyeDepth( i.pos.z ) * _LightShadowData.z + _LightShadowData.w ) )
    #else
        #define VOXELPLAY_SHADOW_ATTENUATION(i) min(1, SHADOW_ATTENUATION(i) + max(0, LinearEyeDepth( i.pos.z ) * _LightShadowData.z + _LightShadowData.w ) )
    #endif
    #define VOXELPLAY_LIGHT_ATTENUATION(i) saturate(VOXELPLAY_SHADOW_ATTENUATION(i) * i.light.x + _WorldSpaceLightPos0.y * _VPDaylightShadowAtten * i.light.z)
    #define UNITY_SHADOW_ATTEN(i) SHADOW_ATTENUATION(i)
#endif

#define UnityObjectToWorldPos(v) mul(unity_ObjectToWorld, v).xyz


#include "VPCommonCore.cginc"


#endif // VOXELPLAY_COMMON

