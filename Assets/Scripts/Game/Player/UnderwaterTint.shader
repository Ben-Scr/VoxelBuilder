Shader "Hidden/UnderwaterTint"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.1, 0.4, 0.8, 1)
        _Strength ("Strength", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _TintColor;
            float _Strength;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv);
                source.rgb = lerp(source.rgb, _TintColor.rgb, saturate(_Strength));
                return source;
            }
            ENDCG
        }
    }
}