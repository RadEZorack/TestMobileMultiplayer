Shader "Hidden/VoxelPlay/VoxelPlayPostProcessingBuiltin"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "VP Post Process Pass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define DEPTH_THRESHOLD 100
            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 depthUV : TEXCOORD1;
		        UNITY_VERTEX_INPUT_INSTANCE_ID
		        UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            float4 _MainTex_TexelSize;
            float4 _MainTex_ST;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);


            v2f vert (appdata v)
            {
                v2f o;
		        UNITY_SETUP_INSTANCE_ID(v);
		        UNITY_TRANSFER_INSTANCE_ID(v, o);
		        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = UnityObjectToClipPos(v.positionOS);
                o.uv = UnityStereoScreenSpaceUVAdjust(v.uv, _MainTex_ST);
                o.depthUV = o.uv;
                #if UNITY_UV_STARTS_AT_TOP
    	            if (_MainTex_TexelSize.y < 0) {
	                    // Depth texture is inverted WRT the main texture
            	        o.depthUV.y = 1.0 - o.depthUV.y;
                    }
                #endif
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
		        UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 uvInc = float3(_MainTex_TexelSize.x, _MainTex_TexelSize.y, 0);
                float  depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.depthUV ));
                float  depthW = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.depthUV - uvInc.xz));
		        float  depthE = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.depthUV + uvInc.xz));

                float deltaW = abs(depth - depthW);
                float deltaE = abs(depth - depthE);
                float2 uv = i.uv;
                if (deltaW > DEPTH_THRESHOLD && deltaE > DEPTH_THRESHOLD) {
                    uv -= uvInc.xz;
                }

                half4 col = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv);
                return col;
            }
            ENDCG
        }
    }
}
