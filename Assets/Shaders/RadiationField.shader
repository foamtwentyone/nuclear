// Купол радиоактивного поля: светящаяся зелёная «аврора» на поверхности купола.
// Ярче к краю силуэта (френель), снизу у земли гуще, вверх бегут рваные полосы шума,
// всё медленно пульсирует. Аддитивно, без записи глубины, видно и изнутри (Cull Off).
Shader "Nuclear/RadiationField"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.45, 1.4, 0.3, 1)
        _Strength ("Strength", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color;
            half _Strength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float height : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.height = saturate(v.vertex.y);
                float f = unity_FogParams.y * length(UnityObjectToViewPos(v.vertex));
                o.fog = saturate(exp2(-f * f));
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i + float3(0, 0, 0)), hash(i + float3(1, 0, 0)), f.x),
                                 lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x),
                                 lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float3 n = normalize(i.normal);
                // Изнутри купола видна его изнанка — берём модуль, чтобы край светился и оттуда.
                float facing = abs(dot(n, view));
                float rim = pow(1.0 - facing, 2.5);

                float t = _Time.y;
                // Полосы «авроры»: шум вытянут по вертикали и бежит вверх.
                float3 q = i.world * float3(0.9, 0.25, 0.9) + float3(0.0, -t * 0.35, 0.0);
                float bands = noise(q) * 0.65 + noise(q * 2.3 + float3(t * 0.1, 0.0, 0.0)) * 0.35;
                bands = smoothstep(0.35, 0.85, bands);

                // Гуще у земли, к макушке тает.
                float ground = pow(1.0 - i.height, 2.0);
                float pulse = 0.85 + 0.15 * sin(t * 1.7 + i.world.x * 0.3);

                float glow = (rim * 0.9 + bands * 0.55 * (0.3 + rim) + ground * 0.25) * pulse * _Strength;
                return half4(_Color.rgb, saturate(glow * i.fog));
            }
            ENDCG
        }
    }
}
