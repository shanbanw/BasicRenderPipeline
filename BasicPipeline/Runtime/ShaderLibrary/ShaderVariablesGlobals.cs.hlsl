//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef SHADERVARIABLESGLOBALS_CS_HLSL
#define SHADERVARIABLESGLOBALS_CS_HLSL
// Generated from ShaderVariablesGlobal
// PackingRules = Exact
GLOBAL_CBUFFER_START(ShaderVariablesGlobal, b0)
    float4x4 _ViewMatrix;
    float4x4 _InvViewMatrix;
    float4x4 _ProjMatrix;
    float4x4 _InvProjMatrix;
    float4x4 _ViewProjMatrix;
    float4x4 _InvViewProjMatrix;
    float4 _WorldSpaceCameraPos;
    float4 _PerspectiveValues;
    float4 _RTHandleScale;
    float4 _ViewportSize;
CBUFFER_END


#endif
