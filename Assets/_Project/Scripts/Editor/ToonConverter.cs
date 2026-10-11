using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Opak URP/Lit materyalleri "PestCo/Toon" shader'ina cevirir (renk/doku/emisyon korunur).
public static class ToonConverter
{
    static readonly string[] Skip = { "Glass", "Water", "Mist", "Web", "Thread", "Mirror", "CorpseFade", "Marker", "Screen" };
    const string BrushPath = "Assets/_Project/Textures/Stylized/T_BrushAtlas_512.png";

    [MenuItem("PestCo/Tum Materyalleri Toon Yap")]
    public static void ConvertAll()
    {
        var toon = Shader.Find("PestCo/Toon");
        var brush = AssetDatabase.LoadAssetAtPath<Texture2D>(BrushPath);
        int done = 0, skipped = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Materials", "Assets/PestCo" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m || m.shader == toon || m.shader == null) continue;
            if (m.shader.name != "Universal Render Pipeline/Lit" && m.shader.name != "Universal Render Pipeline/Simple Lit") { skipped++; continue; }
            if (ShouldSkip(m)) { skipped++; continue; }
            Convert(m, toon, brush);
            done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Toon] {done} materyal cevrildi, {skipped} atlandi.");
    }

    static bool ShouldSkip(Material m)
    {
        if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) return true;
        if (m.renderQueue >= 3000) return true;
        foreach (var s in Skip) if (m.name.Contains(s)) return true;
        return false;
    }

    static void Convert(Material m, Shader toon, Texture2D brush)
    {
        Color b = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
        Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
        Vector2 sc = m.HasProperty("_BaseMap") ? m.GetTextureScale("_BaseMap") : Vector2.one;
        Vector2 of = m.HasProperty("_BaseMap") ? m.GetTextureOffset("_BaseMap") : Vector2.zero;
        Color em = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
        float met = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
        float smo = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0f;

        m.shader = toon;
        m.renderQueue = -1;
        b.a = 1f;
        Color.RGBToHSV(b, out float h, out float s, out float v);
        // golge: biraz daha doygun, koyu ve soguk ton
        Color shadow = Color.HSVToRGB(Mathf.Repeat(h + 0.03f, 1f), Mathf.Clamp01(s * 1.1f + 0.04f), v * 0.52f);
        Color outline = Color.HSVToRGB(h, Mathf.Clamp01(s * 0.8f), Mathf.Min(v * 0.22f, 0.22f));

        m.SetColor("_BaseColor", b);
        m.SetColor("_ShadowColor", shadow);
        m.SetColor("_OutlineColor", outline);
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", sc);
        m.SetTextureOffset("_BaseMap", of);
        m.SetTexture("_BrushTex", brush);
        m.SetColor("_EmissionColor", em);
        m.SetFloat("_Threshold", 0.5f);
        m.SetFloat("_Softness", 0.12f);
        m.SetFloat("_BrushScale", 1.5f);
        m.SetFloat("_BrushStrength", 0.12f);
        m.SetFloat("_OutlinePixels", 2f);
        m.SetFloat("_RimStrength", 0.35f);
        m.SetFloat("_GradStrength", 0.28f);
        m.SetFloat("_LampBoost", 0.9f);
        m.SetFloat("_SpecStrength", (met > 0.5f || smo > 0.6f) ? 0.7f : 0f);
        m.SetFloat("_SpecSize", 0.93f);
        m.SetShaderPassEnabled("SRPDefaultUnlit", true);
        EditorUtility.SetDirty(m);
    }
}
