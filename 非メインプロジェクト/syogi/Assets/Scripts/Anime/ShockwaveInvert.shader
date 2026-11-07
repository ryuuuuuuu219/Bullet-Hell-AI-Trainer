Shader "Custom/ShockwaveInvert"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _RectSize ("Rect Size", Vector) = (1920, 1080, 0, 0)
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0
        _Width ("Width", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend OneMinusDstColor Zero
            ColorMask RGB

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RectSize;
                float4 _Center;
                float _Radius;
                float _Width;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // UVをCanvas上の距離に変換する。
                // 横長の画面でも円が楕円にならない。
                float2 offset =
                    (input.uv - _Center.xy) * _RectSize.xy;

                float distanceFromCenter = length(offset);

                // 円周からの距離
                float distanceFromRing =
                    abs(distanceFromCenter - _Radius);

                // 帯の外側は描画しない
                clip(_Width - 0.0001);
                clip(_Width * 0.5 - distanceFromRing);

                // 描画済みの背景・UIを反転
                return half4(1, 1, 1, 1);
            }

            ENDHLSL
        }
    }
}