Shader "Voxel Play/Models/GPU Instanced Indirect/Texture/Alpha"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" {}
		[HDR] _Color ("Color", Color) = (1,1,1,1)
		[HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _CustomDaylightShadowAtten ("Daylight Shadow Atten", Range(0,1)) = 0.15
	}

	SubShader {

		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass {
			Tags { "LightMode" = "UniversalForwardOnly" }
		    HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile _ VOXELPLAY_USE_ROTATION
			#pragma multi_compile_instancing nolightprobe nolodfade
			#define SUBTLE_SELF_SHADOWS
			#define USE_TEXTURE
			#define NON_ARRAY_TEXTURE
            #include "VPCommonURP.cginc"
            #include "VPCommonCore.cginc"
			#include "VPIndirect.cginc"
			ENDHLSL
		}
	}

	SubShader {

		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		Pass {
			Tags { "LightMode" = "ForwardBase" }
			Blend SrcAlpha OneMinusSrcAlpha
			CGPROGRAM
			#pragma target 4.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight nodirlightmap
			#pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG
			#pragma multi_compile _ VOXELPLAY_USE_ROTATION
			#pragma multi_compile_instancing nolightprobe nolodfade
			#define SUBTLE_SELF_SHADOWS
			#define USE_TEXTURE
			#define NON_ARRAY_TEXTURE
            #include "VPCommon.cginc"
			#include "VPIndirect.cginc"
			ENDCG
		}

	}
	Fallback Off
}