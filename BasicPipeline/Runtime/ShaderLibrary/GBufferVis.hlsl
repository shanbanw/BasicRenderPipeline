#ifndef _GBUFFER_VIS_HLSL_
#define _GBUFFER_VIS_HLSL_

#include "Common.hlsl"

static const float2 arrOffsets[4] = 
{
	float2(-0.75, -0.75),
	float2(-0.25, -0.75),
	float2(0.25, -0.75),
	float2(0.75, -0.75),
};

static const float2 arrBasePos[6] = 
{
	float2(1.0, 1.0),
	float2(1.0, -1.0),
	float2(-1.0, 1.0),
	float2(1.0, -1.0),
	float2(-1.0, 1.0),
	float2(-1.0, -1.0),
};

static const float2 arrUV[6] = 
{
	float2(1.0, 1.0),
	float2(1.0, 0.0),
	float2(0.0, 1.0),
	float2(1.0, 0.0),
	float2(0.0, 1.0),
	float2(0.0, 0.0),
};

static const float4 arrMask[4] = 
{
	float4(1.0, 0.0, 0.0, 0.0),
	float4(0.0, 1.0, 0.0, 0.0),
	float4(0.0, 0.0, 1.0, 0.0),
	float4(0.0, 0.0, 0.0, 1.0),
};

struct VS_OUTPUT
{
	float4 positionCS : SV_POSITION;
	float2 uv : TEXCOORD0;
	float4 sampMask : TEXCOORD1;
};

VS_OUTPUT GBufferVisVS(uint vertexId : SV_VertexID ) 
{
	VS_OUTPUT output;
	
	//output.positionCS = float4(arrBasePos[vertexId % 6].xy * 0.2 + arrOffsets[vertexId / 6], 0.0, 1.0);
	//output.uv = arrUV[vertexId % 6];

	float4 posCS = GetQuadVertexPosition(vertexId % 4);
	posCS.y = 1 - posCS.y;
	output.positionCS = float4(posCS.xy * 0.4 - 0.2 + arrOffsets[vertexId / 4], posCS.zw);
	float2 uv = GetQuadTexCoord(vertexId % 4);
	
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = 1.0 - uv.y;
	#endif
	output.uv = uv;
	//output.sampMask = arrMask[vertexId / 6];
	output.sampMask = arrMask[vertexId / 4];

	return output;
}

float4 GBufferVisPS(VS_OUTPUT input) : SV_TARGET0
{
	SURFACE_DATA surface = UnpackGBuffer(input.uv);
	float4 finalColor = float4(0.0, 0.0, 0.0, 1.0);
	finalColor += float4(1.0 - saturate(surface.linearDepth / 75.0), 1.0 - saturate(surface.linearDepth / 125.0), 1.0 - saturate(surface.linearDepth / 200.0), 0.0) * input.sampMask.xxxx;
	finalColor += float4(surface.diffuseColor.xyz, 0.0) * input.sampMask.yyyy;
	finalColor += float4(surface.normal.xyz * 0.5 + 0.5, 0.0) * input.sampMask.zzzz;
	finalColor += float4(surface.specIntensity, surface.specPow, 0.0, 0.0) * input.sampMask.wwww;

	return finalColor;
}

#endif