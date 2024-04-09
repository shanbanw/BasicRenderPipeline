using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

using CoreRendererList = UnityEngine.Rendering.RendererList;
using CoreRendererListDesc = UnityEngine.Rendering.RendererUtils.RendererListDesc;

public class RendererSceneManager
{
    private RendererSceneManager()
    {

    }

    private static RendererSceneManager m_Instance;
    public static RendererSceneManager instance
    {
        get 
        { 
            m_Instance ??= new RendererSceneManager();
            return m_Instance; 
        }
    }

    private static readonly ShaderTagId s_ForwardOpaqueBasePassId = new ShaderTagId("ForwardOpaqueBase");
    private static readonly ShaderTagId s_ForwardOpaqueAddPassId = new ShaderTagId("ForwardOpaqueAdd");
    private static readonly ShaderTagId s_DepthPrepassId = new ShaderTagId("DepthPrepass");
    private static readonly ShaderTagId s_GBufferPassId = new ShaderTagId("GBuffer");
    private static readonly ShaderTagId s_EmissivePassId = new ShaderTagId("Emissive");

    private CullingResults m_CullingResults;

    public void Init(ref CullingResults cullingResults)
    {
        m_CullingResults = cullingResults;
    }

    class DepthPrepassData
    {
        public RendererListHandle renderListHandle;
        public TextureHandle cameraDepthBuffer;
    }
    public void DepthPrepass(RenderGraph renderGraph, BackCamera backCamera)
    {
        using (var builder = renderGraph.AddRenderPass<DepthPrepassData>("Depth Prepass", out var passData))
        {
            RendererListDesc renderListDesc = new RendererListDesc(s_DepthPrepassId, m_CullingResults, backCamera.camera)
            {
                sortingCriteria = SortingCriteria.CommonOpaque,
                renderQueueRange = RenderQueueRange.opaque,
            };
            passData.renderListHandle = builder.UseRendererList(renderGraph.CreateRendererList(renderListDesc));
            RenderTargetIdentifier target = new RenderTargetIdentifier(BuiltinRenderTextureType.Depth);
            TextureHandle depthTarget = renderGraph.ImportBackbuffer(target);
            passData.cameraDepthBuffer = builder.UseDepthBuffer(depthTarget, DepthAccess.Write);
            builder.SetRenderFunc<DepthPrepassData>((DepthPrepassData data, RenderGraphContext renderContext) =>
            {
                //renderContext.cmd.SetRenderTarget(data.cameraDepthBuffer);
                renderContext.cmd.ClearRenderTarget(backCamera.camera.clearFlags <= CameraClearFlags.Depth, backCamera.camera.clearFlags <= CameraClearFlags.Color, backCamera.camera.clearFlags == CameraClearFlags.Color ? backCamera.camera.backgroundColor.linear : Color.clear);
                //renderContext.cmd.ClearRenderTarget(true, true, new(0.6f, 0.62f, 1.0f, 0.0f));
                CoreUtils.DrawRendererList(renderContext.renderContext, renderContext.cmd, data.renderListHandle);
                renderContext.renderContext.ExecuteCommandBuffer(renderContext.cmd);
                renderContext.cmd.Clear();
            });
        }
    }

    class OpaqueForwardBasePassData
    {
        public RendererListHandle renderListHandle;
        public TextureHandle backBuffer;
    }
    public void RenderOpaqueForwardBase(RenderGraph renderGraph, BackCamera backCamera)
    {
        using (var builder = renderGraph.AddRenderPass<OpaqueForwardBasePassData>("Render Forward Opaque Base Pass", out var passData))
        {
            RendererListDesc renderListDesc = new RendererListDesc(s_ForwardOpaqueBasePassId, m_CullingResults, backCamera.camera)
            {
                sortingCriteria = SortingCriteria.CommonOpaque,
                renderQueueRange = RenderQueueRange.opaque,
            };
            passData.renderListHandle = builder.UseRendererList(renderGraph.CreateRendererList(renderListDesc));
            RenderTargetIdentifier target = backCamera.camera.targetTexture ?? new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            passData.backBuffer = builder.WriteTexture(renderGraph.ImportBackbuffer(target));

            if (LightManager.instance.directionalCastShadow)
                builder.ReadTexture(LightManager.instance.cascadeShadowmap);

            builder.SetRenderFunc<OpaqueForwardBasePassData>((OpaqueForwardBasePassData data, RenderGraphContext renderContext) =>
            {
                renderContext.cmd.SetRenderTarget(data.backBuffer);
                LightManager.instance.LightSetup(renderContext.renderContext, renderContext.cmd, 0);
                //renderContext.cmd.ClearRenderTarget(false, backCamera.camera.clearFlags <= CameraClearFlags.Color, backCamera.camera.clearFlags == CameraClearFlags.Color ? backCamera.camera.backgroundColor.linear : Color.clear);
                CoreUtils.DrawRendererList(renderContext.renderContext, renderContext.cmd, data.renderListHandle);
                renderContext.renderContext.ExecuteCommandBuffer(renderContext.cmd);
                renderContext.cmd.Clear();
            });
        }
    }
    
    class OpaqueForwardAddPassData
    {
        public RendererListHandle renderListHandle;
        public TextureHandle backBuffer;
        public int lightIndex;
    }

    public void RenderOpaqueForwardAdd(RenderGraph renderGraph, BackCamera backCamera, int index)
    {
        using (var builder = renderGraph.AddRenderPass<OpaqueForwardAddPassData>("Render Forward Opaque Add Pass", out var passData))
        {
            RendererListDesc renderListDesc = new RendererListDesc(s_ForwardOpaqueAddPassId, m_CullingResults, backCamera.camera)
            {
                sortingCriteria = SortingCriteria.CommonOpaque,
                renderQueueRange = RenderQueueRange.opaque,
            };
            passData.renderListHandle = builder.UseRendererList(renderGraph.CreateRendererList(renderListDesc));
            RenderTargetIdentifier target = backCamera.camera.targetTexture ?? new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            passData.backBuffer = builder.WriteTexture(renderGraph.ImportBackbuffer(target));
            passData.lightIndex = index;
            if (LightManager.instance.IsLightCastShadow(index))
            {
                if (LightManager.instance.GetLightType(index) == LightType.Spot)
                    builder.ReadTexture(LightManager.instance.spotShadowmap);
                else if (LightManager.instance.GetLightType(index) == LightType.Point)
                    builder.ReadTexture(LightManager.instance.pointShadowmap);
            }

            builder.SetRenderFunc<OpaqueForwardAddPassData>((OpaqueForwardAddPassData data, RenderGraphContext renderContext) =>
            {
                renderContext.cmd.SetRenderTarget(data.backBuffer);
                LightManager.instance.LightSetup(renderContext.renderContext, renderContext.cmd, data.lightIndex);
                CoreUtils.DrawRendererList(renderContext.renderContext, renderContext.cmd, data.renderListHandle);
                renderContext.renderContext.ExecuteCommandBuffer(renderContext.cmd);
                renderContext.cmd.Clear();
            });
        }
    }

    class RenderGBufferPassData
    {
        public RendererListHandle renderListHandle;
        public BackCamera backCamera;
    }
    
    public void RenderGBufferPass(RenderGraph renderGraph, BackCamera backCamera)
    {
        using (var builder = renderGraph.AddRenderPass<RenderGBufferPassData>("GBuffer Generated Pass", out var passData))
        {
            CoreRendererListDesc renderListDesc = new(s_GBufferPassId, m_CullingResults, backCamera.camera)
            {
                sortingCriteria = SortingCriteria.CommonOpaque,
                renderQueueRange = RenderQueueRange.opaque,
            };
            passData.renderListHandle = builder.UseRendererList(renderGraph.CreateRendererList(renderListDesc));
            builder.WriteTexture(GBuffer.Instance.basicColorTextureHandle);
            builder.WriteTexture(GBuffer.Instance.normalTextureHandle);
            builder.WriteTexture(GBuffer.Instance.specPowerTextureHandle);
            builder.WriteTexture(GBuffer.Instance.depthStencilTextureHandle);
            passData.backCamera = backCamera;

            builder.SetRenderFunc<RenderGBufferPassData>((RenderGBufferPassData data, RenderGraphContext context) =>
            {
                GBuffer.Instance.PreRender(context);
                backCamera.DeferredSetup(context.renderContext, context.cmd);
                CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.renderListHandle);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });

            builder.AllowPassCulling(false);
        }
    }

    private float m_SunRadius = 25f;
    private static int s_SunMatrix = Shader.PropertyToID("_SunWorldViewProjectMatrix");
    private static int s_SunColor = Shader.PropertyToID("_SunColor");
    private Material m_SunMaterial;
    public Material sunMaterial
    {
        get
        {
            if (m_SunMaterial == null)
            {
                m_SunMaterial = CoreUtils.CreateEngineMaterial("Hidden/SunEmissive");
            }
            return m_SunMaterial;
        }
    }

    class RenderSunPassData
    {
        public Matrix4x4 sunMatrix;
        public Color sunColor;
        public Material sunMaterial;
    }

    public void RenderSunPass(RenderGraph renderGraph, Color sunColor, Vector3 sunDir, BackCamera backCamera)
    {
        using (var builder = renderGraph.AddRenderPass<RenderSunPassData>("Render Sun Pass", out var passData))
        {
            passData.sunColor = sunColor;
            var camPos = backCamera.camera.transform.position;
            var view = backCamera.camera.worldToCameraMatrix;
            var proj = backCamera.camera.projectionMatrix;
            proj = GL.GetGPUProjectionMatrix(proj, true);
            passData.sunMatrix = proj * view * Matrix4x4.Translate(new(camPos.x - 200f * sunDir.x, -200f * sunDir.y, camPos.z - 200f * sunDir.z)) * Matrix4x4.Scale(new(m_SunRadius, m_SunRadius, m_SunRadius));
            passData.sunMaterial = sunMaterial;
            builder.SetRenderFunc<RenderSunPassData>((data, context) =>
            {
                context.cmd.SetGlobalColor(s_SunColor, data.sunColor);
                context.cmd.SetGlobalMatrix(s_SunMatrix, data.sunMatrix);
                context.cmd.DrawMesh(BasicPipeline.asset.sunMesh, Matrix4x4.identity, data.sunMaterial);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });

        }
    }

    class RenderEmissivePassData
    {
        public RendererListHandle renderListHandle;
        public TextureHandle backBuffer;
        public TextureHandle depthBuffer;
    }

    public void RenderEmissivePass(RenderGraph renderGraph, BackCamera backCamera, TextureHandle backBuffer)
    {
        using (var builder = renderGraph.AddRenderPass<RenderEmissivePassData>("Render Emissive Pass", out var passData))
        {
            CoreRendererListDesc renderListDesc = new(s_EmissivePassId, m_CullingResults, backCamera.camera)
            {
                sortingCriteria = SortingCriteria.CommonOpaque,
                renderQueueRange = RenderQueueRange.opaque,
            };
            passData.renderListHandle = builder.UseRendererList(renderGraph.CreateRendererList(renderListDesc));

            builder.UseColorBuffer(backBuffer, 0);
            builder.UseDepthBuffer(GBuffer.Instance.depthStencilTextureHandle, DepthAccess.Write);

            builder.SetRenderFunc<RenderEmissivePassData>((data, context) =>
            {
                CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.renderListHandle);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
        }
            
    }

    class ScreenSpaceReflectionPassData
    {
        public RendererListHandle rendererListHandle;
        public TextureHandle hdrTex;
    }
    private static readonly int s_HDRTextureId = Shader.PropertyToID("_HDRTexture");
    public void RenderReflection(RenderGraph renderGraph, BackCamera backCamera, TextureHandle reflectionTex, ShaderTagId reflectionPassId)
    {
        using var builder = renderGraph.AddRenderPass<ScreenSpaceReflectionPassData>("Screen Space Reflection", out var passData);
        RendererListDesc desc = new CoreRendererListDesc(reflectionPassId, m_CullingResults, backCamera.camera)
        {
            sortingCriteria = SortingCriteria.CommonOpaque,
            renderQueueRange = RenderQueueRange.opaque,
        };
        passData.rendererListHandle = builder.UseRendererList(renderGraph.CreateRendererList(desc));
        builder.UseColorBuffer(reflectionTex, 0);
        //builder.UseDepthBuffer(GBuffer.Instance.depthStencilTextureHandle, DepthAccess.Read);
        passData.hdrTex = builder.ReadTexture(LightManager.instance.colorTextureHandle);
        builder.SetRenderFunc<ScreenSpaceReflectionPassData>((data, context) =>
        {
            context.cmd.SetGlobalTexture(s_HDRTextureId, data.hdrTex);
            context.cmd.DrawRendererList(data.rendererListHandle);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        });
    }

    public void Dispose()
    {
        CoreUtils.Destroy(m_SunMaterial);

        m_SunMaterial = null;
    }

}
