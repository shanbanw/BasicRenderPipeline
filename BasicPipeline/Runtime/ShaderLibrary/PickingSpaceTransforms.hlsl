#ifndef _PICKING_SPACE_TRANSFORMS_INCLUDE_
#define _PICKING_SPACE_TRANSFORMS_INCLUDE_

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_PREV_MATRIX_M unity_MatrixPreviousM
#define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI
#define UNITY_MATRIX_V _ViewMatrix
#define UNITY_MATRIX_I_V  _InvViewMatrix
#define UNITY_MATRIX_VP _ViewProjMatrix
#define UNITY_MATRIX_P _ProjMatrix

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#endif