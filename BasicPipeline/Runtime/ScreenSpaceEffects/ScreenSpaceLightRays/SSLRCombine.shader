Shader "Basic/ScreenSpaceEffects/SSLRCombine"
{
    SubShader
    {
        Pass
        {
            Name "SSLRCombine"
            Cull Off
            ZWrite Off
            Blend One One
            BlendOp Add
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/Common.hlsl"
            #include "ShaderVariablesSSLR.cs.hlsl"

            TEXTURE2D(_LightRaysTex);

            struct VS_OUTPUT
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            VS_OUTPUT vert(uint vertexId : SV_VERTEXID)
            {
                VS_OUTPUT output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexId);
                output.uv = GetFullScreenTriangleTexCoord(vertexId);
                return output;
            }

            float4 frag(VS_OUTPUT input) : SV_TARGET0
            {
                float rayIntensity = SAMPLE_TEXTURE2D(_LightRaysTex, sampler_Linear_Clamp, input.uv * _RTHandleScale.xy).r;
                return float4(_RayColor * rayIntensity, 1.0);
            }
            
            ENDHLSL
        }
    }
}
