// Une couche de poils (« shell fur ») de FleshFur.shader. Inclus une fois par couche, avec SHELL_K défini
// (0 = racine, 1 = pointe) : le maillage est regonflé le long des normales et seuls les poils restent visibles.
// Les poils poussent seulement là où la fourrure est présente (FurCoverage) : un membre abîmé les perd par plaques.
#include "FleshCore.cginc"

half _FurLength;     // longueur des poils (mètres)
half _FurDensity;    // poils par mètre
half _FurGravity;    // les poils retombent vers le bas
half _FurRootShade;  // assombrissement à la racine (fausse occlusion)

struct Input
{
    float4 color : COLOR;
    float4 furData;
    float vein;      // veines (le bourrelet des sutures n'a pas de fourrure : inutile ici, place comptée)
    float3 strandPos;   // position d'origine (repère du mesh) : les poils ne glissent pas quand la couche gonfle
};

void vertShell (inout appdata_full v, out Input o)
{
    UNITY_INITIALIZE_OUTPUT(Input, o);
    o.furData = v.texcoord1;
    o.vein = v.texcoord2.x;
    o.strandPos = v.vertex.xyz;
    half length = _FurLength * FurCoverage(v.texcoord1.a);
    float3 down = mul((float3x3)unity_WorldToObject, float3(0, -1, 0));
    v.vertex.xyz += v.normal * length * SHELL_K + down * length * _FurGravity * SHELL_K * SHELL_K;
}

float StrandHash (float3 cell)
{
    return frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

void surfShell (Input IN, inout SurfaceOutputStandard o)
{
    FleshLook look = ComputeFlesh(IN.color, IN.furData, float2(IN.vein, 0));
    // Pas de poil sur la chair à nu
    clip(look.fur - 0.5);

    // Poil : un par cellule, épais à la racine et effilé vers la pointe ; les plus courts s'arrêtent avant
    float3 p = IN.strandPos * _FurDensity;
    float3 cell = floor(p);
    float h = StrandHash(cell);
    float d = length(frac(p) - 0.5) * 2.0;
    clip(h - SHELL_K * 0.85);
    clip((1.0 - SHELL_K) * 1.25 - d);

    o.Albedo = look.albedo * lerp(_FurRootShade, 1.0, SHELL_K);
    o.Metallic = 0;
    o.Smoothness = _FurGlossiness;
    o.Emission = look.emission;
}
