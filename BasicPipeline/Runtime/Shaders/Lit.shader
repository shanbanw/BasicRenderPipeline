Shader "Basic/Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SpecExp("Specular Exp", Range(10, 260)) = 250
        _SpecIntensity("Specular Intensity", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque"}
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            Name "ShadowCaster"
            ColorMask 0
            ZClip [_ZClip]
            //Cull Off
            HLSLPROGRAM
            
            #pragma vertex ShadowGenVS
            #pragma geometry ShadowGenGS
            #pragma fragment ShadowGenPS
            #include_with_pragmas "../ShaderLibrary/ShadowGen.hlsl"

            ENDHLSL
        }


        Pass
        {
            Tags { "LightMode" = "GBuffer" }
            Name "GBufferPass"
            Stencil
            {
                Ref 2
                Comp Always
                Pass Replace
                Fail Replace
                ZFail Replace
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment GBufferFrag

            #include "../ShaderLibrary/GBufferPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "DepthPrepass" }
            Name "DepthPrepass"
            ColorMask 0
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "../ShaderLibrary/ShaderVariables.hlsl"
            #include "../ShaderLibrary/PickingSpaceTransforms.hlsl"

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }
            float4 frag() : SV_TARGET
            {
                return 1;
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode" = "ForwardOpaqueBase" }
            Name "LitBasePass"
            ZWrite Off
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "../ShaderLibrary/ForwardLightCommon.hlsl"
            #include "../ShaderLibrary/ShaderVariablesLights.cs.hlsl"

            float3 CalcAmbient(float3 normal, float3 color)
            {
                float up = normal.y * 0.5 + 0.5;
                float3 ambient = _AmbientLower + up * _AmbientRange;
                return ambient * color;
            }

            TEXTURE2D_ARRAY(_CascadeShadowmapTexture);
            float CascadedShadow(float3 position)
            {
                float4 posShadowSpace = mul(_ToCascadeShadowSpace, float4(position, 1.0));

                float4 posCascadeSpaceX = (posShadowSpace.xxxx + _ToCascadeOffsetX) * _ToCascadeScale;
                float4 posCascadeSpaceY = (posShadowSpace.yyyy + _ToCascadeOffsetY) * _ToCascadeScale;

                float4 inCascadeX = abs(posCascadeSpaceX) <= 1.0;
                float4 inCascadeY = abs(posCascadeSpaceY) <= 1.0;
                float4 inCascade = inCascadeX * inCascadeY;

                float4 bestCascadeMask = inCascade;
	            bestCascadeMask.yzw = (1.0 - bestCascadeMask.x) * bestCascadeMask.yzw;
	            bestCascadeMask.zw = (1.0 - bestCascadeMask.y) * bestCascadeMask.zw;
	            bestCascadeMask.w = (1.0 - bestCascadeMask.z) * bestCascadeMask.w;

                float bestCascade = dot(bestCascadeMask, float4(0.0, 1.0, 2.0, 3.0));

                float3 uvd;
	            uvd.x = dot(posCascadeSpaceX, bestCascadeMask);
	            uvd.y = dot(posCascadeSpaceY, bestCascadeMask);
	            uvd.z = posShadowSpace.z;

                uvd.xy = uvd.xy * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uvd.y = 1.0 - uvd.y;
                #endif

                float shadow = SAMPLE_TEXTURE2D_ARRAY_SHADOW(_CascadeShadowmapTexture, sampler_Linear_Clamp_Compare, uvd, bestCascade);

                // set the shadow to one (fully lit) for positions with no cascade coverage
	            shadow = saturate(shadow + 1.0 - any(bestCascadeMask));

                return shadow;
            }


            float3 CalcDirectional(float3 position, Material material)
            {
                // Phong diffuse
                float NDotL = dot(_DirToLight, material.normal);
                float3 finalColor = _DirectionalColor.rgb * saturate(NDotL);

                // Blinn specular
                float3 toEye = _WorldSpaceCameraPos.xyz - position;
                toEye = SafeNormalize(toEye);
                float3 halfWay = SafeNormalize(toEye + _DirToLight);
                float NDotH = saturate(dot(halfWay, material.normal));
                finalColor += _DirectionalColor.rgb * pow(NDotH, material.specExp) * material.specIntensity;

                float shadowAttn = 1.0;
                if (_CascadeShadowmapIndex >= 0)
                    shadowAttn = CascadedShadow(position);

                return finalColor * material.diffuseColor.rgb * shadowAttn;
            }

            float4 frag(VS_OUTPUT input) : SV_TARGET0
            {
                float2 uv = TRANSFORM_TEX(input.uv, _MainTex);
                Material material = PrepareMaterial(input.normalWS, uv);

                float3 finalColor = CalcAmbient(input.normalWS, material.diffuseColor.rgb);

                finalColor += CalcDirectional(input.positionWS, material);

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode" = "ForwardOpaqueAdd" }
            Name "LitAddPass"
            Blend 0 One One
            BlendOp Add
            ZWrite Off
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma multi_compile _ POINT_LIGHT_ON SPOT_LIGHT_ON
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "../ShaderLibrary/ForwardLightCommon.hlsl"
            #include "../ShaderLibrary/ShaderVariablesPointLights.cs.hlsl"
            #include "../ShaderLibrary/ShaderVariablesSpotLights.cs.hlsl"
            #include "../ShaderLibrary/ShadowPCF.hlsl"

            float3 CalcPoint(float3 position, Material material)
            {
                float3 toLight = _PointPosition.xyz - position;
                float3 toEye = _WorldSpaceCameraPos.xyz - position;
                float distanceToLight = length(toLight);

                // Phong diffuse
                toLight /= distanceToLight;
                float NDotL = saturate(dot(toLight, material.normal));
                float3 finalColor = _PointColor.rgb * NDotL;

                // Blinn specular
                toEye = SafeNormalize(toEye);
                float3 H = SafeNormalize(toEye + toLight);
                float NDotH = saturate(dot(H, material.normal));
                finalColor += _PointColor.rgb * pow(NDotH, material.specExp) * material.specIntensity;

                // Attenuation
                float distanceToLightNorm = 1.0 - saturate(distanceToLight * _PointRangeRcp);
                float attn = distanceToLightNorm * distanceToLightNorm;

                float shadowAttn = 1.0;
                if (_PointShadowmapIndex >= 0)
                    shadowAttn = PointShadowPCF(position - _PointPosition);

                finalColor *= material.diffuseColor.rgb * attn * shadowAttn;

                return finalColor;
            }

            float3 CalcSpot(float3 position, Material material)
            {
                float3 toLight = _SpotPosition - position;
                float3 toEye = _WorldSpaceCameraPos.xyz - position;
                float distanceToLight = length(toLight);

                // Phong diffuse
                toLight /= distanceToLight;
                float NDotL = saturate(dot(toLight, material.normal));
                float3 finalColor = _SpotColor.rgb * NDotL;

                // Blinn specular
                toEye = SafeNormalize(toEye);
                float3 H = SafeNormalize(toEye + toLight);
                float NDotH = saturate(dot(H, material.normal));
                finalColor += _SpotColor.rgb * pow(NDotH, material.specExp) * material.specIntensity;

                // Cone attenuation
                float cosAngle = dot(_SpotDirection, toLight);
                float conAttn = saturate((cosAngle - _SpotCosOuterAngle) * _SpotCosAttnRangeRcp);
                conAttn *= conAttn;

                // Attenuation
                float distanceToLightNorm = 1.0f - saturate(distanceToLight * _SpotRangeRcp);
                float attn = distanceToLightNorm * distanceToLightNorm;

                float shadowAttn = 1.0;
                if (_SpotShadowmapIndex >= 0)
                    shadowAttn = SpotShadowPCF(position);

                finalColor *= material.diffuseColor.rgb * attn * conAttn * shadowAttn;

                return finalColor;
            }

            float4 frag(VS_OUTPUT input) : SV_TARGET0
            {
                float2 uv = TRANSFORM_TEX(input.uv, _MainTex);
                Material material = PrepareMaterial(input.normalWS, uv);

                float3 finalColor = 0.0;
                if (POINT_LIGHT_ON)
                {
                    finalColor += CalcPoint(input.positionWS, material);
                }

                if (SPOT_LIGHT_ON)
                {
                    finalColor += CalcSpot(input.positionWS, material);
                }

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}
