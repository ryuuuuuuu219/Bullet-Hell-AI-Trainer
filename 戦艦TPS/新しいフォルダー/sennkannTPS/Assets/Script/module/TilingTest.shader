Shader "Battleship/TilingTest"
{
    Properties { _Opacity ("Opacity", Range(0, 1)) = 1 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Opacity;
            struct Input { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; fixed4 color : COLOR; };
            Output vert(Input v) { Output o; o.position = UnityObjectToClipPos(v.vertex); o.color = v.color; return o; }
            fixed4 frag(Output i) : SV_Target { i.color.a *= _Opacity; return i.color; }
            ENDCG
        }
    }
}
