using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Hamam böceğine iskelet (bone) sistemi kurar: tek parça mesh'i SkinnedMeshRenderer'a çevirir,
// kemikleri ve ağırlıkları üretir, klip'lere tam vücut animasyonu ekler.
// Menü: PestCo/Hamam Boecegi Iskeleti Kur  (önce "Hamam Boecegi Modelini Bagla" çalışmış olmalı; tekrar çalıştırılabilir)
//
// Kemik hiyerarşisi (Pivot altında; antenlerde ikinci eklem AntL2/AntR2 var):
//   Hips > Spine > Chest > Head > AntL, AntR
//                  Chest > ArmL > ForearmL      Chest > ArmR > ForearmR
//   Hips > ThighL > ShinL > FootL                Hips > ThighR > ShinR > FootR
public static class CockroachRigSetup
{
    private const string Dir = "Assets/_Project/FX/Cockroach";

    // Kemik indeksleri
    private const int Hips = 0, Spine = 1, Chest = 2, Head = 3, AntL = 4, AntR = 5,
        ArmL = 6, ForeL = 7, ArmR = 8, ForeR = 9,
        ThL = 10, ShL = 11, FtL = 12, ThR = 13, ShR = 14, FtR = 15, Ant2L = 16, Ant2R = 17;

    private static readonly string[] Paths =
    {
        "Hips", "Hips/Spine", "Hips/Spine/Chest", "Hips/Spine/Chest/Head", "Hips/Spine/Chest/Head/AntL", "Hips/Spine/Chest/Head/AntR",
        "Hips/Spine/Chest/ArmL", "Hips/Spine/Chest/ArmL/ForearmL", "Hips/Spine/Chest/ArmR", "Hips/Spine/Chest/ArmR/ForearmR",
        "Hips/ThighL", "Hips/ThighL/ShinL", "Hips/ThighL/ShinL/FootL", "Hips/ThighR", "Hips/ThighR/ShinR", "Hips/ThighR/ShinR/FootR",
        "Hips/Spine/Chest/Head/AntL/AntL2", "Hips/Spine/Chest/Head/AntR/AntR2",
    };

    [MenuItem("PestCo/Hamam Boecegi Iskeleti Kur")]
    public static void Run()
    {
        var pivot = Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(t => t.name == "Pivot" && t.parent != null && t.parent.name == "CockroachRoot" && t.gameObject.scene.IsValid());
        if (pivot == null) { Debug.LogError("CockroachRoot/Pivot yok. Önce 'Hamam Boecegi Modelini Bagla' çalıştır."); return; }
        var model = pivot.Find("Model");
        if (model == null) { Debug.LogError("Pivot/Model yok (zaten iskeletli olabilir). Önce 'Hamam Boecegi Modelini Bagla' çalıştır."); return; }

        Material mat = model.GetComponent<MeshRenderer>().sharedMaterial;
        Mesh src = model.GetComponent<MeshFilter>().sharedMesh;

        foreach (var n in new[] { "Hips", "Body" })
        {
            var old = pivot.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // --- Gövde objesi: Model ile aynı yerde (sabit dik duruş)
        var body = new GameObject("Body").transform;
        body.SetParent(pivot, false);
        body.localPosition = model.localPosition;
        body.localRotation = model.localRotation;
        body.localScale = model.localScale;

        // --- Köşe noktaları Pivot uzayında (Y yukarı, Z ileri, X yan)
        Vector3[] verts = src.vertices;
        var p = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) p[i] = pivot.InverseTransformPoint(body.TransformPoint(verts[i]));

        // --- Ölçümler
        var legPts = p.Where(v => v.y < 0.03f && v.y > 0.008f).ToArray();
        float legX = Mathf.Max(0.002f, Average(legPts.Select(v => Mathf.Abs(v.x))));
        float legZ = Average(legPts.Select(v => v.z));
        var armPts = p.Where(v => Mathf.Abs(v.x) > 0.012f && v.y > 0.040f && v.y < 0.072f && v.z < 0.006f).ToArray();
        float armX = armPts.Length > 20 ? Average(armPts.Select(v => Mathf.Abs(v.x))) : 0.014f;
        float armZ = armPts.Length > 20 ? Average(armPts.Select(v => v.z)) : 0f;
        var antPts = p.Where(v => v.y > 0.095f && Mathf.Abs(v.x) > 0.004f && Mathf.Abs(v.x) < 0.012f).OrderBy(v => v.y).ToArray();
        Vector3 antBase = antPts.Length > 0 ? new Vector3(Average(antPts.Take(Mathf.Max(1, antPts.Length / 10)).Select(v => Mathf.Abs(v.x))), antPts[0].y, antPts[0].z) : new Vector3(0.005f, 0.098f, 0f);

        // --- Kemikler (konumlar Pivot uzayında)
        // anten ikinci eklem: anten yüksekliğinin ortası
        float antTop = antPts.Length > 0 ? antPts[antPts.Length - 1].y : antBase.y + 0.02f;
        if (antTop - antBase.y < 0.008f) antTop = antBase.y + 0.02f;
        float antMid = Mathf.Lerp(antBase.y, antTop, 0.5f);
        var midPts = antPts.Where(v => Mathf.Abs(v.y - antMid) < 0.004f).ToArray();
        float antMidX = midPts.Length > 0 ? Average(midPts.Select(v => Mathf.Abs(v.x))) : antBase.x;
        float antMidZ = midPts.Length > 0 ? Average(midPts.Select(v => v.z)) : antBase.z;

        var bones = new Transform[18];
        bones[Hips] = Bone("Hips", pivot, pivot, new Vector3(0, 0.040f, 0));
        bones[Spine] = Bone("Spine", bones[Hips], pivot, new Vector3(0, 0.052f, 0));
        bones[Chest] = Bone("Chest", bones[Spine], pivot, new Vector3(0, 0.066f, 0));
        bones[Head] = Bone("Head", bones[Chest], pivot, new Vector3(0, 0.088f, 0));
        bones[AntL] = Bone("AntL", bones[Head], pivot, antBase);
        bones[AntR] = Bone("AntR", bones[Head], pivot, new Vector3(-antBase.x, antBase.y, antBase.z));
        bones[Ant2L] = Bone("AntL2", bones[AntL], pivot, new Vector3(antMidX, antMid, antMidZ));
        bones[Ant2R] = Bone("AntR2", bones[AntR], pivot, new Vector3(-antMidX, antMid, antMidZ));
        bones[ArmL] = Bone("ArmL", bones[Chest], pivot, new Vector3(armX, 0.074f, armZ));
        bones[ForeL] = Bone("ForearmL", bones[ArmL], pivot, new Vector3(armX, 0.056f, armZ));
        bones[ArmR] = Bone("ArmR", bones[Chest], pivot, new Vector3(-armX, 0.074f, armZ));
        bones[ForeR] = Bone("ForearmR", bones[ArmR], pivot, new Vector3(-armX, 0.056f, armZ));
        bones[ThL] = Bone("ThighL", bones[Hips], pivot, new Vector3(legX, 0.042f, legZ));
        bones[ShL] = Bone("ShinL", bones[ThL], pivot, new Vector3(legX, 0.026f, legZ));
        bones[FtL] = Bone("FootL", bones[ShL], pivot, new Vector3(legX, 0.008f, legZ));
        bones[ThR] = Bone("ThighR", bones[Hips], pivot, new Vector3(-legX, 0.042f, legZ));
        bones[ShR] = Bone("ShinR", bones[ThR], pivot, new Vector3(-legX, 0.026f, legZ));
        bones[FtR] = Bone("FootR", bones[ShR], pivot, new Vector3(-legX, 0.008f, legZ));

        // --- Ağırlıklar
        var weights = new BoneWeight[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = p[i];
            var w = new float[bones.Length];
            float ax = Mathf.Abs(v.x);
            bool left = v.x >= 0f;

            float antW = Smooth(0.092f, 0.100f, v.y) * Smooth(0.009f, 0.015f, ax);
            float legW = Smooth(0.046f, 0.036f, v.y) * (1f - Smooth(0.0075f, 0.011f, ax));
            legW *= 1f - antW;
            // kol: gövdenin yanında, önde/ortada (kabuk arkada kalır), omuz ile el arası
            float armW = Smooth(0.011f, 0.0135f, ax) * Smooth(0.078f, 0.068f, v.y) * Smooth(0.036f, 0.044f, v.y) * Smooth(0.009f, 0.003f, v.z);
            armW *= (1f - antW) * (1f - legW);
            float rest = Mathf.Max(0f, 1f - antW - legW - armW);

            if (antW > 0f)
            {
                float toTip = Smooth(antMid - 0.003f, antMid + 0.003f, v.y);
                w[left ? AntL : AntR] += antW * (1f - toTip);
                w[left ? Ant2L : Ant2R] += antW * toTip;
            }
            if (legW > 0f)
            {
                float toShin = Smooth(0.034f, 0.026f, v.y);
                float toFoot = Smooth(0.016f, 0.008f, v.y);
                w[left ? ThL : ThR] += legW * (1f - toShin);
                w[left ? ShL : ShR] += legW * toShin * (1f - toFoot);
                w[left ? FtL : FtR] += legW * toShin * toFoot;
            }
            if (armW > 0f)
            {
                float toFore = Smooth(0.064f, 0.054f, v.y);
                w[left ? ArmL : ArmR] += armW * (1f - toFore);
                w[left ? ForeL : ForeR] += armW * toFore;
            }
            if (rest > 0f)
            {
                float headW = Smooth(0.078f, 0.090f, v.y);
                float chestW = Smooth(0.056f, 0.068f, v.y) * (1f - headW);
                float spineW = Smooth(0.040f, 0.050f, v.y) * (1f - headW - chestW);
                float hipsW = 1f - headW - chestW - spineW;
                w[Head] += rest * headW; w[Chest] += rest * chestW; w[Spine] += rest * spineW; w[Hips] += rest * hipsW;
            }
            weights[i] = Top4(w);
        }

        var mesh = Object.Instantiate(src);
        mesh.name = "CockroachSkinned";
        mesh.boneWeights = weights;
        var bind = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * body.localToWorldMatrix;
        mesh.bindposes = bind;
        mesh.RecalculateBounds();
        Directory.CreateDirectory(Dir);
        string meshPath = $"{Dir}/CockroachSkinned.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var smr = body.gameObject.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.sharedMaterial = mat;
        smr.bones = bones;
        smr.rootBone = bones[Hips];
        smr.updateWhenOffscreen = true;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Object.DestroyImmediate(model.gameObject);

        // --- Klip'lere kemik eğrileri
        var idle = Load("Cockroach_Idle"); var walk = Load("Cockroach_Walk"); var run = Load("Cockroach_Run"); var jump = Load("Cockroach_Jump");
        Vector3 hip0 = bones[Hips].localPosition;
        BuildIdle(idle, hip0);
        BuildGait(walk, hip0, 0.6f, swing: 32f, knee: 48f, armSwing: 30f, elbow: 25f, lean: 4f, twist: 8f, bounce: 0.004f, sway: 0.0025f, antBack: 6f, antWave: 8f, squash: 0.03f);
        BuildGait(run, hip0, 0.32f, swing: 62f, knee: 95f, armSwing: 75f, elbow: 60f, lean: 20f, twist: 16f, bounce: 0.016f, sway: 0.004f, antBack: 40f, antWave: 16f, squash: 0.10f, crouch: 24f, elbowBase: 50f);
        BuildJump(jump, hip0);

        foreach (var c in new[] { idle, walk, run, jump }) EditorUtility.SetDirty(c);

        // Ayrı açılıp kapatılabilir ikincil hareket scriptleri
        var rootT = pivot.parent;
        if (rootT.GetComponent<PestAntennaSpring>() == null) rootT.gameObject.AddComponent<PestAntennaSpring>();
        if (rootT.GetComponent<PestSquashStretch>() == null) rootT.gameObject.AddComponent<PestSquashStretch>();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cockroach] İskelet kuruldu: {bones.Length} kemik, {verts.Length} köşe. legX={legX:F4} legZ={legZ:F4} armX={armX:F4} armZ={armZ:F4} (kol köşe sayısı {armPts.Length}) antBase={antBase}");
    }

    // ---------- Klip tanımları
    private static float Ph(float t, float T) => 2f * Mathf.PI * t / T;

    // Bekleme: nefes alma, kafa eğme, anten ve kol sallanması.
    private static void BuildIdle(AnimationClip c, Vector3 hip0)
    {
        const float T = 4f;   // 2 sn'lik nefes döngüsü x2; ikinci yarıda ara sıra bacak kıpırdaması
        Pos(c, Paths[Hips], T, t => hip0 + new Vector3(0, 0.0007f * Mathf.Sin(Ph(t, 2f)), 0));
        Rot(c, Paths[Hips], T, t => new Vector3(0, 3f * Mathf.Sin(Ph(t, 2f)), 1.5f * Mathf.Sin(Ph(t, 2f))));
        Rot(c, Paths[Spine], T, t => new Vector3(1.5f * Mathf.Sin(Ph(t, 2f) + 0.5f), 0, 0));
        Scl(c, Paths[Spine], T, t => new Vector3(1f + 0.02f * Mathf.Sin(Ph(t, 2f)), 1f + 0.035f * Mathf.Sin(Ph(t, 2f)), 1f + 0.02f * Mathf.Sin(Ph(t, 2f))));
        Rot(c, Paths[Chest], T, t => new Vector3(-2f * Mathf.Sin(Ph(t, 2f) + 0.5f), -4f * Mathf.Sin(Ph(t, 2f)), 0));
        Rot(c, Paths[Head], T, t => new Vector3(4f * Mathf.Sin(Ph(t, 2f) * 2f), 8f * Mathf.Sin(Ph(t, 2f)), 4f * Mathf.Sin(Ph(t, 2f) + 1f)));
        Rot(c, Paths[AntL], T, t => new Vector3(8f * Mathf.Sin(Ph(t, 2f) * 2f + 0.6f), 0, 10f * Mathf.Sin(Ph(t, 2f) * 2f)));
        Rot(c, Paths[AntR], T, t => new Vector3(8f * Mathf.Sin(Ph(t, 2f) * 2f + 1.2f), 0, -10f * Mathf.Sin(Ph(t, 2f) * 2f + 0.5f)));
        Rot(c, Paths[ArmL], T, t => new Vector3(5f * Mathf.Sin(Ph(t, 2f) + 0.3f), 0, 6f + 3f * Mathf.Sin(Ph(t, 2f))));
        Rot(c, Paths[ArmR], T, t => new Vector3(5f * Mathf.Sin(Ph(t, 2f) + 0.3f), 0, -6f - 3f * Mathf.Sin(Ph(t, 2f))));
        Rot(c, Paths[ForeL], T, t => new Vector3(-8f - 3f * Mathf.Sin(Ph(t, 2f)), 0, 0));
        Rot(c, Paths[ForeR], T, t => new Vector3(-8f - 3f * Mathf.Sin(Ph(t, 2f)), 0, 0));
        // Antenin ikinci eklemi: kökü gecikmeli takip eder (follow-through)
        Rot(c, Paths[Ant2L], T, t => new Vector3(10f * Mathf.Sin(Ph(t, 2f) * 2f + 1.4f), 0, 14f * Mathf.Sin(Ph(t, 2f) * 2f + 0.8f)));
        Rot(c, Paths[Ant2R], T, t => new Vector3(10f * Mathf.Sin(Ph(t, 2f) * 2f + 2.0f), 0, -14f * Mathf.Sin(Ph(t, 2f) * 2f + 1.3f)));
        // Ara sıra kıpırdama: 2.6-3.1 sn'de sol ayak hafif kalkıp iner, 3.4-3.8'de sağ ayak (anticipation: kısa ters hamle + overshoot)
        float Tap(float t, float t0, float len)
        {
            float u = (t - t0) / len;
            if (u < 0f || u > 1f) return 0f;
            return Mathf.Sin(u * Mathf.PI) * (u < 0.25f ? -0.35f : 1f) * (1f + 0.15f * Mathf.Sin(u * Mathf.PI * 3f));
        }
        Rot(c, Paths[ThL], T, t => new Vector3(-14f * Tap(t, 2.6f, 0.5f), 0, 0));
        Rot(c, Paths[ShL], T, t => new Vector3(26f * Tap(t, 2.6f, 0.5f), 0, 0));
        Rot(c, Paths[FtL], T, t => new Vector3(-10f * Tap(t, 2.6f, 0.5f), 0, 0));
        Rot(c, Paths[ThR], T, t => new Vector3(-12f * Tap(t, 3.4f, 0.45f), 0, 0));
        Rot(c, Paths[ShR], T, t => new Vector3(22f * Tap(t, 3.4f, 0.45f), 0, 0));
        Rot(c, Paths[FtR], T, t => new Vector3(-8f * Tap(t, 3.4f, 0.45f), 0, 0));
    }

    // Yürüme / koşma döngüsü. Faz: sol bacak ileri iken sağ kol ileri (çapraz).
    private static void BuildGait(AnimationClip c, Vector3 hip0, float T, float swing, float knee, float armSwing, float elbow, float lean, float twist, float bounce, float sway, float antBack, float antWave, float squash, float crouch = 0f, float elbowBase = 0f)
    {
        float S(float t, float off = 0f) => Mathf.Sin(Ph(t, T) + off);
        float Lift(float t, float off) => Mathf.Max(0f, Mathf.Sin(Ph(t, T) + off));

        Pos(c, Paths[Hips], T, t => hip0 + new Vector3(sway * S(t), -crouch * 0.00018f + bounce * Mathf.Abs(Mathf.Cos(Ph(t, T))), 0));
        Rot(c, Paths[Hips], T, t => new Vector3(lean * 0.5f, twist * S(t), 5f * S(t)));
        Rot(c, Paths[Spine], T, t => new Vector3(lean * 0.4f + 2f * Mathf.Sin(Ph(t, T) * 2f), -twist * 0.6f * S(t), -3f * S(t)));
        Scl(c, Paths[Spine], T, t => new Vector3(1f - squash * 0.5f * Mathf.Cos(Ph(t, T) * 2f), 1f + squash * Mathf.Cos(Ph(t, T) * 2f), 1f - squash * 0.5f * Mathf.Cos(Ph(t, T) * 2f)));
        Rot(c, Paths[Chest], T, t => new Vector3(lean * 0.3f, -twist * 0.5f * S(t), -2f * S(t)));
        Rot(c, Paths[Head], T, t => new Vector3(-lean * 0.7f + 4f * Mathf.Sin(Ph(t, T) * 2f), twist * 0.6f * S(t), 5f * S(t)));
        Rot(c, Paths[AntL], T, t => new Vector3(antBack + antWave * S(t, 0.5f), 0, 8f + antWave * S(t, 1.5f)));
        Rot(c, Paths[AntR], T, t => new Vector3(antBack + antWave * S(t, 1.0f), 0, -8f - antWave * S(t, 2.0f)));

        Rot(c, Paths[Ant2L], T, t => new Vector3(antBack * 0.6f + 1.6f * antWave * S(t, 1.3f), 0, 6f + 1.6f * antWave * S(t, 2.3f)));
        Rot(c, Paths[Ant2R], T, t => new Vector3(antBack * 0.6f + 1.6f * antWave * S(t, 1.8f), 0, -6f - 1.6f * antWave * S(t, 2.8f)));

        // bacaklar (ileri = negatif X)
        Rot(c, Paths[ThL], T, t => new Vector3(-swing * S(t) - crouch * 0.4f, 0, 0));
        Rot(c, Paths[ThR], T, t => new Vector3(-swing * S(t, Mathf.PI) - crouch * 0.4f, 0, 0));
        Rot(c, Paths[ShL], T, t => new Vector3(crouch + knee * Lift(t, 1.4f), 0, 0));
        Rot(c, Paths[ShR], T, t => new Vector3(crouch + knee * Lift(t, Mathf.PI + 1.4f), 0, 0));
        Rot(c, Paths[FtL], T, t => new Vector3(0.35f * swing * S(t) - 0.35f * knee * Lift(t, 1.4f), 0, 0));
        Rot(c, Paths[FtR], T, t => new Vector3(0.35f * swing * S(t, Mathf.PI) - 0.35f * knee * Lift(t, Mathf.PI + 1.4f), 0, 0));

        // kollar (bacaklara ters)
        Rot(c, Paths[ArmL], T, t => new Vector3(armSwing * S(t), 0, 10f + 4f * Mathf.Abs(S(t))));
        Rot(c, Paths[ArmR], T, t => new Vector3(armSwing * S(t, Mathf.PI), 0, -10f - 4f * Mathf.Abs(S(t, Mathf.PI))));
        Rot(c, Paths[ForeL], T, t => new Vector3(-elbowBase - elbow * (0.5f + 0.5f * S(t, -1.57f)), 0, 0));
        Rot(c, Paths[ForeR], T, t => new Vector3(-elbowBase - elbow * (0.5f + 0.5f * S(t, Mathf.PI - 1.57f)), 0, 0));
    }

    // Zıplama: çömelip toplanma → kollar yukarı, bacaklar karna, vücut uzar.
    private static void BuildJump(AnimationClip c, Vector3 hip0)
    {
        const float T = 0.5f;
        float J(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (T * 0.45f)));
        float Dip(float t) => Mathf.Sin(Mathf.Clamp01(t / (T * 0.25f)) * Mathf.PI) * (1f - J(t));
        Pos(c, Paths[Hips], T, t => hip0 + new Vector3(0, -0.006f * Dip(t), 0));
        Rot(c, Paths[Hips], T, t => new Vector3(14f * Dip(t) - 6f * J(t), 0, 0));
        Rot(c, Paths[Spine], T, t => new Vector3(-8f * J(t), 0, 0));
        Scl(c, Paths[Spine], T, t => new Vector3(1f - 0.06f * J(t) + 0.08f * Dip(t), 1f + 0.10f * J(t) - 0.12f * Dip(t), 1f - 0.06f * J(t) + 0.08f * Dip(t)));
        Rot(c, Paths[Chest], T, t => new Vector3(-10f * J(t), 0, 0));
        Rot(c, Paths[Head], T, t => new Vector3(-14f * J(t), 0, 0));
        Rot(c, Paths[AntL], T, t => new Vector3(-40f * J(t), 0, 15f * J(t)));
        Rot(c, Paths[AntR], T, t => new Vector3(-40f * J(t), 0, -15f * J(t)));
        Rot(c, Paths[Ant2L], T, t => new Vector3(-25f * J(t) * (1f + 0.4f * Mathf.Sin(t * 30f)), 0, 10f * J(t)));
        Rot(c, Paths[Ant2R], T, t => new Vector3(-25f * J(t) * (1f + 0.4f * Mathf.Sin(t * 30f)), 0, -10f * J(t)));
        Rot(c, Paths[ArmL], T, t => new Vector3(-150f * J(t), 0, 25f * J(t)));
        Rot(c, Paths[ArmR], T, t => new Vector3(-150f * J(t), 0, -25f * J(t)));
        Rot(c, Paths[ForeL], T, t => new Vector3(-20f * J(t), 0, 0));
        Rot(c, Paths[ForeR], T, t => new Vector3(-20f * J(t), 0, 0));
        foreach (int th in new[] { ThL, ThR }) Rot(c, Paths[th], T, t => new Vector3((30f * Dip(t)) - 45f * J(t), 0, 0));
        foreach (int sh in new[] { ShL, ShR }) Rot(c, Paths[sh], T, t => new Vector3(60f * Dip(t) + 80f * J(t), 0, 0));
        foreach (int ft in new[] { FtL, FtR }) Rot(c, Paths[ft], T, t => new Vector3(-25f * J(t), 0, 0));
    }

    private static void Still(AnimationClip c, float T, params int[] bones)
    {
        foreach (int b in bones) Rot(c, Paths[b], T, t => Vector3.zero);
    }

    // ---------- yardımcılar
    private static float Smooth(float a, float b, float x)
    {
        if (Mathf.Approximately(a, b)) return x >= b ? 1f : 0f;
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Average(IEnumerable<float> xs)
    {
        float s = 0; int n = 0;
        foreach (var x in xs) { s += x; n++; }
        return n == 0 ? 0f : s / n;
    }

    private static Transform Bone(string name, Transform parent, Transform pivot, Vector3 pivotSpacePos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pivot.TransformPoint(pivotSpacePos);
        t.rotation = pivot.rotation;
        return t;
    }

    private static BoneWeight Top4(float[] w)
    {
        var idx = Enumerable.Range(0, w.Length).OrderByDescending(i => w[i]).Take(4).ToArray();
        float sum = idx.Sum(i => w[i]);
        if (sum <= 1e-6f) return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        return new BoneWeight
        {
            boneIndex0 = idx[0], weight0 = w[idx[0]] / sum,
            boneIndex1 = idx[1], weight1 = w[idx[1]] / sum,
            boneIndex2 = idx[2], weight2 = w[idx[2]] / sum,
            boneIndex3 = idx[3], weight3 = w[idx[3]] / sum,
        };
    }

    private static AnimationClip Load(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Dir}/{name}.anim");
        if (c == null) throw new System.Exception("Klip yok: " + name + " (önce 'Hamam Boecegi Modelini Bagla')");
        return c;
    }

    private const int N = 40;

    private static void Write(AnimationClip clip, string path, string[] props, float T, System.Func<float, Vector3> f)
    {
        for (int a = 0; a < 3; a++)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= N; i++) { float t = T * i / N; var v = f(t); curve.AddKey(new Keyframe(t, a == 0 ? v.x : a == 1 ? v.y : v.z)); }
            for (int i = 0; i < curve.length; i++) { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto); }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Pivot/" + path, typeof(Transform), props[a]), curve);
        }
    }

    private static void Rot(AnimationClip c, string path, float T, System.Func<float, Vector3> f) =>
        Write(c, path, new[] { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" }, T, f);
    private static void Pos(AnimationClip c, string path, float T, System.Func<float, Vector3> f) =>
        Write(c, path, new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" }, T, f);
    private static void Scl(AnimationClip c, string path, float T, System.Func<float, Vector3> f) =>
        Write(c, path, new[] { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" }, T, f);
}
