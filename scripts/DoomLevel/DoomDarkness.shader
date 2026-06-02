Shader "Hidden/DoomDarkness"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Darkness ("Darkness", Range(0, 1)) = 0.5
        _DarknessColor ("Darkness Color", Color) = (0,0,0,0.5)
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _Darkness;
            float4 _DarknessColor;
            float _VignetteIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Затемнение
                col.rgb = lerp(col.rgb, _DarknessColor.rgb, _Darkness);
                
                // Виньетка (затемнение по краям)
                float2 center = i.uv - 0.5;
                float vignette = 1.0 - dot(center, center) * _VignetteIntensity * 2.0;
                vignette = saturate(vignette);
                col.rgb *= vignette;
                
                return col;
            }
            ENDCG
        }
    }
}
