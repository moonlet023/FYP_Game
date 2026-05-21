Shader "UI/RectBorder"
{
    Properties{
        _Color("Border Color", Color) = (1,1,1,0.12)
        _Thickness("Thickness (px)", Float) = 8
        _RectSize("Rect Size", Vector) = (600,700,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appv { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;
            float _Thickness;
            float4 _RectSize;

            v2f vert(appv v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float sdRectEdge(float2 uv, float2 size, float radius)
            {
                // uv in 0..1, convert to pixel space with origin at bottom-left
                float2 p = uv * size;
                float2 half = size * 0.5;
                float2 d = abs(p - half) - (half - radius);
                d = max(d, 0.0);
                return length(d) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = _RectSize.xy;
                float2 uv = i.uv;

                // compute distance to inner rounded rect edge (approx radius = min/8)
                float radius = min(size.x, size.y) * 0.06;
                float dist = sdRectEdge(uv, size, radius);

                // We want to draw where distance is between -thickness and 0 (inside border)
                float edge = _Thickness;
                float a = smoothstep(edge + 1.0, edge - 1.0, dist);
                // invert: a==1 when inside outer area? adjust
                a = saturate(1.0 - a);

                fixed4 outc = _Color;
                outc.a *= a;
                return outc;
            }
            ENDCG
        }
    }
}
