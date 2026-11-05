// 웅덩이 (PuddleFx.cs가 전역값을 넣는다): 어두운 물 위에 바람의 잔물결, 발을 디딘 자리에서 퍼지는 둥근 물결(_PuddleRipples),
// 비가 올 때의 빗방울 물결(_PuddleRain). 달빛(주 방향광)의 날카로운 반사와 손전등·전구 같은 점광원의 반사점, 하늘의 프레넬 반사가
// 물결을 따라 일렁인다. 얼룩(부채꼴) 메시의 가장자리는 바닥에 부드럽게 녹아든다 (상자 메시는 그대로 불투명)
Shader "BunkerLab/Puddle"
{
    Properties
    {
        _BaseColor ("Water Tint", Color) = (0.1, 0.11, 0.13, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _WaveAmp ("Wind Ripple", Range(0, 0.1)) = 0.0025
        _RippleAmp ("Step Ripple", Range(0, 1)) = 0.1
        _RainAmp ("Rain Ripple", Range(0, 1)) = 0.025
        _SkyBoost ("Reflection", Range(0, 4)) = 1.2
        _EdgeFade ("Edge Fade", Range(0.01, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-30" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Puddle"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _WaveAmp;
                half _RippleAmp;
                half _RainAmp;
                half _SkyBoost;
                half _EdgeFade;
            CBUFFER_END

            #define MAX_RIPPLES 16
            float4 _PuddleRipples[MAX_RIPPLES]; // x,z 자리 / y 시작 시각 / w 세기
            float _PuddleRain;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 fanFog : TEXCOORD1; // x: 부채꼴 얼룩 메시에서 가운데 0 → 가장자리 1, y: 안개
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fanFog.x = dot(v.positionOS.xz, v.positionOS.xz) > 1e-6 ? 1.0 : 0.0;
                o.fanFog.y = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 물면 높이의 기울기 (x: d/dx, y: d/dz). 작을수록 잔잔하다
            float2 Slope(float2 p, float t)
            {
                float2 g = 0;
                // 바람의 잔물결: 방향이 다른 사인파 셋
                float2 d1 = normalize(float2(0.8, 0.6)), d2 = normalize(float2(-0.5, 0.85)), d3 = normalize(float2(0.3, -0.95));
                g += d1 * cos(dot(d1, p) * 6.0 + t * 1.4) * 6.0 * _WaveAmp;
                g += d2 * cos(dot(d2, p) * 11.0 - t * 2.1) * 11.0 * _WaveAmp * 0.6;
                g += d3 * cos(dot(d3, p) * 17.0 + t * 2.9) * 17.0 * _WaveAmp * 0.35;
                // 발을 디딘 자리: 퍼져 나가는 고리 (반지름은 나이에 비례, 오래될수록 잦아든다)
                [unroll]
                for (int i = 0; i < MAX_RIPPLES; i++)
                {
                    float4 r = _PuddleRipples[i];
                    float age = t - r.y;
                    if (age < 0.0 || age > 2.5 || r.w <= 0.0) continue;
                    float2 dp = p - r.xz;
                    float dist = length(dp) + 1e-4;
                    float radius = age * 1.1;
                    float ring = exp(-pow((dist - radius) * 9.0, 2.0)) * sin((dist - radius) * 40.0);
                    float fade = exp(-age * 1.6) * r.w;
                    g += (dp / dist) * ring * fade * _RippleAmp * 6.0;
                }
                // 빗방울: 격자 칸마다 제 박자로 떨어지는 작은 고리
                if (_PuddleRain > 0.5)
                {
                    float2 cell = floor(p * 3.0);
                    [unroll]
                    for (int y = -1; y <= 1; y++)
                    {
                        [unroll]
                        for (int x = -1; x <= 1; x++)
                        {
                            float2 c = cell + float2(x, y);
                            float h = hash21(c);
                            float period = 0.9 + h * 0.8;
                            float phase = frac(t / period + h * 7.0) * period;
                            float2 center = (c + float2(hash21(c + 3.1), hash21(c + 7.7))) / 3.0;
                            float2 dp = p - center;
                            float dist = length(dp) + 1e-4;
                            float radius = phase * 0.9;
                            float ring = exp(-pow((dist - radius) * 14.0, 2.0)) * sin((dist - radius) * 60.0);
                            g += (dp / dist) * ring * exp(-phase * 3.0) * _RainAmp * 6.0;
                        }
                    }
                }
                return g;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.positionWS.xz;
                float2 g = Slope(p, t);
                float3 N = normalize(float3(-g.x, 1.0, -g.y));
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float gloss = lerp(40.0, 700.0, _Smoothness);

                // 달빛: 날카로운 반사점 + 아주 약한 확산
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 H = normalize(mainLight.direction + V);
                float ndh = saturate(dot(N, H));
                // 날카로운 반사점 + 넓게 번지는 광택 (물결이 광택을 흔들어 일렁임이 보인다)
                float3 spec = (pow(ndh, gloss) * 1.6 + pow(ndh, 24.0) * 0.14) * mainLight.color * mainLight.shadowAttenuation;
                float3 diffuse = _BaseColor.rgb * (SampleSH(N) + mainLight.color * mainLight.shadowAttenuation * saturate(dot(N, mainLight.direction)) * 0.3);

                // 점광원 (손전등·전구·불): 작은 반사점
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, i.positionWS);
                    float atten = light.distanceAttenuation * light.shadowAttenuation;
                    float3 Hl = normalize(light.direction + V);
                    float ndhl = saturate(dot(N, Hl));
                    spec += (pow(ndhl, gloss * 0.6) + pow(ndhl, 16.0) * 0.1) * light.color * atten;
                    diffuse += _BaseColor.rgb * light.color * atten * saturate(dot(N, light.direction)) * 0.35;
                LIGHT_LOOP_END
                #endif

                // 하늘·주변 반사 (프레넬: 비스듬히 볼수록 거울처럼). 하늘이 없는 밤 도시에서는 안개색(멀리 보이는 하늘빛)을 바닥값으로 쓴다
                float3 R = reflect(-V, N);
                float3 sky = max(GlossyEnvironmentReflection(R, 1.0 - _Smoothness, 1.0), unity_FogColor.rgb * 0.8) * _SkyBoost;
                float fres = 0.03 + 0.97 * pow(1.0 - saturate(dot(N, V)), 5.0);
                float3 color = lerp(diffuse, sky, fres) + spec;
                color = MixFog(color, i.fanFog.y);

                // 가장자리 흐리기: 부채꼴 얼룩 메시(가운데 꼭짓점 하나)에서만. 값이 면 전체에서 같으면(상자) 그대로 둔다
                float fan = i.fanFog.x;
                float alpha = fwidth(fan) < 1e-6 ? 1.0 : saturate((1.0 - fan) / _EdgeFade);
                return half4(color, alpha * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
