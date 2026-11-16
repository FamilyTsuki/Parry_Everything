// Corps du chat, version « toon » : même chair et même état des membres que FleshLit,
// éclairage en aplats (ombre nette), liseré de lumière sur les bords et contour sombre (coque inversée).
Shader "Custom/FleshToon"
{
    Properties
    {
        _Color ("Teinte", Color) = (1, 1, 1, 1)
        _RawColor ("Chair à vif", Color) = (0.8, 0.15, 0.13, 1)
        _BleedColor ("Hémorragie", Color) = (0.55, 0.02, 0.03, 1)
        _InfectColor ("Veines d'infection", Color) = (0.42, 0.12, 0.5, 1)
        _Fur ("Fourrure (0 à 1)", Range(0, 1)) = 0
        _Damage ("Abîmé (0 à 1)", Range(0, 1)) = 0
        _Bleed ("Hémorragie (0 à 1)", Range(0, 1)) = 0
        _Infection ("Infection (0 à 1)", Range(0, 1)) = 0
        _Flash ("Flash blanc (0 à 1)", Range(0, 1)) = 0
        _Glossiness ("Brillance de la chair", Range(0, 1)) = 0.45
        _FurGlossiness ("Brillance de la fourrure", Range(0, 1)) = 0.12
        _EmissionColor ("Lueur", Color) = (0, 0, 0, 1)
        _ToonEdge ("Limite ombre / lumière", Range(0, 1)) = 0.5
        _ToonShadow ("Clarté de l'ombre", Range(0, 1)) = 0.45
        _RimColor ("Liseré de lumière", Color) = (0.55, 0.6, 0.95, 1)
        _RimPower ("Finesse du liseré", Range(1, 8)) = 3.5
        _OutlineColor ("Contour", Color) = (0.02, 0.02, 0.05, 1)
        _OutlineWidth ("Épaisseur du contour (m)", Range(0, 0.04)) = 0.012
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        // Contour : le maillage regonflé, vu de l'intérieur, en couleur unie
        Pass
        {
            Name "Outline"
            Cull Front
            CGPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            half _OutlineWidth;

            float4 vertOutline (appdata_base v) : SV_POSITION
            {
                return UnityObjectToClipPos(v.vertex.xyz + v.normal * _OutlineWidth);
            }

            fixed4 fragOutline () : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows vertex:vert
        #pragma target 3.0
        #include "FleshCore.cginc"

        half _ToonEdge;
        half _ToonShadow;
        fixed4 _RimColor;
        half _RimPower;

        struct Input
        {
            float4 color : COLOR;
            float4 furData;
            float2 vein;     // x : veines, y : bourrelet des sutures
            float3 viewDir;
        };

        // Deux tons : éclairé ou dans l'ombre, avec une transition courte
        half4 LightingToon (SurfaceOutput s, half3 lightDir, half atten)
        {
            half light = (dot(s.Normal, lightDir) * 0.5 + 0.5) * atten;
            half band = smoothstep(_ToonEdge - 0.03, _ToonEdge + 0.03, light);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * lerp(_ToonShadow, 1.0, band);
            c.a = s.Alpha;
            return c;
        }

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.furData = v.texcoord1;
            o.vein = v.texcoord2.xy;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            FleshLook look = ComputeFlesh(IN.color, IN.furData, IN.vein);
            half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
            o.Albedo = look.albedo;
            o.Emission = look.emission + _RimColor.rgb * step(0.35, rim) * 0.35;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
