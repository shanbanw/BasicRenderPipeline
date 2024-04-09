#ifndef _DEFERRED_SPOT_LIGHT_
#define _DEFERRED_SPOT_LIGHT_

#include "Common.hlsl"
#include "ShaderVariablesSpotLights.cs.hlsl"
#include "ShaderVariables.hlsl"
#include "PickingSpaceTransforms.hlsl"

float4 SpotLightVS() : SV_POSITION
{
	return float4(0.0, 0.0, 0.0, 1.0);
}

struct HS_CONSTANT_DATA_OUTPUT
{
	float Edges[4] : SV_TESSFACTOR;
	float Inside[2] : SV_INSIDETESSFACTOR;
};

HS_CONSTANT_DATA_OUTPUT SpotLightConstantHS()
{
	HS_CONSTANT_DATA_OUTPUT output;

	float tessFactor = 36.0;
	output.Edges[0] = output.Edges[1] = output.Edges[2] = output.Edges[3] = tessFactor;
	output.Inside[0] = output.Inside[1] = tessFactor;

	return output;
}

struct HS_OUTPUT
{
	float4 position : POSITION;
};

[domain("quad")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(4)]
[patchconstantfunc("SpotLightConstantHS")]
HS_OUTPUT SpotLightHS()
{
	HS_OUTPUT output;

	output.position = float4(0.0, 0.0, 0.0, 1.0);

	return output;
}

struct DS_OUTPUT
{
	float4 positionCS : SV_POSITION;
	noperspective float2 csPos : TEXCOORD0;
};

#define CylinderPortion 0.2
#define ExpendAmout (1.0 + CylinderPortion)

[domain("quad")]
DS_OUTPUT SpotLightDS(HS_CONSTANT_DATA_OUTPUT input, float2 uv : SV_DOMAINLOCATION, const OutputPatch<HS_OUTPUT, 4> quad)
{
	float2 posClipSpace = uv.xy * 2.0 - 1.0;
	//float2 posClipSpace = uv.xy * float2(2.0, -2.0) + float2(-1.0, 1.0);

	float2 posClipSpaceAbs = abs(posClipSpace);
	float maxLen = max(posClipSpaceAbs.x, posClipSpaceAbs.y);

	float2 posExpendAbs = saturate(posClipSpaceAbs * ExpendAmout);
	float maxLenExpend = max(posExpendAbs.x, posExpendAbs.y);
	float2 posExpend = sign(posClipSpace) * posExpendAbs;

	float3 halfSpherePos = normalize(float3(posExpend, 1.0 - maxLenExpend));

	// Scale the sphere to the size of the cones rounded base
	halfSpherePos = normalize(float3(halfSpherePos.xy * _SpotSinOuterAngle, _SpotCosOuterAngle));

	float cylinderOffset = saturate((maxLen * ExpendAmout - 1.0) / CylinderPortion);

	//when offset > 0, _SpotCosOuterAngle = halfSpherePos.z
	float4 posLS = float4(halfSpherePos.xy * (1.0 - cylinderOffset), halfSpherePos.z - cylinderOffset * _SpotCosOuterAngle, 1.0);
	float4 posWS = mul(_SpotLightMatrix, posLS);

	DS_OUTPUT output;
	output.positionCS = TransformWorldToHClip(posWS.xyz);
	output.csPos = output.positionCS.xy / output.positionCS.w;
	
	return output;
}

TEXTURE2D_ARRAY(_SpotShadowmapTexture);

float SpotShadowPCF(float3 position)
{
	float4 posShadowSpace = mul(_ToShadowMap, float4(position, 1.0));

	float3 uvd = posShadowSpace.xyz / posShadowSpace.w;
	uvd.xy = uvd.xy * 0.5 + 0.5;

	return SAMPLE_TEXTURE2D_ARRAY_SHADOW(_SpotShadowmapTexture, sampler_Linear_Clamp_Compare, uvd, _SpotShadowmapIndex);
}

float3 CalcSpot(float3 position, Material material)
{
	float3 toLight = _SpotPosition - position;
	float3 toEye = _WorldSpaceCameraPos.xyz - position;
	float distanceToLight = length(toLight);

	toLight /= distanceToLight;
	float NDotL = saturate(dot(toLight, material.normal));
	float3 finalColor = material.diffuseColor.rgb * NDotL;

	toEye = SafeNormalize(toEye);
	float3 H = SafeNormalize(toEye + toLight);
	float NDotH = saturate(dot(material.normal, H));
	finalColor += material.diffuseColor.rgb * pow(NDotH, material.specPow) * material.specIntensity;

	float cosAng = dot(_SpotDirection, toLight);
	float conAttn = saturate((cosAng - _SpotCosOuterAngle) * _SpotCosAttnRangeRcp);
	conAttn *= conAttn;

	float distanceToLightNorm = 1.0 - saturate(distanceToLight * _SpotRangeRcp);
	float distAttn = distanceToLightNorm * distanceToLightNorm;

	float shadowAttn = 1.0;
	if (_SpotShadowmapIndex >= 0)
		shadowAttn = SpotShadowPCF(position);

	finalColor *= _SpotColor.rgb * conAttn * distAttn * shadowAttn;

	return finalColor;
}

float4 SpotLightPS(DS_OUTPUT input) : SV_TARGET0
{
	SURFACE_DATA surface = UnpackGBuffer_Loc(input.positionCS.xy);

	Material material;
	MaterialFromGBuffer(surface, material);

	float3 position = CalcWorldPos(input.csPos, surface.linearDepth);

	float3 finalColor = CalcSpot(position, material);

	return float4(finalColor, 1.0);
}


#endif