// HUD 유리판 셰이더 (UIManager.HudGlass / HudPill가 쓴다): 뒤 화면(URP 불투명 텍스처)을 흐려 비추는 uGUI 그림 셰이더.
// 모양과 가장자리 페더는 스프라이트 알파, 색은 Image.color × _Color. _Darken만큼 바탕색을 섞고,
// 스프라이트 R채널(비스듬한 면 높이)의 화면 기울기로 가장자리를 굴절시킨다(_Refract, 알약 핫바).
// 화면 좌표는 오버레이 캔버스의 월드 좌표(= 픽셀)에서 구하므로 렌더 타깃이 뒤집혀도(에디터 게임 뷰·빌드) 같은 결과다.
// _Backdrop이 0이면(불투명 텍스처를 만들지 않는 파이프라인 설정) 바탕색만 칠한다
Shader "BunkerLab/HudGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Blur ("Blur Spread (texels)", Range(0, 4)) = 2
        _Darken ("Tint Amount", Range(0, 1)) = 0.6
        _Refract ("Edge Refraction", Range(0, 0.2)) = 0
        _Backdrop ("Backdrop", Range(0, 1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 screenUV : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            sampler2D _CameraOpaqueTexture;
            float4 _CameraOpaqueTexture_TexelSize;
            float _Blur;
            float _Darken;
            float _Refract;
            float _Backdrop;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                // 오버레이 캔버스의 월드 좌표는 화면 픽셀 좌표(왼쪽 아래가 0)다
                float2 pixel = mul(unity_ObjectToWorld, v.vertex).xy;
                OUT.screenUV = pixel / _ScreenParams.xy;
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 spr = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                float a = spr.a * IN.color.a;

                // 가장자리 굴절: 비스듬한 면 높이(R)의 화면 기울기만큼 뒤 화면을 당겨 찍는다
                float2 uv = IN.screenUV;
                float2 grad = float2(ddx(spr.r), ddy(spr.r));
                uv -= grad * _Refract;

                // 9×9 가우시안(이항 가중치). 반 텍셀 어긋나게 찍어 bilinear로 두 배 더 부드럽게
                float2 t = _CameraOpaqueTexture_TexelSize.xy * _Blur;
                float2 halfTexel = _CameraOpaqueTexture_TexelSize.xy * 0.5;
                static const float w[9] = { 1, 8, 28, 56, 70, 56, 28, 8, 1 };
                half3 acc = half3(0, 0, 0);
                float wsum = 0;
                [unroll]
                for (int y = 0; y < 9; y++)
                {
                    [unroll]
                    for (int x = 0; x < 9; x++)
                    {
                        float wt = w[x] * w[y];
                        float2 o = float2(x - 4, y - 4) * t + halfTexel;
                        acc += tex2D(_CameraOpaqueTexture, uv + o).rgb * wt;
                        wsum += wt;
                    }
                }
                acc /= wsum;
                acc = acc / (1 + acc); // 밝은 빛이 하얗게 뭉치지 않게 (간단한 톤매핑)

                float dark = max(_Darken, 1 - _Backdrop);
                half3 col = lerp(acc, IN.color.rgb, dark);

                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(a - 0.001);
                #endif

                return fixed4(col * a, a);
            }
            ENDCG
        }
    }
}
