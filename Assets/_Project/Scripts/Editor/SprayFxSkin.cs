using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Spreyin duman/partikül görünümünü (SprayFX, SprayBubbles, SprayStars) menüden değiştirir.
//  Klasik : pastel puf + baloncuk + yıldız (ilk hâl)
//  Toksik : zehirli yeşil-mor puflar + minik kafatasları + hipnoz spiralleri
public static class SprayFxSkin
{
    private const string Menu = "PEST CO/Sprey Efekti/";
    private const string Dir = "Assets/_Project/Materials/Spray/";
    private const string OldDir = "Assets/_Project/Materials/";

    [MenuItem(Menu + "1 - Klasik (pastel)")] private static void Klasik() => Apply(false);
    [MenuItem(Menu + "2 - Toksik (kafatasi + spiral)")] private static void Toksik() => Apply(true);

    public static bool Apply(bool toxic)
    {
        var anim = Object.FindFirstObjectByType<SprayGunAnimator>(FindObjectsInactive.Include);
        if (anim == null || anim.transform.parent == null) { Debug.LogWarning("PEST CO: Sprey aleti sahnede bulunamadi."); return false; }
        Transform tool = anim.transform.parent;

        Material puff, bubble, star;
        if (toxic)
        {
            puff = MakeMat("M_ToxicPuff", OldDir + "M_SprayPuff.mat", "Assets/_Project/Textures/Spray/T_SprayPuff.png", null);
            bubble = MakeMat("M_ToxicSkull", OldDir + "M_SprayBubble.mat", Dir + "T_Skull.png", MakeSkull);
            star = MakeMat("M_ToxicSpiral", OldDir + "M_SprayStar.mat", Dir + "T_Spiral.png", MakeSpiral);
        }
        else
        {
            puff = AssetDatabase.LoadAssetAtPath<Material>(OldDir + "M_SprayPuff.mat");
            bubble = AssetDatabase.LoadAssetAtPath<Material>(OldDir + "M_SprayBubble.mat");
            star = AssetDatabase.LoadAssetAtPath<Material>(OldDir + "M_SprayStar.mat");
        }

        Setup(tool, "SprayFX", puff, toxic
            ? Grad((0.55f, 1f, 0.15f), (0.7f, 0.3f, 1f), (0.2f, 1f, 0.55f), (1f, 0.45f, 0.9f))
            : Grad((0.50f, 0.91f, 0.75f), (1f, 0.62f, 0.78f), (1f, 0.89f, 0.48f), (0.56f, 0.85f, 1f)));
        Setup(tool, "SprayBubbles", bubble, toxic
            ? Grad((1f, 1f, 1f), (0.75f, 1f, 0.6f), (0.9f, 0.7f, 1f))
            : Grad((1f, 1f, 1f), (0.84f, 0.96f, 1f), (1f, 0.89f, 0.95f)));
        Setup(tool, "SprayStars", star, toxic
            ? Grad((1f, 0.9f, 0.1f), (1f, 0.35f, 0.8f), (0.3f, 1f, 0.9f))
            : Grad((1f, 0.88f, 0.4f), (1f, 1f, 1f), (1f, 0.62f, 0.78f)));

        EditorSceneManager.MarkSceneDirty(tool.gameObject.scene);
        return true;
    }

    private static void Setup(Transform tool, string name, Material mat, Gradient startColor)
    {
        Transform t = tool.Find(name);
        if (t == null) return;
        var ps = t.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(startColor);
        var r = t.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
    }

    // Verilen renklerden eşit aralıklı, tam opak bir gradient yapar.
    private static Gradient Grad(params (float r, float g, float b)[] cols)
    {
        var g = new Gradient();
        var keys = new GradientColorKey[cols.Length];
        for (int i = 0; i < cols.Length; i++)
            keys[i] = new GradientColorKey(new Color(cols[i].r, cols[i].g, cols[i].b), (i + 1f) / cols.Length);
        g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    // Eski malzemeden kopya malzeme üretir; gerekirse dokuyu üretir ve atar.
    private static Material MakeMat(string name, string srcMat, string texPath, System.Func<Texture2D> generator)
    {
        string path = Dir + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(AssetDatabase.LoadAssetAtPath<Material>(srcMat));
            AssetDatabase.CreateAsset(mat, path);
        }
        Texture tex;
        if (generator != null)
        {
            if (!File.Exists(texPath)) WritePng(generator(), texPath);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }
        else tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) ?? mat.mainTexture;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void WritePng(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
    }

    // ---- Doku üretimi: beyaz dolgu + koyu kontur, kenarlar yumuşak ----
    private const int Size = 128;
    private static readonly Color Ink = new Color(0.1f, 0.05f, 0.14f, 1f);

    private static Texture2D Paint(System.Func<Vector2, (float fill, float outline)> f)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var (fill, outline) = f(new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size));
                Color c = Color.Lerp(Color.white, Ink, Mathf.Clamp01(outline));
                c.a = Mathf.Clamp01(Mathf.Max(fill, outline));
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }

    private static float Soft(float d) => Mathf.Clamp01(0.5f - d * Size * 0.5f); // d<0 içeride

    private static float SdCircle(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;

    private static float SdBox(Vector2 p, Vector2 c, Vector2 half, float round)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * round;
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
    }

    private static Texture2D MakeSkull()
    {
        return Paint(p =>
        {
            float head = Mathf.Min(SdCircle(p, new Vector2(0.5f, 0.6f), 0.33f), SdBox(p, new Vector2(0.5f, 0.28f), new Vector2(0.19f, 0.13f), 0.05f));
            float eyeL = SdCircle(p, new Vector2(0.38f, 0.58f), 0.085f);
            float eyeR = SdCircle(p, new Vector2(0.62f, 0.58f), 0.085f);
            float nose = SdBox(p, new Vector2(0.5f, 0.43f), new Vector2(0.025f, 0.04f), 0.01f);
            float teeth = Mathf.Min(SdBox(p, new Vector2(0.43f, 0.2f), new Vector2(0.008f, 0.07f), 0f), SdBox(p, new Vector2(0.57f, 0.2f), new Vector2(0.008f, 0.07f), 0f));
            teeth = Mathf.Min(teeth, SdBox(p, new Vector2(0.5f, 0.2f), new Vector2(0.008f, 0.07f), 0f));
            float holes = Mathf.Min(Mathf.Min(eyeL, eyeR), nose);
            // Dış kontur: kafatasının dışına 0.035 kalınlık
            float ring = Soft(head - 0.035f) * (1f - Soft(head));
            float inkHoles = Soft(holes) + Soft(teeth - 0.004f) * 0.9f;
            float inside = Soft(head);
            float o = Mathf.Clamp01(ring + inkHoles * inside);
            return (inside, o);
        });
    }

    private static Texture2D MakeSpiral()
    {
        const int N = 500;
        var pts = new Vector2[N];
        for (int i = 0; i < N; i++)
        {
            float t = i / (float)(N - 1);
            float ang = t * Mathf.PI * 2f * 2.6f;
            float r = 0.04f + t * 0.4f;
            pts[i] = new Vector2(0.5f + Mathf.Cos(ang) * r, 0.5f + Mathf.Sin(ang) * r);
        }
        return Paint(p =>
        {
            float d = float.MaxValue;
            for (int i = 0; i < N; i++) d = Mathf.Min(d, Vector2.Distance(p, pts[i]));
            float stroke = d - 0.035f;              // çizgi kalınlığı
            float fill = Soft(stroke);
            float ring = Soft(stroke - 0.03f) * (1f - Soft(stroke));
            return (fill, Mathf.Clamp01(ring));
        });
    }
}
