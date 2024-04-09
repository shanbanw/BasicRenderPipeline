using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[GenerateHLSL(needAccessors = false, generateCBuffer = true, constantRegister = (int)ConstantRegister.PointLight)]
unsafe struct ShaderVariablesPointLight
{
    public Vector3 _PointPosition;
    public float _PointRangeRcp;
    public Vector3 _PointColor;
    public float _PointRange;
    public Vector2 _PointPerspectiveValues;
    public float _PointShadowmapIndex;
    public float _padPL1;
}
