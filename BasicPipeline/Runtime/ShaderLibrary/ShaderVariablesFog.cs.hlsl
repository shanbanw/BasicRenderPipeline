//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef SHADERVARIABLESFOG_CS_HLSL
#define SHADERVARIABLESFOG_CS_HLSL
// Generated from FogCB
// PackingRules = Exact
CBUFFER_START(FogCB)
    float4 _FogColor; // x: r y: g z: b w: a 
    float4 _FogHighlightColor; // x: r y: g z: b w: a 
    float _FogStartDepth;
    float _FogGlobalDensity;
    float _FogHeightFalloff;
    float _padFog;
CBUFFER_END


#endif
