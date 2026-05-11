Shader "Voxel Play/Models/GPU Instanced Indirect/Texture/Cutout"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" {}
		[HDR] _Color ("Color", Color) = (1,1,1,1)
		[HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _CustomDaylightShadowAtten ("Daylight Shadow Atten", Range(0,1)) = 0.65
	}

	SubShader {

		Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" }
        Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
		    HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile _ VOXELPLAY_USE_ROTATION
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
			#pragma multi_compile_instancing nolightprobe nolodfade
			#if UNITY_VERSION >= 60010000
				#pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
				#define USE_FORWARD_PLUS USE_CLUSTER_LIGHT_LOOP
			#elif UNITY_VERSION >= 202200			
				#pragma multi_compile _ _FORWARD_PLUS
			#endif
			#define SUBTLE_SELF_SHADOWS
			#define USE_TEXTURE
			#define NON_ARRAY_TEXTURE
            #define VP_CUTOUT
            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"
			#include "VPIndirect.cginc"
			ENDHLSL
		}

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_instancing nolightprobe nolodfade
			#pragma multi_compile _ VOXELPLAY_USE_ROTATION
			#define USE_TEXTURE
			#define NON_ARRAY_TEXTURE
            #define VP_CUTOUT
            #include "VPCommonURP.cginc"
			#include "VPCommonCore.cginc"
			#include "VPIndirectShadows.cginc"
			ENDHLSL
		}

	}

	SubShader {

		Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
		Pass {
			Tags { "LightMode" = "ForwardBase" }
			CGPROGRAM
			#pragma target 4.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight nodirlightmap
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile _ VOXELPLAY_USE_ROTATION
			#pragma multi_compile_instancing nolightprobe nolodfade
			#define SUBTLE_SELF_SHADOWS
			#define USE_TEXTURE
			#define NON_ARRAY_TEXTURE
            #define VP_CUTOUT
            #include "VPCommon.cginc"
			#include "VPIndirect.cginc"
			ENDCG
		}

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			CGPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_instancing nolightprobe nolodfade
			#pragma multi_compile _ VOXELPLAY_USE_ROTATION
			#define USE_TEXTURE
			#define NON_ARRAY_TEXTURE
            #define VP_CUTOUT
            #include "VPCommon.cginc"
			#include "VPIndirectShadows.cginc"
			ENDCG
		}
	}

	Fallback Off
}