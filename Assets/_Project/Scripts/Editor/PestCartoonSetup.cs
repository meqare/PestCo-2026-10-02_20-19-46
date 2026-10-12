using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Haşere çizgi film efektleri kurulumu: dokular, sesler, materyaller, yüz hiyerarşisi, partiküller ve PestCartoonFX bağlantıları.
// Menü: PestCo/Hasere Cizgi Film Kur  (tekrar çalıştırılabilir; eski FX nesnelerini yeniden kurar)
public static class PestCartoonSetup
{
    private const string FxDir = "Assets/_Project/FX/PestCartoon";

    [MenuItem("PestCo/Hasere Cizgi Film Kur")]
    public static void Run()
    {
        Directory.CreateDirectory(FxDir);
        AssetDatabase.Refresh();

        string pPuff = SaveTex("T_Puff", 64, Puff);
        string pStar = SaveTex("T_Star", 64, Star);
        string pSpiral = SaveTex("T_Spiral", 64, Spiral);
        string pIcons = SaveTex("T_Icons", 128, 64, Icons);
        string pTik = SaveWav("A_Tik", 0.07f, Tik);
        string pPop = SaveWav("A_Pop", 0.18f, PopSfx);
        AssetDatabase.Refresh();
        Texture2D texPuff = Reload(pPuff), texStar = Reload(pStar), texSpiral = Reload(pSpiral), texIcons = Reload(pIcons);
        var clipTik = AssetDatabase.LoadAssetAtPath<AudioClip>(pTik);
        var clipPop = AssetDatabase.LoadAssetAtPath<AudioClip>(pPop);

        Material green = ToonMat("M_PestGreen", new Color(0.32f, 0.85f, 0.22f), new Color(0.08f, 0.38f, 0.42f), true);
        Material white = ToonMat("M_PestEye", new Color(0.97f, 0.97f, 1f), new Color(0.65f, 0.68f, 0.9f), false);
        Material black = ToonMat("M_PestBlack", new Color(0.03f, 0.03f, 0.04f), new Color(0.02f, 0.02f, 0.05f), false);
        Material brow = ToonMat("M_PestBrow", new Color(0.05f, 0.22f, 0.08f), new Color(0.02f, 0.1f, 0.1f), false);
        Material mouthMat = ToonMat("M_PestMouth", new Color(0.18f, 0.02f, 0.05f), new Color(0.08f, 0.01f, 0.05f), false);
        Material pink = ToonMat("M_PestTongue", new Color(1f, 0.38f, 0.55f), new Color(0.65f, 0.15f, 0.4f), false);

        var pest = GameObject.Find("PestPlayer");
        if (pest == null) { Debug.LogError("PestPlayer sahnede yok (PestTestRig açık olmalı)."); return; }

        // --- Görsel hiyerarşi: Visual > Rig > (Body, Face > ...)
        Transform visual = pest.transform.Find("Visual (placeholder)");
        if (visual == null) { visual = new GameObject("Visual (placeholder)").transform; visual.SetParent(pest.transform, false); }
        foreach (var c in visual.GetComponents<Component>())
            if (c is MeshFilter || c is MeshRenderer || c is Collider) Object.DestroyImmediate(c);
        for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
        visual.localScale = Vector3.one;
        visual.localPosition = new Vector3(0, 0.05f, 0);
        visual.localRotation = Quaternion.identity;

        var rig = New("Rig", visual, Vector3.zero);
        var body = Prim("Body", PrimitiveType.Capsule, rig, Vector3.zero, new Vector3(0.07f, 0.05f, 0.09f), green);
        var face = New("Face", rig, Vector3.zero);

        var eyeL = Prim("SolGoz", PrimitiveType.Sphere, face, new Vector3(-0.017f, 0.02f, 0.036f), Vector3.one * 0.032f, white);
        var eyeR = Prim("SagGoz", PrimitiveType.Sphere, face, new Vector3(0.017f, 0.02f, 0.036f), Vector3.one * 0.032f, white);
        var pupilL = Prim("Pupil", PrimitiveType.Sphere, eyeL, new Vector3(0, 0, 0.42f), Vector3.one * 0.42f, black);
        var pupilR = Prim("Pupil", PrimitiveType.Sphere, eyeR, new Vector3(0, 0, 0.42f), Vector3.one * 0.42f, black);
        var browL = Prim("BrowL", PrimitiveType.Cube, face, new Vector3(-0.017f, 0.043f, 0.038f), new Vector3(0.02f, 0.005f, 0.006f), brow);
        var browR = Prim("BrowR", PrimitiveType.Cube, face, new Vector3(0.017f, 0.043f, 0.038f), new Vector3(0.02f, 0.005f, 0.006f), brow);
        var mouth = Prim("Mouth", PrimitiveType.Sphere, face, new Vector3(0, -0.008f, 0.046f), new Vector3(0.026f, 0.007f, 0.004f), mouthMat);
        var teeth = Prim("Teeth", PrimitiveType.Cube, face, new Vector3(0, -0.0045f, 0.0475f), new Vector3(0.02f, 0.004f, 0.003f), white);
        var tongue = New("Tongue", face, new Vector3(0, -0.012f, 0.047f));
        tongue.localScale = new Vector3(0.011f, 0.018f, 0.003f);
        Prim("Mesh", PrimitiveType.Cube, tongue, new Vector3(0, -0.5f, 0), Vector3.one, pink);

        // --- FX nesneleri
        var old = pest.transform.Find("CartoonFX"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var fx = New("CartoonFX", pest.transform, Vector3.zero);
        var smoke = Particles("Smoke", fx, new Vector3(0, 0.015f, -0.04f), texPuff, true);
        var stars = Particles("Stars", fx, new Vector3(0, 0.05f, 0), texStar, true);
        var lines = Particles("SpeedLines", fx, new Vector3(0, 0.04f, -0.05f), texPuff, true);
        var spiral = Particles("Spiral", fx, new Vector3(0, 0.09f, 0), texSpiral, true);
        var icons = Particles("Icons", fx, new Vector3(0, 0.085f, 0), texIcons, true);
        ConfigSmoke(smoke); ConfigStars(stars); ConfigLines(lines); ConfigSpiral(spiral); ConfigIcons(icons);
        lines.transform.localRotation = Quaternion.Euler(0, 180, 0);

        var audio = pest.GetComponent<AudioSource>();
        if (audio == null) audio = pest.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 0f;

        // --- PestMovement bağlantıları
        var mv = pest.GetComponent<PestMovement>();
        var smv = new SerializedObject(mv);
        smv.FindProperty("visual").objectReferenceValue = visual;
        smv.FindProperty("solGoz").objectReferenceValue = eyeL;
        smv.FindProperty("sagGoz").objectReferenceValue = eyeR;
        smv.FindProperty("eyePopDistance").floatValue = 0.025f; // Face ölçeksiz: değer metre
        smv.ApplyModifiedProperties();

        // --- PestCartoonFX
        var cfx = pest.GetComponent<PestCartoonFX>();
        if (cfx == null) cfx = pest.AddComponent<PestCartoonFX>();
        var so = new SerializedObject(cfx);
        Ref(so, "movement", mv); Ref(so, "rig", rig); Ref(so, "face", face);
        Ref(so, "eyeL", eyeL); Ref(so, "eyeR", eyeR); Ref(so, "pupilL", pupilL); Ref(so, "pupilR", pupilR);
        Ref(so, "browL", browL); Ref(so, "browR", browR); Ref(so, "mouth", mouth); Ref(so, "teeth", teeth); Ref(so, "tongue", tongue);
        Ref(so, "bodyRenderer", body.GetComponent<Renderer>());
        Ref(so, "smoke", smoke); Ref(so, "stars", stars); Ref(so, "speedLines", lines); Ref(so, "spiral", spiral); Ref(so, "icons", icons);
        Ref(so, "audioSource", audio); Ref(so, "tik", clipTik); Ref(so, "pop", clipPop);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[PestCartoon] Kurulum tamam.");
    }

    // ---------- Sahne yardımcıları
    private static void Ref(SerializedObject so, string name, Object value) { so.FindProperty(name).objectReferenceValue = value; }

    private static Transform New(string name, Transform parent, Vector3 pos)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = pos; return g.transform;
    }

    private static Transform Prim(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    private static Material ToonMat(string name, Color baseCol, Color shadow, bool body)
    {
        string path = $"{FxDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("PestCo/Toon")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("PestCo/Toon");
        m.SetColor("_BaseColor", baseCol); m.SetColor("_ShadowColor", shadow);
        m.SetColor("_OutlineColor", new Color(0.02f, 0.05f, 0.03f));
        m.SetFloat("_OutlinePixels", body ? 3.5f : 2.5f);
        m.SetFloat("_GradStrength", 0f);   // dünya yüksekliğine göre koyulaşma küçük haşerede anlamsız
        m.SetFloat("_BrushScale", 12f);     // minik nesnede fırça izi ölçeği
        if (body)
        {
            // parlak vurgu + hafif gradyan (yeşil kimliği korunur)
            m.SetFloat("_SpecStrength", 0.9f); m.SetFloat("_SpecSize", 0.88f);
            m.SetFloat("_RimStrength", 1f); m.SetFloat("_HiStrength", 0.3f);
            m.SetFloat("_GradAmount", 0.8f);
            m.SetColor("_GradTop", new Color(1.15f, 1.2f, 0.85f));
            m.SetColor("_GradBottom", new Color(0.65f, 0.9f, 1.05f));
            m.SetFloat("_GradY0", 0f); m.SetFloat("_GradH", 0.1f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Partiküller
    private static ParticleSystem Particles(string name, Transform parent, Vector3 pos, Texture2D tex, bool world)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = pos;
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.playOnAwake = false;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.enabled = true;
        var rend = g.GetComponent<ParticleSystemRenderer>();
        string mp = $"{FxDir}/M_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("Sprites/Default"); mat.mainTexture = tex; EditorUtility.SetDirty(mat);
        rend.sharedMaterial = mat; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rend.receiveShadows = false;
        return ps;
    }

    private static void ConfigSmoke(ParticleSystem ps)
    {
        var m = ps.main; m.startLifetime = 0.5f; m.startSpeed = 0.05f; m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
        m.startColor = new Color(0.9f, 0.9f, 1f, 0.75f); m.gravityModifier = -0.05f; m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.01f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 1.6f)));
        FadeOut(ps);
        ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 1;
    }

    private static void ConfigStars(ParticleSystem ps)
    {
        var m = ps.main; m.startLifetime = 0.55f; m.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f); m.gravityModifier = 0.6f;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.2f), new Color(1f, 1f, 1f));
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.02f;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0f)));
    }

    private static void ConfigLines(ParticleSystem ps)
    {
        var m = ps.main; m.startLifetime = 0.2f; m.startSpeed = 0.5f; m.startSize = 0.004f;
        m.startColor = new Color(1f, 1f, 1f, 0.8f);
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 12f; s.radius = 0.025f;
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 8f; r.velocityScale = 0f;
        FadeOut(ps);
    }

    private static void ConfigSpiral(ParticleSystem ps)
    {
        var m = ps.main; m.startLifetime = 0.8f; m.startSpeed = 0.06f; m.startSize = 0.05f;
        m.startColor = new Color(0.5f, 0.9f, 1f, 1f); m.simulationSpace = ParticleSystemSimulationSpace.Local;
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Circle; s.radius = 0.001f; s.rotation = new Vector3(-90, 0, 0);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(8f);
        FadeOut(ps);
    }

    private static void ConfigIcons(ParticleSystem ps)
    {
        var m = ps.main; m.startLifetime = 0.9f; m.startSpeed = 0.14f; m.startSize = 0.04f;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.5f), new Color(1f, 0.85f, 0.15f));
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 25f; s.radius = 0.01f; s.rotation = new Vector3(-90, 0, 0);
        var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 1;
        sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.frameOverTime = 0f;
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.99f);
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.2f, 1.2f), new Keyframe(0.35f, 1f), new Keyframe(1, 0.9f)));
        FadeOut(ps);
    }

    private static void FadeOut(ParticleSystem ps)
    {
        var c = ps.colorOverLifetime; c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        c.color = g;
    }

    // ---------- Doku üretimi
    private delegate float Shape(float x, float y); // x,y: -1..1, dönüş: alfa 0-1

    private static float Soft(float d, float edge = 0.08f) { return Mathf.Clamp01(d / edge); }

    private static float Puff(float x, float y) { float r = Mathf.Sqrt(x * x + y * y); return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.95f - r) / 0.35f)); }

    private static float Star(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y); float a = Mathf.Atan2(y, x) - Mathf.PI * 0.5f;
        float f = Mathf.Repeat(a / (2f * Mathf.PI) * 5f, 1f); float tri = 1f - Mathf.Abs(2f * f - 1f);
        float R = Mathf.Lerp(0.38f, 0.95f, Mathf.SmoothStep(0f, 1f, tri));
        return Soft(R - r);
    }

    private static float Spiral(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y); float a = Mathf.Atan2(y, x); if (a < 0) a += 2f * Mathf.PI;
        float best = 0f; const float turns = 2.5f;
        for (int k = 0; k < 4; k++)
        {
            float rk = (a + 2f * Mathf.PI * k) / (2f * Mathf.PI * turns) * 0.9f;
            best = Mathf.Max(best, Soft(0.11f - Mathf.Abs(r - rk), 0.06f));
        }
        return best;
    }

    private static float Icons(float x, float y)
    {
        // x: -1..1 tüm doku; sol hücre "!", sağ hücre kalp
        bool left = x < 0f; float lx = left ? (x + 0.5f) * 2f : (x - 0.5f) * 2f; float ly = y;
        if (left)
        {
            float bar = Soft(0.22f - Mathf.Abs(lx), 0.08f) * Soft(0.55f - Mathf.Abs(ly - 0.3f), 0.08f);
            float dot = Soft(0.2f - Mathf.Sqrt(lx * lx + (ly + 0.65f) * (ly + 0.65f)), 0.08f);
            return Mathf.Max(bar, dot);
        }
        float hx = lx * 1.3f, hy = ly * 1.3f + 0.1f;
        float v = Mathf.Pow(hx * hx + hy * hy - 1f, 3f) - hx * hx * hy * hy * hy;
        return Soft(-v, 0.25f);
    }

    private static string SaveTex(string name, int size, Shape shape) { return SaveTex(name, size, size, shape); }

    private static string SaveTex(string name, int w, int h, Shape shape)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                float x = (px + 0.5f) / w * 2f - 1f, y = (py + 0.5f) / h * 2f - 1f;
                tex.SetPixel(px, py, new Color(1, 1, 1, Mathf.Clamp01(shape(x, y))));
            }
        string path = $"{FxDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return path;
    }

    private static Texture2D Reload(string path)
    {
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- Ses üretimi (16-bit mono WAV)
    private delegate float Wave(float t, float duration);

    private static float Tik(float t, float d) { float e = Mathf.Exp(-t * 90f); return (Mathf.Sin(2f * Mathf.PI * 1900f * t) * 0.7f + (Mathf.PerlinNoise(t * 4000f, 0.5f) - 0.5f) * 0.6f) * e; }
    private static float PopSfx(float t, float d) { float f = Mathf.Lerp(700f, 220f, t / d); return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 18f); }

    private static string SaveWav(string name, float seconds, Wave wave)
    {
        const int rate = 44100; int n = (int)(rate * seconds);
        using var ms = new MemoryStream(); using var bw = new BinaryWriter(ms);
        bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + n * 2); bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(n * 2);
        for (int i = 0; i < n; i++) bw.Write((short)(Mathf.Clamp(wave(i / (float)rate, seconds), -1f, 1f) * 30000));
        string path = $"{FxDir}/{name}.wav";
        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }
}
