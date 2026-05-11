Shader "Voxel Play/FX/Underwater"
{
	Properties
	{
		_Color ("Water Color", Color) = (0.4,0.4,1,0.5)
		_WaterLevel ("Water Level", Float) = 60
        _WaveAmplitude ("Wave Amplitude", Float) = 1.0
	}
	SubShader {

		Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" }

		Pass {
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma target 3.0
			#include "UnityCG.cginc"
			#include "VPUnderwaterPass.cginc"
			ENDHLSL
		}
	}

	Fallback Off
}
