using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Meshy hamam böceği modelini PestPlayer'a bağlar: Toon materyal, üretilmiş klipler, Animator Controller, hiyerarşi, PestAnimationManager.
// Menü: PestCo/Hamam Boecegi Modelini Bagla  (tekrar çalıştırılabilir)
public static class CockroachSetup
{
    private const string FbxPath = "Assets/_Project/Models/Meshy_AI_Cheerful_Cockroach_Ma_1012205350_texture_fbx/Meshy_AI_Cheerful_Cockroach_Ma_1012205350_texture.fbx";
    private const string TexPath = "Assets/_Project/Models/Meshy_AI_Cheerful_Cockroach_Ma_1012205350_texture_fbx/Meshy_AI_Cheerful_Cockroach_Ma_1012205350_texture.png";
    private const string Dir = "Assets/_Project/FX/Cockroach";
    private const float TargetLength = 0.12f;   // metre (haşere ~10 cm ölçeği)
    private const float ModelYaw = 0f;           // modelin ileri yönü +Z değilse buradan döndür
    private const float ModelPitch = -90f;      // -90 = iki ayak üstünde dik durur (baş yukarı, karın ileri); gövde ters dönerse +90 yap

    [MenuItem("PestCo/Hamam Boecegi Modelini Bagla")]
    public static void Run()
    {
        Directory.CreateDirectory(Dir);
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null) { Debug.LogError("FBX bulunamadı: " + FbxPath); return; }

        // --- Sahne: PestPlayer (rig kapalı olsa da bul)
        Transform pest = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == "PestPlayer" && t.gameObject.scene.IsValid()) { pest = t; break; }
        if (pest == null) { Debug.LogError("PestPlayer sahnede yok."); return; }
        Transform rig = pest.Find("Visual (placeholder)/Rig");
        if (rig == null) { Debug.LogError("Visual/Rig yok; önce 'Hasere Cizgi Film Kur' çalıştırılmalı."); return; }

        // --- Materyal
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/M_CockroachToon.mat");
        if (mat == null) { mat = new Material(Shader.Find("PestCo/Toon")); AssetDatabase.CreateAsset(mat, $"{Dir}/M_CockroachToon.mat"); }
        mat.shader = Shader.Find("PestCo/Toon");
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath));
        mat.SetColor("_BaseColor", Color.white); mat.SetColor("_ShadowColor", new Color(0.62f, 0.50f, 0.78f));
        mat.SetColor("_OutlineColor", new Color(0.04f, 0.025f, 0.04f)); mat.SetFloat("_OutlinePixels", 3.5f);
        mat.SetFloat("_GradStrength", 0f); mat.SetFloat("_BrushScale", 14f); mat.SetFloat("_BrushStrength", 0.06f);
        mat.SetFloat("_SpecStrength", 0.5f); mat.SetFloat("_SpecSize", 0.9f); mat.SetFloat("_RimStrength", 0.5f);
        EditorUtility.SetDirty(mat);

        // --- Eski kapsül + prosedürel yüz görünmez (Collider/CharacterController kalır)
        int disabled = 0;
        foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.transform.IsChildOf(rig.Find("CockroachRoot") ?? rig) && rig.Find("CockroachRoot") != null && r.transform.IsChildOf(rig.Find("CockroachRoot"))) continue;
            r.enabled = false; disabled++;
        }
        foreach (var f in rig.GetComponentsInChildren<MeshFilter>(true))
            if (rig.Find("CockroachRoot") == null || !f.transform.IsChildOf(rig.Find("CockroachRoot"))) { /* MeshFilter pasif bırakılamaz; renderer kapalı yeterli */ }

        // --- Hiyerarşi: Rig > CockroachRoot (Animator) > Model
        var oldRoot = rig.Find("CockroachRoot"); if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);
        var root = new GameObject("CockroachRoot").transform; root.SetParent(rig, false);
        root.localPosition = new Vector3(0, -0.05f, 0);   // Visual y=0.05 (orta pivot): zemin PestPlayer köküdür
        root.localRotation = Quaternion.identity; root.localScale = Vector3.one;

        // Hiyerarşi: CockroachRoot > Pivot (animasyon burada: salınım/eğilme/zıplama) > Model (sabit dik duruş)
        var pivot = new GameObject("Pivot").transform; pivot.SetParent(root, false);
        var model = (PrefabUtility.InstantiatePrefab(fbx, pivot) as GameObject).transform;
        model.name = "Model";
        float importScale = model.localScale.x;
        var mf = model.GetComponentInChildren<MeshFilter>();
        Bounds mb = mf.sharedMesh.bounds;
        float longest = Mathf.Max(mb.size.x, mb.size.z) * importScale;
        float s = importScale * (TargetLength / longest);
        model.localScale = Vector3.one * s;
        model.localRotation = Quaternion.Euler(ModelPitch, ModelYaw, 0);
        // dönmüş kutunun alt orta noktası zemine (pivot ayak hizası), x/z ortalı
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 w = model.localRotation * (corner * s);
            min = Vector3.Min(min, w); max = Vector3.Max(max, w);
        }
        model.localPosition = new Vector3(-(min.x + max.x) * 0.5f, -min.y, -(min.z + max.z) * 0.5f);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // --- Klipler + Controller
        // Animasyon Pivot'ta: eksenler dünya-hizalı (Z = ileri, Y = yukarı), model dik durduğu için eğilme/salınım doğru çalışır.
        var idle = MakeClip("Cockroach_Idle", pivot, 4f, true, (k, T) => Pose(k, T, 0.0008f, 0f, 0f, 3f, 2f, 0.02f, 0.03f, 0f, 2));
        var walk = MakeClip("Cockroach_Walk", pivot, 0.6f, true, (k, T) => Pose(k, T, 0.0f, 2f, 3f, 2f, 0f, 0f, 0f, 0f, 2));
        var run = MakeClip("Cockroach_Run", pivot, 0.32f, true, (k, T) => Pose(k, T, 0.012f, 14f, 6f, 6f, 0f, 0f, 0f, 0.04f, 2));
        var jump = MakeClip("Cockroach_Jump", pivot, 0.5f, false, (k, T) => PoseStatic(k, T));
        var controller = BuildController(idle, walk, run, jump);

        var animator = root.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // --- Manager
        var mgr = pest.GetComponent<PestAnimationManager>();
        if (mgr == null) mgr = pest.gameObject.AddComponent<PestAnimationManager>();
        var so = new SerializedObject(mgr); so.FindProperty("animator").objectReferenceValue = animator; so.ApplyModifiedProperties();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cockroach] Kurulum tamam. Kapatılan renderer: {disabled}, model ölçeği: {s:F3}");
    }

    // ---------- Klip üretimi (modelin taban pozunu referans alır)
    private delegate float Sampler(string prop, float t);

    private static AnimationClip MakeClip(string name, Transform target, float T, bool loop, System.Func<Transform, float, Sampler> pose)
    {
        string path = $"{Dir}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();
        clip.frameRate = 30;
        var sampler = pose(target, T);
        string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
        const int N = 16;
        foreach (var p in props)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= N; i++) { float t = T * i / N; curve.AddKey(new Keyframe(t, sampler(p, t))); }
            for (int i = 0; i < curve.length; i++) { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto); }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Pivot", typeof(Transform), p), curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; settings.loopBlendPositionY = loop; settings.loopBlendOrientation = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // Titreşimli döngü pozu: bob (Y), pitch (x), roll (z), yaw (y), nefes (ölçek), Z uzaması.
    private static Sampler Pose(Transform m, float T, float bob, float pitch, float roll, float yaw, float rollIdle, float breathXZ, float breathY, float stretchZ, int cycles)
    {
        Vector3 p0 = m.localPosition, e0 = m.localEulerAngles, s0 = m.localScale;
        return (prop, t) =>
        {
            float a = 2f * Mathf.PI * cycles * t / T;
            switch (prop)
            {
                case "m_LocalPosition.x": return p0.x;
                case "m_LocalPosition.y": return p0.y + bob * (0.5f + 0.5f * Mathf.Sin(a * 2f));
                case "m_LocalPosition.z": return p0.z;
                case "localEulerAnglesRaw.x": return e0.x + pitch;
                case "localEulerAnglesRaw.y": return e0.y + yaw * Mathf.Sin(a + 1.2f);
                case "localEulerAnglesRaw.z": return e0.z + (roll != 0f ? roll : rollIdle) * Mathf.Sin(a);
                case "m_LocalScale.x": return s0.x * (1f + breathXZ * Mathf.Sin(a));
                case "m_LocalScale.y": return s0.y * (1f + stretchZ + breathY * Mathf.Sin(a));
                default: return s0.z * (1f + breathXZ * Mathf.Sin(a));
            }
        };
    }

    // Zıplama: dikeyde uzama, burun yukarı.
    private static Sampler PoseStatic(Transform m, float T)
    {
        Vector3 p0 = m.localPosition, e0 = m.localEulerAngles, s0 = m.localScale;
        return (prop, t) =>
        {
            float u = Mathf.Clamp01(t / T);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, u * 3f));
            switch (prop)
            {
                case "m_LocalPosition.x": return p0.x;
                case "m_LocalPosition.y": return p0.y;
                case "m_LocalPosition.z": return p0.z;
                case "localEulerAnglesRaw.x": return e0.x - 10f * k;
                case "localEulerAnglesRaw.y": return e0.y;
                case "localEulerAnglesRaw.z": return e0.z;
                case "m_LocalScale.x": return s0.x * Mathf.Lerp(1f, 0.85f, k);
                case "m_LocalScale.y": return s0.y * Mathf.Lerp(1f, 1.25f, k);
                default: return s0.z * Mathf.Lerp(1f, 0.85f, k);
            }
        };
    }

    // ---------- Animator Controller
    private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip jump)
    {
        string path = $"{Dir}/AC_Cockroach.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        ac.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        ac.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ac.AddParameter("IsClimbing", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("AnimSpeed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        // IsGrounded varsayılanı true (yerde başla)
        var ps = ac.parameters; for (int i = 0; i < ps.Length; i++) if (ps[i].name == "IsGrounded" || ps[i].name == "AnimSpeed") { ps[i].defaultBool = ps[i].name == "IsGrounded"; ps[i].defaultFloat = 1f; }
        ac.parameters = ps;

        var sm = ac.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle", new Vector3(260, 40, 0)); sIdle.motion = idle;
        var sWalk = sm.AddState("Walk", new Vector3(520, -40, 0)); sWalk.motion = walk; sWalk.speedParameterActive = true; sWalk.speedParameter = "AnimSpeed";
        var sRun = sm.AddState("Run", new Vector3(520, 120, 0)); sRun.motion = run; sRun.speedParameterActive = true; sRun.speedParameter = "AnimSpeed";
        var sJump = sm.AddState("Jump", new Vector3(260, 220, 0)); sJump.motion = jump;
        sm.defaultState = sIdle;

        T(sIdle, sWalk, ("IsWalking", true)); T(sWalk, sIdle, ("IsWalking", false), ("IsRunning", false));
        T(sIdle, sRun, ("IsRunning", true)); T(sWalk, sRun, ("IsRunning", true));
        T(sRun, sWalk, ("IsRunning", false), ("IsWalking", true)); T(sRun, sIdle, ("IsRunning", false), ("IsWalking", false));

        var any = sm.AddAnyStateTransition(sJump); any.hasExitTime = false; any.duration = 0.05f; any.canTransitionToSelf = false;
        any.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        var anyTrig = sm.AddAnyStateTransition(sJump); anyTrig.hasExitTime = false; anyTrig.duration = 0.05f; anyTrig.canTransitionToSelf = false;
        anyTrig.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        var land = sJump.AddTransition(sIdle); land.hasExitTime = false; land.duration = 0.08f;
        land.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        return ac;
    }

    private static void T(AnimatorState from, AnimatorState to, params (string, bool)[] conds)
    {
        var t = from.AddTransition(to); t.hasExitTime = false; t.duration = 0.1f;
        foreach (var (n, v) in conds) t.AddCondition(v ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, n);
    }
}
