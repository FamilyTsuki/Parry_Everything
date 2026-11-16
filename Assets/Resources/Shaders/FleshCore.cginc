// Calcul commun aux shaders du corps du chat (FleshLit, FleshFur, FleshToon, FleshFacet) :
// chair cousue sous la fourrure, état du membre (abîmé, hémorragie, infection, flash).
// Données des sommets et réglages : voir l'en-tête de FleshLit.shader.
#ifndef FLESH_CORE_INCLUDED
#define FLESH_CORE_INCLUDED

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

struct FleshLook
{
    fixed3 albedo;
    half smoothness;
    fixed3 emission;
    half fur;          // 1 = fourrure visible ici, 0 = chair à nu
};

// Part de fourrure présente à ce sommet : la fourrure disparaît quand _Fur passe sous le seuil (bord net, un peu adouci)
half FurCoverage(half threshold)
{
    return saturate((_Fur * 1.1 - threshold) * 10);
}

FleshLook ComputeFlesh(float4 color, float4 furData, float vein)
{
    FleshLook look;
    half fleshMask = color.a;   // 0 sur les coutures

    // Chair : abîmée, elle vire à vif (les coutures restent claires)
    fixed3 flesh = color.rgb * _Color.rgb;
    flesh = lerp(flesh, _RawColor.rgb, saturate(_Damage) * fleshMask);

    // Fourrure par-dessus
    half fur = FurCoverage(furData.a);
    fixed3 c = lerp(flesh, furData.rgb * _Color.rgb, fur);

    // Hémorragie : la chair à nu rougit et pulse ; la fourrure se tache
    half pulse = 0.5 + 0.5 * sin(_Time.y * 6.0);
    half bleed = saturate(_Bleed) * lerp(0.35, 0.6, pulse) * lerp(fleshMask, 0.4, fur);
    c = lerp(c, _BleedColor.rgb, bleed);

    // Infection : le réseau de veines apparaît, de plus en plus étendu, et la teinte devient malade
    half inf = saturate(_Infection);
    half veins = saturate((vein - (1.0 - inf * 0.75)) * 6.0) * step(0.001, inf);
    c = lerp(c, c * fixed3(0.8, 0.75, 0.95), inf * 0.4);
    c = lerp(c, _InfectColor.rgb, veins * 0.85);

    look.albedo = lerp(c, fixed3(1, 1, 1), _Flash);
    // Chair humide (plus brillante à vif) ; fourrure mate
    half fleshGloss = _Glossiness * lerp(0.6, 1.0, fleshMask) + _Damage * 0.2 * fleshMask;
    look.smoothness = lerp(fleshGloss, _FurGlossiness, fur);
    look.emission = _EmissionColor.rgb + _Flash.xxx + _BleedColor.rgb * bleed * 0.25 + _InfectColor.rgb * veins * inf * 0.3;
    look.fur = fur;
    return look;
}

#endif
