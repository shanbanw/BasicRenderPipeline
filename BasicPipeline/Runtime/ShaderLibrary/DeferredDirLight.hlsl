#ifndef _DEFERRED_DIR_LIGHT_
#define _DEFERRED_DIR_LIGHT_

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/ShaderVariablesLights.cs.hlsl"

TEXTURE2D_ARRAY(_CascadeShadowmapTexture);
TEXTURE2D(_AOTexture);

struct VS_OUTPUT
{
    float4 positionCS : SV_POSITION;
    float2 csPos : TEXCOORD0;
    float2 uv : TEXCOORD1;
};

VS_OUTPUT vert(uint vertexId : SV_VertexID)
{
    VS_OUTPUT output;

    output.positionCS = GetFullScreenTriangleVertexPosition(vertexId);
    output.csPos = output.positionCS.xy;
    output.uv = GetFullScreenTriangleTexCoord(vertexId);

    return output;
}

float3 CalcAmbient(float3 normal, float3 color)
{
    float up = normal.y * 0.5 + 0.5;
    float3 ambient = _AmbientLower + up * _AmbientRange;
    return ambient * color;
}

float CascadedShadow(float3 position)
{
    float4 posShadowSpace = mul(_ToCascadeShadowSpace, float4(position, 1.0));

    float4 posCascadeSpaceX = (posShadowSpace.xxxx + _ToCascadeOffsetX) * _ToCascadeScale;
    float4 posCascadeSpaceY = (posShadowSpace.yyyy + _ToCascadeOffsetY) * _ToCascadeScale;

    float4 inCascadeX = abs(posCascadeSpaceX) <= 1.0;
    float4 inCascadeY = abs(posCascadeSpaceY) <= 1.0;
    float4 inCascade = inCascadeX * inCascadeY;

    float4 bestCascadeMask = inCascade;
	bestCascadeMask.yzw = (1.0 - bestCascadeMask.x) * bestCascadeMask.yzw;
	bestCascadeMask.zw = (1.0 - bestCascadeMask.y) * bestCascadeMask.zw;
	bestCascadeMask.w = (1.0 - bestCascadeMask.z) * bestCascadeMask.w;

    float bestCascade = dot(bestCascadeMask, float4(0.0, 1.0, 2.0, 3.0));

    float3 uvd;
	uvd.x = dot(posCascadeSpaceX, bestCascadeMask);
	uvd.y = dot(posCascadeSpaceY, bestCascadeMask);
	uvd.z = posShadowSpace.z;

    uvd.xy = uvd.xy * 0.5 + 0.5;
    #if UNITY_UV_STARTS_AT_TOP
        uvd.y = 1.0 - uvd.y;
    #endif

    float shadow = SAMPLE_TEXTURE2D_ARRAY_SHADOW(_CascadeShadowmapTexture, sampler_Linear_Clamp_Compare, uvd, bestCascade);

    // set the shadow to one (fully lit) for positions with no cascade coverage
	shadow = saturate(shadow + 1.0 - any(bestCascadeMask));

    return shadow;
}


float3 CalcDirectional(float3 position, Material material)
{
    // Phong diffuse
    float NDotL = dot(_DirToLight, material.normal);
    float3 finalColor = _DirectionalColor.rgb * saturate(NDotL);

    // Blinn specular
    float3 toEye = _WorldSpaceCameraPos.xyz - position;
    toEye = SafeNormalize(toEye);
    float3 halfWay = SafeNormalize(toEye + _DirToLight);
    float NDotH = saturate(dot(halfWay, material.normal));
    finalColor += _DirectionalColor.rgb * pow(NDotH, material.specPow) * material.specIntensity;

    float shadowAttn = 1.0;
    if (_CascadeShadowmapIndex >= 0)
        shadowAttn = CascadedShadow(position);

    return finalColor * material.diffuseColor.rgb * shadowAttn;
}

float4 frag(VS_OUTPUT input) : SV_TARGET0
{
    float2 location = input.positionCS.xy;
    SURFACE_DATA surface = UnpackGBuffer_Loc(location);
    Material mat;
    MaterialFromGBuffer(surface, mat);

    float3 position = CalcWorldPos(input.csPos, surface.linearDepth);

    float ao = SAMPLE_TEXTURE2D(_AOTexture, sampler_Linear_Clamp, input.uv * _RTHandleScale.xy).r;

    float3 finalColor = CalcAmbient(mat.normal, mat.diffuseColor.rgb) * ao;
    finalColor += CalcDirectional(position, mat);

    if (FOG_ON)
    {
        float3 eyeToPixel = position - _WorldSpaceCameraPos.xyz;
        finalColor = ApplyFog(finalColor, _WorldSpaceCameraPos.y, eyeToPixel, _DirToLight);
    }

    return float4(finalColor, 1.0);
}

#endif