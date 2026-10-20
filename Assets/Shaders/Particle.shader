// Частицы для огня, искр, дыма и радиации: текстура × цвет вершины × _Color.
// Режим смешивания задаётся материалом (_SrcBlend/_DstBlend): аддитивный для свечения (One One)
// или обычная прозрачность для дыма (SrcAlpha OneMinusSrcAlpha). Туман гасит частицы с расстоянием.
// CG без LightMode — работает и во встроенном рендере, и в URP, как Nuclear/Outline.
Shader "Nuclear/Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float fog : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                float3 view = UnityObjectToViewPos(v.vertex);
                // Экспоненциальный квадратичный туман, как в сцене (unity_FogParams.y = плотность / sqrt(ln 2)).
                float f = unity_FogParams.y * length(view);
                o.fog = saturate(exp2(-f * f));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * i.color;
                // Туман гасит частицу через альфу: при SrcAlpha и для свечения, и для дыма.
                c.a *= i.fog;
                return c;
            }
            ENDCG
        }
    }
}
