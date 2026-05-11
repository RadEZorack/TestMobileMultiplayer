
#include "VPCommonURP.cginc"
#include "VPCommonCore.cginc"

struct Attributes
{
    float4 positionOS   : POSITION;
    #if defined(VP_CUTOUT)
        float3 normalOS  : NORMAL;
        float4 uv      : TEXCOORD0;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    #if defined(VP_CUTOUT)
        float4 uv           : TEXCOORD0;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	float3 wpos = TransformObjectToWorld(input.positionOS.xyz);
	VOXELPLAY_MODIFY_VERTEX(input.positionOS, wpos)
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

	#if defined(VP_CUTOUT)
		float4 uv = input.uv;
		int iuvz = (int)uv.z;
		float disp = (iuvz>>16) * sin(wpos.x + wpos.y + _Time.w) * _VPTreeWindSpeed;
		input.positionOS.xy += disp;
		uv.z = iuvz & 65535; // remove wind animation flag

	    #if defined(USE_WORLD_SPACE_UV)
			float2 uv2 = VPGetWorldSpaceUV(wpos, input.normalOS);
			//if (uv.y<0.5) uv2.y += 0.002; else uv2.y -= 0.002; // hack: prevents texture bleeding for side voxels -> removed as it causes texture distortion with microvoxels
			uv.xy = uv2;
			uv.xy = TRANSFORM_TEX(uv.xy, _MainTex);
		#endif
		VOXELPLAY_OUTPUT_UV(uv, output)
	#endif

    return output;
}

half4 DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	#if defined(VP_CUTOUT)
		fixed4 color   = VOXELPLAY_GET_TEXEL_DD(input.uv.xyz);
	    clip(color.a - _CutOff);
	#endif
    
    return 0;
}
