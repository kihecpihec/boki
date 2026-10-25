// Procedural whirlpool for portals. The same shader draws the in-world disc (additive) and the full-screen
// swirl (opaque, depth test off); VortexDisc.CreateMaterial sets the blend state for each use.
Shader "Boki/Portal Vortex"
{
    Properties
    {
        // UGUI assigns _MainTex on every Image material; the swirl is procedural and ignores it.
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Color", Color) = (0.2, 1, 0.85, 1)
        _Intensity ("Intensity (brightness)", Float) = 1
        _Alpha ("Alpha (fade)", Range(0, 1)) = 1
        _Spin ("Spin", Float) = 0
        _Opacity ("Base opacity", Range(0, 1)) = 0.3
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Alpha;
                float _Spin;
                float _Opacity;
                float _SrcBlend;
                float _DstBlend;
                float _ZTest;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float GlintHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Quad space: the disc edge is at r = 1.
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float angle = atan2(p.y, p.x);

                // Two spiral arm sets whose constant-phase lines slide inward as _Spin grows.
                float armsA = 0.5 + 0.5 * cos(angle * 3.0 + r * 6.0 + _Spin);
                float armsB = 0.5 + 0.5 * cos(angle * 5.0 + r * 9.0 + _Spin * 1.6);
                float armsB2 = armsB * armsB;
                float arms = max(armsA * armsA * armsA, 0.6 * armsB2 * armsB2);
                arms *= smoothstep(0.02, 0.22, r);

                // Glints drift inward along the same flow. 24 angular cells divide a full turn evenly,
                // so the cell index wraps at the atan2 seam without a visible break.
                float2 q = float2(angle * (24.0 / 6.2831853), r * 8.0 + _Spin * 0.35);
                float2 cell = floor(q);
                cell.x = fmod(cell.x + 24.0, 24.0);
                float glint = step(0.88, GlintHash(cell)) * (1.0 - smoothstep(0.0, 0.38, length(frac(q) - 0.5)));
                glint *= smoothstep(0.1, 0.3, r) * (1.0 - smoothstep(0.7, 0.85, r));

                float core = exp(-r * r * 16.0);
                float rimDistance = (r - 0.8) * 13.0;
                float rim = exp(-rimDistance * rimDistance);
                float edge = 1.0 - smoothstep(0.78, 1.0, r);

                half3 deep = _Color.rgb * 0.35;
                half3 bright = lerp(_Color.rgb, half3(1, 1, 1), 0.55);
                half3 color = lerp(deep, bright, arms) + (bright + 0.3) * (core + rim * 0.7 + glint * 0.9);
                float alpha = edge * saturate(_Opacity + arms * 0.7 + core + rim * 0.8 + glint);
                return half4(color * _Intensity, alpha * saturate(_Alpha));
            }
            ENDHLSL
        }
    }
}
