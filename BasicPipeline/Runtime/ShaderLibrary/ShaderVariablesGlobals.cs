using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

// Global Constant Buffers - b registers. Unity supports a maximum of 16 global constant buffers.
enum ConstantRegister
{
    Global = 0,
    Light = 1,
    PointLight = 2,
    SpotLight = 3,
}

// We need to keep the number of different constant buffers low.
// Indeed, those are bound for every single drawcall so if we split things in various CB (lightloop, SSS, Fog, etc)
// We multiply the number of CB we have to bind per drawcall.
// This is why this CB is big.
// It should only contain 2 sorts of things:
// - Global data for a camera (view matrices, RTHandle stuff, etc)
// - Things that are needed per draw call (like fog or lighting info for forward rendering)
// Anything else (such as engine passes) can have their own constant buffers (and still use this one as well).

// PARAMETERS DECLARATION GUIDELINES:
// All data is aligned on Vector4 size, arrays elements included.
// - Shader side structure will be padded for anything not aligned to Vector4. Add padding accordingly.
// - Base element size for array should be 4 components of 4 bytes (Vector4 or Vector4Int basically) otherwise the array will be interlaced with padding on shader side.
// - In Metal the float3 and float4 are both actually sized and aligned to 16 bytes, whereas for Vulkan/SPIR-V, the alignment is the same. Do not use Vector3!
// Try to keep data grouped by access and rendering system as much as possible (fog params or light params together for example).
// => Don't move a float parameter away from where it belongs for filling a hole. Add padding in this case.
[GenerateHLSL(needAccessors = false, generateCBuffer = true, constantRegister = (int)ConstantRegister.Global)]
unsafe struct ShaderVariablesGlobal
{
    public Matrix4x4 _ViewMatrix;
    public Matrix4x4 _InvViewMatrix;
    public Matrix4x4 _ProjMatrix;
    public Matrix4x4 _InvProjMatrix;
    public Matrix4x4 _ViewProjMatrix;
    public Matrix4x4 _InvViewProjMatrix;
    public Vector4 _WorldSpaceCameraPos;
    public Vector4 _PerspectiveValues;
    public Vector4 _RTHandleScale;
    public Vector4 _ViewportSize;
}


