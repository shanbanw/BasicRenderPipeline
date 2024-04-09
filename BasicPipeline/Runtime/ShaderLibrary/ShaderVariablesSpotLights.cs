using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[GenerateHLSL(needAccessors = false, generateCBuffer = true, constantRegister = (int)ConstantRegister.SpotLight)]
unsafe struct ShaderVariablesSpotLight
{
    public Vector3 _SpotPosition;
    public float _SpotRangeRcp;
    public Vector3 _SpotDirection;
    public float _SpotCosOuterAngle;
    public Vector3 _SpotColor;
    public float _SpotCosAttnRangeRcp;
    public Matrix4x4 _SpotLightMatrix;
    public Matrix4x4 _ToShadowMap;
    public float _SpotSinOuterAngle;
    public float _SpotShadowmapIndex;
    public float _padSL1;
    public float _padSL2;
}