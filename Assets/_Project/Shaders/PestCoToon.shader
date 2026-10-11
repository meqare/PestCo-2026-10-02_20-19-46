// Stilize iki tonlu boyali gorunum: aydinlik/golge rampasi, fircaizi dokusu, sabit piksel kalinlikli kontur.
Shader "PestCo/Toon"
{
    Properties
    {
        [MainColor] _BaseColor ("Aydinlik Renk", Color) = (1,1,1,1)
        _ShadowColor ("Golge Renk", Color) = (0.5,0.5,0.6,1)
        _BaseMap ("Ana Doku (opsiyonel)", 2D) = "white" {}
        _Threshold ("Golge Esigi", Range(0,1)) = 0.5
        _Softness ("Gecis Yumusakligi", Range(0.01,0.5)) = 0.18
        _BrushTex ("Firca Dokusu", 2D) = "gray" {}
        _BrushScale ("Firca Olcegi (1/m)", Float) = 1.5
        _BrushStrength ("Firca Siddeti", Range(0,0.4)) = 0.2
        _SpecStrength ("Parlama Siddeti", Range(0,1.5)) = 0.0
        _SpecSize ("Parlama Boyutu", Range(0.5,0.99)) = 0.9
        _RimStrength ("Kenar Isigi", Range(0,1)) = 0.35
        _GradStrength ("Zemin Koyulasma", Range(0,0.6)) = 0.28
        _LampBoost ("Ek Isik (Lamba) Gucu", Range(0,2)) = 0.9
        [Normal] _BumpMap ("Normal Map (opsiyonel)", 2D) = "bump" {}
        _BumpScale ("Normal Siddeti", Range(0,2)) = 1.0
        _HiStrength ("Parlak Bant Siddeti", Range(0,0.5)) = 0.16
        _AmbientStrength ("Ortam Yansimasi", Range(0,1)) = 0.5
        [HDR] _EmissionColor ("Emisyon", Color) = (0,0,0,1)
        _OutlineColor ("Kontur Renk", Color) = (0.1,0.2,0.18,1)
        _OutlinePixels ("Kontur Kalinligi (px)", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma shader_feature_local_fragment _NORMALMAP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BrushTex); SAMPLER(sampler_BrushTex);
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor; float4 _BaseMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength;
            CBUFFER_END

            struct A { float4 posOS : POSITION; float3 nOS : NORMAL; float4 tOS : TANGENT; float2 uv : TEXCOORD0; };
            struct V { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; half3 nWS : TEXCOORD1; float2 uv : TEXCOORD2; half4 tWS : TEXCOORD3; };

            V vert(A i)
            {
                V o;
                o.posWS = TransformObjectToWorld(i.posOS.xyz);
                o.nWS = TransformObjectToWorldNormal(i.nOS);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                o.tWS = half4(TransformObjectToWorldDir(i.tOS.xyz), i.tOS.w * GetOddNegativeScale());
                return o;
            }

            half Ramp(half ndl, half atten)
            {
                return smoothstep(_Threshold - _Softness, _Threshold + _Softness, ndl * 0.5h + 0.5h) * atten;
            }

            half4 frag(V i) : SV_Target
            {
                half3 n = normalize(i.nWS);
                #if defined(_NORMALMAP)
                    half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    half3 bWS = cross(n, i.tWS.xyz) * i.tWS.w;
                    n = normalize(tn.x * i.tWS.xyz + tn.y * bWS + tn.z * n);
                #endif
                // UV'den bagimsiz uc eksenli fircaizi
                half3 w = pow(abs(n), 4); w /= (w.x + w.y + w.z);
                half b = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, i.posWS.zy * _BrushScale).r * w.x
                       + SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, i.posWS.xz * _BrushScale).r * w.y
                       + SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, i.posWS.xy * _BrushScale).r * w.z;
                half lf = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, i.posWS.xz * _BrushScale * 0.18 + i.posWS.y * 0.11).r
                        + SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, i.posWS.zy * _BrushScale * 0.18).r;
                half brush = 1 + (b - 0.5h) * 2 * _BrushStrength + (lf - 1.0h) * 0.22h;

                float4 sc = TransformWorldToShadowCoord(i.posWS);
                Light ml = GetMainLight(sc);
                half t = Ramp(dot(n, ml.direction), ml.shadowAttenuation);

                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half3 lit = _BaseColor.rgb * tex;
                half3 shd = _ShadowColor.rgb * tex * half3(0.82h, 0.84h, 0.95h);
                half3 col = lerp(shd, lit, t) * brush;

                // Ek isiklar (lamba): yumusak rampa ile sicak katki
                half3 extra = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                    uint cnt = GetAdditionalLightsCount();
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.posWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.posCS);
                    LIGHT_LOOP_BEGIN(cnt)
                        Light l = GetAdditionalLight(lightIndex, i.posWS, half4(1,1,1,1));
                        extra += l.color * (l.distanceAttenuation * l.shadowAttenuation * Ramp(dot(n, l.direction), 1));
                    LIGHT_LOOP_END
                #endif
                half lampT = saturate(dot(extra, half3(0.33h,0.33h,0.33h))) * _LampBoost;
                half3 warm = extra / max(max(extra.r, max(extra.g, extra.b)), 1e-3h);
                half tt = saturate(t + lampT * 0.9h);
                // gokyuzu/zemin ortam yansimasi: golge tarafi yukari bakinca soguk, asagi bakinca sicak
                half3 amb = lerp(half3(0.62h, 0.50h, 0.38h), half3(0.62h, 0.76h, 1.0h), n.y * 0.5h + 0.5h);
                shd = lerp(shd, shd * amb * 1.55h, _AmbientStrength * 0.55h);
                col = lerp(shd, lit, tt) * brush;
                // ucuncu parlak bant: isigin en cok vurdugu yer boyali vurgu
                half hiT = smoothstep(_Threshold + 0.26h - _Softness, _Threshold + 0.26h + _Softness, dot(n, ml.direction) * 0.5h + 0.5h) * ml.shadowAttenuation;
                col += lit * hiT * _HiStrength * brush;
                col *= lerp(half3(1,1,1), lerp(half3(1,1,1), warm, 0.35h), saturate(lampT));
                // gunes rengi (lit kisima sicak ton)
                half3 sunTint = ml.color / max(max(ml.color.r, max(ml.color.g, ml.color.b)), 1e-3h);
                col *= lerp(half3(1,1,1), sunTint, 0.55h * t);
                // zemine dogru boyali koyulasma
                half ao = lerp(1.0h - _GradStrength * 1.3h, 1.0h, saturate(i.posWS.y * 0.8h));
                ao *= lerp(1.0h, 0.86h, saturate((i.posWS.y - 2.1h) * 1.6h));
                col *= ao;
                // derinlik: uzaklastikca sicak-mavimsi pus
                float dist = distance(_WorldSpaceCameraPos, i.posWS);
                half fog = 1.0h - exp(-dist * 0.07h);
                col = lerp(col, half3(0.86h, 0.80h, 0.72h) * dot(col, half3(0.33h,0.33h,0.33h)) * 1.15h + half3(0.05h,0.05h,0.06h), fog * 0.35h);
                // stilize parlama + sicak kenar isigi
                half3 v = GetWorldSpaceNormalizeViewDir(i.posWS);
                half3 hv = normalize(ml.direction + v);
                half sp = smoothstep(_SpecSize, _SpecSize + 0.02h, dot(n, hv)) * t;
                half3 lampDir = half3(0,1,0);
                col += sp * _SpecStrength * half3(1.0h, 0.97h, 0.88h);
                half rim = smoothstep(0.55h, 0.85h, 1.0h - saturate(dot(n, v))) * saturate(t * 0.6h + 0.4h);
                col += rim * _RimStrength * half3(1.0h, 0.86h, 0.55h) * 0.22h;
                col += _EmissionColor.rgb;
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Kontur: ters kabuk, ekranda sabit piksel kalinligi
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor; float4 _BaseMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength;
            CBUFFER_END

            struct A { float4 posOS : POSITION; float3 nOS : NORMAL; };
            struct V { float4 posCS : SV_POSITION; };

            V vert(A i)
            {
                V o;
                float4 cs = TransformObjectToHClip(i.posOS.xyz);
                float3 nVS = mul((float3x3)UNITY_MATRIX_IT_MV, i.nOS);
                float2 nCS = normalize(mul((float2x2)UNITY_MATRIX_P, nVS.xy) + 1e-5);
                float2 px = nCS * (_OutlinePixels * 2.0) / _ScreenParams.xy;
                cs.xy += px * cs.w;
                o.posCS = cs;
                return o;
            }
            half4 frag(V i) : SV_Target { return half4(_OutlineColor.rgb, 1); }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor; float4 _BaseMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength;
            CBUFFER_END
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 posOS : POSITION; float3 nOS : NORMAL; };
            struct V { float4 posCS : SV_POSITION; };
            V vert(A i)
            {
                V o;
                float3 pw = TransformObjectToWorld(i.posOS.xyz);
                float3 nw = TransformObjectToWorldNormal(i.nOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - pw);
                #else
                    float3 ld = _LightDirection;
                #endif
                o.posCS = TransformWorldToHClip(ApplyShadowBias(pw, nw, ld));
                #if UNITY_REVERSED_Z
                    o.posCS.z = min(o.posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.posCS.z = max(o.posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor; float4 _BaseMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength;
            CBUFFER_END
            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; };
            V vert(A i) { V o; o.posCS = TransformObjectToHClip(i.posOS.xyz); return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor; float4 _BaseMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float3 nOS : NORMAL; };
            struct V { float4 posCS : SV_POSITION; half3 n : TEXCOORD0; };
            V vert(A i) { V o; o.posCS = TransformObjectToHClip(i.posOS.xyz); o.n = TransformObjectToWorldNormal(i.nOS); return o; }
            half4 frag(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.n), 0); }
            ENDHLSL
        }
    }
}
