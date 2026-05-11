#include "VPCommonCore.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
                #if defined(USE_EMISSION)
                    float2 uv_EmissionMap: TEXCOORD1;
                #endif
				UNITY_VERTEX_OUTPUT_STEREO
			};

            sampler2D _EmissionMap;
            float4 _EmissionMap_ST;
            fixed4 _EmissionColor;
            UNITY_INSTANCING_BUFFER_START(TorchProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _TorchMainTexOffset)
            UNITY_INSTANCING_BUFFER_END(TorchProps)

			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float torchOffset = 0;
                #ifdef UNITY_INSTANCING_ENABLED
                    torchOffset = UNITY_ACCESS_INSTANCED_PROP(TorchProps, _TorchMainTexOffset);
                #endif
				VOXELPLAY_MODIFY_VERTEX_NO_WPOS(v.vertex)
				o.vertex = UnityObjectToClipPos(v.vertex);
                float2 mainUv = TRANSFORM_TEX(v.uv, _MainTex);
                mainUv.y += torchOffset;
				o.uv = mainUv;

                #if defined(USE_EMISSION)
                    float2 emissionUv = TRANSFORM_TEX(v.uv, _EmissionMap);
                    emissionUv.y += torchOffset;
                    o.uv_EmissionMap = emissionUv;
                #endif
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                #if defined(USE_EMISSION)
                    col += tex2D(_EmissionMap, i.uv_EmissionMap) * _EmissionColor;
                #endif
				return col;
			}
