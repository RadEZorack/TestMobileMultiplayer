#include "VPCommonURP.cginc"
#include "VPCommonCore.cginc"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 uv           : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float3 normalWS     : TEXCOORD1;
    #if VOXELPLAY_USE_NORMAL || defined(VP_CUTOUT)
        float4 uv           : TEXCOORD0;
    #endif
    #if VOXELPLAY_USE_NORMAL
        half4 matProps      : COLOR1;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	float3 wpos = TransformObjectToWorld(input.positionOS.xyz);
	VOXELPLAY_MODIFY_VERTEX(input.positionOS, wpos)

#if VOXELPLAY_USE_NORMAL || defined(VP_CUTOUT)
    float4 uv = input.uv;
    int iuvz = (int)uv.z;
    #if defined(VP_CUTOUT)
        float disp = (iuvz>>16) * sin(wpos.x + wpos.y + _Time.w) * _VPTreeWindSpeed;
        input.positionOS.xy += disp;
        uv.z = iuvz & 65535; // remove wind animation flag
        wpos = TransformObjectToWorld(input.positionOS.xyz);
    #elif defined(USE_ANIMATION)
        int frameCount = (iuvz>>14) & 0xF;
        float speed = (iuvz>>18);
        speed = speed * speed / 8.0;
        uv.z = (iuvz & 16383) + ((uint)(_Time.y * speed) % frameCount);
    #endif

    #if defined(USE_WORLD_SPACE_UV)
        float2 uv2 = VPGetWorldSpaceUV(wpos, input.normalOS);
        //if (uv.y<0.5) uv2.y += 0.002; else uv2.y -= 0.002; // hack: prevents texture bleeding for side voxels -> removed as it causes texture distortion with microvoxels
        uv.xy = uv2;
        uv.xy = TRANSFORM_TEX(uv.xy, _MainTex);
    #endif
    VOXELPLAY_OUTPUT_UV(uv, output)
    #if VOXELPLAY_USE_NORMAL
        float __uvz = (float)(iuvz & 16383);
        output.matProps = tex2Dlod(_VPMatProps, float4((__uvz + 0.5) * _VPMatProps_TexelSize.x, 0.5, 0, 0)) * 255;
    #endif
#endif

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

#if defined(USES_URP)
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
#else
    output.normalWS = 1;
#endif

    return output;
}

float4 DepthNormalsFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(VP_CUTOUT)
    fixed4 color   = VOXELPLAY_GET_TEXEL_DD(input.uv.xyz);
    clip(color.a - _CutOff);
#endif

#if defined(USES_URP)
    float3 normalWS = normalize(input.normalWS);
    
    #if VOXELPLAY_USE_NORMAL
        _vp_matProps = input.matProps;
        UNITY_BRANCH
        if (NORMAL_MAP_OFFSET > 0.5) {
            float3 uv = float3(frac(input.uv.xy), input.uv.z);
            uv.z += NORMAL_MAP_OFFSET;
            half3 normalMap = UNITY_SAMPLE_TEX2DARRAY(_MainTex, uv).xyz;
            float3 tangentNormal = normalMap * 2.0 - 1.0;
            tangentNormal.y *= -1.0;
            
            float3 N = normalWS;
            float3 T = normalize(float3(N.y - N.z, 0, N.x));
            float3 B = cross(T, N);
            normalWS = normalize(tangentNormal.x * T + tangentNormal.y * B + tangentNormal.z * N);
        }
    #endif
    
    // Output...
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octNormalWS = PackNormalOctQuadEncode(normalWS);             // values between [-1, +1], must use fp32 on some platforms
        float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);     // values between [ 0,  1]
        half3 packedNormalWS = half3(PackFloat2To888(remappedOctNormalWS)); // values between [ 0,  1]
        return half4(packedNormalWS, 0.0);
    #else
        return half4(NormalizeNormalPerPixel(normalWS), 0.0);
    #endif
#else
    return 0;
#endif

}

