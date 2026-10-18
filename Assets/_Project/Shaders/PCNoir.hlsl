// "Noir dedektif" bolgesi (Shadow of Doubt tarzi): kutu icinde dithered gölge/isik gecisi, posterize palet,
// soluk renkler, loş ortam + sicak isik havuzlari, grenli piksel blok dokusu, kontursuz yuzeyler.
// NoirZone bileseni global'leri yazar; _PC_NoirC.w = 0 iken hicbir etkisi yok (eski gorunum).
#ifndef PC_NOIR_INCLUDED
#define PC_NOIR_INCLUDED

float4 _PC_NoirC; // xyz: bolge merkezi, w: genel miktar (0 = kapali)
float4 _PC_NoirE; // xyz: yari boyutlar, w: yumusak kenar (m)
float4 _PC_NoirP; // x: dither blok boyutu (px), y: posterize seviye sayisi, z: renk soldurma (0..1), w: ortam karartma (0..1)
float4 _PC_NoirT; // rgb: golge tonu (teal), a: kontur silme (1 = kontur yok)
float4 _PC_NoirL; // rgb: isik tonu (sicak), a: tane (grain) miktari

// 0..1 bolge agirligi (miktar dahil).
float PCNoirZone(float3 pw)
{
    if (_PC_NoirC.w <= 0.0) return 0.0;
    float3 q = abs(pw - _PC_NoirC.xyz) - _PC_NoirE.xyz;
    return (1.0 - smoothstep(0.0, max(_PC_NoirE.w, 0.01), max(q.x, max(q.y, q.z)))) * _PC_NoirC.w;
}

float PCBayer2(float2 a) { return 2.0 * a.x + 3.0 * a.y - 4.0 * a.x * a.y; }

// 4x4 siralanmis (Bayer) dither esigi, 0..1. p: ekran pikseli / blok boyutu.
float PCBayer4(float2 p)
{
    p = floor(p);
    float2 lo = fmod(p, 2.0);
    float2 hi = fmod(floor(p * 0.5), 2.0);
    return (4.0 * PCBayer2(lo) + PCBayer2(hi) + 0.5) / 16.0;
}

float PCNoirHash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}
#endif
