//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef SHADERVARIABLESRAIN_CS_HLSL
#define SHADERVARIABLESRAIN_CS_HLSL
// Generated from RainSimulationCB
// PackingRules = Exact
CBUFFER_START(RainSimulationCB)
    float4x4 _ToHeight;
    float3 _BoundCenter;
    float _DeltaTime;
    float3 _BoundHalfSize;
    float _WindVariation;
    float2 _WindForce;
    float _VerticalSpeed;
    float _HeightMapSize;
CBUFFER_END

// Generated from RainDrop
// PackingRules = Exact
struct RainDrop
{
    float3 position;
    float3 velocity;
};

// Generated from RainRenderCB
// PackingRules = Exact
CBUFFER_START(RainRenderCB)
    float3 _ViewDir;
    float _RainScale;
    float4 _RainAmbientColor; // x: r y: g z: b w: a 
CBUFFER_END


#endif
