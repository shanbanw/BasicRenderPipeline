Shader "Basic/ScreenSpaceEffects/LightRayTracing"
{
    SubShader
    {
        Pass
        {
            Name "ScreenSpaceLightRayTracing"
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex RayTraceVS
            #pragma fragment RayTracePS
            
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/Common.hlsl"
            #include "ShaderVariablesSSLR.cs.hlsl"

            TEXTURE2D(_OcclusionRT);

            struct VS_OUTPUT
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            VS_OUTPUT RayTraceVS(uint vertexId : SV_VERTEXID)
            {
                VS_OUTPUT output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexId);
                output.uv = GetFullScreenTriangleTexCoord(vertexId);
                return output;
            }
            static const int k_NumSteps = 64;
            static const float k_NumDelta = 1.0 / 63.0f;

            float4 RayTracePS(VS_OUTPUT input) : SV_TARGET0
            {
                float2 dirToSun = _SunPos - input.uv;
                float lengthToSun = length(dirToSun);
                dirToSun /= lengthToSun;

                float deltaLen = min(_MaxDeltaLen, lengthToSun * k_NumDelta);
                float2 rayDelta = dirToSun * deltaLen;

                // Each step decay
                float stepDecay = _DistDecay * deltaLen;

                float2 rayOffset = float2(0.0, 0.0);
                float decay = _InitDecay;
                float rayIntensity = 0.0;

                // Ray march towards the sun
                for(int i = 0; i < k_NumSteps; i++)
                {
                    float2 sampPos = input.uv + rayOffset;
                    float curIntensity = SAMPLE_TEXTURE2D(_OcclusionRT, sampler_Linear_Clamp, sampPos * _RTHandleScale.xy).r;
                    rayIntensity += curIntensity * decay;
                    rayOffset += rayDelta;
                    decay = saturate(decay - stepDecay);
                }
                return float4(rayIntensity, 0.0, 0.0, 0.0);
            }

            ENDHLSL
        }
    }
}
