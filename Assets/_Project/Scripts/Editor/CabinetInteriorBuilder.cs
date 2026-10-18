using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Mutfak dolaplarının içini çizgi film tarzında boyar: tahta arka duvar, kenar karartması (derinlik hissi),
/// raf ve rafın üstünde kavanoz/tencere/tabak gibi eşyalar. Her kapağın arkasına, kapak açılınca görünen
/// bir yüzey (quad) koyar. Tekrar çalıştırılabilir: eski iç yüzeyleri silip yeniden üretir.
/// Menü: PestCo/Dolap Iclerini Yenile
/// </summary>
public static class CabinetInteriorBuilder
{
    private const string Dir = "Assets/_Project/Materials/CabinetInterior";
    private const float OpenAngleDegrees = 90f;
    private const float PixelsPerMeter = 400f;

    private static readonly Color32 Outline = new Color32(26, 18, 15, 255);
    private static readonly Color32[] ItemColors =
    {
        new Color32(51, 178, 166, 255),   // turkuaz
        new Color32(217, 51, 51, 255),    // kırmızı
        new Color32(242, 191, 51, 255),   // hardal
        new Color32(242, 115, 166, 255),  // pembe
        new Color32(240, 240, 232, 255),  // beyaz
    };

    /// <summary>Menüden çalıştırılır; sahnedeki mutfak dolap iç yüzeylerini yeniler.</summary>
    [MenuItem("PestCo/Dolap Iclerini Yenile")]
    public static void Build()
    {
        GameObject kitchen = GameObject.Find("Pro_Kitchen");
        if (kitchen == null) { Debug.LogError("Pro_Kitchen sahnede yok."); return; }

        Directory.CreateDirectory(Dir);
        int built = 0;

        foreach (string groupName in new[] { "BaseCabinets", "UpperCabinets" })
        {
            Transform group = kitchen.transform.Find(groupName);
            if (group == null) continue;

            // Eski iç yüzeyleri sil.
            for (int i = group.childCount - 1; i >= 0; i--)
            {
                Transform child = group.GetChild(i);
                if (child.name.StartsWith("Interior_")) Object.DestroyImmediate(child.gameObject);
            }

            int index = 0;
            foreach (Transform pivot in group)
            {
                CabinetDoor cabinetDoor = pivot.GetComponent<CabinetDoor>();
                if (cabinetDoor == null) continue;

                // Ölçüler kapalı duruşta alınır.
                pivot.localRotation = Quaternion.identity;
                Transform door = FindDoorMesh(pivot);
                if (door == null) continue;

                BuildInterior(group, door, index++);
                SetOpenAngle(cabinetDoor);
                built++;
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[Dolap] {built} dolap içi yenilendi.");
    }

    // Pivotun altındaki kapak mesh'ini (adı _Door ile biten) bulur.
    private static Transform FindDoorMesh(Transform pivot)
    {
        foreach (Transform child in pivot)
            if (child.name.EndsWith("_Door")) return child;
        return null;
    }

    // Kapağın arkasına, boyalı iç yüzeyi (quad) yerleştirir.
    private static void BuildInterior(Transform group, Transform door, int index)
    {
        Bounds bounds = door.GetComponent<Renderer>().bounds;
        bool thinX = bounds.size.x < bounds.size.z;
        Vector3 normal = thinX ? Vector3.left : Vector3.back;
        float width = thinX ? bounds.size.z : bounds.size.x;
        float height = bounds.size.y;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"Interior_{door.name}_{index}";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(group, true);
        quad.transform.position = bounds.center - normal * 0.012f;
        quad.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
        quad.transform.localScale = new Vector3(width * 0.97f, height * 0.96f, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = GetMaterial(width, height, index);
    }

    // Kapak ölçüsüne göre doku üretir (aynı ölçü tekrar kullanılır) ve malzemesini döner.
    private static Material GetMaterial(float width, float height, int seed)
    {
        int w = Mathf.Max(64, Mathf.RoundToInt(width * PixelsPerMeter / 8f) * 8);
        int h = Mathf.Max(64, Mathf.RoundToInt(height * PixelsPerMeter / 8f) * 8);
        string key = $"{w}x{h}";
        string texPath = $"{Dir}/Interior_{key}.png";
        string matPath = $"{Dir}/Interior_{key}.mat";

        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material != null) return material;

        File.WriteAllBytes(texPath, ImageConversion.EncodeToPNG(Paint(w, h, seed)));
        AssetDatabase.ImportAsset(texPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0f);
        // Gölgede kalsa da içi okunaklı olsun: kendi dokusundan hafif ışık verir.
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", texture);
        material.SetColor("_EmissionColor", new Color(0.4f, 0.4f, 0.4f));
        AssetDatabase.CreateAsset(material, matPath);
        return material;
    }

    // ---------- Boyama ----------

    private static Texture2D Paint(int w, int h, int seed)
    {
        var px = new Color32[w * h];
        var rnd = new System.Random(seed * 7919 + w * 31 + h);

        // 1) Tahta arka duvar: sıcak krem + dikey tahta çizgileri.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool seam = x % 64 < 3;
                px[y * w + x] = seam ? new Color32(204, 178, 128, 255) : new Color32(235, 212, 152, 255);
            }

        // 2) Raf (yüksek dolaplarda ortada bir raf).
        int floorY = Mathf.RoundToInt(h * 0.07f);
        bool hasShelf = h > 220;
        int shelfTop = Mathf.RoundToInt(h * 0.52f);
        if (hasShelf)
        {
            // Rafın altına gölge.
            for (int y = shelfTop - 36; y < shelfTop; y++)
                for (int x = 0; x < w; x++)
                {
                    float k = Mathf.InverseLerp(shelfTop - 36, shelfTop, y);
                    px[y * w + x] = Multiply(px[y * w + x], Mathf.Lerp(0.95f, 0.6f, k));
                }
            FillRectOutlined(px, w, h, 0, shelfTop, w, shelfTop + 14, new Color32(196, 150, 92, 255));
        }

        // 3) Eşyalar: alt tabanda ve rafın üstünde.
        PlaceItems(px, w, h, floorY, rnd);
        if (hasShelf) PlaceItems(px, w, h, shelfTop + 14, rnd);

        // 4) Tabanda koyu bir çizgi (zemin).
        FillRect(px, w, h, 0, 0, w, floorY, new Color32(176, 138, 88, 255));
        FillRect(px, w, h, 0, floorY, w, floorY + 3, Outline);

        // 5) Kenarlarda koyulaşan vinyet: derinlik (içeri doğru gidiyormuş) hissi.
        float edge = Mathf.Min(w, h) * 0.30f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                float k = Mathf.SmoothStep(0.30f, 1f, d / edge);
                // Üstte biraz daha koyu (tavan gölgesi).
                float top = Mathf.SmoothStep(0.55f, 1f, 1f - (float)y / h * 0.35f);
                px[y * w + x] = Multiply(px[y * w + x], k * top);
            }

        // 6) Dış çerçeve çizgisi.
        FillRect(px, w, h, 0, 0, w, 3, Outline);
        FillRect(px, w, h, 0, h - 3, w, h, Outline);
        FillRect(px, w, h, 0, 0, 3, h, Outline);
        FillRect(px, w, h, w - 3, 0, w, h, Outline);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Verilen taban çizgisine rastgele eşyalar dizer (kavanoz, tencere, tabak yığını, kutu).
    private static void PlaceItems(Color32[] px, int w, int h, int baseY, System.Random rnd)
    {
        int slots = Mathf.Max(2, w / 120);
        float slotWidth = w / (float)slots;
        for (int i = 0; i < slots; i++)
        {
            if (rnd.NextDouble() < 0.12) continue; // bazı yerler boş kalsın
            int cx = Mathf.RoundToInt((i + 0.5f) * slotWidth + (float)(rnd.NextDouble() - 0.5) * slotWidth * 0.2f);
            Color32 color = ItemColors[rnd.Next(ItemColors.Length)];
            int type = rnd.Next(4);
            int maxH = Mathf.Min(h - baseY - 20, 110);
            if (maxH < 30) continue;

            switch (type)
            {
                case 0: // kavanoz
                    FillRectOutlined(px, w, h, cx - 24, baseY, cx + 24, baseY + Mathf.Min(maxH, 78), color);
                    FillRectOutlined(px, w, h, cx - 27, baseY + Mathf.Min(maxH, 78), cx + 27, baseY + Mathf.Min(maxH, 78) + 12, new Color32(120, 120, 128, 255));
                    break;
                case 1: // tencere
                    FillEllipseOutlined(px, w, h, cx, baseY + 26, 36, 26, color);
                    FillRectOutlined(px, w, h, cx - 40, baseY + 46, cx + 40, baseY + 56, new Color32(90, 90, 96, 255));
                    break;
                case 2: // tabak yığını
                    for (int s = 0; s < 4; s++)
                        FillEllipseOutlined(px, w, h, cx, baseY + 8 + s * 11, 40, 9, s % 2 == 0 ? new Color32(245, 245, 238, 255) : color);
                    break;
                default: // kutu
                    FillRectOutlined(px, w, h, cx - 30, baseY, cx + 30, baseY + Mathf.Min(maxH, 64), color);
                    FillRect(px, w, h, cx - 30, baseY + Mathf.Min(maxH, 64) / 2 - 4, cx + 30, baseY + Mathf.Min(maxH, 64) / 2 + 4, new Color32(255, 255, 255, 200));
                    break;
            }
        }
    }

    private static Color32 Multiply(Color32 c, float k)
    {
        return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);
    }

    private static void FillRect(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color32 c)
    {
        x0 = Mathf.Clamp(x0, 0, w); x1 = Mathf.Clamp(x1, 0, w);
        y0 = Mathf.Clamp(y0, 0, h); y1 = Mathf.Clamp(y1, 0, h);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                px[y * w + x] = c;
    }

    private static void FillRectOutlined(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color32 c)
    {
        FillRect(px, w, h, x0 - 3, y0 - 3, x1 + 3, y1 + 3, Outline);
        FillRect(px, w, h, x0, y0, x1, y1, c);
    }

    private static void FillEllipseOutlined(Color32[] px, int w, int h, int cx, int cy, int rx, int ry, Color32 c)
    {
        FillEllipse(px, w, h, cx, cy, rx + 3, ry + 3, Outline);
        FillEllipse(px, w, h, cx, cy, rx, ry, c);
    }

    private static void FillEllipse(Color32[] px, int w, int h, int cx, int cy, int rx, int ry, Color32 c)
    {
        for (int y = Mathf.Max(0, cy - ry); y < Mathf.Min(h, cy + ry + 1); y++)
            for (int x = Mathf.Max(0, cx - rx); x < Mathf.Min(w, cx + rx + 1); x++)
            {
                float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                if (dx * dx + dy * dy <= 1f) px[y * w + x] = c;
            }
    }

    // Kapağın açılma açısını 90° yapar (işaret korunur: kapak odaya doğru açılır).
    private static void SetOpenAngle(CabinetDoor cabinetDoor)
    {
        var so = new SerializedObject(cabinetDoor);
        SerializedProperty angle = so.FindProperty("_openAngle");
        angle.floatValue = Mathf.Sign(angle.floatValue) * OpenAngleDegrees;
        so.ApplyModifiedProperties();
    }
}
