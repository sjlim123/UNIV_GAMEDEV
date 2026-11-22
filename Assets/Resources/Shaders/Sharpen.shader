// 선명도 향상 (설정 > 그래픽 > 선명도 향상, 2026-11-22). 대비에 맞춰 세기를 바꾸는 날카롭게 하기(CAS 방식):
// 이미 대비가 큰 곳(글자 테두리·밝은 불빛)은 덜, 흐릿한 곳(먼 벽·바닥 무늬)은 더 날카롭게 해 하얀 테두리가 생기지 않게 한다.
// SharpenPass.cs가 후처리 앞에 화면 전체에 한 번 그린다 (그레인·FXAA·업스케일이 뒤에 온다). _Amount 0~1
Shader "BunkerLab/Sharpen"
{
    Properties
    {
        _Amount ("Amount", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "Sharpen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Amount;

            // 후처리 앞이라 색이 HDR(1 넘음)이다: x/(1+x)로 0~1에 눌러 넣고 날카롭게 한 뒤 되돌린다 (밝은 불빛 둘레에 테두리가 생기지 않게)
            float3 Squash(float3 x) { return x / (1.0 + x); }
            float3 Unsquash(float3 y) { return y / max(1.0 - y, 1e-4); }
            float3 Tap(float2 uv) { return Squash(max(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb, 0)); }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 t = _BlitTexture_TexelSize.xy;
                float4 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float3 c = Squash(max(center.rgb, 0));
                float3 n = Tap(uv + float2(0, t.y));
                float3 s = Tap(uv - float2(0, t.y));
                float3 e = Tap(uv + float2(t.x, 0));
                float3 w = Tap(uv - float2(t.x, 0));
                float3 mn = min(c, min(min(n, s), min(e, w)));
                float3 mx = max(c, max(max(n, s), max(e, w)));
                // 대비가 작을수록 1, 클수록 0에 가까운 세기
                float3 amp = sqrt(saturate(min(mn, 1.0 - mx) / max(mx, 1e-4)));
                float peak = -lerp(0.125, 0.2, saturate(_Amount));
                float3 wv = amp * peak;
                float3 res = saturate((c + (n + s + e + w) * wv) / (1.0 + 4.0 * wv));
                return float4(Unsquash(min(res, 0.999)), center.a);
            }
            ENDHLSL
        }
    }
}
