using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class SSAOManager
{
    private SSAOManager()
    {

    }

    private static SSAOManager m_Instance;
    public static SSAOManager instance
    {
        get
        {
            m_Instance ??= new SSAOManager();
            return m_Instance;
        }
    }

    private static readonly int s_DepthTextureId = Shader.PropertyToID("_DepthTex");
    private static readonly int s_NormalTextureId = Shader.PropertyToID("_NormalTex");

    private static readonly int s_SSAmbientOcclusionCBId = Shader.PropertyToID("SSAmbientOcclusion");
    private static readonly int s_DepthDownScaleBufferId = Shader.PropertyToID("_DepthDownScaleBuffer");
    private static readonly int s_DepthDownScaleSRVId = Shader.PropertyToID("_DepthDownScaleSRV");
    private static readonly int s_AORT = Shader.PropertyToID("_AORT");

    private TextureDesc m_AORTDesc = new TextureDesc(new Vector2(0.5f, 0.5f), true)
    {
        colorFormat = GraphicsFormat.R32_SFloat,
        useMipMap = false,
        enableRandomWrite = true,
    };

    class SSAOComputePassData
    {
        public ComputeShader aoCS;
        public TextureHandle aoTexture;
        public ComputeBufferHandle depthDownScaleBuffer;
        public TextureHandle depthTexture;
        public TextureHandle normalTexture;
        public float width;
        public float height;
        public float farClip;
        public float offsetRadius;
        public float radius;

    }
    public TextureHandle Compute(RenderGraph renderGraph, BackCamera backCamera)
    {
        using var builder = renderGraph.AddRenderPass<SSAOComputePassData>("SSAO Compute Pass", out var passData);
        Vector2Int res = backCamera.finalViewport;
        passData.width = res.x / 2.0f;
        passData.height = res.y / 2.0f;
        passData.farClip = backCamera.camera.farClipPlane;
        passData.offsetRadius = BasicPipeline.asset.aoSampleRadius;
        passData.radius = BasicPipeline.asset.aoRadius;
        passData.aoCS = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.shaders.ssaoCS;
        passData.depthDownScaleBuffer = builder.CreateTransientComputeBuffer(new ComputeBufferDesc((int)(passData.width * passData.height), 4 * sizeof(float), ComputeBufferType.Structured));
        passData.aoTexture = renderGraph.CreateTexture(m_AORTDesc);
        builder.ReadWriteTexture(passData.aoTexture);
        passData.depthTexture = builder.ReadTexture(GBuffer.Instance.depthStencilTextureHandle);
        passData.normalTexture = builder.ReadTexture(GBuffer.Instance.normalTextureHandle);
        builder.SetRenderFunc<SSAOComputePassData>((data, context) =>
        {
            instance.DownScaleDepth(data, context);
            instance.ComputeSSAO(data, context);
            // TODO Blur 
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        });
        return passData.aoTexture;
    }

    private void DownScaleDepth(SSAOComputePassData data, RenderGraphContext context)
    {
        SSAmbientOcclusion aoCB = new()
        {
            _DepthDownScaleRes = new Vector4(data.width, data.height, 1.0f / data.width, 1.0f / data.height),
            _SSAOOffsetRadius = data.offsetRadius,
            _SSAORadius = data.radius,
            _SSAOMaxDepth = data.farClip,
        };
        ConstantBuffer.Push(context.cmd, aoCB, data.aoCS, s_SSAmbientOcclusionCBId);

        // Output
        context.cmd.SetComputeBufferParam(data.aoCS, 0, s_DepthDownScaleBufferId, data.depthDownScaleBuffer);
        // Input
        context.cmd.SetComputeTextureParam(data.aoCS, 0, s_DepthTextureId, data.depthTexture);
        context.cmd.SetComputeTextureParam(data.aoCS, 0, s_NormalTextureId, data.normalTexture);

        context.cmd.DispatchCompute(data.aoCS, 0, Mathf.CeilToInt(data.width * data.height / 1024.0f), 1, 1);
    }

    private void ComputeSSAO(SSAOComputePassData data, RenderGraphContext context)
    {
        // Output
        context.cmd.SetComputeTextureParam(data.aoCS, 1, s_AORT, data.aoTexture);
        // Input
        context.cmd.SetComputeBufferParam(data.aoCS, 1, s_DepthDownScaleSRVId, data.depthDownScaleBuffer);
        context.cmd.DispatchCompute(data.aoCS, 1, Mathf.CeilToInt(data.width * data.height / 1024.0f), 1, 1);
    }

}
