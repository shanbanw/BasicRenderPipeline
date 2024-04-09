Shader "Hidden/HeightMap"
{
    SubShader
    {
        Pass
        {
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 _ToHeight;
            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = mul(_ToHeight, v.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
            
        }
    }
}
