using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Procedural PBR doku uretimi ve gercek olcekli UV'ye sahip pahli kutu mesh'leri.
public partial class FamilyHouseBuilder
{
    const string TexFolder = "Assets/_Project/Textures/House";
    const string TexVersion = "v3";
    static bool ForceTex = false;

    class TexSet { public Texture2D albedo, normal; public Vector2 tile = Vector2.one; }
    struct Surf { public Color c; public float h; public Surf(Color c, float h) { this.c = c; this.h = h; } }

    static Dictionary<string, TexSet> texSets;
    static Dictionary<string, Mesh> meshCache;
    static float bev = 0.008f;   // varsayilan pah (m)
    static bool uvHoriz = false; // true: duvar fayansi icin u yatay, v dikey

    // ---------------- gurultu ----------------
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
    // periyodik deger gurultusu (px,py periyot)
    static float VNoise(float x, float y, int px, int py, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py);
        float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
        float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }
    static float Fbm(float u, float v, int fx, int fy, int oct, int seed)
    {
        float sum = 0f, amp = 0.5f, tot = 0f;
        for (int o = 0; o < oct; o++)
        {
            sum += amp * VNoise(u * fx, v * fy, fx, fy, seed + o * 17);
            tot += amp; fx *= 2; fy *= 2; amp *= 0.5f;
        }
        return sum / tot;
    }
    static float Sm(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
    static Color Lc(Color a, Color b, float t) { return Color.Lerp(a, b, Mathf.Clamp01(t)); }
    static Color Rgb(float r, float g, float b) { return new Color(r, g, b, 1f); }

    // ---------------- doku uretimi ----------------
    static TexSet Gen(string key, int w, int h, Vector2 tile, float nStr, bool normal, Func<float, float, Surf> f)
    {
        string baseName = key + "_" + TexVersion;
        string pa = TexFolder + "/" + baseName + "_D.png";
        string pn = TexFolder + "/" + baseName + "_N.png";
        var set = new TexSet { tile = tile };
        if (!ForceTex && File.Exists(pa))
        {
            set.albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(pa);
            if (normal) set.normal = AssetDatabase.LoadAssetAtPath<Texture2D>(pn);
            if (set.albedo != null) return set;
        }
        var cols = new Color32[w * h];
        var ht = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Surf s = f((x + 0.5f) / w, (y + 0.5f) / h);
                cols[y * w + x] = s.c;
                ht[y * w + x] = s.h;
            }
        var ta = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        ta.SetPixels32(cols);
        set.albedo = SaveTex(pa, ta, false);
        if (normal)
        {
            var nc = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float hl = ht[y * w + Mod(x - 1, w)], hr = ht[y * w + Mod(x + 1, w)];
                    float hd = ht[Mod(y - 1, h) * w + x], hu = ht[Mod(y + 1, h) * w + x];
                    var n = new Vector3((hl - hr) * nStr, (hd - hu) * nStr, 1f).normalized;
                    nc[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            var tn = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tn.SetPixels32(nc);
            set.normal = SaveTex(pn, tn, true);
        }
        return set;
    }

    static Texture2D SaveTex(string path, Texture2D t, bool normal)
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Textures")) AssetDatabase.CreateFolder("Assets/_Project", "Textures");
        if (!AssetDatabase.IsValidFolder(TexFolder)) AssetDatabase.CreateFolder("Assets/_Project/Textures", "House");
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !normal;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 8;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.filterMode = FilterMode.Trilinear;
        ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------- doku katalogu ----------------
    static Surf WoodGrain(float u, float v, int seed, Color a, Color b)
    {
        float warp = Fbm(u, v, 2, 3, 3, seed);
        float streak = Fbm(u, v + warp * 0.04f, 3, 56, 4, seed + 5);
        float ring = Mathf.Sin((v * 9f + warp * 3.5f) * Mathf.PI * 2f) * 0.5f + 0.5f;
        float pores = Fbm(u, v, 24, 160, 2, seed + 9);
        float t = 0.45f * streak + 0.35f * ring + 0.2f * warp;
        Color c = Lc(a, b, t);
        c *= 0.93f + 0.14f * pores;
        c.a = 1f;
        return new Surf(c, 0.5f * streak + 0.3f * (1f - pores) + 0.2f * ring);
    }

    static TexSet GetTex(string key)
    {
        TexSet s;
        if (texSets.TryGetValue(key, out s)) return s;
        switch (key)
        {
            case "oakGrain":
                s = Gen(key, 512, 512, new Vector2(1f, 1f), 6f, true, (u, v) => WoodGrain(u, v, 11, Rgb(0.82f, 0.70f, 0.54f), Rgb(0.66f, 0.52f, 0.38f)));
                break;
            case "oakFloor":
                // 2m x 1m: 5 adet 20cm genisliginde tahta, tahta basina rastgele ton ve uc birlesimi
                s = Gen(key, 1024, 512, new Vector2(2f, 1f), 7f, true, (u, v) =>
                {
                    int idx = Mathf.FloorToInt(v * 5f);
                    float lv = v * 5f - idx;
                    float tone = 0.9f + 0.2f * Hash(idx, 3, 7);
                    float uu = Mathf.Repeat(u + Hash(idx, 9, 3), 1f);
                    var w = WoodGrain(uu, v, 20 + idx, Rgb(0.80f, 0.69f, 0.53f), Rgb(0.62f, 0.49f, 0.36f));
                    float seamV = Mathf.Min(lv, 1f - lv);
                    float ju = Hash(idx, 5, 11);
                    float seamU = Mathf.Abs(Mathf.Repeat(u - ju + 0.5f, 1f) - 0.5f) * 2f; // 2m / 1.0
                    float seam = Mathf.Min(Sm(0.012f, 0f, seamV), 1f) + (seamU < 0.006f ? 1f : 0f);
                    seam = Mathf.Clamp01(seam);
                    Color c = w.c * tone;
                    c = Lc(c, Rgb(0.18f, 0.12f, 0.08f), seam * 0.85f);
                    c.a = 1f;
                    return new Surf(c, w.h * 0.6f - seam * 0.8f);
                });
                break;
            case "plaster":
                s = Gen(key, 512, 512, new Vector2(2f, 2f), 3f, true, (u, v) =>
                {
                    float n1 = Fbm(u, v, 8, 8, 4, 31), n2 = Fbm(u, v, 96, 96, 2, 37);
                    float g = 0.95f + 0.04f * n1 + 0.02f * n2;
                    return new Surf(new Color(g, g, g * 0.99f, 1f), 0.5f * n1 + 0.5f * n2);
                });
                break;
            case "concrete":
                s = Gen(key, 512, 512, new Vector2(2f, 2f), 4f, true, (u, v) =>
                {
                    float n1 = Fbm(u, v, 6, 6, 5, 41), n2 = Fbm(u, v, 80, 80, 2, 43);
                    float g = 0.74f + 0.2f * n1 + 0.08f * n2;
                    return new Surf(new Color(g, g, g * 0.98f, 1f), n1 * 0.6f + n2 * 0.4f);
                });
                break;
            case "floorTile":
                s = Gen(key, 1024, 1024, new Vector2(1.2f, 1.2f), 10f, true, (u, v) =>
                {
                    float fu = u * 2f, fv = v * 2f;
                    int cu = Mathf.FloorToInt(fu), cv = Mathf.FloorToInt(fv);
                    float lu = fu - cu, lv = fv - cv;
                    float d = Mathf.Min(Mathf.Min(lu, 1f - lu), Mathf.Min(lv, 1f - lv));
                    float grout = Sm(0.016f, 0.008f, d);
                    float speck = Fbm(u, v, 64, 64, 3, 51);
                    float cloud = Fbm(u, v, 5, 5, 4, 53);
                    float tone = 0.80f + 0.07f * Hash(cu, cv, 5) + 0.05f * cloud + 0.05f * speck;
                    Color c = new Color(tone, tone, tone * 0.98f, 1f);
                    c = Lc(c, Rgb(0.50f, 0.50f, 0.48f), grout);
                    float edge = Sm(0.0f, 0.03f, d);
                    return new Surf(c, edge * 0.7f + 0.3f * speck - grout * 0.6f);
                });
                break;
            case "stone":
                s = Gen(key, 1024, 1024, new Vector2(1f, 1f), 9f, true, (u, v) =>
                {
                    float fu = u * 2f, fv = v * 2f;
                    int cu = Mathf.FloorToInt(fu), cv = Mathf.FloorToInt(fv);
                    float lu = fu - cu, lv = fv - cv;
                    float d = Mathf.Min(Mathf.Min(lu, 1f - lu), Mathf.Min(lv, 1f - lv));
                    float grout = Sm(0.014f, 0.007f, d);
                    float cloud = Fbm(u, v, 6, 6, 4, 61);
                    float vein = 1f - Mathf.Abs(Fbm(u, v, 4, 4, 4, 63) - 0.5f) * 2f;
                    vein = Mathf.Pow(Mathf.Clamp01(vein), 14f);
                    float speck = Fbm(u, v, 90, 90, 2, 65);
                    float tone = 0.68f + 0.1f * Hash(cu, cv, 9) + 0.12f * cloud;
                    Color c = new Color(tone * 1.02f, tone * 0.97f, tone * 0.9f, 1f);
                    c = Lc(c, Rgb(0.52f, 0.48f, 0.43f), vein * 0.22f);
                    c *= 0.95f + 0.1f * speck;
                    c = Lc(c, Rgb(0.35f, 0.33f, 0.30f), grout);
                    c.a = 1f;
                    return new Surf(c, Sm(0f, 0.03f, d) * 0.6f + 0.4f * speck - vein * 0.15f - grout * 0.5f);
                });
                break;
            case "subway":
                s = Gen(key, 1024, 1024, new Vector2(0.6f, 0.6f), 8f, true, (u, v) =>
                {
                    int row = Mathf.FloorToInt(v * 6f);
                    float lv = v * 6f - row;
                    float uu = u * 2f + (row % 2) * 0.5f;
                    int col = Mathf.FloorToInt(uu);
                    float lu = uu - col;
                    float du = Mathf.Min(lu, 1f - lu) * 0.3f;  // metre
                    float dv = Mathf.Min(lv, 1f - lv) * 0.1f;
                    float d = Mathf.Min(du, dv);
                    float grout = Sm(0.0025f, 0.0015f, d);
                    float tone = 0.93f + 0.03f * Hash(col + row * 7, row, 13) + 0.02f * Fbm(u, v, 8, 8, 2, 71);
                    Color c = new Color(tone, tone, tone * 0.99f, 1f);
                    c = Lc(c, Rgb(0.62f, 0.62f, 0.60f), grout);
                    float bevel = Sm(0.0f, 0.004f, d);
                    c *= 0.9f + 0.1f * bevel;
                    c.a = 1f;
                    return new Surf(c, bevel * 0.8f - grout);
                });
                break;
            case "marble":
                s = Gen(key, 1024, 1024, new Vector2(1f, 1f), 2f, true, (u, v) =>
                {
                    float warp = Fbm(u, v, 3, 3, 4, 81);
                    float v1 = Mathf.Abs(Fbm(u + warp * 0.12f, v, 3, 3, 5, 83) - 0.5f) * 2f;
                    float v2 = Mathf.Abs(Fbm(u, v + warp * 0.1f, 5, 5, 4, 85) - 0.5f) * 2f;
                    float vein1 = Mathf.Pow(Mathf.Clamp01(1f - v1 * 4.5f), 3f);
                    float vein2 = Mathf.Pow(Mathf.Clamp01(1f - v2 * 7f), 4f) * 0.5f;
                    float cloud = Fbm(u, v, 4, 4, 4, 87);
                    float g = 0.94f - 0.05f * cloud;
                    Color c = new Color(g, g, g * 0.985f, 1f);
                    c = Lc(c, Rgb(0.55f, 0.56f, 0.58f), Mathf.Clamp01(vein1 + vein2) * 0.8f);
                    return new Surf(c, cloud * 0.1f);
                });
                break;
            case "fabric":
                s = Gen(key, 256, 256, new Vector2(0.08f, 0.08f), 6f, true, (u, v) =>
                {
                    float fx = u * 16f, fy = v * 16f;
                    int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                    float lx = fx - ix, ly = fy - iy;
                    bool over = ((ix + iy) & 1) == 0;
                    float thread = over ? Mathf.Sin(lx * Mathf.PI) : Mathf.Sin(ly * Mathf.PI);
                    float fuzz = Fbm(u, v, 64, 64, 2, 91);
                    float g = 0.74f + 0.2f * thread + 0.06f * fuzz;
                    return new Surf(new Color(g, g, g, 1f), thread * 0.7f + fuzz * 0.3f);
                });
                break;
            case "rug":
                s = Gen(key, 512, 512, new Vector2(0.5f, 0.5f), 5f, true, (u, v) =>
                {
                    float fib = Fbm(u, v, 96, 96, 3, 101);
                    float dx = Mathf.Abs(Mathf.Repeat(u * 2f, 1f) - 0.5f), dy = Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - 0.5f);
                    float dia = Mathf.Abs(dx + dy - 0.5f);
                    float line = Sm(0.045f, 0.0f, dia);
                    float dia2 = Mathf.Abs(dx + dy - 0.22f);
                    float line2 = Sm(0.03f, 0.0f, dia2) * 0.6f;
                    float g = 0.78f + 0.14f * fib - 0.2f * Mathf.Max(line, line2);
                    return new Surf(new Color(g, g, g, 1f), fib * 0.7f - Mathf.Max(line, line2) * 0.2f);
                });
                break;
            case "brushed":
                s = Gen(key, 256, 256, new Vector2(0.3f, 0.3f), 2f, true, (u, v) =>
                {
                    float st = Fbm(u, v, 2, 128, 3, 111);
                    float g = 0.72f + 0.2f * st;
                    return new Surf(new Color(g, g, g * 1.01f, 1f), st * 0.4f);
                });
                break;
            case "leaf":
                s = Gen(key, 256, 256, Vector2.one, 5f, true, (u, v) =>
                {
                    float n = Fbm(u, v, 6, 6, 3, 121);
                    float rib = Sm(0.03f, 0.0f, Mathf.Abs(u - 0.5f));
                    float side = Sm(0.05f, 0.0f, Mathf.Abs(Mathf.Repeat((u - 0.5f) * 0.0f + v * 7f + Mathf.Abs(u - 0.5f) * 5f, 1f) - 0.5f) * 0.4f);
                    float g = 0.55f + 0.3f * n + 0.25f * rib + 0.08f * side;
                    return new Surf(new Color(g * 0.85f, g, g * 0.78f, 1f), rib * 0.6f + side * 0.2f + n * 0.2f);
                });
                break;
            case "cardboard":
                s = Gen(key, 256, 256, new Vector2(0.3f, 0.3f), 6f, true, (u, v) =>
                {
                    float st = Mathf.Sin(u * 60f * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float n = Fbm(u, v, 24, 24, 3, 131);
                    float g = 0.82f + 0.08f * st + 0.1f * n;
                    return new Surf(new Color(g, g * 0.97f, g * 0.92f, 1f), st * 0.5f + n * 0.3f);
                });
                break;
            default:
                return null;
        }
        texSets[key] = s;
        return s;
    }

    // materyale doku uygula (URP/Lit)
    static void ApplyTex(Material m, string key, float normalScale)
    {
        TexSet t = string.IsNullOrEmpty(key) ? null : GetTex(key);
        if (t == null)
        {
            m.SetTexture("_BaseMap", null);
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.SetTextureScale("_BaseMap", Vector2.one);
            return;
        }
        m.SetTexture("_BaseMap", t.albedo);
        m.SetTextureScale("_BaseMap", new Vector2(1f / t.tile.x, 1f / t.tile.y));
        m.SetTextureOffset("_BaseMap", Vector2.zero);
        if (t.normal != null)
        {
            m.SetTexture("_BumpMap", t.normal);
            m.SetFloat("_BumpScale", normalScale);
            m.EnableKeyword("_NORMALMAP");
        }
    }

    // ---------------- pahli kutu mesh'i (UV = metre cinsinden, uzun kenar boyunca u) ----------------
    static Vector3 PV(int a, float va, int c, float vc, int d, float vd)
    {
        var v = Vector3.zero; v[a] = va; v[c] = vc; v[d] = vd; return v;
    }
    static void Poly(List<Vector3> vs, List<Vector2> uvs, List<Vector3> ns, List<int> tr, Vector3[] pts, Vector3 n, float[] H, bool horiz)
    {
        Vector3 cr = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]);
        if (Vector3.Dot(cr, n) < 0f) Array.Reverse(pts);
        int ax = 0; float ma = Mathf.Abs(n.x);
        if (Mathf.Abs(n.y) > ma) { ax = 1; ma = Mathf.Abs(n.y); }
        if (Mathf.Abs(n.z) > ma) { ax = 2; }
        int p = (ax + 1) % 3, q = (ax + 2) % 3;
        bool swap = H[p] < H[q];
        if (horiz && ax != 1) { p = (ax == 0) ? 2 : 0; q = 1; swap = false; }
        int st = vs.Count;
        var nn = n.normalized;
        foreach (var v in pts)
        {
            vs.Add(v); ns.Add(nn);
            float a = v[p] + H[p], b = v[q] + H[q];
            uvs.Add(swap ? new Vector2(b, a) : new Vector2(a, b));
        }
        for (int i = 1; i < pts.Length - 1; i++) { tr.Add(st); tr.Add(st + i); tr.Add(st + i + 1); }
    }
    static Mesh BoxMesh(float sx, float sy, float sz, float b, bool horiz)
    {
        float mn = Mathf.Min(sx, Mathf.Min(sy, sz));
        if (mn < 0.012f) b = 0f;
        b = Mathf.Min(b, 0.3f * mn);
        string key = Mathf.RoundToInt(sx * 1000) + "_" + Mathf.RoundToInt(sy * 1000) + "_" + Mathf.RoundToInt(sz * 1000) + "_" + Mathf.RoundToInt(b * 1000) + (horiz ? "_h" : "");
        Mesh m;
        if (meshCache.TryGetValue(key, out m)) return m;
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var ns = new List<Vector3>(); var tr = new List<int>();
        var H = new float[] { sx * 0.5f, sy * 0.5f, sz * 0.5f };
        for (int a = 0; a < 3; a++)
            for (int sa = -1; sa <= 1; sa += 2)
            {
                int p = (a + 1) % 3, q = (a + 2) % 3;
                var n = Vector3.zero; n[a] = sa;
                float ap = H[p] - b, aq = H[q] - b;
                Poly(vs, uvs, ns, tr, new[] {
                    PV(a, sa * H[a], p, -ap, q, -aq), PV(a, sa * H[a], p, ap, q, -aq),
                    PV(a, sa * H[a], p, ap, q, aq), PV(a, sa * H[a], p, -ap, q, aq) }, n, H, horiz);
            }
        if (b > 0.0001f)
        {
            for (int a = 0; a < 3; a++)
                for (int c = a + 1; c < 3; c++)
                {
                    int d = 3 - a - c;
                    for (int sa = -1; sa <= 1; sa += 2)
                        for (int sc = -1; sc <= 1; sc += 2)
                        {
                            var n = Vector3.zero; n[a] = sa; n[c] = sc;
                            float hd = H[d] - b;
                            var A0 = PV(a, sa * H[a], c, sc * (H[c] - b), d, -hd);
                            var A1 = PV(a, sa * H[a], c, sc * (H[c] - b), d, hd);
                            var C1 = PV(c, sc * H[c], a, sa * (H[a] - b), d, hd);
                            var C0 = PV(c, sc * H[c], a, sa * (H[a] - b), d, -hd);
                            Poly(vs, uvs, ns, tr, new[] { A0, A1, C1, C0 }, n, H, horiz);
                        }
                }
            for (int sx2 = -1; sx2 <= 1; sx2 += 2)
                for (int sy2 = -1; sy2 <= 1; sy2 += 2)
                    for (int sz2 = -1; sz2 <= 1; sz2 += 2)
                    {
                        var v1 = new Vector3(sx2 * H[0], sy2 * (H[1] - b), sz2 * (H[2] - b));
                        var v2 = new Vector3(sx2 * (H[0] - b), sy2 * H[1], sz2 * (H[2] - b));
                        var v3 = new Vector3(sx2 * (H[0] - b), sy2 * (H[1] - b), sz2 * H[2]);
                        Poly(vs, uvs, ns, tr, new[] { v1, v2, v3 }, new Vector3(sx2, sy2, sz2), H, horiz);
                    }
        }
        m = new Mesh { name = "Box_" + key };
        m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, uvs); m.SetTriangles(tr, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        meshCache[key] = m;
        return m;
    }

    [MenuItem("PestCo/Aile Evi - Dokulari Yeniden Uret")]
    static void RegenerateTextures()
    {
        ForceTex = true;
        texSets = new Dictionary<string, TexSet>();
        foreach (var k in new[] { "oakGrain", "oakFloor", "plaster", "concrete", "floorTile", "stone", "subway", "marble", "fabric", "rug", "brushed", "leaf", "cardboard" })
            GetTex(k);
        ForceTex = false;
        AssetDatabase.SaveAssets();
        Debug.Log("Ev dokulari yeniden uretildi: " + TexFolder);
    }
}
