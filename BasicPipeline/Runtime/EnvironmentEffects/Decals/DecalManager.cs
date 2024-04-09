using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.UniversalDelegates;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class DecalManager
{
    private DecalManager()
    {
        m_DecalStride = UnsafeUtility.SizeOf<DecalVertexLayout>();
        m_CounterBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        m_DecalBuffer = new ComputeBuffer(m_DecalMaxTriangles, UnsafeUtility.SizeOf<DecalVertexLayout>(), ComputeBufferType.Append);
    }
    public static DecalManager instance = new DecalManager();

    const uint k_MaxDecalCount = 10;
    private int m_DecalMaxTriangles = 20;
    private float m_DecalSize = 0.75f;
    private int m_DecalStride;

    public void AddDecal(Vector3 hitPos, Vector3 hitNormal, Matrix4x4 objectToWorld, Mesh mesh)
    {
        bool tooClose = false;
        foreach(var decalEntry in m_DecalList)
        {
            Vector3 hitToEntry = decalEntry.hitPos - hitPos;
            if(hitToEntry.sqrMagnitude < m_DecalSize * m_DecalSize)
            {
                tooClose = true;
                break;
            }
        }
        if(!tooClose)
        {
            foreach(var decalEntry in m_DecalAddList) 
            {
                Vector3 hitToEntry = decalEntry.hitPos - hitPos;
                if(hitToEntry.sqrMagnitude < m_DecalSize / m_DecalSize)
                {
                    tooClose = true;
                    break;
                }
            }
        }

        if(!tooClose)
        {
            DecalAddEntry decalAddEntry = new DecalAddEntry();
            decalAddEntry.hitPos = hitPos;
            decalAddEntry.hitNormal = hitNormal;
            decalAddEntry.objectToWorld = objectToWorld;
            decalAddEntry.mesh = mesh;
            decalAddEntry.started = false;
            m_DecalAddList.Enqueue(decalAddEntry);
        }
    }
    private ComputeBuffer m_DecalBuffer;
    class DecalPreRenderData
    {
        public ComputeShader decalGenCS;
        public DecalGenCB decalGenCB;
        public VertexAttributeCB vertexAttributeCB;
        public GraphicsBuffer vertexBuffer;
        public GraphicsBuffer indexBuffer;
        public ComputeBuffer decalBuffer;
        public ComputeBuffer decalVertexBuffer;
        public ComputeBuffer counterBuffer;
        public uint indexCount;
    }
    private int[] dData = new int[3];
    public void PreRender(RenderGraph renderGraph)
    {
        //if (m_DecalBufferList.Count != 0)
        //{
        //    var buffer = m_DecalBufferList[0];
        //    DecalVertex[] layout = new DecalVertex[m_DecalMaxTriangles * 3];
        //    buffer.GetData(layout);
        //    DecalVertexLayout[] decalVertexLayout = new DecalVertexLayout[m_DecalMaxTriangles];
        //    m_DecalBuffer.GetData(decalVertexLayout);
        //    for (int i = 0; i < m_DecalMaxTriangles; i ++)
        //    {
        //        Debug.Log($"{layout[i*3+0].position}   {layout[i * 3 + 0].normal}  {layout[i * 3 + 0].uv}");
        //        Debug.Log($"{decalVertexLayout[i].position0}  {decalVertexLayout[i].normal0} {decalVertexLayout[i].uv0}");
        //        //Debug.Log($"{v.position1}   {v.normal1}  {v.uv1}");
        //        //Debug.Log($"{v.position2}   {v.normal2}  {v.uv2}");
        //    }
            
        //    Debug.Log("BufferEnd");
        //}
        if (m_DecalAddList.Count == 0) return;
        DecalAddEntry addEntry = m_DecalAddList.Dequeue();
        DecalEntry entry = new DecalEntry() 
        {
            hitPos = addEntry.hitPos,
        };
        m_DecalList.Add(entry);
        using var builder = renderGraph.AddRenderPass<DecalPreRenderData>("Decal PreRender", out var passData);
        PrepareGenConstantBuffer(addEntry.hitPos, addEntry.hitNormal, out passData.decalGenCB);
        passData.decalGenCS = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.shaders.decalGenCS;
        passData.vertexAttributeCB = new VertexAttributeCB();
        passData.vertexAttributeCB._ObjectToWorld = addEntry.objectToWorld;
        passData.vertexAttributeCB._VertexBufferStride = addEntry.mesh.GetVertexBufferStride(0);
        passData.vertexAttributeCB._PositionAttributeOffset = addEntry.mesh.GetVertexAttributeOffset(VertexAttribute.Position);
        passData.vertexAttributeCB._NormalAttributeOffset = addEntry.mesh.GetVertexAttributeOffset(VertexAttribute.Normal);
        passData.indexCount = addEntry.mesh.GetIndexCount(0);
        addEntry.mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        passData.vertexBuffer = addEntry.mesh.GetVertexBuffer(0);
        addEntry.mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        passData.indexBuffer = addEntry.mesh.GetIndexBuffer();
        passData.decalBuffer = m_DecalBuffer;
        passData.decalVertexBuffer = new ComputeBuffer(m_DecalMaxTriangles * 3, UnsafeUtility.SizeOf<DecalVertex>(), ComputeBufferType.Counter);
        m_DecalBufferList.Add(passData.decalVertexBuffer);
        passData.counterBuffer = m_DispatchIndirectBuffer;
        passData.counterBuffer.SetData(s_DispatchInit);
        builder.SetRenderFunc<DecalPreRenderData>((data, context) =>
        {
            ConstantBuffer.Push(context.cmd, data.decalGenCB, data.decalGenCS, s_DecalGenCBId);
            ConstantBuffer.Push(context.cmd, data.vertexAttributeCB, data.decalGenCS, s_VertexAttributeCBId);
            context.cmd.SetComputeBufferParam(data.decalGenCS, 0, s_IndexBufferId, data.indexBuffer);
            context.cmd.SetComputeBufferParam(data.decalGenCS, 0, s_VertexBufferId, data.vertexBuffer);
            context.cmd.SetBufferCounterValue(data.decalBuffer, 0);
            context.cmd.SetComputeBufferParam(data.decalGenCS, 0, s_DecalBufferId, data.decalBuffer);
            context.cmd.DispatchCompute(data.decalGenCS, 0, (int)data.indexCount / 3, 1, 1);

            context.cmd.CopyCounterValue(data.decalBuffer, data.counterBuffer, 0);
            context.cmd.SetBufferCounterValue(data.decalVertexBuffer, 0);
            context.cmd.SetComputeBufferParam(data.decalGenCS, 1, s_DecalBufferSRVId, data.decalBuffer);
            context.cmd.SetComputeBufferParam(data.decalGenCS, 1, s_DecalVertexBufferId, data.decalVertexBuffer);
            context.cmd.DispatchCompute(data.decalGenCS, 1, data.counterBuffer, 0);

            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();

            data.vertexBuffer.Dispose();
            data.indexBuffer.Dispose();
        });
    }

    private void PrepareGenConstantBuffer(Vector3 pos, Vector3 normal, out DecalGenCB decalGenCB)
    {
        normal = normal.normalized;
        Vector3 up = Vector3.up;
        if (Mathf.Abs(normal.y) > 0.95f)
        {
            up.x = 1f;
            up.z = 0f;
        }
        Vector3 right = Vector3.Cross(up, normal).normalized;
        up = Vector3.Cross(normal, right).normalized;
        decalGenCB = new DecalGenCB();
        unsafe
        {
            // +X
            decalGenCB._ClipPlane[0] = right.x;
            decalGenCB._ClipPlane[1] = right.y;
            decalGenCB._ClipPlane[2] = right.z;
            decalGenCB._ClipPlane[3] = 0.5f * m_DecalSize - Vector3.Dot(pos, right);
            // -X
            decalGenCB._ClipPlane[4] = -right.x;
            decalGenCB._ClipPlane[5] = -right.y;
            decalGenCB._ClipPlane[6] = -right.z;
            decalGenCB._ClipPlane[7] = 0.5f * m_DecalSize - Vector3.Dot(pos, -right);
            // -Y
            decalGenCB._ClipPlane[8] = up.x;
            decalGenCB._ClipPlane[9] = up.y;
            decalGenCB._ClipPlane[10] = up.z;
            decalGenCB._ClipPlane[11] = 0.5f * m_DecalSize - Vector3.Dot(pos, up);
            // +Y
            decalGenCB._ClipPlane[12] = -up.x;
            decalGenCB._ClipPlane[13] = -up.y;
            decalGenCB._ClipPlane[14] = -up.z;
            decalGenCB._ClipPlane[15] = 0.5f * m_DecalSize - Vector3.Dot(pos, -up);
            // +Z
            decalGenCB._ClipPlane[16] = normal.x;
            decalGenCB._ClipPlane[17] = normal.y;
            decalGenCB._ClipPlane[18] = normal.z;
            decalGenCB._ClipPlane[19] = 5f - Vector3.Dot(pos, normal);
            // -Z
            decalGenCB._ClipPlane[20] = -normal.x;
            decalGenCB._ClipPlane[21] = -normal.y;
            decalGenCB._ClipPlane[22] = -normal.z;
            decalGenCB._ClipPlane[23] = 5f - Vector3.Dot(pos, -normal);

            decalGenCB._DecalSize = new Vector2(m_DecalSize, m_DecalSize);

            decalGenCB._HitNormal = normal;
        }
        
    }


    class DecalRenderData
    {
        public List<ComputeBuffer> buffers;
        public Material decalMat;
    }

    public void Render(RenderGraph renderGraph)
    {
        
        if (m_DecalBufferList.Count == 0) return;
        m_CounterBuffer.SetData(s_DrawBufferInit);
        using var builder = renderGraph.AddRenderPass<DecalRenderData>("Decal Render", out var passData);
        passData.decalMat = BasicRenderPipelineGlobalSettings.instance.renderPipelineResources.materials.decalRender;
        passData.buffers = m_DecalBufferList;
        builder.UseColorBuffer(GBuffer.Instance.basicColorTextureHandle, 0);
        builder.UseColorBuffer(GBuffer.Instance.normalTextureHandle, 1);
        builder.UseColorBuffer(GBuffer.Instance.specPowerTextureHandle, 2);
        builder.UseDepthBuffer(GBuffer.Instance.depthStencilTextureHandle, DepthAccess.Write);
        builder.SetRenderFunc<DecalRenderData>((data, context) =>
        {
            foreach(var buffer in data.buffers)
            {
                instance.RenderDecal(buffer, data.decalMat, context);
            }
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        });
    }
    private static readonly List<int> s_DrawBufferInit = new() { 0, 1, 0, 0 };
    private static readonly List<int> s_DispatchInit = new () { 0, 1, 1 };
    private ComputeBuffer m_CounterBuffer;
    private ComputeBuffer m_DispatchIndirectBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
    private void RenderDecal(ComputeBuffer decalBuffer, Material decalMat, RenderGraphContext context)
    {
        context.cmd.CopyCounterValue(decalBuffer, m_CounterBuffer, 0);

        context.cmd.SetGlobalBuffer(s_DecalVertexBufferId, decalBuffer);
        context.cmd.DrawProceduralIndirect(Matrix4x4.identity, decalMat, 0, MeshTopology.Triangles, m_CounterBuffer);
        //context.cmd.DrawProcedural(Matrix4x4.identity, decalMat, 0, MeshTopology.Triangles, 12);
    }
    int[] zdata = new int[4];


    struct DecalAddEntry
    {
        public Vector3 hitPos;
        public Vector3 hitNormal;
        public Matrix4x4 objectToWorld;
        public Mesh mesh;
        public bool started;
    }
    Queue<DecalAddEntry> m_DecalAddList = new Queue<DecalAddEntry>();

    struct DecalEntry
    {
        public Vector3 hitPos;
        public uint vertCount;
    }
    List<DecalEntry> m_DecalList = new List<DecalEntry>();

    List<ComputeBuffer> m_DecalBufferList = new List<ComputeBuffer>();

    public void Dispose()
    {
        foreach(var buffer in m_DecalBufferList)
        {
            CoreUtils.SafeRelease(buffer);
        }
        m_DecalBufferList.Clear();
        m_DecalAddList.Clear();
        m_DecalList.Clear();
        CoreUtils.SafeRelease(m_CounterBuffer);
        CoreUtils.SafeRelease(m_DecalBuffer);
        CoreUtils.SafeRelease(m_DispatchIndirectBuffer);
    }

    private static readonly int s_IndexBufferId = Shader.PropertyToID("_IndexBuffer");
    private static readonly int s_VertexBufferId = Shader.PropertyToID("_VertexBuffer");
    private static readonly int s_DecalBufferId = Shader.PropertyToID("_DecalBuffer");
    private static readonly int s_DecalBufferSRVId = Shader.PropertyToID("_DecalBufferSRV");
    private static readonly int s_VertexAttributeCBId = Shader.PropertyToID("VertexAttributeCB");
    private static readonly int s_DecalGenCBId = Shader.PropertyToID("DecalGenCB");
    private static readonly int s_DecalVertexBufferId = Shader.PropertyToID("_DecalVertexBuffer");
}
