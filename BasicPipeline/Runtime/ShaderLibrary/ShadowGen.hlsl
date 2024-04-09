#ifndef _SHADOW_GEN_HLSL_
#define _SHADOW_GEN_HLSL_

#pragma multi_compile SPOT_SHADOW_GEN POINT_SHADOW_GEN CASCADE_SHADOW_GEN

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "ShaderVariables.hlsl"

CBUFFER_START(ShadowGenMat)
float4x4 _SpotViewProj;
float4x4 _PointViewProj[6];
float4x4 _CascadeViewProj[4];
int _ShadowmapIndex;
int _CascadeCount;
CBUFFER_END

float4 ShadowGenVS(float4 positionOS : POSITION) : SV_POSITION
{
	return mul(unity_ObjectToWorld, positionOS);
}

struct GS_OUTPUT
{
	float4 positionCS : SV_POSITION;
	uint RTIndex : SV_RENDERTARGETARRAYINDEX;
};

[maxvertexcount(18)]
void ShadowGenGS(triangle float4 positionWS[3] : SV_POSITION, inout TriangleStream<GS_OUTPUT> stream)
{
	int face;
	if(SPOT_SHADOW_GEN)
		face = 1;
	if(POINT_SHADOW_GEN)
		face = 6;
	if(CASCADE_SHADOW_GEN)
		face = _CascadeCount;

	[unroll]
	for(int i = 0; i < face; i++)
	{
		float4x4 vp;
		if(SPOT_SHADOW_GEN)
			vp = _SpotViewProj;
		if(POINT_SHADOW_GEN)
			vp = _PointViewProj[i];
		if(CASCADE_SHADOW_GEN)
			vp = _CascadeViewProj[i];
		GS_OUTPUT output;
		
		output.RTIndex = i + face * _ShadowmapIndex;
		for(int v = 0; v < 3; v++)
		{
			output.positionCS = mul(vp, positionWS[v]);
			stream.Append(output);
		}
		//output.positionCS = mul(vp, positionWS[0]);
		//stream.Append(output);
		//output.positionCS = mul(vp, positionWS[2]);
		//stream.Append(output);
		//output.positionCS = mul(vp, positionWS[1]);
		//stream.Append(output);
		stream.RestartStrip();
	}
}

float4 ShadowGenPS() :SV_TARGET0
{
	return 1.0;
}

#endif