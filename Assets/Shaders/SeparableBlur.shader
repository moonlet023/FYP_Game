Shader "Hidden/SeparableBlur"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _BlurSize("Blur Size", Float) = 1.0
        _Direction("Direction", Vector) = (1,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;
            float _Blend;
            float4 _Direction;

            struct appv { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appv v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 9-tap Gaussian approximation (weights sum ~= 1)
                float2 dir = normalize(_Direction.xy);
                float2 offs = dir * _BlurSize * _MainTex_TexelSize.xy;

                fixed4 c = tex2D(_MainTex, i.uv) * 0.2270270270;
                c += tex2D(_MainTex, i.uv + offs * 1.0) * 0.1945945946;
                c += tex2D(_MainTex, i.uv - offs * 1.0) * 0.1945945946;
                c += tex2D(_MainTex, i.uv + offs * 2.0) * 0.1216216216;
                c += tex2D(_MainTex, i.uv - offs * 2.0) * 0.1216216216;
                c += tex2D(_MainTex, i.uv + offs * 3.0) * 0.0540540541;
                c += tex2D(_MainTex, i.uv - offs * 3.0) * 0.0540540541;
                c += tex2D(_MainTex, i.uv + offs * 4.0) * 0.0162162162;
                c += tex2D(_MainTex, i.uv - offs * 4.0) * 0.0162162162;

                fixed4 orig = tex2D(_MainTex, i.uv);
                // blend between original and blurred result for control over 'strength'
                fixed4 outCol = lerp(orig, c, saturate(_Blend));
                return outCol;
            }
            ENDCG
        }
    }
}
