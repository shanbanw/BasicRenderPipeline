//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef POSTPROCESSINGSHADERVARIABLES_CS_HLSL
#define POSTPROCESSINGSHADERVARIABLES_CS_HLSL
// Generated from BokehHighlightScanCB
// PackingRules = Exact
CBUFFER_START(BokehHighlightScanCB)
    float _BokehBlurThreshold;
    float _BokehLumThreshold;
    float _BokehRadiusScale;
    float _BokehColorScale;
CBUFFER_END

// Generated from PostFXFinalPass
// PackingRules = Exact
CBUFFER_START(PostFXFinalPass)
    float _MiddleGrey;
    float _LumWhiteSqr;
    float2 _RTScale;
    float _BloomScale;
    float _DOFFarStart;
    float _DOFFarRangeRcp;
    float _padFX;
CBUFFER_END

// Generated from DownScaleCB
// PackingRules = Exact
CBUFFER_START(DownScaleCB)
    float _Width;
    float _Height;
    float _TotalPixels;
    float _GroupSize;
    float _Adaptation;
    float _BloomThreshold;
    float2 _padDS;
CBUFFER_END


#endif
