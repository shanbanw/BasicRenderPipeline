using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class GBuffer
{
    private GBuffer() { }
    private static GBuffer s_GBuffer;
    public static GBuffer Instance
    {
        get
        {
            if (s_GBuffer == null)
            {
                s_GBuffer = new GBuffer();
            }
            return s_GBuffer;
        }
    }

    //private static readonly GraphicsFormat s_DepthStencilTextureFormat = GraphicsFormat.D24_UNorm_S8_UInt;
    private static readonly GraphicsFormat s_BasicColorTextureFormat = GraphicsFormat.R8G8B8A8_UNorm;
    private static readonly GraphicsFormat s_NormalTextureFormat = GraphicsFormat.B10G11R11_UFloatPack32;
    private static readonly GraphicsFormat s_SpecPowerTextureFormat = GraphicsFormat.R8G8B8A8_UNorm;

    private static readonly TextureDesc s_DepthStencilTextureDesc = new TextureDesc(new Vector2(1f, 1f), true)
    {
        //colorFormat = s_DepthStencilTextureFormat,
        depthBufferBits = DepthBits.Depth24,
        useMipMap = false,
        clearBuffer = true,
        clearColor = Color.clear,
    };
    private static readonly TextureDesc s_BasicColorTextureDesc = new TextureDesc(new Vector2(1f, 1f), true)
    {
        colorFormat = s_BasicColorTextureFormat,
        useMipMap = false,
        clearBuffer = true,
        clearColor = Color.clear,
    };
    private static readonly TextureDesc s_NormalTextureDesc = new TextureDesc(new Vector2(1f, 1f), true)
    {
        colorFormat = s_NormalTextureFormat,
        useMipMap = false ,
        clearBuffer = true,
        clearColor = Color.clear,
    };
    private static readonly TextureDesc s_SpecPowerTextureDesc = new TextureDesc(new Vector2(1f, 1f), true)
    {
        colorFormat = s_SpecPowerTextureFormat,
        useMipMap = false,
        clearBuffer = true,
        clearColor = Color.clear,
    };

    public TextureHandle depthStencilTextureHandle { get; private set; }
    public TextureHandle basicColorTextureHandle { get; private set; }
    public TextureHandle normalTextureHandle { get; private set; }
    public TextureHandle specPowerTextureHandle { get; private set; }

    public void CreateTextures(RenderGraph renderGraph)
    {
        depthStencilTextureHandle = renderGraph.CreateTexture(s_DepthStencilTextureDesc);
        basicColorTextureHandle = renderGraph.CreateTexture(s_BasicColorTextureDesc);
        normalTextureHandle = renderGraph.CreateTexture(s_NormalTextureDesc);
        specPowerTextureHandle = renderGraph.CreateTexture(s_SpecPowerTextureDesc);
    }

    public void PreRender(RenderGraphContext context)
    {
        var colorBuffers = context.renderGraphPool.GetTempArray<RenderTargetIdentifier>(3);
        colorBuffers[0] = basicColorTextureHandle;
        colorBuffers[1] = normalTextureHandle;
        colorBuffers[2] = specPowerTextureHandle;
        CoreUtils.SetRenderTarget(context.cmd, colorBuffers, depthStencilTextureHandle, ClearFlag.None);
    }

    private static readonly int s_DepthTexId = Shader.PropertyToID("_DepthTex");
    private static readonly int s_BasicColorTexId = Shader.PropertyToID("_ColorSpecIntensityTex");
    private static readonly int s_NormalTexId = Shader.PropertyToID("_NormalTex");
    private static readonly int s_SpecPowerTexId = Shader.PropertyToID("_SpecPowTex");

    public void SetTextures(RenderGraphContext context)
    {
        var cmd = context.cmd;
        cmd.SetGlobalTexture(s_DepthTexId, depthStencilTextureHandle);
        cmd.SetGlobalTexture(s_BasicColorTexId, basicColorTextureHandle);
        cmd.SetGlobalTexture(s_NormalTexId, normalTextureHandle);
        cmd.SetGlobalTexture(s_SpecPowerTexId, specPowerTextureHandle);
    }
}
