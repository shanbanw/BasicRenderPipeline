Shader "Basic/Emissive"
{
	Properties
	{
		[HDR] _Color ("Color", Color) = (1.3, 0.8, 0.7, 1.0)
	}
	SubShader
	{
		
		Pass
		{
			Tags {"LightMode" = "Emissive"}
			Name "Emissive"

			Stencil
			{
				Ref 2
				Comp Always
				Pass Replace
                Fail Replace
                ZFail Replace
			}

			HLSLPROGRAM
			#pragma vertex RenderEmissiveVS
			#pragma fragment RenderEmissivePS

			#include "../ShaderLibrary/Emissive.hlsl"
			ENDHLSL
		}


	}
}