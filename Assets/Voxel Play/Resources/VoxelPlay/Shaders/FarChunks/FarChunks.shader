Shader "Voxel Play/FarChunks"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TerrainFarChunksTex ("Terrain Info", 2D) = "black" {}
        _CloudsTex("Clouds Tex", 2D) = "black" {}
        _WaterTex("Water Tex", 2D) = "white" {}
        [HideInInspector] _SnapshotData ("SnapshotData", Vector) = (0,0,0)
        _WaterColor ("WaterColor", Color) = (0,0,0)
        _WaterLevel ("WaterLevel", Float) = 0
        _ShoreColor ("Shore Color", Color) = (1,1,1)
        _TerrainMaxAltitude ("Terrain Max Altitude", Float) = 100
        _ShadowIntensity ("Shadow Intensity", Float) = 0.2
        _StarBlockSize ("Star Block Size", Range(100,300)) = 200
        _StarAmount("Star Amount", Range(0.9,1)) = 0.997
        _SunFlare("Sun Flare", Range(0, 1.0)) = 0.4
        _SunLightColor("Sun Light Color", Color) = (1,1,1)
        _MoonFlare("Moon Flare", Range(0, 1.0)) = 0.1     
        _SpecularPower("Specular Power", Float) = 64
        _SpecularIntensity("Specular Intensity", Float) = 2
       
    }
    SubShader {

        Tags { "RenderType" = "Opaque" "Queue" = "Transparent-1" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZClip False
        
		Pass {
            Name "Far Chunks"
			Tags { "LightMode" = "UniversalForwardOnly" }

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex   vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_local _ _SHADOWS
            #pragma multi_compile_local _ _WATER_REFLECTIONS
            #pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG

            #include "../VPCommonURP.cginc"
            #include "../VPCommonCore.cginc"
            #include "../VPCommonSky.cginc"
            #include "../VPCommonVertexModifier.cginc"
            #include "FarChunksPass.cginc"            


			ENDHLSL
		}
    }

    SubShader {
    
        Tags { "Queue"="Transparent-1" "RenderType"="Opaque" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZClip False
 
        Pass
        {
            Name "Far Chunks"
			Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _SHADOWS
            #pragma multi_compile_local _ _WATER_REFLECTIONS
            #pragma multi_compile _ VOXELPLAY_GLOBAL_USE_FOG

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"     
            #include "../VPCommon.cginc"                   
            #include "../VPCommonSky.cginc"
            #include "FarChunksPass.cginc"

            ENDCG
        }
    }

 
		Fallback Off
}    

