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
        [SerializeField] private Shader textShader;
        [SerializeField] private Shader imageShader;
        private Material solidMaterial;
        private Material textMaterial;
        private Material imageMaterial;
        private XamlityRenderPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(solidMaterial);
            CoreUtils.Destroy(textMaterial);
            CoreUtils.Destroy(imageMaterial);
            solidMaterial = solidShader == null ? null : CoreUtils.CreateEngineMaterial(solidShader);
            textMaterial = textShader == null ? null : CoreUtils.CreateEngineMaterial(textShader);
            imageMaterial = imageShader == null ? null : CoreUtils.CreateEngineMaterial(imageShader);
            pass = new XamlityRenderPass(solidMaterial, textMaterial, imageMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (solidMaterial != null)
                renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(solidMaterial);
            CoreUtils.Destroy(textMaterial);
            CoreUtils.Destroy(imageMaterial);
        }

        private sealed class XamlityRenderPass : ScriptableRenderPass
        {
            private static readonly int ProjectionId = Shader.PropertyToID("_XamlityProjection");
            private static readonly int AtlasId = Shader.PropertyToID("_MainTex");
            private readonly Material solidMaterial;
            private readonly Material textMaterial;
            private readonly Material imageMaterial;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

            private sealed class PassData
            {
                internal IReadOnlyList<XamlityRoot> Roots;
                internal Material SolidMaterial;
                internal Material TextMaterial;
                internal Material ImageMaterial;
                internal MaterialPropertyBlock Properties;
                internal Matrix4x4 Projection;
                internal Rect Viewport;
            }

            internal XamlityRenderPass(Material solidMaterial, Material textMaterial, Material imageMaterial)
            {
                this.solidMaterial = solidMaterial;
                this.textMaterial = textMaterial;
                this.imageMaterial = imageMaterial;
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
                data.SolidMaterial = solidMaterial;
                data.TextMaterial = textMaterial;
                data.ImageMaterial = imageMaterial;
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
                        var root = passData.Roots[i];
                        Mesh mesh = root.Mesh;
                        if (mesh == null)
                            continue;
                        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                        {
                            var batch = root.Batches[submesh];
                            var texture = batch.Texture;
                            var material = batch.Kind == MeshDrawingContext.BatchKind.Solid ? passData.SolidMaterial
                                : batch.Kind == MeshDrawingContext.BatchKind.Text ? passData.TextMaterial : passData.ImageMaterial;
                            if (material == null)
                                continue;
                            if (texture != null)
                                passData.Properties.SetTexture(AtlasId, texture);
                            context.cmd.DrawMesh(mesh, Matrix4x4.identity, material,
                                submesh, 0, passData.Properties);
                        }
                    }
                });
            }
        }
    }
}
