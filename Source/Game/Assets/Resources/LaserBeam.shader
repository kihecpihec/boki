Shader "Boki/Laser Beam"
{
    // Additive glow for the laser ribbon (LineRenderer) and its flares (particles). The falloff is
    // procedural from the quad UVs, so no texture is needed and bloom stays off.
    Properties
    {
        _CoreColor ("Core", Color) = (1, 1, 1, 1)
        _GlowColor ("Glow", Color) = (0.1, 0.9, 1, 1)
        _EdgeColor ("Edge", Color) = (0.55, 0.15, 1, 1)
        _Intensity ("Intensity", Range(0, 2)) = 1
        [ToggleUI] _Radial ("Radial (flares)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _GlowColor;
                half4 _EdgeColor;
                half _Intensity;
                half _Radial;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // A ribbon fades across its width (v); a flare fades radially from the quad centre.
                float2 p = input.uv * 2.0 - 1.0;
                half falloff = 1.0 - saturate(_Radial > 0.5 ? length(p) : abs(p.y));
                half halo = falloff * falloff;
                half core = pow(falloff, 5.0);
                half3 colour = lerp(_EdgeColor.rgb, _GlowColor.rgb, falloff) + _CoreColor.rgb * core;
                half alpha = saturate(halo + core) * _Intensity * input.color.a;
                return half4(colour * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
