Shader "Basic/PostFX/BokehRender"
{
    Properties
    {
        _BokehTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            Name "BokenRender"
            Blend SrcAlpha OneMinusSrcAlpha
            BlendOp Add
            HLSLPROGRAM
            #pragma vertex BokehVS
            #pragma geometry BokehGS
            #pragma fragment BokehPS
            
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BokehTex);
            struct Bokeh
            {
                float2 positionCS;
                float radius;
                float4 bokehColor;
            };
            StructuredBuffer<Bokeh> _BokehStackSRV;

            struct VS_OUTPUT
            {
                float4 positionCS : SV_POSITION;
                float radius : TEXCOORD0;
                float4 bokehColor : TEXCOORD1;
            };

            VS_OUTPUT BokehVS( uint vertexId : SV_VERTEXID)
            {
                VS_OUTPUT output;

                Bokeh bokeh = _BokehStackSRV[vertexId];

                output.positionCS = float4(bokeh.positionCS, UNITY_NEAR_CLIP_VALUE, 1.0);
                output.radius = bokeh.radius;
                output.bokehColor = bokeh.bokehColor;

                return output;
            }

            //float2 _BokehAspectRatio;
            static const float2 arrBasePos[6] = {
	            float2(-1.0, 1.0),
	            float2(1.0, 1.0),
	            float2(-1.0, -1.0),
	            float2(1.0, 1.0),
	            float2(1.0, -1.0),
	            float2(-1.0, -1.0),
            };

            static const float2 arrUV[6] = {
	            float2(0.0, 1.0),
	            float2(1.0, 1.0),
	            float2(0.0, 0.0),
	            float2(1.0, 1.0),
	            float2(1.0, 0.0),
	            float2(0.0, 0.0),
            };
            struct GS_OUTPUT
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 bokehColor : TEXCOORD1;
            };
            [maxvertexcount(6)]
            void BokehGS(point VS_OUTPUT input[1], inout TriangleStream<GS_OUTPUT> stream)
            {
                GS_OUTPUT output;
                for(int i =0, idx = 0; i < 2; i++)
                {
                    for(int j = 0; j < 3; j++, idx++)
                    {
                        float2 base = arrBasePos[idx];
                        #if UNITY_UV_STARTS_AT_TOP
                            base.y *= -1;
                        #endif
                        float2 pos = input[0].positionCS.xy + base * input[0].radius; //* _BokehAspectRatio;
                        output.positionCS = float4(pos, input[0].positionCS.zw);
                        output.uv = arrUV[idx];
                        
                        output.bokehColor = input[0].bokehColor;
                        stream.Append(output);
                    }
                    stream.RestartStrip();
                }
            }

            float4 BokehPS(GS_OUTPUT input) : SV_TARGET0
            {
                float alpha = SAMPLE_TEXTURE2D(_BokehTex, sampler_Linear_Clamp, input.uv.xy).r;
                return float4(input.bokehColor.xyz, input.bokehColor.w * alpha);
            }

            ENDHLSL
        }
    }
}
