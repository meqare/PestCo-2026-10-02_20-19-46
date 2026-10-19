using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Poppy Playtime görünümü: canlı oyuncak renkleri, parlak plastik yüzeyler, güçlü kontrast, kontursuz temiz görüntü.
// Uygula: materyal ve sahne değerlerini Settings/PoppyBackup.json'a yedekler. Geri Al: yedekten eski hale döndürür.
public static class PoppyPlaytimeLook
{
    const string BackupPath = "Assets/_Project/Settings/PoppyBackup.json";
    const string ProfilePath = "Assets/_Project/Settings/PEST_CO_Poppy.asset";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";

    static readonly string[] FloatProps = { "_Realism", "_Smoothness", "_SpecStrength", "_SpecSize", "_BrushStrength", "_RimStrength", "_OutlinePixels", "_GradStrength", "_AmbientStrength" };

    // Böcekler Poppy oyuncak karakterleri gibi: fare Huggy mavisi, örümcek Mommy Long Legs pembesi, hamamböceği parlak oyuncak plastiği.
    static readonly Dictionary<string, Color> ToyColors = new Dictionary<string, Color>
    {
        { "Assets/_Project/Materials/M_Mouse.mat", new Color(0.10f, 0.38f, 1.00f) },
        { "Assets/_Project/Materials/M_Pest.mat", new Color(0.10f, 0.38f, 1.00f) },
        { "Assets/_Project/Materials/M_SpiderBody.mat", new Color(1.00f, 0.22f, 0.62f) },
        { "Assets/_Project/Materials/M_SpiderLeg.mat", new Color(0.85f, 0.10f, 0.45f) },
    };
    static readonly string[] ToyGloss = { "Assets/_Project/FX/Cockroach/M_CockroachToon.mat", "Assets/_Project/Materials/M_SpiderEye.mat" };

    [System.Serializable] class MatEntry { public string path; public string[] names; public float[] values; public Color baseColor; public bool hasBase; }
    [System.Serializable] class Backup
    {
        public List<MatEntry> mats = new List<MatEntry>();
        public string globalProfile; public bool courageActive; public bool inkActive;
        public Color shadowTint; public float shadowTintAmount, yellowCut, outlineThinning, saturationBoost, flatColors;
        public Color ambSky, ambEq, ambGround;
    }

    [MenuItem("PestCo/Görünüm/Poppy Playtime Uygula")]
    public static string Apply()
    {
        if (EditorApplication.isPlaying) return "Play modunda uygulanamaz";
        if (File.Exists(BackupPath)) return "Zaten uygulanmış (önce Geri Al)";
        var b = new Backup();

        // 1) Materyaller: parlak oyuncak plastiği, fırça dokusu yok, ince kontur
        var toonGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        foreach (var g in toonGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null || m.shader.name != "PestCo/Toon") continue;
            var e = new MatEntry { path = path, names = FloatProps.Where(m.HasProperty).ToArray() };
            e.values = e.names.Select(m.GetFloat).ToArray();
            e.hasBase = m.HasProperty("_BaseColor"); if (e.hasBase) e.baseColor = m.GetColor("_BaseColor");
            b.mats.Add(e);

            bool toy = ToyColors.ContainsKey(path) || ToyGloss.Contains(path);
            Set(m, "_Realism", toy ? 0.75f : 0.6f);
            Set(m, "_Smoothness", Mathf.Max(m.GetFloat("_Smoothness"), toy ? 0.82f : 0.5f));
            Set(m, "_SpecStrength", Mathf.Max(m.GetFloat("_SpecStrength"), toy ? 0.6f : 0.25f));
            Set(m, "_SpecSize", 0.93f);
            Set(m, "_BrushStrength", m.GetFloat("_BrushStrength") * 0.25f);
            Set(m, "_RimStrength", toy ? 0.5f : 0.2f);
            Set(m, "_GradStrength", Mathf.Max(m.GetFloat("_GradStrength"), 0.3f));
            Set(m, "_OutlinePixels", m.GetFloat("_OutlinePixels") * (toy ? 0.5f : 0.25f));
            if (e.hasBase)
            {
                if (ToyColors.TryGetValue(path, out var c)) m.SetColor("_BaseColor", c);
                else m.SetColor("_BaseColor", Vivid(m.GetColor("_BaseColor"), 1.35f));
            }
            EditorUtility.SetDirty(m);
        }

        // 2) Global volume: ACES, kontrast, doygunluk, bloom, nötr koyu vinyet
        var global = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(v => v.name == "Global Volume");
        if (global != null)
        {
            b.globalProfile = AssetDatabase.GetAssetPath(global.sharedProfile);
            global.sharedProfile = CreateProfile();
            EditorUtility.SetDirty(global);
        }

        // 3) Courage/çizgi film bölgesi (soldurma, eğrilik, titrek kontur) kapanır
        var courage = FindAny("KitchenCourage");
        if (courage != null) { b.courageActive = courage.activeSelf; courage.SetActive(false); }

        // 4) Mor gölge paleti -> nötr serin gölge, yüksek doygunluk, düz renk
        var tpg = Object.FindFirstObjectByType<ToonPaletteGrade>();
        if (tpg != null)
        {
            var so = new SerializedObject(tpg);
            b.shadowTint = so.FindProperty("shadowTint").colorValue;
            b.shadowTintAmount = so.FindProperty("shadowTintAmount").floatValue;
            b.yellowCut = so.FindProperty("yellowCut").floatValue;
            b.outlineThinning = so.FindProperty("outlineThinning").floatValue;
            b.saturationBoost = so.FindProperty("saturationBoost").floatValue;
            b.flatColors = so.FindProperty("flatColors").floatValue;
            so.FindProperty("shadowTint").colorValue = new Color(0.92f, 0.95f, 1.0f);
            so.FindProperty("shadowTintAmount").floatValue = 0.35f;
            so.FindProperty("yellowCut").floatValue = 0.2f;
            so.FindProperty("outlineThinning").floatValue = 0.8f;
            so.FindProperty("saturationBoost").floatValue = 0.6f;
            so.FindProperty("flatColors").floatValue = 1f;
            so.ApplyModifiedProperties();
        }

        // 5) Ekran mürekkep çizgileri kapanır (Poppy temiz 3D görüntü)
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        var ink = rd != null ? rd.rendererFeatures.FirstOrDefault(f => f != null && f.name == "DepthInk") : null;
        if (ink != null) { b.inkActive = ink.isActive; ink.SetActive(false); EditorUtility.SetDirty(rd); }

        // 6) Ortam ışığı: gri pus yerine daha koyu, hafif serin -> lambalar ve renkler öne çıkar
        b.ambSky = RenderSettings.ambientSkyColor; b.ambEq = RenderSettings.ambientEquatorColor; b.ambGround = RenderSettings.ambientGroundColor;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.47f, 0.50f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.30f, 0.30f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.11f);

        File.WriteAllText(BackupPath, JsonUtility.ToJson(b, true));
        AssetDatabase.ImportAsset(BackupPath);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "Poppy uygulandı: " + b.mats.Count + " materyal";
    }

    [MenuItem("PestCo/Görünüm/Poppy Playtime Geri Al")]
    public static string Revert()
    {
        if (EditorApplication.isPlaying) return "Play modunda geri alınamaz";
        if (!File.Exists(BackupPath)) return "Yedek yok";
        var b = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
        foreach (var e in b.mats)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(e.path);
            if (m == null) continue;
            for (int i = 0; i < e.names.Length; i++) m.SetFloat(e.names[i], e.values[i]);
            if (e.hasBase) m.SetColor("_BaseColor", e.baseColor);
            EditorUtility.SetDirty(m);
        }
        var global = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(v => v.name == "Global Volume");
        if (global != null && !string.IsNullOrEmpty(b.globalProfile)) { global.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(b.globalProfile); EditorUtility.SetDirty(global); }
        var courage = FindAny("KitchenCourage");
        if (courage != null) courage.SetActive(b.courageActive);
        var tpg = Object.FindFirstObjectByType<ToonPaletteGrade>();
        if (tpg != null)
        {
            var so = new SerializedObject(tpg);
            so.FindProperty("shadowTint").colorValue = b.shadowTint;
            so.FindProperty("shadowTintAmount").floatValue = b.shadowTintAmount;
            so.FindProperty("yellowCut").floatValue = b.yellowCut;
            so.FindProperty("outlineThinning").floatValue = b.outlineThinning;
            so.FindProperty("saturationBoost").floatValue = b.saturationBoost;
            so.FindProperty("flatColors").floatValue = b.flatColors;
            so.ApplyModifiedProperties();
        }
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        var ink = rd != null ? rd.rendererFeatures.FirstOrDefault(f => f != null && f.name == "DepthInk") : null;
        if (ink != null) { ink.SetActive(b.inkActive); EditorUtility.SetDirty(rd); }
        RenderSettings.ambientSkyColor = b.ambSky; RenderSettings.ambientEquatorColor = b.ambEq; RenderSettings.ambientGroundColor = b.ambGround;
        AssetDatabase.DeleteAsset(BackupPath);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "Poppy geri alındı";
    }

    static VolumeProfile CreateProfile()
    {
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, ProfilePath); }
        foreach (var c in p.components.ToList()) { p.Remove(c.GetType()); Object.DestroyImmediate(c, true); }

        var tm = Add<Tonemapping>(p); tm.mode.Override(TonemappingMode.ACES);
        var ca = Add<ColorAdjustments>(p);
        ca.postExposure.Override(0.35f); ca.contrast.Override(22f); ca.saturation.Override(24f);
        var smh = Add<ShadowsMidtonesHighlights>(p);
        smh.shadows.Override(new Vector4(0.92f, 0.97f, 1.08f, -0.04f));
        smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.94f, 0.02f));
        var bl = Add<Bloom>(p); bl.threshold.Override(0.95f); bl.intensity.Override(0.55f); bl.scatter.Override(0.68f);
        var vg = Add<Vignette>(p); vg.color.Override(Color.black); vg.intensity.Override(0.3f); vg.smoothness.Override(0.5f);
        var fg = Add<FilmGrain>(p); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.08f);
        var chr = Add<ChromaticAberration>(p); chr.intensity.Override(0.06f);
        EditorUtility.SetDirty(p);
        return p;
    }

    static T Add<T>(VolumeProfile p) where T : VolumeComponent
    {
        var c = p.Add<T>(true);
        c.name = typeof(T).Name; c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    static Color Vivid(Color c, float sat)
    {
        Color.RGBToHSV(c, out var h, out var s, out var v);
        return Color.HSVToRGB(h, Mathf.Clamp01(s * sat), v).WithAlpha(c.a);
    }
    static Color WithAlpha(this Color c, float a) { c.a = a; return c; }

    static void Set(Material m, string n, float v) { if (m.HasProperty(n)) m.SetFloat(n, v); }

    static GameObject FindAny(string name) =>
        Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == name && g.scene.IsValid());
}
