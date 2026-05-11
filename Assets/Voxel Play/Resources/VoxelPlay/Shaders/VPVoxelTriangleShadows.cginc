#include "VPCommon.cginc"

struct appdata {
	float4 vertex   : POSITION;
	float3 normal   : NORMAL;
	#if defined(VP_CUTOUT)
		float4 uv      : TEXCOORD0;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f {
	#if defined(VP_CUTOUT)
		float4 uv      : TEXCOORD0;
	#endif
	V2F_SHADOW_CASTER;
	UNITY_VERTEX_OUTPUT_STEREO
};


v2f vert (appdata v) {
	v2f o;

	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(v2f, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    #if defined(IS_CLOUD)
        v.vertex.xyz *= float3(4, 2, 4);
    #endif
	float3 wpos = UnityObjectToWorldPos(v.vertex);
	VOXELPLAY_MODIFY_VERTEX(v.vertex, wpos)

	#if defined(VP_CUTOUT)
		float4 uv = v.uv;
		int iuvz = (int)uv.z;
		float disp = (iuvz>>16) * sin(wpos.x + wpos.y + _Time.w) * _VPTreeWindSpeed;
		v.vertex.xy += disp;
		uv.z = iuvz & 65535; // remove wind animation flag

	    #if defined(USE_WORLD_SPACE_UV)
			float2 uv2 = VPGetWorldSpaceUV(wpos, v.normal);
			uv.xy = uv2;
			uv.xy = TRANSFORM_TEX(uv.xy, _MainTex);
		#endif
		VOXELPLAY_OUTPUT_UV(uv, o)
	#endif

	TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);

	return o;
}

fixed4 frag (v2f i) : SV_Target {
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	#if defined(VP_CUTOUT)
		fixed4 color   = VOXELPLAY_GET_TEXEL_DD(i.uv.xyz);
	    clip(color.a - _CutOff);
	#endif

	SHADOW_CASTER_FRAGMENT(i)
}

