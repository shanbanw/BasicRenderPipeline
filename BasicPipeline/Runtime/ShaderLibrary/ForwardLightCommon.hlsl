#ifndef _FORWARD_LIGHT_COMMON_HLSL_
#define _FORWARD_LIGHT_COMMON_HLSL_

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/ShaderVariables.hlsl"
#include "../ShaderLibrary/PickingSpaceTransforms.hlsl"
#include "../ShaderLibrary/Sampler.hlsl"

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
    float3 positionWS : TEXCOORD2;
};

VS_OUTPUT vert(VS_INPUT input)
{
    VS_OUTPUT output;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.uv = input.uv;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

struct Material
{
    float3 normal;
    float4 diffuseColor;
    float specExp;
    float specIntensity;
};

Material PrepareMaterial(float3 normal, float2 uv)
{
    Material material;

    // Normalize the interpulated vertex normal
    material.normal = SafeNormalize(normal);

    // Sample the texture
    material.diffuseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

    // Copy the specular values from the constant buffer
    material.specExp = _SpecExp;
    material.specIntensity = _SpecIntensity;

    return material;
}



#endif