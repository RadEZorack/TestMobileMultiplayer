Shader "Voxel Play/FX/DamagedVoxel"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,0.5)
		_VoxelLight ("Voxel Ambient Light", Float) = 1
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Offset -1, -1

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ VERTEXLIGHT_ON
			#define NON_ARRAY_TEXTURE
			#include "UnityLightingCommon.cginc"
			#include "VPCommon.cginc"

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv     : TEXCOORD0;
				fixed3 diff : COLOR0;
                #if defined(VERTEXLIGHT_ON)
	                fixed3 vertexLightColor: TEXCOORD1;
                #endif
				UNITY_VERTEX_OUTPUT_STEREO
			};


			v2f vert (appdata_base v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				VOXELPLAY_MODIFY_VERTEX(v.vertex, worldPos)

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord.xy;
 				// Daylight
                half3 worldNormal = UnityObjectToWorldNormal(v.normal);
		fixed daylight    = saturate(1.0 + _WorldSpaceLightPos0.y * 2.0);
                half nl = (0.25 + 0.75 * saturate(dot(worldNormal, _WorldSpaceLightPos0.xyz))) * daylight;
                // factor in the light color
                fixed sunLight, torchLight;
                UnpackVoxelLight(_VoxelLight, sunLight, torchLight);
                fixed3 ambientLight = GetAmbientLighting(worldNormal);
                o.diff = max(nl, ambientLight) * sunLight * _LightColor0.rgb + torchLight;
				o.diff *= _Color.rgb;
                o.diff += ShadePointLights(worldPos, worldNormal);
				#if defined(VERTEXLIGHT_ON)
					o.vertexLightColor = Shade4PointLights(unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,unity_LightColor[0].rgb, unity_LightColor[1].rgb,unity_LightColor[2].rgb, unity_LightColor[3].rgb,unity_4LightAtten0, worldPos, worldNormal);
                #endif

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
	            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				fixed4 col = tex2D(_MainTex, i.uv) * 0.5;
				#if defined(VERTEXLIGHT_ON)
					col.rgb *= i.diff + i.vertexLightColor;
                #else
					col.rgb *= i.diff;
                #endif
				return col;
			}
			ENDCG
		}
	}
}
