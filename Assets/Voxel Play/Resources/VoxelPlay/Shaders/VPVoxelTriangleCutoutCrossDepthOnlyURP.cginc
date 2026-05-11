
#include "VPCommonURP.cginc"
#include "VPCommonCore.cginc"

float3 _LightDirection;

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
	float3 uv       : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
	float3 uv     : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input)
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

    return output;
}

half4 DepthOnlyFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
	fixed4 color   = UNITY_SAMPLE_TEX2DARRAY(_MainTex, input.uv.xyz);
	clip(color.a - 0.25);

    return 0;
}



