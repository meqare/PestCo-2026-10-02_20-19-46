using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Ayrintilandirma katmani: silmeler, lambri, fayans kaplama, aksesuarlar, pencere gun isigi, yansima probu, preview rig.
public partial class FamilyHouseBuilder
{
    static Transform gPrev;

    // ---------- editor penceresi yardimcilari ----------
    static Transform FindRoot()
    {
        var r = GameObject.Find(RootName);
        return r != null ? r.transform : null;
    }
    static void ToggleChild(string path)
    {
        var r = FindRoot();
        if (r == null) { Debug.LogWarning("SpaciousFamilyHouse bulunamadi."); return; }
        var t = r.Find(path);
        if (t == null) { Debug.LogWarning(path + " bulunamadi."); return; }
        t.gameObject.SetActive(!t.gameObject.activeSelf);
    }
    static void TogglePreviewCamera()
    {
        var r = FindRoot();
        if (r == null) return;
        var t = r.Find("Preview/HousePreviewCamera");
        if (t == null) return;
        var c = t.GetComponent<Camera>();
        c.enabled = !c.enabled;
    }

    // ---------- kucuk yardimcilar ----------
    static void CurtainFolds(string n, bool ax, float f, float a0, float a1, float k0, float k1, int inS)
    {
        int count = Mathf.Max(3, Mathf.RoundToInt((a1 - a0) / 0.07f));
        float w = (a1 - a0) / count;
        float shift = inS > 0 ? 0.035f : -0.035f;
        for (int i = 0; i < count; i++)
        {
            float s = (i % 2 == 0) ? 0f : shift;
            WA(gDW, n + "_Fold", ax, f, a0 + i * w, a0 + (i + 1) * w + 0.004f, k0 + s, k1 + s, 0.05f, 2.55f, sage);
        }
        WA(gDW, n + "_Hem", ax, f, a0, a1, k0, k1 + shift, 0.04f, 0.07f, sageDark);
    }

    static void ShakerFrame(Transform g, float x0, float y0, float x1, float y1, float z)
    {
        if (x1 - x0 < 0.2f || y1 - y0 < 0.2f) return;
        float bw = 0.045f, z1 = z + 0.014f;
        D(g, "ShakerT", x0 + 0.01f, y1 - bw - 0.01f, z, x1 - 0.01f, y1 - 0.01f, z1, sagePaint);
        D(g, "ShakerB", x0 + 0.01f, y0 + 0.01f, z, x1 - 0.01f, y0 + bw + 0.01f, z1, sagePaint);
        D(g, "ShakerL", x0 + 0.01f, y0 + 0.01f, z, x0 + bw + 0.01f, y1 - 0.01f, z1, sagePaint);
        D(g, "ShakerR", x1 - bw - 0.01f, y0 + 0.01f, z, x1 - 0.01f, y1 - 0.01f, z1, sagePaint);
    }

    static void NightstandAsset(Transform g, float x, float z)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Materials/painted_wooden_nightstand_4k.fbx");
        if (asset == null) { Nightstand(g, x, z, 0f); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.name = "Nightstand_Model";
        go.transform.SetParent(g, false);
        go.transform.localPosition = new Vector3(x, 0f, z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        var want = g.TransformPoint(new Vector3(x, 0f, z));
        go.transform.position += new Vector3(want.x - b.center.x, g.position.y - b.min.y, want.z - b.center.z);
        // ustune lamba
        Vector3 top = g.InverseTransformPoint(new Vector3(want.x, b.max.y, want.z));
        Cy(g, "LampBase", x, top.y, z, 0.06f, 0.18f, brass);
        Cy(g, "LampShade", x, top.y + 0.15f, z, 0.13f, 0.18f, emis);
    }

    static void RugFringe(Transform p, string n, float cx, float cz, float sx, float sz, Material m)
    {
        for (float z = cz - sz / 2 + 0.05f; z < cz + sz / 2 - 0.04f; z += 0.045f)
        {
            D(p, n + "_FringeA", cx - sx / 2 - 0.05f, 0.001f, z, cx - sx / 2 + 0.01f, 0.008f, z + 0.018f, m);
            D(p, n + "_FringeB", cx + sx / 2 - 0.01f, 0.001f, z, cx + sx / 2 + 0.05f, 0.008f, z + 0.018f, m);
        }
    }

    // ---------- lambri / fayans kaplama ----------
    static void WainscotLine(Transform p, bool ax, float wl, float a0, float a1, int inward, float inset, float height, Material panel, float t)
    {
        var gl = new List<Gap>();
        foreach (var g in gaps) if (g.ax == ax && Mathf.Abs(g.f - wl) < 0.01f) gl.Add(g);
        gl.Sort((u, v) => u.c.CompareTo(v.c));
        float cur = a0;
        foreach (var g in gl)
        {
            float s = g.c - g.w / 2 - 0.07f, e = g.c + g.w / 2 + 0.07f;
            if (e < a0 || s > a1) continue;
            if (s > cur) WainSeg(p, ax, wl, cur, s, inward, inset, height, panel, t);
            cur = Mathf.Max(cur, e);
        }
        if (a1 > cur) WainSeg(p, ax, wl, cur, a1, inward, inset, height, panel, t);
    }
    static void WainSeg(Transform p, bool ax, float wl, float s, float e, int inward, float inset, float height, Material panel, float t)
    {
        float face = wl + inward * inset;
        float q0 = Mathf.Min(face, face + inward * t), q1 = Mathf.Max(face, face + inward * t);
        float r0 = Mathf.Min(face, face + inward * (t + 0.012f)), r1 = Mathf.Max(face, face + inward * (t + 0.012f));
        bool tile = (panel == subway);
        uvHoriz = tile;
        if (ax)
        {
            D(p, "Wall_Panel", s, 0.1f, q0, e, height, q1, panel);
            uvHoriz = false;
            D(p, "Wall_Rail", s, height, r0, e, height + 0.03f, r1, trim);
            if (!tile) for (float x = s + 0.35f; x < e - 0.25f; x += 0.6f) D(p, "Wall_Stile", x, 0.15f, r0, x + 0.025f, height - 0.05f, r1, trim);
        }
        else
        {
            D(p, "Wall_Panel", q0, 0.1f, s, q1, height, e, panel);
            uvHoriz = false;
            D(p, "Wall_Rail", r0, height, s, r1, height + 0.03f, e, trim);
            if (!tile) for (float z = s + 0.35f; z < e - 0.25f; z += 0.6f) D(p, "Wall_Stile", r0, 0.15f, z, r1, height - 0.05f, z + 0.025f, trim);
        }
        uvHoriz = false;
    }

    // tavan silmesi
    static void CrownRoom(Room r)
    {
        float iw = r.x0 < 0.01f ? 0.1f : 0.06f;
        float ie = Mathf.Abs(r.x1 - 14f) < 0.01f ? 0.1f : 0.06f;
        float isz = r.z0 < 0.01f ? 0.1f : 0.06f;
        float inn = Mathf.Abs(r.z1 - 12f) < 0.01f ? 0.1f : 0.06f;
        float xa = r.x0 + iw, xb = r.x1 - ie, za = r.z0 + isz, zb = r.z1 - inn;
        // yatay silme profili: iki kademe
        D(gS, "Crown", xa, 2.74f, za, xb, 2.8f, za + 0.05f, trim);
        D(gS, "Crown", xa, 2.70f, za, xb, 2.74f, za + 0.025f, trim);
        D(gS, "Crown", xa, 2.74f, zb - 0.05f, xb, 2.8f, zb, trim);
        D(gS, "Crown", xa, 2.70f, zb - 0.025f, xb, 2.74f, zb, trim);
        D(gS, "Crown", xa, 2.74f, za, xa + 0.05f, 2.8f, zb, trim);
        D(gS, "Crown", xa, 2.70f, za, xa + 0.025f, 2.74f, zb, trim);
        D(gS, "Crown", xb - 0.05f, 2.74f, za, xb, 2.8f, zb, trim);
        D(gS, "Crown", xb - 0.025f, 2.70f, za, xb, 2.74f, zb, trim);
    }

    // duvar paneli (picture-frame molding)
    static void MoldingPanels(Transform p, string n, bool ax, float face, int inward, float a0, float a1, float y0, float y1, int cols, Material fill)
    {
        float gapW = 0.08f;
        float w = (a1 - a0 - gapW * (cols - 1)) / cols;
        for (int i = 0; i < cols; i++)
        {
            float s = a0 + i * (w + gapW), e = s + w;
            float q0 = Mathf.Min(face, face + inward * 0.01f), q1 = Mathf.Max(face, face + inward * 0.01f);
            float r0 = Mathf.Min(face, face + inward * 0.024f), r1 = Mathf.Max(face, face + inward * 0.024f);
            float rw = 0.035f;
            if (ax)
            {
                D(p, n + "_Fill", s, y0, q0, e, y1, q1, fill);
                D(p, n + "_RailT", s, y1 - rw, r0, e, y1, r1, trim);
                D(p, n + "_RailB", s, y0, r0, e, y0 + rw, r1, trim);
                D(p, n + "_RailL", s, y0, r0, s + rw, y1, r1, trim);
                D(p, n + "_RailR", e - rw, y0, r0, e, y1, r1, trim);
            }
            else
            {
                D(p, n + "_Fill", q0, y0, s, q1, y1, e, fill);
                D(p, n + "_RailT", r0, y1 - rw, s, r1, y1, e, trim);
                D(p, n + "_RailB", r0, y0, s, r1, y0 + rw, e, trim);
                D(p, n + "_RailL", r0, y0, s, r1, y1, s + rw, trim);
                D(p, n + "_RailR", r0, y0, e - rw, r1, y1, e, trim);
            }
        }
    }

    static void SpotLight(Transform p, string n, Vector3 pos, Vector3 dir, float angle, float range, float intensity, Color c, bool shadows)
    {
        var g = new GameObject(n);
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.LookRotation(dir);
        var l = g.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = angle;
        l.innerSpotAngle = angle * 0.6f;
        l.range = range;
        l.intensity = intensity;
        l.color = c;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }
    static void WindowLight(Transform p, string n, bool ax, float f, float c, float y, int inS, float intensity, float range)
    {
        Vector3 pos = ax ? new Vector3(c, y, f - inS * 0.45f) : new Vector3(f - inS * 0.45f, y, c);
        Vector3 dir = ax ? new Vector3(0f, -0.38f, inS) : new Vector3(inS, -0.38f, 0f);
        SpotLight(p, n, pos, dir, 95f, range, intensity, new Color(0.85f, 0.92f, 1f), false);
    }

    // ====================== ANA DETAY ASAMASI ======================
    static void BuildDetails()
    {
        bev = 0.008f;
        // ---- yapi: kose profilleri, tavan silmeleri ----
        D(gS, "CornerPost_FL", -0.12f, 0.12f, -0.12f, 0.05f, H, 0.05f, trim);
        D(gS, "CornerPost_FR", 13.95f, 0.12f, -0.12f, 14.12f, H, 0.05f, trim);
        D(gS, "CornerPost_BL", -0.12f, 0.12f, 11.95f, 0.05f, H, 12.12f, trim);
        D(gS, "CornerPost_BR", 13.95f, 0.12f, 11.95f, 14.12f, H, 12.12f, trim);
        D(gS, "WallCap", -0.12f, H, -0.12f, 14.12f, H + 0.04f, 0.0f, trim);
        D(gS, "WallCap", -0.12f, H, 12.0f, 14.12f, H + 0.04f, 12.12f, trim);
        D(gS, "WallCap", -0.12f, H, 0f, 0.0f, H + 0.04f, 12.0f, trim);
        D(gS, "WallCap", 14.0f, H, 0f, 14.12f, H + 0.04f, 12.0f, trim);
        foreach (var r in rooms) CrownRoom(r);

        // ---- lambri / fayans kaplama ----
        // koridor
        WainscotLine(gCor, true, Z1, 0.1f, 13.9f, 1, 0.06f, 0.95f, sagePaint, 0.02f);
        WainscotLine(gCor, true, Z2, 0.1f, 13.9f, -1, 0.06f, 0.95f, sagePaint, 0.02f);
        // banyo
        WainscotLine(gBath, true, 0f, 3.86f, 6.34f, 1, 0.1f, 1.3f, subway, 0.015f);
        WainscotLine(gBath, false, 3.8f, 0.1f, Z1 - 0.06f, 1, 0.06f, 1.3f, subway, 0.015f);
        WainscotLine(gBath, false, 6.4f, 0.1f, Z1 - 0.06f, -1, 0.06f, 1.3f, subway, 0.015f);
        WainscotLine(gBath, true, Z1, 3.86f, 6.34f, -1, 0.06f, 1.3f, subway, 0.015f);
        // dus kabini: tavana kadar fayans
        WainscotLine(gBath, true, 0f, 3.86f, 4.76f, 1, 0.1f, 2.1f, subway, 0.03f);
        WainscotLine(gBath, false, 3.8f, 0.1f, 1.0f, 1, 0.06f, 2.1f, subway, 0.03f);
        // WC
        WainscotLine(gWC, true, 0f, 2.26f, 3.74f, 1, 0.1f, 1.2f, subway, 0.015f);
        WainscotLine(gWC, false, 2.2f, 0.1f, Z1 - 0.06f, 1, 0.06f, 1.2f, subway, 0.015f);
        WainscotLine(gWC, false, 3.8f, 0.1f, Z1 - 0.06f, -1, 0.06f, 1.2f, subway, 0.015f);
        WainscotLine(gWC, true, Z1, 2.26f, 3.74f, -1, 0.06f, 1.2f, subway, 0.015f);

        // ---- salon: dekoratif duvar panelleri ----
        MoldingPanels(gLiv, "Boiserie_S1", true, Z2 + 0.06f, 1, 0.25f, 2.45f, 0.9f, 2.35f, 1, sagePaint);
        MoldingPanels(gLiv, "Boiserie_S2", true, Z2 + 0.06f, 1, 4.55f, 6.75f, 0.9f, 2.35f, 1, sagePaint);
        MoldingPanels(gLiv, "Boiserie_W", false, 0.1f, 1, 10.35f, 11.8f, 0.9f, 2.35f, 1, sagePaint);

        // ---- salon: kitaplik + aksesuarlar ----
        var bc = Grp(gLiv, "Bookcase", 6.85f, 11.9f, 180f);
        B(bc, "SideL", 0f, 0f, 0f, 0.035f, 2.1f, 0.34f, oak);
        B(bc, "SideR", 0.915f, 0f, 0f, 0.95f, 2.1f, 0.34f, oak);
        D(bc, "Back", 0f, 0f, 0f, 0.95f, 2.1f, 0.012f, oakDark);
        D(bc, "Top", -0.01f, 2.1f, 0f, 0.96f, 2.13f, 0.36f, oak);
        D(bc, "Plinth", 0.035f, 0f, 0.02f, 0.915f, 0.08f, 0.32f, oakDark);
        for (int i = 0; i <= 5; i++) D(bc, "Board", 0.035f, 0.08f + i * 0.4f, 0.01f, 0.915f, 0.11f + i * 0.4f, 0.34f, oak);
        var bks = new[] { bookA, bookB, bookC, bookD, cream, terra };
        for (int r = 0; r < 5; r++)
        {
            float x = 0.05f;
            int k = 0;
            while (x < 0.82f)
            {
                float bw = 0.035f + 0.02f * Hash(r, k, 3);
                float bh = 0.22f + 0.1f * Hash(k, r, 5);
                if ((r == 1 && k == 5) || (r == 3 && k == 3)) { x += 0.2f; k++; continue; }
                D(bc, "Book", x, 0.11f + r * 0.4f, 0.06f, x + bw, 0.11f + r * 0.4f + bh, 0.3f, bks[(k + r) % bks.Length]);
                x += bw + 0.004f; k++;
            }
        }
        Cy(bc, "Vase", 0.25f, 0.91f, 0.17f, 0.05f, 0.2f, ceramic);
        D(bc, "Frame", 0.6f, 1.31f, 0.08f, 0.82f, 1.51f, 0.1f, brass);
        Sp(bc, "Globe", 0.7f, 1.7f, 0.17f, 0.2f, 0.2f, 0.2f, kBlue);
        // sehpa ustu: dergiler, tepsi
        D(gLiv, "Magazine1", 3.15f, 0.42f, 8.2f, 3.4f, 0.435f, 8.5f, cream, 0f, 15f, 0f);
        D(gLiv, "Magazine2", 3.17f, 0.435f, 8.22f, 3.4f, 0.45f, 8.5f, bookA, 0f, -10f, 0f);
        // koltuk ustu atki ve sepet
        Cy(gLiv, "Basket", 1.35f, 0f, 10.9f, 0.22f, 0.32f, oakLight);
        D(gLiv, "BasketBlanket", 1.2f, 0.3f, 10.75f, 1.52f, 0.4f, 11.05f, cream, 0f, 10f, 0f);
        // duvar aplikleri
        D(gLiv, "SconceL", 6.86f, 1.7f, 5.95f, 6.94f, 1.95f, 6.15f, emis);
        // halilarda saclar
        RugFringe(gLiv, "Rug_Salon", 3.3f, 8.7f, 3.8f, 3.2f, cream);
        RugFringe(gMas, "Rug_Master", 11.0f, 2.3f, 3.2f, 2.8f, cream);
        RugFringe(gKid, "Rug_Kids", 12.0f, 8.3f, 1.9f, 1.5f, kBlue);
        RugFringe(gCor, "Runner", 7.0f, (Z1 + Z2) / 2, 12.0f, 0.7f, cream);

        // ---- mutfak: aksesuarlar ve aydinlatma seritleri ----
        var e = gKit.Find("Run_East");
        D(e, "CuttingBoard", 6.75f, 0.9f, 0.1f, 7.45f, 0.925f, 0.42f, oakLight);
        D(e, "KnifeBlock", 7.7f, 0.9f, 0.12f, 7.9f, 1.12f, 0.3f, oakDark);
        D(e, "KnifeHandleA", 7.74f, 1.12f, 0.18f, 7.78f, 1.22f, 0.22f, black);
        D(e, "KnifeHandleB", 7.82f, 1.12f, 0.2f, 7.86f, 1.2f, 0.24f, black);
        Cy(e, "UtensilJar", 8.05f, 0.9f, 0.28f, 0.05f, 0.18f, steel);
        D(e, "Spoon", 8.04f, 1.08f, 0.27f, 8.06f, 1.3f, 0.29f, oakDark, 0f, 0f, 8f);
        Cy(e, "Kettle", 8.42f, 0.915f, 0.16f, 0.09f, 0.14f, steel);
        Sp(e, "KettleLid", 8.42f, 1.07f, 0.16f, 0.1f, 0.05f, 0.1f, black);
        D(e, "KettleHandle", 8.5f, 0.97f, 0.14f, 8.53f, 1.1f, 0.18f, black);
        Cy(e, "Pan", 8.62f, 0.915f, 0.42f, 0.11f, 0.05f, black);
        D(e, "PanHandle", 8.62f, 0.94f, 0.5f, 8.64f, 0.96f, 0.72f, black);
        D(e, "Toaster", 9.15f, 0.9f, 0.1f, 9.45f, 1.06f, 0.34f, steel);
        D(e, "ToasterSlot", 9.2f, 1.06f, 0.18f, 9.4f, 1.065f, 0.2f, black);
        D(e, "CoffeeMachine", 9.75f, 0.9f, 0.05f, 10.1f, 1.3f, 0.38f, black);
        D(e, "CoffeeJug", 9.8f, 0.9f, 0.1f, 10.0f, 1.0f, 0.3f, glass);
        D(e, "OvenTowel", 8.3f, 0.35f, 0.6f, 8.55f, 0.72f, 0.64f, towelB);
        D(e, "UnderCabLED_1", 6.75f, 1.47f, 0.04f, 8.1f, 1.495f, 0.28f, emis);
        D(e, "UnderCabLED_2", 8.95f, 1.47f, 0.04f, 11.25f, 1.495f, 0.28f, emis);
        for (int i = 0; i < 4; i++) D(e, "SpiceJar", 6.75f + i * 0.08f, 0.9f, 0.45f, 6.8f + i * 0.08f, 0.99f, 0.5f, (i % 2 == 0) ? terra : mustard);
        D(e, "FridgeMagnet1", 5.98f, 1.5f, 0.69f, 6.04f, 1.56f, 0.7f, kYellow);
        D(e, "FridgeMagnet2", 6.1f, 1.38f, 0.69f, 6.16f, 1.44f, 0.7f, kBlue);
        D(e, "FridgeNote", 6.05f, 1.42f, 0.69f, 6.2f, 1.58f, 0.695f, cream);
        var kn = gKit.Find("Run_North");
        Cy(kn, "SoapPump", 0.46f, 0.9f, 0.45f, 0.03f, 0.12f, sage);
        D(kn, "DishRack", 1.4f, 0.9f, 0.1f, 2.0f, 0.97f, 0.4f, steel);
        Cy(kn, "Plate1", 1.7f, 0.95f, 0.25f, 0.11f, 0.01f, ceramic);
        Cy(kn, "HerbPot", 2.05f, 0.9f, 0.45f, 0.05f, 0.08f, pot);
        Sp(kn, "Herb", 2.05f, 1.02f, 0.45f, 0.12f, 0.1f, 0.12f, plant);

        // ---- ana yatak: tuftlu basluk dugmeleri, ayna ----
        var bed = gMas.Find("Bed_King");
        for (int c = 0; c < 5; c++)
            for (int r = 0; r < 3; r++)
            {
                float bx = -0.72f + c * 0.36f;
                Sp(bed, "Button", bx, 0.62f + r * 0.24f, -0.99f, 0.035f, 0.035f, 0.025f, brass);
            }
        D(gMas, "BedWallLight", 9.1f, 1.55f, 0.95f, 9.2f, 1.6f, 1.25f, emis);
        D(gMas, "BedWallLight", 9.1f, 1.55f, 3.35f, 9.2f, 1.6f, 3.65f, emis);
        Cy(gMas, "FloorBasket", 13.4f, 0f, 4.0f, 0.2f, 0.3f, oakLight);

        // ---- cocuk odasi: kisisel esyalar ----
        Sp(gKid, "Beanbag", 12.95f, 0.26f, 7.0f, 0.7f, 0.52f, 0.7f, kBlue);
        Sp(gKid, "BeanbagTop", 12.95f, 0.5f, 7.0f, 0.4f, 0.2f, 0.4f, kBlue);
        Frame(gKid, "Poster1", 10.75f, 1.3f, Z2 + 0.06f, 0.5f, 0.7f, 0f, kYellow, kPink);
        Frame(gKid, "Poster2", 13.25f, 1.4f, Z2 + 0.06f, 0.5f, 0.7f, 0f, kBlue, sage);
        var desk = gKid.Find("DoubleDesk");
        D(desk, "CorkBoard", 0.2f, 1.14f, 0.0f, 1.8f, 1.44f, 0.025f, cardboard);
        D(desk, "Note1", 0.35f, 1.22f, 0.025f, 0.5f, 1.34f, 0.03f, kYellow);
        D(desk, "Note2", 0.8f, 1.2f, 0.025f, 0.95f, 1.32f, 0.03f, kPink);
        D(desk, "Note3", 1.3f, 1.24f, 0.025f, 1.45f, 1.36f, 0.03f, kBlue);
        D(desk, "TrophyBase", 1.65f, 1.54f, 0.08f, 1.75f, 1.58f, 0.18f, oakDark);
        Cy(desk, "TrophyStem", 1.7f, 1.58f, 0.13f, 0.012f, 0.1f, brass);
        Cy(desk, "TrophyCup", 1.7f, 1.68f, 0.13f, 0.04f, 0.06f, brass);
        D(gKid, "Backpack", 11.2f, 0f, 8.85f, 11.5f, 0.42f, 9.05f, kBlue);
        D(gKid, "BackpackPocket", 11.25f, 0.05f, 9.05f, 11.45f, 0.22f, 9.1f, kYellow);
        D(gKid, "ToyTrain1", 11.6f, 0.02f, 8.55f, 11.8f, 0.1f, 8.65f, kPink);
        D(gKid, "ToyTrain2", 11.85f, 0.02f, 8.55f, 12.05f, 0.1f, 8.65f, kYellow);
        D(gKid, "ToyTrain3", 12.1f, 0.02f, 8.55f, 12.3f, 0.1f, 8.65f, kBlue);
        Cy(gKid, "KidLampBase", 11.98f, 0.5f, 11.65f, 0.05f, 0.15f, kYellow);
        Cy(gKid, "KidLampShade", 11.98f, 0.62f, 11.65f, 0.1f, 0.14f, emis);

        // ---- banyo / WC ----
        D(gBath, "BathShelf", 6.0f, 1.5f, 2.9f, 6.34f, 1.53f, 3.8f, oak);
        D(gBath, "ShelfTowels", 6.05f, 1.53f, 3.0f, 6.3f, 1.68f, 3.35f, towelA);
        D(gBath, "ShelfTowels2", 6.07f, 1.68f, 3.02f, 6.28f, 1.8f, 3.3f, towelB);
        Cy(gBath, "ShelfJar", 6.2f, 1.53f, 3.6f, 0.04f, 0.1f, ceramic);
        D(gBath, "SoapDish", 3.88f, 0.95f, 0.15f, 3.98f, 0.97f, 0.35f, ceramic);
        D(gBath, "ShowerBar", 3.86f, 0.9f, 0.55f, 3.89f, 2.0f, 0.58f, chrome);
        D(gBath, "ShowerHandHolder", 3.89f, 1.5f, 0.54f, 3.95f, 1.6f, 0.62f, chrome);
        D(gWC, "WCShelf", 2.6f, 1.45f, 0.1f, 3.4f, 1.48f, 0.3f, oak);
        Cy(gWC, "WCRollSpare", 2.75f, 1.48f, 0.2f, 0.05f, 0.11f, cream);
        Cy(gWC, "WCBrushHolder", 3.55f, 0f, 0.3f, 0.05f, 0.35f, steel);
        Cy(gWC, "WCBin", 2.5f, 0f, 1.0f, 0.12f, 0.3f, steel);
        Frame(gWC, "WCArt", 3.0f, 1.65f, 0.1f, 0.5f, 0.35f, 0f, sage, mustard);

        // ---- depo / antre ----
        Cy(gSto, "VacuumBody", 0.85f, 0f, 3.65f, 0.15f, 0.3f, steel);
        D(gSto, "VacuumHandle", 0.83f, 0.3f, 3.6f, 0.87f, 1.05f, 3.64f, black, 20f, 0f, 0f);
        D(gSto, "ToolBox", 1.0f, 0f, 0.35f, 1.4f, 0.22f, 0.6f, kYellow);
        Sp(gEnt, "Hat", 8.7f, 1.7f, 1.0f + 0.65f, 0.28f, 0.1f, 0.28f, cream);
        D(gEnt, "KeyTray", 6.62f, 1.0f, 1.55f, 6.82f, 1.02f, 1.9f, brass);
        Cy(gEnt, "PorchPlant", 5.7f, -0.0f, -1.6f, 0.2f, 0.4f, pot);
        Sp(gEnt, "PorchPlantLeaves", 5.7f, 0.65f, -1.6f, 0.5f, 0.45f, 0.5f, plant);

        // ---- isiklar: pencere gun isigi, yansima probu ----
        var wl = new GameObject("WindowLights").transform;
        wl.SetParent(gLight, false);
        WindowLight(wl, "WL_Salon_North", true, 12f, 3.5f, 1.6f, -1, 3.0f, 7f);
        WindowLight(wl, "WL_Salon_West", false, 0f, 9.0f, 1.6f, 1, 2.5f, 6f);

        var ucl = gLight.Find("Light_Mutfak");
                var sal = gLight.Find("Light_Salon");
        if (sal != null) sal.GetComponent<Light>().shadows = LightShadows.None;

        var probe = new GameObject("HouseReflectionProbe");
        probe.transform.SetParent(gLight, false);
        probe.transform.localPosition = new Vector3(7f, 1.4f, 6f);
        var rp = probe.AddComponent<ReflectionProbe>();
        rp.size = new Vector3(14.4f, 3.2f, 12.4f);
        rp.boxProjection = true;
        rp.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        rp.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        rp.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        rp.resolution = 64;
        rp.intensity = 1f;
        try { rp.RenderProbe(); } catch (System.Exception) { }

        // ---- preview rig: kamera + dolgu isiklari (kolayca ac/kapa) ----
        gPrev = new GameObject("Preview").transform;
        gPrev.SetParent(gS.parent, false);
        var camGo = new GameObject("HousePreviewCamera");
        camGo.transform.SetParent(gPrev, false);
        Vector3 cp = new Vector3(21f, 15f, -13f), look = new Vector3(7f, 0.5f, 6f);
        camGo.transform.localPosition = cp;
        camGo.transform.localRotation = Quaternion.LookRotation(look - cp);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 36f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        cam.enabled = false;
        camGo.AddComponent<UniversalAdditionalCameraData>();
        var pl = new GameObject("PreviewLights").transform;
        pl.SetParent(gPrev, false);
        pl.gameObject.SetActive(false);
        foreach (var pt in new[] { new Vector3(3.5f, 6f, 3f), new Vector3(10.5f, 6f, 3f), new Vector3(3.5f, 6f, 9f), new Vector3(10.5f, 6f, 9f) })
            Lamp(pl, "PreviewFill", pt.x, pt.y, pt.z, 11f, 0.9f, new Color(1f, 0.95f, 0.88f));
    }

    // ====================== PERFORMANS ======================
    static void OptimizeHouse(Transform root)
    {
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var sz = r.bounds.size;
            float mx = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            if (mx < 0.6f || (r.sharedMaterial != null && r.sharedMaterial == glass))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r.sharedMaterial == glass) r.receiveShadows = false;
        }
        // statik batching (ayni materyaldeki cok sayida parca birlesir)
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Light>() != null || t.GetComponent<Camera>() != null) continue;
            if (t.name.EndsWith("_Leaf")) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
        // uzaktan oda ici kesme: LODGroup
        foreach (var n in new[] { "LivingRoom", "Kitchen", "MasterBedroom", "KidsBedroom", "Bathroom", "WC", "Storage", "Entrance", "Corridor" })
        {
            var g = root.Find(n);
            if (g == null) continue;
            var rs = g.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) continue;
            var lg = g.gameObject.AddComponent<LODGroup>();
            lg.fadeMode = LODFadeMode.None;
            lg.SetLODs(new[] { new LOD(0.015f, rs) });
            lg.RecalculateBounds();
        }
    }
}
