Shader "Hidden/SSRCombine"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            BlendOp Add
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            TEXTURE2D(_ReflectionTex);

            float4 vert(uint vertexId : SV_VERTEXID) : SV_POSITION
            {
                return GetFullScreenTriangleVertexPosition(vertexId);
            }

            float4 frag(float4 positionCS : SV_POSITION) : SV_TARGET0
            {
                return LOAD_TEXTURE2D(_ReflectionTex, positionCS.xy);
            }

            ENDHLSL
        }
    }
}
