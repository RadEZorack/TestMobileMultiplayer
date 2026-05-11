#if URP_VERSION_INSTALLED

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;

namespace VoxelPlay {

    public class VoxelPlayPostProcessingRenderFeature : ScriptableRendererFeature {

        class CustomRenderPass : ScriptableRenderPass {

            static class ShaderParams {
                public static int MainTex = Shader.PropertyToID("_MainTex");
                public static int InputTex = Shader.PropertyToID("_InputTex");

                public const string SHADER_NAME = "Hidden/VoxelPlay/VoxelPlayPostProcessingURP";
            }

            static Material mat;
            static RenderTextureDescriptor sourceDesc;

            public void Setup() {
                this.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                if (mat == null) {
                    mat = CoreUtils.CreateEngineMaterial(Shader.Find(ShaderParams.SHADER_NAME));
                }
            }

#if !UNITY_6000_4_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                sourceDesc = cameraTextureDescriptor;
                sourceDesc.depthBufferBits = 0;
            }
#endif

#if !UNITY_6000_4_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
                var cmd = CommandBufferPool.Get("VP");
#if UNITY_2022_2_OR_NEWER
                RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;
#else
                RenderTargetIdentifier source = renderingData.cameraData.renderer.cameraColorTarget;
#endif
                cmd.GetTemporaryRT(ShaderParams.InputTex, sourceDesc);
                FullScreenBlit(cmd, source, ShaderParams.InputTex, mat, 0);
                FullScreenBlit(cmd, ShaderParams.InputTex, source, mat, 1);
                cmd.ReleaseTemporaryRT(ShaderParams.InputTex);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#endif

#if UNITY_2023_3_OR_NEWER

            class PassData {
                public TextureHandle colorTexture;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                using (var builder = renderGraph.AddUnsafePass<PassData>("Voxel Play RG Pass", out var passData)) {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    passData.colorTexture = resourceData.activeColorTexture;

                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    sourceDesc = cameraData.cameraTargetDescriptor;
                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);

                    builder.SetRenderFunc((PassData passData, UnsafeGraphContext context) => {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        cmd.GetTemporaryRT(ShaderParams.InputTex, sourceDesc);
                        FullScreenBlit(cmd, passData.colorTexture, ShaderParams.InputTex, mat, 0);
                        FullScreenBlit(cmd, ShaderParams.InputTex, passData.colorTexture, mat, 1);
                        cmd.ReleaseTemporaryRT(ShaderParams.InputTex);
                    });
                }
            }
#endif

            public void CleanUp() {
                CoreUtils.Destroy(mat);
            }


            static Mesh _fullScreenMesh;

            static Mesh fullscreenMesh {
                get {
                    if (_fullScreenMesh != null) {
                        return _fullScreenMesh;
                    }
                    float num = 1f;
                    float num2 = 0f;
                    Mesh val = new Mesh();
                    _fullScreenMesh = val;
                    _fullScreenMesh.SetVertices(new List<Vector3> {
            new Vector3 (-1f, -1f, 0f),
            new Vector3 (-1f, 1f, 0f),
            new Vector3 (1f, -1f, 0f),
            new Vector3 (1f, 1f, 0f)
        });
                    _fullScreenMesh.SetUVs(0, new List<Vector2> {
            new Vector2 (0f, num2),
            new Vector2 (0f, num),
            new Vector2 (1f, num2),
            new Vector2 (1f, num)
        });
                    _fullScreenMesh.SetIndices(new int[6] { 0, 1, 2, 2, 1, 3 }, (MeshTopology)0, 0, false);
                    _fullScreenMesh.UploadMeshData(true);
                    return _fullScreenMesh;
                }
            }

            static void FullScreenBlit(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int passIndex) {
                destination = new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1);
                cmd.SetRenderTarget(destination);
                cmd.SetGlobalTexture(ShaderParams.MainTex, source);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, material, 0, passIndex);
            }
        }

        CustomRenderPass customPass;

        public override void Create() {
            customPass = new CustomRenderPass();
            customPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        private void OnDestroy() {
            if (customPass != null) customPass.CleanUp();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            customPass.Setup();
            renderer.EnqueuePass(customPass);
        }
    }
}

#endif

