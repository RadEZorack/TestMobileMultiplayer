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
	VOXELPLAY_NORMAL_DATA
	VOXELPLAY_MATPROPS_DATA
	VOXELPLAY_GRADIENT_WPOS_DATA(9)
	UNITY_VERTEX_OUTPUT_STEREO
};

struct vertexInfo {
	float4 vertex;
};

#if defined(USE_6_TEXTURES) || defined(USE_3_TEXTURES)
    sampler _BottomTex;
    sampler _LeftTex;
    sampler _RightTex;
    sampler _ForwardTex;
    sampler _BackTex;
    texture2D _BottomBumpMap;
    texture2D _LeftBumpMap;
    texture2D _RightBumpMap;
    texture2D _ForwardBumpMap;
    texture2D _BackBumpMap;
    float4 _BottomTex_TexelSize;
    float4 _BottomTex_ST;
    float4 _LeftTex_ST;
    float4 _LeftTex_TexelSize;
    float4 _RightTex_ST;
    float4 _RightTex_TexelSize;
    float4 _ForwardTex_ST;
    float4 _ForwardTex_TexelSize;
    float4 _BackTex_ST;
    float4 _BackTex_TexelSize;
#endif

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
        #if defined (VOXELPLAY_USE_NORMAL) || defined (VOXELPLAY_USE_PARALLAX)
			uv.z = (iuvz & 16383) + ((uint)(_Time.y * speed) % frameCount) * 2;
		#else
			uv.z = (iuvz & 16383) + ((uint)(_Time.y * speed) % frameCount);
		#endif
	#endif

	o.pos    = UnityObjectToClipPos(v.vertex);

    #if defined(USE_WORLD_SPACE_NORMAL)
        v.normal = UnityObjectToWorldNormal(v.normal);
    #endif

	VOXELPLAY_OUTPUT_TINTCOLOR(o);
	VOXELPLAY_INITIALIZE_LIGHT_AND_FOG_NORMAL(uv, wpos, v.normal);
	VOXELPLAY_INITIALIZE_MATPROPS(o, uv)

	VOXELPLAY_SET_LIGHT(o, wpos, v.normal);
	TRANSFER_SHADOW(o);

	VOXELPLAY_SET_TANGENT_SPACE(tang, v.normal)

    #if defined(USE_WORLD_SPACE_UV)
        uv.xy = VPGetWorldSpaceUV(wpos, v.normal);
    #endif

    #if defined(USE_6_TEXTURES)
        if (v.normal.y>0) {
            uv.xy = TRANSFORM_TEX(uv.xy, _MainTex);
        } else if (v.normal.y<0) {
            uv.xy = TRANSFORM_TEX(uv.xy, _BottomTex);
        } else if (v.normal.x<0) {
            uv.xy = TRANSFORM_TEX(uv.xy, _LeftTex);
        } else if (v.normal.x>0) {
            uv.xy = TRANSFORM_TEX(uv.xy, _RightTex);
        } else if (v.normal.z<0) {
            uv.xy = TRANSFORM_TEX(uv.xy, _BackTex);
        } else {
            uv.xy = TRANSFORM_TEX(uv.xy, _ForwardTex);
        }
    #elif defined(USE_3_TEXTURES)
        if (v.normal.y>0) {
            uv.xy = TRANSFORM_TEX(uv.xy, _MainTex);
        } else if (v.normal.y<0) {
            uv.xy = TRANSFORM_TEX(uv.xy, _BottomTex);
        } else {
            uv.xy = TRANSFORM_TEX(uv.xy, _BackTex);
        }
    #else
        uv.xy = TRANSFORM_TEX(uv.xy, _MainTex);
    #endif

	VOXELPLAY_OUTPUT_PARALLAX_DATA(wpos, v, uv, o)
	VOXELPLAY_OUTPUT_BUMPMAP_DATA(uv, o)
	VOXELPLAY_OUTPUT_UV(uv, o)

	return o;
}


fixed4 frag (v2f i) : SV_Target {
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	VOXELPLAY_READ_MATPROPS(i);

	VOXELPLAY_COMPUTE_SCREEN_UV(i);

	VOXELPLAY_APPLY_PARALLAX(i);

	// Diffuse
    fixed4 color;
    float4 nrm;
    #if defined(USE_6_TEXTURES)
        if (i.norm.y>0) {
          	color   = VOXELPLAY_GET_TEXEL_DD_X(_MainTex, _MainTex_TexelSize.zw, i.uv.xy);
            nrm = _BumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        } else if (i.norm.y<0) {
            color   = VOXELPLAY_GET_TEXEL_DD_X(_BottomTex, _BottomTex_TexelSize.zw, i.uv.xy);
            nrm = _BottomBumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        } else if (i.norm.x<0) {
            color   = VOXELPLAY_GET_TEXEL_DD_X(_LeftTex, _LeftTex_TexelSize.zw, i.uv.xy);
            nrm = _LeftBumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        } else if (i.norm.x>0) {
            color   = VOXELPLAY_GET_TEXEL_DD_X(_RightTex, _RightTex_TexelSize.zw, i.uv.xy);
            nrm = _RightBumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        } else if (i.norm.z<0) {
            color   = VOXELPLAY_GET_TEXEL_DD_X(_BackTex, _BackTex_TexelSize.zw, i.uv.xy);
            nrm = _BackBumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        } else {
            color   = VOXELPLAY_GET_TEXEL_DD_X(_ForwardTex, _ForwardTex_TexelSize.zw, i.uv.xy);
            nrm = _ForwardBumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        }
    #elif defined(USE_3_TEXTURES)
        if (i.norm.y>0) {
          	color   = VOXELPLAY_GET_TEXEL_DD_X(_MainTex, _MainTex_TexelSize.zw, i.uv.xy);
            nrm = _BumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        } else if (i.norm.y<0) {
            color   = VOXELPLAY_GET_TEXEL_DD_X(_BottomTex, _BottomTex_TexelSize.zw, i.uv.xy);
            nrm = _BottomBumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        } else {
            color   = VOXELPLAY_GET_TEXEL_DD_X(_BackTex, _BackTex_TexelSize.zw, i.uv.xy);
            nrm = _BackBumpMap.Sample(sampler_Point_Repeat, i.uv.xy);
        }
    #else
    	color = VOXELPLAY_GET_TEXEL_DD_X(_MainTex, _MainTex_TexelSize.zw, i.uv.xy);
        nrm = 0;
    #endif

	#if defined(VP_CUTOUT)
	    clip(color.a - 0.5);
	#endif

	VOXELPLAY_APPLY_METALLIC(color, i);

	VOXELPLAY_COMPUTE_EMISSION(color, i.uv.xyz)

	VOXELPLAY_APPLY_CUSTOM_BUMPMAP(i, nrm);

	VOXELPLAY_APPLY_GRADIENT_TINT(color, i.uv, i.gradWpos);
	VOXELPLAY_APPLY_TINTCOLOR(color, i);

	VOXELPLAY_APPLY_OUTLINE_SIMPLE(color, i);

    #if defined(IS_CLOUD)
        VOXELPLAY_APPLY_LIGHTING(color, i);
    #else
	    VOXELPLAY_APPLY_LIGHTING_AO_AND_GI(color, i);
    #endif

	VOXELPLAY_ADD_EMISSION(color)

	VOXELPLAY_APPLY_FOG(color, i);

	return color;
}

