using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;


[GenerateHLSL(needAccessors = false, generateCBuffer = true, constantRegister = (int)ConstantRegister.Light)]
unsafe struct ShaderVariablesLight
{
    public Vector3 _AmbientLower;
    public float _padL0;
    public Vector3 _AmbientRange;
    public float _padL1;
    public Vector3 _DirToLight;
    public float _padL2;
    public Vector3 _DirectionalColor;
    public float _CascadeShadowmapIndex;
    public Matrix4x4 _ToCascadeShadowSpace;
    public Vector4 _ToCascadeOffsetX;
    public Vector4 _ToCascadeOffsetY;
    public Vector4 _ToCascadeScale;
}