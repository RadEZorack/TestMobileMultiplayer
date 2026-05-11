#ifndef VOXELPLAY_SSAO
#define VOXELPLAY_SSAO

// Support for URP SSAO
#if defined(_SCREEN_SPACE_OCCLUSION) && defined(USES_URP)
    #define VOXELPLAY_SSAO_DATA(idx1) float4 clipPos : TEXCOORD##idx1;
    #define VOXELPLAY_OUTPUT_SSAO(o) o.clipPos = ComputeScreenPos(o.pos);
    #define VOXELPLAY_APPLY_SSAO(color, i) float2 ssaoUV = i.clipPos.xy / i.clipPos.w; AmbientOcclusionFactor ssaoFactor = GetScreenSpaceAmbientOcclusion(ssaoUV); color.rgb *= ssaoFactor.directAmbientOcclusion;
#else
    #define VOXELPLAY_SSAO_DATA(idx1)
    #define VOXELPLAY_OUTPUT_SSAO(o)
    #define VOXELPLAY_APPLY_SSAO(color, i)
#endif

#endif // VOXELPLAY_SSAO

