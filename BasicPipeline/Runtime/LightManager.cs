using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class LightManager
{
    private LightManager()
    {

    }

    private static LightManager m_Instance;
    public static LightManager instance
    {
        get
        {
            m_Instance ??= new LightManager();
            return m_Instance;
        }
    }

    private static readonly int s_AOTextureId = Shader.PropertyToID("_AOTexture");

    private static readonly int s_ShaderVariablesLightId = Shader.PropertyToID("ShaderVariablesLight");
    private static readonly int s_ShaderVariablesPointLightId = Shader.PropertyToID("ShaderVariablesPointLight");
    private static readonly int s_ShaderVariablesSpotLightId = Shader.PropertyToID("ShaderVariablesSpotLight");

    private static readonly string PointLightOn = "POINT_LIGHT_ON";
    private static readonly string SpotLightOn = "SPOT_LIGHT_ON";

    private static readonly string FogOn = "FOG_ON";
    private static readonly int s_FogCBId = Shader.PropertyToID("FogCB");

    private CullingResults m_CullingResults;

    private Vector3 m_DirectionalDir = -Vector3.up;
    private Vector3 m_DirectionalColor = Vector3.zero;
    public Vector3 sunDir
    {
        get { return m_DirectionalDir; }
    }
    public Color sunColor
    {
        get { return new Color(m_DirectionalColor.x, m_DirectionalColor.y, m_DirectionalColor.z, 1.0f);  }
    }

    private bool m_DirectionalCastShadow = false;
    private float m_DirectionalShadowNear = 0.2f;
    private int m_DirectionalVisibleLightIndex = -1;
    public bool isSunVisible { get { return m_DirectionalVisibleLightIndex >= 0; } }
    public bool directionalCastShadow { get { return m_DirectionalCastShadow; } }

    private static readonly int s_ShadowMapSize = 1024;
    private int m_LastShadowLight = -1;
    private int m_NextFreeSpotShadowmap = -1;
    private int m_TotalShadowSpotLights = 0;

    private int m_NextFreePointShadowmap = -1;
    private int m_TotalShadowPointLights = 0;

    private CascadedMatrixSet m_CascadedMatrixSet;

    struct LightData
    {
        public int visibleLightIndex;
        public LightType lightType;
        public Vector3 position;
        public Vector3 direction;
        public float range;
        public float outerAngle;
        public float innerAngle;
        public Vector3 color;
        public int shadowmapIndex;
        public float shadowNear;
        public float depthBias;
        public Matrix4x4 viewMatrix;
        public Matrix4x4 gpuProjMatrix;
        public Matrix4x4 nonFlipedProjMatrix;
        public Vector2 perspectiveValues;
        public ShadowSplitData shadowSplitData;
    }
    private List<LightData> m_Lights = new List<LightData>();
    public int numLights
    {
        get { return m_Lights.Count + 1;}
    }

    private void SetDirectional(int lightIndex, Vector3 dir, Vector3 color, float shadowNear = 0.2f, bool castShadow = false)
    {
        m_DirectionalVisibleLightIndex = lightIndex;
        m_DirectionalDir = dir;
        m_DirectionalColor = color;
        m_DirectionalShadowNear = shadowNear;
        m_DirectionalCastShadow = castShadow;
    }

    public readonly static float kEpsilon = Mathf.Pow(2f, -19f);
    private void AddPointLight(int lightIndex, Vector3 position, float range, Vector3 color, bool castShadow, float shadowNear, float depthBias)
    {
        LightData lightData = new LightData();
        lightData.visibleLightIndex = lightIndex;
        lightData.lightType = LightType.Point;
        lightData.position = position;
        lightData.range = range;
        lightData.color = color;
        lightData.shadowmapIndex = castShadow ? ++m_NextFreePointShadowmap : -1;
        lightData.shadowNear = shadowNear;
        lightData.depthBias = depthBias;

        if (castShadow)
        {
            Matrix4x4 projMatrix = Matrix4x4.Perspective(90f, 1f, shadowNear, range);
            lightData.gpuProjMatrix = GL.GetGPUProjectionMatrix(projMatrix, true);
            lightData.perspectiveValues = new Vector2(lightData.gpuProjMatrix[2, 2], lightData.gpuProjMatrix[2, 3]);
            float epsilon = -kEpsilon;
            if (SystemInfo.usesReversedZBuffer)
            {
                epsilon = kEpsilon;
            }
            lightData.gpuProjMatrix[2, 2] += epsilon;
        }

        m_Lights.Add(lightData);
    }

    private void AddSpotLight(int lightIndex, Vector3 position, Vector3 direction, float range, float outerAngle, float innerAngle, Vector3 color, bool castShadow, float shadowNear, float depthBias)
    {
        LightData lightData = new LightData();
        lightData.visibleLightIndex = lightIndex;
        lightData.lightType|= LightType.Spot;
        lightData.position = position;
        lightData.direction = direction;
        lightData.range = range;
        lightData.outerAngle = Mathf.Deg2Rad * outerAngle * 0.5f;
        lightData.innerAngle = Mathf.Deg2Rad * innerAngle * 0.5f;
        lightData.color = color;
        lightData.shadowmapIndex = castShadow ? ++m_NextFreeSpotShadowmap : -1;
        lightData.shadowNear = shadowNear;
        lightData.depthBias = depthBias;

        if (castShadow)
        {
            bool shadow = m_CullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(lightData.visibleLightIndex, out lightData.viewMatrix, out var projMatrix, out lightData.shadowSplitData);
            if (!shadow) Debug.LogError($"Spot Light {lightData.position} not cast shadow");
            lightData.gpuProjMatrix = GL.GetGPUProjectionMatrix(projMatrix, true);
            float epsilon = -kEpsilon;
            if (SystemInfo.usesReversedZBuffer)
            {
                epsilon = kEpsilon;
            }
            lightData.gpuProjMatrix[2, 2] += epsilon;
            lightData.nonFlipedProjMatrix = GL.GetGPUProjectionMatrix(projMatrix, false);
        }

        m_Lights.Add(lightData);
    }

    public void Init(BackCamera backCamera, ref CullingResults cullingResults)
    {
        m_CullingResults = cullingResults;
        int lightIndex = -1;
        foreach(var light in cullingResults.visibleLights)
        {
            lightIndex++;
            if (light.lightType == LightType.Directional)
            {
                //bool castShadow = cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(lightIndex, 0, 1, new Vector3(1.0f, 0.0f, 0.0f), 1024, 0.2f, out var viewMatrix, out var projMatrix, out var splitData);
                bool castShadow = light.light.shadows != LightShadows.None;
                castShadow &= cullingResults.GetShadowCasterBounds(lightIndex, out Bounds bounds);
                if (castShadow)
                {
                    m_CascadedMatrixSet = CascadedMatrixSet.GetOrCreateCascadedMatrixSet(backCamera.camera);
                    m_CascadedMatrixSet.Init(backCamera.camera, s_ShadowMapSize, BasicPipeline.asset.cascadeCount, BasicPipeline.asset.cascadeRanges);
                    m_CascadedMatrixSet.antiFlickerOn = BasicPipeline.asset.antiFlickerOn;
                    m_CascadedMatrixSet.Update(light.localToWorldMatrix.GetColumn(2));
                }
                SetDirectional(lightIndex, light.localToWorldMatrix.GetColumn(2), RendererUtils.ColorToVector3(light.finalColor), light.light.shadowNearPlane, castShadow);
            }
            else if (light.lightType == LightType.Point)
            {
                bool castShadow = light.light.shadows != LightShadows.None;
                castShadow &= cullingResults.GetShadowCasterBounds(lightIndex, out Bounds bounds);
                if (castShadow)
                {
                    m_TotalShadowPointLights++;
                }
                AddPointLight(lightIndex, light.localToWorldMatrix.GetColumn(3), light.range, RendererUtils.ColorToVector3(light.light.color), castShadow, light.light.shadowNearPlane, light.light.shadowBias);
            }
            else if (light.lightType == LightType.Spot)
            {
                bool castShadow = light.light.shadows != LightShadows.None;
                castShadow &= cullingResults.GetShadowCasterBounds(lightIndex, out Bounds bounds);
                if (castShadow)
                {
                    m_TotalShadowSpotLights++;
                }
                AddSpotLight(lightIndex, light.localToWorldMatrix.GetColumn(3), light.localToWorldMatrix.GetColumn(2), light.range, light.spotAngle, light.light.innerSpotAngle, RendererUtils.ColorToVector3(light.finalColor), castShadow, light.light.shadowNearPlane, light.light.shadowBias);
            }
        }

    }

    public void DeInit()
    {
        SetDirectional(-1, Vector3.zero, Vector3.zero);
        ClearLights();
    }

    private void ClearLights()
    {
        m_Lights.Clear();
        m_LastShadowLight = -1;
        m_NextFreeSpotShadowmap = -1;
        m_TotalShadowSpotLights = 0;

        m_NextFreePointShadowmap = -1;
        m_TotalShadowPointLights = 0;
    }

    public void LightSetup(ScriptableRenderContext context, CommandBuffer cmd, int lightIndex, bool debugVolume = false)
    {
        if (lightIndex == 0)
        {
            DirectionalSetup(context, cmd);
        }
        else
        {
            LightData lightData = m_Lights[lightIndex - 1];
            if (lightData.lightType == LightType.Point)
                SetupPoint(context, cmd, ref lightData, debugVolume);
            else if (lightData.lightType == LightType.Spot)
                SetupSpot(context, cmd, ref lightData, debugVolume);
        }
    }

    private void DirectionalSetup(ScriptableRenderContext context, CommandBuffer cmd)
    {
        ShaderVariablesLight lightVariables = new ShaderVariablesLight();
        if (RenderSettings.ambientMode == AmbientMode.Trilight)
        {
            lightVariables._AmbientLower = RendererUtils.ColorToVector3(RenderSettings.ambientGroundColor);
            lightVariables._AmbientRange = RendererUtils.ColorToVector3(RenderSettings.ambientSkyColor - RenderSettings.ambientGroundColor);
        }
        else
        {
            lightVariables._AmbientLower = RendererUtils.ColorToVector3(BasicPipeline.asset.ambientLightData.lowerColor);
            lightVariables._AmbientRange = RendererUtils.ColorToVector3(BasicPipeline.asset.ambientLightData.upperColor - BasicPipeline.asset.ambientLightData.lowerColor);
        }
        lightVariables._DirToLight = -m_DirectionalDir;
        lightVariables._DirectionalColor = m_DirectionalColor;
        lightVariables._CascadeShadowmapIndex = -1f;
        if (m_DirectionalCastShadow)
        {
            lightVariables._CascadeShadowmapIndex = 0f;
            lightVariables._ToCascadeShadowSpace = m_CascadedMatrixSet.worldToShadowSpace;
            lightVariables._ToCascadeOffsetX = m_CascadedMatrixSet.toCascadeOffsetX;
            lightVariables._ToCascadeOffsetY = m_CascadedMatrixSet.toCascadeOffsetY;
            lightVariables._ToCascadeScale = m_CascadedMatrixSet.toCascadeScale;
            //m_CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(0, 0, 1, Vector3.zero, 1024, 1f, out var viewMat, out var projMat, out var splitData);
            //lightVariables._ToCascadeShadowSpace = GL.GetGPUProjectionMatrix(projMat, true) * viewMat;
            cmd.SetGlobalTexture(s_CascadeShadowmapId, cascadeShadowmap);
        }
        ConstantBuffer.PushGlobal<ShaderVariablesLight>(cmd, lightVariables, s_ShaderVariablesLightId);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    private void SetupPoint(ScriptableRenderContext context, CommandBuffer cmd, ref LightData lightData, bool debugVolume = false)
    {
        ShaderVariablesPointLight pointLightVariables = new ShaderVariablesPointLight();
        pointLightVariables._PointPosition = lightData.position;
        pointLightVariables._PointRangeRcp = 1.0f / lightData.range;
        pointLightVariables._PointColor = lightData.color;
        pointLightVariables._PointRange = lightData.range;
        pointLightVariables._PointPerspectiveValues = lightData.perspectiveValues;//new Vector2(lightData.gpuProjMatrix[2, 2] - (2 ^ -19), lightData.gpuProjMatrix[2, 3]);
        pointLightVariables._PointShadowmapIndex = lightData.shadowmapIndex;
        ConstantBuffer.PushGlobal<ShaderVariablesPointLight>(cmd, pointLightVariables, s_ShaderVariablesPointLightId);
        SetKeywords(cmd, LightType.Point);
        
        if (lightData.shadowmapIndex >= 0 && !debugVolume)
        {
            cmd.SetGlobalTexture(s_PointShadowmapId, pointShadowmap);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear( );
    }

    private void SetupSpot(ScriptableRenderContext context, CommandBuffer cmd, ref LightData lightData, bool debugVolume = false)
    {
        float cosInnerAngle = Mathf.Cos(lightData.innerAngle);
        float sinOuterAngle = Mathf.Sin(lightData.outerAngle);
        float cosOuterAngle = Mathf.Cos(lightData.outerAngle);

        Vector3 dir = lightData.direction;
        Vector3 up = (dir.y > 0.9 || dir.y < -0.9) ? new Vector3(0.0f, 0.0f, dir.y) : Vector3.up;
        float range = lightData.range + 2f;
        Matrix4x4 lightMat = Matrix4x4.LookAt(lightData.position, lightData.position + dir, up) * Matrix4x4.Scale(new Vector3(range, range, range));

        ShaderVariablesSpotLight spotLightVariables = new ShaderVariablesSpotLight();
        spotLightVariables._SpotPosition = lightData.position;
        spotLightVariables._SpotDirection = -lightData.direction;
        spotLightVariables._SpotRangeRcp = 1.0f / lightData.range;
        spotLightVariables._SpotCosOuterAngle = cosOuterAngle;
        spotLightVariables._SpotCosAttnRangeRcp = 1.0f / (cosInnerAngle - cosOuterAngle);
        spotLightVariables._SpotSinOuterAngle = sinOuterAngle;
        spotLightVariables._SpotColor = lightData.color;
        spotLightVariables._SpotLightMatrix = lightMat;
        spotLightVariables._ToShadowMap = lightData.nonFlipedProjMatrix * lightData.viewMatrix;
        spotLightVariables._SpotShadowmapIndex = lightData.shadowmapIndex;
        ConstantBuffer.PushGlobal<ShaderVariablesSpotLight>(cmd, spotLightVariables, s_ShaderVariablesSpotLightId);
        SetKeywords(cmd, LightType.Spot);

        if (lightData.shadowmapIndex >= 0 && !debugVolume)
            cmd.SetGlobalTexture(s_SpotShadowmapId, spotShadowmap);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear( );
    }

    private void SetKeywords(CommandBuffer cmd, LightType lightType)
    {
        CoreUtils.SetKeyword(cmd, PointLightOn, lightType == LightType.Point);
        CoreUtils.SetKeyword(cmd, SpotLightOn, lightType == LightType.Spot);
    }

    public void DoLighting(RenderGraph renderGraph, BackCamera backCamera, TextureHandle aoTexture)
    {
        DeferredBasePass(renderGraph, backCamera, aoTexture);

        for(int i = 1; i < numLights; i++) 
        {
            var lightData = m_Lights[i - 1];
            if (lightData.lightType == LightType.Point)
                DeferredPointPass(renderGraph, backCamera, i);
            else if (lightData.lightType == LightType.Spot)
                DeferredSpotPass(renderGraph, backCamera, i);
        }
    }

    private Material m_DeferredLitMat;
    public Material deferredLitMat
    {
        get
        {
            if (m_DeferredLitMat == null)
            {
                m_DeferredLitMat = CoreUtils.CreateEngineMaterial("Hidden/DeferredLit");
            }
            return m_DeferredLitMat;
        }
    }
    private static readonly TextureDesc s_ColorTextureDesc = new TextureDesc(new Vector2(1f, 1f), true)
    {
        colorFormat = BasicPipeline.asset.hdr ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_UNorm,
        useMipMap = false,
        clearBuffer = false,
        clearColor = new(0.02f, 0.02f, 0.02f, 0.0f)
    };
    public TextureHandle colorTextureHandle { get; private set; }

    private readonly RenderBufferLoadAction[] m_ColorBufferLoadAction = new RenderBufferLoadAction[] { RenderBufferLoadAction.Load };
    private readonly RenderBufferStoreAction[] m_ColorBufferStoreAction = new RenderBufferStoreAction[] { RenderBufferStoreAction.Store };

    class DeferredBasePassData
    {
        public TextureHandle colorBuffer;
        public TextureHandle depthBuffer;
        public TextureHandle aoTexture;
        public Color clearColor;
        public Material litMat;
        public bool fog;
        public FogCB fogCB;
    }
    private void DeferredBasePass(RenderGraph renderGraph, BackCamera backCamera, TextureHandle aoTexture)
    {
        using (var builder = renderGraph.AddRenderPass<DeferredBasePassData>("Deferred Base", out var passData))
        {
            passData.depthBuffer = builder.ReadTexture(GBuffer.Instance.depthStencilTextureHandle);
            builder.ReadTexture(GBuffer.Instance.basicColorTextureHandle);
            builder.ReadTexture(GBuffer.Instance.normalTextureHandle);
            builder.ReadTexture(GBuffer.Instance.specPowerTextureHandle);
            colorTextureHandle = renderGraph.CreateTexture(s_ColorTextureDesc);
            var colorBuffer = builder.WriteTexture(colorTextureHandle);
            passData.colorBuffer = colorBuffer;
            passData.litMat = deferredLitMat;
            if (m_DirectionalCastShadow)
                builder.ReadTexture(cascadeShadowmap);
            passData.aoTexture = builder.ReadTexture(aoTexture);

            passData.clearColor = backCamera.camera.backgroundColor;

            passData.fog = RenderSettings.fog;
            if (RenderSettings.fog)
            {
                FogCB fogCB = new FogCB()
                {
                    _FogColor = BasicPipeline.asset.fogColor,
                    _FogHighlightColor = BasicPipeline.asset.fogHighlightColor,
                    _FogGlobalDensity = BasicPipeline.asset.fogGlobalDensity,
                    _FogStartDepth = BasicPipeline.asset.fogStartDepth,
                    _FogHeightFalloff = BasicPipeline.asset.fogHeightFalloff,
                };
                passData.fogCB = fogCB;

                float highlightColorFactor = -Vector3.Dot(backCamera.camera.transform.forward, m_DirectionalDir) * 0.75f * (1.0f + Vector3.Dot(Vector3.up, m_DirectionalDir)); // * Mathf.Abs();
                Color clearColor = fogCB._FogHighlightColor * highlightColorFactor + fogCB._FogColor * (1.0f - highlightColorFactor);
                passData.clearColor = clearColor;
            }

            builder.SetRenderFunc<DeferredBasePassData>((DeferredBasePassData data, RenderGraphContext context)=>
            {
                var tempArr = context.renderGraphPool.GetTempArray<RenderTargetIdentifier>(1);
                tempArr[0] = data.colorBuffer;
                var bindingTarget = new RenderTargetBinding(tempArr, m_ColorBufferLoadAction, m_ColorBufferStoreAction, data.depthBuffer,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store)
                {
                    flags = RenderTargetFlags.ReadOnlyDepthStencil
                };
                context.cmd.SetRenderTarget(bindingTarget);
                context.cmd.ClearRenderTarget(false, true, data.clearColor);
                CoreUtils.SetViewport(context.cmd, data.colorBuffer);

                if (data.fog)
                {
                    ConstantBuffer.PushGlobal(context.cmd, data.fogCB, s_FogCBId);
                }

                CoreUtils.SetKeyword(context.cmd, FogOn, data.fog);
                
                //context.cmd.ClearRenderTarget(false, true, new(0.6f, 0.62f, 1.0f, 0.0f));
                GBuffer.Instance.SetTextures(context);
                context.cmd.SetGlobalTexture(s_AOTextureId, data.aoTexture);
                LightManager.instance.LightSetup(context.renderContext, context.cmd, 0);
                CoreUtils.DrawFullScreen(context.cmd, data.litMat, null, 0);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
        }
    }

    class DeferredAddPassData
    {
        public Material litMat;
        public int lightIndex;
    }
    private void DeferredPointPass(RenderGraph renderGraph, BackCamera backCamera, int lightIndex)
    {
        using (var builder = renderGraph.AddRenderPass<DeferredAddPassData>("Deferred Point", out var passData))
        {
            builder.ReadTexture(GBuffer.Instance.depthStencilTextureHandle);
            builder.ReadTexture(GBuffer.Instance.basicColorTextureHandle);
            builder.ReadTexture(GBuffer.Instance.normalTextureHandle);
            builder.ReadTexture(GBuffer.Instance.specPowerTextureHandle);
            if (IsLightCastShadow(lightIndex))
            {
                builder.ReadTexture(pointShadowmap);
            }

            passData.litMat = deferredLitMat;
            passData.lightIndex = lightIndex;

            builder.SetRenderFunc<DeferredAddPassData>((DeferredAddPassData data, RenderGraphContext context) =>
            {
                LightManager.instance.LightSetup(context.renderContext, context.cmd, data.lightIndex);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.litMat, 1, MeshTopology.Points, 2);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
        }
    }

    private static readonly int s_SpotShadowmapId = Shader.PropertyToID("_SpotShadowmapTexture");
    private static readonly int s_PointShadowmapId = Shader.PropertyToID("_PointShadowmapTexture");
    private static readonly int s_CascadeShadowmapId = Shader.PropertyToID("_CascadeShadowmapTexture");

    private void DeferredSpotPass(RenderGraph renderGraph, BackCamera backCamera, int lightIndex)
    {
        using (var builder = renderGraph.AddRenderPass<DeferredAddPassData>("Deferred Spot", out var passData))
        {
            builder.ReadTexture(GBuffer.Instance.depthStencilTextureHandle);
            builder.ReadTexture(GBuffer.Instance.basicColorTextureHandle);
            builder.ReadTexture(GBuffer.Instance.normalTextureHandle);
            builder.ReadTexture(GBuffer.Instance.specPowerTextureHandle);
            if (IsLightCastShadow(lightIndex))
            {
                builder.ReadTexture(spotShadowmap);
            }
            
            passData.litMat = deferredLitMat;
            passData.lightIndex = lightIndex;

            builder.SetRenderFunc<DeferredAddPassData>((DeferredAddPassData data, RenderGraphContext context) =>
            {
                LightManager.instance.LightSetup(context.renderContext, context.cmd, data.lightIndex);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.litMat, 2, MeshTopology.Points, 1);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
        }
    }

    private Material m_DebugLightVolumeMat;
    public Material debugLightVolumeMat
    {
        get
        {
            if (m_DebugLightVolumeMat == null)
                m_DebugLightVolumeMat = CoreUtils.CreateEngineMaterial("Hidden/DebugLightVolume");
            return m_DebugLightVolumeMat;
        }
    }
    public void DebugPointLightVolume(RenderGraph renderGraph, BackCamera backCamera, int lightIndex)
    {
        using (var builder = renderGraph.AddRenderPass<DeferredAddPassData>("Debug Point Light Volume", out var passData))
        {
            passData.litMat = debugLightVolumeMat;
            passData.lightIndex = lightIndex;
            builder.UseColorBuffer(renderGraph.ImportBackbuffer(new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget)), 0);
            builder.SetRenderFunc<DeferredAddPassData>((DeferredAddPassData data, RenderGraphContext context) =>
            {
                LightManager.instance.LightSetup(context.renderContext, context.cmd, data.lightIndex, true);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.litMat, 0, MeshTopology.Points, 2);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
        }
    }

    public void DebugSpotLightVolume(RenderGraph renderGraph, BackCamera backCamera, int lightIndex)
    {
        using (var builder = renderGraph.AddRenderPass<DeferredAddPassData>("Debug Spot Light Volume", out var passData))
        {
            passData.litMat = debugLightVolumeMat;
            passData.lightIndex = lightIndex;
            builder.UseColorBuffer(renderGraph.ImportBackbuffer(new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget)), 0);
            builder.SetRenderFunc<DeferredAddPassData>((DeferredAddPassData data, RenderGraphContext context) =>
            {
                LightManager.instance.LightSetup(context.renderContext, context.cmd, data.lightIndex, true);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.litMat, 1, MeshTopology.Points, 1);
                context.renderContext.ExecuteCommandBuffer(context.cmd);
                context.cmd.Clear();
            });
        }
    }

    public void DebugLightsVolume(RenderGraph renderGraph, BackCamera backCamera)
    {
        for (int i = 1; i < numLights; i++)
        {
            var lightData = m_Lights[i-1];
            if (lightData.lightType == LightType.Point)
                DebugPointLightVolume(renderGraph, backCamera, i);
            else if (lightData.lightType == LightType.Spot)
                DebugSpotLightVolume(renderGraph, backCamera, i);
        }
    }

    public bool IsLightCastShadow(int lightIndex)
    {
        lightIndex--;
        if (lightIndex < 0 || lightIndex >= m_Lights.Count)
            return false;

        return m_Lights[lightIndex].shadowmapIndex >= 0;
    }

    public LightType GetLightType(int lightIndex)
    {
        return m_Lights[--lightIndex].lightType;
    }

    public void Dispose()
    {
        CoreUtils.Destroy(m_DeferredLitMat);
        CoreUtils.Destroy(m_DebugLightVolumeMat);

        m_DeferredLitMat = null;
        m_DebugLightVolumeMat = null;
    }
}
