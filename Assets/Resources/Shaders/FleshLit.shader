// Chair cousue (corps du chat) : éclairage standard, couleur portée par les sommets du modèle
// (morceaux dépareillés, coutures). _Damage fait virer la chair à vif, _Flash la blanchit (parade réussie).
// Alpha des sommets : 1 = chair, 0 = couture (ne vire pas au rouge, reste claire).
Shader "Custom/FleshLit"
{
    Properties
    {
        _Color ("Teinte", Color) = (1, 1, 1, 1)
        _RawColor ("Chair à vif", Color) = (0.8, 0.15, 0.13, 1)
        _Damage ("Abîmé (0 à 1)", Range(0, 1)) = 0
        _Flash ("Flash blanc (0 à 1)", Range(0, 1)) = 0
        _Glossiness ("Brillance", Range(0, 1)) = 0.45
        _EmissionColor ("Lueur", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        fixed4 _Color;
        fixed4 _RawColor;
        half _Damage;
        half _Flash;
        half _Glossiness;
        fixed4 _EmissionColor;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 c = IN.color.rgb * _Color.rgb;
            // La chair abîmée vire à vif ; les coutures restent claires
            c = lerp(c, _RawColor.rgb, saturate(_Damage) * IN.color.a);
            o.Albedo = lerp(c, fixed3(1, 1, 1), _Flash);
            o.Metallic = 0;
            // Chair humide : plus brillante quand elle est à vif
            o.Smoothness = _Glossiness * lerp(0.6, 1.0, IN.color.a) + _Damage * 0.2 * IN.color.a;
            o.Emission = _EmissionColor.rgb + _Flash.xxx;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
