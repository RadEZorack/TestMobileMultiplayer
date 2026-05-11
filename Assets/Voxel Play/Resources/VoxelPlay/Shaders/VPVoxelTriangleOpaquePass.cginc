#include "VPCommonBevel.cginc"

struct appdata {
	float4 vertex   : POSITION;
	float4 uv       : TEXCOORD0;
	float3 normal   : NORMAL;
	VOXELPLAY_TINTCOLOR_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};


struct v2f {
	float4 pos     : SV_POSITION;
	float4 uv      : TEXCOORD0;
	VOXELPLAY_LIGHT_DATA(1,2)
	VOXELPLAY_FOG_DATA(3)
	SHADOW_COORDS(4)
	VOXELPLAY_TINTCOLOR_DATA
	VOXELPLAY_BUMPMAP_DATA(5)
	VOXELPLAY_PARALLAX_DATA(6)
	VOXELPLAY_SSAO_DATA(7)
	VOXELPLAY_NORMAL_DATA
    VOXELPLAY_SMOOTHNESS_DATA(8)
    VOXELPLAY_MATPROPS_DATA
    VOXELPLAY_GRADIENT_WPOS_DATA(11)
	UNITY_VERTEX_OUTPUT_STEREO
};

struct vertexInfo {
	float4 vertex;
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
	VOXELPLAY_SET_GRADIENT_WPOS(o, wpos)

	float4 uv = v.uv;
	#if defined(VP_CUTOUT)
		int iuvz = (int)uv.z;
		float disp = (iuvz>>16) * sin(wpos.x + wpos.y + _Time.w) * _VPTreeWindSpeed;
		v.vertex.xy += disp;
		uv.z = iuvz & 65535; // remove wind animation flag
    #elif defined(USE_ANIMATION)
		int iuvz = (int)uv.z;
        int frameCount = (iuvz>>14) & 0xF;
        float speed = (iuvz>>18);
        speed = speed * speed / 8.0;
		uv.z = (iuvz & 16383) + ((uint)(_Time.y * speed) % frameCount);
	#endif

	#if defined(USE_PACKED_LIGHT)
		uv.w = _VoxelLight;
	#endif

	o.pos    = UnityObjectToClipPos(v.vertex);

    #if defined(IS_CLOUD)
        // Recalculate clip-space Z using an extended far clip to prevent cloud clipping
        float cloudFarClip = max(_ProjectionParams.z, 10000.0);
        float n = _ProjectionParams.y;
        float d = o.pos.w; // view-space depth
        #if UNITY_REVERSED_Z
            o.pos.z = n * (cloudFarClip - d) / (cloudFarClip - n);
        #else
            o.pos.z = (d * (cloudFarClip + n) - 2.0 * cloudFarClip * n) / (cloudFarClip - n);
        #endif
    #endif

    #if defined(USE_WORLD_SPACE_NORMAL)
        v.normal = UnityObjectToWorldNormal(v.normal);
    #endif

	VOXELPLAY_OUTPUT_TINTCOLOR(o);
	VOXELPLAY_INITIALIZE_MATPROPS(o, uv)
	VOXELPLAY_INITIALIZE_LIGHT_AND_FOG_NORMAL(uv, wpos, v.normal);
	VOXELPLAY_SET_LIGHT(o, wpos, v.normal);
	TRANSFER_SHADOW(o);

	VOXELPLAY_SET_TANGENT_SPACE(tang, v.normal)

    #if defined(USE_WORLD_SPACE_UV)
		// microvoxel local-uv flag to force local uv coordinates instead of world space
		const int VP_FLAG_LOCAL_UV = (1<<23);
		bool vp_useLocalUV = (((int)v.uv.z) & VP_FLAG_LOCAL_UV) != 0;
		uv.z = (int)uv.z & ~VP_FLAG_LOCAL_UV;
	    if (!vp_useLocalUV) {
	        float2 uv2 = VPGetWorldSpaceUV(wpos, v.normal);
	        uv.xy = TRANSFORM_TEX(uv2, _MainTex);
	    }
    #endif

	VOXELPLAY_OUTPUT_PARALLAX_DATA(wpos, v, uv, o)
	VOXELPLAY_OUTPUT_BUMPMAP_DATA(uv, o)
	VOXELPLAY_OUTPUT_UV(uv, o)
	VOXELPLAY_OUTPUT_SSAO(o)
    VOXELPLAY_OUTPUT_SMOOTHNESS_DATA(o)
	return o;
}

fixed4 VPVoxelTriangleFragment(v2f i) {
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    VOXELPLAY_READ_MATPROPS(i);
    
    VOXELPLAY_COMPUTE_SCREEN_UV(i);

    VOXELPLAY_COMPUTE_BEVEL(i);

    VOXELPLAY_APPLY_PARALLAX(i);

	// Diffuse
	fixed4 color = VOXELPLAY_GET_TEXEL_DD(i.uv.xyz);

	#if defined(VP_CUTOUT)
	    clip(color.a - _CutOff);
	#endif

	VOXELPLAY_APPLY_BEVEL(i);

	VOXELPLAY_APPLY_BUMPMAP(i);

	VOXELPLAY_APPLY_METALLIC(color, i);

	#if defined(USE_EMISSION)
		VOXELPLAY_COMPUTE_EMISSION(color, i.uv.xyz)
	#endif

	VOXELPLAY_APPLY_GRADIENT_TINT(color, i.uv, i.gradWpos);
	VOXELPLAY_APPLY_TINTCOLOR(color, i);

	VOXELPLAY_APPLY_OUTLINE_SIMPLE(color, i);

    #if defined(IS_CLOUD)
        VOXELPLAY_APPLY_LIGHTING(color, i);
    #else
		VOXELPLAY_APPLY_SSAO(color, i);
	    VOXELPLAY_APPLY_LIGHTING_AO_AND_GI(color, i);
    #endif

 	VOXELPLAY_APPLY_SMOOTHNESS(color, i);	

	VOXELPLAY_ADD_EMISSION(color)

	VOXELPLAY_APPLY_FOG(color, i);

	return color;
}

fixed4 frag (v2f i) : SV_Target {
	return VPVoxelTriangleFragment(i);
}

