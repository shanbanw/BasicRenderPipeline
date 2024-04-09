Shader "Hidden/FinalBlit"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "FinalBlit"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "../ShaderLibrary/ShaderVariablesGlobals.cs.hlsl"
            TEXTURE2D(_FinalColorTex);

            float4 vert(uint vertexId : SV_VertexID) : SV_POSITION
            {
                return GetFullScreenTriangleVertexPosition(vertexId);
            }

            float4 frag(float4 positionCS : SV_POSITION) : SV_TARGET0
            {
                float2 location = positionCS.xy;
                #if UNITY_UV_STARTS_AT_TOP
                    location.y = _ViewportSize.y - location.y;
                #endif
                return LOAD_TEXTURE2D(_FinalColorTex, location);
            }
            ENDHLSL
        }
    }
}
