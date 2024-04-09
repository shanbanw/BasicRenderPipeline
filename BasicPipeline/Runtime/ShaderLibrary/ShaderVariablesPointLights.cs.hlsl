//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef SHADERVARIABLESPOINTLIGHTS_CS_HLSL
#define SHADERVARIABLESPOINTLIGHTS_CS_HLSL
// Generated from ShaderVariablesPointLight
// PackingRules = Exact
GLOBAL_CBUFFER_START(ShaderVariablesPointLight, b2)
    float3 _PointPosition;
    float _PointRangeRcp;
    float3 _PointColor;
    float _PointRange;
    float2 _PointPerspectiveValues;
    float _PointShadowmapIndex;
    float _padPL1;
CBUFFER_END


#endif
