// Effet plein écran d'ambiance (utilisé par ScreenMood, sur la caméra) :
// désaturation qui épargne les rouges, teinte froide, vignette, grain, aberration chromatique
// et légère ondulation de l'image. Dans Resources pour être inclus dans les builds (Shader.Find).
Shader "Hidden/ParryEverything/ScreenMood"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Desaturate;       // 0 : couleurs d'origine, 1 : noir et blanc
            float _KeepRed;          // part des rouges épargnée par la désaturation
            fixed4 _Tint;            // teinte multipliée sur toute l'image
            float _Vignette;         // force de la vignette
            fixed4 _VignetteColor;
            float _Grain;            // amplitude du grain
            float _Aberration;       // décalage rouge/bleu vers les bords
            float _Wobble;           // ondulation lente de l'image

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;

                // Ondulation : l'image "respire" à peine, comme vue à travers un air glacé
                uv.x += sin(uv.y * 9.0 + t * 0.6) * _Wobble * 0.0015;
                uv.y += sin(uv.x * 7.0 - t * 0.45) * _Wobble * 0.0010;

                // Aberration chromatique, nulle au centre, plus forte vers les bords
                float2 d = uv - 0.5;
                float2 shift = d * dot(d, d) * _Aberration * 0.04;
                fixed3 col;
                col.r = tex2D(_MainTex, uv + shift).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - shift).b;

                // Désaturation, sauf pour les rouges francs (l'accent de la zone)
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float redness = saturate((col.r - max(col.g, col.b)) * 4.0);
                float desat = _Desaturate * (1.0 - redness * _KeepRed);
                col = lerp(col, lum.xxx, desat);

                col *= _Tint.rgb;

                // Vignette ovale, douce
                float v = smoothstep(0.85, 0.25, length(d * float2(1.0, 1.25)));
                col = lerp(_VignetteColor.rgb, col, lerp(1.0, v, _Vignette));

                // Grain animé (change à chaque image)
                float n = hash(i.uv * _ScreenParams.xy + frac(t * 7.13) * 100.0) - 0.5;
                col += n * _Grain;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
