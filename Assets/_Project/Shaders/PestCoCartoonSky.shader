// Tatlı çizgi film gökyüzü: pastel degrade, yumuşak kenarlı çizgi film bulutları ve basit güneş diski.
Shader "PestCo/CartoonSky"
{
    Properties
    {
        _TopColor ("Tepe Rengi", Color) = (0.55, 0.72, 1.0, 1)
        _MidColor ("Orta Renk", Color) = (0.78, 0.88, 1.0, 1)
        _HorizonColor ("Ufuk Rengi", Color) = (1.0, 0.86, 0.88, 1)
        _GroundColor ("Zemin Rengi", Color) = (0.95, 0.85, 0.82, 1)
        _CloudColor ("Bulut Rengi", Color) = (1, 1, 1, 1)
        _CloudShade ("Bulut Golgesi", Color) = (0.82, 0.8, 0.95, 1)
        _CloudScale ("Bulut Olcegi", Float) = 1.6
        _CloudCover ("Bulut Miktari", Range(0,1)) = 0.5
        _CloudSpeed ("Bulut Hizi", Float) = 0.004
        _SunColor ("Gunes Rengi", Color) = (1.0, 0.93, 0.6, 1)
        _SunSize ("Gunes Boyutu", Range(0.005, 0.2)) = 0.045
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _MidColor, _HorizonColor, _GroundColor, _CloudColor, _CloudShade, _SunColor;
                float _CloudScale, _CloudCover, _CloudSpeed, _SunSize;
            CBUFFER_END

            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; float3 dir : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.posCS = TransformObjectToHClip(i.posOS.xyz);
                o.dir = i.posOS.xyz;
                return o;
            }

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * Noise(p); p = p * 2.03 + 11.7; a *= 0.5; }
                return v;
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                // degrade: zemin -> ufuk (pembe) -> orta -> tepe
                half3 sky = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.25, h));
                sky = lerp(sky, _TopColor.rgb, smoothstep(0.2, 0.8, h));
                half3 col = lerp(_GroundColor.rgb, sky, smoothstep(-0.08, 0.02, h));

                // gunes diski + hale (yumusak halka)
                float3 sunDir = normalize(_MainLightPosition.xyz);
                float sd = acos(clamp(dot(d, sunDir), -1.0, 1.0));
                float disc = 1.0 - smoothstep(_SunSize, _SunSize + 0.01, sd);
                float halo = (1.0 - smoothstep(_SunSize + 0.02, _SunSize * 3.2, sd)) * 0.28;
                col = lerp(col, _SunColor.rgb, saturate(halo));
                col = lerp(col, _SunColor.rgb, disc);

                // cizgi film bulutlari (ufuk ustu): sert esikli, golgeli
                if (h > 0.02)
                {
                    float2 uv = d.xz / (h + 0.28) * _CloudScale + _Time.y * _CloudSpeed * float2(1, 0.4);
                    float n = Fbm(uv);
                    float t = lerp(0.72, 0.42, _CloudCover);
                    float cloud = smoothstep(t, t + 0.015, n);
                    float n2 = Fbm(uv + float2(0.06, 0.09));   // golge icin kaydirilmis ornek
                    float shade = smoothstep(0.0, 0.07, n - n2) ; // alt/sol kenar golgesi
                    half3 cc = lerp(_CloudColor.rgb, _CloudShade.rgb, saturate(shade * 1.4));
                    float fade = smoothstep(0.02, 0.18, h);
                    col = lerp(col, cc, cloud * fade);
                }
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
