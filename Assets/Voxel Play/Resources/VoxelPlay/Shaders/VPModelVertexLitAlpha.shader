Shader "Voxel Play/Models/VertexLitAlpha"
{
	Properties
	{
		[HideInInspector] _MainTex ("Main Texture", 2D) = "white" {}
		[HDR] _Color ("Color", Color) = (1,1,1,1)
        _CustomDaylightShadowAtten ("Daylight Shadow Atten", Range(0,1)) = 0.65
		 [HideInInspector] _TintColor ("Per Instance Tint Color", Color) = (1,1,1,1)
		_VoxelLight ("Voxel Light", Range(0,15)) = 15
	}
	SubShader {

		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"  }
		Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
            #pragma multi_compile_local _ VOXELPLAY_GPU_INSTANCING
			#pragma multi_compile_instancing nolightprobe nolodfade
			#if UNITY_VERSION >= 60010000
				#pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
				#define USE_FORWARD_PLUS USE_CLUSTER_LIGHT_LOOP
			#elif UNITY_VERSION >= 202200			
				#pragma multi_compile _ _FORWARD_PLUS
			#endif
			#define SUBTLE_SELF_SHADOWS
			#define NON_ARRAY_TEXTURE
			#define NO_SHADOWS
            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"
			#include "VPModel.cginc"
			ENDHLSL
		}
	}

	SubShader {

		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		Pass {
			Tags { "LightMode" = "ForwardBase" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
            #pragma multi_compile_local _ VOXELPLAY_GPU_INSTANCING
			#pragma multi_compile_instancing nolightprobe nolodfade
			#define SUBTLE_SELF_SHADOWS
			#define NON_ARRAY_TEXTURE
			#define NO_SHADOWS
            #include "VPCommon.cginc"
			#include "VPModel.cginc"
			ENDCG
		}
	}
	Fallback Off
}