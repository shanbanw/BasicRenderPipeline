#ifndef _DEFERRED_POINT_LIGHT_
#define _DEFERRED_POINT_LIGHT_

#include "Common.hlsl"
#include "ShaderVariablesPointLights.cs.hlsl"
#include "ShaderVariables.hlsl"
#include "PickingSpaceTransforms.hlsl"

TEXTURECUBE_ARRAY(_PointShadowmapTexture);

float4 PointLightVS() : SV_POSITION
{
	return float4(0.0, 0.0, 0.0, 1.0);
}

// Hull Shader
struct HS_CONSTANT_DATA_OUTPUT
{
	float Edges[4] : SV_TESSFACTOR;
	float Inside[2] : SV_INSIDETESSFACTOR;
};

HS_CONSTANT_DATA_OUTPUT PointLightConstantHS()
{
	HS_CONSTANT_DATA_OUTPUT output;

	float tessFactor = 18.0;
	output.Edges[0] = output.Edges[1] = output.Edges[2] = output.Edges[3] = tessFactor;
	output.Inside[0] = output.Inside[1] = tessFactor;

	return output;
}

struct HS_OUTPUT
{
	float4 HemiDir : POSITION;
};

static const float3 HemilDir[2] = 
{
	float3(1.0, 1.0, 1.0),
	float3(-1.0, 1.0, -1.0)
};

[domain("quad")]
[partitioning("integer")]
[outputtopology("triangle_cw")] //Unity default front face is clock-wise
[outputcontrolpoints(4)]
[patchconstantfunc("PointLightConstantHS")]
HS_OUTPUT PointLightHS(uint PatchID : SV_PRIMITIVEID)
{
	HS_OUTPUT output;

	output.HemiDir = float4(HemilDir[PatchID], 1.0);

	return output;
}

// Domain Shader
struct DS_OUTPUT
{
	float4 positionCS : SV_POSITION;
	noperspective float2 csPos : TEXCOORD0;
};

[domain("quad")]
DS_OUTPUT PointLightDS(HS_CONSTANT_DATA_OUTPUT input, float2 uv : SV_DOMAINLOCATION, const OutputPatch<HS_OUTPUT, 4> quad)
{
	// d3d y轴从上向下
	float2 posClipSpace = uv * float2(2.0, -2.0) + float2(-1.0, 1.0);

	float2 posClipSpaceAbs = abs(posClipSpace.xy);
	float maxLen = max(posClipSpaceAbs.x, posClipSpaceAbs.y);

	float3 normDir = normalize(float3(posClipSpace.xy, (maxLen - 1.0)) * quad[0].HemiDir.xyz);
	//float4 posLS = float4(normDir.xyz, 1.0);
	float3 posWS = normDir * ((1.0 / _PointRangeRcp)) + _PointPosition;

	DS_OUTPUT output;
	output.positionCS = TransformWorldToHClip(posWS);
	output.csPos = output.positionCS.xy / output.positionCS.w;

	return output;
}

float PointShadowPCF(float3 toPixel)
{
	float3 toPixelAbs = abs(toPixel);
	float z = max(toPixelAbs.x, max(toPixelAbs.y, toPixelAbs.z));
	float depth = -_PointPerspectiveValues.x + _PointPerspectiveValues.y / z;

	if (abs(toPixel.y) >= abs(toPixel.x) && abs(toPixel.y) >= abs(toPixel.z))
    {
        toPixel.z *= -1;
    }
    else
    {
        toPixel.y *= -1;
    }

	return SAMPLE_TEXTURECUBE_ARRAY_SHADOW(_PointShadowmapTexture, sampler_Linear_Clamp_Compare, float4(toPixel, depth), _PointShadowmapIndex);
}

// Pixel Shader
float3 CalcPoint(float3 position, Material material, bool bUseShadow)
{
	float3 toLight = _PointPosition.xyz - position;
	float3 toEye = _WorldSpaceCameraPos.xyz - position;
	float distanceToLight = length(toLight);

	// Phong diffuse
	toLight /= distanceToLight;
	float NDotL = saturate(dot(toLight, material.normal));
	float3 finalColor = material.diffuseColor.rgb * NDotL;

	// Blinn specular
	toEye = normalize(toEye);
	float3 H = normalize(toEye + toLight);
	float NDotH = saturate(dot(H, material.normal));
	finalColor += pow(NDotH, material.specPow) * material.specIntensity;

	// Attenuation
	float distanceToLightNorm = 1.0 - saturate(distanceToLight * _PointRangeRcp);
	float attn = distanceToLightNorm * distanceToLightNorm;

	float shadowAttn = 1.0;
    if (_PointShadowmapIndex >= 0)
        shadowAttn = PointShadowPCF(position - _PointPosition);

	finalColor *= _PointColor.rgb * attn * shadowAttn;
	return finalColor;
}

float4 PointLightCommonPS(DS_OUTPUT input, bool bUseShadow) : SV_TARGET0
{
	SURFACE_DATA surface = UnpackGBuffer_Loc(input.positionCS.xy);

	Material material;
	MaterialFromGBuffer(surface, material);

	float3 position = CalcWorldPos(input.csPos, surface.linearDepth);

	float3 finalColor = CalcPoint(position, material, bUseShadow);

	return float4(finalColor, 1.0);
}

float4 PointLightPS(DS_OUTPUT input) : SV_TARGET0
{
	return PointLightCommonPS(input, true);
}

#endif