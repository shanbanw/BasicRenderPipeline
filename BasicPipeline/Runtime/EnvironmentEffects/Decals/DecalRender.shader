Shader "Basic/EnvironmentEffects/DecalRender"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha, One Zero
            BlendOp Add
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
            #pragma fragment frag

            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/Common.hlsl"
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"
            #include "ShaderVariablesDecals.cs.hlsl"
            StructuredBuffer<DecalVertex> _DecalVertexBuffer;
            //StructuredBuffer<DecalVertexLayout> _DecalBuffer;
            TEXTURE2D(_MainTex);

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 positionCS : SV_POSITION;
            };

            v2f vert (uint vertexId : SV_VERTEXID)
            {
                DecalVertex vertex = _DecalVertexBuffer[vertexId];
                v2f o;
                o.positionCS = TransformWorldToHClip(vertex.position);
                o.normalWS = vertex.normal;
                o.uv = vertex.uv;
                return o;
            }

            struct PS_GBUFFER_OUT
            {
                float4 colorSpecIntensity : SV_TARGET0;
                float4 normal : SV_TARGET1;
                float4 specPow : SV_TARGET2;
            };
            PS_GBUFFER_OUT PackGBuffer(float3 baseColor, float3 normal, float specIntensity, float specPower)
            {
                PS_GBUFFER_OUT gbuffer;
    
                // Normalize the specular power
                float specPowerNorm = max(0.0001, (specPower - g_SpecPowerRange.x) / g_SpecPowerRange.y);

                gbuffer.colorSpecIntensity = float4(baseColor, specIntensity);
                gbuffer.normal = float4(normal * 0.5 + 0.5, 0.0);
                gbuffer.specPow = float4(specPowerNorm, 0.0, 0.0, 0.0);

                return gbuffer;
            }
            PS_GBUFFER_OUT frag (v2f i)
            {
                PS_GBUFFER_OUT output;
                float4 diffuseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_Linear_Clamp, i.uv);
                output = PackGBuffer(diffuseColor.rgb, normalize(i.normalWS), 1.0, 50.0);
                output.colorSpecIntensity.a = diffuseColor.a;
                return output;
            }
            ENDHLSL
        }
    }
}
