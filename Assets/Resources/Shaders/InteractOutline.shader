// 상호작용 윤곽선 (InteractOutline.cs가 쓴다): 물체 메시를 조금 부풀린 껍데기의 뒷면만 그려
// 물체 둘레에만 테두리가 보이게 한다. 더하기 섞기라 블룸과 함께 은은하게 빛난다.
// 부풀리는 방향은 메시 중심(_Center, 물체 공간)에서 꼭짓점 쪽이다. 상자처럼 모서리 노멀이 갈라진 메시도 테두리가 끊기지 않는다
Shader "BunkerLab/InteractOutline"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.8, 0.45, 0.8)
        _Width ("Width (m)", Float) = 0.01
        _Center ("Mesh Center (object space)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
                float4 _Center;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centerWS = TransformObjectToWorld(_Center.xyz);
                float3 dir = posWS - centerWS;
                // 중심과 겹친 꼭짓점(납작한 판의 가운데 등)은 노멀 방향으로 민다
                dir = dot(dir, dir) > 1e-8 ? normalize(dir) : normalize(TransformObjectToWorldNormal(input.normalOS));
                o.positionCS = TransformWorldToHClip(posWS + dir * _Width);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
