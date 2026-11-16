// Corps du chat, version « fourrure douce » : même chair et même état des membres que FleshLit,
// plus 12 couches de poils (« shell fur », FleshFurShell.cginc) au lieu de touffes taillées dans le maillage.
// Les poils ne poussent que sur la fourrure présente : un membre abîmé les perd par plaques (_Fur).
Shader "Custom/FleshFur"
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
        _FurLength ("Longueur des poils (m)", Range(0, 0.15)) = 0.065
        _FurDensity ("Poils par mètre", Range(20, 600)) = 110
        _FurGravity ("Poils qui retombent", Range(0, 2)) = 0.6
        _FurRootShade ("Ombre à la racine", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        // Base : la peau (chair + sous-poil)
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        #include "FleshCore.cginc"

        struct Input
        {
            float4 color : COLOR;
            float4 furData;
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
            o.Albedo = look.albedo * lerp(1.0, 0.7, look.fur);   // sous-poil un peu plus sombre entre les poils
            o.Metallic = 0;
            o.Smoothness = look.smoothness;
            o.Emission = look.emission;
        }
        ENDCG

        // Couche 1 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.0833
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 2 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.1667
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 3 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.2500
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 4 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.3333
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 5 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.4167
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 6 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.5000
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 7 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.5833
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 8 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.6667
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 9 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.7500
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 10 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.8333
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 11 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 0.9167
        #include "FleshFurShell.cginc"
        ENDCG

        // Couche 12 / 12
        CGPROGRAM
        #pragma surface surfShell Standard vertex:vertShell
        #pragma target 3.0
        #define SHELL_K 1.0000
        #include "FleshFurShell.cginc"
        ENDCG
    }
    FallBack "Diffuse"
}
