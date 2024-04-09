#ifndef _SHADER_VARIABLES_INLCUDE_
#define _SHADER_VARIABLES_INCLUDE_

#include "ShaderVariablesGlobals.cs.hlsl"

CBUFFER_START(UnityPerDraw)

float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4 unity_LODFade; // x is the fade value ranging within [0,1]. y is x quantized into 16 levels
float4 unity_WorldTransformParams; // w is usually 1.0, or -1.0 for odd-negative scale transforms
float4 unity_RenderingLayer;

float4 unity_LightmapST;
float4 unity_DynamicLightmapST;

// SH lighting environment
float4 unity_SHAr;
float4 unity_SHAg;
float4 unity_SHAb;
float4 unity_SHBr;
float4 unity_SHBg;
float4 unity_SHBb;
float4 unity_SHC;

// Renderer bounding box.
float4 unity_RendererBounds_Min;
float4 unity_RendererBounds_Max;

// x = Disabled(0)/Enabled(1)
// y = Computation are done in global space(0) or local space(1)
// z = Texel size on U texture coordinate
float4 unity_ProbeVolumeParams;
float4x4 unity_ProbeVolumeWorldToObject;
float4 unity_ProbeVolumeSizeInv; // Note: This variable is float4 and not float3 (compare to builtin unity) to be compatible with SRP batcher
float4 unity_ProbeVolumeMin; // Note: This variable is float4 and not float3 (compare to builtin unity) to be compatible with SRP batcher

// This contain occlusion factor from 0 to 1 for dynamic objects (no SH here)
float4 unity_ProbesOcclusion;

// Velocity
float4x4 unity_MatrixPreviousM;
float4x4 unity_MatrixPreviousMI;
//X : Use last frame positions (right now skinned meshes are the only objects that use this
//Y : Force No Motion
//Z : Z bias value
//W : Camera only
float4 unity_MotionVectorsParams;

CBUFFER_END

#endif