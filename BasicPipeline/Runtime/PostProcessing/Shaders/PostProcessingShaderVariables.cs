using UnityEngine.Rendering;
using UnityEngine;

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct DownScaleCB
{
    public float _Width;
    public float _Height;
    public float _TotalPixels;
    public float _GroupSize;
    public float _Adaptation;
    public float _BloomThreshold;
    public Vector2 _padDS;
}

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct PostFXFinalPass
{
    public float _MiddleGrey;
    public float _LumWhiteSqr;
    public Vector2 _RTScale;
    public float _BloomScale;
    public float _DOFFarStart;
    public float _DOFFarRangeRcp;
    public float _padFX;
}

[GenerateHLSL(needAccessors =false, generateCBuffer = true)]
unsafe struct BokehHighlightScanCB
{
    public float _BokehBlurThreshold;
    public float _BokehLumThreshold;
    public float _BokehRadiusScale;
    public float _BokehColorScale;
}
