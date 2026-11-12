// Shader de l'âme de chat : flamme bleue translucide, bord lumineux, bruit qui monte, halo additif.
// Dans Resources pour être inclus dans les builds (Shader.Find).
Shader "Custom/CatSoul"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (0.75, 0.95, 1, 1)
        _RimColor ("Rim Color", Color) = (0.1, 0.4, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _Alpha ("Alpha", Range(0, 1)) = 0.85
        _Glow ("Glow", Range(0, 4)) = 1
        _NoiseScale ("Noise Scale", Float) = 3
        _NoiseSpeed ("Noise Speed", Float) = 2.5
        _HaloSize ("Halo Size", Float) = 0.08
        _HaloStrength ("Halo Strength", Range(0, 2)) = 0.6
        _Flash ("Flash", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        CGINCLUDE
        #include "UnityCG.cginc"

        fixed4 _CoreColor, _RimColor, _FlashColor;
        float _RimPower, _Alpha, _Glow, _NoiseScale, _NoiseSpeed, _HaloSize, _HaloStrength, _Flash;

        float hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // Bruit de valeur 3D lissé
        float noise3(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(lerp(hash(i + float3(0, 0, 0)), hash(i + float3(1, 0, 0)), f.x),
                     lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x),
                     lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y),
                f.z);
        }

        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            fixed4 color : COLOR;   // alpha = 1 sur le corps, diminue vers la pointe de la queue
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float3 normal : TEXCOORD0;
            float3 viewDir : TEXCOORD1;
            float3 lpos : TEXCOORD2;
            fixed4 color : COLOR;
        };

        v2f build(appdata v, float extrude)
        {
            v2f o;
            float4 vert = v.vertex;
            vert.xyz += v.normal * extrude;
            o.pos = UnityObjectToClipPos(vert);
            o.normal = UnityObjectToWorldNormal(v.normal);
            o.viewDir = WorldSpaceViewDir(vert);
            o.lpos = v.vertex.xyz;
            o.color = v.color;
            return o;
        }
        ENDCG

        // Halo : coque légèrement agrandie, faces arrière, additive
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            v2f vert(appdata v) { return build(v, _HaloSize); }

            fixed4 frag(v2f i) : SV_Target
            {
                float ndv = abs(dot(normalize(i.normal), normalize(i.viewDir)));
                float a = pow(ndv, 3.0) * _HaloStrength * i.color.a;
                float3 col = lerp(_RimColor.rgb, _FlashColor.rgb, _Flash) * _Glow;
                return fixed4(col, saturate(a));
            }
            ENDCG
        }

        // Corps : cœur clair, bord bleu, bandes de bruit qui montent comme une flamme
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            v2f vert(appdata v) { return build(v, 0.0); }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 V = normalize(i.viewDir);
                float ndv = saturate(abs(dot(N, V)));
                float rim = pow(1.0 - ndv, _RimPower);

                float n = noise3(i.lpos * _NoiseScale + float3(0, -_Time.y * _NoiseSpeed, 0));
                float tail = 1.0 - i.color.a;

                float3 col = lerp(_CoreColor.rgb, _RimColor.rgb, saturate(rim * 1.2 + tail * 0.6));
                col *= _Glow * (0.75 + 0.5 * n);
                col = lerp(col, _FlashColor.rgb, _Flash);

                float a = _Alpha * lerp(1.0, 0.5, rim) * i.color.a * (0.85 + 0.3 * n);
                return fixed4(col, saturate(a + _Flash * 0.5));
            }
            ENDCG
        }
    }
}
