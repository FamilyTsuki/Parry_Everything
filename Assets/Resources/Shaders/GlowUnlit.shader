// Parties lumineuses du chat (yeux, flamme de la queue) : non éclairées, couleur des sommets × teinte × intensité.
// Opaque (écrit la profondeur) : les pupilles posées devant les yeux s'affichent sans souci d'ordre.
Shader "Custom/GlowUnlit"
{
    Properties
    {
        _Color ("Teinte", Color) = (1, 1, 1, 1)
        _Intensity ("Intensité", Range(0, 4)) = 1.3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                UNITY_FOG_COORDS(0)
            };

            fixed4 _Color;
            half _Intensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color * _Color * _Intensity;
                c.a = 1;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
