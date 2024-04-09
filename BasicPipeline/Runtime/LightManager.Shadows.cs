using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class LightManager
{
    private static int s_SpotViewProj = Shader.PropertyToID("_SpotViewProj");
    private static int s_PointViewProj = Shader.PropertyToID("_PointViewProj");
    private static int s_CascadeViewProj = Shader.PropertyToID("_CascadeViewProj");

    private static int s_ShadowmapIndex = Shader.PropertyToID("_ShadowmapIndex");
    private static int s_CascadeCount = Shader.PropertyToID("_CascadeCount");
    private static int s_ZClip = Shader.PropertyToID("_ZClip");

    private static readonly string s_SpotShadowGen = "SPOT_SHADOW_GEN";
    private static readonly string s_PointShadowGen = "POINT_SHADOW_GEN";
    private static readonly string s_CascadeShadowGen = "CASCADE_SHADOW_GEN";

    void SetShadowKeywords(CommandBuffer cmd, LightType lightType)
    {
        CoreUtils.SetKeyword(cmd, s_SpotShadowGen, lightType == LightType.Spot);
        CoreUtils.SetKeyword(cmd, s_PointShadowGen, lightType == LightType.Point);
        CoreUtils.SetKeyword(cmd, s_CascadeShadowGen, lightType == LightType.Directional);
    }

    public TextureHandle spotShadowmap { get; private set; }
    public TextureHandle pointShadowmap { get; private set; }
    public TextureHandle cascadeShadowmap { get; private set; }

    public void RenderShadows(RenderGraph renderGraph)
    {
        if (m_TotalShadowSpotLights > 0)
        {
            TextureDesc spotShadowmapDesc = new TextureDesc(s_ShadowMapSize, s_ShadowMapSize)
            {
                slices = m_TotalShadowSpotLights,
                depthBufferBits = DepthBits.Depth32,
                dimension = TextureDimension.Tex2DArray,
                isShadowMap = true,
            };
            spotShadowmap = renderGraph.CreateTexture(spotShadowmapDesc);
        }

        if (m_TotalShadowPointLights > 0)
        {
            TextureDesc pointShadowmapDesc = new TextureDesc(s_ShadowMapSize, s_ShadowMapSize)
            {
                slices = m_TotalShadowPointLights * 6,
                depthBufferBits = DepthBits.Depth32,
                dimension = TextureDimension.CubeArray,
                isShadowMap = true,
                clearBuffer = true,
            };
            pointShadowmap = renderGraph.CreateTexture(pointShadowmapDesc);
        }

        if(m_DirectionalCastShadow)
        {
            TextureDesc dirShadowmapDesc = new TextureDesc(s_ShadowMapSize, s_ShadowMapSize)
            {
                slices = BasicPipeline.asset.cascadeCount,
                depthBufferBits = DepthBits.Depth32,
                dimension = TextureDimension.Tex2DArray,
                isShadowMap = true,
            };
            cascadeShadowmap = renderGraph.CreateTexture(dirShadowmapDesc);
        }

        while (PrepareNextShadowLight())
        {
            LightData lightData = m_Lights[m_LastShadowLight];
            if(lightData.lightType == LightType.Spot)
            {
                SpotShadowGen(renderGraph, lightData);
            }
            else if(lightData.lightType == LightType.Point)
            {
                PointShadowGen(renderGraph, lightData);
            }
        }

        if (m_DirectionalCastShadow)
        {
            DirectionalShadowGen(renderGraph);
        }
    }

    private bool PrepareNextShadowLight()
    {
        while (++m_LastShadowLight < m_Lights.Count && m_Lights[m_LastShadowLight].shadowmapIndex < 0) ;

        return m_LastShadowLight < m_Lights.Count;
    }

    class SpotShadowGenPassData
    {
        public TextureHandle spotShadowmap;
        public ShadowDrawingSettings shadowDrawingSettings;
        public Matrix4x4 spotVP;
        public float depthBias;
        public int shadowmapIndex;
    }
    private void SpotShadowGen(RenderGraph renderGraph, in LightData lightData)
    {
        if (lightData.shadowmapIndex == -1) return;

        using (var builder = renderGraph.AddRenderPass<SpotShadowGenPassData>("Spot Shadow Gen Pass", out var passData))
        {
            passData.spotShadowmap = builder.WriteTexture(spotShadowmap);
            passData.spotVP = lightData.gpuProjMatrix * lightData.viewMatrix;

            passData.shadowDrawingSettings = new ShadowDrawingSettings(m_CullingResults, lightData.visibleLightIndex, BatchCullingProjectionType.Perspective)
            {
                splitData = lightData.shadowSplitData,
                objectsFilter = ShadowObjectsFilter.AllObjects
            };

            passData.depthBias = lightData.depthBias;
            passData.shadowmapIndex = lightData.shadowmapIndex;

            builder.SetRenderFunc<SpotShadowGenPassData>((SpotShadowGenPassData data, RenderGraphContext context) =>
            {
                context.cmd.SetRenderTarget(data.spotShadowmap, 0, CubemapFace.Unknown, data.shadowmapIndex);
                context.cmd.ClearRenderTarget(true, false, Color.clear);
                context.cmd.SetGlobalDepthBias(0.0f, 3.0f);
                context.cmd.SetGlobalMatrix(s_SpotViewProj, data.spotVP);
                context.cmd.SetGlobalInteger(s_ShadowmapIndex, 0);
                context.cmd.SetGlobalFloat(s_ZClip, 1.0f);
                LightManager.instance.SetShadowKeywords(context.cmd, LightType.Spot);
                RendererList rendererList = context.renderContext.CreateShadowRendererList(ref passData.shadowDrawingSettings);
                context.cmd.DrawRendererList(rendererList);
                context.cmd.SetGlobalDepthBias(0.0f, 0.0f);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
            builder.AllowPassCulling(false);
        }

    }


    class PointShadowGenPassData
    {
        public TextureHandle pointShadowmap;
        public ShadowDrawingSettings shadowDrawingSettings;
        public Matrix4x4 projMatrix;
        public float depthBias;
        public int shadowmapIndex;
        public CullingResults cullingResults;
        public int index;
        public Vector3 position;
    }

    void PointShadowGen(RenderGraph renderGraph, in LightData lightData)
    {
        if (lightData.shadowmapIndex < 0) return;

        using (var builder = renderGraph.AddRenderPass<PointShadowGenPassData>("Point Shadow Gen Pass", out var passData))
        {
            passData.pointShadowmap = builder.WriteTexture(pointShadowmap);
            passData.projMatrix = lightData.gpuProjMatrix;
            passData.depthBias = lightData.depthBias;
            passData.shadowmapIndex = lightData.shadowmapIndex;
            passData.cullingResults = m_CullingResults;
            passData.index = lightData.visibleLightIndex;
            passData.position = lightData.position;

            ShadowSplitData splitData = new ShadowSplitData();
            splitData.cullingSphere = new Vector4(lightData.position.x, lightData.position.y, lightData.position.z, Mathf.Infinity);

            passData.shadowDrawingSettings = new ShadowDrawingSettings(m_CullingResults, lightData.visibleLightIndex, BatchCullingProjectionType.Perspective)
            {
                splitData = splitData,
                objectsFilter = ShadowObjectsFilter.AllObjects
            };

            builder.SetRenderFunc<PointShadowGenPassData>((PointShadowGenPassData data, RenderGraphContext context) =>
            {
                Matrix4x4[] vpMats = context.renderGraphPool.GetTempArray<Matrix4x4>(6);
                for (int i = 0; i < 6; i++)
                {
                    Matrix4x4 viewMat = RendererUtils.GetCubemapViewMatrixCorrected((CubemapFace)i);
                    vpMats[i] = data.projMatrix * viewMat * Matrix4x4.Translate(-1f * data.position);
                    //data.cullingResults.ComputePointShadowMatricesAndCullingPrimitives(data.index, (CubemapFace)i, 0f, out var view, out var proj, out var splitData);
                    //vpMats[i] = GL.GetGPUProjectionMatrix(proj, true) * view;
                    //context.cmd.SetRenderTarget(data.pointShadowmap, 0, CubemapFace.Unknown, i);
                    //context.cmd.SetGlobalDepthBias(1.0f, 1.0f);
                    //context.cmd.SetGlobalMatrix(s_PointViewProj, vpMats[i]);
                    //data.shadowDrawingSettings.splitData = splitData;
                    //RendererList rendererList = context.renderContext.CreateShadowRendererList(ref data.shadowDrawingSettings);
                    //context.cmd.DrawRendererList(rendererList);
                }

                context.cmd.SetRenderTarget(data.pointShadowmap, 0, CubemapFace.Unknown, RenderTargetIdentifier.AllDepthSlices);
                //context.cmd.ClearRenderTarget(true, false, Color.clear);
                context.cmd.SetGlobalDepthBias(0f, 3f);
                context.cmd.SetGlobalMatrixArray(s_PointViewProj, vpMats);
                context.cmd.SetGlobalInteger(s_ShadowmapIndex, data.shadowmapIndex);
                context.cmd.SetGlobalFloat(s_ZClip, 1.0f);
                LightManager.instance.SetShadowKeywords(context.cmd, LightType.Point);
                RendererList rendererList = context.renderContext.CreateShadowRendererList(ref data.shadowDrawingSettings);
                context.cmd.DrawRendererList(rendererList);
                context.cmd.SetGlobalDepthBias(0.0f, 0.0f);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });

            builder.AllowPassCulling(false);
        }
    }

    class DirectionalShadowPassData
    {
        public TextureHandle dirShadowmap;
        public int cascadeCount;
        public CascadedMatrixSet cascadeSet;
        public ShadowDrawingSettings shadowDrawingSettings;
        public CullingResults cullingResults;
    }
    private void DirectionalShadowGen(RenderGraph renderGraph)
    {
        using (var builder = renderGraph.AddRenderPass<DirectionalShadowPassData>("Directional Shadow Gen", out var passData))
        {
            passData.dirShadowmap = builder.WriteTexture(cascadeShadowmap);
            passData.cascadeCount = BasicPipeline.asset.cascadeCount;
            passData.cascadeSet = m_CascadedMatrixSet;
            var splitData = new ShadowSplitData();
            splitData.cullingSphere = new(0f, 0f, 0f, Mathf.Infinity);
            passData.shadowDrawingSettings = new ShadowDrawingSettings(m_CullingResults, m_DirectionalVisibleLightIndex, BatchCullingProjectionType.Orthographic)
            {
                splitData = splitData,
                objectsFilter = ShadowObjectsFilter.AllObjects
            };
            passData.cullingResults = m_CullingResults;
            builder.SetRenderFunc<DirectionalShadowPassData>((DirectionalShadowPassData data, RenderGraphContext context) =>
            {
                Matrix4x4[] cascadeMat = context.renderGraphPool.GetTempArray<Matrix4x4>(4);
                for (int i = 0; i < 4; i++)
                {
                    //data.cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(0, 0, 1, Vector3.zero, 1024, 1f, out var viewMat, out var projMat, out var splitData);
                    cascadeMat[i] = data.cascadeSet.worldToCascadeProj[i]; //GL.GetGPUProjectionMatrix(projMat, true) * viewMat;//data.cascadeSet.worldToCascadeProj[i];
                }
                context.cmd.SetRenderTarget(data.dirShadowmap, 0, CubemapFace.Unknown, RenderTargetIdentifier.AllDepthSlices);
                context.cmd.ClearRenderTarget(true, false, Color.clear);
                context.cmd.SetGlobalDepthBias(0f, 3f);
                context.cmd.SetGlobalInteger(s_ShadowmapIndex, 0);
                context.cmd.SetGlobalMatrixArray(s_CascadeViewProj, cascadeMat);
                context.cmd.SetGlobalInteger(s_CascadeCount, data.cascadeCount);
                context.cmd.SetGlobalFloat(s_ZClip, 0.0f);
                LightManager.instance.SetShadowKeywords(context.cmd, LightType.Directional);
                RendererList rendererList = context.renderContext.CreateShadowRendererList(ref data.shadowDrawingSettings);
                context.cmd.DrawRendererList(rendererList);
                context.cmd.SetGlobalDepthBias(0.0f, 0.0f);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
            builder.AllowPassCulling(false);
        }
    }
}
