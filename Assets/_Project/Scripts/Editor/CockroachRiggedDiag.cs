using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// GECICI TESHIS: hamam boecegi klip/Animator davranisini edit modda olcer. Sonuc: <proje>/cockroach_diag.txt
[InitializeOnLoad]
public static class CockroachRiggedDiag
{
    private static readonly string Req = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "cockroach_diag.request");
    private static readonly string Out = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "cockroach_diag.txt");
    private static StringBuilder sb;

    static CockroachRiggedDiag() { EditorApplication.delayCall += Auto; }

    private static void Auto()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(Req)) return;
        File.Delete(Req);
        try { Run(); }
        catch (System.Exception e) { File.WriteAllText(Out, "EXCEPTION: " + e); Debug.LogError("[CockroachDiag] " + e); }
    }

    private static void P(string s) { sb.AppendLine(s); }

    [MenuItem("PestCo/Hamam Boecegi Teshis")]
    public static void Run()
    {
        sb = new StringBuilder();
        Transform root = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name != "CockroachRoot" || EditorUtility.IsPersistent(t) || !t.gameObject.scene.IsValid()) continue;
            root = t; break;
        }
        if (root == null) { File.WriteAllText(Out, "CockroachRoot sahnede yok"); return; }

        var clone = Object.Instantiate(root.gameObject);
        clone.name = "DiagClone";
        var ps = EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(clone, ps);
        clone.transform.position = Vector3.zero; clone.transform.rotation = Quaternion.identity;
        foreach (var mb in clone.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(mb);

        var animator = clone.GetComponent<Animator>();
        var pivot = clone.transform.Find("Pivot");
        P("Animator controller: " + (animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NULL"));
        P("Pivot var mi: " + (pivot != null) + "  localScale=" + (pivot != null ? pivot.localScale.ToString("F4") : "-"));
        if (pivot == null) { File.WriteAllText(Out, sb.ToString()); return; }
        var smrs = pivot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        P("SkinnedMeshRenderer sayisi: " + smrs.Length + (smrs.Length > 0 ? "  bones=" + smrs[0].bones.Length : ""));

        animator.Rebind();
        animator.SetBool("IsGrounded", true);
        animator.Update(0f);

        var allT = pivot.GetComponentsInChildren<Transform>(true);
        var restScale = allT.ToDictionary(t => t, t => t.localScale);
        var restPos = allT.ToDictionary(t => t, t => t.localPosition);
        P("");
        P("=== BASELINE (Animator.Rebind sonrasi) ===");
        Sample("baseline", 0f, animator, pivot, smrs);
        Bounds b0 = Measure(pivot, smrs);
        P("  baseline mesh boyutu: " + b0.size.ToString("F4") + "  y araligi [" + b0.min.y.ToString("F4") + ", " + b0.max.y.ToString("F4") + "]");

        // --- A) Animator Pivot olcegini her karede geri yaziyor mu?
        P("");
        P("=== A) Animator Pivot olcegini geri yaziyor mu? ===");
        animator.Update(0.1f);
        pivot.localScale = new Vector3(1f, 2f, 1f);
        P("  elle (1,2,1) yazildi; Update(0.016) sonrasi Pivot.localScale = " + Step(animator, pivot, 0.016f).ToString("F4"));
        pivot.localScale = new Vector3(1.3f, 0.6f, 1.3f);
        P("  elle (1.3,0.6,1.3) yazildi; Update(0.016) sonrasi Pivot.localScale = " + Step(animator, pivot, 0.016f).ToString("F4"));
        pivot.localScale = Vector3.one;

        // --- B) Ziplama
        P("");
        P("=== B) ZIPLAMA (3 kez art arda) ===");
        animator.Rebind(); animator.SetBool("IsGrounded", true); animator.Update(0f);
        float t0 = 0f;
        for (int i = 0; i < 12; i++) { animator.Update(1f / 60f); t0 += 1f / 60f; }
        for (int jump = 1; jump <= 3; jump++)
        {
            P("  -- ziplama #" + jump);
            animator.SetTrigger("Jump");
            float dt = 1f / 60f;
            float tmin = 99, tmax = 0;
            for (int s = 0; s <= 100; s++)
            {
                float t = s * dt;
                bool grounded = !(t > 0.08f && t < 0.55f);
                animator.SetBool("IsGrounded", grounded);
                animator.Update(dt);
                if (s % 6 == 0) Sample("j" + jump, t, animator, pivot, smrs);
            }
            for (int i = 0; i < 30; i++) animator.Update(1f / 60f);
        }

        // --- C) Yuruyus / Kosma
        foreach (var mode in new[] { "IsWalking", "IsRunning" })
        {
            P("");
            P("=== C) " + mode + " (1 sn) ===");
            animator.Rebind(); animator.SetBool("IsGrounded", true); animator.SetBool("IsWalking", false); animator.SetBool("IsRunning", false); animator.Update(0f);
            for (int i = 0; i < 12; i++) animator.Update(1f / 60f);
            animator.SetBool(mode, true);
            for (int s = 0; s <= 60; s++)
            {
                animator.Update(1f / 60f);
                if (s % 6 == 0) Sample(mode, s / 60f, animator, pivot, smrs);
            }
        }

        // --- D) kemik olcek/konum sapmasi
        P("");
        P("=== D) Ziplama sirasinda en cok sapan kemikler (baseline'a gore) ===");
        animator.Rebind(); animator.SetBool("IsGrounded", true); animator.Update(0f);
        for (int i = 0; i < 12; i++) animator.Update(1f / 60f);
        animator.SetTrigger("Jump");
        var worstS = new Dictionary<string, float>(); var worstP = new Dictionary<string, float>();
        for (int s = 0; s <= 100; s++)
        {
            float t = s / 60f;
            animator.SetBool("IsGrounded", !(t > 0.08f && t < 0.55f));
            animator.Update(1f / 60f);
            foreach (var tr in allT)
            {
                if (tr == pivot) continue;
                Vector3 rs = restScale[tr];
                float ds = Mathf.Max(Mathf.Abs(tr.localScale.x / Mathf.Max(1e-6f, rs.x) - 1f), Mathf.Abs(tr.localScale.y / Mathf.Max(1e-6f, rs.y) - 1f), Mathf.Abs(tr.localScale.z / Mathf.Max(1e-6f, rs.z) - 1f));
                float dp = (tr.localPosition - restPos[tr]).magnitude;
                string path = tr.name;
                if (!worstS.ContainsKey(path) || ds > worstS[path]) worstS[path] = ds;
                if (!worstP.ContainsKey(path) || dp > worstP[path]) worstP[path] = dp;
            }
        }
        foreach (var kv in worstS.OrderByDescending(k => k.Value).Take(8))
            P("  olcek sapmasi " + kv.Key + ": " + (kv.Value * 100f).ToString("F1") + " %   konum sapmasi: " + worstP[kv.Key].ToString("F4"));

        Object.DestroyImmediate(clone);
        EditorSceneManager.ClosePreviewScene(ps);
        File.WriteAllText(Out, sb.ToString(), Encoding.UTF8);
        Debug.Log("[CockroachDiag] tamam -> " + Out);
    }

    private static Vector3 Step(Animator a, Transform pivot, float dt) { a.Update(dt); return pivot.localScale; }

    private static Bounds Measure(Transform pivot, SkinnedMeshRenderer[] smrs)
    {
        Bounds b = new Bounds(); bool any = false;
        var tmp = new Mesh();
        foreach (var smr in smrs)
        {
            smr.BakeMesh(tmp, false);
            var vs = tmp.vertices;
            for (int i = 0; i < vs.Length; i += 5)
            {
                Vector3 p = pivot.InverseTransformPoint(smr.transform.TransformPoint(vs[i]));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        Object.DestroyImmediate(tmp);
        return b;
    }

    private static void Sample(string tag, float t, Animator a, Transform pivot, SkinnedMeshRenderer[] smrs)
    {
        var info = a.GetCurrentAnimatorClipInfo(0);
        string clip = info.Length > 0 ? info[0].clip.name : "-";
        var tr = a.GetNextAnimatorClipInfo(0);
        if (tr.Length > 0) clip += ">" + tr[0].clip.name;
        Bounds b = Measure(pivot, smrs);
        P("  " + tag + " t=" + t.ToString("F2") + " " + clip.PadRight(30) + " pivotScale=" + pivot.localScale.ToString("F3") +
          " meshSize=" + b.size.ToString("F3") + " y=[" + b.min.y.ToString("F3") + "," + b.max.y.ToString("F3") + "]");
    }
}
