// Calcul commun aux shaders du corps du chat (FleshLit, FleshFur, FleshToon, FleshFacet) :
// chair cousue sous la fourrure, état du membre (abîmé, hémorragie, infection, flash).
// Données des sommets et réglages : voir l'en-tête de FleshLit.shader.
//
// Membre abîmé (_Damage 0 → 1, fondu lentement par CatBody) — pas de teinte rouge :
//  - la fourrure tombe par plaques (_Fur) et montre la chair cousue, dans ses propres couleurs ;
//  - usure terne : le membre se désature et s'assombrit, la fourrure grisonne ;
//  - plaies aux impacts (_Wounds) : entaille sombre et meurtrissure brune, collées à la peau ;
//  - coutures qui lâchent : des points de fil sautent, le bourrelet des sutures s'ouvre en fente sombre.
// Les plaies demandent la position de repos (UV3) : FLESH_NO_DETAIL les coupe (couches de poils).
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
float4 _Wounds[4];   // xyz : centre (espace de repos du maillage), w : graine (> 0) ; w = 0 : pas de plaie

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

// rest.xyz : position de repos du sommet (UV3) ; rest.w : réseau de veines (UV2.x)
// Sutures (CatSutures) : pas de fourrure (furData.a = 2), donc furData.rg porte leurs données :
// r = 1 sur le bourrelet, g = numéro d'un point de fil (0 ailleurs). Place comptée : pas de donnée de plus à transmettre.
FleshLook ComputeFlesh(float4 color, float4 furData, float4 rest)
{
    FleshLook look;
    float2 detail = furData.a > 1.5 ? furData.rg : float2(0, 0);
    float vein = rest.w;
    half lip = saturate(detail.x);
    half damage = saturate(_Damage);
    half fleshMask = color.a;   // 0 sur les coutures

    // Coutures qui lâchent : des points de fil sautent (les plus fragiles d'abord)
    if (detail.y > 0.0) clip(detail.y - saturate((damage - 0.2) * 0.9));

    // Chair cousue dans ses propres couleurs, fourrure par-dessus (elle tombe par plaques)
    fixed3 flesh = color.rgb * _Color.rgb;
    half fur = FurCoverage(furData.a);
    // Usure : la fourrure grisonne
    fixed3 furColor = furData.rgb * _Color.rgb;
    furColor = lerp(furColor, dot(furColor, fixed3(0.3, 0.59, 0.11)).xxx * fixed3(0.88, 0.85, 0.8) + 0.04, damage * 0.55);
    fixed3 c = lerp(flesh, furColor, fur);

    // Usure terne : le membre se désature et s'assombrit
    half grey = dot(c, fixed3(0.3, 0.59, 0.11));
    c = lerp(c, grey.xxx * fixed3(0.84, 0.8, 0.76), damage * 0.5);
    c *= lerp(1.0, 0.72, damage);

    // Bourrelet des sutures : s'ouvre en fente sombre
    c = lerp(c, fixed3(0.06, 0.012, 0.016), lip * damage * 0.85);

#ifndef FLESH_NO_DETAIL
    float3 p = rest.xyz;

    // Plaies aux impacts : entaille sombre allongée, meurtrissure brune autour
    for (int i = 0; i < 4; i++)
    {
        float4 w = _Wounds[i];
        if (w.w <= 0.0) continue;
        float3 v = p - w.xyz;
        float3 axis = normalize(float3(sin(w.w * 12.9), cos(w.w * 7.3), sin(w.w * 3.1)) + 0.001);
        float along = clamp(dot(v, axis), -0.06, 0.06);
        float gash = length(v - axis * along) / lerp(1.0, 0.35, abs(along) / 0.06);   // entaille en fuseau
        half bruise = 1.0 - smoothstep(0.03, 0.11, length(v));
        c = lerp(c, c * fixed3(0.5, 0.36, 0.3), bruise * 0.7);
        half cut = 1.0 - smoothstep(0.008, 0.014, gash);
        c = lerp(c, fixed3(0.05, 0.01, 0.015), cut);
    }
#endif

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
    // Chair humide, fourrure mate ; l'usure ternit tout
    half fleshGloss = _Glossiness * lerp(0.6, 1.0, fleshMask);
    look.smoothness = lerp(fleshGloss, _FurGlossiness, fur) * lerp(1.0, 0.5, damage);
    look.emission = _EmissionColor.rgb + _Flash.xxx + _BleedColor.rgb * bleed * 0.25 + _InfectColor.rgb * veins * inf * 0.3;
    look.fur = fur;
    return look;
}

#endif
