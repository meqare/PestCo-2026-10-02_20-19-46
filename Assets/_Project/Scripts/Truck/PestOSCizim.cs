using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PestCo.Truck
{
    /// <summary>
    /// PestOS görselleri kodla, "elle çizilmiş" çizgi film stilinde üretilir (oyunun Cesur Korkak Köpek havasıyla uyumlu):
    /// SDF şekiller + kahve mürekkep kontur + hafif titrek çizgi. İkonlar, böcek maskotu, Hiçlik gecesi duvar kağıdı,
    /// tarama çizgisi / vinyet / kağıt greni dokuları. Her şey bir kez üretilip önbelleğe alınır.
    /// </summary>
    public static class PestOSCizim
    {
        public static readonly Color Murekkep = new Color(0.17f, 0.1f, 0.08f);
        public static readonly Color BocekYesil = new Color(0.44f, 0.76f, 0.26f);
        private static readonly Color Kagit = new Color(0.97f, 0.93f, 0.82f);
        private static readonly Color Altin = new Color(0.95f, 0.74f, 0.26f);
        private static readonly Color Pembe = new Color(0.96f, 0.58f, 0.64f);
        private static readonly Color AgizIci = new Color(0.18f, 0.02f, 0.05f);

        private static readonly Dictionary<string, Sprite> onbellek = new Dictionary<string, Sprite>();

        // ------------------------------------------------------------------ TUVAL

        private delegate float Sdf(Vector2 p);

        private class Tuval
        {
            private readonly int w, h;
            private readonly Color[] px;
            private readonly float titreme, frekans, tohum;

            public Tuval(int w, int h, float titreme = 0.9f, float frekans = 0.16f, float tohum = 1.7f)
            {
                this.w = w; this.h = h;
                px = new Color[w * h];
                this.titreme = titreme; this.frekans = frekans; this.tohum = tohum;
            }

            private Vector2 Titret(Vector2 p) => titreme <= 0f ? p :
                p + titreme * new Vector2(Mathf.Sin(p.y * frekans + tohum), Mathf.Cos(p.x * frekans * 1.3f + tohum * 1.7f));

            // Kontur şeklin DIŞINA çizilir (d ∈ [0, kontur]); içi dolgu.
            public void Ciz(Sdf sdf, Color dolgu, float kontur = 3.5f, Color? konturRenk = null)
            {
                var ink = konturRenk ?? Murekkep;
                // 8x8 bloklar: blok merkezi şekilden yeterince uzaksa bloğun tamamı atlanır (SDF mesafe alt sınırı verir)
                const int B = 8;
                float esik = kontur + 2f + B * 0.7072f + titreme * 1.5f;
                for (int by = 0; by < h; by += B)
                    for (int bx = 0; bx < w; bx += B)
                    {
                        if (sdf(new Vector2(bx + B * 0.5f, by + B * 0.5f)) > esik) continue;
                        int ymax = Mathf.Min(by + B, h), xmax = Mathf.Min(bx + B, w);
                        for (int y = by; y < ymax; y++)
                            for (int x = bx; x < xmax; x++)
                            {
                                float d = sdf(Titret(new Vector2(x + 0.5f, y + 0.5f)));
                                if (d > kontur + 1f) continue;
                                int i = y * w + x;
                                if (kontur > 0f) Karistir(i, ink, Mathf.Clamp01(0.5f - (d - kontur)) * ink.a);
                                Karistir(i, dolgu, Mathf.Clamp01(0.5f - d) * dolgu.a);
                            }
                    }
            }

            private void Karistir(int i, Color c, float a)
            {
                if (a <= 0f) return;
                var d = px[i];
                float oa = a + d.a * (1f - a);
                if (oa <= 0f) return;
                px[i] = new Color(
                    (c.r * a + d.r * d.a * (1f - a)) / oa,
                    (c.g * a + d.g * d.a * (1f - a)) / oa,
                    (c.b * a + d.b * d.a * (1f - a)) / oa, oa);
            }

            public Sprite Bitir(string ad)
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = ad };
                tex.SetPixels(px);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        // ------------------------------------------------------------------ SDF ŞEKİLLER

        private static Sdf Daire(float cx, float cy, float r) => p => Vector2.Distance(p, new Vector2(cx, cy)) - r;

        private static Sdf Elips(float cx, float cy, float rx, float ry) => p =>
        {
            var q = new Vector2((p.x - cx) / rx, (p.y - cy) / ry);
            return (q.magnitude - 1f) * Mathf.Min(rx, ry);
        };

        private static Sdf Kutu(float cx, float cy, float yx, float yy, float kose = 0f, float aci = 0f) => p =>
        {
            var q = Dondur(p - new Vector2(cx, cy), -aci);
            float qx = Mathf.Abs(q.x) - yx + kose, qy = Mathf.Abs(q.y) - yy + kose;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - kose;
        };

        private static Sdf Cizgi(float ax, float ay, float bx, float by, float r) => p =>
        {
            var a = new Vector2(ax, ay);
            Vector2 pa = p - a, ba = new Vector2(bx, by) - a;
            float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * t).magnitude - r;
        };

        private static Sdf Ucgen(Vector2 p0, Vector2 p1, Vector2 p2) => p =>
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(Vector2.Min(
                new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        };

        private static Sdf Birlesim(params Sdf[] s) => p =>
        {
            float d = float.MaxValue;
            foreach (var f in s) d = Mathf.Min(d, f(p));
            return d;
        };

        private static Vector2 Dondur(Vector2 v, float aciDerece)
        {
            float r = aciDerece * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // Merkez etrafında döndürülmüş nokta (ikon parçalarını eğik kağıda oturtmak için)
        private static Vector2 D(float x, float y, float cx, float cy, float aci) => new Vector2(cx, cy) + Dondur(new Vector2(x - cx, y - cy), aci);

        private static Sprite Onbellekli(string anahtar, Func<Sprite> uret)
        {
            if (onbellek.TryGetValue(anahtar, out var s) && s != null) return s;
            s = uret();
            onbellek[anahtar] = s;
            return s;
        }

        // ------------------------------------------------------------------ UYGULAMA İKONLARI (128 px)

        public static Sprite Ikon(string id) => Onbellekli("ikon_" + id, () => id switch
        {
            "gorev" => Pano(),
            "market" => SpreyKutusu(),
            "fanus" => Kavanoz(),
            "banka" => Paralar(),
            "not" => NotDefteri(),
            "ayar" => Disli(),
            _ => Bocek(BocekYesil, false)
        });

        private static Sprite Pano()
        {
            var t = new Tuval(128, 128);
            const float a = -6f, cx = 64f, cy = 58f;
            t.Ciz(Kutu(cx, cy, 40, 52, 9, a), new Color(0.74f, 0.46f, 0.24f), 4f);
            t.Ciz(Kutu(cx, cy - 4, 31, 41, 4, a), Kagit, 3f);
            for (int i = 0; i < 4; i++)
            {
                var p0 = D(52, 76 - i * 15, cx, cy, a); var p1 = D(86, 76 - i * 15, cx, cy, a);
                t.Ciz(Cizgi(p0.x, p0.y, p1.x, p1.y, 2f), new Color(0.55f, 0.45f, 0.4f), 0f);
                var k = D(42, 76 - i * 15, cx, cy, a);
                t.Ciz(Kutu(k.x, k.y, 5, 5, 1.5f, a), Color.white, 2f);
            }
            // Yeşil tik
            var c0 = D(37, 78, cx, cy, a); var c1 = D(42, 71, cx, cy, a); var c2 = D(53, 88, cx, cy, a);
            t.Ciz(Birlesim(Cizgi(c0.x, c0.y, c1.x, c1.y, 3.4f), Cizgi(c1.x, c1.y, c2.x, c2.y, 3.4f)), new Color(0.36f, 0.72f, 0.24f), 2.5f);
            var m = D(64, 110, cx, cy, a);
            t.Ciz(Kutu(m.x, m.y, 17, 9, 4, a), new Color(0.66f, 0.66f, 0.64f), 3.5f);
            t.Ciz(Daire(m.x, m.y + 2, 3.5f), new Color(0.25f, 0.2f, 0.18f), 0f);
            return t.Bitir("Ikon_Gorev");
        }

        private static Sprite SpreyKutusu()
        {
            var t = new Tuval(128, 128);
            const float a = 8f, cx = 66f, cy = 50f;
            // Sis bulutu
            t.Ciz(Daire(30, 110, 7), new Color(0.86f, 0.95f, 0.74f), 2.5f);
            t.Ciz(Daire(18, 98, 5), new Color(0.86f, 0.95f, 0.74f), 2.5f);
            t.Ciz(Daire(32, 92, 4), new Color(0.86f, 0.95f, 0.74f), 2.5f);
            t.Ciz(Daire(16, 116, 3.5f), new Color(0.86f, 0.95f, 0.74f), 2.5f);
            var kap = D(cx, cy + 46, cx, cy, a);
            t.Ciz(Kutu(kap.x, kap.y, 14, 9, 3, a), new Color(0.7f, 0.7f, 0.68f), 3.5f);
            var uc = D(cx - 8, cy + 58, cx, cy, a);
            t.Ciz(Kutu(uc.x, uc.y, 8, 5, 2, a), new Color(0.3f, 0.26f, 0.24f), 3f);
            t.Ciz(Kutu(cx, cy, 25, 40, 10, a), new Color(0.84f, 0.3f, 0.24f), 4f);
            t.Ciz(Kutu(cx, cy - 2, 25, 14, 2, a), new Color(0.98f, 0.8f, 0.34f), 3f);
            // Etiketteki minik böcek + çarpı
            var b = D(cx, cy - 2, cx, cy, a);
            t.Ciz(Daire(b.x, b.y, 8), BocekYesil, 2.5f);
            t.Ciz(Cizgi(b.x - 9, b.y - 9, b.x + 9, b.y + 9, 2.2f), new Color(0.75f, 0.15f, 0.12f), 0f);
            t.Ciz(Cizgi(b.x - 9, b.y + 9, b.x + 9, b.y - 9, 2.2f), new Color(0.75f, 0.15f, 0.12f), 0f);
            // Parlama
            var p0 = D(cx - 16, cy + 30, cx, cy, a); var p1 = D(cx - 16, cy + 16, cx, cy, a);
            t.Ciz(Cizgi(p0.x, p0.y, p1.x, p1.y, 3f), new Color(1f, 1f, 1f, 0.55f), 0f);
            return t.Bitir("Ikon_Market");
        }

        private static Sprite Kavanoz()
        {
            var t = new Tuval(128, 128);
            t.Ciz(Kutu(64, 52, 38, 44, 15), new Color(0.72f, 0.88f, 0.84f, 1f), 4f);
            BocekCiz(t, 64, 46, 0.42f, BocekYesil, false);
            t.Ciz(Kutu(64, 52, 38, 44, 15), new Color(0.75f, 0.92f, 0.88f, 0.28f), 0f); // cam
            t.Ciz(Cizgi(38, 34, 38, 74, 4f), new Color(1f, 1f, 1f, 0.6f), 0f);
            t.Ciz(Cizgi(46, 80, 46, 86, 3f), new Color(1f, 1f, 1f, 0.6f), 0f);
            t.Ciz(Kutu(64, 102, 34, 10, 4), new Color(0.95f, 0.72f, 0.26f), 4f);
            for (int i = -2; i <= 2; i++) t.Ciz(Cizgi(64 + i * 11, 96, 64 + i * 11, 108, 1.4f), new Color(0.55f, 0.36f, 0.12f), 0f);
            return t.Bitir("Ikon_Fanus");
        }

        private static Sprite Paralar()
        {
            var t = new Tuval(128, 128);
            t.Ciz(Kutu(60, 76, 42, 25, 4, -12f), new Color(0.58f, 0.78f, 0.46f), 4f);
            t.Ciz(Elips(60, 76, 13, 15), new Color(0.72f, 0.88f, 0.6f), 2.5f);
            t.Ciz(Cizgi(60, 66, 60, 86, 2.2f), new Color(0.24f, 0.42f, 0.18f), 0f);
            for (int i = 0; i < 4; i++)
            {
                t.Ciz(Elips(88, 22 + i * 10, 25, 9), Altin, 3.5f);
                t.Ciz(Elips(88, 24 + i * 10, 25, 7), new Color(0.99f, 0.84f, 0.4f), 0f);
            }
            t.Ciz(Daire(40, 38, 23), Altin, 4f);
            t.Ciz(Daire(40, 38, 15), new Color(0.88f, 0.62f, 0.18f), 2.5f);
            t.Ciz(Cizgi(40, 31, 40, 45, 2.4f), new Color(0.55f, 0.33f, 0.08f), 0f);
            t.Ciz(Cizgi(31, 46, 37, 52, 2.2f), new Color(1f, 1f, 1f, 0.7f), 0f);
            return t.Bitir("Ikon_Banka");
        }

        private static Sprite NotDefteri()
        {
            var t = new Tuval(128, 128);
            const float a = 5f, cx = 54f, cy = 60f;
            t.Ciz(Kutu(cx, cy, 34, 46, 5, a), Kagit, 4f);
            var sp = D(cx, cy + 44, cx, cy, a);
            t.Ciz(Kutu(sp.x, sp.y, 34, 8, 3, a), new Color(0.82f, 0.3f, 0.26f), 3.5f);
            for (int i = 0; i < 5; i++)
            {
                var p0 = D(cx - 24, cy + 24 - i * 13, cx, cy, a); var p1 = D(cx + 24, cy + 24 - i * 13, cx, cy, a);
                t.Ciz(Cizgi(p0.x, p0.y, p1.x, p1.y, 1.5f), new Color(0.45f, 0.6f, 0.82f), 0f);
            }
            var k0 = D(cx - 20, cy + 34, cx, cy, a); var k1 = D(cx - 20, cy - 42, cx, cy, a);
            t.Ciz(Cizgi(k0.x, k0.y, k1.x, k1.y, 1.2f), new Color(0.88f, 0.4f, 0.4f), 0f);
            // Kalem
            Vector2 uc = new Vector2(72, 30), son = new Vector2(108, 94);
            var yon = (uc - son).normalized; var dik = new Vector2(-yon.y, yon.x);
            t.Ciz(Cizgi(son.x, son.y, son.x - yon.x * 9f, son.y - yon.y * 9f, 8f), Pembe, 3f);
            t.Ciz(Cizgi(uc.x, uc.y, son.x, son.y, 8f), new Color(0.98f, 0.78f, 0.24f), 3.5f);
            var tepe = uc + yon * 17f;
            t.Ciz(Ucgen(uc + dik * 8.5f, uc - dik * 8.5f, tepe), new Color(0.96f, 0.84f, 0.62f), 3f);
            t.Ciz(Ucgen(tepe - yon * 6f + dik * 3f, tepe - yon * 6f - dik * 3f, tepe), Murekkep, 0f);
            return t.Bitir("Ikon_Not");
        }

        private static Sprite Disli()
        {
            var t = new Tuval(128, 128);
            var parcalar = new List<Sdf> { Daire(64, 64, 36) };
            for (int i = 0; i < 8; i++)
            {
                float ac = i * 45f;
                var c = new Vector2(64, 64) + Dondur(new Vector2(0, 42), ac);
                parcalar.Add(Kutu(c.x, c.y, 9, 11, 3, ac));
            }
            t.Ciz(Birlesim(parcalar.ToArray()), new Color(0.6f, 0.66f, 0.68f), 4f);
            t.Ciz(Daire(64, 64, 22), new Color(0.72f, 0.78f, 0.8f), 0f);
            t.Ciz(Daire(64, 64, 12), new Color(0.32f, 0.24f, 0.3f), 4f);
            t.Ciz(Cizgi(42, 78, 50, 88, 3f), new Color(1f, 1f, 1f, 0.6f), 0f);
            return t.Bitir("Ikon_Ayar");
        }

        // ------------------------------------------------------------------ YARATIKLAR

        /// <summary>Koca gözlü çizgi film böceği (oyundaki haşere tasarımıyla aynı aile). Renk istenen tonda.</summary>
        public static Sprite Bocek(Color renk, bool sinirli = false)
        {
            Color32 c = renk;
            return Onbellekli($"bocek_{c.r}_{c.g}_{c.b}_{sinirli}", () =>
            {
                var t = new Tuval(128, 128);
                BocekCiz(t, 64, 56, 1f, renk, sinirli);
                return t.Bitir("Bocek");
            });
        }

        private static void BocekCiz(Tuval t, float cx, float cy, float o, Color renk, bool sinirli)
        {
            Vector2 P(float x, float y) => new Vector2(cx + x * o, cy + y * o);
            float k = Mathf.Max(1.6f, 4f * Mathf.Sqrt(o));
            // Bacaklar
            for (int i = -1; i <= 1; i++)
            {
                var a = P(-26, -6 + i * 12); var b = P(-46, -16 + i * 14);
                t.Ciz(Cizgi(a.x, a.y, b.x, b.y, 2.6f * o + 0.4f), Murekkep, 0f);
                a = P(26, -6 + i * 12); b = P(46, -16 + i * 14);
                t.Ciz(Cizgi(a.x, a.y, b.x, b.y, 2.6f * o + 0.4f), Murekkep, 0f);
            }
            // Antenler
            var a0 = P(-10, 26); var a1 = P(-22, 54); var a2 = P(10, 26); var a3 = P(22, 54);
            t.Ciz(Cizgi(a0.x, a0.y, a1.x, a1.y, 2.2f * o + 0.4f), Murekkep, 0f);
            t.Ciz(Cizgi(a2.x, a2.y, a3.x, a3.y, 2.2f * o + 0.4f), Murekkep, 0f);
            t.Ciz(Daire(a1.x, a1.y, 5.5f * o), renk, k * 0.6f);
            t.Ciz(Daire(a3.x, a3.y, 5.5f * o), renk, k * 0.6f);
            // Gövde + karın
            var g = P(0, 0);
            t.Ciz(Elips(g.x, g.y, 36 * o, 32 * o), renk, k);
            var kr = P(0, -10);
            t.Ciz(Elips(kr.x, kr.y, 21 * o, 14 * o), Color.Lerp(renk, Color.white, 0.38f), 0f);
            // Gözler
            var g1 = P(-14, 14); var g2 = P(14, 14);
            t.Ciz(Daire(g1.x, g1.y, 13 * o), Color.white, k * 0.75f);
            t.Ciz(Daire(g2.x, g2.y, 13 * o), Color.white, k * 0.75f);
            var b1 = P(-11, 11); var b2 = P(17, 11);
            t.Ciz(Daire(b1.x, b1.y, 5.8f * o), Murekkep, 0f);
            t.Ciz(Daire(b2.x, b2.y, 5.8f * o), Murekkep, 0f);
            t.Ciz(Daire(b1.x + 2 * o, b1.y + 3 * o, 2f * o), Color.white, 0f);
            t.Ciz(Daire(b2.x + 2 * o, b2.y + 3 * o, 2f * o), Color.white, 0f);
            // Kaşlar (sinirli: içe çatık; normal: endişeli yukarı)
            var k1 = P(-26, sinirli ? 34 : 30); var k2 = P(-6, sinirli ? 27 : 33);
            var k3 = P(6, sinirli ? 27 : 33); var k4 = P(26, sinirli ? 34 : 30);
            t.Ciz(Cizgi(k1.x, k1.y, k2.x, k2.y, 2.8f * o + 0.3f), Murekkep, 0f);
            t.Ciz(Cizgi(k3.x, k3.y, k4.x, k4.y, 2.8f * o + 0.3f), Murekkep, 0f);
            // Ağız + dil
            var m = P(0, -8);
            t.Ciz(Elips(m.x, m.y, 10 * o, (sinirli ? 4 : 6) * o), AgizIci, k * 0.55f);
            if (!sinirli) t.Ciz(Elips(m.x + 2 * o, m.y - 3 * o, 5 * o, 2.6f * o), Pembe, 0f);
        }

        public static Sprite Fare() => Onbellekli("fare", () =>
        {
            var t = new Tuval(128, 128);
            var gri = new Color(0.66f, 0.63f, 0.66f);
            t.Ciz(Daire(34, 86, 19), gri, 4f);
            t.Ciz(Daire(94, 86, 19), gri, 4f);
            t.Ciz(Daire(34, 86, 10), Pembe, 0f);
            t.Ciz(Daire(94, 86, 10), Pembe, 0f);
            t.Ciz(Elips(64, 54, 34, 30), gri, 4f);
            t.Ciz(Elips(64, 42, 16, 11), new Color(0.85f, 0.82f, 0.82f), 0f);
            t.Ciz(Daire(52, 62, 6), Murekkep, 0f);
            t.Ciz(Daire(76, 62, 6), Murekkep, 0f);
            t.Ciz(Daire(54, 64, 2), Color.white, 0f);
            t.Ciz(Daire(78, 64, 2), Color.white, 0f);
            t.Ciz(Daire(64, 46, 5.5f), Pembe, 2.5f);
            for (int i = -1; i <= 1; i += 2)
            {
                t.Ciz(Cizgi(64 + i * 10, 44, 64 + i * 34, 50, 1.3f), Murekkep, 0f);
                t.Ciz(Cizgi(64 + i * 10, 41, 64 + i * 34, 36, 1.3f), Murekkep, 0f);
            }
            t.Ciz(Cizgi(58, 35, 64, 32, 1.6f), Murekkep, 0f);
            t.Ciz(Cizgi(64, 32, 70, 35, 1.6f), Murekkep, 0f);
            return t.Bitir("Fare");
        });

        public static Sprite HamamBocegi() => Onbellekli("hamam", () =>
        {
            var t = new Tuval(128, 128);
            var kahve = new Color(0.56f, 0.32f, 0.18f);
            t.Ciz(Cizgi(56, 100, 30, 124, 1.8f), Murekkep, 0f);
            t.Ciz(Cizgi(72, 100, 98, 124, 1.8f), Murekkep, 0f);
            for (int i = -1; i <= 1; i++)
            {
                t.Ciz(Cizgi(46, 56 + i * 16, 22, 50 + i * 22, 2.4f), Murekkep, 0f);
                t.Ciz(Cizgi(82, 56 + i * 16, 106, 50 + i * 22, 2.4f), Murekkep, 0f);
            }
            t.Ciz(Elips(64, 52, 26, 38), kahve, 4f);
            t.Ciz(Cizgi(64, 84, 64, 18, 1.6f), Murekkep, 0f);
            t.Ciz(Elips(56, 60, 5, 18), new Color(0.75f, 0.5f, 0.32f), 0f);
            t.Ciz(Elips(64, 94, 15, 11), new Color(0.38f, 0.22f, 0.13f), 4f);
            t.Ciz(Daire(58, 96, 4.5f), Color.white, 1.8f);
            t.Ciz(Daire(70, 96, 4.5f), Color.white, 1.8f);
            t.Ciz(Daire(59, 95, 2f), Murekkep, 0f);
            t.Ciz(Daire(71, 95, 2f), Murekkep, 0f);
            return t.Bitir("HamamBocegi");
        });

        /// <summary>Kontrat haşere türü için çizim.</summary>
        public static Sprite Tur(string tur) => tur switch
        {
            "Cockroach" => HamamBocegi(),
            "Mouse" => Fare(),
            "Unknown" => Bocek(new Color(0.55f, 0.38f, 0.66f), true),
            _ => Bocek(BocekYesil)
        };

        // ------------------------------------------------------------------ DUVAR KAĞIDI: "Hiçlik" gecesi

        public struct Manzara
        {
            public string ad;
            public Color ust, ufuk, ay, zemin, isik, goz;
            public float yildiz, bulut;
        }

        public static readonly Manzara[] Manzaralar =
        {
            new Manzara { ad = "Hiçlik Gecesi", ust = new Color(0.09f, 0.05f, 0.13f), ufuk = new Color(0.47f, 0.27f, 0.36f), ay = new Color(0.99f, 0.93f, 0.72f),
                          zemin = new Color(0.13f, 0.08f, 0.1f), isik = new Color(1f, 0.78f, 0.32f), goz = new Color(0.75f, 1f, 0.35f), yildiz = 1f, bulut = 0.7f },
            new Manzara { ad = "Gün Batımı", ust = new Color(0.3f, 0.14f, 0.26f), ufuk = new Color(0.98f, 0.6f, 0.3f), ay = new Color(1f, 0.92f, 0.62f),
                          zemin = new Color(0.22f, 0.11f, 0.12f), isik = new Color(1f, 0.85f, 0.45f), goz = new Color(1f, 0.9f, 0.3f), yildiz = 0.25f, bulut = 0.9f },
            new Manzara { ad = "Kızıl Ay", ust = new Color(0.07f, 0.02f, 0.04f), ufuk = new Color(0.42f, 0.09f, 0.1f), ay = new Color(0.95f, 0.34f, 0.24f),
                          zemin = new Color(0.07f, 0.03f, 0.04f), isik = new Color(1f, 0.55f, 0.25f), goz = new Color(1f, 0.25f, 0.2f), yildiz = 0.6f, bulut = 0.5f },
            new Manzara { ad = "Sisli Sabah", ust = new Color(0.42f, 0.5f, 0.52f), ufuk = new Color(0.86f, 0.8f, 0.64f), ay = new Color(1f, 0.98f, 0.9f),
                          zemin = new Color(0.31f, 0.3f, 0.27f), isik = new Color(1f, 0.9f, 0.6f), goz = new Color(0.9f, 1f, 0.6f), yildiz = 0f, bulut = 1f },
        };

        public const int DuvarGenislik = 960, DuvarYukseklik = 540;

        private static Manzara ManzaraAl(int indeks) => Manzaralar[Mathf.Clamp(indeks, 0, Manzaralar.Length - 1)];

        public static bool DuvarHazirMi(int indeks) => onbellek.TryGetValue("duvar_" + indeks, out var s) && s != null;

        /// <summary>Ayarlar'daki küçük önizleme (hızlı, senkron).</summary>
        public static Sprite DuvarOnizleme(int indeks) => Onbellekli("duvarK_" + indeks, () =>
            PikseldenSprite(DuvarPiksel(ManzaraAl(indeks), 240, 135), 240, 135, "PestOS_DuvarOnizleme"));

        /// <summary>Tam boy duvar kağıdı: pikseller arka plan iş parçacığında hesaplanır (oyun donmaz), bitince sprite verilir.</summary>
        public static IEnumerator DuvarHazirla(int indeks, Action<Sprite> bitti)
        {
            string anahtar = "duvar_" + indeks;
            if (onbellek.TryGetValue(anahtar, out var hazir) && hazir != null) { bitti(hazir); yield break; }
            var m = ManzaraAl(indeks);
            Color[] px;
            if (Application.platform == RuntimePlatform.WebGLPlayer) px = DuvarPiksel(m, DuvarGenislik, DuvarYukseklik);
            else
            {
                var is_ = System.Threading.Tasks.Task.Run(() => DuvarPiksel(m, DuvarGenislik, DuvarYukseklik));
                while (!is_.IsCompleted) yield return null;
                if (is_.IsFaulted) { Debug.LogException(is_.Exception); yield break; }
                px = is_.Result;
            }
            if (onbellek.TryGetValue(anahtar, out hazir) && hazir != null) { bitti(hazir); yield break; } // bu arada başka biri üretti
            var s = PikseldenSprite(px, DuvarGenislik, DuvarYukseklik, "PestOS_Duvar_" + m.ad);
            onbellek[anahtar] = s;
            bitti(s);
        }

        private static Sprite PikseldenSprite(Color[] px, int w, int h, string ad)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = ad };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // GLSL smoothstep (Mathf.SmoothStep farklı çalışır: iki değer arasında ara değer döndürür)
        private static float Yumusak(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // Ufuk çizgisi (dalgalı tepeler) ve silüetlerin oturduğu zemin yüksekliği
        private static float Ufuk(float fx) => 108f + 9f * Mathf.Sin(fx * 0.011f) + 5f * Mathf.Sin(fx * 0.033f + 1.3f) + 2f * Mathf.Sin(fx * 0.09f);
        private static float ZeminY(float fx) => 108f + 9f * Mathf.Sin(fx * 0.011f) + 5f * Mathf.Sin(fx * 0.033f + 1.3f);

        // Saf hesap (Unity API'si yok): arka plan iş parçacığında güvenle çalışır.
        private static Color[] DuvarPiksel(Manzara m, int PW, int PH)
        {
            // Sahne 640x360 birimde tasarlandı; istenen piksel boyutunda örneklenir
            const float W = 640f, H = 360f;
            var px = new Color[PW * PH];
            var ayM = new Vector2(W * 0.74f, H * 0.7f);
            float ayR = H * 0.13f, haleSiniri = ayR * 6f;
            var sil = Color.Lerp(m.zemin, Color.black, 0.45f);
            var bulutRenk = Color.Lerp(m.ust, m.ufuk, 0.55f) * 0.85f;
            var zeminAlt = Color.Lerp(m.zemin, Color.black, 0.35f);
            var zeminUst = Color.Lerp(m.zemin, m.ufuk, 0.25f);
            var yolRenk = Color.Lerp(m.zemin, m.ufuk, 0.35f);
            // Ev ve yel değirmeni (Hiçlik'teki yalnız çiftlik evi havası)
            const float evX = 168f, evG = 30f, evY = 46f;
            const float degX = 262f;
            const float calX = 470f;
            float evZemin = ZeminY(evX) - 3f, degZemin = ZeminY(degX) - 3f, calZemin = ZeminY(calX) - 2f;
            var gobek = new Vector2(degX, degZemin + 100f);
            var kanatlar = new Vector2[4];
            for (int k = 0; k < 4; k++) kanatlar[k] = Dondur(Vector2.up, 20f + k * 90f);
            var pen = new Vector2(evX - 12f, evZemin + 24f);
            float siluetUst = degZemin + 145f;

            var ufukDizi = new float[PW];
            for (int x = 0; x < PW; x++) ufukDizi[x] = Ufuk((x + 0.5f) * W / PW);

            for (int y = 0; y < PH; y++)
                for (int x = 0; x < PW; x++)
                {
                    float fx = (x + 0.5f) * W / PW, fy = (y + 0.5f) * H / PH;
                    float ufukY = ufukDizi[x];
                    Color c;
                    if (fy > ufukY)
                    {
                        // Gökyüzü
                        float t = (fy - ufukY) / (H - ufukY);
                        c = Color.Lerp(m.ufuk, m.ust, Mathf.Pow(t, 0.55f));
                        // Yıldızlar
                        if (m.yildiz > 0f && t > 0.25f)
                        {
                            float hs = Hash(Mathf.Floor(fx / 2f), Mathf.Floor(fy / 2f));
                            if (hs > 0.9965f) c = Color.Lerp(c, new Color(1f, 0.95f, 0.85f), m.yildiz * (hs - 0.9965f) / 0.0035f);
                        }
                        // Ay + hale (yalnız ayın çevresinde hesaplanır)
                        float adx = fx - ayM.x, ady = fy - ayM.y;
                        if (Mathf.Abs(adx) < haleSiniri && Mathf.Abs(ady) < haleSiniri)
                        {
                            float ad = Mathf.Sqrt(adx * adx + ady * ady);
                            if (ad > ayR) c += m.ay * (0.35f * Mathf.Exp(-(ad - ayR) / (ayR * 0.9f)));
                            if (ad < ayR + 1f)
                            {
                                var ayRenk = m.ay;
                                // Kraterler
                                if (Vector2.Distance(new Vector2(fx, fy), ayM + new Vector2(-14, 10)) < 9f) ayRenk *= 0.9f;
                                if (Vector2.Distance(new Vector2(fx, fy), ayM + new Vector2(16, -12)) < 6f) ayRenk *= 0.88f;
                                if (Vector2.Distance(new Vector2(fx, fy), ayM + new Vector2(6, 22)) < 4f) ayRenk *= 0.9f;
                                c = Color.Lerp(c, ayRenk, Mathf.Clamp01(ayR + 0.5f - ad));
                            }
                        }
                        // Ayın önünden geçen uzun bulutlar
                        if (m.bulut > 0f && fy > H * 0.55f && fy < H * 0.8f)
                        {
                            for (int b = 0; b < 3; b++)
                            {
                                float bx = W * (0.62f + b * 0.12f) - b * 40f, by = H * (0.62f + b * 0.06f) + (b == 1 ? -18f : 0f);
                                float bd = new Vector2((fx - bx) / (90f + b * 20f), (fy - by) / 9f).magnitude;
                                if (bd < 1.2f) c = Color.Lerp(c, bulutRenk, m.bulut * 0.85f * Mathf.Clamp01((1.2f - bd) * 3f));
                            }
                        }
                    }
                    else
                    {
                        // Toprak: ufukta açık, aşağı doğru koyulaşır; iz yolu
                        float t = fy / ufukY;
                        c = Color.Lerp(zeminAlt, zeminUst, t * t);
                        float yol = Mathf.Abs(fx - (W * 0.5f + (ufukY - fy) * 1.6f)) - (ufukY - fy) * 0.9f - 3f;
                        if (yol < 0f) c = Color.Lerp(c, yolRenk, 0.35f);
                    }

                    // Silüetler: ev, baca, çatı, yel değirmeni, çalılık (yalnız kendi bölgelerinde test edilir)
                    bool siluet = false, cali = false;
                    if (fy < siluetUst)
                    {
                        if (Mathf.Abs(fx - evX) < evG + 10f)
                        {
                            if (fx > evX - evG && fx < evX + evG && fy > evZemin && fy < evZemin + evY) siluet = true;
                            float catiY = evZemin + evY + (evG + 8f - Mathf.Abs(fx - evX)) * 0.85f;
                            if (Mathf.Abs(fx - evX) < evG + 8f && fy >= evZemin + evY - 1f && fy < catiY) siluet = true;
                            if (fx > evX + 10f && fx < evX + 18f && fy > evZemin + evY && fy < evZemin + evY + 34f) siluet = true;
                        }
                        if (Mathf.Abs(fx - degX) < 45f)
                        {
                            float direkYari = 5f - (fy - degZemin) * 0.035f;
                            if (fy > degZemin && fy < degZemin + 100f && Mathf.Abs(fx - degX) < Mathf.Max(1.5f, direkYari)) siluet = true;
                            var rel = new Vector2(fx, fy) - gobek;
                            if (rel.sqrMagnitude < 42f * 42f)
                            {
                                foreach (var yon in kanatlar)
                                {
                                    float boy = Vector2.Dot(rel, yon), en = Mathf.Abs(rel.x * yon.y - rel.y * yon.x);
                                    if (boy > 0f && boy < 40f && en < 2.2f + boy * 0.12f) siluet = true;
                                }
                                if (rel.sqrMagnitude < 16f) siluet = true;
                            }
                        }
                        if (fx > calX - 40f && fx < calX + 42f && fy < calZemin + 28f)
                        {
                            cali = Vector2.Distance(new Vector2(fx, fy), new Vector2(calX, calZemin + 4f)) < 22f
                                   || Vector2.Distance(new Vector2(fx, fy), new Vector2(calX - 22f, calZemin)) < 15f
                                   || Vector2.Distance(new Vector2(fx, fy), new Vector2(calX + 24f, calZemin - 2f)) < 16f;
                            if (cali && fy > calZemin - 30f) siluet = true;
                        }
                    }
                    if (siluet) c = sil;

                    // Evin yanan penceresi (+ hafif ışık)
                    if (Mathf.Abs(fx - pen.x) < 60f && Mathf.Abs(fy - pen.y) < 60f)
                    {
                        float pd = Mathf.Max(Mathf.Abs(fx - pen.x) - 5f, Mathf.Abs(fy - pen.y) - 6f);
                        if (pd < 0f && Mathf.Abs(fx - pen.x) > 0.8f && Mathf.Abs(fy - pen.y) > 0.8f) c = m.isik;
                        else c += m.isik * 0.25f * Mathf.Exp(-Mathf.Max(0f, pd) / 9f);
                    }

                    // Çalılıkta parlayan böcek gözleri
                    if (cali)
                    {
                        for (int g = 0; g < 2; g++)
                        {
                            var gm = new Vector2(calX - 14f + g * 26f, calZemin + 4f + g * 5f);
                            for (int s = -1; s <= 1; s += 2)
                            {
                                var gp = gm + new Vector2(s * 4.5f, 0f);
                                float gd = new Vector2((fx - gp.x) / 3.2f, (fy - gp.y) / 2.2f).magnitude;
                                if (gd < 1f) c = Color.Lerp(c, m.goz, Mathf.Clamp01((1f - gd) * 3f));
                            }
                        }
                    }

                    // Kağıt greni + vinyet (çizgi film arka planı gibi)
                    float gren = 1f + (Hash(x, y) - 0.5f) * 0.08f;
                    var uv = new Vector2(fx / W - 0.5f, (fy / H - 0.5f) * 0.75f);
                    float vin = 1f - 0.55f * Yumusak(0.3f, 0.8f, uv.magnitude);
                    c = new Color(c.r * gren * vin, c.g * gren * vin, c.b * gren * vin, 1f);
                    px[y * PW + x] = c;
                }
            return px;
        }

        // ------------------------------------------------------------------ YARDIMCI DOKULAR

        /// <summary>CRT tarama çizgileri (tekrarlı, nokta filtreli).</summary>
        public static Texture2D TaramaDokusu()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "PestOS_Tarama" };
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, y == 0 ? 1f : y == 1 ? 0.35f : 0f));
            tex.Apply();
            return tex;
        }

        /// <summary>Kağıt lifi/greni (tekrarlı, beyaz; rengi Image tonu verir).</summary>
        public static Texture2D GrenDokusu()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "PestOS_Gren" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = Hash(x, y);
                    float lif = Mathf.Abs(Mathf.Sin((x + Hash(y, 3f) * 6f) * 0.21f + y * 0.05f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((a - 0.55f) * 1.6f) * 0.7f + (lif > 0.985f ? 0.25f : 0f)));
                }
            tex.Apply();
            return tex;
        }

        /// <summary>Ekran kenarlarını karartan vinyet (eski CRT monitör hissi).</summary>
        public static Sprite Vinyet() => Onbellekli("vinyet", () =>
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "PestOS_Vinyet" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var uv = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f) * 2f;
                    float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(uv.x), 4f) + Mathf.Pow(Mathf.Abs(uv.y), 4f), 0.25f); // yuvarlatılmış kare (tüp ekran)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Yumusak(0.72f, 1.02f, d)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        });
    }
}
