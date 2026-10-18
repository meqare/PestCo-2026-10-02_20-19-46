using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Blender'da riglenen hamam böceğini (Cockroach_Rigged.fbx) PestPlayer'a bağlar.
//  - FBX import ayarları (Generic, materyal yok)
//  - Klipleri CockroachRoot Animator'ının yollarına (Pivot/Model/...) kopyalar: Idle, Walk, Run, JumpUp, JumpAir, Land
//  - AC_CockroachRigged controller (PestAnimationManager ile aynı parametreler)
//  - Sahnede: eski Pivot -> Pivot_Old (kapalı), yeni Pivot/Model eklenir; boyut ve zemin eski modele eşitlenir
// Menü: PestCo/Hamam Boecegi Blender Animasyonlarini Bagla   (tekrar çalıştırılabilir)
//       PestCo/Hamam Boecegi Blender Animasyonlarini Geri Al (eski haline döndürür)
[InitializeOnLoad]
public static class CockroachRiggedSetup
{
    public const string FbxPath = "Assets/_Project/Models/CockroachRigged/Cockroach_Rigged.fbx";
    private const string Dir = "Assets/_Project/FX/CockroachRigged";
    private const string MatPath = "Assets/_Project/FX/Cockroach/M_CockroachToon.mat";
    private const string OldControllerPath = "Assets/_Project/FX/Cockroach/AC_Cockroach.controller";
    private const string ModelPath = "Pivot/Model";
    private const string Log = "[CockroachRigged] ";

    private static string DoneKey => "PestCo.CockroachRigged.Done." + Application.dataPath;

    static CockroachRiggedSetup()
    {
        EditorApplication.delayCall += AutoRun;
    }

    internal static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool(DoneKey, false)) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) == null) return;
        if (FindRoot() == null)
        {
            Debug.Log(Log + "Model hazır. PestPlayer'ın olduğu sahneyi (Assets/Scenes/SampleScene) açıp menüden " +
                      "'PestCo/Hamam Boecegi Blender Animasyonlarini Bagla' çalıştır.");
            return;
        }
        Run();
    }

    [MenuItem("PestCo/Hamam Boecegi Blender Animasyonlarini Bagla")]
    public static void Run()
    {
        var root = FindRoot();
        if (root == null) { Debug.LogError(Log + "Sahnede PestPlayer/.../CockroachRoot yok. SampleScene'i açıp tekrar dene."); return; }
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null) { Debug.LogError(Log + "FBX bulunamadı: " + FbxPath); return; }

        EnsureImportSettings();

        var src = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();
        AnimationClip Take(string key)
        {
            var c = src.FirstOrDefault(x => x.name == key || x.name.EndsWith("|" + key));
            if (c == null) Debug.LogError(Log + "FBX içinde klip yok: " + key + "  (bulunanlar: " + string.Join(", ", src.Select(x => x.name)) + ")");
            return c;
        }
        var idleSrc = Take("BugIdle"); var walkSrc = Take("BugWalk"); var runSrc = Take("BugRun"); var jumpSrc = Take("BugJumpInPlace");
        if (idleSrc == null || walkSrc == null || runSrc == null || jumpSrc == null) return;

        Directory.CreateDirectory(Dir);
        var idle = CopyClip(idleSrc, "Cockroach_Idle", 1, 97, true);
        var walk = CopyClip(walkSrc, "Cockroach_Walk", 1, 25, true);
        var run = CopyClip(runSrc, "Cockroach_Run", 1, 13, true);
        var jumpUp = CopyClip(jumpSrc, "Cockroach_JumpUp", 11, 21, false);
        var jumpAir = CopyClip(jumpSrc, "Cockroach_JumpAir", 21, 27, false);
        var land = CopyClip(jumpSrc, "Cockroach_Land", 29, 42, false);
        var controller = BuildController(idle, walk, run, jumpUp, jumpAir, land);

        if (!ApplyToScene(root, fbx, controller)) return;

        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(DoneKey, true);
        Debug.Log(Log + "Kurulum tamam: yeni model PestPlayer'a bağlandı, AC_CockroachRigged atandı. Geri almak için: PestCo/Hamam Boecegi Blender Animasyonlarini Geri Al");
    }

    [MenuItem("PestCo/Hamam Boecegi Blender Animasyonlarini Geri Al")]
    public static void Revert()
    {
        var root = FindRoot();
        if (root == null) { Debug.LogError(Log + "Sahnede CockroachRoot yok."); return; }
        var oldPivot = root.Find("Pivot_Old");
        if (oldPivot == null) { Debug.LogWarning(Log + "Pivot_Old yok, geri alınacak bir şey bulunamadı."); return; }
        var newPivot = root.Find("Pivot");
        if (newPivot != null) Object.DestroyImmediate(newPivot.gameObject);
        oldPivot.name = "Pivot";
        oldPivot.gameObject.SetActive(true);

        var anim = root.GetComponent<Animator>();
        var oldAc = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OldControllerPath);
        if (anim != null && oldAc != null) anim.runtimeAnimatorController = oldAc;
        SetSquashTarget(root, oldPivot);
        var spring = root.GetComponent<PestAntennaSpring>();
        if (spring != null) spring.enabled = true;

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
        EditorPrefs.SetBool(DoneKey, true); // otomatik tekrar kurulmasın
        Debug.Log(Log + "Eski hamam böceği modeline geri dönüldü.");
    }

    // ---------------------------------------------------------------- import

    private static void EnsureImportSettings()
    {
        var mi = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (mi == null) return;
        if (CockroachRiggedImport.Configure(mi)) mi.SaveAndReimport();
    }

    // ---------------------------------------------------------------- clips

    private static AnimationClip CopyClip(AnimationClip src, string name, int f0, int f1, bool loop)
    {
        var bindings = AnimationUtility.GetCurveBindings(src);
        float fps = src.frameRate > 0f ? src.frameRate : 24f;
        float tMin = float.MaxValue;
        foreach (var b in bindings)
        {
            var c = AnimationUtility.GetEditorCurve(src, b);
            if (c != null && c.length > 0) tMin = Mathf.Min(tMin, c.keys[0].time);
        }
        if (tMin == float.MaxValue) tMin = 0f;
        float t0 = tMin + (f0 - 1) / fps, t1 = tMin + (f1 - 1) / fps;
        float len = t1 - t0;

        var dst = new AnimationClip { name = name, frameRate = fps };
        foreach (var b in bindings)
        {
            var c = AnimationUtility.GetEditorCurve(src, b);
            if (c == null) continue;
            var keys = new List<Keyframe>();
            foreach (var k in c.keys)
            {
                if (k.time < t0 - 1e-3f || k.time > t1 + 1e-3f) continue;
                var nk = k; nk.time = k.time - t0; keys.Add(nk);
            }
            if (keys.Count == 0)
            {
                keys.Add(new Keyframe(0f, c.Evaluate(t0)));
                keys.Add(new Keyframe(len, c.Evaluate(t1)));
            }
            var nb = b;
            nb.path = string.IsNullOrEmpty(b.path) ? ModelPath : ModelPath + "/" + b.path;
            AnimationUtility.SetEditorCurve(dst, nb, new AnimationCurve(keys.ToArray()));
        }
        // Pivot ölçeği her karede 1 yazılır: PestSquashStretch bunun üstüne çarpar (birikmez).
        foreach (var p in new[] { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" })
            AnimationUtility.SetEditorCurve(dst, EditorCurveBinding.FloatCurve("Pivot", typeof(Transform), p), AnimationCurve.Constant(0f, len, 1f));

        var st = AnimationUtility.GetAnimationClipSettings(dst);
        st.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(dst, st);

        string path = Dir + "/" + name + ".anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(dst, path);
        return dst;
    }

    // ---------------------------------------------------------------- controller

    private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run,
        AnimationClip jumpUp, AnimationClip jumpAir, AnimationClip land)
    {
        string path = Dir + "/AC_CockroachRigged.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        ac.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        ac.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ac.AddParameter("IsClimbing", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("AnimSpeed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        var ps = ac.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].name == "IsGrounded") ps[i].defaultBool = true;
            if (ps[i].name == "AnimSpeed") ps[i].defaultFloat = 1f;
        }
        ac.parameters = ps;

        var sm = ac.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle", new Vector3(260, 40, 0)); sIdle.motion = idle;
        var sWalk = sm.AddState("Walk", new Vector3(520, -60, 0)); sWalk.motion = walk;
        sWalk.speedParameterActive = true; sWalk.speedParameter = "AnimSpeed";
        var sRun = sm.AddState("Run", new Vector3(520, 140, 0)); sRun.motion = run;
        sRun.speedParameterActive = true; sRun.speedParameter = "AnimSpeed";
        var sUp = sm.AddState("JumpUp", new Vector3(260, 260, 0)); sUp.motion = jumpUp;
        var sAir = sm.AddState("JumpAir", new Vector3(520, 300, 0)); sAir.motion = jumpAir;
        var sLand = sm.AddState("Land", new Vector3(780, 220, 0)); sLand.motion = land;
        sm.defaultState = sIdle;

        // yer hareketi
        T(sIdle, sWalk, null, 0.12f, ("IsWalking", true));
        T(sIdle, sRun, null, 0.12f, ("IsRunning", true));
        T(sWalk, sRun, null, 0.15f, ("IsRunning", true));
        T(sWalk, sIdle, null, 0.15f, ("IsWalking", false), ("IsRunning", false));
        T(sRun, sWalk, null, 0.15f, ("IsRunning", false), ("IsWalking", true));
        T(sRun, sIdle, null, 0.15f, ("IsRunning", false), ("IsWalking", false));

        // zıplama: tetikte kalkış -> havada -> iniş
        var any = sm.AddAnyStateTransition(sUp);
        any.hasExitTime = false; any.duration = 0.04f; any.canTransitionToSelf = false;
        any.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        T(sUp, sAir, 1f, 0.05f);
        T(sUp, sLand, 0.35f, 0.04f, ("IsGrounded", true));
        T(sAir, sLand, null, 0.04f, ("IsGrounded", true));
        // kenardan düşme
        T(sIdle, sAir, null, 0.1f, ("IsGrounded", false));
        T(sWalk, sAir, null, 0.1f, ("IsGrounded", false));
        T(sRun, sAir, null, 0.1f, ("IsGrounded", false));
        // iniş sonrası
        T(sLand, sRun, 0.3f, 0.1f, ("IsRunning", true));
        T(sLand, sWalk, 0.45f, 0.12f, ("IsWalking", true));
        T(sLand, sIdle, 0.9f, 0.12f);

        EditorUtility.SetDirty(ac);
        return ac;
    }

    private static void T(AnimatorState from, AnimatorState to, float? exitTime, float duration, params (string name, bool value)[] conds)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = exitTime.HasValue;
        if (exitTime.HasValue) t.exitTime = exitTime.Value;
        t.duration = duration;
        foreach (var (n, v) in conds) t.AddCondition(v ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, n);
    }

    // ---------------------------------------------------------------- scene

    private static Transform FindRoot()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name != "CockroachRoot" || EditorUtility.IsPersistent(t) || !t.gameObject.scene.IsValid()) continue;
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == "PestPlayer") return t;
        }
        return null;
    }

    private static bool ApplyToScene(Transform root, GameObject fbx, RuntimeAnimatorController controller)
    {
        var oldPivot = root.Find("Pivot_Old");
        var current = root.Find("Pivot");
        if (oldPivot == null && current != null)
        {
            oldPivot = current; current = null;
            oldPivot.name = "Pivot_Old";
        }
        if (current != null) Object.DestroyImmediate(current.gameObject); // önceki kurulumu yenile

        float targetH = 0.12f, targetBottom = 0f; Vector3 targetC = Vector3.zero;
        if (oldPivot != null)
        {
            bool wasActive = oldPivot.gameObject.activeSelf;
            if (LocalBounds(root, oldPivot, out var ob) && ob.size.y > 1e-4f)
            {
                targetH = ob.size.y; targetBottom = ob.min.y; targetC = ob.center;
            }
            oldPivot.gameObject.SetActive(false);
            if (wasActive) Debug.Log(Log + "Eski model 'Pivot_Old' olarak kapatıldı (silinmedi).");
        }

        var pivot = new GameObject("Pivot").transform;
        pivot.SetParent(root, false);
        pivot.SetSiblingIndex(0);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, pivot);
        model.name = "Model";
        var mt = model.transform;
        mt.localPosition = Vector3.zero; mt.localRotation = Quaternion.identity; mt.localScale = Vector3.one;

        // Yön kontrolü: antenler ileri (+Z) bakmalı
        var a1 = FindDeep(mt, "Antenna1_L"); var a2 = FindDeep(mt, "Antenna2_L");
        var hips = FindDeep(mt, "Hips"); var head = FindDeep(mt, "Head");
        if (a1 != null && a2 != null)
        {
            Vector3 d = pivot.InverseTransformDirection(a2.position - a1.position);
            if (d.z < 0f) { mt.localRotation = Quaternion.Euler(0f, 180f, 0f); Debug.Log(Log + "Model 180° döndürüldü (ileri = +Z)."); }
        }
        if (hips != null && head != null && pivot.InverseTransformPoint(head.position).y < pivot.InverseTransformPoint(hips.position).y)
            Debug.LogWarning(Log + "Model yatık görünüyor (baş kalçanın altında). FBX eksen ayarını kontrol et.");

        if (!LocalBounds(pivot, mt, out var nb) || nb.size.y < 1e-5f)
        {
            Debug.LogError(Log + "Yeni modelin boyutu ölçülemedi.");
            return false;
        }
        float s = targetH / nb.size.y;
        mt.localScale = Vector3.one * s;
        LocalBounds(pivot, mt, out nb);
        mt.localPosition += new Vector3(targetC.x - nb.center.x, targetBottom - nb.min.y, targetC.z - nb.center.z);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (mat != null) smr.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, smr.sharedMaterials.Length)).ToArray();
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.updateWhenOffscreen = true;
        }
        var childAnim = model.GetComponent<Animator>();
        if (childAnim != null) childAnim.enabled = false;

        var anim = root.GetComponent<Animator>();
        if (anim == null) anim = root.gameObject.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.avatar = null;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        SetSquashTarget(root, pivot);
        var spring = root.GetComponent<PestAntennaSpring>();
        if (spring != null) spring.enabled = false; // antenler klipte yay hareketiyle geliyor

        var mgr = root.GetComponentInParent<PestAnimationManager>(true);
        if (mgr != null)
        {
            var so = new SerializedObject(mgr);
            var prop = so.FindProperty("animator");
            if (prop != null) { prop.objectReferenceValue = anim; so.ApplyModifiedProperties(); }
        }
        else Debug.LogWarning(Log + "PestAnimationManager bulunamadı; Animator parametrelerini kimse sürmeyecek.");

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
        Debug.Log(Log + $"Model ölçeği {s:F4}, boy {targetH:F3} m. Sahne kaydedildi: {root.gameObject.scene.path}");
        return true;
    }

    private static void SetSquashTarget(Transform root, Transform target)
    {
        var sq = root.GetComponent<PestSquashStretch>();
        if (sq == null) return;
        var so = new SerializedObject(sq);
        var prop = so.FindProperty("target");
        if (prop != null) { prop.objectReferenceValue = target; so.ApplyModifiedProperties(); }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // Renderer'ların mesh sınırlarını 'space' uzayında toplar (bind/dinlenme pozu).
    private static bool LocalBounds(Transform space, Transform under, out Bounds result)
    {
        result = default; bool any = false;
        foreach (var r in under.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            Mesh m = null;
            if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh;
            else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
            if (m == null) continue;
            var mb = m.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = space.InverseTransformPoint(r.transform.TransformPoint(c));
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; } else result.Encapsulate(p);
            }
        }
        return any;
    }
}

// FBX import ayarları + import bitince otomatik kurulum
public class CockroachRiggedImport : AssetPostprocessor
{
    public static bool Configure(ModelImporter mi)
    {
        bool dirty = false;
        if (mi.animationType != ModelImporterAnimationType.Generic) { mi.animationType = ModelImporterAnimationType.Generic; dirty = true; }
        if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
        if (!mi.importAnimation) { mi.importAnimation = true; dirty = true; }
        if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
        if (mi.importCameras) { mi.importCameras = false; dirty = true; }
        if (mi.importLights) { mi.importLights = false; dirty = true; }
        if (mi.optimizeGameObjects) { mi.optimizeGameObjects = false; dirty = true; }
        return dirty;
    }

    private void OnPreprocessModel()
    {
        if (assetPath != CockroachRiggedSetup.FbxPath) return;
        Configure((ModelImporter)assetImporter);
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Contains(CockroachRiggedSetup.FbxPath))
            EditorApplication.delayCall += CockroachRiggedSetup.AutoRun;
    }
}
