Shader "Voxel Play/Voxels/Dynamic/Triangle Cutout"
{
	Properties
	{
		[HideInInspector] _MainTex ("Main Texture Array", Any) = "white" {}
		[HideInInspector] _Color ("Color", Color) = (1,1,1,1)
		[HideInInspector] _VPMatProps("Material Properties", 2D) = "black" {}
		[HideInInspector] _VPParallaxStrength("__Elev", Float) = 0.2
		[HideInInspector] _VPParallaxMaxDistanceSqr("__MaxDistSqr", Float) = 625
		[HideInInspector] _VPParallaxIterations("__Iterations", Float) = 10
		[HideInInspector] _VPParallaxIterationsBinarySearch("__IterationsBinarySearch", Float) = 6
		_VoxelLight ("Voxel Light", Range(0,15)) = 15
		_CutOff("Cut Off", Float) = 0.5
	}

	SubShader {

        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
		Offset 1, 1 // used to avoid z-fighting when regular voxel and dynamic coexist for a fraction of second
		Pass {
			AlphaToMask On
			Tags { "LightMode" = "UniversalForwardOnly" }
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			//#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_NORMAL
			#pragma multi_compile_local _ VOXELPLAY_USE_AA VOXELPLAY_USE_PARALLAX
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
			#pragma multi_compile_local _ VOXELPLAY_USE_PBR
			#define VP_CUTOUT
            #define USE_WORLD_SPACE_NORMAL
			#define USE_PACKED_LIGHT
			#if UNITY_VERSION < 202100
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#elif UNITY_VERSION < 202200
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#else
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#endif
			// #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
			// #if UNITY_VERSION >= 202200
			// 	#pragma multi_compile _ _FORWARD_PLUS
			// #endif

            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"
			#include "VPVoxelTriangleOpaquePass.cginc"
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
			#include "VPVoxelTriangleShadowsURP.cginc"
			ENDHLSL
		}

		Pass {
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
			#pragma target 3.5
			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment
		    #pragma multi_compile_instancing
			#include "VPVoxelTriangleDepthOnlyURP.cginc"
			ENDHLSL
		}

		Pass {
			Name "DepthNormalsOnly"
			Tags { "LightMode" = "DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
			#pragma target 3.5
			#pragma vertex DepthNormalsVertex
			#pragma fragment DepthNormalsFragment
		    #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
			#include "VPVoxelTriangleDepthNormalsURP.cginc"
			ENDHLSL
		}
	}

	SubShader {

		Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
		Offset 1, 1 // used to avoid z-fighting when regular voxel and dynamic coexist for a fraction of second
		Pass {
			AlphaToMask On
			Tags { "LightMode" = "ForwardBase" }
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight nodirlightmap
			//#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_NORMAL
			#pragma multi_compile_local _ VOXELPLAY_USE_AA VOXELPLAY_USE_PARALLAX
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
			#pragma multi_compile_local _ VOXELPLAY_USE_PBR
			#define VP_CUTOUT
            #define USE_WORLD_SPACE_NORMAL
			#define USE_PACKED_LIGHT
            #include "VPCommon.cginc"
            #include "VPCommonCore.cginc"
			#include "VPVoxelTriangleOpaquePass.cginc"
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
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "VPVoxelTriangleShadows.cginc"
			ENDCG
		}
	}

	Fallback Off
}