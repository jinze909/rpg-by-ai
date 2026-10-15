Shader "Hidden/Starfall/HeroStraightAlpha"
{
    Properties { _MainTex ("Premultiplied hero render", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Blend Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 frag(v2f_img input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                // SpriteFrag writes premultiplied RGB; the later GUI texture
                // blend applies alpha once. Transparent pixels remain zero.
                return color.a > 0 ? float4(color.rgb / max(color.a, 0.000001), color.a) : float4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
