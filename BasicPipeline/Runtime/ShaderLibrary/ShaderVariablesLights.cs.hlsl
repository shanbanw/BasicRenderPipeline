//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef SHADERVARIABLESLIGHTS_CS_HLSL
#define SHADERVARIABLESLIGHTS_CS_HLSL
// Generated from ShaderVariablesLight
// PackingRules = Exact
GLOBAL_CBUFFER_START(ShaderVariablesLight, b1)
    float3 _AmbientLower;
    float _padL0;
    float3 _AmbientRange;
    float _padL1;
    float3 _DirToLight;
    float _padL2;
    float3 _DirectionalColor;
    float _CascadeShadowmapIndex;
    float4x4 _ToCascadeShadowSpace;
    float4 _ToCascadeOffsetX;
    float4 _ToCascadeOffsetY;
    float4 _ToCascadeScale;
CBUFFER_END


#endif
