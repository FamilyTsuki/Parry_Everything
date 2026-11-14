// Corps du chat : fourrure par-dessus de la chair cousue. L'état du membre se lit sur sa surface.
//
// Données des sommets (écrites par CatModelBuilder) :
//   couleur (COLOR)   : la chair dessous (morceaux dépareillés, coutures). Alpha 1 = chair, 0 = couture.
//   UV1 (TEXCOORD1)   : rgb = couleur de la fourrure, a = seuil (0 à 1) : la fourrure de ce sommet
//                       disparaît quand _Fur passe sous ce seuil → elle part par plaques.
//   UV2 (TEXCOORD2).x : réseau de veines (0 à 1), révélé par l'infection.
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

        struct Input
        {
            float4 color : COLOR;
            float4 furData;   // rgb fourrure, a seuil
            float vein;
        };

        fixed4 _Color;
        fixed4 _RawColor;
        fixed4 _BleedColor;
        fixed4 _InfectColor;
        half _Fur;
        half _Damage;
        half _Bleed;
        half _Infection;
        half _Flash;
        half _Glossiness;
        half _FurGlossiness;
        fixed4 _EmissionColor;

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.furData = v.texcoord1;
            o.vein = v.texcoord2.x;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half fleshMask = IN.color.a;   // 0 sur les coutures

            // Chair : abîmée, elle vire à vif (les coutures restent claires)
            fixed3 flesh = IN.color.rgb * _Color.rgb;
            flesh = lerp(flesh, _RawColor.rgb, saturate(_Damage) * fleshMask);

            // Fourrure : couvre la chair là où son seuil est sous _Fur (bord net, un peu adouci)
            half fur = saturate((_Fur * 1.1 - IN.furData.a) * 10);
            fixed3 c = lerp(flesh, IN.furData.rgb * _Color.rgb, fur);

            // Hémorragie : la chair à nu rougit et pulse ; la fourrure se tache
            half pulse = 0.5 + 0.5 * sin(_Time.y * 6.0);
            half bleed = saturate(_Bleed) * lerp(0.35, 0.6, pulse) * lerp(fleshMask, 0.4, fur);
            c = lerp(c, _BleedColor.rgb, bleed);

            // Infection : le réseau de veines apparaît, de plus en plus étendu, et la teinte devient malade
            half inf = saturate(_Infection);
            half veins = saturate((IN.vein - (1.0 - inf * 0.75)) * 6.0) * step(0.001, inf);
            c = lerp(c, c * fixed3(0.8, 0.75, 0.95), inf * 0.4);
            c = lerp(c, _InfectColor.rgb, veins * 0.85);

            o.Albedo = lerp(c, fixed3(1, 1, 1), _Flash);
            o.Metallic = 0;
            // Chair humide (plus brillante à vif) ; fourrure mate
            half fleshGloss = _Glossiness * lerp(0.6, 1.0, fleshMask) + _Damage * 0.2 * fleshMask;
            o.Smoothness = lerp(fleshGloss, _FurGlossiness, fur);
            o.Emission = _EmissionColor.rgb + _Flash.xxx + _BleedColor.rgb * bleed * 0.25 + _InfectColor.rgb * veins * inf * 0.3;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
