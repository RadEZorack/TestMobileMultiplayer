#ifndef VP_POST_PROCESSING
#define VP_POST_PROCESSING

#if defined(USES_URP)

    TEXTURE2D_X(_MainTex);
    float4 _MainTex_TexelSize;

    #define DEPTH_THRESHOLD 100

    struct AttributesSimple {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

  	struct VaryingsSimple {
    	float4 positionCS : SV_POSITION;
    	float2 uv  : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
	};

    float GetEyeDepth(float2 uv) {
        return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
    }

	VaryingsSimple VertOS(AttributesSimple input) {
	    VaryingsSimple output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = input.positionOS;
        output.positionCS.y *= _ProjectionParams.x;
        output.uv = input.uv.xy;
    	return output;
	}

	half4 FragCopy (VaryingsSimple i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);
        half4 col = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uv, 0);
        return col;
	}


	half4 FragVP (VaryingsSimple i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float3 uvInc = float3(_MainTex_TexelSize.x, _MainTex_TexelSize.y, 0);
        float2 uv = i.uv;
        float  depth = GetEyeDepth(uv);
        float  depthW = GetEyeDepth(uv - uvInc.xz);
        float  depthE = GetEyeDepth(uv + uvInc.xz);

        float deltaW = abs(depth - depthW);
        float deltaE = abs(depth - depthE);

        if (deltaW > DEPTH_THRESHOLD && deltaE > DEPTH_THRESHOLD) {
            uv -= uvInc.xz;
        }
        
        half4 col = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(uv), 0);
        return col;
	}

#else

    void VertOS() {}
    void FragCopy() {}
    void FragVP() {}


#endif // USES_URP


#endif // VP_POST_PROCESSING