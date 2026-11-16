// Corps du chat : fourrure par-dessus de la chair cousue. L'état du membre se lit sur sa surface.
//
// Données des sommets (écrites par CatModelBuilder) :
//   couleur (COLOR)   : la chair dessous (morceaux dépareillés, coutures). Alpha 1 = chair, 0 = couture.
//   UV1 (TEXCOORD1)   : rgb = couleur de la fourrure, a = seuil (0 à 1) : la fourrure de ce sommet
//                       disparaît quand _Fur passe sous ce seuil → elle part par plaques.
//   UV2 (TEXCOORD2).x : réseau de veines (0 à 1), révélé par l'infection.
//   UV2 (TEXCOORD2).y : 1 sur le bourrelet de chair des sutures entre membres (luit quand le membre est abîmé).
//   Sutures : UV1.a = 2 (jamais de fourrure) ; fil des points en alpha 0 (reste sombre).
//
// Réglages par membre (CatBody, via MaterialPropertyBlock) :
//   _Fur       1 = fourrure intacte, 0 = toute la chair cousue à nu (suit la vie du membre)
//   _Damage    la chair vire à vif
//   _Bleed     hémorragie : la chair rougit et pulse
//   _Infection infection : veines violacées qui s'étendent, teinte malade
//   _Flash     flash blanc (parade réussie)
// Sans données de fourrure (UV1 vide) et avec _Fur = 0, rendu identique à l'ancienne chair.
Shader "Custom/FleshLit"
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
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        #include "FleshCore.cginc"

        struct Input
        {
            float4 color : COLOR;
            float4 furData;   // rgb fourrure, a seuil
            float2 vein;     // x : veines, y : bourrelet des sutures
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.furData = v.texcoord1;
            o.vein = v.texcoord2.xy;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            FleshLook look = ComputeFlesh(IN.color, IN.furData, IN.vein);
            o.Albedo = look.albedo;
            o.Metallic = 0;
            o.Smoothness = look.smoothness;
            o.Emission = look.emission;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
