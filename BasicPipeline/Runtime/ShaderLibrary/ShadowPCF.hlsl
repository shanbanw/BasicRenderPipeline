#ifndef _SHADOW_PCF_HLSL_
#define _SHADOW_PCF_HLSL_

TEXTURE2D_ARRAY(_SpotShadowmapTexture);
TEXTURECUBE_ARRAY(_PointShadowmapTexture);

float SpotShadowPCF(float3 position)
{
	float4 posShadowSpace = mul(_ToShadowMap, float4(position, 1.0));

	float3 uvd = posShadowSpace.xyz / posShadowSpace.w;
	 uvd.xy = uvd.xy * 0.5 + 0.5;

	 return SAMPLE_TEXTURE2D_ARRAY_SHADOW(_SpotShadowmapTexture, sampler_Linear_Clamp_Compare, uvd, _SpotShadowmapIndex);
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


#endif