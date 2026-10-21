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
        _OutlineBias ("Kontur Derinlik Payi (metre, ince detay kenarlari icin)", Range(0,0.05)) = 0
        // Mesafeyle incelen kontur: kucuk nesneler uzaktan siyah leke olmasin (1 = kapali, diger malzemeler etkilenmez)
        _OutlineFarMin ("Uzakta Kontur Carpani (1 = kapali)", Range(0,1)) = 1
        _OutlineFarStart ("Incelme Baslangic Mesafesi (m)", Float) = 3
        _OutlineFarEnd ("Incelme Bitis Mesafesi (m)", Float) = 10
        // Opsiyonel yukseklik gradyani (KitchenGradient bileseni yazar; _GradAmount=0 iken hicbir etkisi yok)
        _GradAmount ("Renk Gradyani Gucu", Range(0,1)) = 0
        _GradTop ("Gradyan Ust Ton", Color) = (1.08,1.05,0.95,1)
        _GradBottom ("Gradyan Alt Ton", Color) = (0.78,0.86,1.0,1)
        _GradY0 ("Gradyan Taban Y (m)", Float) = 0
        _GradH ("Gradyan Yukseklik (m)", Float) = 2.6
        _Realism ("Gerceklik (0 = saf cizgi film, 1 = cizgi film + PBR isik/yansima)", Range(0,1)) = 0
        _Smoothness ("Puruzsuzluk (Gerceklik > 0 iken)", Range(0,1)) = 0.3
        _Metallic ("Metalik (Gerceklik > 0 iken)", Range(0,1)) = 0
        _DetailAmt ("Detay Albedo Siddeti (PBR detay seti, 0 = kapali)", Range(0,1)) = 0
        _DetailMap ("Detay Albedo (gri ton, dunya uzayi projeksiyon)", 2D) = "white" {}
        [Normal] _DetailBump ("Detay Normal (dunya uzayi projeksiyon)", 2D) = "bump" {}
        _DetailBumpScale ("Detay Normal Siddeti", Range(0,2)) = 1
        _RoughMap ("Puruzluluk Haritasi (0.5 = nötr)", 2D) = "gray" {}
        _DetailScale ("Detay Olcegi (1/m)", Float) = 1
        _DetailNorm ("Detay Albedo Normalizasyon (1/ortalama)", Float) = 1
        _UseRoughUV ("Puruzluluk UV ile (1) / dunya uzayi (0)", Range(0,1)) = 0
        _WonkyOn ("Egri Bugru Bukme (1 = uygulanir, 0 = bu materyal bukulmez)", Range(0,1)) = 1
        // Kose rengi (Polyfork gibi doku yerine vertex color tasiyan modeller). 0 = kapali, eski materyaller etkilenmez.
        _UseVertexColor ("Kose Rengini Kullan (1 = acik)", Range(0,1)) = 0
        [IntRange] _StencilRef ("Kalem Cizgisi Stencil (2 = cizgi cizilsin)", Range(0,255)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            Stencil { Ref [_StencilRef] Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local_fragment _NORMALMAP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PCWonky.hlsl"
            #include "PCNoir.hlsl"
            #include "PCCourage.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BrushTex); SAMPLER(sampler_BrushTex);
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
            TEXTURE2D(_DetailBump); SAMPLER(sampler_DetailBump);
            TEXTURE2D(_RoughMap); SAMPLER(sampler_RoughMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor, _GradTop, _GradBottom; float4 _BaseMap_ST, _BumpMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength, _OutlineBias, _OutlineFarMin, _OutlineFarStart, _OutlineFarEnd, _GradAmount, _GradY0, _GradH, _Realism, _Smoothness, _Metallic, _DetailAmt, _DetailBumpScale, _DetailScale, _DetailNorm, _UseRoughUV, _WonkyOn, _UseVertexColor;
            CBUFFER_END
            // Kuresel palet ayari (ToonPaletteGrade yazar). Atanmazsa 0 = eski gorunum.
            half4 _PC_ShadowTint; half _PC_ShadowTintAmt, _PC_YellowCut, _PC_SatBoost, _PC_BrushCut;
            // Temiz/canli gorunum (ToonPaletteGrade yazar): golgeler gri-mavi camura donmez, renkli kalir; leke ve zemin/kose kararmasi azalir. 0 = eski.
            half _PC_Clean;
            // Kuresel gerceklik tabani: materyalin _Realism degeri bundan dusukse bu kullanilir (0 = eski). Uzaklik derinlik ipucu (0 = kapali).
            half _PC_Realism, _PC_Depth;
            // Oda isigi karartmasi (RoomLightSystem yazar). _PC_RoomCount = 0 ise hicbir etkisi yok.
            float4 _PC_RoomMin[10]; float4 _PC_RoomMax[10]; float _PC_RoomDark[10]; float _PC_RoomCount; half4 _PC_DarkTint;
            half PCRoomDark(float3 p)
            {
                half d = 0.0h;
                int cnt = (int)_PC_RoomCount;
                for (int k = 0; k < 10; k++)
                {
                    if (k >= cnt) break;
                    float3 mn = _PC_RoomMin[k].xyz; float3 mx = _PC_RoomMax[k].xyz;
                    if (p.x >= mn.x && p.x <= mx.x && p.y >= mn.y && p.y <= mx.y && p.z >= mn.z && p.z <= mx.z)
                        d = max(d, (half)_PC_RoomDark[k]);
                }
                return d;
            }

            struct A { float4 posOS : POSITION; float3 nOS : NORMAL; float4 tOS : TANGENT; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; half3 nWS : TEXCOORD1; float2 uv : TEXCOORD2; half4 tWS : TEXCOORD3; half3 col : TEXCOORD4; };

            V vert(A i)
            {
                V o;
                o.posWS = PCWonk(TransformObjectToWorld(i.posOS.xyz), _WonkyOn);
                o.nWS = TransformObjectToWorldNormal(i.nOS);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                o.tWS = half4(TransformObjectToWorldDir(i.tOS.xyz), i.tOS.w * GetOddNegativeScale());
                o.col = lerp(half3(1, 1, 1), i.col.rgb, _UseVertexColor);
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
                    half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv * _BumpMap_ST.xy + _BumpMap_ST.zw), _BumpScale);
                    half3 bWS = cross(n, i.tWS.xyz) * i.tWS.w;
                    n = normalize(tn.x * i.tWS.xyz + tn.y * bWS + tn.z * n);
                #endif
                // UV'den bagimsiz fircaizi: baskin eksen duzlemi (2 doku okuma; eski 3 eksenli karisim 5 okuma yapiyordu)
                half3 an = abs(n);
                float2 puv = (an.x >= an.y && an.x >= an.z) ? i.posWS.zy : (an.y >= an.z ? i.posWS.xz : i.posWS.xy);
                // Noir bolgesi: firca dokusu buyuk piksel bloklara oturur (dusuk cozunurluklu doku hissi)
                half nw = PCNoirZone(i.posWS);
                if (nw > 0.35h) puv = (floor(puv * 22.0) + 0.5) / 22.0;
                half b = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, puv * _BrushScale).r;
                half lf = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, puv * (_BrushScale * 0.18)).r * 2.0h;
                // fircaizi dokusu: _PC_BrushCut ile duzlestirilir (cizgi film = daha duz renk)
                half brush = 1 + ((b - 0.5h) * 2 * _BrushStrength + (lf - 1.0h) * lerp(0.22h, 0.08h, _PC_Clean)) * (1.0h - _PC_BrushCut);

                // PBR detay seti (varsayilan kapali): dunya uzayi projeksiyon; UV'den bagimsiz oldugu icin her mesh'te duzgun gorunur.
                half3 detailTint = half3(1, 1, 1);
                half rough = 0.5h;
                if (_DetailAmt > 0.001h)
                {
                    float2 duv = puv * _DetailScale;
                    half3 dn = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailBump, sampler_DetailBump, duv), _DetailBumpScale);
                    half3 dT = (an.x >= an.y && an.x >= an.z) ? half3(0, 0, 1) : half3(1, 0, 0);
                    half3 dB = (an.x >= an.y && an.x >= an.z) ? half3(0, 1, 0) : (an.y >= an.z ? half3(0, 0, 1) : half3(0, 1, 0));
                    n = normalize(dn.x * dT + dn.y * dB + dn.z * n);
                    detailTint = lerp(half3(1, 1, 1), SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, duv).rgb * _DetailNorm, _DetailAmt);
                    rough = SAMPLE_TEXTURE2D(_RoughMap, sampler_RoughMap, duv).r;
                }
                else if (_UseRoughUV > 0.5h)
                {
                    rough = SAMPLE_TEXTURE2D(_RoughMap, sampler_RoughMap, i.uv * _BumpMap_ST.xy + _BumpMap_ST.zw).r;
                }
                half smoothMul = saturate((1.0h - rough) * 2.0h);

                float4 sc = TransformWorldToShadowCoord(i.posWS);
                Light ml = GetMainLight(sc);
                half t = Ramp(dot(n, ml.direction), ml.shadowAttenuation);

                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb; tex *= detailTint * i.col;
                half3 lit = _BaseColor.rgb * tex;
                half3 shd = _ShadowColor.rgb * tex * lerp(half3(0.82h, 0.84h, 0.95h), half3(0.95h, 0.93h, 0.98h), _PC_Clean);
                // temiz modda golge, aydinlik rengin koyu ve doygun hali olur (gri camur yerine)
                half3 shdClean = lit * half3(0.62h, 0.60h, 0.70h);
                half shdL = dot(shdClean, half3(0.299h, 0.587h, 0.114h));
                shdClean = max(lerp(shdL.xxx, shdClean, 1.25h), 0.0h);
                shd = lerp(shd, lerp(shd, shdClean, 0.5h), _PC_Clean);
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
                // Duz PBR modu: URP Lit ile ayni isik hesabi (ana/ek isik, golge, SSAO, ortam isigi, yansima probu).
                // Cizgi film efektlerinin hicbiri uygulanmaz; sadece oda isigi karartmasi (oyun mekanigi) korunur.
                if (_PC_PureLit > 0.5h)
                {
                    InputData pIn = (InputData)0;
                    pIn.positionWS = i.posWS;
                    pIn.normalWS = n;
                    pIn.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.posWS);
                    pIn.shadowCoord = sc;
                    pIn.bakedGI = SampleSH(n);
                    pIn.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.posCS);
                    pIn.shadowMask = half4(1, 1, 1, 1);
                    SurfaceData pSurf = (SurfaceData)0;
                    pSurf.albedo = _BaseColor.rgb * tex;
                    pSurf.metallic = _Metallic;
                    pSurf.smoothness = _Smoothness * smoothMul;
                    pSurf.occlusion = 1.0h;
                    pSurf.emission = _EmissionColor.rgb;
                    pSurf.alpha = 1.0h;
                    pSurf.normalTS = half3(0, 0, 1);
                    half3 pc = UniversalFragmentPBR(pIn, pSurf).rgb;
                    half pDark = PCRoomDark(i.posWS) * (1.0h - saturate(lampT * 1.5h));
                    half pLum = dot(pc, half3(0.299h, 0.587h, 0.114h));
                    pc = lerp(pc, pLum.xxx, pDark * _PC_DarkTint.a);
                    pc *= lerp(half3(1, 1, 1), _PC_DarkTint.rgb, pDark);
                    return half4(pc, 1);
                }
                half tt = saturate(t + lampT * 0.9h);
                // Noir bolgesi: isik/golge gecisi yumusak degil, buyuk piksel bloklu Bayer dither (Shadow of Doubt)
                float nBayer = PCBayer4(i.posCS.xy / max(_PC_NoirP.x, 1.0));
                // once kontrast artirilir: genis alanlar duz kalir, dither sadece isik havuzu kenarlarinda ve golgede gorunur
                half ttHard = saturate((tt - 0.5h) * 2.6h + 0.5h);
                tt = lerp(tt, (half)step(nBayer, ttHard), saturate(nw * 4.0h));
                // Courage bolgesi: sert toon basamagi yerine genis, yumusak airbrush golge gecisi
                half cw = PCCourZone(i.posWS);
                if (cw > 0.001h)
                {
                    half soft = smoothstep(0.05h, 0.95h, dot(n, ml.direction) * 0.5h + 0.5h) * lerp(1.0h, ml.shadowAttenuation, 0.95h);
                    tt = lerp(tt, saturate(soft + lampT * 0.9h), cw * _PC_CourS.a);
                }
                // gokyuzu/zemin ortam yansimasi: golge tarafi yukari bakinca soguk, asagi bakinca sicak
                half3 amb = lerp(half3(0.62h, 0.50h, 0.38h), half3(0.62h, 0.76h, 1.0h), n.y * 0.5h + 0.5h);
                shd = lerp(shd, shd * amb * 1.55h, _AmbientStrength * 0.55h);
                // golgeye mor/lacivert palet tonu
                shd = lerp(shd, shd * _PC_ShadowTint.rgb, _PC_ShadowTintAmt);
                // yumusak ortam dolgusu: golge tarafi tamamen sonmesin (gercekciye yakin sekil okunur)
                shd += lit * SampleSH(n) * (0.18h * _PC_Clean);
                col = lerp(shd, lit, tt) * brush;
                // ucuncu parlak bant: isigin en cok vurdugu yer boyali vurgu
                half hiT = smoothstep(_Threshold + 0.26h - _Softness, _Threshold + 0.26h + _Softness, dot(n, ml.direction) * 0.5h + 0.5h) * ml.shadowAttenuation;
                col += lit * hiT * _HiStrength * brush;
                col *= lerp(half3(1,1,1), lerp(half3(1,1,1), warm, 0.35h * (1.0h - _PC_YellowCut)), saturate(lampT));
                // gunes rengi (lit kisima sicak ton)
                half3 sunTint = ml.color / max(max(ml.color.r, max(ml.color.g, ml.color.b)), 1e-3h);
                col *= lerp(half3(1,1,1), sunTint, 0.55h * (1.0h - _PC_YellowCut) * t);
                // zemine dogru boyali koyulasma
                half ao = lerp(1.0h - _GradStrength * lerp(1.3h, 0.6h, _PC_Clean), 1.0h, saturate(i.posWS.y * 0.8h));
                ao *= lerp(1.0h, lerp(0.86h, 0.94h, _PC_Clean), saturate((i.posWS.y - 2.1h) * 1.6h));
                col *= ao;
                // opsiyonel renk gradyani: alt koyu/soguk -> ust acik/sicak (kontur pass'ine dokunmaz)
                half gt = saturate((i.posWS.y - _GradY0) / max(_GradH, 0.01h));
                col *= lerp(half3(1,1,1), lerp(_GradBottom.rgb, _GradTop.rgb, gt), _GradAmount);
                // derinlik: uzaklastikca sicak-mavimsi pus
                float dist = distance(_WorldSpaceCameraPos, i.posWS);
                half fog = 1.0h - exp(-dist * 0.07h);
                col = lerp(col, lerp(half3(0.86h, 0.80h, 0.72h), half3(0.72h, 0.76h, 0.92h), _PC_YellowCut) * dot(col, half3(0.33h,0.33h,0.33h)) * 1.15h + half3(0.05h,0.05h,0.06h), fog * 0.0h);
                // stilize parlama + sicak kenar isigi
                half3 v = GetWorldSpaceNormalizeViewDir(i.posWS);
                half3 hv = normalize(ml.direction + v);
                half sp = smoothstep(_SpecSize, _SpecSize + 0.02h, dot(n, hv)) * t;
                half3 lampDir = half3(0,1,0);
                col += sp * _SpecStrength * half3(1.0h, 0.97h, 0.88h);
                half rim = smoothstep(0.55h, 0.85h, 1.0h - saturate(dot(n, v))) * saturate(t * 0.6h + 0.4h);
                col += rim * _RimStrength * half3(1.0h, 0.86h, 0.55h) * 0.22h;
                // hafif genis parlaklik: yuzeyler mat kil gibi degil, hafif cilali/plastik okunur
                col += lit * pow(saturate(dot(n, hv)), 24.0h) * (0.14h * _PC_Clean) * t;
                // Gerceklik (varsayilan 0 = hicbir etkisi yok): cizgi film rampasinin ustune yumusak sarmali difuz, ortam isigi (SH),
                // Blinn-Phong/Fresnel parlama ve ortam yansimasi karistirilir; kontur ve boyali gorunum korunur.
                half realism = max(_Realism, _PC_Realism);
                if (realism > 0.001h)
                {
                    half sm = _Smoothness * smoothMul;
                    half3 albedo = _BaseColor.rgb * tex * lerp(1.0h, brush, 0.5h);
                    half dl = dot(n, ml.direction);
                    half wrapN = saturate((dl + 0.4h) / 1.4h);
                    half3 gi = SampleSH(n);
                    half3 dif = albedo * (1.0h - _Metallic) * (ml.color * (wrapN * ml.shadowAttenuation) + gi * 0.9h + extra * 0.5h);
                    half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), albedo, _Metallic);
                    half pw = exp2(10.0h * sm + 1.0h);
                    half nh = saturate(dot(n, hv));
                    half sp2 = pow(nh, pw) * (pw + 8.0h) / (8.0h * 3.14159h) * saturate(dl) * ml.shadowAttenuation;
                    half fres = pow(1.0h - saturate(dot(n, v)), 5.0h);
                    half3 F = f0 + (1.0h - f0) * fres;
                    half3 refl = GlossyEnvironmentReflection(reflect(-v, n), 1.0h - sm, 1.0h);
                    half3 spc = ml.color * sp2 * F * 0.55h + refl * F * (sm * sm) * 1.4h;
                    half3 real = dif + spc;
                    // golge tarafi cizgi film palet tonunu korusun
                    real = lerp(real, real * lerp(half3(1,1,1), _PC_ShadowTint.rgb, _PC_ShadowTintAmt), saturate(1.0h - t) * 0.6h);
                    real *= ao; // zemine dogru / tavana dogru koyulasma gercekci isikta da gecerli
                    real = real / (1.0h + real * 0.12h); // yumusak tonlama: parlak alanlar patlamasin
                    col = lerp(col, real, saturate(realism * 1.15h));
                }
                // Ekran uzayi ortam golgelemesi (SSAO ozelligi aciksa): koselere, dolap altlarina temas golgesi
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoF = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.posCS));
                    col *= lerp(1.0h, aoF.indirectAmbientOcclusion, lerp(0.85h, 0.55h, _PC_Clean)) * lerp(1.0h, aoF.directAmbientOcclusion, lerp(0.5h, 0.3h, _PC_Clean));
                #endif
                // Elle cizim: golgede kalem taramasi (hatching) + kagit greni. Sadece CartoonWonky bolgesinde (_PC_HatchP.x > 0).
                if (_PC_HatchP.x > 0.001)
                {
                    float zw = PCZone(i.posWS);
                    if (zw > 0.001)
                    {
                        float fr = floor(_Time.y * _PC_WonkyP.y);
                        float2 sp = i.posCS.xy + 3.0 * float2(sin(i.posCS.y * 0.045 + fr * 1.7), sin(i.posCS.x * 0.04 + fr * 2.3));
                        float dark = saturate(1.0 - tt) * 0.85 + saturate(1.0 - ao) * 1.2 + _PC_HatchP.w; // w: aydinlik yuzeylerde bile hafif renkli kalem dolgusu
                        float sp1 = abs(frac((sp.x + sp.y) / _PC_HatchP.y) - 0.5);                // 45 derece kalem cizgileri
                        float sp2 = abs(frac((sp.x - sp.y) / (_PC_HatchP.y * 1.15)) - 0.5);       // karsi yon (koyu bolgelerde)
                        float l1 = 1.0 - smoothstep(0.10, 0.22, sp1);
                        float l2 = (1.0 - smoothstep(0.10, 0.22, sp2)) * smoothstep(0.55, 0.9, dark);
                        float strokes = saturate(max(l1 * smoothstep(0.18, 0.5, dark), l2));
                        half3 pencil = col * half3(0.16h, 0.15h, 0.18h); // siyah kalem darbesi
                        col = lerp(col, pencil, strokes * _PC_HatchP.x * zw);
                        // kagit greni: dusuk genlikli, kareli degisen gurultu
                        float g = frac(sin(dot(floor(sp * 0.7) + fr, float2(12.9898, 78.233))) * 43758.5453);
                        col *= 1.0 + (g - 0.5) * _PC_HatchP.z * zw;
                    }
                }
                // cizgi film doygunlugu (0 = eski)
                half lum = dot(col, half3(0.299h, 0.587h, 0.114h));
                col = max(lerp(lum.xxx, col, 1.0h + _PC_SatBoost * (1.0h - 0.9h * realism)), 0.0h);
                // canlilik (vibrance): soluk renkler daha cok, zaten doygun renkler az artar; ten/beyaz patlamaz
                half sat = (max(col.r, max(col.g, col.b)) - min(col.r, min(col.g, col.b))) / max(max(col.r, max(col.g, col.b)), 1e-3h);
                half lumV = dot(col, half3(0.299h, 0.587h, 0.114h));
                col = max(lerp(lumV.xxx, col, 1.0h + 0.18h * _PC_Clean * (1.0h - sat)), 0.0h);
                // Oda isigi kapaliysa karart; lamba/el feneri (ek isik) vuran yerler aydinlik kalir.
                half roomDark = PCRoomDark(i.posWS) * (1.0h - saturate(lampT * 1.5h));
                half darkLum = dot(col, half3(0.299h, 0.587h, 0.114h));
                col = lerp(col, darkLum.xxx, roomDark * _PC_DarkTint.a); // karanlikta renkler soner (gece gorusu gibi, mor/mavi olmaz)
                col *= lerp(half3(1,1,1), _PC_DarkTint.rgb, roomDark);
                // Noir bolgesi (Shadow of Doubt): los ortam + sicak isik havuzlari, soluk renkler, teal golge, posterize, tane.
                if (nw > 0.001h)
                {
                    half pool = saturate(lampT * 1.4h);
                    col *= lerp(1.0h, lerp(1.0h - _PC_NoirP.w, 1.0h, pool), nw);
                    half lumN = dot(col, half3(0.299h, 0.587h, 0.114h));
                    col = lerp(col, lerp(lumN.xxx, col, 1.0h - _PC_NoirP.z), nw);
                    half lumG = dot(col, half3(0.299h, 0.587h, 0.114h));
                    col *= lerp(half3(1,1,1), lerp(_PC_NoirT.rgb, _PC_NoirL.rgb, saturate(lumG * 1.6h)), nw * 0.85h);
                    half lv = max(_PC_NoirP.y, 2.0h);
                    // parlakliga gore bantlama: kanal basina posterize ton kaydirir (mor/pembe), parlaklik bantlari kaydirmaz
                    half lumQ = dot(col, half3(0.299h, 0.587h, 0.114h));
                    half3 q = col * (floor(lumQ * lv + 0.5h) / lv / max(lumQ, 0.02h));
                    col = lerp(col, q, saturate(nw * 4.0h));
                    half gr = PCNoirHash(floor(i.posCS.xy / max(_PC_NoirP.x, 1.0)) + floor(_Time.y * 12.0));
                    col *= 1.0h + (gr - 0.5h) * _PC_NoirL.a * nw;
                }
                // Courage bolgesi (Cesur Korkak Kopek): elle boyanmis guaj alacasi, soluk 90'lar paleti,
                // mor-kahve golge / krem-hardal isik, kose-tavan airbrush kararmasi, sabit kagit greni.
                if (cw > 0.001h)
                {
                    float2 mp = (an.x >= an.y && an.x >= an.z) ? i.posWS.zy : (an.y >= an.z ? i.posWS.xz : i.posWS.xy);
                    col *= 1.0h + (half)PCCourMottle(mp) * _PC_CourP.y * cw;
                    half lc = dot(col, half3(0.299h, 0.587h, 0.114h));
                    col = lerp(col, lerp(lc.xxx, col, 1.0h - _PC_CourP.x), cw);
                    half lt = dot(col, half3(0.299h, 0.587h, 0.114h));
                    half3 tone = lerp(_PC_CourS.rgb, _PC_CourL.rgb, smoothstep(0.08h, 0.8h, lt));
                    col = lerp(col, col * tone, cw);
                    // soluk baski: siyahlar mor-kahveye kalkar, beyazlar kremlesir
                    col = lerp(col, col * 0.86h + _PC_CourS.rgb * 0.06h, cw);
                    half vig = saturate(PCCourEdge(i.posWS) * 0.7h + smoothstep(1.9h, 2.8h, i.posWS.y) * 0.8h + (1.0h - smoothstep(0.0h, 0.45h, i.posWS.y)) * 0.35h);
                    half3 vigCol = lerp(half3(1, 1, 1), half3(0.5h, 0.4h, 0.5h), vig);
                    col *= lerp(half3(1, 1, 1), vigCol, _PC_CourL.a * cw);
                    half g = PCCourHash(floor(i.posCS.xy * 0.5));
                    col *= 1.0h + (g - 0.5h) * _PC_CourP.z * cw;
                }
                // hava perspektifi: uzak yuzeyler hafifce kontrast/doygunluk kaybeder (sis degil; derinlik ipucu)
                if (_PC_Depth > 0.001h)
                {
                    half aerial = (1.0h - exp(-dist * 0.045h)) * _PC_Depth;
                    half lumA = dot(col, half3(0.299h, 0.587h, 0.114h));
                    col = lerp(col, lerp(lumA.xxx, col, 0.6h) * 0.9h + half3(0.045h, 0.05h, 0.06h), aerial);
                }
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
            #include "PCWonky.hlsl"
            #include "PCNoir.hlsl"
            #include "PCCourage.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor, _GradTop, _GradBottom; float4 _BaseMap_ST, _BumpMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength, _OutlineBias, _OutlineFarMin, _OutlineFarStart, _OutlineFarEnd, _GradAmount, _GradY0, _GradH, _Realism, _Smoothness, _Metallic, _DetailAmt, _DetailBumpScale, _DetailScale, _DetailNorm, _UseRoughUV, _WonkyOn, _UseVertexColor;
            CBUFFER_END

            half _PC_OutlineCut; // kuresel kontur inceltme (0 = eski kalinlik)
            half _PC_OutlineFar; // kuresel uzak kontur carpani: > 0 ise materyalin _OutlineFarMin'i en fazla bu olur (derinlik hissi)

            struct A { float4 posOS : POSITION; float3 nOS : NORMAL; };
            struct V { float4 posCS : SV_POSITION; half cw : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                // Derinlik payi gorus uzayinda metre cinsinden uygulanir. NDC'de sabit olsaydi
                // uzakta (z ~ 1/d) metrelerce kayar, kontur kabugu on yuzleri kapatirdi.
                float3 pvs = TransformWorldToView(PCWonk(TransformObjectToWorld(i.posOS.xyz), _WonkyOn));
                pvs.z += _OutlineBias; // gorus uzayinda kamera -Z'ye bakar; +Z kameraya dogru
                float4 cs = mul(UNITY_MATRIX_P, float4(pvs, 1.0));
                float3 nVS = mul((float3x3)UNITY_MATRIX_IT_MV, i.nOS);
                float2 nCS = normalize(mul((float2x2)UNITY_MATRIX_P, nVS.xy) + 1e-5);
                // Kalem baskisi: kontur kalinligi çizgi boyunca hafifçe değişir (elle çizilmiş görünüm).
                float3 pw = PCWonk(TransformObjectToWorld(i.posOS.xyz), _WonkyOn);
                float wobble = 1.0 + 0.22 * sin(dot(pw, float3(7.3, 5.1, 6.7))) + 0.12 * sin(dot(pw, float3(23.0, 19.0, 17.0)));
                // Courage bolgesi: ince, daha titrek elle cizilmis kontur
                float cwo = PCCourZone(pw);
                o.cw = (half)cwo;
                wobble = lerp(wobble, _PC_CourP.w * (1.0 + 0.45 * sin(dot(pw, float3(11.3, 8.1, 9.7))) + 0.2 * sin(dot(pw, float3(31.0, 27.0, 23.0)))), saturate(cwo));
                // Uzaklastikca kontur incelir (ekranda sabit piksel olursa kucuk nesne siyah leke gorunur).
                float farT = saturate((length(pvs) - _OutlineFarStart) / max(0.01, _OutlineFarEnd - _OutlineFarStart));
                float farMin = _PC_OutlineFar > 0.001 ? min(_OutlineFarMin, _PC_OutlineFar) : _OutlineFarMin;
                float farScale = lerp(1.0, farMin, farT);
                // Noir bolgesinde kontur silinir (Shadow of Doubt konturlu degil)
                float noirCut = 1.0 - saturate(PCNoirZone(pw) * 1.5) * _PC_NoirT.a;
                float2 px = nCS * (_OutlinePixels * (1.0 - _PC_OutlineCut) * wobble * farScale * noirCut * 2.0) / _ScreenParams.xy;
                cs.xy += px * cs.w;
                o.posCS = _PC_PureLit > 0.5h ? float4(2, 2, 2, 1) : cs; // duz PBR modunda kontur yok (kirpma disina atilir)
                return o;
            }
            half4 frag(V i) : SV_Target { return half4(lerp(_OutlineColor.rgb, _PC_CourO.rgb, saturate(i.cw) * _PC_CourO.a), 1); }
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
            #include "PCWonky.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor, _GradTop, _GradBottom; float4 _BaseMap_ST, _BumpMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength, _OutlineBias, _OutlineFarMin, _OutlineFarStart, _OutlineFarEnd, _GradAmount, _GradY0, _GradH, _Realism, _Smoothness, _Metallic, _DetailAmt, _DetailBumpScale, _DetailScale, _DetailNorm, _UseRoughUV, _WonkyOn, _UseVertexColor;
            CBUFFER_END
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 posOS : POSITION; float3 nOS : NORMAL; };
            struct V { float4 posCS : SV_POSITION; };
            V vert(A i)
            {
                V o;
                float3 pw = PCWonk(TransformObjectToWorld(i.posOS.xyz), _WonkyOn);
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
            #include "PCWonky.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor, _GradTop, _GradBottom; float4 _BaseMap_ST, _BumpMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength, _OutlineBias, _OutlineFarMin, _OutlineFarStart, _OutlineFarEnd, _GradAmount, _GradY0, _GradH, _Realism, _Smoothness, _Metallic, _DetailAmt, _DetailBumpScale, _DetailScale, _DetailNorm, _UseRoughUV, _WonkyOn, _UseVertexColor;
            CBUFFER_END
            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; };
            V vert(A i) { V o; o.posCS = TransformWorldToHClip(PCWonk(TransformObjectToWorld(i.posOS.xyz), _WonkyOn)); return o; }
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
            #include "PCWonky.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor, _EmissionColor, _OutlineColor, _GradTop, _GradBottom; float4 _BaseMap_ST, _BumpMap_ST;
                half _Threshold, _Softness, _BrushScale, _BrushStrength, _LampBoost, _OutlinePixels, _SpecStrength, _SpecSize, _RimStrength, _GradStrength, _BumpScale, _HiStrength, _AmbientStrength, _OutlineBias, _OutlineFarMin, _OutlineFarStart, _OutlineFarEnd, _GradAmount, _GradY0, _GradH, _Realism, _Smoothness, _Metallic, _DetailAmt, _DetailBumpScale, _DetailScale, _DetailNorm, _UseRoughUV, _WonkyOn, _UseVertexColor;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float3 nOS : NORMAL; };
            struct V { float4 posCS : SV_POSITION; half3 n : TEXCOORD0; };
            V vert(A i) { V o; o.posCS = TransformWorldToHClip(PCWonk(TransformObjectToWorld(i.posOS.xyz), _WonkyOn)); o.n = TransformObjectToWorldNormal(i.nOS); return o; }
            half4 frag(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.n), 0); }
            ENDHLSL
        }
    }
}
