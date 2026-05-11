Shader "Voxel Play/Voxels/Triangle/Cutout Cross"
{
	Properties
	{
		[HideInInspector] _MainTex ("Main Texture Array", Any) = "white" {}
		[HideInInspector] _VPMatProps("Material Properties", 2D) = "black" {}
	}

    SubShader {

        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
		Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
			AlphaToMask On
			Cull Off
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_AA
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
			#define VP_CUTOUT
            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"
			#include "VPVoxelTriangleCutoutCrossPass.cginc"
			ENDHLSL
		}

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
//			Cull Off // Commented out for performance; two sided shadows can be expensive for mass grass
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
			#pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag
		    #pragma multi_compile_instancing
			#include "VPVoxelTriangleCutoutCrossShadowsURP.cginc"
			ENDHLSL
		}


		Pass {
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }
//			Cull Off // Commented out for performance
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
			#pragma target 3.5
			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment
		    #pragma multi_compile_instancing
			#include "VPVoxelTriangleCutoutCrossDepthOnlyURP.cginc"
			ENDHLSL
		}

		Pass {
			Name "DepthNormalsOnly"
			Tags { "LightMode" = "DepthNormalsOnly" }
//			Cull Off // Commented out for performance
			HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
			#pragma target 3.5
			#pragma vertex DepthNormalsVertex
			#pragma fragment DepthNormalsFragment
		    #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
			#define VP_CUTOUT
			#define USE_WORLD_SPACE_UV
			#include "VPVoxelTriangleCutoutCrossDepthNormalsURP.cginc"
			ENDHLSL
		}
	}

	SubShader {

		Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector"="True"}
		Pass {
			AlphaToMask On
			Tags { "LightMode" = "ForwardBase" }
			Cull Off
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight nodirlightmap
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_AA
			#define VP_CUTOUT
            #include "VPCommon.cginc"
			#include "VPVoxelTriangleCutoutCrossPass.cginc"
			ENDCG
		}

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
//			Cull Off // Commented out for performance; two sided shadows can be expensive for mass grass
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#pragma fragmentoption ARB_precision_hint_fastest
            #include "VPCommon.cginc"
			#include "VPVoxelTriangleCutoutCrossShadows.cginc"
			ENDCG
		}
	}

	Fallback Off
}