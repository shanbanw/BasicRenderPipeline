using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.VisualScripting;
using UnityEngine.UIElements;

public partial class BasicPipeline : RenderPipeline
{
    private BasicRenderPipelineGlobalSettings m_GlobalSettings;
    public override RenderPipelineGlobalSettings defaultSettings => m_GlobalSettings;

    internal BasicRenderPipelineRuntimeResources defaultResources { get { return m_GlobalSettings.renderPipelineResources; } }

    private RenderGraph m_RenderGraph = new("Basic Render Graph");

    private TextureHandle backBuffer;

    private int m_FrameCount = 0;
    public BasicPipeline(BasicPipelineAsset asset)
    {
        BasicPipeline.asset = asset;
        m_GlobalSettings = BasicRenderPipelineGlobalSettings.instance;
        RTHandles.Initialize(Screen.width, Screen.height);

        GraphicsSettings.useScriptableRenderPipelineBatching = true;
    }

    public static BasicPipelineAsset asset { get; private set; }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        Render(context, new List<Camera>(cameras));
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        BeginContextRendering(context, cameras);

#if UNITY_EDITOR
        int newCount = m_FrameCount;
        foreach (var camera in cameras)
        {
            if (camera.cameraType != CameraType.Preview)
            {
                newCount++;
                break;
            }
        }
#else
        int newCount = Time.frameCount;
#endif

        PostFX.instance.isFirstFrame = m_FrameCount == 0;

        if (newCount != m_FrameCount)
        {
            m_FrameCount = newCount;
        }

        cameras.Sort(CameraComparison);
        foreach (var camera in cameras)
        {
            BeginCameraRendering(context, camera);

            RTHandles.SetReferenceSize(camera.pixelWidth, camera.pixelHeight);

#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
            {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            }
#endif
            camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters);
            cullingParameters.shadowDistance = asset.shadowDistance;

            CullingResults cullingResults = context.Cull(ref cullingParameters);
            CommandBuffer cmd = CommandBufferPool.Get();
            BackCamera backCamera = BackCamera.GetOrCreateBackCamera(camera);

            RendererSceneManager.instance.Init(ref cullingResults);
            LightManager.instance.Init(backCamera, ref cullingResults);

            backCamera.Setup(context, cmd);
            VolumeManager.instance.Update(backCamera.volumeStack, null, 1 << camera.gameObject.layer);
            PostFX.instance.Init(backCamera);

            RenderGraphParameters renderGraphParameters = new RenderGraphParameters
            {
                executionName = backCamera.name,
                currentFrameIndex = m_FrameCount,
                rendererListCulling = true,
                commandBuffer = cmd,
                scriptableRenderContext = context
            };
            using (m_RenderGraph.RecordAndExecute(renderGraphParameters))
            {
                LightManager.instance.RenderShadows(m_RenderGraph);

                if (!backCamera.isMainGameView) 
                {
                    RenderForwardOpaque(context, cmd, backCamera);
                }
                else
                {
                    DecalManager.instance.PreRender(m_RenderGraph);
                    RainManager.instance.Update(backCamera);

                    RenderDeferredOpaque(context, cmd, backCamera);

                    if (asset.visualizeLightVolume)
                    {
                        LightManager.instance.DebugLightsVolume(m_RenderGraph, backCamera);
                    }

                    if (asset.visualizeGBuffer && backCamera.isMainGameView)
                    {
                        VisualizeGBuffer();
                    }
                }
                
            }

            LightManager.instance.DeInit();
            //context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            //context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            EndCameraRendering(context, camera);
            context.Submit();
        }

        m_RenderGraph.EndFrame();
        EndContextRendering(context, cameras);
    }

    private void RenderForwardOpaque(ScriptableRenderContext context, CommandBuffer cmd, BackCamera backCamera)
    {
        RendererSceneManager.instance.DepthPrepass(m_RenderGraph, backCamera);

        //LightManager.Instance.LightSetup(context, cmd, 0);
        RendererSceneManager.instance.RenderOpaqueForwardBase(m_RenderGraph, backCamera);

        for (int i = 1; i < LightManager.instance.numLights; i++)
        {
            //LightManager.Instance.LightSetup(context, cmd, i);
            RendererSceneManager.instance.RenderOpaqueForwardAdd(m_RenderGraph, backCamera, i);
        }
        //if (LightManager.Instance.isSunVisible)
        //{
        //    CSceneManager.Instance.RenderSunPass(m_RenderGraph, LightManager.Instance.sunColor, LightManager.Instance.sunDir, backCamera);
        //}
    }

    private void RenderDeferredOpaque(ScriptableRenderContext context, CommandBuffer cmd, BackCamera backCamera)
    {
        RainManager.instance.Simulation(m_RenderGraph);
        GBuffer.Instance.CreateTextures(m_RenderGraph);
        RendererSceneManager.instance.RenderGBufferPass(m_RenderGraph, backCamera);
        DecalManager.instance.Render(m_RenderGraph);
        TextureHandle aoTexture;
        if (asset.enableSSAO)
        {
            aoTexture = SSAOManager.instance.Compute(m_RenderGraph, backCamera);
        }
        else
            aoTexture = m_RenderGraph.defaultResources.whiteTexture;

        LightManager.instance.DoLighting(m_RenderGraph, backCamera, aoTexture);

        RendererSceneManager.instance.RenderEmissivePass(m_RenderGraph, backCamera, LightManager.instance.colorTextureHandle);

        if (LightManager.instance.isSunVisible)
        {
            RendererSceneManager.instance.RenderSunPass(m_RenderGraph, LightManager.instance.sunColor * 2f, LightManager.instance.sunDir, backCamera);
        }

        if (asset.enableSSR)
        {
            SSReflectionManager.Instance.RenderReflection(m_RenderGraph, backCamera);
            SSReflectionManager.Instance.DoReflectionBlend(m_RenderGraph);  
        }

        RainManager.instance.Render(m_RenderGraph);

        if(asset.enableSSAO && asset.enableSSLR)
        {
            SSLRManager.instance.Render(m_RenderGraph, backCamera, aoTexture);
        }

        PostFX.instance.PostProcessing(m_RenderGraph, backCamera);

        FinalBlitPass(backCamera);
    }

    private Material m_GBufferVisMat;
    public Material gbufferVisMat
    {
        get
        {
            if (m_GBufferVisMat == null)
                m_GBufferVisMat = new Material(Shader.Find("Hidden/GBufferVis"));
            return m_GBufferVisMat;
        }
    }

    class VisualizeGBufferData
    {
        public TextureHandle backBuffer;
        public Material visMat;
    }
    public void VisualizeGBuffer()
    {
        using (var builder = m_RenderGraph.AddRenderPass<VisualizeGBufferData>("Visualize GBuffer", out var passData))
        {
            builder.ReadTexture(GBuffer.Instance.depthStencilTextureHandle);
            builder.ReadTexture(GBuffer.Instance.basicColorTextureHandle);
            builder.ReadTexture(GBuffer.Instance.normalTextureHandle);
            builder.ReadTexture(GBuffer.Instance.specPowerTextureHandle);
            passData.visMat = gbufferVisMat;
            passData.backBuffer = builder.UseColorBuffer(m_RenderGraph.ImportBackbuffer(new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget)), 0);
            builder.SetRenderFunc<VisualizeGBufferData>((VisualizeGBufferData data, RenderGraphContext context) =>
            {
                GBuffer.Instance.SetTextures(context);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.visMat, 0, MeshTopology.Quads, 16, 1);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
        }
    }

    private Material m_FinalBlitMat;
    public Material finalBlitMat
    {
        get
        {
            if (m_FinalBlitMat == null)
            {
                m_FinalBlitMat = new Material(Shader.Find("Hidden/FinalBlit"));
            }
            return m_FinalBlitMat;
        }
    }

    private static readonly int s_FinalColorTexId = Shader.PropertyToID("_FinalColorTex");
    class FinalBlitPassData
    {
        public TextureHandle colorTex;
        public TextureHandle colorBuffer;
        public Material finalBlitMat;
    }

    public void FinalBlitPass(BackCamera backCamera)
    {
        using (var builder = m_RenderGraph.AddRenderPass<FinalBlitPassData>("Final Blit Pass", out var passData))
        {
            passData.colorTex = builder.ReadTexture(PostFX.instance.enabled ? PostFX.instance.finalColorTarget : LightManager.instance.colorTextureHandle);
            RenderTargetIdentifier backBuffer = backCamera.camera.targetTexture ?? new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            passData.colorBuffer = builder.WriteTexture(m_RenderGraph.ImportBackbuffer(backBuffer));
            passData.finalBlitMat = finalBlitMat;
            builder.SetRenderFunc<FinalBlitPassData>((FinalBlitPassData data, RenderGraphContext context) =>
            {
                context.cmd.SetRenderTarget(data.colorBuffer);
                context.cmd.ClearRenderTarget(true, true, Color.clear);
                context.cmd.SetGlobalTexture(s_FinalColorTexId, data.colorTex);
                CoreUtils.DrawFullScreen(context.cmd, data.finalBlitMat);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
        }
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        BackCamera.ClearAll();
        CleanupRenderGraph();
        ConstantBuffer.ReleaseAll();
        PostFX.instance.Release();
        LightManager.instance.Dispose();
        RendererSceneManager.instance.Dispose();
        RainManager.instance.Dispose();
        SSReflectionManager.Instance.Dispose();
        DecalManager.instance.Dispose();

        CoreUtils.Destroy(m_FinalBlitMat);
        CoreUtils.Destroy(m_GBufferVisMat); 
        
        m_FinalBlitMat = null;
        m_GBufferVisMat = null;
    }

    void CleanupRenderGraph()
    {
        m_RenderGraph.Cleanup();
        m_RenderGraph = null;
    }

    private int CameraComparison(Camera left, Camera right) 
    {
        return (int)left.depth - (int)right.depth;
    }
}