// Мультяшный контур методом «вывернутой оболочки»: меш с усреднёнными нормалями выдавливается наружу,
// рисуется только его изнанка (Cull Front) тёмным цветом — вокруг силуэта остаётся обводка.
// Толщина растёт с расстоянием до камеры (на экране линия почти постоянной ширины), но не бесконечно.
// Туман считается вручную по unity_FogParams/unity_FogColor: их задают и встроенный рендер, и URP,
// поэтому шейдер работает в обоих конвейерах без вариантов.
Shader "Nuclear/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (0.17, 0.12, 0.1, 1)
        _Width ("Width per metre of distance", Float) = 0.005
        _MaxDistance ("Width stops growing after (m)", Float) = 18
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;
            float _MaxDistance;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float fog : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float distanceToCamera = distance(world, _WorldSpaceCameraPos);
                world += normal * _Width * clamp(distanceToCamera, 1.0, _MaxDistance);
                o.position = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                // Экспоненциальный туман: exp2(-y * d), где y = плотность / ln 2.
                o.fog = saturate(exp2(-unity_FogParams.y * distanceToCamera));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(lerp(unity_FogColor.rgb, _Color.rgb, i.fog), 1.0);
            }
            ENDCG
        }
    }
}
