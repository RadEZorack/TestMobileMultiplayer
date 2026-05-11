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
	VOXELPLAY_SEE_THROUGH_DATA(4,5)
	fixed4 color   : COLOR; 	// always passed to support custom alpha
	VOXELPLAY_NORMAL_DATA
	VOXELPLAY_BUMPMAP_DATA(6)
	VOXELPLAY_PARALLAX_DATA(7)
	VOXELPLAY_SMOOTHNESS_DATA(8)
	VOXELPLAY_MATPROPS_DATA
	VOXELPLAY_GRADIENT_WPOS_DATA(11)
	UNITY_VERTEX_OUTPUT_STEREO
};


v2f vert (appdata v) {
	v2f o;

	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(v2f, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	float3 wpos = UnityObjectToWorldPos(v.vertex);
	VOXELPLAY_MODIFY_VERTEX(v.vertex, wpos)
	VOXELPLAY_SET_GRADIENT_WPOS(o, wpos)
	o.pos    = UnityObjectToClipPos(v.vertex);

	VOXELPLAY_OUTPUT_SEE_THROUGH_DATA(o, wpos)

	float4 uv = v.uv;

	VOXELPLAY_OUTPUT_TINTCOLOR(o);
	#if defined(USES_TINTING)
		o.color.a *= uv.y;
	#else 
		o.color = fixed4(1.0.xxx, uv.y);
	#endif

	int iuvx = (int)uv.x;
	uv.x = iuvx >> 1;
	uv.y = iuvx & 1;

	VOXELPLAY_INITIALIZE_LIGHT_AND_FOG_NORMAL(uv, wpos, v.normal);
	VOXELPLAY_INITIALIZE_MATPROPS(o, uv)

	VOXELPLAY_SET_LIGHT(o, wpos, v.normal);
	VOXELPLAY_SET_TANGENT_SPACE(tang, v.normal)

	VOXELPLAY_OUTPUT_PARALLAX_DATA(wpos, v, uv, o)
	VOXELPLAY_OUTPUT_BUMPMAP_DATA(uv, o)
    VOXELPLAY_OUTPUT_UV(uv, o);
    VOXELPLAY_OUTPUT_SMOOTHNESS_DATA(o)
	return o;
}


fixed4 frag (v2f i) : SV_Target {

	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	
	VOXELPLAY_READ_MATPROPS(i);

	VOXELPLAY_COMPUTE_SCREEN_UV(i);
	
	VOXELPLAY_APPLY_PARALLAX(i);

	// Diffuse
	fixed4 color   = VOXELPLAY_GET_TEXEL(i.uv.xyz);

	#if VOXELPLAY_TRANSP_BLING
	color.ba += (1.0 - color.a) * 0.1 * (frac(_Time.x)>0.99) * (frac(_Time.y + (i.uv.x + i.uv.y) * 0.1) > 0.9);
	#endif

	VOXELPLAY_APPLY_BUMPMAP(i);

	VOXELPLAY_APPLY_METALLIC(color, i);

	#if defined(USE_EMISSION)
		VOXELPLAY_COMPUTE_EMISSION(color, i.uv.xyz)
	#endif

	VOXELPLAY_APPLY_GRADIENT_TINT(color, i.uv, i.gradWpos);

	color *= i.color;

	VOXELPLAY_APPLY_SEE_THROUGH(color, i)

	VOXELPLAY_APPLY_LIGHTING_AND_GI(color, i);

	VOXELPLAY_APPLY_SMOOTHNESS(color, i);

	VOXELPLAY_ADD_EMISSION(color)

	VOXELPLAY_APPLY_FOG(color, i);

	return color;
}

