Shader "Basic/EnvironmentEffects/RainRender"
{
    Properties
    {
        _RainStreakTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "Rain Render"
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            BlendOp Add
            HLSLPROGRAM
            #pragma vertex RainVS
            #pragma fragment RainPS

            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/Common.hlsl"
            #include "ShaderVariablesRain.cs.hlsl"

            StructuredBuffer<RainDrop> _RainDataBuffer;
            TEXTURE2D(_RainStreakTex);

            struct VS_OUTPUT
            {
                float4 positionCS : SV_POSITION;
                //float clip : SV_CLIPDISTANCE0;
                float2 uv : TEXCOORD0;
            };

            VS_OUTPUT RainVS(uint vertexId : SV_VERTEXID)
            {
                VS_OUTPUT output;
                RainDrop curDrop = _RainDataBuffer[vertexId / 4];

                float3 position = curDrop.position;

                float3 rainDir = normalize(curDrop.velocity);
                float3 rainRight = normalize(cross(_ViewDir, rainDir));

                float2 offsets = GetQuadVertexPosition(vertexId % 4).xy;
                offsets = offsets * 2,0 - 1.0;
                position += rainRight * offsets.x * _RainScale * 0.025;
                position += rainDir * offsets.y * _RainScale;

                output.positionCS = mul(_ViewProjMatrix, float4(position, 1.0));
                output.uv = GetQuadTexCoord(vertexId % 4);

                //output.clip = curDrop.state;

                return output;
            }

            float4 RainPS(VS_OUTPUT input) : SV_TARGET0
            {
                float alpha = _RainStreakTex.Sample(sampler_Linear_Clamp, input.uv).r;
                return float4(_RainAmbientColor.rgb, _RainAmbientColor.a * alpha);
            }
            ENDHLSL
            
        }
    }
}
