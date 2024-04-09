Shader "Hidden/DebugLightVolume"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "Debug Point Light Volume"
            ZWrite Off
            ZTest LEqual
            ZClip True
            Cull Back  // for case camera inside point light range
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma require tessellation tessHW
            #pragma target 4.0
            #pragma vertex PointLightVS
            #pragma hull PointLightHS
            #pragma domain PointLightDS
            #pragma geometry PointLightGS
            #pragma fragment DebugLightPS

            #include "../ShaderLibrary/DeferredPointLight.hlsl"

            struct GS_OUTPUT
            {
                float4 positionCS : SV_POSITION;
                float3 barycentric : TEXCOORD0;
            };

            [maxvertexcount(3)]
            void PointLightGS(triangle DS_OUTPUT input[3], inout TriangleStream<GS_OUTPUT> stream)
            {
                GS_OUTPUT output;
                output.positionCS = input[0].positionCS;
                output.barycentric = float3(1.0, 0.0, 0.0);
                stream.Append(output);
                output.positionCS = input[1].positionCS;
                output.barycentric = float3(0.0, 1.0, 0.0);
                stream.Append(output);
                output.positionCS = input[2].positionCS;
                output.barycentric = float3(0.0, 0.0, 1.0);
                stream.Append(output);
            }

            #define WireframeWidth 0.05
            #define WireframeColour float3(1.0, 1.0, 1.0)

            float4 DebugLightPS(GS_OUTPUT i) : SV_TARGET0
            {
                float closest = min(i.barycentric.x, min(i.barycentric.y, i.barycentric.z));

                float alpha = step(closest, WireframeWidth);

                return float4(WireframeColour, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Debug Spot Light Volume"
            ZWrite Off
            ZTest LEqual
            ZClip True
            Cull Back  // for case camera inside point light range
            HLSLPROGRAM
            #pragma target 4.0
            #pragma require tessellation tessHW
            #pragma vertex SpotLightVS
            #pragma hull SpotLightHS
            #pragma domain SpotLightDS
            #pragma fragment DebugLightPS

            #include "../ShaderLibrary/DeferredSpotLight.hlsl"

            float4 DebugLightPS() : SV_TARGET0
            {
	            return float4(1.0, 1.0, 1.0, 1.0);
            }
            ENDHLSL
        }
    }
}