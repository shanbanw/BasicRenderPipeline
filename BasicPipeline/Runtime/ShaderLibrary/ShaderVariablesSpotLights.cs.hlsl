//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef SHADERVARIABLESSPOTLIGHTS_CS_HLSL
#define SHADERVARIABLESSPOTLIGHTS_CS_HLSL
// Generated from ShaderVariablesSpotLight
// PackingRules = Exact
GLOBAL_CBUFFER_START(ShaderVariablesSpotLight, b3)
    float3 _SpotPosition;
    float _SpotRangeRcp;
    float3 _SpotDirection;
    float _SpotCosOuterAngle;
    float3 _SpotColor;
    float _SpotCosAttnRangeRcp;
    float4x4 _SpotLightMatrix;
    float4x4 _ToShadowMap;
    float _SpotSinOuterAngle;
    float _SpotShadowmapIndex;
    float _padSL1;
    float _padSL2;
CBUFFER_END


#endif
