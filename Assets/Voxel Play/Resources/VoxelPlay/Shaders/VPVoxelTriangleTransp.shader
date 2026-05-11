Shader "Voxel Play/Voxels/Triangle/Transp"
{
	Properties
	{
		[HideInInspector] _MainTex ("Main Texture Array", Any) = "white" {}
		[HideInInspector] _VPMatProps("Material Properties", 2D) = "black" {}
		_VPSeeThroughScreenMask ("Screen Mask", 2D) = "white" {}
		[HideInInspector] _VPParallaxStrength("__Elev", Float) = 0.2
		[HideInInspector] _VPParallaxMaxDistanceSqr("__MaxDistSqr", Float) = 625
		[HideInInspector] _VPParallaxIterations("__Iterations", Float) = 10
		[HideInInspector] _VPParallaxIterationsBinarySearch("__IterationsBinarySearch", Float) = 6
	}
	SubShader {

        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
		Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Offset -1, -1
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_NORMAL
			#pragma multi_compile_local _ VOXELPLAY_USE_AA VOXELPLAY_USE_PARALLAX
			#pragma multi_compile_local _ VOXELPLAY_USE_OUTLINE
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
			#pragma multi_compile_local _ VOXELPLAY_USE_PBR
			#pragma multi_compile_local _ VOXELPLAY_TRANSP_BLING
			#define NO_SHADOWS
            #include "VPCommonURP.cginc"
			#include "VPCommonCore.cginc"
			#include "VPVoxelTriangleTranspPass.cginc"
			ENDHLSL
		}
	}

	SubShader {

		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		Pass {
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Offset -1, -1
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_NORMAL
			#pragma multi_compile_local _ VOXELPLAY_USE_AA VOXELPLAY_USE_PARALLAX
			#pragma multi_compile_local _ VOXELPLAY_USE_OUTLINE
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
			#pragma multi_compile_local _ VOXELPLAY_USE_PBR
			#pragma multi_compile_local _ VOXELPLAY_TRANSP_BLING
			#include "VPCommon.cginc"
			#include "VPVoxelTriangleTranspPass.cginc"
			ENDCG
		}
	}
	Fallback Off
}