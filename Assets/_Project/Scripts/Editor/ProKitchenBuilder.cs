#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mutfagi stilize ama "uretim kalitesinde" yeniden kurar: pahli kutular, yukseltilmis panelli kapaklar,
// standoff'lu kulplar, kalin yuvarlak kenarli tezgah, gomme evye, retro buzdolabi ve ocak.
// Eski Run_East / Run_North parcalari silinmez; sadece renderer'lari kapatilir (collider'lar yerinde kalir).
// Tekrar calistirilabilir: once kendi kurdugu "Pro_Kitchen" kokunu siler.
public static class ProKitchenBuilder
{
    private const string MeshAssetPath = "Assets/_Project/Models/ProKitchenMeshes.asset";
    private const string MatDir = "Assets/_Project/Materials/Kitchen/";

    private static Dictionary<string, Mesh> cache;
    private static Mesh container;
    private static Transform root;

    // ------------------------------------------------------------------
    // MENU
    // ------------------------------------------------------------------

    [MenuItem("PEST CO/Mutfak/Pro Mutfagi Kur")]
    public static void Build()
    {
        BeginMeshes();
        var kitchen = GameObject.Find("SpaciousFamilyHouse").transform.Find("Kitchen");
        var old = kitchen.Find("Pro_Kitchen");
        if (old) Object.DestroyImmediate(old.gameObject);

        root = new GameObject("Pro_Kitchen").transform;
        root.SetParent(kitchen, false);
        // Ust hiyerarsideki olcegi notrle: mesh'ler dunya metresiyle uretiliyor.
        root.rotation = Quaternion.identity;
        var ps = kitchen.lossyScale;
        root.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
        root.position = Vector3.zero;

        HideOld(kitchen);
        BuildBaseRuns(kitchen);
        BuildUpperRuns(kitchen);
        BuildSink();
        BuildFridge();
        BuildStove(kitchen);
        BuildProps(kitchen);
        BuildMicrowave();
        FixFridgeArt(kitchen);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(kitchen.gameObject.scene);
        Debug.Log($"Pro mutfak kuruldu: {cache.Count} benzersiz mesh, {CountTris(root)} ucgen");
    }

    // ------------------------------------------------------------------
    // MESH URETICILER
    // ------------------------------------------------------------------

    private static void BeginMeshes()
    {
        cache = new Dictionary<string, Mesh>();
        AssetDatabase.DeleteAsset(MeshAssetPath);
        container = new Mesh { name = "ProKitchen_Root" };
        AssetDatabase.CreateAsset(container, MeshAssetPath);
    }

    private static Mesh Store(string key, Mesh m)
    {
        m.name = key;
        m.RecalculateBounds();
        FixWinding(m);
        AssetDatabase.AddObjectToAsset(m, container);
        cache[key] = m;
        return m;
    }

    // Pahli kutu: kenar ve koseler r yaricapla yuvarlanir, normaller yumusak oldugu icin kenarlar isik yakalar.
    public static Mesh RoundedBox(Vector3 size, float r, int seg)
    {
        var h = size * 0.5f;
        r = seg == 0 ? 0f : Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
        string key = $"RB_{size.x:F3}_{size.y:F3}_{size.z:F3}_{r:F4}_{seg}";
        if (cache.TryGetValue(key, out var hit)) return hit;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        var inner = h - Vector3.one * r;

        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3, v = (d + 2) % 3;
            float[] au = Axis(h[u], r, seg), av = Axis(h[v], r, seg);
            foreach (float sg in new[] { -1f, 1f })
            {
                int start = verts.Count;
                for (int j = 0; j < av.Length; j++)
                for (int i = 0; i < au.Length; i++)
                {
                    var p = Vector3.zero;
                    p[d] = sg * h[d]; p[u] = au[i]; p[v] = av[j];
                    var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    var n = p - c;
                    if (n.sqrMagnitude < 1e-12f) { n = Vector3.zero; n[d] = sg; }
                    n.Normalize();
                    verts.Add(c + n * r);
                    norms.Add(n);
                    uvs.Add(new Vector2(p[u], p[v]));
                }
                int w = au.Length;
                for (int j = 0; j < av.Length - 1; j++)
                for (int i = 0; i < w - 1; i++)
                {
                    int a = start + j * w + i, b = a + 1, c2 = a + w, e = c2 + 1;
                    if (sg > 0) { tris.Add(a); tris.Add(b); tris.Add(c2); tris.Add(b); tris.Add(e); tris.Add(c2); }
                    else { tris.Add(a); tris.Add(c2); tris.Add(b); tris.Add(b); tris.Add(c2); tris.Add(e); }
                }
            }
        }

        var m = new Mesh();
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        return Store(key, m);
    }

    private static float[] Axis(float h, float r, int s)
    {
        if (s == 0) return new[] { -h, h };   // pahsiz duz kutu: yuz basina 2 ucgen
        var l = new List<float>();
        for (int i = 0; i <= s; i++) l.Add(-h + r * i / s);
        for (int i = 0; i <= s; i++) l.Add(h - r + r * i / s);
        return l.ToArray();
    }

    // Torna: profil (yaricap, yukseklik) noktalarini Y ekseni etrafinda dondurur.
    // Ayni nokta iki kez yazilirsa o noktada sert kenar olusur.
    public static Mesh Lathe(string key, Vector2[] prof, int sides)
    {
        key = "LT_" + key + "_" + sides;
        if (cache.TryGetValue(key, out var hit)) return hit;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        int last = prof.Length - 1;
        for (int i = 0; i <= last; i++)
        {
            var prev = (i > 0 && prof[i - 1] != prof[i]) ? (prof[i] - prof[i - 1]).normalized : Vector2.zero;
            var next = (i < last && prof[i + 1] != prof[i]) ? (prof[i + 1] - prof[i]).normalized : Vector2.zero;
            if (i < last && prof[i + 1] == prof[i]) next = Vector2.zero;
            if (i > 0 && prof[i - 1] == prof[i]) prev = Vector2.zero;
            var t = prev + next;
            if (t.sqrMagnitude < 1e-8f) t = Vector2.up;
            var n2 = new Vector2(t.y, -t.x).normalized;
            for (int k = 0; k <= sides; k++)
            {
                float a = k * Mathf.PI * 2f / sides;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                verts.Add(new Vector3(prof[i].x * cs, prof[i].y, prof[i].x * sn));
                norms.Add(new Vector3(n2.x * cs, n2.y, n2.x * sn).normalized);
                uvs.Add(new Vector2((float)k / sides, prof[i].y));
            }
        }
        int ring = sides + 1;
        for (int i = 0; i < last; i++)
        {
            if (prof[i] == prof[i + 1]) continue;
            for (int k = 0; k < sides; k++)
            {
                int a = i * ring + k, b = a + 1, c = a + ring, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }
        var m = new Mesh();
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        return Store(key, m);
    }

    // Isaretli hacim negatifse ucgen sirasini cevir: tum yuzler disa baksin.
    private static void FixWinding(Mesh m)
    {
        var v = m.vertices; var t = m.triangles; var c = m.bounds.center;
        double vol = 0;
        for (int i = 0; i < t.Length; i += 3)
            vol += Vector3.Dot(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]), (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f - c);
        if (vol >= 0) return;
        for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
        m.triangles = t;
    }

    // ------------------------------------------------------------------
    // YARDIMCILAR
    // ------------------------------------------------------------------

    private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");

    private static Material ToonMat(string name, string baseHex, string shadowHex, float spec, bool outline)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("PestCo/Toon")); AssetDatabase.CreateAsset(m, path); }
        ColorUtility.TryParseHtmlString(baseHex, out var b);
        ColorUtility.TryParseHtmlString(shadowHex, out var s);
        m.SetColor("_BaseColor", b); m.SetColor("_ShadowColor", s);
        m.SetColor("_OutlineColor", new Color(s.r * 0.4f, s.g * 0.42f, s.b * 0.45f));
        m.SetTexture("_BrushTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/Stylized/T_BrushAtlas_512.png"));
        m.SetFloat("_SpecStrength", spec); m.SetFloat("_SpecSize", 0.93f);
        m.SetColor("_EmissionColor", Color.black);
        m.SetShaderPassEnabled("SRPDefaultUnlit", outline);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static GameObject Part(string name, Mesh mesh, Vector3 worldCenter, Material mat, Transform parent = null, Quaternion? rot = null)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent ? parent : root, false);
        g.transform.localScale = Vector3.one;
        g.transform.position = worldCenter;
        g.transform.rotation = rot ?? Quaternion.identity;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    private static Transform Group(string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root, false);
        return g;
    }

    // min/max koselerinden pahli kutu yerlestir
    private static GameObject Box(string name, Vector3 min, Vector3 max, float r, int seg, Material mat, Transform parent)
        => Part(name, RoundedBox(max - min, r, seg), (min + max) * 0.5f, mat, parent);

    private static int CountTris(Transform t)
    {
        int n = 0;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh) n += mf.sharedMesh.triangles.Length / 3;
        return n;
    }

    private static readonly string[] OldDetailNames =
    {
        "Fridge_Band", "Fridge_Badge", "Fridge_Display", "Fridge_DisplayDot", "Fridge_Vent", "Fridge_Trim",
        "Oven_Panel", "Oven_Knob", "Grate_Z", "Grate_X", "Hood_Light",
        "Pot", "Pot_Lid", "Pot_Knob", "Mug", "Mug_Handle",
        "Can_Toon", "Pot_Toon", "ForkHandle_Toon", "ForkHead_Toon",
        "Toaster_Lever", "Toaster_Knob", "Toaster_Stripe"
    };

    private static void HideOld(Transform kitchen)
    {
        foreach (var run in new[] { "Run_East", "Run_North" })
            foreach (var r in kitchen.Find(run).GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        var names = new HashSet<string>(OldDetailNames);
        foreach (var r in kitchen.GetComponentsInChildren<Renderer>(true))
            if (names.Contains(r.name) && !r.transform.IsChildOf(root)) r.enabled = false;
    }

    // ------------------------------------------------------------------
    // ALT DOLAPLAR + TEZGAH
    // ------------------------------------------------------------------

    private static Material mint, mintHi, mintOut, mintDark, cream, creamOut, chrome, chromeOut, steel, steelDark, glass, lamp, red, redOut, wood;

    private static void LoadMats()
    {
        mint = Mat("K_Cabinet_Mint"); mintOut = Mat("K_Cabinet_Mint_Out");
        cream = Mat("K_White"); creamOut = Mat("K_White_Out");
        chrome = Mat("K_Chrome"); chromeOut = Mat("K_Chrome_Out");
        glass = Mat("K_Glass"); lamp = Mat("K_Lamp");
        red = Mat("K_Red"); redOut = Mat("K_Red_Out"); wood = Mat("K_Wood");
        mintDark = ToonMat("K_Mint_Dark", "#2E8C74", "#1F6654", 0.1f, false);
        steelDark = ToonMat("K_Steel_Dark", "#3A4250", "#252B35", 0.7f, false);
        steel = ToonMat("K_Steel", "#A9B3C1", "#6F7A89", 0.9f, false);
        // Yukseltilmis panel: dolaptan bir ton acik, bevel'in okunmasi icin
        mintHi = ToonMat("K_Cabinet_Mint_Hi", "#78DEC4", "#4DB89D", 0.45f, false);
        chrome.SetFloat("_SpecStrength", 1.1f); chrome.SetFloat("_SpecSize", 0.9f);
        chromeOut.SetFloat("_SpecStrength", 1.1f); chromeOut.SetFloat("_SpecSize", 0.9f);
    }

    private static void BuildBaseRuns(Transform kitchen)
    {
        LoadMats();
        var g = Group("BaseCabinets");

        // Dogu: karkas, supurgelik girintisi
        foreach (var z in new[] { new Vector2(1.414f, 3.206f), new Vector2(3.878f, 7.328f) })
        {
            Box("Carcass_E", new Vector3(42.483f, 0.10f, z.x), new Vector3(43.13f, 0.86f, z.y), 0.006f, 1, mintOut, g);
            Box("ToeKick_E", new Vector3(42.56f, 0.0f, z.x + 0.01f), new Vector3(43.13f, 0.10f, z.y - 0.01f), 0.004f, 1, mintDark, g);
            Counter(g, new Vector3(42.44f, 0.86f, z.x), new Vector3(43.13f, 0.905f, z.y), Vector3.left);
        }
        // Kuzey
        // Evye altinda karkas alcak: hazne tabani gorunsun
        Box("Carcass_N", new Vector3(39.952f, 0.10f, 6.678f), new Vector3(41.07f, 0.86f, 7.33f), 0.006f, 1, mintOut, g);
        Box("Carcass_N", new Vector3(41.07f, 0.10f, 6.678f), new Vector3(41.97f, 0.69f, 7.33f), 0.006f, 1, mintOut, g);
        Box("Carcass_N", new Vector3(41.97f, 0.10f, 6.678f), new Vector3(42.47f, 0.86f, 7.33f), 0.006f, 1, mintOut, g);
        Box("ToeKick_N", new Vector3(39.96f, 0.0f, 6.74f), new Vector3(42.46f, 0.10f, 7.33f), 0.004f, 1, mintDark, g);

        // Kapak ve cekmeceler: eski parcalarin olcu ve konumundan
        foreach (Transform t in kitchen.Find("Run_East"))
            if (IsFront(t.name) && !t.name.StartsWith("EU")) Front(t, Vector3.left, g, t.name.Contains("Drawer"));
        foreach (Transform t in kitchen.Find("Run_North"))
            if (IsFront(t.name) && !t.name.StartsWith("NU")) Front(t, Vector3.back, g, false);
    }

    private static bool IsFront(string n) => n.EndsWith("_Door") || n.EndsWith("_Drawer");

    // Yukseltilmis panelli kapak + standoff'lu krom kulp
    private static void Front(Transform src, Vector3 f, Transform parent, bool drawer)
    {
        var b = src.GetComponent<Renderer>().bounds;
        bool alongX = Mathf.Abs(f.x) > 0.5f;              // on yuz X'e mi bakiyor
        float thick = alongX ? b.size.x : b.size.z;
        float lat = alongX ? b.size.z : b.size.x;
        Vector3 back = b.center - f * thick * 0.5f;       // karkas on duzlemi
        const float T = 0.024f;

        float w = lat - 0.008f, h = b.size.y - 0.008f;
        Vector3 Sz(float lt, float hh, float th) => alongX ? new Vector3(th, hh, lt) : new Vector3(lt, hh, th);

        var slabC = back + f * (T * 0.5f);
        Part(src.name, RoundedBox(Sz(w, h, T), 0.009f, 2), slabC, mint, parent);

        float inset = drawer ? 0.045f : 0.06f;
        if (w > 0.2f && h > 0.15f)
            Part(src.name + "_Panel", RoundedBox(Sz(w - inset * 2, h - inset * 2, 0.012f), 0.0055f, 1), back + f * (T + 0.004f), mintHi, parent);

        // Kulp: eski cekme parcasi varsa onun konumu, yoksa merkez
        Vector3 hc = b.center; bool vertical = !drawer;
        float len = drawer ? Mathf.Min(0.22f, w * 0.35f) : 0.15f;
        var pull = FindPull(src);
        if (pull)
        {
            var pb = pull.GetComponent<Renderer>().bounds;
            hc = pb.center;
            vertical = pb.size.y > (alongX ? pb.size.z : pb.size.x);
            if (drawer) hc.y = b.center.y;
        }
        hc = new Vector3(alongX ? 0 : hc.x, hc.y, alongX ? hc.z : 0);
        if (alongX) hc.x = back.x; else hc.z = back.z;
        Handle(hc + f * (T + 0.012f), f, vertical, len, parent);
    }

    // Kapagin yuzeyi icine dusen eski cekme parcasini konumdan bul
    private static Transform FindPull(Transform door)
    {
        var db = door.GetComponent<Renderer>().bounds;
        foreach (Transform c in door.parent)
        {
            if (!c.name.Contains("Pull")) continue;
            var p = c.GetComponent<Renderer>().bounds.center;
            if (p.y < db.min.y || p.y > db.max.y) continue;
            bool lateralX = db.size.x > db.size.z;
            float lp = lateralX ? p.x : p.z, lmin = lateralX ? db.min.x : db.min.z, lmax = lateralX ? db.max.x : db.max.z;
            if (lp >= lmin && lp <= lmax) return c;
        }
        return null;
    }

    // Bar kulp: kapsul gibi yuvarlak bar + iki ayak
    private static void Handle(Vector3 baseCenter, Vector3 f, bool vertical, float len, Transform parent)
    {
        bool alongX = Mathf.Abs(f.x) > 0.5f;
        const float bar = 0.016f, reach = 0.026f;
        Vector3 axis = vertical ? Vector3.up : (alongX ? Vector3.forward : Vector3.right);
        Vector3 barSize = vertical ? new Vector3(bar, len, bar) : (alongX ? new Vector3(bar, bar, len) : new Vector3(len, bar, bar));
        Part("Handle", RoundedBox(barSize, bar * 0.49f, 1), baseCenter + f * reach, chrome, parent);
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 legSize = alongX ? new Vector3(reach, 0.011f, 0.011f) : new Vector3(0.011f, 0.011f, reach);
            Part("HandleLeg", RoundedBox(legSize, 0f, 0), baseCenter + f * (reach * 0.5f) + axis * s * (len * 0.5f - 0.02f), chrome, parent);
        }
    }

    // Kalin, yuvarlak on kenarli tezgah + krom kenar bandi (50'ler diner)
    private static void Counter(Transform g, Vector3 min, Vector3 max, Vector3 f)
    {
        Box("Counter", min, max, 0.013f, 2, creamOut, g);
        bool alongX = Mathf.Abs(f.x) > 0.5f;
        Vector3 bmin, bmax;
        if (alongX) { bmin = new Vector3(min.x - 0.004f, min.y + 0.009f, min.z); bmax = new Vector3(min.x + 0.003f, max.y - 0.009f, max.z); }
        else { bmin = new Vector3(min.x, min.y + 0.009f, min.z - 0.004f); bmax = new Vector3(max.x, max.y - 0.009f, min.z + 0.003f); }
        Box("CounterBand", bmin, bmax, 0.0035f, 1, chrome, g);
    }

    // ------------------------------------------------------------------
    // UST DOLAPLAR
    // ------------------------------------------------------------------

    private static void BuildUpperRuns(Transform kitchen)
    {
        var g = Group("UpperCabinets");
        foreach (var z in new[] { new Vector2(1.504f, 3.128f), new Vector2(3.968f, 6.656f) })
        {
            Box("UpperCarcass_E", new Vector3(42.763f, 1.50f, z.x), new Vector3(43.13f, 2.20f, z.y), 0.008f, 1, mintOut, g);
            Box("Crown_E", new Vector3(42.725f, 2.20f, z.x - 0.03f), new Vector3(43.13f, 2.245f, z.y + 0.03f), 0.012f, 2, creamOut, g);
            Box("LightRail_E", new Vector3(42.735f, 1.475f, z.x), new Vector3(42.77f, 1.50f, z.y), 0.006f, 1, cream, g);
            Box("UnderLight_E", new Vector3(42.80f, 1.488f, z.x + 0.05f), new Vector3(43.02f, 1.498f, z.y - 0.05f), 0.004f, 1, lamp, g);
        }
        // Kuzey ust dolap
        Box("UpperCarcass_N", new Vector3(39.952f, 1.50f, 6.958f), new Vector3(40.758f, 2.20f, 7.33f), 0.008f, 1, mintOut, g);
        Box("Crown_N", new Vector3(39.952f, 2.20f, 6.92f), new Vector3(40.79f, 2.245f, 7.33f), 0.012f, 2, creamOut, g);
        // Buzdolabi ustu dolap
        Box("FridgeTopCab", new Vector3(42.573f, 1.86f, 0.63f), new Vector3(43.13f, 2.30f, 1.414f), 0.008f, 1, mintOut, g);
        Box("Crown_F", new Vector3(42.54f, 2.30f, 0.60f), new Vector3(43.13f, 2.345f, 1.444f), 0.012f, 2, creamOut, g);

        foreach (Transform t in kitchen.Find("Run_East"))
            if (t.name.StartsWith("EU") && IsFront(t.name)) Front(t, Vector3.left, g, false);
        foreach (Transform t in kitchen.Find("Run_North"))
            if (t.name.StartsWith("NU") && IsFront(t.name)) Front(t, Vector3.back, g, false);

        // Buzdolabi ustu kapak (yukari acilir): alt kenarda yatay kulp
        var back = new Vector3(42.573f, 2.08f, 1.022f);
        Part("FridgeTopDoor", RoundedBox(new Vector3(0.024f, 0.40f, 0.74f), 0.009f, 2), back + Vector3.left * 0.012f, mint, g);
        Part("FridgeTopDoor_Panel", RoundedBox(new Vector3(0.012f, 0.28f, 0.62f), 0.0055f, 1), back + Vector3.left * 0.028f, mintHi, g);
        Handle(new Vector3(42.573f - 0.036f, 1.93f, 1.022f), Vector3.left, false, 0.22f, g);
    }

    // ------------------------------------------------------------------
    // EVYE + BATARYA + KUZEY TEZGAH
    // ------------------------------------------------------------------

    private static void BuildSink()
    {
        var g = Group("Sink");
        float y0 = 0.86f, y1 = 0.905f, front = 6.64f, back = 7.33f;
        float sx0 = 41.08f, sx1 = 41.96f, sz0 = 6.77f, sz1 = 7.21f;
        Box("Counter", new Vector3(39.952f, y0, front), new Vector3(sx0, y1, back), 0.013f, 2, creamOut, g);
        Box("Counter", new Vector3(sx1, y0, front), new Vector3(42.44f, y1, back), 0.013f, 2, creamOut, g);
        Box("Counter", new Vector3(sx0 - 0.01f, y0, front), new Vector3(sx1 + 0.01f, y1, sz0), 0.013f, 2, creamOut, g);
        Box("Counter", new Vector3(sx0 - 0.01f, y0, sz1), new Vector3(sx1 + 0.01f, y1, back), 0.013f, 2, creamOut, g);
        Box("CounterBand", new Vector3(39.952f, y0 + 0.009f, front - 0.004f), new Vector3(42.44f, y1 - 0.009f, front + 0.003f), 0.0035f, 1, chrome, g);

        // Gomme hazne: taban + dort duvar + ust cerceve
        Box("Basin_Bottom", new Vector3(sx0, 0.70f, sz0), new Vector3(sx1, 0.715f, sz1), 0.005f, 1, steel, g);
        Box("Basin_Wall", new Vector3(sx0, 0.70f, sz0), new Vector3(sx0 + 0.012f, y1, sz1), 0.004f, 1, steel, g);
        Box("Basin_Wall", new Vector3(sx1 - 0.012f, 0.70f, sz0), new Vector3(sx1, y1, sz1), 0.004f, 1, steel, g);
        Box("Basin_Wall", new Vector3(sx0, 0.70f, sz0), new Vector3(sx1, y1, sz0 + 0.012f), 0.004f, 1, steel, g);
        Box("Basin_Wall", new Vector3(sx0, 0.70f, sz1 - 0.012f), new Vector3(sx1, y1, sz1), 0.004f, 1, steel, g);
        Box("Basin_Divider", new Vector3(41.515f, 0.70f, sz0), new Vector3(41.525f, 0.87f, sz1), 0.004f, 1, steel, g);
        foreach (var x in new[] { 41.30f, 41.74f })
            Part("Drain", Lathe("drain", new[] { new Vector2(0, 0), new Vector2(0.03f, 0), new Vector2(0.034f, 0.004f), new Vector2(0, 0.004f) }, 16), new Vector3(x, 0.714f, 6.99f), steelDark, g);

        // Batarya: taban, kugu boyun, uc, iki musluk
        var bx = 41.52f; var bz = 7.27f;
        Part("Tap_Base", Lathe("tapbase", new[] { new Vector2(0, 0), new Vector2(0.034f, 0), new Vector2(0.034f, 0.012f), new Vector2(0.022f, 0.03f), new Vector2(0, 0.03f) }, 16), new Vector3(bx, y1, bz), chrome, g);
        Box("Tap_Riser", new Vector3(bx - 0.012f, y1 + 0.02f, bz - 0.012f), new Vector3(bx + 0.012f, 1.20f, bz + 0.012f), 0.0118f, 2, chrome, g);
        Box("Tap_Spout", new Vector3(bx - 0.012f, 1.176f, 7.06f), new Vector3(bx + 0.012f, 1.20f, bz + 0.012f), 0.0118f, 2, chrome, g);
        Box("Tap_Tip", new Vector3(bx - 0.012f, 1.13f, 7.06f), new Vector3(bx + 0.012f, 1.19f, 7.084f), 0.0118f, 2, chrome, g);
        var knob = Lathe("tapknob", new[] { new Vector2(0, 0), new Vector2(0.02f, 0), new Vector2(0.024f, 0.02f), new Vector2(0.03f, 0.04f), new Vector2(0.03f, 0.048f), new Vector2(0, 0.05f) }, 12);
        Part("Tap_Hot", knob, new Vector3(bx - 0.11f, y1, bz), red, g);
        Part("Tap_Cold", knob, new Vector3(bx + 0.11f, y1, bz), chrome, g);
    }

    // ------------------------------------------------------------------
    // RETRO BUZDOLABI
    // ------------------------------------------------------------------

    private static void BuildFridge()
    {
        var g = Group("Fridge");
        Box("Body", new Vector3(42.38f, 0.02f, 0.63f), new Vector3(43.13f, 1.84f, 1.414f), 0.07f, 3, creamOut, g);
        Box("Door_Upper", new Vector3(42.352f, 1.235f, 0.655f), new Vector3(42.39f, 1.80f, 1.389f), 0.035f, 3, cream, g);
        Box("Door_Lower", new Vector3(42.352f, 0.10f, 0.655f), new Vector3(42.39f, 1.205f, 1.389f), 0.035f, 3, cream, g);
        Box("Grille", new Vector3(42.37f, 0.03f, 0.70f), new Vector3(42.40f, 0.085f, 1.344f), 0.012f, 2, steelDark, g);
        Box("Badge", new Vector3(42.344f, 1.73f, 0.95f), new Vector3(42.352f, 1.76f, 1.094f), 0.004f, 2, chrome, g);
        BigHandle(new Vector3(42.352f, 1.45f, 1.30f), 0.30f, g);
        BigHandle(new Vector3(42.352f, 0.95f, 1.30f), 0.36f, g);
        // magnetler
        var mags = new[] { (red, new Vector3(42.345f, 1.55f, 0.78f)), (mint, new Vector3(42.345f, 1.42f, 0.88f)), (lamp, new Vector3(42.345f, 0.70f, 0.80f)) };
        foreach (var mg in mags) Part("Magnet", RoundedBox(new Vector3(0.012f, 0.05f, 0.05f), 0.006f, 1), mg.Item2, mg.Item1, g);
    }

    private static void BigHandle(Vector3 doorFront, float len, Transform g)
    {
        const float reach = 0.04f, bar = 0.026f;
        Part("Handle", RoundedBox(new Vector3(bar, len, bar), bar * 0.49f, 3), doorFront + Vector3.left * reach, chrome, g);
        foreach (float s in new[] { -1f, 1f })
            Part("HandleLeg", RoundedBox(new Vector3(reach, 0.022f, 0.018f), 0.008f, 1), doorFront + Vector3.left * (reach * 0.5f) + Vector3.up * s * (len * 0.5f - 0.03f), chrome, g);
    }

    // ------------------------------------------------------------------
    // RETRO OCAK + DAVLUMBAZ
    // ------------------------------------------------------------------

    private static void BuildStove(Transform kitchen)
    {
        var g = Group("Stove");
        float z0 = 3.215f, z1 = 3.87f, zc = 3.5425f;
        Box("Body", new Vector3(42.47f, 0.0f, z0), new Vector3(43.13f, 0.88f, z1), 0.02f, 2, creamOut, g);
        Box("Cooktop", new Vector3(42.45f, 0.88f, z0 - 0.005f), new Vector3(43.13f, 0.905f, z1 + 0.005f), 0.01f, 2, chromeOut, g);
        Box("Backguard", new Vector3(43.04f, 0.905f, z0), new Vector3(43.13f, 1.10f, z1), 0.02f, 2, creamOut, g);
        Box("BackguardTrim", new Vector3(43.03f, 1.06f, z0 + 0.02f), new Vector3(43.04f, 1.08f, z1 - 0.02f), 0.004f, 1, chrome, g);
        Box("ControlStrip", new Vector3(42.448f, 0.74f, z0 + 0.015f), new Vector3(42.47f, 0.855f, z1 - 0.015f), 0.008f, 2, chrome, g);

        var knob = Lathe("stoveknob", new[] { new Vector2(0, 0), new Vector2(0.024f, 0), new Vector2(0.024f, 0.006f), new Vector2(0.02f, 0.024f), new Vector2(0.008f, 0.03f), new Vector2(0, 0.03f) }, 14);
        foreach (Transform t in kitchen.Find("Run_East"))
            if (t.name == "Knob")
                Part("Knob", knob, new Vector3(42.448f, 0.80f, t.GetComponent<Renderer>().bounds.center.z), steelDark, g, Quaternion.Euler(0, 0, 90));

        // Firin kapagi + cam + kulp
        Box("OvenDoor", new Vector3(42.443f, 0.10f, z0 + 0.03f), new Vector3(42.47f, 0.715f, z1 - 0.03f), 0.015f, 2, cream, g);
        Box("OvenGlass", new Vector3(42.436f, 0.27f, z0 + 0.09f), new Vector3(42.446f, 0.57f, z1 - 0.09f), 0.02f, 2, glass, g);
        Part("OvenHandle", RoundedBox(new Vector3(0.018f, 0.018f, 0.50f), 0.0088f, 2), new Vector3(42.443f - 0.03f, 0.665f, zc), chrome, g);
        foreach (float s in new[] { -1f, 1f })
            Part("HandleLeg", RoundedBox(new Vector3(0.03f, 0.012f, 0.012f), 0f, 0), new Vector3(42.443f - 0.015f, 0.665f, zc + s * 0.22f), chrome, g);
        Box("Kick", new Vector3(42.48f, 0.0f, z0 + 0.02f), new Vector3(42.50f, 0.07f, z1 - 0.02f), 0.006f, 1, steelDark, g);

        // Ocak gozleri + izgara
        var burner = Lathe("burner", new[] { new Vector2(0, 0), new Vector2(0.068f, 0), new Vector2(0.07f, 0.006f), new Vector2(0.05f, 0.012f), new Vector2(0.035f, 0.012f), new Vector2(0.035f, 0.02f), new Vector2(0, 0.022f) }, 20);
        foreach (Transform t in kitchen.Find("Run_East"))
        {
            if (!t.name.StartsWith("Burner")) continue;
            var c = t.GetComponent<Renderer>().bounds.center;
            Part("Burner", burner, new Vector3(c.x, 0.905f, c.z), steelDark, g);
            Part("Grate", RoundedBox(new Vector3(0.17f, 0.012f, 0.012f), 0f, 0), new Vector3(c.x, 0.935f, c.z), steelDark, g);
            Part("Grate", RoundedBox(new Vector3(0.012f, 0.012f, 0.17f), 0f, 0), new Vector3(c.x, 0.935f, c.z), steelDark, g);
        }

        // Davlumbaz
        Box("HoodCanopy", new Vector3(42.60f, 1.55f, 3.17f), new Vector3(43.13f, 1.76f, 3.91f), 0.04f, 3, creamOut, g);
        Box("HoodBand", new Vector3(42.592f, 1.585f, 3.165f), new Vector3(42.61f, 1.61f, 3.915f), 0.006f, 1, chrome, g);
        Box("HoodLight", new Vector3(42.75f, 1.543f, 3.35f), new Vector3(43.0f, 1.551f, 3.735f), 0.003f, 1, lamp, g);
        Box("HoodChimney", new Vector3(42.87f, 1.76f, 3.41f), new Vector3(43.13f, 2.79f, 3.675f), 0.015f, 2, creamOut, g);
        Box("HoodTrim", new Vector3(42.59f, 1.55f, 3.16f), new Vector3(42.62f, 1.57f, 3.92f), 0.008f, 1, mint, g);
    }

    // ------------------------------------------------------------------
    // TEZGAH USTU ESYALAR (torna ile)
    // ------------------------------------------------------------------

    private static void BuildProps(Transform kitchen)
    {
        var g = Group("Props");
        float top = 0.905f;

        // Tencere (ocak uzerinde) + kapak + kulplar
        var potC = new Vector3(42.66f, 0.935f, 3.43f);
        Part("Pot", Lathe("pot", new[] { new Vector2(0, 0), new Vector2(0.088f, 0), new Vector2(0.095f, 0.01f), new Vector2(0.098f, 0.11f), new Vector2(0.104f, 0.118f), new Vector2(0.098f, 0.122f), new Vector2(0.09f, 0.12f), new Vector2(0, 0.12f) }, 16), potC, redOut, g);
        Part("PotLid", Lathe("potlid", new[] { new Vector2(0, 0), new Vector2(0.1f, 0), new Vector2(0.1f, 0.006f), new Vector2(0.07f, 0.03f), new Vector2(0.015f, 0.04f), new Vector2(0.015f, 0.05f), new Vector2(0.024f, 0.058f), new Vector2(0, 0.062f) }, 16), potC + Vector3.up * 0.12f, chrome, g);
        foreach (float s in new[] { -1f, 1f })
            Part("PotHandle", RoundedBox(new Vector3(0.05f, 0.016f, 0.03f), 0.007f, 1), potC + new Vector3(s * 0.115f, 0.1f, 0), chrome, g);

        // Caydanlik (mint), egik ag, kavisli sap
        var kc = new Vector3(42.954f, 0.935f, 3.654f);
        Part("Kettle", Lathe("kettle", new[] { new Vector2(0, 0), new Vector2(0.07f, 0), new Vector2(0.09f, 0.025f), new Vector2(0.095f, 0.06f), new Vector2(0.08f, 0.11f), new Vector2(0.045f, 0.14f), new Vector2(0.04f, 0.145f), new Vector2(0, 0.145f) }, 16), kc, Mat("K_Cabinet_Mint_Out"), g);
        Part("KettleKnob", Lathe("kknob", new[] { new Vector2(0, 0), new Vector2(0.018f, 0), new Vector2(0.02f, 0.015f), new Vector2(0, 0.025f) }, 12), kc + Vector3.up * 0.145f, steelDark, g);
        Part("KettleSpout", RoundedBox(new Vector3(0.12f, 0.024f, 0.024f), 0.0115f, 1), kc + new Vector3(-0.11f, 0.08f, 0), mint, g, Quaternion.Euler(0, 0, -35));
        Part("KettleHandleTop", RoundedBox(new Vector3(0.11f, 0.02f, 0.02f), 0.0095f, 1), kc + new Vector3(0.0f, 0.2f, 0), steelDark, g);
        foreach (float s in new[] { -1f, 1f })
            Part("KettleHandleLeg", RoundedBox(new Vector3(0.018f, 0.07f, 0.018f), 0.0085f, 1), kc + new Vector3(s * 0.05f, 0.165f, 0), steelDark, g, Quaternion.Euler(0, 0, s * 15));

        // Kasik kavanozu + tahta kasik + spatula
        var jc = new Vector3(42.819f, top, 3.016f);
        Part("UtensilCrock", Lathe("crock", new[] { new Vector2(0, 0), new Vector2(0.05f, 0), new Vector2(0.055f, 0.01f), new Vector2(0.052f, 0.14f), new Vector2(0.058f, 0.15f), new Vector2(0.05f, 0.152f), new Vector2(0.046f, 0.14f), new Vector2(0, 0.14f) }, 14), jc, Mat("K_Tile_Light"), g);
        Part("Spoon", RoundedBox(new Vector3(0.016f, 0.26f, 0.016f), 0.0075f, 1), jc + new Vector3(-0.012f, 0.24f, 0.01f), wood, g, Quaternion.Euler(8, 0, 6));
        Part("SpoonHead", Lathe("spoonhead", new[] { new Vector2(0, 0), new Vector2(0.03f, 0.01f), new Vector2(0.032f, 0.04f), new Vector2(0.02f, 0.065f), new Vector2(0, 0.07f) }, 12), jc + new Vector3(-0.03f, 0.355f, 0.02f), wood, g, Quaternion.Euler(8, 0, 6));
        Part("Spatula", RoundedBox(new Vector3(0.014f, 0.26f, 0.014f), 0.0065f, 1), jc + new Vector3(0.015f, 0.235f, -0.01f), red, g, Quaternion.Euler(-6, 0, -8));
        Part("SpatulaHead", RoundedBox(new Vector3(0.012f, 0.08f, 0.06f), 0.005f, 1), jc + new Vector3(0.03f, 0.39f, -0.025f), red, g, Quaternion.Euler(-6, 0, -8));

        // Bicak blogu (egik) + saplar
        var kb = new Vector3(42.90f, top + 0.10f, 2.736f);
        Part("KnifeBlock", RoundedBox(new Vector3(0.12f, 0.20f, 0.16f), 0.018f, 1), kb, wood, g, Quaternion.Euler(0, 0, 18));
        for (int i = 0; i < 3; i++)
            Part("KnifeHandle", RoundedBox(new Vector3(0.022f, 0.09f, 0.03f), 0.01f, 1), kb + new Vector3(-0.045f, 0.13f, -0.045f + i * 0.045f), steelDark, g, Quaternion.Euler(0, 0, 18));

        // Kesme tahtasi (yuvarlak koseli, sapli)
        Part("CuttingBoard", RoundedBox(new Vector3(0.34f, 0.022f, 0.60f), 0.01f, 1), new Vector3(42.84f, top + 0.011f, 1.95f), wood, g);

        // Tost makinesi: yuvarlak govde, krom sirt, iki yuva, kol
        var tc = new Vector3(42.886f, top + 0.085f, 4.416f);
        Part("Toaster", RoundedBox(new Vector3(0.20f, 0.17f, 0.30f), 0.05f, 3), tc, Mat("K_Cloth_Pink_Out"), g);
        Part("ToasterBand", RoundedBox(new Vector3(0.205f, 0.03f, 0.305f), 0.012f, 1), tc + Vector3.down * 0.05f, chrome, g);
        foreach (float s in new[] { -1f, 1f })
            Part("ToasterSlot", RoundedBox(new Vector3(0.022f, 0.012f, 0.20f), 0.006f, 1), tc + new Vector3(s * 0.035f, 0.083f, 0), steelDark, g);
        Part("ToasterLever", RoundedBox(new Vector3(0.03f, 0.018f, 0.04f), 0.008f, 1), tc + new Vector3(-0.115f, 0.03f, 0.08f), steelDark, g);

        // Kahve demligi: cam govde + krom kapak + sap
        var cc = new Vector3(42.909f, top, 5.088f);
        Part("CoffeePot", Lathe("coffee", new[] { new Vector2(0, 0), new Vector2(0.06f, 0), new Vector2(0.075f, 0.04f), new Vector2(0.07f, 0.09f), new Vector2(0.045f, 0.14f), new Vector2(0, 0.14f) }, 14), cc, glass, g);
        Part("CoffeeLid", Lathe("coffeelid", new[] { new Vector2(0, 0), new Vector2(0.05f, 0), new Vector2(0.05f, 0.02f), new Vector2(0.03f, 0.04f), new Vector2(0, 0.045f) }, 16), cc + Vector3.up * 0.135f, chrome, g);
        Part("CoffeeHandle", RoundedBox(new Vector3(0.02f, 0.10f, 0.02f), 0.0095f, 1), cc + new Vector3(0.09f, 0.08f, 0), steelDark, g);

        // Kupalar (kuzey tezgah) + C kulp
        var mug = Lathe("mug", new[] { new Vector2(0, 0), new Vector2(0.036f, 0), new Vector2(0.04f, 0.006f), new Vector2(0.04f, 0.09f), new Vector2(0.035f, 0.09f), new Vector2(0.034f, 0.012f), new Vector2(0, 0.012f) }, 14);
        foreach (var mc in new[] { (cream, new Vector3(40.95f, top, 7.08f)), (red, new Vector3(41.15f, top, 7.10f)) })
        {
            Part("Mug", mug, mc.Item2, mc.Item1, g);
            Part("MugHandle", RoundedBox(new Vector3(0.012f, 0.06f, 0.03f), 0.005f, 1), mc.Item2 + new Vector3(0.05f, 0.045f, 0), mc.Item1, g);
        }

        // Sabunluk
        var sp = new Vector3(41.95f, top, 6.82f);
        Part("Soap", Lathe("soap", new[] { new Vector2(0, 0), new Vector2(0.03f, 0), new Vector2(0.032f, 0.08f), new Vector2(0.015f, 0.1f), new Vector2(0.008f, 0.1f), new Vector2(0.008f, 0.13f), new Vector2(0, 0.13f) }, 16), sp, mint, g);
        Part("SoapNozzle", RoundedBox(new Vector3(0.04f, 0.01f, 0.01f), 0.0045f, 1), sp + new Vector3(-0.02f, 0.13f, 0), chrome, g);

        // Saksi + yapraklar (pencere kosesi)
        var hp = new Vector3(40.165f, top, 6.86f);
        Part("HerbPot", Lathe("herbpot", new[] { new Vector2(0, 0), new Vector2(0.045f, 0), new Vector2(0.055f, 0.07f), new Vector2(0.062f, 0.075f), new Vector2(0.062f, 0.09f), new Vector2(0.05f, 0.09f), new Vector2(0, 0.085f) }, 16), hp, Mat("K_Red"), g);
        var leaf = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Ico80.asset");
        var leafPos = new[] { new Vector3(0, 0.13f, 0), new Vector3(0.03f, 0.16f, 0.02f), new Vector3(-0.03f, 0.15f, -0.01f), new Vector3(0.0f, 0.19f, -0.02f) };
        foreach (var lp in leafPos)
        {
            var l = Part("Leaf", leaf, hp + lp, Mat("K_Tile_Green"), g);
            l.transform.localScale = new Vector3(0.04f, 0.03f, 0.04f);
        }

        // Bulasik rafi: cubuklar + dik tabaklar
        var dr = new Vector3(40.56f, top, 7.05f);
        Part("RackBase", RoundedBox(new Vector3(0.40f, 0.015f, 0.26f), 0.007f, 1), dr + Vector3.up * 0.0075f, chrome, g);
        for (int i = 0; i < 6; i++)
            Part("RackWire", RoundedBox(new Vector3(0.008f, 0.09f, 0.008f), 0f, 0), dr + new Vector3(-0.15f + i * 0.06f, 0.05f, 0.12f), chrome, g);
        var plate = Lathe("plate", new[] { new Vector2(0, 0), new Vector2(0.06f, 0), new Vector2(0.1f, 0.012f), new Vector2(0.11f, 0.018f), new Vector2(0.1f, 0.02f), new Vector2(0.06f, 0.008f), new Vector2(0, 0.008f) }, 16);
        for (int i = 0; i < 3; i++)
            Part("Plate", plate, dr + new Vector3(-0.09f + i * 0.06f, 0.12f, 0.0f), i == 1 ? mint : cream, g, Quaternion.Euler(0, 0, 80));
    }
    // ------------------------------------------------------------------
    // RETRO MIKRODALGA (eski keskin kutunun yerine)
    // ------------------------------------------------------------------

    private static void BuildMicrowave()
    {
        foreach (var n in new[] { "SM_Microwave" })
        {
            var go = GameObject.Find(n);
            if (go) foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
        foreach (var r in root.parent.GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("MW_")) r.enabled = false;

        var g = Group("Microwave");
        float fx = 42.60f;
        Box("Body", new Vector3(fx, 0.905f, 4.92f), new Vector3(43.09f, 1.29f, 5.60f), 0.035f, 2, redOut, g);
        Box("DoorFrame", new Vector3(fx - 0.018f, 0.935f, 4.95f), new Vector3(fx + 0.002f, 1.26f, 5.40f), 0.012f, 2, cream, g);
        Box("Glass", new Vector3(fx - 0.024f, 0.99f, 5.00f), new Vector3(fx - 0.014f, 1.21f, 5.34f), 0.018f, 2, glass, g);
        Box("Panel", new Vector3(fx - 0.014f, 0.935f, 5.42f), new Vector3(fx + 0.002f, 1.26f, 5.575f), 0.01f, 2, chrome, g);
        Box("Display", new Vector3(fx - 0.02f, 1.19f, 5.44f), new Vector3(fx - 0.012f, 1.235f, 5.555f), 0.006f, 1, lamp, g);
        var btn = Lathe("mwbtn", new[] { new Vector2(0, 0), new Vector2(0.016f, 0), new Vector2(0.016f, 0.01f), new Vector2(0.012f, 0.016f), new Vector2(0, 0.016f) }, 12);
        var cols = new[] { red, cream, cream };
        for (int i = 0; i < 3; i++)
            Part("Button", btn, new Vector3(fx - 0.014f, 1.12f - i * 0.06f, 5.4975f), cols[i], g, Quaternion.Euler(0, 0, 90));
        Part("Handle", RoundedBox(new Vector3(0.018f, 0.22f, 0.018f), 0.0088f, 1), new Vector3(fx - 0.05f, 1.097f, 5.375f), chrome, g);
        foreach (float s in new[] { -1f, 1f })
            Part("HandleLeg", RoundedBox(new Vector3(0.032f, 0.011f, 0.011f), 0f, 0), new Vector3(fx - 0.034f, 1.097f + s * 0.09f, 5.375f), chrome, g);
        foreach (var z in new[] { 4.97f, 5.55f }) foreach (var x in new[] { 42.64f, 43.05f })
            Part("Foot", RoundedBox(new Vector3(0.03f, 0.012f, 0.03f), 0.005f, 1), new Vector3(x, 0.911f, z), steelDark, g);
    }

    // Buzdolabi kapaklarindaki cizimleri kulplardan uzak, kapak yuzeyine yasla
    private static void FixFridgeArt(Transform kitchen)
    {
        var spots = new Dictionary<string, Vector3>
        {
            { "SM__Drawing_A_L3", new Vector3(42.347f, 1.52f, 0.86f) },
            { "SM__Drawing_B_L3", new Vector3(42.347f, 0.62f, 0.88f) },
            { "SM__Drawing_C_L3", new Vector3(42.347f, 0.98f, 0.84f) },
        };
        foreach (var t in kitchen.GetComponentsInChildren<Transform>(true))
            if (spots.TryGetValue(t.name, out var p))
            {
                var r = t.GetComponent<Renderer>();
                t.position += p - r.bounds.center;
            }
    }
}
#endif
