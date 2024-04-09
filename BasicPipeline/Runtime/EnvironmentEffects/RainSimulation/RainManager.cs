using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.Collections.LowLevel.Unsafe;

public class RainManager
{
    private RainManager()
    {

    }

    private static RainManager m_Instance;
    public static RainManager instance
    {
        get
        {
            m_Instance ??= new RainManager();
            return m_Instance;
        }
    }

    const int k_NumRainGroupSize = 4;
    const int k_RainGridSize = k_NumRainGroupSize * 32;
    //const int k_HeightMapSize = 512;
    const int k_MaxRainDrops = k_RainGridSize * k_RainGridSize;

    static readonly int[] s_OffsetDelta = new[] { 13, 27 };
    static readonly RainDrop[] bufferInitData = new RainDrop[k_MaxRainDrops];

    RainVolume m_RainVolume;
    //Matrix4x4 m_RainViewProjMatrix;
    Vector3 m_ViewDir = Vector3.zero;
    float m_VecticalSpeed = -25f;
    float m_MaxWindEffect = 10f;
    float m_MaxWindVariance = 10f;
    float m_RainScale = 1.0f;
    Vector2 m_CurWindEffect = Vector2.zero;
    Vector3 m_BoundCenter = Vector3.zero;

    // Change the rain density gradually
    float m_MaxDiffPerSecond = 0.01f;
    float m_PrevRainDensity = 0;

    //RTHandle heightMapRT;
    ComputeBuffer m_RainDataBuffer;

    private bool paused
    {
        get
        {
            return m_RainVolume.pauseRainSimulation.value;
        }
    }
    private float boundHalfSizeX { get { return m_RainVolume.rainBoundHalfSizeX.value; } }
    private float boundHalfSizeY { get { return m_RainVolume.rainBoundHalfSizeY.value; } }
    private float boundHalfSizeZ { get { return m_RainVolume.rainBoundHalfSizeZ.value; } }
    private float timeScale { get { return m_RainVolume.rainTimeScale.value; } }
    private float simulationSpeed { get { return m_RainVolume.rainSimulationSpeed.value; } }
    private float rainDensity { get { return m_RainVolume.rainDensity.value; } }
    
    public void Update(BackCamera backCamera)
    {
        m_RainVolume = backCamera.volumeStack.GetComponent<RainVolume>();
        if (paused) return;
        var camPos = backCamera.camera.transform.position;
        var camForward = backCamera.camera.transform.forward;
        var forward = new Vector3(camForward.x, 0.0f, camForward.z);
        forward = forward.normalized;
        var offset = new Vector3(forward.x * boundHalfSizeX, 0.0f, forward.z * boundHalfSizeZ);
        m_BoundCenter = camPos + offset * 0.8f; // keep around 20 percent behind the camera

        var from = m_BoundCenter + new Vector3(0.0f, m_RainVolume.rainBoundHalfSizeY.value, 0.0f);
        var viewMat = Matrix4x4.LookAt(from, m_BoundCenter, Vector3.forward);
        viewMat[0,2] = -viewMat[0, 2]; viewMat[1, 2] = -viewMat[1, 2]; viewMat[2, 2] = -viewMat[2, 2];

        var projMat = Matrix4x4.Ortho(-boundHalfSizeX, boundHalfSizeX, -boundHalfSizeY, boundHalfSizeY, 0, 2f * boundHalfSizeZ);
        projMat = GL.GetGPUProjectionMatrix(projMat, true);

        //m_RainViewProjMatrix = projMat * viewMat;
        m_ViewDir = backCamera.camera.transform.forward;
    }

    class SimulationPassData
    {
        public ComputeBuffer rainDataBuffer;
        public RainSimulationCB simulationCB;
        public ComputeShader simulationCS;
        public Texture2D noiseTex;
    }
    public void Simulation(RenderGraph renderGraph)
    {
        if (paused) return;
        float simDt = Time.deltaTime * timeScale * simulationSpeed;

        float rainDensityDiff = rainDensity - m_PrevRainDensity;
        float maxDiffThisFrame = m_MaxDiffPerSecond * simDt;
        if (rainDensityDiff > maxDiffThisFrame)
            rainDensityDiff = maxDiffThisFrame;
        else if (rainDensityDiff < -maxDiffThisFrame)
            rainDensityDiff = -maxDiffThisFrame;
        m_PrevRainDensity += rainDensityDiff;

        float randX = Random.Range(-1f, 1f);
        m_CurWindEffect.x += randX * m_MaxWindVariance * simDt;
        if (m_CurWindEffect.x > m_MaxWindEffect)
            m_CurWindEffect.x = m_MaxWindEffect;
        else if (m_CurWindEffect.x < -m_MaxWindEffect)
            m_CurWindEffect.x = -m_MaxWindEffect;

        float randY = Random.Range(-1f, 1f);
        m_CurWindEffect.y += randY * m_MaxWindVariance * simDt;
        if (m_CurWindEffect.y > m_MaxWindEffect)
            m_CurWindEffect.y = m_MaxWindEffect;
        else if (m_CurWindEffect.y < -m_MaxWindEffect)
            m_CurWindEffect.y = -m_MaxWindEffect;

        RainSimulationCB simulationCB = new RainSimulationCB()
        {
            _BoundCenter = m_BoundCenter,
            _DeltaTime = simDt,
            _BoundHalfSize = new Vector3(boundHalfSizeX, boundHalfSizeY, boundHalfSizeZ),
            _WindVariation = 0.2f,
            _WindForce = m_CurWindEffect,
            _VerticalSpeed = m_VecticalSpeed,
        };
        using var builder = renderGraph.AddRenderPass<SimulationPassData>("Rain Simulation", out var passData);
        if (m_RainDataBuffer == null)
        {
            m_RainDataBuffer = new ComputeBuffer(k_MaxRainDrops, UnsafeUtility.SizeOf<RainDrop>(), ComputeBufferType.Structured);
            for (int i = 0; i < k_MaxRainDrops; ++i)
            {
                bufferInitData[i].position = new Vector3(0.0f, -1000f, 0.0f);
                bufferInitData[i].velocity = new Vector3(0.0f, m_VecticalSpeed, 0.0f);
            }
            m_RainDataBuffer.SetData(bufferInitData);
        }
        passData.simulationCB = simulationCB;
        passData.rainDataBuffer = m_RainDataBuffer;
        passData.noiseTex = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.textures.noiseTex;
        passData.simulationCS = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.shaders.rainSimulationCS;
        builder.SetRenderFunc<SimulationPassData>((data, context) =>
        {
            context.cmd.SetComputeBufferParam(data.simulationCS, 0, s_RainDataBufferId, m_RainDataBuffer);
            ConstantBuffer.Push(context.cmd, data.simulationCB, data.simulationCS, s_RainSimulationCBId);
            context.cmd.SetGlobalTexture(s_NoiseTexId, data.noiseTex);
            context.cmd.DispatchCompute(data.simulationCS, 0, k_NumRainGroupSize, k_NumRainGroupSize, 1);
        });
    }

    class RainRenderPassData
    {
        public Material material;
        public RainRenderCB rainCB;
        public int totalDrops;
        public ComputeBuffer rainData;
    }
    public void Render(RenderGraph renderGraph)
    {
        if (paused) return;
        using var builder = renderGraph.AddRenderPass<RainRenderPassData>("Rain Render Pass", out var passData);
        passData.rainCB = new RainRenderCB()
        {
            _ViewDir = m_ViewDir,
            _RainAmbientColor = new Color(0.4f, 0.4f, 0.4f, 0.25f),
            _RainScale = m_RainScale
        };
        passData.totalDrops = (int)((float)k_MaxRainDrops * m_PrevRainDensity);
        passData.material = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.materials.rainRenderer;
        passData.rainData = m_RainDataBuffer;
        builder.UseColorBuffer(LightManager.instance.colorTextureHandle, 0);
        builder.UseDepthBuffer(GBuffer.Instance.depthStencilTextureHandle, DepthAccess.Read);
        builder.SetRenderFunc<RainRenderPassData>((data, context) =>
        {
            ConstantBuffer.PushGlobal(context.cmd, data.rainCB, s_RainRenderCBId);
            context.cmd.SetGlobalBuffer(s_RainDataBufferId, data.rainData);
            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Quads, 4 * data.totalDrops);
        });
    }

    //private Material m_HeightMapMat;
    //Material heightMapMat
    //{
    //    get
    //    {
    //        if (m_HeightMapMat == null)
    //            m_HeightMapMat = new Material(Shader.Find("Hidden/HeightMap"));
    //        return m_HeightMapMat;
    //    }
    //}
    
    //class RenderHeightMapPassData
    //{
    //    public Material heightMapMat;
    //    public TextureHandle heightMap;
    //    public Matrix4x4 toHeight;
    //}
    //void RenderHeightMap(RenderGraph renderGraph)
    //{
    //    using var builder = renderGraph.AddRenderPass<RenderHeightMapPassData>("Render HeightMap", out var passData);
    //    if (heightMapRT == null)
    //    {
    //        heightMapRT = RTHandles.Alloc(k_HeightMapSize, k_HeightMapSize, 1, DepthBits.Depth32);
    //    }
    //    passData.heightMapMat = heightMapMat;
    //    passData.toHeight = m_RainViewProjMatrix;
    //    passData.heightMap = builder.WriteTexture(renderGraph.ImportTexture(heightMapRT));
    //    builder.SetRenderFunc<RenderHeightMapPassData>((data, context) =>
    //    {
    //        context.cmd.SetRenderTarget(data.heightMap);
    //        context.cmd.ClearRenderTarget(RTClearFlags.DepthStencil, Color.clear, 1.0f, 0);

    //    });
        
    //}

    public void Dispose()
    {
        CoreUtils.SafeRelease(m_RainDataBuffer);

        m_RainDataBuffer = null;
    }

    private static readonly int s_RainDataBufferId = Shader.PropertyToID("_RainDataBuffer");
    private static readonly int s_RainSimulationCBId = Shader.PropertyToID("RainSimulationCB");
    private static readonly int s_NoiseTexId = Shader.PropertyToID("_NoiseTex");
    private static readonly int s_RainRenderCBId = Shader.PropertyToID("RainRenderCB");
}
