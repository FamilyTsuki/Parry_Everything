// Corps du chat, version « low-poly facetté » : même chair et même état des membres que FleshLit,
// mais chaque triangle est éclairé d'un seul ton (normale de la face, calculée à l'écran) : facettes nettes.
Shader "Custom/FleshFacet"
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
        _FacetAmbient ("Lumière ambiante des faces", Range(0, 1)) = 0.35
        _FacetKey ("Lumière d'appoint des faces", Range(0, 3)) = 0.9
        _FacetKeyDir ("Direction de l'appoint (monde)", Vector) = (0.5, 0.8, -0.6, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        // noambient : l'ambiance est refaite à la main avec la normale de la face (sinon elle lisse les facettes)
        #pragma surface surf Facet fullforwardshadows vertex:vert noambient
        #pragma target 3.0
        #include "FleshCore.cginc"

        half _FacetAmbient;
        half _FacetKey;
        float4 _FacetKeyDir;

        struct Input
        {
            float4 color : COLOR;
            float4 furData;
            float2 vein;     // x : veines, y : bourrelet des sutures
            float3 worldPos;
            float3 worldNormal;
        };

        // Sortie de surface avec la normale de la face (la normale lissée sert seulement à l'orienter)
        struct SurfaceOutputFacet
        {
            fixed3 Albedo;
            fixed3 Normal;
            fixed3 Emission;
            half Specular;
            fixed Gloss;
            fixed Alpha;
            half3 Flat;
        };

        half4 LightingFacet (SurfaceOutputFacet s, half3 lightDir, half atten)
        {
            half nl = saturate(dot(s.Flat, lightDir));
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * nl * atten;
            c.a = s.Alpha;
            return c;
        }

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.furData = v.texcoord1;
            o.vein = v.texcoord2.xy;
        }

        void surf (Input IN, inout SurfaceOutputFacet o)
        {
            FleshLook look = ComputeFlesh(IN.color, IN.furData, IN.vein);
            // Normale de la face : perpendiculaire aux variations de position d'un pixel à l'autre
            half3 flat = normalize(cross(ddy(IN.worldPos), ddx(IN.worldPos)));
            if (dot(flat, IN.worldNormal) < 0) flat = -flat;
            o.Flat = flat;
            o.Albedo = look.albedo;
            // Ambiance par face aussi : lumière ambiante de la scène dans la direction de la face,
            // plus un léger dégradé haut/bas qui sépare les facettes même dans l'ombre
            half sky = flat.y * 0.5 + 0.5;
            half3 ambient = ShadeSH9(half4(flat, 1.0)) + _FacetAmbient * lerp(0.2, 1.0, sky);
            // Appoint fixe (haut, avant, côté caméra) : chaque face a son ton même dans une scène sombre
            ambient += _FacetKey * saturate(dot(flat, normalize(_FacetKeyDir.xyz)));
            o.Emission = look.emission + look.albedo * ambient;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
