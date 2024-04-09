using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class SSLRManager
{
    private SSLRManager() { }
    public static SSLRManager instance = new SSLRManager();

    private float m_InitDecay = 0.2f;
    private float m_DistDecay = 0.8f;
    private float m_MaxDeltaLen = 0.005f;

    private static readonly TextureDesc s_OcclusionRTDesc = new(new Vector2(0.5f, 0.5f), false)
    {
        colorFormat = GraphicsFormat.R8_UNorm,
        useMipMap = false,
        enableRandomWrite = true,
    };
    private static readonly TextureDesc s_LightRaysRTDesc = new(new Vector2(0.5f, 0.5f), false)
    {
        colorFormat = GraphicsFormat.R8_UNorm,
        useMipMap = false,
        enableRandomWrite = true,
        clearBuffer = true,
        clearColor = Color.clear,
    };

    static readonly float s_MaxSunDist = 1.3f;
    public void Render(RenderGraph renderGraph, BackCamera backCamera, TextureHandle aoTexture)
    {
        var sunDir = LightManager.instance.sunDir;
        if (-Vector3.Dot(sunDir, backCamera.camera.transform.forward) <= 0.0f) return;
        var sunPos = -200f * sunDir;
        var camPos = backCamera.camera.transform.position;
        sunPos.x += camPos.x;
        sunPos.z += camPos.z;
        var viewMat = backCamera.camera.worldToCameraMatrix;
        var projMat = backCamera.camera.projectionMatrix;
        var sunPosSS = (projMat * viewMat).MultiplyPoint(sunPos);

        if (Mathf.Abs(sunPosSS.x) >= s_MaxSunDist || Mathf.Abs(sunPosSS.y) >= s_MaxSunDist)
            return;

        var sunColorAtt = LightManager.instance.sunColor * BasicPipeline.asset.sslrIntensity;
        float maxDist = Mathf.Max(Mathf.Abs(sunPosSS.x), Mathf.Abs(sunPosSS.y));
        if(maxDist >= 1.0f)
            sunColorAtt *= (s_MaxSunDist - maxDist);

        var occlusionRT = PrepareOcclusion(renderGraph, backCamera.finalViewport, aoTexture);    
        var rayTraceRT = RayTrace(renderGraph, occlusionRT, sunPosSS, sunColorAtt);
        Combine(renderGraph, rayTraceRT);
    }

    class PrepareOcclusionData
    {
        public Vector4 res;
        public TextureHandle occlusionRT;
        public ComputeShader occlusionCS;
    }
    TextureHandle PrepareOcclusion(RenderGraph renderGraph, Vector2Int finalViewport, TextureHandle ao)
    {
        using var builder = renderGraph.AddRenderPass<PrepareOcclusionData>("Prepare Occlusion Data", out var passData);
        builder.ReadTexture(ao);
        Vector4 res = Vector4.zero;
        res.x = finalViewport.x / 2;
        res.y = finalViewport.y / 2;
        passData.res = res;
        passData.occlusionRT = builder.WriteTexture(renderGraph.CreateTexture(s_OcclusionRTDesc));
        passData.occlusionCS = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.shaders.occlusionCS;
        builder.SetRenderFunc<PrepareOcclusionData>((data, context) =>
        {
            context.cmd.SetComputeVectorParam(data.occlusionCS, s_OcclusionResId, data.res);
            context.cmd.SetComputeTextureParam(data.occlusionCS, 0, s_OcclusionRTId, data.occlusionRT);
            int x = Mathf.CeilToInt((data.res.x * data.res.y) / 1024.0f);
            context.cmd.DispatchCompute(data.occlusionCS, 0, x, 1, 1);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        });
        return passData.occlusionRT;
    }

    class RayTraceData
    {
        public LightRayTraceCB rayTracyCB;
        public Material rayTraceMat;
        public TextureHandle occlusionRT;
    }
    TextureHandle RayTrace(RenderGraph renderGraph, TextureHandle occlusionRT, Vector2 sunPosSS, Color sunColor)
    {
        using var builder = renderGraph.AddRenderPass<RayTraceData>("Ray Trace Data", out var passData);
        passData.rayTracyCB = new LightRayTraceCB()
        {
            _SunPos = sunPosSS * 0.5f + new Vector2(0.5f, 0.5f),
            _InitDecay = m_InitDecay,
            _DistDecay = m_DistDecay,
            _RayColor = RendererUtils.ColorToVector3(sunColor),
            _MaxDeltaLen = m_MaxDeltaLen,
        };
        passData.occlusionRT = builder.ReadTexture(occlusionRT);
        var rayTraceRT = builder.UseColorBuffer(renderGraph.CreateTexture(s_LightRaysRTDesc), 0);
        passData.rayTraceMat = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.materials.lightRayRender;
        builder.SetRenderFunc<RayTraceData>((data, context) =>
        {
            ConstantBuffer.PushGlobal(context.cmd, data.rayTracyCB, s_RayTraceCBId);
            context.cmd.SetGlobalTexture(s_OcclusionRTId, data.occlusionRT);
            context.cmd.DrawProcedural(Matrix4x4.identity, data.rayTraceMat, 0, MeshTopology.Triangles, 3);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        });
        return rayTraceRT;
    }
    class SSLRCombineData
    {
        public TextureHandle rayTraceRT;
        public Material combineMat;
    }
    void Combine(RenderGraph renderGraph, TextureHandle rayTraceRT)
    {
        using var builder = renderGraph.AddRenderPass<SSLRCombineData>("SSLR Combine", out var passData);
        builder.UseColorBuffer(LightManager.instance.colorTextureHandle, 0);
        passData.rayTraceRT = builder.ReadTexture(rayTraceRT);
        passData.combineMat = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.materials.sslrCombine;
        builder.SetRenderFunc<SSLRCombineData>((data, context) =>
        {
            context.cmd.SetGlobalTexture(s_LightRaysTex, data.rayTraceRT);
            context.cmd.DrawProcedural(Matrix4x4.identity, data.combineMat, 0, MeshTopology.Triangles, 3);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        });
    }


    private static readonly int s_OcclusionResId = Shader.PropertyToID("_OcclusionRes");
    private static readonly int s_OcclusionRTId = Shader.PropertyToID("_OcclusionRT");
    private static readonly int s_RayTraceCBId = Shader.PropertyToID("LightRayTraceCB");
    private static readonly int s_LightRaysTex = Shader.PropertyToID("_LightRaysTex");
}