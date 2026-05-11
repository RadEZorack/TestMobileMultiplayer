Shader "Hidden/VoxelPlay/VoxelPlayPostProcessingURP" {
Properties {
}

Subshader {	

    Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
    LOD 100
    ZWrite Off ZTest Always Blend Off Cull Off

    HLSLINCLUDE
    #pragma target 3.0
    #pragma prefer_hlslcc gles
    #pragma exclude_renderers d3d11_9x

    #include "../../VPCommonURP.cginc"

    #if defined(USES_URP)
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #endif
    ENDHLSL

  Pass { // 0
      Name "VP Raw Copy"
      HLSLPROGRAM
      #pragma vertex VertOS
      #pragma fragment FragCopy
      #include "VoxelPlayPostProcessingPass.hlsl"
      ENDHLSL
  }

  Pass { // 1
      Name "VP Post Process Pass"
      HLSLPROGRAM
      #pragma vertex VertOS
      #pragma fragment FragVP
      #include "VoxelPlayPostProcessingPass.hlsl"
      ENDHLSL
  }

}
FallBack Off
}
