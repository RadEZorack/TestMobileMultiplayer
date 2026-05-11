Shader "Voxel Play/Voxels/Override Examples/Triangle/Opaque World Space UV 6 Sides"
{
	Properties
	{
        _MainTex ("Top Texture", 2D) = "white" {}
		[HideInInspector] _VPMatProps("Material Properties", 2D) = "black" {}
        _BottomTex ("Bottom Texture", 2D) = "white" {}
        _LeftTex ("Left Texture", 2D) = "white" {}
        _RightTex ("Right Texture", 2D) = "white" {}
        _ForwardTex ("Forward Texture", 2D) = "white" {}
        _BackTex ("Back Texture", 2D) = "white" {}
		_OutlineColor ("Outline Color", Color) = (1,1,1,0.5)
		_OutlineThreshold("Outline Threshold", Float) = 0.48
	}


	SubShader {

		Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
		Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_AA
			#pragma multi_compile_local _ VOXELPLAY_USE_OUTLINE
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
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
            #define NON_ARRAY_TEXTURE
            #define USE_WORLD_SPACE_UV
            #define USE_NORMAL
            #define USE_6_TEXTURES
            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"
			#include "VPVoxelTriangleOpaqueWorldSpaceUVMultiTex.cginc"
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
			#pragma multi_compile_local _ VOXELPLAY_USE_AA
			#pragma multi_compile_local _ VOXELPLAY_USE_OUTLINE
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
            #define NON_ARRAY_TEXTURE
            #define USE_WORLD_SPACE_UV
            #define USE_NORMAL
            #define USE_6_TEXTURES
            #include "VPCommon.cginc"
			#include "VPVoxelTriangleOpaqueWorldSpaceUVMultiTex.cginc"
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