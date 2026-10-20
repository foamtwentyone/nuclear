// Мультяшное освещение для URP в духе Peak и Pratfall: свет ложится несколькими ровными ступенями
// с мягкими краями, тень слегка окрашена вместо серой, по освещённому силуэту — светлый ободок.
// По яркости не светлее URP Lit: те же солнце + небо, только ступенями.
// Тени, глубина и нормали для SSAO берутся готовыми проходами из URP Lit.
Shader "Nuclear/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1

        _Steps ("Light steps", Range(1, 6)) = 3
        _StepSoftness ("Step softness", Range(0.01, 1)) = 0.12
        _ShadowTint ("Shadow tint", Color) = (0.82, 0.76, 0.95, 1)
        _AmbientStrength ("Ambient strength", Range(0, 2)) = 1
        _RimColor ("Rim color", Color) = (1, 0.95, 0.85, 1)
        _RimStrength ("Rim strength", Range(0, 1)) = 0.35
        _RimWidth ("Rim width", Range(0, 1)) = 0.3

        // Нужны проходам, взятым из URP Lit.
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Smoothness;
                half _Steps;
                half _StepSoftness;
                half4 _ShadowTint;
                half _AmbientStrength;
                half4 _RimColor;
                half _RimStrength;
                half _RimWidth;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Ступенчатая «лесенка» освещённости 0..1 с мягкими краями ступеней.
            half Ramp(half light)
            {
                half steps = max(1.0h, _Steps);
                half x = saturate(light) * steps;
                half band = floor(x);
                half edge = smoothstep(1.0h - _StepSoftness, 1.0h, frac(x));
                return saturate((band + edge) / steps);
            }

            // Освещённость источником 0..1 ступенями. Немного «обёрнута» за терминатор, чтобы ступени
            // ложились на форму мягче, но тёмная сторона остаётся тёмной.
            half ToonDiffuse(Light light, half3 normalWS)
            {
                half wrapped = saturate((dot(normalWS, light.direction) + 0.15h) / 1.15h);
                return Ramp(wrapped * light.shadowAttenuation);
            }

            // Блик — одно чёткое пятно, только у гладких поверхностей (металл, стекло, краска).
            half ToonSpecular(Light light, half3 normalWS, half3 viewWS)
            {
                half3 halfDir = SafeNormalize(light.direction + viewWS);
                half specularPower = exp2(10.0h * _Smoothness + 1.0h);
                return smoothstep(0.5h, 0.56h, pow(saturate(dot(normalWS, halfDir)), specularPower))
                    * saturate((_Smoothness - 0.2h) * 3.0h) * 0.35h;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = NormalizeNormalPerPixel(
                    TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                half3 viewWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                AmbientOcclusionFactor occlusion = GetScreenSpaceAmbientOcclusion(screenUV);

                // Небо и отражённый свет — по нормали; в тени слегка окрашены (_ShadowTint), а не серые.
                half3 ambient = SampleSH(normalWS) * _AmbientStrength * occlusion.indirectAmbientOcclusion;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half mainLit = ToonDiffuse(mainLight, normalWS) * occlusion.directAmbientOcclusion;
                half3 shadowAmbient = ambient * lerp(_ShadowTint.rgb, 1.0h, mainLit);
                half3 color = albedo.rgb * (shadowAmbient + mainLight.color * mainLit)
                    + mainLight.color * ToonSpecular(mainLight, normalWS, viewWS) * mainLit;

                // Огни и лампы только добавляют свет и гаснут с расстоянием — вне радиуса их не видно.
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    half lit = ToonDiffuse(light, normalWS) * light.distanceAttenuation;
                    color += light.color * lit * (albedo.rgb + ToonSpecular(light, normalWS, viewWS));
                LIGHT_LOOP_END
                #endif

                // Ободок по силуэту, только на освещённой стороне и неяркий.
                half fresnel = 1.0h - saturate(dot(normalWS, viewWS));
                half rim = smoothstep(1.0h - _RimWidth, 1.0h - _RimWidth + 0.08h, fresnel);
                color += _RimColor.rgb * mainLight.color * albedo.rgb * rim * mainLit * _RimStrength;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
