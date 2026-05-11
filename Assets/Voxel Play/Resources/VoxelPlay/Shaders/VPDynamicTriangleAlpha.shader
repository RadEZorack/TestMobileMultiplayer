Shader "Voxel Play/Voxels/Dynamic/Triangle Alpha"
{
	Properties
	{
		[HideInInspector] _MainTex ("Main Texture Array", Any) = "white" {}
		[HideInInspector] _Color ("Color", Color) = (1,1,1,1)
		[HideInInspector] _VPMatProps("Material Properties", 2D) = "black" {}
		_OutlineColor ("Outline Color", Color) = (1,1,1,0.5)
		_OutlineThreshold("Outline Threshold", Float) = 0.48
		[HideInInspector] _VPParallaxStrength("__Elev", Float) = 0.2
		[HideInInspector] _VPParallaxMaxDistanceSqr("__MaxDistSqr", Float) = 625
		[HideInInspector] _VPParallaxIterations("__Iterations", Float) = 10
		[HideInInspector] _VPParallaxIterationsBinarySearch("__IterationsBinarySearch", Float) = 6
		[HideInInspector] _VoxelLight ("Voxel Light", Range(0,15)) = 15
		[HideInInspector] _VPGhostAlpha ("Ghost Alpha", Float) = 1.0
	}

    SubShader {

        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
		Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment fragTransp
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
            //#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_NORMAL
			#pragma multi_compile_local _ VOXELPLAY_USE_AA VOXELPLAY_USE_PARALLAX
			#pragma multi_compile_local _ VOXELPLAY_USE_OUTLINE
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
			#pragma multi_compile_local _ VOXELPLAY_USE_PBR
			#define USE_EMISSION
            #define USE_WORLD_SPACE_NORMAL
			#define USE_PACKED_LIGHT
            #define NO_SHADOWS
			// #if UNITY_VERSION >= 202200
			// 	#pragma multi_compile _ _FORWARD_PLUS
			// #endif

            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"
			#include "VPVoxelTriangleOpaquePass.cginc"
			
			float _VPGhostAlpha;
			
			fixed4 fragTransp (v2f i) : SV_Target {
				fixed4 color = VPVoxelTriangleFragment(i);
				color.a *= _VPGhostAlpha;
				return color;
			}
			
			ENDHLSL
		}

        


	}

	SubShader {

		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		Offset -1, -1
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Pass {
			Tags { "LightMode" = "ForwardBase" }
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment fragTransp
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight nodirlightmap
			//#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_NORMAL
			#pragma multi_compile_local _ VOXELPLAY_USE_AA VOXELPLAY_USE_PARALLAX
			#pragma multi_compile_local _ VOXELPLAY_USE_OUTLINE
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
			#pragma multi_compile_local _ VOXELPLAY_USE_PBR
			#define USE_EMISSION
            #define USE_WORLD_SPACE_NORMAL
			#define USE_PACKED_LIGHT
            #include "VPCommon.cginc"
			#include "VPVoxelTriangleOpaquePass.cginc"
			
			float _VPGhostAlpha;
			
			fixed4 fragTransp (v2f i) : SV_Target {
				fixed4 color = VPVoxelTriangleFragment(i);
				color.a *= _VPGhostAlpha;
				return color;
			}
			
			ENDCG
		}



	}
	Fallback Off
}