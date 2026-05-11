
#include "VPCommonURP.cginc"
#include "VPCommonCore.cginc"

float3 _LightDirection;

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
	float3 uv       : TEXCOORD0;
    float4 tangentOS    : TANGENT;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
	float3 uv     : TEXCOORD0;
    float3 normalWS     : TEXCOORD1;
	UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	float3 wpos = UnityObjectToWorldPos(input.positionOS);
	VOXELPLAY_MODIFY_VERTEX(input.positionOS, wpos)

    int iuvz = (int)input.uv.z;
    float disp = (iuvz>>16) * sin(wpos.x + _Time.w) * _VPGrassWindSpeed;
    input.positionOS.x += disp * input.uv.y;

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

    float3 uv = input.uv;
    uv.z = iuvz & 65535; // remove wind animation flag
    output.uv     = uv;

#if defined(USES_URP)
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
#else
    output.normalWS = 1;
#endif

    return output;
}

half4 DepthNormalsFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
	fixed4 color   = UNITY_SAMPLE_TEX2DARRAY(_MainTex, input.uv.xyz);
	clip(color.a - 0.25);

#if defined(USES_URP)
    // Output...
    #if defined(_GBUFFER_NORMALS_OCT)
        float3 normalWS = normalize(input.normalWS);
        float2 octNormalWS = PackNormalOctQuadEncode(normalWS);             // values between [-1, +1], must use fp32 on some platforms
        float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);     // values between [ 0,  1]
        half3 packedNormalWS = half3(PackFloat2To888(remappedOctNormalWS)); // values between [ 0,  1]
        return half4(packedNormalWS, 0.0);
    #else
        return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
    #endif
#else
    return 0;
#endif    
}



