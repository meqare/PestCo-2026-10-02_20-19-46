// Ekran tabanlı kalem çizgisi: derinlik tamponunun ikinci türevine (Laplace) bakar.
// Düz yüzeylerde (eğik olsa bile) sıfırdır; kapı/dolap çıtaları, panel girintileri ve nesne kenarlarında çizgi çıkar.
// FullScreenPassRendererFeature ile kullanılır (Requirements: Depth).
Shader "PestCo/DepthInk"
{
    Properties
    {
        _InkColor ("Cizgi Rengi", Color) = (0.04, 0.025, 0.04, 1)
        _Threshold ("Esik (goreli derinlik farki)", Range(0.0005, 0.03)) = 0.004
        _Width ("Cizgi Kalinligi (px)", Range(0.5, 3)) = 1.2
        _FadeStart ("Solma Baslangici (m)", Float) = 10
        _FadeEnd ("Solma Bitisi (m)", Float) = 20
        _ShadeStrength ("Derinlik Golgesi Siddeti", Range(0, 0.8)) = 0.35
        _ShadeRadius ("Derinlik Golgesi Yaricapi (px)", Range(1, 12)) = 4
        _FarWidth ("Uzakta Kalinlik Orani (1 = solmaz)", Range(0.3, 1)) = 0.8
        _FarWidthEnd ("Kalinlik Kuculmesi Bitisi (m)", Float) = 45
        _NoiseGrow ("Uzakta Esik Artisi (kucuk = uzak cizgi korunur)", Range(0, 0.4)) = 0.1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "DepthInk"
            ZTest Always ZWrite Off Cull Off
            // Tum sahne icin tek ekran-alani gecisi: nesne basina ters-kabuk kontur pass'inin (cift cizim) yerini alir.
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            half4 _InkColor;
            float4 _PC_InkTint;
            float4 _PC_InkZoneCenter, _PC_InkZoneExtents, _PC_InkWobble; // _PC_InkWobble: x genlik (px), y fps, z kursun kalem rengi karisimi, w cizgi kalinligi carpani // InkZone bileseni yazar (w: icerideki carpan / yumusak kenar m)
            float _Threshold, _Width, _FadeStart, _FadeEnd, _ShadeStrength, _ShadeRadius, _FarWidth, _FarWidthEnd, _NoiseGrow;

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;

                float d0 = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (d0 <= 0.0) return 0;                  // gökyüzü
                #else
                    if (d0 >= 1.0) return 0;
                #endif

                float pencilZone = 0.0;
                float zoneW = 0.0;
                // Elle cizim: cizgi konumu dalgali ve kareden kareye kayar (dusuk fps). Sadece InkZone icinde.
                // (Titreme 0 olsa da bolge rengi/kalinligi icin bolge agirligi hesaplanir: _PC_InkTint.a > 0)
                if ((_PC_InkWobble.x > 0.0 || _PC_InkTint.a > 0.0) && _PC_InkZoneExtents.x > 0.0)
                {
                    float3 wp0 = ComputeWorldSpacePosition(uv, d0, UNITY_MATRIX_I_VP);
                    float3 q0 = abs(wp0 - _PC_InkZoneCenter.xyz) - _PC_InkZoneExtents.xyz;
                    float zw = 1.0 - smoothstep(0.0, max(_PC_InkZoneExtents.w, 0.01), max(q0.x, max(q0.y, q0.z)));
                    pencilZone = zw * _PC_InkWobble.z;
                    zoneW = zw;
                    if (_PC_InkWobble.x > 0.0)
                    {
                        float fr = floor(_Time.y * _PC_InkWobble.y);
                        // derinlik dokusunun gercek piksel boyutu (_ScreenParams render olcegi/hedef doku farkinda tutmaz -> yatay serit)
                        float2 texSize = _CameraDepthTexture_TexelSize.zw;
                        float2 spx = uv * texSize;
                        float2 wob = float2(sin(spx.y * 0.035 + fr * 1.7) + sin(spx.x * 0.017 + fr * 2.9) * 0.7,
                                            sin(spx.x * 0.031 + fr * 2.3) + sin(spx.y * 0.021 + fr * 1.1) * 0.7);
                        // tam piksele yuvarla: kesirli kayma komsu derinlik orneklerini asimetrik yapip halka desenleri uretiyordu
                        float2 sh = round(wob * (_PC_InkWobble.x * zw));
                        uv = (floor(spx) + 0.5 + sh) / texSize;
                        d0 = SampleSceneDepth(uv);
                    }
                }

                // Uzaklastikca cizgi kalinligini azalt: 6 m'de tam, 20 m'de ~%45. Sabit piksel kalinligi uzakta sismis gorunuyordu.
                float eyeNear = LinearEyeDepth(d0, _ZBufferParams);
                float wk = lerp(1.0, _FarWidth, saturate((eyeNear - 6.0) / max(_FarWidthEnd - 6.0, 1.0)));
                float widthMul = lerp(1.0, max(_PC_InkWobble.w, 0.01), zoneW); // InkZone: bolgede cizgi kalinligi carpani
                float2 px = _Width * wk * widthMul / _ScreenParams.xy;

                float dl = SampleSceneDepth(uv + float2(-px.x, 0));
                float dr = SampleSceneDepth(uv + float2( px.x, 0));
                float du = SampleSceneDepth(uv + float2(0,  px.y));
                float dd = SampleSceneDepth(uv + float2(0, -px.y));

                // Ham derinlik ekranda 1/z ile doğrusaldır: düz yüzeyde Laplace ≈ 0.
                float lap = abs(dl + dr + du + dd - 4.0 * d0);
                float eye = LinearEyeDepth(d0, _ZBufferParams);
                // Uzak yüzeylerde derinlik gürültüsü artar: eşik uzaklıkla yükselir, böylece düz koyu yüzeylerde kesik çizgi çıkmaz.
                float rel = lap / max(abs(d0), 1e-5);
                float thr = _Threshold * (1.0 + eye * _NoiseGrow);
                float ink = smoothstep(thr, thr * 1.35, rel);   // keskin gecis: net cizgi
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, eye);
                ink *= fade;

                // Bolgesel zayiflatma (InkZone): kutu icinde cizgi ve temas golgesi carpanla azalir, disarida degisiklik yok.
                float zoneMul = 1.0;
                if (_PC_InkZoneExtents.x > 0.0)
                {
                    float3 wp = ComputeWorldSpacePosition(uv, d0, UNITY_MATRIX_I_VP);
                    float3 q = abs(wp - _PC_InkZoneCenter.xyz) - _PC_InkZoneExtents.xyz;
                    float outside = max(q.x, max(q.y, q.z));
                    zoneMul = lerp(1.0, _PC_InkZoneCenter.w, 1.0 - smoothstep(0.0, max(_PC_InkZoneExtents.w, 0.01), outside));
                }
                ink *= zoneMul;

                // Derinlik hissi: komsu pikseller merkezden yakinsa (cekmece araligi, dolap kapagi girintisi) yumusak temas golgesi.
                // Cok buyuk derinlik farklari (nesne siluetleri) haleyi onlemek icin elenir.
                float2 pr = _ShadeRadius / _ScreenParams.xy;
                float4 nd = float4(SampleSceneDepth(uv + float2(-pr.x, 0)), SampleSceneDepth(uv + float2(pr.x, 0)),
                                   SampleSceneDepth(uv + float2(0, pr.y)),  SampleSceneDepth(uv + float2(0, -pr.y)));
                float4 ne = float4(LinearEyeDepth(nd.x, _ZBufferParams), LinearEyeDepth(nd.y, _ZBufferParams),
                                   LinearEyeDepth(nd.z, _ZBufferParams), LinearEyeDepth(nd.w, _ZBufferParams));
                float4 v = (eye - ne) / eye;                    // > 0: komsu daha yakin, bu piksel cukurda
                float4 c = smoothstep(0.004, 0.02, v) * (1.0 - smoothstep(0.06, 0.12, v));
                float shade = saturate(dot(c, float4(0.6, 0.6, 0.6, 0.6))) * _ShadeStrength * fade * lerp(1.0, zoneMul, 0.5);

                float a = saturate(ink + shade * (1.0 - ink));
                // kursun kalem grisi; _PC_InkTint.a > 0 ise bolge rengi onun yerine (CourageZone: koyu kahve murekkep)
                half3 pencilCol = lerp(half3(0.20h, 0.18h, 0.22h), (half3)_PC_InkTint.rgb, saturate(_PC_InkTint.a));
                half3 inkCol = lerp(_InkColor.rgb, pencilCol, pencilZone);
                return half4(inkCol, a * lerp(_InkColor.a, 0.88h, pencilZone));
            }
            ENDHLSL
        }
    }
}
