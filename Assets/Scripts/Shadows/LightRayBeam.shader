// Put this anywhere under Assets/ (e.g. Assets/Shaders/LightRayBeam.shader)
// Soft additive light beam: fades at the edges, fades in at the top, fades out near the ground,
// and shimmers slowly. Used by LightRays.cs. URP only.
Shader "LightRays/Beam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.86, 0.62, 1)
        _Intensity ("Intensity", Float) = 0.35
        _Strength ("Strength (set per ray)", Float) = 1
        _Seed ("Seed (set per ray)", Float) = 0
        _EdgeSoftness ("Edge Softness", Range(0.05, 1)) = 0.7
        _TopFade ("Top Fade", Range(0.01, 1)) = 0.35
        _BottomFade ("Bottom Fade", Range(0.01, 1)) = 0.5
        _Streaks ("Streak Amount", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One   // additive: only ever adds light
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Strength;
                float _Seed;
                float _EdgeSoftness;
                float _TopFade;
                float _BottomFade;
                float _Streaks;
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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float x = IN.uv.x;   // 0..1 across the beam
                float y = IN.uv.y;   // 0 = top of the beam, 1 = where it hits the ground

                // soft sides
                float edge = 1.0 - abs(x * 2.0 - 1.0);
                edge = smoothstep(0.0, _EdgeSoftness, edge);
                edge *= edge;

                // fade in from the top, fade out toward the ground
                float topF = smoothstep(0.0, _TopFade, y);
                float botF = 1.0 - smoothstep(1.0 - _BottomFade, 1.0, y);

                // slow shimmering streaks inside the beam
                float t = _Time.y;
                float s1 = sin(x * 23.0 + t * 0.45 + _Seed * 6.2831);
                float s2 = sin(x * 11.0 - t * 0.31 + _Seed * 3.1);
                float s3 = sin(y * 14.0 - t * 0.60 + _Seed * 9.0);
                float shimmer = 0.5 + 0.5 * (s1 * 0.5 + s2 * 0.3 + s3 * 0.2);
                float streak = lerp(1.0, shimmer, _Streaks);

                float alpha = edge * topF * botF * streak * _Strength;
                return half4(_Color.rgb * _Intensity, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
