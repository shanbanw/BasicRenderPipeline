Shader "Hidden/GBufferVis"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "GBufferVis"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex GBufferVisVS
            #pragma fragment GBufferVisPS

            #pragma target 3.5

            #include "../ShaderLibrary/GBufferVis.hlsl"
            ENDHLSL
        }
    }
}
