// "Cesur Korkak Kopek" bolgesi: soluk/tozlu 90'lar paleti (mor-kahve golge, krem-hardal isik), yumusak airbrush golge,
// elle boyanmis guaj dokusu (alacali leke + kagit greni), kose/tavan kararmasi, ince koyu kahve titrek kontur.
// CourageZone bileseni global'leri yazar; _PC_CourC.w = 0 iken hicbir etkisi yok (eski gorunum).
#ifndef PC_COURAGE_INCLUDED
#define PC_COURAGE_INCLUDED

float4 _PC_CourC; // xyz: bolge merkezi, w: genel miktar (0 = kapali)
float4 _PC_CourE; // xyz: yari boyutlar, w: yumusak kenar (m)
float4 _PC_CourP; // x: renk soldurma, y: boya alacasi, z: kagit greni, w: kontur kalinlik carpani
float4 _PC_CourS; // rgb: golge tonu (mor-kahve), a: golge yumusakligi (airbrush)
float4 _PC_CourL; // rgb: isik tonu (krem-hardal), a: kose/tavan kararmasi
float4 _PC_CourO; // rgb: kontur rengi (koyu kahve), a: kontur rengi karisimi

// 0..1 bolge agirligi (miktar dahil).
float PCCourZone(float3 pw)
{
    if (_PC_CourC.w <= 0.0) return 0.0;
    float3 q = abs(pw - _PC_CourC.xyz) - _PC_CourE.xyz;
    return (1.0 - smoothstep(0.0, max(_PC_CourE.w, 0.01), max(q.x, max(q.y, q.z)))) * _PC_CourC.w;
}

// Kutunun duvarlarina yakinlik (0 = ortada, 1 = duvar/kose dibinde): airbrush kose kararmasi icin.
float PCCourEdge(float3 pw)
{
    float2 d = _PC_CourE.xz - abs(pw.xz - _PC_CourC.xz);   // duvara kalan mesafe (m)
    float e = (1.0 - smoothstep(0.0, 1.1, d.x)) + (1.0 - smoothstep(0.0, 1.1, d.y));
    return saturate(e * 0.6);
}

float PCCourHash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float PCCourVNoise(float2 p)
{
    float2 i = floor(p); float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = PCCourHash(i), b = PCCourHash(i + float2(1, 0)), c = PCCourHash(i + float2(0, 1)), d = PCCourHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Guaj/suluboya alacasi: dunya uzayi duzlem koordinatinda 3 oktav deger gurultusu, -0.5..0.5
float PCCourMottle(float2 p)
{
    return PCCourVNoise(p * 1.3) * 0.55 + PCCourVNoise(p * 3.7 + 11.3) * 0.3 + PCCourVNoise(p * 11.0 + 5.1) * 0.15 - 0.5;
}
#endif
