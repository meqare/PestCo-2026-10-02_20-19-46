using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyunun çizgi film stiline uyan, kodla çizilen simgeler: kalın siyah mürekkep çizgisi,
/// canlı renk, üstten ışık alan yumuşak gölge ve parlama noktası.
/// Modeli olan eşyalar (sprey, vakum, hamamböceği) için editörde modelden simge üretilir
/// (PEST CO/Envanter Simgelerini Oluştur); bunlar onların yedeği ve modeli olmayanların simgesidir.
/// Ayrıca envanter/HUD panelleri için yuvarlak köşeli 9-dilim sprite'lar üretir.
/// </summary>
public static class CartoonIcons
{
    private const int Size = 128;
    private const float Ink = 5f;

    private static readonly Color InkColor = new Color(0.07f, 0.05f, 0.06f, 1f);
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    #region Simgeler

    /// <summary>ToolData.toolId'ye göre silah simgesi (spray, vacuum, trap, racket).</summary>
    public static Sprite Tool(string toolId)
    {
        switch (toolId)
        {
            case "vacuum": return Get("tool_vacuum", DrawVacuum);
            case "trap": return Get("tool_trap", DrawTrap);
            case "racket": return Get("tool_racket", DrawRacket);
            default: return Get("tool_spray", DrawSpray);
        }
    }

    public static Sprite Food(FoodType food)
    {
        switch (food)
        {
            case FoodType.NuclearSoda: return Get("food_soda", DrawSoda);
            case FoodType.ExplosiveCandy: return Get("food_candy", DrawCandy);
            default: return Get("food_cookie", DrawCookie);
        }
    }

    /// <summary>Verilen renkte çizgi film böceği (modelden simgesi olmayan böcekler için).</summary>
    public static Sprite Bug(Color tint)
    {
        return Get("bug_" + ColorUtility.ToHtmlStringRGB(tint), c => DrawBug(c, tint));
    }

    #endregion

    #region Paneller

    /// <summary>Dolu, yuvarlak köşeli beyaz panel (Image.Type = Sliced ile kullanılır, renk Image'dan verilir).</summary>
    public static Sprite RoundedPanel(int radius = 14)
    {
        string key = "panel_" + radius;
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        int size = radius * 2 + 4;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = RoundBox(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f), new Vector2(size * 0.5f - 1f, size * 0.5f - 1f), radius);
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
        }
        return Cache[key] = MakeSprite(key, pixels, size, radius + 2);
    }

    /// <summary>İçi boş, yuvarlak köşeli çerçeve (seçim halkası için, Sliced).</summary>
    public static Sprite RoundedFrame(int radius = 16, float thickness = 5f)
    {
        string key = $"frame_{radius}_{thickness}";
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        int size = radius * 2 + 4;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = RoundBox(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f), new Vector2(size * 0.5f - 1f, size * 0.5f - 1f), radius);
            float ring = Mathf.Abs(d + thickness * 0.5f) - thickness * 0.5f;
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - ring));
        }
        return Cache[key] = MakeSprite(key, pixels, size, radius + 2);
    }

    #endregion

    #region Çizimler

    private static void DrawBug(Canvas c, Color tint)
    {
        Color dark = Color.Lerp(tint, Color.black, 0.45f);

        // Antenler ve bacaklar gövdenin arkasında kalır.
        c.Shape(p => Capsule(p, new Vector2(56, 96), new Vector2(36, 122), 2.5f), dark, 2.5f);
        c.Shape(p => Capsule(p, new Vector2(72, 96), new Vector2(94, 122), 2.5f), dark, 2.5f);
        for (int i = 0; i < 3; i++)
        {
            float y = 70 - i * 18;
            c.Shape(p => Capsule(p, new Vector2(48, y), new Vector2(20, y - 10 + i * 4), 3f), dark, 2.5f);
            c.Shape(p => Capsule(p, new Vector2(80, y), new Vector2(108, y - 10 + i * 4), 3f), dark, 2.5f);
        }

        c.Shape(p => Ellipse(p, new Vector2(64, 50), new Vector2(30, 40)), tint);
        c.Shape(p => Capsule(p, new Vector2(64, 82), new Vector2(64, 18), 1.2f), dark, 0f);
        c.Shape(p => Circle(p, new Vector2(64, 92), 21), dark);

        // Kocaman çizgi film gözleri.
        c.Shape(p => Circle(p, new Vector2(54, 98), 11), Color.white, 3f, false);
        c.Shape(p => Circle(p, new Vector2(75, 98), 11), Color.white, 3f, false);
        c.Shape(p => Circle(p, new Vector2(57, 96), 4.5f), InkColor, 0f, false);
        c.Shape(p => Circle(p, new Vector2(72, 96), 4.5f), InkColor, 0f, false);

        c.Gloss(new Vector2(52, 66), new Vector2(7, 13));
    }

    private static void DrawSpray(Canvas c)
    {
        Color blue = new Color(0.25f, 0.6f, 1f);
        c.Shape(p => Capsule(p, new Vector2(78, 114), new Vector2(100, 120), 3f), new Color(0.7f, 0.95f, 1f), 2.5f);
        c.Shape(p => RoundBox(p, new Vector2(64, 108), new Vector2(10, 8), 3), new Color(0.95f, 0.25f, 0.2f));
        c.Shape(p => RoundBox(p, new Vector2(64, 96), new Vector2(18, 6), 4), new Color(0.78f, 0.8f, 0.84f));
        c.Shape(p => RoundBox(p, new Vector2(64, 52), new Vector2(28, 42), 12), blue);
        c.Shape(p => RoundBox(p, new Vector2(64, 56), new Vector2(29, 13), 4), new Color(1f, 0.85f, 0.2f));
        c.Shape(p => Circle(p, new Vector2(64, 56), 7), InkColor, 0f, false);
        c.Gloss(new Vector2(48, 60), new Vector2(5, 26));
    }

    private static void DrawVacuum(Canvas c)
    {
        Color orange = new Color(1f, 0.45f, 0.15f);
        c.Shape(p => Capsule(p, new Vector2(70, 40), new Vector2(76, 16), 7f), new Color(0.3f, 0.3f, 0.35f));
        c.Shape(p => Capsule(p, new Vector2(16, 70), new Vector2(60, 70), 7f), new Color(0.2f, 0.2f, 0.26f));
        c.Shape(p => Ellipse(p, new Vector2(18, 70), new Vector2(10, 22)), new Color(1f, 0.85f, 0.15f));
        c.Shape(p => Ellipse(p, new Vector2(78, 64), new Vector2(38, 26)), orange);
        c.Shape(p => Circle(p, new Vector2(86, 92), 15), new Color(0.45f, 0.85f, 0.95f));
        c.Shape(p => RoundBox(p, new Vector2(78, 64), new Vector2(4, 24), 2), new Color(1f, 0.85f, 0.15f), 3f);
        c.Gloss(new Vector2(80, 96), new Vector2(5, 5));
        c.Gloss(new Vector2(64, 76), new Vector2(14, 4));
    }

    private static void DrawTrap(Canvas c)
    {
        Color wood = new Color(0.82f, 0.56f, 0.3f);
        Color metal = new Color(0.8f, 0.82f, 0.88f);
        c.Shape(p => RoundBox(p, new Vector2(64, 40), new Vector2(52, 18), 8), wood);
        c.Shape(p => Capsule(p, new Vector2(24, 52), new Vector2(24, 92), 5f), metal, 4f);
        c.Shape(p => Capsule(p, new Vector2(104, 52), new Vector2(104, 92), 5f), metal, 4f);
        c.Shape(p => Capsule(p, new Vector2(24, 92), new Vector2(104, 92), 5f), metal, 4f);
        c.Shape(p => Triangle(p, new Vector2(46, 52), new Vector2(84, 52), new Vector2(84, 74)), new Color(1f, 0.85f, 0.25f));
        c.Shape(p => Circle(p, new Vector2(74, 59), 3.5f), new Color(0.85f, 0.6f, 0.1f), 0f, false);
        c.Gloss(new Vector2(36, 46), new Vector2(14, 3));
    }

    private static void DrawRacket(Canvas c)
    {
        Color yellow = new Color(1f, 0.85f, 0.15f);
        c.Shape(p => Capsule(p, new Vector2(64, 10), new Vector2(64, 42), 9f), new Color(0.25f, 0.5f, 1f));
        c.Shape(p => Capsule(p, new Vector2(64, 40), new Vector2(64, 54), 5f), new Color(0.6f, 0.62f, 0.7f));

        Vector2 center = new Vector2(64, 88);
        Vector2 radii = new Vector2(36, 34);
        c.Shape(p => Ellipse(p, center, radii), new Color(0.6f, 0.95f, 1f, 0.55f));
        for (int i = -3; i <= 3; i++)
        {
            float o = i * 9f;
            c.Shape(p => Mathf.Max(Capsule(p, new Vector2(64 + o, 40), new Vector2(64 + o, 130), 1.2f), Ellipse(p, center, radii)), yellow, 0f, false);
            c.Shape(p => Mathf.Max(Capsule(p, new Vector2(20, 88 + o), new Vector2(108, 88 + o), 1.2f), Ellipse(p, center, radii)), yellow, 0f, false);
        }
        c.Shape(p => Mathf.Abs(Ellipse(p, center, radii)) - 4f, yellow);
        c.Gloss(new Vector2(46, 104), new Vector2(4, 8));
    }

    private static void DrawSoda(Canvas c)
    {
        Color green = new Color(0.3f, 0.9f, 0.25f);
        c.Shape(p => RoundBox(p, new Vector2(64, 106), new Vector2(22, 7), 5), new Color(0.78f, 0.8f, 0.84f));
        c.Shape(p => RoundBox(p, new Vector2(64, 56), new Vector2(28, 46), 12), green);
        c.Shape(p => RoundBox(p, new Vector2(64, 58), new Vector2(29, 15), 4), new Color(1f, 0.88f, 0.15f));
        // Radyasyon işareti
        Vector2 sym = new Vector2(64, 58);
        c.Shape(p => Circle(p, sym, 4f), InkColor, 0f, false);
        for (int i = 0; i < 3; i++)
        {
            float a = (90f + i * 120f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            c.Shape(p => Mathf.Max(Circle(p, sym, 12f), -Circle(p, sym, 6f), Circle(p, sym + dir * 9f, 6.5f)), InkColor, 0f, false);
        }
        c.Shape(p => Ellipse(p, new Vector2(64, 104), new Vector2(8, 2.5f)), InkColor, 0f, false);
        c.Gloss(new Vector2(48, 70), new Vector2(5, 22));
    }

    private static void DrawCandy(Canvas c)
    {
        Color pink = new Color(1f, 0.55f, 0.75f);
        Color red = new Color(0.95f, 0.2f, 0.25f);
        Vector2 center = new Vector2(64, 64);
        c.Shape(p => Triangle(p, new Vector2(10, 38), new Vector2(10, 90), new Vector2(48, 64)), pink);
        c.Shape(p => Triangle(p, new Vector2(118, 38), new Vector2(118, 90), new Vector2(80, 64)), pink);
        c.Shape(p => Circle(p, center, 32), red);
        for (int i = -2; i <= 2; i++)
        {
            float o = i * 16f;
            c.Shape(p => Mathf.Max(Capsule(p, new Vector2(40 + o, 30), new Vector2(88 + o, 98), 4f), Circle(p, center, 28)), Color.white, 0f, false);
        }
        // Fitil: patlayan şeker
        c.Shape(p => Capsule(p, new Vector2(80, 92), new Vector2(92, 110), 2.5f), new Color(0.35f, 0.25f, 0.2f), 2.5f);
        c.Shape(p => Circle(p, new Vector2(94, 114), 6), new Color(1f, 0.8f, 0.1f), 3f, false);
        c.Gloss(new Vector2(50, 78), new Vector2(7, 5));
    }

    private static void DrawCookie(Canvas c)
    {
        Color dough = new Color(0.82f, 0.55f, 0.28f);
        Color chip = new Color(0.45f, 0.15f, 0.6f);
        Vector2 center = new Vector2(64, 62);
        Vector2 bite = new Vector2(104, 98);
        c.Shape(p => Mathf.Max(Circle(p, center, 46), -Circle(p, bite, 18)), dough);

        Vector2[] chips = { new Vector2(44, 80), new Vector2(70, 84), new Vector2(40, 52), new Vector2(64, 58), new Vector2(88, 60), new Vector2(54, 32), new Vector2(80, 36) };
        for (int i = 0; i < chips.Length; i++)
        {
            Vector2 ch = chips[i];
            c.Shape(p => RoundBox(p, ch, new Vector2(5, 4), 2), chip, 2.5f, false);
        }
        c.Gloss(new Vector2(42, 92), new Vector2(8, 4));
    }

    #endregion

    #region Tuval ve Uzaklık Fonksiyonları

    /// <summary>
    /// Arkadan öne katman katman çizilen küçük tuval. Her şekil bir uzaklık fonksiyonudur
    /// (içi negatif); kenarı yumuşatılır ve dışına kalın siyah çizgi çekilir.
    /// </summary>
    private class Canvas
    {
        public readonly Color[] Pixels = new Color[Size * Size];

        public void Shape(Func<Vector2, float> sdf, Color fill, float outline = Ink, bool shade = true)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = sdf(p);

                float coverage = Mathf.Clamp01(0.5f - (d - outline));
                if (coverage <= 0f) continue;

                Color body = fill;
                if (shade)
                {
                    // Üstten ışık: yukarısı açık, aşağısı koyu; kenara yakın hafif gölge bandı.
                    float light = Mathf.Lerp(0.78f, 1.12f, y / (float)Size);
                    float rim = Mathf.Clamp01(-d / 6f);
                    body = new Color(fill.r * light, fill.g * light, fill.b * light, fill.a);
                    body = Color.Lerp(Color.Lerp(body, Color.black, 0.18f), body, rim);
                }

                float inside = outline > 0f ? Mathf.Clamp01(0.5f - d) : 1f;
                Color col = Color.Lerp(InkColor, body, inside);
                float alpha = coverage * Mathf.Lerp(1f, body.a, inside);

                Blend(y * Size + x, col, alpha);
            }
        }

        /// <summary>Plastik parlama: yarı saydam beyaz elips (çizgisiz).</summary>
        public void Gloss(Vector2 center, Vector2 radii)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Ellipse(new Vector2(x + 0.5f, y + 0.5f), center, radii);
                float a = Mathf.Clamp01(0.5f - d) * 0.6f;
                if (a > 0f) Blend(y * Size + x, Color.white, a);
            }
        }

        private void Blend(int i, Color col, float alpha)
        {
            Color dst = Pixels[i];
            float outA = alpha + dst.a * (1f - alpha);
            if (outA <= 0f) return;

            Color rgb = (col * alpha + dst * dst.a * (1f - alpha)) / outA;
            Pixels[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
        }
    }

    private static Sprite Get(string key, Action<Canvas> draw)
    {
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        var canvas = new Canvas();
        draw(canvas);
        return Cache[key] = MakeSprite(key, canvas.Pixels, Size, 0);
    }

    private static Sprite MakeSprite(string name, Color[] pixels, int size, int border)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    private static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
    {
        Vector2 q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
        return (q.magnitude - 1f) * Mathf.Min(r.x, r.y);
    }

    private static float RoundBox(Vector2 p, Vector2 c, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(radius, radius);
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
        Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
        Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
        Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
        Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
        float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
        Vector2 d = Vector2.Min(Vector2.Min(
            new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
            new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
            new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
        return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
    }

    #endregion
}
