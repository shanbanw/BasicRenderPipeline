using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class SSReflectionManager
{
    private SSReflectionManager() { }
    public static SSReflectionManager Instance = new SSReflectionManager();

    private static readonly TextureDesc s_ReflectionTexDesc = new TextureDesc(new Vector2(1f, 1f), false)
    {
        colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
        useMipMap = false,
        enableRandomWrite = false,
        clearBuffer = true,
        clearColor = new Color(0.0f, 0.0f, 0.0f, 0.0f),
    };

    private static readonly ShaderTagId s_ReflectionPassId = new ShaderTagId("ScreenSpaceReflection");
    private TextureHandle m_ReflectionTex;

    public void RenderReflection(RenderGraph renderGraph, BackCamera backCamera)
    {
        m_ReflectionTex = renderGraph.CreateTexture(s_ReflectionTexDesc);
        RendererSceneManager.instance.RenderReflection(renderGraph, backCamera, m_ReflectionTex, s_ReflectionPassId);
    }

    Material m_SSRCombineMat = null;
    public Material ssrCombine
    {
        get
        {
            if (m_SSRCombineMat == null)
            {
                m_SSRCombineMat = new Material(Shader.Find("Hidden/SSRCombine"));
            }
            return m_SSRCombineMat;
        }
    }
    private static readonly int s_ReflectionTexId = Shader.PropertyToID("_ReflectionTex");
    class SSRCombineData
    {
        public Material blendMat;
        public TextureHandle reflectionTex;
    }
    public void DoReflectionBlend(RenderGraph renderGraph)
    {
        using var builder = renderGraph.AddRenderPass<SSRCombineData>("SSR Blend", out var passData);
        passData.reflectionTex = builder.ReadTexture(m_ReflectionTex);
        builder.UseColorBuffer(LightManager.instance.colorTextureHandle, 0);
        passData.blendMat = ssrCombine;
        builder.SetRenderFunc<SSRCombineData>((data, context) =>
        {
            context.cmd.SetGlobalTexture(s_ReflectionTexId, data.reflectionTex);
            context.cmd.DrawProcedural(Matrix4x4.identity, data.blendMat, 0, MeshTopology.Quads, 4);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        });

    }


    public void Dispose()
    {
        CoreUtils.Destroy(m_SSRCombineMat);

        m_SSRCombineMat = null;
    }
}
