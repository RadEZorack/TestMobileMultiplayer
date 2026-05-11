#include "VPCommonCore.cginc"

sampler2D _NoiseTex;

struct appdata {
	float4 vertex   : POSITION;
	float4 uv       : TEXCOORD0;
	float3 normal   : NORMAL;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};


struct v2f {
	float4 pos     : SV_POSITION;
	float4 uv      : TEXCOORD0;
	VOXELPLAY_LIGHT_DATA(1,2)
	VOXELPLAY_FOG_DATA(3)
	VOXELPLAY_NORMAL_DATA
	VOXELPLAY_BUMPMAP_DATA(6)
	VOXELPLAY_PARALLAX_DATA(7)
	float2 flow    : TEXCOORD8;
	VOXELPLAY_SMOOTHNESS_DATA(9)
	VOXELPLAY_MATPROPS_DATA
	UNITY_VERTEX_OUTPUT_STEREO
};


v2f vert (appdata v) {
	v2f o;

	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(v2f, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	float3 wpos = UnityObjectToWorldPos(v.vertex);

    VOXELPLAY_MODIFY_VERTEX(v.vertex, wpos)

    // wave effect
    float noise = tex2Dlod(_NoiseTex, float4(wpos.xz * 0.05, 0, 0)).r;
    v.vertex.y -= 0.5 * _WaveAmplitude * (sin(_Time.z + noise * 10.0) * 0.025 - 0.028);

	o.pos    = UnityObjectToClipPos(v.vertex);

	float4 uv = v.uv;
	int w = (int)v.uv.w;
	o.flow   = float2(((w>>8) & 3) - 1.0, ((w>>10) & 3) - 1.0);

    VOXELPLAY_INITIALIZE_MATPROPS(o, uv)

	o.uv = v.uv;
	o.uv.w = ((w>>13) & 15) / 15.0; // (w & 122880) / 122880.0; // light intensity encoded in bits 13-16 (8192+16384+32768+65536)

	VOXELPLAY_INITIALIZE_LIGHT_AND_FOG_NORMAL_NO_GI(wpos, v.normal);
    o.light.y = ((w>>17) & 15) / 15.0; // assign torch light contribution manually due to special water uv packing
	VOXELPLAY_SET_LIGHT(o, wpos, v.normal);

	VOXELPLAY_SET_TANGENT_SPACE(tang, v.normal)

	VOXELPLAY_OUTPUT_PARALLAX_DATA(wpos, v, uv, o)
	VOXELPLAY_OUTPUT_BUMPMAP_DATA(uv, o)
    VOXELPLAY_OUTPUT_SMOOTHNESS_DATA(o)
	return o;
}


fixed4 frag (v2f i) : SV_Target {

	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	
	VOXELPLAY_READ_MATPROPS(i);

	VOXELPLAY_COMPUTE_SCREEN_UV(i);
	
	VOXELPLAY_APPLY_PARALLAX(i);

	// Animate
	i.uv.xy = i.uv.xy - (_Time.xx * i.flow + _Time.xx * _WaveSpeed) * 0.4;

	// Diffuse
	fixed4 color   = VOXELPLAY_GET_TEXEL_DD(i.uv.xyz);

	VOXELPLAY_APPLY_BUMPMAP(i);

	VOXELPLAY_APPLY_METALLIC(color, i);

	#if defined(USE_EMISSION)
		VOXELPLAY_COMPUTE_EMISSION(color, i.uv.xyz)
	#endif

	VOXELPLAY_APPLY_LIGHTING_AND_GI(color, i);
    
	VOXELPLAY_APPLY_SMOOTHNESS(color, i);

	VOXELPLAY_ADD_EMISSION(color)

	VOXELPLAY_APPLY_FOG(color, i);

	return color;
} 