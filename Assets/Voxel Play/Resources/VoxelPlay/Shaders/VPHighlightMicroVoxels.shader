Shader "Voxel Play/Misc/Highlight MicroVoxels"
{
	Properties
	{
		_Color ("Color", Color) = (1,0,0,0.5)
		_Width ("Width", Float) = 0.05
		_FadeAmplitude ("Fade Amplitude", Float) = 1
		_MicroVoxelsSize ("MicroVoxels Size", Float) = 16
	}
	SubShader
	{
		Tags { "Queue"="Transparent-2" "RenderType"="Transparent" }

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			Offset -4, -4
			ZWrite Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "VPCommon.cginc"

			half4 _Color;
			half _Width;
			half _FadeAmplitude;
			half _MicroVoxelsSize;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv     : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv     : TEXCOORD0;
				half4  color  : COLOR;
				UNITY_VERTEX_OUTPUT_STEREO
			};


			v2f vert (appdata v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				VOXELPLAY_MODIFY_VERTEX_NO_WPOS(v.vertex);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				half fade =  (sin(_Time.w * 2.0) + 1.0) * 0.2 + 0.25;
				fade = 1.0 - fade * _FadeAmplitude;
				o.color  = half4(_Color.rgb, fade * _Color.a);
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
				i.uv *= _MicroVoxelsSize;
				half2 grd = abs(frac(i.uv + 0.5) - 0.5);
				grd /= fwidth(i.uv);
				half  lin = min(grd.x, grd.y);
				half  edge = 1.0 - min(lin.xxx * _Width, 1.0);
				i.color.a *= pow(edge, 8);
				return i.color;
			}
			ENDCG
		}
	}
}
