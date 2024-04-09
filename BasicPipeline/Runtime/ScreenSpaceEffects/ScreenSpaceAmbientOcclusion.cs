using UnityEngine.Rendering;
using UnityEngine;

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct SSAmbientOcclusion
{
    public Vector4 _DepthDownScaleRes;
    public float _SSAOOffsetRadius;
    public float _SSAORadius;
    public float _SSAOMaxDepth;
    public float _padSSAO;
}

