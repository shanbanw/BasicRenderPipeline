#ifndef _GBUFFER_PASS_HLSL_
#define _GBUFFER_PASS_HLSL_

#include "Common.hlsl"
#include "ShaderVariables.hlsl"
#include "PickingSpaceTransforms.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
CBUFFER_END

struct VS_INPUT
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct VS_OUTPUT
{
    float4 positionCS : SV_POSITION;
    float scale : TEXCOORD0;
};

VS_OUTPUT RenderEmissiveVS(VS_INPUT input)
{
    VS_OUTPUT output;

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    
    float scale = (float)TransformWorldToViewDir((real3)TransformObjectToWorldDir(input.normalOS)).z;
    output.scale = saturate(scale + 0.5);

    return output;
}

float4 RenderEmissivePS(VS_OUTPUT input) : SV_TARGET0
{
    return float4(_Color.rgb * input.scale, 1.0);
}

#endif