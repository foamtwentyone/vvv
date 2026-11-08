// Пост-обработка камеры VANDALZ (Built-in): свечение неона (bloom), цвет, виньетка, зерно.
// Используется компонентом VandalzPost. Проходы:
//  0 — выделить яркое (порог), 1 — уменьшить, 2 — увеличить и сложить, 3 — итог.
Shader "Hidden/VANDALZ/Post"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _BloomTex;
    float4 _MainTex_TexelSize;
    half _Threshold;
    half _SoftKnee;
    half _Intensity;
    half _Vignette;
    half _Contrast;
    half _Saturation;
    half _Grain;
    half4 _Tint;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
    }

    half3 SampleBox (float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half3 Prefilter (half3 c)
    {
        half brightness = max(c.r, max(c.g, c.b));
        half knee = _Threshold * _SoftKnee;
        half soft = clamp(brightness - _Threshold + knee, 0, 2 * knee);
        soft = soft * soft / (4 * knee + 0.00001);
        half contribution = max(soft, brightness - _Threshold) / max(brightness, 0.00001);
        return c * contribution;
    }
    ENDCG

    SubShader
    {
        Cull Off ZTest Always ZWrite Off

        Pass // 0: порог
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                return half4(Prefilter(SampleBox(i.uv, 1)), 1);
            }
            ENDCG
        }

        Pass // 1: уменьшение
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                return half4(SampleBox(i.uv, 1), 1);
            }
            ENDCG
        }

        Pass // 2: увеличение со сложением
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                return half4(SampleBox(i.uv, 0.5), 1);
            }
            ENDCG
        }

        Pass // 3: итог — картинка + свечение, цвет, виньетка, зерно
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                half4 src = tex2D(_MainTex, i.uv);
                float2 bloomUV = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) bloomUV.y = 1 - bloomUV.y;
                #endif
                half3 col = src.rgb + tex2D(_BloomTex, bloomUV).rgb * _Intensity;

                col *= _Tint.rgb;
                half lum = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(lum.xxx, col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;

                float2 d = i.uv - 0.5;
                col *= saturate(1 - dot(d, d) * _Vignette);

                float n = frac(sin(dot(i.uv * _ScreenParams.xy + _Time.y * 61.0, float2(12.9898, 78.233))) * 43758.5453);
                col += (n - 0.5) * _Grain;

                return half4(max(col, 0), src.a);
            }
            ENDCG
        }
    }
}
