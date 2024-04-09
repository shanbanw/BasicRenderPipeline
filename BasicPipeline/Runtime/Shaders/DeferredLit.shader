Shader "Hidden/DeferredLit"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "Deferred Base"
            ZWrite Off
            Cull Off
            Stencil
            {
                Ref 2
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma multi_compile _ FOG_ON

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "../ShaderLibrary/DeferredDirLight.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Deferred Point"
            ZWrite Off
            ZTest GEqual
            ZClip False
            Cull Front  // for case camera inside point light range
            Blend 0 One One
            BlendOp Add

            Stencil
            {
                Ref 2
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex PointLightVS
            #pragma hull PointLightHS
            #pragma domain PointLightDS
            #pragma fragment PointLightPS

            #include "../ShaderLibrary/DeferredPointLight.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Deferred Spot"
            ZWrite Off
            ZTest GEqual
            ZClip False
            Cull Front
            Blend 0 One One
            BlendOp Add

            Stencil
            {
                Ref 2
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma require tessellation tessHW
            #pragma vertex SpotLightVS
            #pragma hull SpotLightHS
            #pragma domain SpotLightDS
            #pragma fragment SpotLightPS

            #include "../ShaderLibrary/DeferredSpotLight.hlsl"

            ENDHLSL
        }

    }
}
