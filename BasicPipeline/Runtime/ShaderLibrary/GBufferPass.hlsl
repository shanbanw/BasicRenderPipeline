#ifndef _GBUFFER_PASS_HLSL_
#define _GBUFFER_PASS_HLSL_

#include "Common.hlsl"
#include "ShaderVariables.hlsl"
#include "PickingSpaceTransforms.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float _SpecExp;
    float _SpecIntensity;
CBUFFER_END

struct VS_INPUT
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
};
        
struct VS_OUTPUT
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
};

VS_OUTPUT vert(VS_INPUT input)
{
    VS_OUTPUT output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

struct PS_GBUFFER_OUT
{
    float4 colorSpecIntensity : SV_TARGET0;
    float4 normal : SV_TARGET1;
    float4 specPow : SV_TARGET2;

};

PS_GBUFFER_OUT PackGBuffer(float3 baseColor, float3 normal, float specIntensity, float specPower)
{
    PS_GBUFFER_OUT gbuffer;
    
    // Normalize the specular power
    float specPowerNorm = max(0.0001, (specPower - g_SpecPowerRange.x) / g_SpecPowerRange.y);

    gbuffer.colorSpecIntensity = float4(baseColor, specIntensity);
    gbuffer.normal = float4(normal * 0.5 + 0.5, 0.0);
    gbuffer.specPow = float4(specPowerNorm, 0.0, 0.0, 0.0);

    return gbuffer;
}

PS_GBUFFER_OUT GBufferFrag(VS_OUTPUT input)
{
    float2 uv = TRANSFORM_TEX(input.uv, _MainTex);
    float3 diffuseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;

    return PackGBuffer(diffuseColor, SafeNormalize(input.normalWS), _SpecIntensity, _SpecExp);
}

#endif
