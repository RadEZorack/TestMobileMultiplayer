Shader "Voxel Play/Voxels/Triangle/Fluid"
{
	Properties
	{
		[HideInInspector] _MainTex ("Main Texture Array", Any) = "white" {}
		[HideInInspector] _VPMatProps("Material Properties", 2D) = "black" {}
        _NoiseTex("Noise Tex", 2D) = "black" {}
		[HideInInspector] _VPParallaxStrength("__Elev", Float) = 0.2
		[HideInInspector] _VPParallaxMaxDistanceSqr("__MaxDistSqr", Float) = 625
		[HideInInspector] _VPParallaxIterations("__Iterations", Float) = 10
		[HideInInspector] _VPParallaxIterationsBinarySearch("__IterationsBinarySearch", Float) = 6
        [HideInInspector] _WaveAmplitude ("Wave Amplitude", Float) = 1.0
		[HideInInspector] _WaveSpeed ("Wave Speed", Float) = 1.0

	}
	SubShader {

        Tags { "RenderType" = "Transparent" "Queue" = "AlphaTest+1" "RenderPipeline" = "UniversalPipeline" }
		Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On // avoids overdraw issues; can be removed if neccessary
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
			#define USE_EMISSION
			#define IS_WATER
			#define NO_SHADOWS
            #include "VPCommonURP.cginc"
			#include "VPCommonCore.cginc"
			#include "VPVoxelTriangleFluidPass.cginc"
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

		Tags { "Queue" = "Transparent" "RenderType" = "AlphaTest+1" }
		Pass {
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On // avoids overdraw issues; can be removed if neccessary
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
			#define USE_EMISSION
			#define IS_WATER
			#include "VPCommon.cginc"
			#include "VPVoxelTriangleFluidPass.cginc"
			ENDCG
		}


		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			Cull Off // avoids shadow artifacts
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