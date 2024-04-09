#ifndef _COMMON_HLSL_
#define _COMMON_HLSL_

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Sampler.hlsl"
#include "ShaderVariablesGlobals.cs.hlsl"
#include "ShaderVariablesFog.cs.hlsl"

TEXTURE2D(_DepthTex);
TEXTURE2D(_ColorSpecIntensityTex);
TEXTURE2D(_NormalTex);
TEXTURE2D(_SpecPowTex);

static const float2 g_SpecPowerRange = { 10.0, 250.0 };

float ConvertZToLinearDepth(float depth)
{
	#if !(UNITY_REVERSED_Z)
		depth = depth * 2 - 1;
	#endif
	float linearDepth = _PerspectiveValues.z / (depth + _PerspectiveValues.w);
	return linearDepth;
}

float3 CalcViewPos(float2 csPos, float depth)
{
	float3 position;

	position.xy = csPos * _PerspectiveValues.xy * depth;
	position.z = -depth;

	return position;
}

float3 CalcWorldPos(float2 csPos, float depth)
{
	float4 position;

	position.xy = csPos * _PerspectiveValues.xy * depth;
	position.z = -depth;
	position.w = 1.0;

	return mul(_InvViewMatrix, position).xyz;
}

struct SURFACE_DATA
{
	float linearDepth;
	float3 diffuseColor;
	float3 normal;
	float specPow;
	float specIntensity;
};

SURFACE_DATA UnpackGBuffer(float2 uv)
{
	uv = uv * _RTHandleScale.xy;
	SURFACE_DATA surface;

	float depth = SAMPLE_DEPTH_TEXTURE(_DepthTex, sampler_Point_Repeat, uv);
	surface.linearDepth = ConvertZToLinearDepth(depth);
	float4 baseColorSpecIntensity = SAMPLE_TEXTURE2D(_ColorSpecIntensityTex, sampler_Point_Repeat, uv);
	surface.diffuseColor = baseColorSpecIntensity.xyz;
	surface.specIntensity = baseColorSpecIntensity.w;
	float3 normal = SAMPLE_TEXTURE2D(_NormalTex, sampler_Point_Repeat, uv).xyz;
	surface.normal = SafeNormalize(normal * 2.0 - 1.0);
	surface.specPow = SAMPLE_TEXTURE2D(_SpecPowTex, sampler_Point_Repeat, uv).x;

	return surface;
}

SURFACE_DATA UnpackGBuffer_Loc(uint2 location)
{
	SURFACE_DATA surface;

	float depth = LOAD_TEXTURE2D(_DepthTex, location).x;
	surface.linearDepth = ConvertZToLinearDepth(depth);
	float4 baseColorSpecIntensity = LOAD_TEXTURE2D(_ColorSpecIntensityTex, location);
	surface.diffuseColor = baseColorSpecIntensity.xyz;
	surface.specIntensity = baseColorSpecIntensity.w;
	float3 normal = LOAD_TEXTURE2D(_NormalTex, location).xyz;
	surface.normal = SafeNormalize(normal * 2.0 - 1.0);
	surface.specPow = LOAD_TEXTURE2D(_SpecPowTex, location).x;

	return surface;
}

struct Material
{
	float3 normal;
	float4 diffuseColor;
	float specPow;
	float specIntensity;
};

void MaterialFromGBuffer(SURFACE_DATA surface, inout Material material)
{
	material.normal = surface.normal;
	material.diffuseColor.rgb = surface.diffuseColor;
	material.diffuseColor.a = 1.0;
	material.specPow = g_SpecPowerRange.x + g_SpecPowerRange.y * surface.specPow;
	material.specIntensity = surface.specIntensity;
}

float3 ApplyFog(float3 originalColor, float eyePosY, float3 eyeToPixel, float3 toLight)
{
	// the distance light ray traveled in the fog
	float pixelDist = length(eyeToPixel);
	float3 eyeToPixelNorm = eyeToPixel / pixelDist;

	float fogDist = max(pixelDist - _FogStartDepth, 0.0);
	float fogHeightDensityAtViewer = exp(-_FogHeightFalloff * eyePosY);
	float fogDistIntensity = fogDist * fogHeightDensityAtViewer;

	float eyeToPixelY = eyeToPixel.y * (fogDist / pixelDist);
	float t = _FogHeightFalloff * eyeToPixelY;
	const float thresholdT = 0.01;
	float fogHeightIntensity = abs(t) > thresholdT ? (1.0 - exp(-t)) / t : 1.0;

	float fogFinalFactor = exp(-_FogGlobalDensity * fogDistIntensity * fogHeightIntensity);

	float highlightFactor = saturate(dot(eyeToPixelNorm, toLight));
	highlightFactor = pow(highlightFactor, 8.0);
	float3 fogFinalColor = lerp(_FogColor.rgb, _FogHighlightColor.rgb, highlightFactor);

	return lerp(fogFinalColor, originalColor, fogFinalFactor);
}

#endif