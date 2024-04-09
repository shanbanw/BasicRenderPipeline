using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

public class PostFX
{
    private static PostFX m_Instance;
    public static PostFX instance
    {
        get
        {
            m_Instance ??= new PostFX();
            return m_Instance;
        }
    }
    private static readonly int s_DownScaleCB = Shader.PropertyToID("DownScaleCB");
    private static readonly int s_PostFXCB = Shader.PropertyToID("PostFXFinalPass");
    private static readonly int s_BokehHighlightScanCB = Shader.PropertyToID("BokehHighlightScanCB");

    private static readonly int s_AverageValues1DId = Shader.PropertyToID("_AverageValues1D");
    private static readonly int s_AverageLumId = Shader.PropertyToID("_AverageLum");
    private static readonly int s_PrevAverageLumId = Shader.PropertyToID("_PrevAverageLum");
    private static readonly int s_HDRTextureId = Shader.PropertyToID("_HDRTexture");
    private static readonly int s_DownScaleRTId = Shader.PropertyToID("_DownScaleRT");
    private static readonly int s_DownScaleTextureId = Shader.PropertyToID("_DownScaleTexture");
    private static readonly int s_AvgLumId = Shader.PropertyToID("_AvgLum");
    private static readonly int s_BloomRTId = Shader.PropertyToID("_BloomRT");
    private static readonly int s_BloomTexureId = Shader.PropertyToID("_BloomTexture");
    private static readonly int s_BlurInputTextureId = Shader.PropertyToID("_BlurInputTexture");
    private static readonly int s_BlurOutputRTId = Shader.PropertyToID("_BlurOutputRT");
    private static readonly int s_BlurInputResId = Shader.PropertyToID("_BlurInputRes");
    private static readonly int s_BokehStackBufferId = Shader.PropertyToID("_BokehStackBuffer");
    private static readonly int s_BokehStackSRVId = Shader.PropertyToID("_BokehStackSRV");

    public bool enabled { get; set; } = false;
    public bool isFirstFrame { get; set; } = false;
    public bool cameraTP { get; set; } = false;

    Tonemapping m_Tonemapping;
    Bloom m_Bloom;
    DepthOfField m_DepthOfField;
    Bokeh m_Bokeh;

    private int m_Width, m_Height;
    private int m_DownScaleGroups;
    private ComputeBuffer m_DownScale1DBuffer;
    private ComputeBuffer m_AvgLumBuffer;
    private ComputeBuffer m_PrevAvgLumBuffer;
    private ComputeBuffer m_BokehStackBuffer;
    private ComputeBuffer m_BokehIndirectDrawBuffer;

    private static readonly List<int> s_DrawBufferInit = new() { 0, 1, 0, 0};

    private TextureHandle m_DownScaleRT;
    private TextureHandle[] m_BloomTempRT = new TextureHandle[2];
    private TextureHandle m_BloomRT;
    private TextureDesc s_DownScaleDesc = new TextureDesc(new Vector2(0.25f, 0.25f), true)
    {
        colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
        useMipMap = false,
        enableRandomWrite = true,
    };

    public void Init(BackCamera backCamera)
    {
        var volumeStack = backCamera.volumeStack;

        m_Tonemapping = volumeStack.GetComponent<Tonemapping>();
        m_Bloom = volumeStack.GetComponent<Bloom>();
        m_DepthOfField = volumeStack.GetComponent<DepthOfField>();
        m_Bokeh = volumeStack.GetComponent<Bokeh>();
    }

    const int s_MaxBokehInst = 4056;
    private void PreparePostProcessing()
    {
        var rt = (RTHandle)LightManager.instance.colorTextureHandle;
        Vector2Int res = rt.GetScaledSize(rt.rtHandleProperties.currentViewportSize);
        m_Width = res.x;
        m_Height = res.y;
        m_DownScaleGroups = Mathf.CeilToInt((float)(m_Width * m_Height / (16.0 * 1024f)));
        if (m_Tonemapping.IsActive())
        {
            if (m_DownScale1DBuffer != null && m_DownScale1DBuffer.count != m_DownScaleGroups)
            {
                m_DownScale1DBuffer.Release();
                m_DownScale1DBuffer = null;
            }
            if (m_DownScale1DBuffer == null)
            {
                m_DownScale1DBuffer = new ComputeBuffer(m_DownScaleGroups, sizeof(float));
            }

            if (m_AvgLumBuffer == null)
                m_AvgLumBuffer = new ComputeBuffer(1, sizeof(float));

            if (m_PrevAvgLumBuffer == null)
                m_PrevAvgLumBuffer = new ComputeBuffer(1, sizeof(float));

            if (m_BokehStackBuffer == null)
                m_BokehStackBuffer = new ComputeBuffer(s_MaxBokehInst, 7 * sizeof(float), ComputeBufferType.Append);
            if (m_BokehIndirectDrawBuffer == null)
            {
                m_BokehIndirectDrawBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
                m_BokehIndirectDrawBuffer.SetData(s_DrawBufferInit);
            }
                
        }


    }

    public TextureHandle finalColorTarget { get; private set; }
    private static readonly TextureDesc s_ColorTextureDesc = new TextureDesc(new Vector2(1f, 1f), true)
    {
        //colorFormat = GraphicsFormat.R8G8B8A8_SRGB,
        colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
        useMipMap = false,
    };

    private Material m_FinalMat;
    public Material finalMat
    {
        get
        {
            if (m_FinalMat == null)
            {
                m_FinalMat = CoreUtils.CreateEngineMaterial("Hidden/PostFX");
            }
            return m_FinalMat;
        }
    }

    class PostProcessingPassData
    {
        public TextureHandle finalColorTarget;
        public TextureHandle downScaleRT;
        public TextureHandle[] tempBloomRT;
        public TextureHandle bloomRT;
        public ComputeShader downScaleCS;
        public ComputeShader blurCS;
        public ComputeShader bokehHighlightScanCS;
        public float middleGrey;
        public float white;
        public float adaptation;
        public float bloomThreshold;
        public float bloomScale;
        public float dofFarStart;
        public float dofFarRangeRcp;
        public Vector4 perspectiveValues;
        public Material finalMat;
        public Material bokehRenderMat;
    }
    public void PostProcessing(RenderGraph renderGraph, BackCamera backCamera)
    {
        enabled = m_Tonemapping.IsActive();

        if (!enabled)
        {
            return;
        }

        finalColorTarget = renderGraph.CreateTexture(s_ColorTextureDesc);
        m_DownScaleRT = renderGraph.CreateTexture(s_DownScaleDesc);
        m_BloomTempRT[0] = renderGraph.CreateTexture(s_DownScaleDesc);
        m_BloomTempRT[1] = renderGraph.CreateTexture(s_DownScaleDesc);
        m_BloomRT = renderGraph.CreateTexture(s_DownScaleDesc);

        using (var builder = renderGraph.AddRenderPass<PostProcessingPassData>("Post Processing", out var passData))
        {
            
            builder.ReadTexture(LightManager.instance.colorTextureHandle);
            passData.downScaleRT = builder.ReadWriteTexture(m_DownScaleRT);
            builder.ReadWriteTexture(m_BloomTempRT[0]);
            builder.ReadWriteTexture(m_BloomTempRT[1]);
            builder.ReadWriteTexture(m_BloomRT);
            passData.tempBloomRT = m_BloomTempRT;
            passData.bloomRT = m_BloomRT;
            passData.finalColorTarget = builder.WriteTexture(finalColorTarget);
            var resources = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources;
            passData.downScaleCS = resources.shaders.downScaleCS;
            passData.blurCS = resources.shaders.blurCS;
            passData.bokehHighlightScanCS = resources.shaders.bokehHighlightScan;
            passData.middleGrey = m_Tonemapping.middleGrey.value;
            passData.white = m_Tonemapping.white.value;
            passData.adaptation = m_Tonemapping.adaptation.value;
            passData.bloomThreshold = m_Bloom.bloomThreshold.value; 
            passData.bloomScale = m_Bloom.bloomScale.value;
            passData.dofFarStart = m_DepthOfField.dofFarStart.value;
            passData.dofFarRangeRcp = 1.0f / Mathf.Max(m_DepthOfField.dofFarRange.value, 0.001f);
            passData.perspectiveValues = backCamera.perspectiveValues;
            passData.finalMat = finalMat;
            passData.bokehRenderMat = resources.materials.bokehRenderMat;

            builder.SetRenderFunc<PostProcessingPassData>((data, context) =>
            {
                instance.PreparePostProcessing();

                instance.DownScale(data, context);
                instance.Bloom(data, context);
                instance.Blur(data, context);
                instance.BokehHighLightScan(data, context);
                instance.FinalPass(data, context);
                instance.BokehRender(data, context);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();

                instance.Swap();
            });
        }
    }

    private void Swap()
    {
        var temp = m_AvgLumBuffer;
        m_AvgLumBuffer = m_PrevAvgLumBuffer;
        m_PrevAvgLumBuffer = temp;
    }

    private void DownScale(PostProcessingPassData data, RenderGraphContext context)
    {
        // output
        context.cmd.SetComputeBufferParam(data.downScaleCS, 0, s_AverageLumId, m_DownScale1DBuffer);
        context.cmd.SetComputeTextureParam(data.downScaleCS, 0, s_DownScaleRTId, m_DownScaleRT);
        // input
        context.cmd.SetComputeTextureParam(data.downScaleCS, 0, s_HDRTextureId, LightManager.instance.colorTextureHandle);
        

        DownScaleCB downScaleCB = new DownScaleCB();
        downScaleCB._Width = m_Width / 4;
        downScaleCB._Height = m_Height / 4;
        downScaleCB._TotalPixels = downScaleCB._Width * downScaleCB._Height;
        downScaleCB._GroupSize = m_DownScaleGroups;
        float adaptationNorm;
        if(isFirstFrame || cameraTP)
        {
            adaptationNorm = 0.0f;
        }
        else
        {
            adaptationNorm = Mathf.Min(data.adaptation < 0.0001f ? 1.0f : Time.deltaTime / data.adaptation, 0.9999f);
        }
        downScaleCB._Adaptation = adaptationNorm;
        downScaleCB._BloomThreshold = data.bloomThreshold;

        //ConstantBuffer.Push(context.cmd, downScaleCB, data.downScaleCS, s_DownScaleCB);

        context.cmd.DispatchCompute(data.downScaleCS, 0, m_DownScaleGroups, 1, 1);

        // output
        context.cmd.SetComputeBufferParam(data.downScaleCS, 1, s_AverageLumId, m_AvgLumBuffer);
        // input
        context.cmd.SetComputeBufferParam(data.downScaleCS, 1, s_AverageValues1DId, m_DownScale1DBuffer);
        context.cmd.SetComputeBufferParam(data.downScaleCS, 1, s_PrevAverageLumId, m_PrevAvgLumBuffer);
        ConstantBuffer.Push(context.cmd, downScaleCB, data.downScaleCS, s_DownScaleCB);
        context.cmd.DispatchCompute(data.downScaleCS, 1, 1, 1, 1);
    }

    private void Bloom(PostProcessingPassData data, RenderGraphContext context)
    {
        //Output
        context.cmd.SetComputeTextureParam(data.downScaleCS, 2, s_BloomRTId, m_BloomTempRT[0]);

        //Input
        context.cmd.SetComputeBufferParam(data.downScaleCS, 2, s_AvgLumId, m_AvgLumBuffer);
        context.cmd.SetComputeTextureParam(data.downScaleCS, 2, s_DownScaleTextureId, m_DownScaleRT);

        context.cmd.DispatchCompute(data.downScaleCS, 2, m_DownScaleGroups, 1, 1);
    }

    private void Blur(PostProcessingPassData data, RenderGraphContext context)
    {
        data.blurCS.SetVector(s_BlurInputResId, new Vector4(m_Width / 4, m_Height / 4, 0, 0));

        // Vertical
        context.cmd.SetComputeTextureParam(data.blurCS, 0, s_BlurInputTextureId, m_BloomTempRT[0]);
        context.cmd.SetComputeTextureParam(data.blurCS, 0, s_BlurOutputRTId, m_BloomTempRT[1]);
        int x = Mathf.CeilToInt(m_Width / 4.0f);
        int y = Mathf.CeilToInt((m_Height / 4.0f) / (128.0f - 12.0f));
        context.cmd.DispatchCompute(data.blurCS, 0, x, y, 1);

        // Horiz
        context.cmd.SetComputeTextureParam(data.blurCS, 1, s_BlurInputTextureId, m_BloomTempRT[1]);
        context.cmd.SetComputeTextureParam(data.blurCS, 1, s_BlurOutputRTId, m_BloomRT);
        x = Mathf.CeilToInt((m_Width / 4.0f) / (128.0f - 12.0f));
        y = Mathf.CeilToInt((m_Height / 4.0f));
        context.cmd.DispatchCompute(data.blurCS, 1, x, y, 1);
    }

    private void FinalPass(PostProcessingPassData data, RenderGraphContext context)
    {
        CoreUtils.SetRenderTarget(context.cmd, data.finalColorTarget);
        context.cmd.SetGlobalTexture(s_HDRTextureId, LightManager.instance.colorTextureHandle);
        context.cmd.SetGlobalBuffer(s_AvgLumId, m_AvgLumBuffer);
        context.cmd.SetGlobalTexture(s_BloomTexureId, m_BloomRT);
        context.cmd.SetGlobalTexture(s_DownScaleTextureId, data.downScaleRT);

        PostFXFinalPass postFXCB = new()
        {
            _MiddleGrey = data.middleGrey,
            _LumWhiteSqr = Mathf.Pow(data.middleGrey * data.white, 2),
            _RTScale = RTHandles.rtHandleProperties.rtHandleScale,
            _BloomScale = data.bloomScale,
            //_PerspectiveValues = data.perspectiveValues,
            _DOFFarStart = data.dofFarStart,
            _DOFFarRangeRcp = data.dofFarRangeRcp,
        };
        ConstantBuffer.PushGlobal(context.cmd, postFXCB, s_PostFXCB);

        CoreUtils.DrawFullScreen(context.cmd, data.finalMat, null, 0);
    }

    private void BokehHighLightScan(PostProcessingPassData data, RenderGraphContext context)
    {
        BokehHighlightScanCB scanCB = new() 
        { 
            _BokehBlurThreshold = m_Bokeh.bokehBlurThreshold.value,
            _BokehLumThreshold = m_Bokeh.bokehLumThreshold.value,
            _BokehRadiusScale = m_Bokeh.bokehRadiusScale.value,
            _BokehColorScale = m_Bokeh.bokehColorScale.value,
        };
        ConstantBuffer.Push(context.cmd, scanCB, data.bokehHighlightScanCS, s_BokehHighlightScanCB);
        context.cmd.SetGlobalTexture(s_HDRTextureId, LightManager.instance.colorTextureHandle);
        context.cmd.SetGlobalBuffer(s_AvgLumId, m_AvgLumBuffer);
        // Output
        m_BokehStackBuffer.SetCounterValue(0);
        context.cmd.SetComputeBufferParam(data.bokehHighlightScanCS, 0, s_BokehStackBufferId, m_BokehStackBuffer);

        int x = Mathf.CeilToInt((m_Width * m_Height) / 1024.0f);
        context.cmd.DispatchCompute(data.bokehHighlightScanCS, 0, x, 1, 1);
    }

    private void BokehRender(PostProcessingPassData data, RenderGraphContext context)
    {
        context.cmd.CopyCounterValue(m_BokehStackBuffer, m_BokehIndirectDrawBuffer, 0);
        CoreUtils.SetRenderTarget(context.cmd, data.finalColorTarget);
        context.cmd.SetGlobalBuffer(s_BokehStackSRVId, m_BokehStackBuffer);
        context.cmd.DrawProceduralIndirect(Matrix4x4.identity, data.bokehRenderMat, 0, MeshTopology.Points, m_BokehIndirectDrawBuffer);
    }

    public void Release()
    {
        CoreUtils.Destroy(m_FinalMat);
        CoreUtils.SafeRelease(m_DownScale1DBuffer);
        CoreUtils.SafeRelease(m_AvgLumBuffer);
        CoreUtils.SafeRelease(m_PrevAvgLumBuffer);
        CoreUtils.SafeRelease(m_BokehStackBuffer);
        CoreUtils.SafeRelease(m_BokehIndirectDrawBuffer);

        m_FinalMat = null;
        m_DownScale1DBuffer = null;
        m_AvgLumBuffer = null;
        m_PrevAvgLumBuffer = null;
        m_BokehStackBuffer = null;
        m_BokehIndirectDrawBuffer = null;
    }
}