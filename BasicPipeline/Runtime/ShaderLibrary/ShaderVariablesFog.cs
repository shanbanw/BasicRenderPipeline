using UnityEngine;
using UnityEngine.Rendering;

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct FogCB
{
    public Color _FogColor;
    public Color _FogHighlightColor;
    public float _FogStartDepth;
    public float _FogGlobalDensity;
    public float _FogHeightFalloff;
    public float _padFog;
}
