Shader "Voxel Play/Models/Texture/Triplanar"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" {}
		[NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
		[HDR] _Color ("Color", Color) = (1,1,1,1)
        _CustomDaylightShadowAtten ("Daylight Shadow Atten", Range(0,1)) = 0.65
		 [HideInInspector] _TintColor ("Tint Color", Color) = (1,1,1,1)
		_VoxelLight ("Voxel Light", Range(0,15)) = 15
        _DiffuseWrap ("Diffuse Wrap", Range(0,1)) = 0.7
	}

	SubShader {

		Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"  }
		Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
            #pragma multi_compile_local _ VOXELPLAY_GPU_INSTANCING
			#pragma multi_compile_instancing nolightprobe nolodfade
			#if UNITY_VERSION < 202100
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#elif UNITY_VERSION < 202200
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#else
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#endif
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
			#if UNITY_VERSION >= 60010000
				#pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
				#define USE_FORWARD_PLUS USE_CLUSTER_LIGHT_LOOP
			#elif UNITY_VERSION >= 202200			
				#pragma multi_compile _ _FORWARD_PLUS
			#endif
			#define SUBTLE_SELF_SHADOWS
			#define USE_TRIPLANAR
			#define NON_ARRAY_TEXTURE
            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"
			#include "VPModel.cginc"
			ENDHLSL
		}

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
			#pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag
		    #pragma multi_compile_instancing
			#include "VPModelShadowsURP.cginc"
			ENDHLSL
		}

		UsePass "Voxel Play/Voxels/Triangle/Opaque/DepthOnly"
		UsePass "Voxel Play/Voxels/Triangle/Opaque/DepthNormalsOnly"

	}

	SubShader {

		Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
		Pass {
			Tags { "LightMode" = "ForwardBase" }
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight nodirlightmap
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
            #pragma multi_compile_local _ VOXELPLAY_GPU_INSTANCING
			#pragma multi_compile_instancing nolightprobe nolodfade
			#define SUBTLE_SELF_SHADOWS
			#define USE_TRIPLANAR
			#define NON_ARRAY_TEXTURE
            #include "VPCommon.cginc"
			#include "VPModel.cginc"
			ENDCG
		}

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#pragma multi_compile_instancing
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "VPModelShadows.cginc"
			ENDCG
		}
	}

	Fallback Off
}