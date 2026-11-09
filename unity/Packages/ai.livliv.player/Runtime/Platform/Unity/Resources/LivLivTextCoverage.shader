Shader "Hidden/LivLiv/TextCoverage"
{
    Properties { _MainTex ("Font atlas", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _View;
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Varying {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            Varying vert(Input v){Varying o;o.position=float4(v.vertex.xy*_View.xy+_View.zw,0,1);o.uv=v.uv;return o;}
            float4 frag(Varying i):SV_Target{return tex2D(_MainTex,i.uv).aaaa;}
            ENDCG
        }
    }
}
