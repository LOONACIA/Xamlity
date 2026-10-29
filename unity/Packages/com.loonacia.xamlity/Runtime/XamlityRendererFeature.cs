using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Xamlity.Unity
{
    public sealed class XamlityRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader solidShader;
        private Material solidMaterial;
        private XamlityRenderPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(solidMaterial);
            solidMaterial = solidShader == null ? null : CoreUtils.CreateEngineMaterial(solidShader);
            pass = new XamlityRenderPass(solidMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (solidMaterial != null)
                renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(solidMaterial);
        }

        private sealed class XamlityRenderPass : ScriptableRenderPass
        {
            private static readonly int ProjectionId = Shader.PropertyToID("_XamlityProjection");
            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

            private sealed class PassData
            {
                internal IReadOnlyList<XamlityRoot> Roots;
                internal Material Material;
                internal MaterialPropertyBlock Properties;
                internal Matrix4x4 Projection;
                internal Rect Viewport;
            }

            internal XamlityRenderPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRendering;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType != CameraType.Game || !cameraData.resolveFinalTarget ||
                    XamlityRoot.ActiveRoots.Count == 0)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                using var builder = renderGraph.AddRasterRenderPass<PassData>("Xamlity", out var data);
                data.Roots = XamlityRoot.ActiveRoots;
                data.Material = material;
                data.Properties = properties;
                data.Viewport = cameraData.camera.pixelRect;
                var projection = Matrix4x4.Ortho(0, data.Viewport.width, 0, data.Viewport.height, -1, 1);
                bool renderIntoTexture = cameraData.targetTexture != null || !resourceData.isActiveTargetBackBuffer;
                data.Projection = GL.GetGPUProjectionMatrix(projection, renderIntoTexture);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                {
                    context.cmd.SetViewport(passData.Viewport);
                    passData.Properties.SetMatrix(ProjectionId, passData.Projection);
                    for (int i = 0; i < passData.Roots.Count; i++)
                    {
                        Mesh mesh = passData.Roots[i].Mesh;
                        if (mesh == null)
                            continue;
                        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                            context.cmd.DrawMesh(mesh, Matrix4x4.identity, passData.Material,
                                submesh, 0, passData.Properties);
                    }
                });
            }
        }
    }
}
