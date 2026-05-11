Shader "Voxel Play/Voxels/Triangle/Water No Shadows"
{
	Properties
	{
		[HideInInspector] _MainTex ("Main Texture Array", Any) = "white" {}
		[HideInInspector] _VPMatProps("Material Properties", 2D) = "black" {}		
        [HideInInspector] _WaveAmplitude ("Wave Amplitude", Float) = 1.0
		[HideInInspector] _WaveSpeed ("Wave Speed", Float) = 1.0
		[HideInInspector] _SpecularPower ("Specular Power", Float) = 64
		[HideInInspector] _SpecularIntensity ("Specular Intensity", Float) = 2.0
		[HideInInspector] _FoamColor ("Foam Color", Color) = (1,1,1)
	}

	SubShader {

		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
		Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On // avoids overdraw issues; can be removed if neccessary
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_NORMAL
			#pragma multi_compile_local _ VOXELPLAY_USE_AA VOXELPLAY_USE_PARALLAX
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
			#define USE_EMISSION
			#define IS_WATER
			#define NO_SHADOWS
            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"		
			#include "VPVoxelTriangleWaterPass.cginc"
			ENDHLSL
		}
	}


	SubShader {
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		Pass {
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On // avoids overdraw issues; can be removed if necessary
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile_local _ VOXELPLAY_USE_NORMAL
			#pragma multi_compile_local _ VOXELPLAY_USE_AA VOXELPLAY_USE_PARALLAX
			#pragma multi_compile_local _ VOXELPLAY_PIXEL_LIGHTS
			#define USE_EMISSION
			#define IS_WATER
			#include "VPCommon.cginc"
			#include "VPVoxelTriangleWaterPass.cginc"
			ENDCG
		}
	}
	Fallback Off
}