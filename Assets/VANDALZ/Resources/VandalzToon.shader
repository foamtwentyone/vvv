// Мультяшный («комиксный») шейдер VANDALZ для встроенного конвейера (Built-in).
//  - ступенчатое (cel) освещение: свет ложится тремя полосами, тени — пятнами;
//  - чёрный «чернильный» контур (inverted hull): копия меша, раздутая на N пикселей, вывернутая наизнанку;
//  - свечение (_EmissionColor) — для ламп и неоновой краски (подхватывает bloom камеры);
//  - туман.
// _OutlineFromCenter: 1 — раздувать от центра объекта (коробки, примитивы — контур без разрывов на углах),
//                     0 — по нормалям (гладкие меши с центром не в объекте, например мазки краски).
Shader "VANDALZ/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _ShadowColor ("Shadow Tint", Color) = (0.55,0.6,0.75,1)
        _OutlineColor ("Outline Color", Color) = (0.03,0.025,0.05,1)
        _OutlineWidth ("Outline Width (px)", Float) = 2.2
        _OutlineFromCenter ("Outline From Center", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        // ---------- Контур ----------
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineFromCenter;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 fromCenter = v.vertex.xyz;
                if (dot(fromCenter, fromCenter) < 1e-8) fromCenter = v.normal;
                float3 dir = normalize(lerp(v.normal, normalize(fromCenter), _OutlineFromCenter));

                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float3 worldDir = mul((float3x3)unity_ObjectToWorld, dir);
                float2 clipDir = mul((float3x3)UNITY_MATRIX_VP, worldDir).xy;
                float len = length(clipDir);
                if (len > 1e-6)
                {
                    // Сдвиг на постоянное число пикселей независимо от расстояния.
                    clipPos.xy += (clipDir / len) * (_OutlineWidth * 2.0 / _ScreenParams.xy) * clipPos.w;
                }
                o.pos = clipPos;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _OutlineColor;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        // ---------- Сам объект: cel-освещение ----------
        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _EmissionColor;
        fixed4 _ShadowColor;

        struct Input
        {
            float2 uv_MainTex;
        };

        // Три полосы света вместо плавного градиента + холодный оттенок в тени.
        half Bands (half x)
        {
            return smoothstep(0.02, 0.06, x) * 0.35
                 + smoothstep(0.30, 0.36, x) * 0.40
                 + smoothstep(0.62, 0.68, x) * 0.25;
        }

        half4 LightingToon (SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir) * 0.5 + 0.5; // «половинчатый» Ламберт — мягче для мультяшного вида
            half lit = Bands(saturate(ndl * atten));
            half3 shade = lerp(_ShadowColor.rgb * 0.35, half3(1, 1, 1), lit);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * shade * lit;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
