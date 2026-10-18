using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tool_2 (vakum) için çizgi film modeli kurar: dönel (lathe) meshler + PestCo/Toon materyalleri + animatör bağlantısı.
// Menü: PestCo/Vakum Cizgi Film Kur  (tekrar çalıştırılabilir; eski modeli silip yeniden kurar)
public static class VacuumCartoonSetup
{
    private const string Dir = "Assets/_Project/FX/VacuumCartoon";

    [MenuItem("PestCo/Vakum Cizgi Film Kur")]
    public static void Run()
    {
        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        var orange = Mat("M_VacBody", new Color(1f, 0.50f, 0.08f), new Color(0.80f, 0.20f, 0.30f), 0f);
        var cream = Mat("M_VacCream", new Color(1f, 0.93f, 0.62f), new Color(0.90f, 0.62f, 0.55f), 0f);
        var dark = Mat("M_VacDark", new Color(0.20f, 0.22f, 0.32f), new Color(0.08f, 0.08f, 0.22f), 0f);
        var glass = Mat("M_VacBin", new Color(0.62f, 0.95f, 1f), new Color(0.30f, 0.55f, 0.95f), 0.8f);
        var black = Mat("M_VacMouth", new Color(0.03f, 0.03f, 0.05f), new Color(0.02f, 0.02f, 0.05f), 0f);
        var lamp = Mat("M_VacLamp", new Color(1f, 0.95f, 0.6f), new Color(1f, 0.7f, 0.3f), 0.5f);
        lamp.SetColor("_EmissionColor", new Color(1.4f, 1.1f, 0.4f)); EditorUtility.SetDirty(lamp);

        var tool = FindTool2();
        if (tool == null) { Debug.LogError("Tool_2 bulunamadı."); return; }

        // Eski silindir görselini kaldır, Tool_2'yi sıfırla (VacuumTool kendi transformunu kullanmaz).
        foreach (var c in tool.GetComponents<Component>()) if (c is MeshFilter || c is MeshRenderer) Object.DestroyImmediate(c);
        tool.localPosition = Vector3.zero; tool.localRotation = Quaternion.identity; tool.localScale = Vector3.one;
        var old = tool.Find("VacuumGunModel"); if (old != null) Object.DestroyImmediate(old.gameObject);

        var model = new GameObject("VacuumGunModel").transform;
        model.SetParent(tool, false);
        model.localPosition = new Vector3(0.14f, -0.04f, 0.30f);
        model.localRotation = Quaternion.Euler(4f, -22f, 3f);
        model.localScale = Vector3.one * 0.95f;

        // --- Gövde (yatay tank), şerit, ışık
        Part("Tank", model, Lathe("VacTank", new[] {
            V(0,-0.19f), V(0.035f,-0.188f), V(0.06f,-0.18f), V(0.078f,-0.165f), V(0.085f,-0.14f),
            V(0.085f,0.04f), V(0.083f,0.07f), V(0.075f,0.09f), V(0.06f,0.10f), V(0.04f,0.105f), V(0,0.105f) }), orange, Vector3.zero, Vector3.zero);
        Part("Band", model, Ring("VacBand", 0.087f, -0.04f, 0.008f), cream, Vector3.zero, Vector3.zero);
        Part("Band2", model, Ring("VacBand", 0.087f, -0.1f, 0.008f), cream, Vector3.zero, Vector3.zero);
        var lampT = Sphere("Headlamp", model, new Vector3(0, 0.083f, 0.07f), 0.034f, lamp);

        // --- Oluklu hortum boynu + huni ağızlık
        var neck = Part("Neck", model, Lathe("VacNeck", Ribbed(0.10f, 0.215f, 0.040f, 0.010f, 9)), dark, Vector3.zero, Vector3.zero);
        Part("Funnel", model, Lathe("VacFunnel", new[] {
            V(0.045f,0.20f), V(0.05f,0.22f), V(0.07f,0.25f), V(0.10f,0.285f), V(0.118f,0.30f),
            V(0.124f,0.305f), V(0.12f,0.312f), V(0.112f,0.31f),
            V(0.098f,0.298f), V(0.07f,0.265f), V(0.045f,0.235f), V(0.038f,0.215f) }), cream, Vector3.zero, Vector3.zero);
        Part("Rim", model, Ring("VacRim", 0.118f, 0.31f, 0.011f), orange, Vector3.zero, Vector3.zero);
        Part("Mouth", model, Lathe("VacMouth", new[] { V(0.04f, 0.228f), V(0, 0.228f) }), black, Vector3.zero, Vector3.zero);

        // --- Şeffaf toz haznesi (üstte, ekseni yukarı)
        var bin = new GameObject("Bin").transform; bin.SetParent(model, false);
        bin.localPosition = new Vector3(0, 0.076f, -0.04f); bin.localRotation = Quaternion.Euler(-90, 0, 0);
        Part("BinGlass", bin, Lathe("VacBin", new[] {
            V(0.052f,0), V(0.055f,0.01f), V(0.055f,0.05f), V(0.05f,0.075f), V(0.035f,0.095f), V(0.015f,0.105f), V(0,0.108f) }), glass, Vector3.zero, Vector3.zero);
        Part("BinRing", bin, Ring("VacBinRing", 0.056f, 0.0f, 0.009f), cream, Vector3.zero, Vector3.zero);
        for (int i = 0; i < 3; i++)   // içinde zıplayan toz topları
            Sphere("Dust" + i, bin, new Vector3((i - 1) * 0.02f, (i % 2) * 0.012f, 0.03f + i * 0.015f), 0.022f, dark);

        // --- Tutacak + tetik
        var handle = new GameObject("Handle").transform; handle.SetParent(model, false);
        handle.localPosition = new Vector3(0, -0.075f, -0.07f); handle.localRotation = Quaternion.Euler(100, 0, 0);
        Part("HandleMesh", handle, Lathe("VacHandle", new[] {
            V(0,0), V(0.012f,0.003f), V(0.02f,0.012f), V(0.022f,0.04f), V(0.02f,0.12f), V(0.012f,0.13f), V(0,0.133f) }), dark, Vector3.zero, Vector3.zero);
        Part("Grip", handle, Ring("VacGrip", 0.023f, 0.07f, 0.007f), cream, Vector3.zero, Vector3.zero);
        Part("Grip2", handle, Ring("VacGrip", 0.023f, 0.095f, 0.007f), cream, Vector3.zero, Vector3.zero);
        var trig = GameObject.CreatePrimitive(PrimitiveType.Cube); trig.name = "Trigger"; Object.DestroyImmediate(trig.GetComponent<Collider>());
        trig.transform.SetParent(model, false); trig.transform.localPosition = new Vector3(0, -0.085f, -0.03f); trig.transform.localScale = new Vector3(0.012f, 0.03f, 0.014f);
        trig.GetComponent<Renderer>().sharedMaterial = cream;

        // --- Layer ve gölge: silah katmanı
        int weaponLayer = LayerMask.NameToLayer("Weapon");
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (weaponLayer >= 0) r.gameObject.layer = weaponLayer;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // --- Animatör
        var anim = model.gameObject.AddComponent<VacuumGunAnimator>();
        var so = new SerializedObject(anim);
        so.FindProperty("vacuum").objectReferenceValue = tool.GetComponent<VacuumTool>();
        so.FindProperty("neck").objectReferenceValue = neck;
        so.FindProperty("bin").objectReferenceValue = bin;
        so.ApplyModifiedProperties();

        // --- Emiş partikülü huni ağzının önüne
        var fx = tool.Find("VacuumFX");
        if (fx != null)
        {
            fx.SetParent(tool, false);
            fx.position = model.TransformPoint(new Vector3(0, 0, 0.33f));
            fx.rotation = model.rotation;
            fx.localScale = Vector3.one;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[VacuumCartoon] Kurulum tamam.");
    }

    // ---------- Yardımcılar
    private static Transform FindTool2()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == "Tool_2" && t.gameObject.scene.IsValid() && t.GetComponent<VacuumTool>() != null) return t;
        return null;
    }

    private static Vector2 V(float r, float z) { return new Vector2(r, z); }

    private static Transform Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localEulerAngles = euler;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g.transform;
    }

    private static Transform Sphere(string name, Transform parent, Vector3 pos, float diameter, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name = name; Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = Vector3.one * diameter;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g.transform;
    }

    private static Material Mat(string name, Color baseCol, Color shadow, float spec)
    {
        string path = $"{Dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("PestCo/Toon")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("PestCo/Toon");
        m.SetColor("_BaseColor", baseCol); m.SetColor("_ShadowColor", shadow);
        m.SetColor("_OutlineColor", new Color(0.04f, 0.025f, 0.04f));   // sprey tabancasıyla aynı kontur
        m.SetFloat("_OutlinePixels", 3.6f);
        m.SetFloat("_SpecStrength", spec); m.SetFloat("_SpecSize", 0.9f);
        m.SetFloat("_RimStrength", 0.5f); m.SetFloat("_HiStrength", 0.2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Oluklu boru profili: yarıçap |sin| ile dalgalanır.
    private static Vector2[] Ribbed(float z0, float z1, float r, float amp, int ribs)
    {
        int n = ribs * 6; var p = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float u = i / (float)n;
            p[i] = new Vector2(r + amp * Mathf.Abs(Mathf.Sin(u * ribs * Mathf.PI)), Mathf.Lerp(z0, z1, u));
        }
        return p;
    }

    // Halka (torus): eksen etrafında z konumunda, yarıçap r0, tüp yarıçapı rho.
    private static Mesh Ring(string name, float r0, float z, float rho)
    {
        const int n = 10; var p = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            p[i] = new Vector2(r0 + rho * Mathf.Cos(a), z + rho * Mathf.Sin(a));
        }
        return Lathe(name, p, closed: true);
    }

    // Profil (r, z) noktalarını Z ekseni etrafında döndürür; normaller profilden analitik hesaplanır (kabuk kontur için yumuşak).
    private static Mesh Lathe(string name, Vector2[] prof, int seg = 28, bool closed = false)
    {
        int n = prof.Length;
        var verts = new Vector3[n * (seg + 1)]; var norms = new Vector3[verts.Length]; var uvs = new Vector2[verts.Length];
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = prof[closed ? (i - 1 + n - 1) % (n - 1) : Mathf.Max(i - 1, 0)];
            Vector2 next = prof[closed ? (i + 1) % (n - 1) : Mathf.Min(i + 1, n - 1)];
            Vector2 t = next - prev; Vector2 nn = new Vector2(t.y, -t.x).normalized;
            for (int j = 0; j <= seg; j++)
            {
                float a = j / (float)seg * Mathf.PI * 2f; float c = Mathf.Cos(a), s = Mathf.Sin(a);
                int idx = i * (seg + 1) + j;
                verts[idx] = new Vector3(prof[i].x * c, prof[i].x * s, prof[i].y);
                norms[idx] = new Vector3(nn.x * c, nn.x * s, nn.y);
                uvs[idx] = new Vector2(j / (float)seg, i / (float)(n - 1));
            }
        }
        var tris = new int[(n - 1) * seg * 6]; int k = 0;
        for (int i = 0; i < n - 1; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = i * (seg + 1) + j, b = a + 1, c2 = a + seg + 1, d = c2 + 1;
                tris[k++] = a; tris[k++] = b; tris[k++] = c2;
                tris[k++] = b; tris[k++] = d; tris[k++] = c2;
            }
        var mesh = new Mesh { name = name, vertices = verts, normals = norms, uv = uvs, triangles = tris };
        mesh.RecalculateBounds();
        string path = $"{Dir}/{name}_{prof.Length}_{(int)(prof[0].x * 1000)}_{(int)(prof[0].y * 1000)}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
