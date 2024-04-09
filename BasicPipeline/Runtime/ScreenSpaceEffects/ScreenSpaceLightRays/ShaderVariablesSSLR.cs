using UnityEngine;
using UnityEngine.Rendering;

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct LightRayTraceCB
{
    public Vector2 _SunPos;
    public float _InitDecay;
    public float _DistDecay;
    public Vector3 _RayColor;
    public float _MaxDeltaLen;
}