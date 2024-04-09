Shader "Hidden/SunEmissive"
{
	SubShader
	{
		Pass
		{
			Name "Sun Emissive"

			ZTest Off
			ZWrite Off
			Stencil
			{
				Ref 0
				Comp Equal
			}

			HLSLPROGRAM
			#pragma vertex SunEmissiveVert
			#pragma fragment SunEmissiveFrag

			//CBUFFER_START(SunEmissive)
			float4x4 _SunWorldViewProjectMatrix;
			float4 _SunColor;
			//CBUFFER_END

			struct VS_INPUT
			{
				float4 positionOS : POSITION;
			};

			struct VS_OUTPUT
			{
				float4 positionCS : SV_POSITION;
			};

			VS_OUTPUT SunEmissiveVert(VS_INPUT input)
			{
				VS_OUTPUT output;
				output.positionCS = mul(_SunWorldViewProjectMatrix, input.positionOS);
				return output;
			}

			float4 SunEmissiveFrag(VS_OUTPUT input) : SV_TARGET0
			{
				return _SunColor;
			}
			ENDHLSL
		}


	}
}