// Objets low-poly texturés (poterie du boss) : même éclairage par face que FleshFacet (chaque triangle d'un seul ton),
// couleur = texture × couleur des sommets × teinte (UV négatifs : sans texture). _Flash : flash blanc quand l'objet est touché.
Shader "Custom/FacetTextured"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Teinte", Color) = (1, 1, 1, 1)
        _Flash ("Flash blanc (0 à 1)", Range(0, 1)) = 0
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
        #pragma surface surf Facet fullforwardshadows noambient
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Flash;
        half _FacetAmbient;
        half _FacetKey;
        float4 _FacetKeyDir;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float3 worldPos;
            float3 worldNormal;
        };

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

        void surf (Input IN, inout SurfaceOutputFacet o)
        {
            // UV négatifs (-1, -1) : face sans texture, couleur des sommets seule (intérieur et tranches d'un tesson plein)
            fixed3 tex = lerp(tex2D(_MainTex, IN.uv_MainTex).rgb, fixed3(1, 1, 1), step(IN.uv_MainTex.x, -0.5));
            fixed3 albedo = tex * IN.color.rgb * _Color.rgb;
            // Normale de la face : perpendiculaire aux variations de position d'un pixel à l'autre
            half3 flat = normalize(cross(ddy(IN.worldPos), ddx(IN.worldPos)));
            if (dot(flat, IN.worldNormal) < 0) flat = -flat;
            o.Flat = flat;
            o.Albedo = albedo;
            half sky = flat.y * 0.5 + 0.5;
            half3 ambient = ShadeSH9(half4(flat, 1.0)) + _FacetAmbient * lerp(0.2, 1.0, sky);
            ambient += _FacetKey * saturate(dot(flat, normalize(_FacetKeyDir.xyz)));
            o.Emission = lerp(albedo * ambient, fixed3(1, 1, 1), _Flash);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
