Shader "Voxel Play/FX/Underwater Realistic"
{
	Properties
	{
		_Color ("Water Color", Color) = (0.4,0.4,1,0.5)
		_WaterLevel ("Water Level", Float) = 60
		_WaterCausticsLevel ("Water Caustics Level", Float) = 60
        _WaveAmplitude ("Wave Amplitude", Float) = 1.0
		_WaterCaustics ("Water Caustics", 2D) = "black" {}
		_Noise3D ("Noise 3D Texture", 3D) = "" {}
	}
	SubShader {

		Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

		Pass {
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma target 3.0
			#define UNDERWATER_HQ
			#include "VPCommonURP.cginc"
			#include "VPUnderwaterPass.cginc"
			ENDHLSL
		}
	}

	SubShader {

		Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" }
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma target 3.0
			#define UNDERWATER_HQ
			#include "VPCommon.cginc"
			#include "VPUnderwaterPass.cginc"
			ENDCG
		}
	}
	Fallback Off
}
