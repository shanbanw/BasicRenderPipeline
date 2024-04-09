using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct VertexAttributeCB
{
    public Matrix4x4 _ObjectToWorld;
    public int _VertexBufferStride;
    public int _PositionAttributeOffset;
    public int _NormalAttributeOffset;
    public int _padAttr;
}

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct DecalGenCB
{
    [HLSLArray(6, typeof(Vector4))]
    public fixed float _ClipPlane[6 * 4];
    public Vector2 _DecalSize;
    public Vector2 _padDecal1;
    public Vector3 _HitNormal;
    public float _padDecal2;
}

[GenerateHLSL(needAccessors = false, generateCBuffer = false)]
unsafe struct DecalVertexLayout
{
    public Vector3 position0;
    public Vector3 normal0;
    public Vector2 uv0;
    public Vector3 position1;
    public Vector3 normal1;
    public Vector2 uv1;
    public Vector3 position2;
    public Vector3 normal2;
    public Vector2 uv2;
}

[GenerateHLSL(needAccessors = false, generateCBuffer = false)]
unsafe struct DecalVertex
{
    public Vector3 position;
    public Vector3 normal;
    public Vector2 uv;
}

