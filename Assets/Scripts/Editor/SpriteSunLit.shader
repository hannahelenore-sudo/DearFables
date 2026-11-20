// Cult-of-the-Lamb-style lit sprite for URP (Universal Renderer, 3D Directional Light).
// Put in Assets/Shaders/SpriteSunLit.shader
Shader "Sprites/Sun Lit (2.5D)"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        _Cutoff ("Shadow Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Sun Shading)]
        _SunInfluence ("Sun Influence", Range(0,1)) = 1
        _ShadeColor ("Shade Colour", Color) = (0.55,0.5,0.75,1)
        _UpBlend ("Normal Tilt Toward Sky", Range(0,1)) = 0.5
        _Wrap ("Light Wrap", Range(0,1)) = 0.5
        _Bands ("Toon Bands (0 = smooth)", Range(0,6)) = 3
        _BandSoftness ("Band Softness", Range(0.01,0.5)) = 0.12
        _SunBoost ("Lit Brightness", Range(0.5,2)) = 1.05
        _SunTint ("Sun Colour Tint", Range(0,1)) = 0.3

        [Header(Rim Light)]
        _RimColor ("Rim Colour", Color) = (1,0.92,0.75,1)
        _RimWidth ("Rim Width (texels)", Range(0,6)) = 2
        _RimStrength ("Rim Strength", Range(0,2)) = 0.8

        [Header(Shadows)]
        [Toggle] _ReceiveSunShadows ("Receive Shadows", Float) = 1
        _ShadowStrength ("Received Shadow Strength", Range(0,1)) = 0.7
        _SelfShadowOffset ("Self Shadow Offset", Range(0,0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            half4  _Color;
            half4  _RendererColor;
            float4 _Flip;
            half   _Cutoff;
            half   _SunInfluence;
            half4  _ShadeColor;
            half   _UpBlend;
            half   _Wrap;
            half   _Bands;
            half   _BandSoftness;
            half   _SunBoost;
            half   _SunTint;
            half4  _RimColor;
            half   _RimWidth;
            half   _RimStrength;
            half   _ReceiveSunShadows;
            half   _ShadowStrength;
            half   _SelfShadowOffset;
        CBUFFER_END

        // ---- cloud shadows (set globally by CloudShadows.cs; strength 0 = off)
        float4 _CloudParams;   // x = 1/world size of a cloud cell, y = coverage, z = softness, w = strength
        float4 _CloudScroll;   // xy = noise-space scroll (wind), z = cloud height above ground

        float CloudHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float CloudNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = CloudHash(i);
            float b = CloudHash(i + float2(1, 0));
            float c = CloudHash(i + float2(0, 1));
            float d = CloudHash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float CloudFbm(float2 p)
        {
            float v = 0.0, a = 0.5;
            for (int k = 0; k < 4; k++)
            {
                v += a * CloudNoise(p);
                p = p * 2.03 + float2(17.1, 9.2);
                a *= 0.5;
            }
            return v;
        }

        // How much of the sun is blocked here by clouds (0 = none, 1 = fully shaded)
        half CloudShadowAmount(float3 positionWS, float3 toSun)
        {
            if (_CloudParams.w <= 0.0) return 0.0h;
            // follow the ray from this point toward the sun up to the cloud layer
            float t = _CloudScroll.z / max(toSun.y, 0.12);
            float2 xz = positionWS.xz + toSun.xz * t;
            float n = CloudFbm(xz * _CloudParams.x + _CloudScroll.xy);
            float c = smoothstep(_CloudParams.y - _CloudParams.z, _CloudParams.y + _CloudParams.z, n);
            return (half)(c * _CloudParams.w);
        }
        ENDHLSL

        // ------------------------------------------------------------ forward
        Pass
        {
            Name "SpriteSunLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 facingWS   : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 pos = v.positionOS;
                pos.xy *= _Flip.xy;
                o.positionWS = TransformObjectToWorld(pos);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color * _RendererColor;
                // sprite quads face -Z in object space
                o.facingWS = TransformObjectToWorldNormal(float3(0, 0, -1));
                return o;
            }

            half ToonBands(half x)
            {
                if (_Bands < 0.5h) return x;
                half s = x * _Bands;
                half f = frac(s);
                half stepped = floor(s) + smoothstep(0.5h - _BandSoftness, 0.5h + _BandSoftness, f);
                return saturate(stepped / _Bands);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 col = tex * i.color;

                // --- main light (the Directional Light = your sun)
                float3 L = normalize(_MainLightPosition.xyz);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS + L * _SelfShadowOffset);
                Light sun = GetMainLight(shadowCoord);

                half maxc = max(sun.color.r, max(sun.color.g, sun.color.b));
                half3 sunHue = lerp(half3(1, 1, 1), sun.color / max(maxc, 1e-4h), _SunTint);
                half sunStrength = saturate(maxc);              // dims at night

                // fake normal: camera-facing billboard leaned toward the sky
                // two-sided: always use the side facing the camera (needed for flat ground tilemaps)
                float3 facing = normalize(i.facingWS);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                if (dot(facing, viewDir) < 0) facing = -facing;
                float3 N = normalize(lerp(facing, float3(0, 1, 0), _UpBlend));
                half ndl = dot(N, L);
                half wrapped = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                half lightAmt = ToonBands(wrapped);

                half shadow = lerp(1.0h, sun.shadowAttenuation, _ReceiveSunShadows * _ShadowStrength);
                shadow *= 1.0h - CloudShadowAmount(i.positionWS, L);
                half sunMask = lightAmt * shadow * sunStrength;

                half3 lit = sunHue * _SunBoost;
                half3 shade = _ShadeColor.rgb;
                half3 lighting = lerp(shade, lit, sunMask);
                col.rgb *= lerp(half3(1, 1, 1), lighting, _SunInfluence);

                // --- rim light on the edge of the silhouette that faces the sun
                float3 lightOS = TransformWorldToObjectDir(L, false);
                float2 d = lightOS.xy * _Flip.xy;
                d = d / max(length(d), 1e-4);
                float2 off = d * _MainTex_TexelSize.xy * _RimWidth;
                half a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + off * 0.5).a;
                half a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + off).a;
                half edge = saturate(tex.a - (a1 + a2) * 0.5h);
                col.rgb += _RimColor.rgb * sunHue * edge * _RimStrength * shadow * sunStrength * _SunInfluence;

                return col;
            }
            ENDHLSL
        }

        // ------------------------------------------------------- shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   alpha      : TEXCOORD1;
            };

            SVaryings ShadowVert(SAttributes v)
            {
                SVaryings o;
                float3 pos = v.positionOS;
                pos.xy *= _Flip.xy;
                float3 positionWS = TransformObjectToWorld(pos);
                float3 normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirWS = _LightDirection;
            #endif

                float4 posCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));
            #if UNITY_REVERSED_Z
                posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE * posCS.w);
            #else
                posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE * posCS.w);
            #endif
                o.positionCS = posCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alpha = v.color.a * _Color.a * _RendererColor.a;
                return o;
            }

            half4 ShadowFrag(SVaryings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.alpha;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
