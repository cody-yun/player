// Platform mapping of puppet.ts VS/FS and its premultiplied blending/stencil states.
Shader "Hidden/LivLiv/Layer"
{
    Properties { _MainTex ("Image", 2D) = "white" {} _StencilRef ("Stencil", Int) = 0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _MaskTex;
        float4 _MaskSel, _MaskRect;
        float _Soft;
        float4 _View;
        float _Alpha, _Cut;
        struct Input { float4 vertex: POSITION; float2 uv: TEXCOORD0; };
        struct Varying { float4 position: SV_POSITION; float2 uv: TEXCOORD0; };
        Varying vert(Input v)
        {
            Varying o;
            float2 p = v.vertex.xy * _View.xy + _View.zw;
            // _View is mapped for Unity RenderTexture orientation by the caller.
            o.position = float4(p, 0, 1);
            o.uv = v.uv;
            return o;
        }
        float4 frag(Varying i): SV_Target
        {
            float4 c = tex2D(_MainTex, i.uv);
            clip(c.a - _Cut);
            return c * _Alpha;
        }
        float4 coverage(Varying i): SV_Target { return tex2D(_MainTex, i.uv).aaaa; }
        float4 softIris(Varying i): SV_Target
        {
            float4 c = tex2D(_MainTex, i.uv);
            float2 q = floor(i.position.xy) - _MaskRect.xy;
            float m = 0;
            if (all(q >= 0) && all(q < _MaskRect.zw))
                m = dot(tex2D(_MaskTex, (q + .5) / _MaskRect.zw), _MaskSel);
            c *= lerp(step(.5, m), m, _Soft);
            clip(c.a - _Cut);
            return c * _Alpha;
        }
        ENDCG
        Pass
        {
            Name "Layer"
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Name "EyeMask"
            ColorMask 0
            Blend Off
            Stencil { Ref [_StencilRef] Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Name "ClippedIris"
            Blend One OneMinusSrcAlpha
            Stencil { Ref [_StencilRef] Comp Equal Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Name "CoverageR"
            ColorMask R
            Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment coverage
            ENDCG
        }
        Pass
        {
            Name "CoverageG"
            ColorMask G
            Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment coverage
            ENDCG
        }
        Pass
        {
            Name "CoverageB"
            ColorMask B
            Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment coverage
            ENDCG
        }
        Pass
        {
            Name "CoverageA"
            ColorMask A
            Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment coverage
            ENDCG
        }
        Pass
        {
            Name "SoftIris"
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment softIris
            ENDCG
        }
    }
}
