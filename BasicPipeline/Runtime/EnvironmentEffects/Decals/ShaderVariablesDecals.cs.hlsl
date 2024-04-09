//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef SHADERVARIABLESDECALS_CS_HLSL
#define SHADERVARIABLESDECALS_CS_HLSL
// Generated from VertexAttributeCB
// PackingRules = Exact
CBUFFER_START(VertexAttributeCB)
    float4x4 _ObjectToWorld;
    int _VertexBufferStride;
    int _PositionAttributeOffset;
    int _NormalAttributeOffset;
    int _padAttr;
CBUFFER_END

// Generated from DecalVertex
// PackingRules = Exact
struct DecalVertex
{
    float3 position;
    float3 normal;
    float2 uv;
};

// Generated from DecalVertexLayout
// PackingRules = Exact
struct DecalVertexLayout
{
    float3 position0;
    float3 normal0;
    float2 uv0;
    float3 position1;
    float3 normal1;
    float2 uv1;
    float3 position2;
    float3 normal2;
    float2 uv2;
};

// Generated from DecalGenCB
// PackingRules = Exact
CBUFFER_START(DecalGenCB)
    float4 _ClipPlane[6];
    float2 _DecalSize;
    float2 _padDecal1;
    float3 _HitNormal;
    float _padDecal2;
CBUFFER_END


#endif
