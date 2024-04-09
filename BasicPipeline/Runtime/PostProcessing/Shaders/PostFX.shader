Shader "Hidden/PostFX"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "PostFX"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma target 4.5

            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/Common.hlsl"
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/Sampler.hlsl"
            #include "PostProcessingShaderVariables.cs.hlsl"

            TEXTURE2D(_HDRTexture);
            StructuredBuffer<float> _AvgLum;
            TEXTURE2D(_BloomTexture);
            TEXTURE2D(_DownScaleTexture);

            struct VS_OUTPUT
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            VS_OUTPUT vert(uint vertexId : SV_VertexID)
            {
                VS_OUTPUT output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexId);
                output.uv = GetFullScreenTriangleTexCoord(vertexId);
                return output;
            }

            static const float3 LUM_FACTOR = float3(0.299, 0.587, 0.114);

            float3 ToneMapping(float3 hdrColor)
            {
                float LScale = dot(hdrColor, LUM_FACTOR);
                LScale *= _MiddleGrey / _AvgLum[0];
                LScale = (LScale + LScale * LScale / _LumWhiteSqr) / (1 + LScale);
                return LScale * hdrColor;
            }

            float3 DistanceDOF(float3 colorFocus, float3 colorBlurred, float depth)
            {
                float blurFactor = saturate((depth - _DOFFarStart) * _DOFFarRangeRcp);

                return lerp(colorFocus, colorBlurred, blurFactor);
            }

            float4 frag(VS_OUTPUT input) : SV_TARGET0
            {
                float2 uv = input.uv * _RTScale.xy;
                float3 color = SAMPLE_TEXTURE2D(_HDRTexture, sampler_Point_Clamp, uv).xyz;

                float depth = SAMPLE_DEPTH_TEXTURE(_DepthTex, sampler_Point_Clamp, uv);
                if (depth != UNITY_RAW_FAR_CLIP_VALUE)
                {
                    float3 colorBlurred = SAMPLE_TEXTURE2D(_DownScaleTexture, sampler_Linear_Clamp, uv).xyz;

                    depth = ConvertZToLinearDepth(depth);

                    color = DistanceDOF(color, colorBlurred, depth);
                }

                // Bloom
                color += _BloomScale * SAMPLE_TEXTURE2D(_BloomTexture, sampler_Linear_Clamp, uv).xyz;

                color = ToneMapping(color);

                return float4(color, 1.0);
            }

            ENDHLSL
        }
    }
}
