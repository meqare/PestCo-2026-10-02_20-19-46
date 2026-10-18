// Cizgi film "egri bugru" dunya: dunya uzayinda SUREKLI bir bukme alani (komsu nesneler arasinda bosluk acilmaz).
// CartoonWonky bileseni global'leri yazar; _PC_WonkyC.w = 0 iken hicbir etkisi yok.
#ifndef PC_WONKY_INCLUDED
#define PC_WONKY_INCLUDED

float4 _PC_WonkyC; // xyz: bolge merkezi, w: bukme miktari (0 = kapali)
float4 _PC_WonkyE; // xyz: bolge yari boyutlari, w: yumusak kenar (m)
float4 _PC_WonkyP; // x: cizgi titremesi (m), y: titreme fps
float4 _PC_HatchP; // x: tarama (hatching) siddeti, y: cizgi aralik (px), z: kagit greni, w: aydinlik dolgu tarama

float PCZone(float3 pw)
{
    if (_PC_WonkyC.w <= 0.0) return 0.0;
    float3 q = abs(pw - _PC_WonkyC.xyz) - _PC_WonkyE.xyz;
    return 1.0 - smoothstep(0.0, max(_PC_WonkyE.w, 0.01), max(q.x, max(q.y, q.z)));
}

float3 PCWonk(float3 pw, half on)
{
    if (_PC_WonkyC.w <= 0.0 || on < 0.5h) return pw;
    float3 q = abs(pw - _PC_WonkyC.xyz) - _PC_WonkyE.xyz;
    float zone = 1.0 - smoothstep(0.0, max(_PC_WonkyE.w, 0.01), max(q.x, max(q.y, q.z)));
    if (zone <= 0.0) return pw;
    float a = _PC_WonkyC.w * zone;
    float h = max(pw.y - _PC_WonkyC.y + _PC_WonkyE.y, 0.0);   // zeminden yukseklik: yukari cikildikca daha cok yatar
    // dolap/duvarlar yukari dogru yana yatar (sabit kayma degil, yukseklikle artan)
    // dusuk frekans = komsu parcalar birlikte yatar (cikinti/yirtik olmaz), genlik buyuk = abartili
    float2 lean = float2(sin(pw.z * 0.55 + 1.3) + 0.45 * sin(pw.z * 1.4), cos(pw.x * 0.5 + 0.4) + 0.45 * sin(pw.x * 1.3 + 2.0));
    pw.xz += a * 0.075 * h * lean;
    // yumusak dalgali bukulme (duz cizgiler kemerlenir)
    pw.y += a * 0.045 * sin(pw.x * 1.1 + pw.z * 0.9);
    pw.x += a * 0.05 * sin(pw.y * 1.9 + pw.z * 1.2);
    pw.z += a * 0.05 * sin(pw.y * 1.7 + pw.x * 1.4);
    // elle cizim titremesi (line boil): dusuk fps'te degisen kucuk kayma
    float t = floor(_Time.y * _PC_WonkyP.y);
    pw += zone * _PC_WonkyP.x * float3(sin(dot(pw, float3(9.1, 7.3, 5.7)) + t * 1.7),
                                       sin(dot(pw, float3(6.3, 8.9, 4.1)) + t * 2.3),
                                       sin(dot(pw, float3(7.7, 5.9, 9.3)) + t * 3.1));
    return pw;
}
#endif
