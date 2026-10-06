#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Vakum aletini sahneye tek tıkla kurar: Tool_2'ye VacuumTool + ses + partikül, HUD'a vakum çubuğu.
// Tekrar çalıştırılabilir: var olanı günceller, eksikleri ekler.
public static class VacuumSetup
{
    private const string ToolName = "Tool_2";
    private const string FxName = "VacuumFX";
    private const string BarName = "VacuumBar";
    private const string WarningName = "VacuumFullWarning";
    private const string UrpParticleMaterial =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    // Menü komutu: aleti, efekti, HUD'u ve sonuç ekranı bağlantısını kurar.
    [MenuItem("PEST CO/Vakum Kur", priority = 50)]
    private static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("PEST CO: Vakum Play modunda kurulamaz, once Play'i durdur.");
            return;
        }

        GameObject tool = FindInScene(ToolName);
        if (tool == null)
        {
            Debug.LogError($"PEST CO: '{ToolName}' bulunamadi (Player > ... > ToolSocket altinda olmali).");
            return;
        }

        VacuumTool vacuum = PestCoSetup.AddOrGet<VacuumTool>(tool);
        ParticleSystem fx = BuildFx(tool.transform);

        var so = new SerializedObject(vacuum);
        so.FindProperty("vacuumFX").objectReferenceValue = fx;
        so.FindProperty("targetLayers").intValue = LayerMaskOf("Bug", "Pest");
        so.FindProperty("obstacleLayers").intValue = LayerMaskOf("Default");
        so.ApplyModifiedProperties();
        PestCoSetup.MarkDirty(tool);

        BuildHud(vacuum);
        AddToResultScreen(vacuum);

        EditorSceneManager.MarkSceneDirty(tool.scene);
        Selection.activeGameObject = tool;
        Debug.Log("PEST CO: Vakum kuruldu. 2 = vakumu sec, sag tik = cek, R = bosalt. Ctrl+S ile kaydet.", tool);
    }

    // Kamera altındaki aletin ucuna, içe doğru akan partikül efektini kurar (varsa yeniden ayarlar).
    private static ParticleSystem BuildFx(Transform tool)
    {
        Transform fxTransform = tool.Find(FxName);
        if (fxTransform == null)
        {
            var obj = new GameObject(FxName);
            Undo.RegisterCreatedObjectUndo(obj, "Create " + FxName);
            fxTransform = obj.transform;
            fxTransform.SetParent(tool, false);
        }
        fxTransform.localPosition = new Vector3(0f, 0f, 0.3f);
        fxTransform.localRotation = Quaternion.identity;

        ParticleSystem ps = PestCoSetup.AddOrGet<ParticleSystem>(fxTransform.gameObject);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.maxParticles = 20;
        main.startLifetime = 0.4f;
        // Negatif hız: parçacıklar koninin içinde doğar ve namluya doğru akar (emme hissi).
        main.startSpeed = -3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.55f, 0.8f, 1f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 50f; // 0.4 sn ömürle ~20 parçacık

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.ConeVolume;
        shape.angle = 15f;
        shape.radius = 0.05f;
        shape.length = 1.5f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        var rend = fxTransform.GetComponent<ParticleSystemRenderer>();
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(UrpParticleMaterial);
        if (mat != null)
            rend.sharedMaterial = mat;
        else
            Debug.LogWarning("PEST CO: URP ParticlesUnlit materyali bulunamadi, VacuumFX'e elle bir partikul materyali ata.", rend);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return ps;
    }

    // Sprey çubuğunu kopyalayıp altına vakum çubuğu + sayaç + "BOŞALT" uyarısı kurar, HudController'a bağlar.
    private static void BuildHud(VacuumTool vacuum)
    {
        HudController hud = Object.FindFirstObjectByType<HudController>(FindObjectsInactive.Include);
        GameObject sprayBar = FindInScene("SprayBar");
        if (hud == null || sprayBar == null)
        {
            Debug.LogWarning("PEST CO: HUD veya SprayBar bulunamadi, vakum cubugu kurulmadi.");
            return;
        }

        var sprayRect = (RectTransform)sprayBar.transform;
        Transform old = sprayRect.parent.Find(BarName);
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);

        // Vakum çubuğu sprey çubuğunun eski yerine, sprey bir sıra yukarı.
        Vector2 basePos = new Vector2(sprayRect.anchoredPosition.x, 40f);
        Undo.RecordObject(sprayRect, "Move SprayBar");
        sprayRect.anchoredPosition = basePos + new Vector2(0f, 56f);

        GameObject bar = Object.Instantiate(sprayBar, sprayRect.parent);
        Undo.RegisterCreatedObjectUndo(bar, "Create " + BarName);
        bar.name = BarName;
        var barRect = (RectTransform)bar.transform;
        barRect.anchoredPosition = basePos;

        // Ön dolgu açık mavi (sprey beyazından ayırt edilsin).
        Transform front = bar.transform.Find("FrontFill_White");
        if (front != null && front.TryGetComponent(out Image frontImage))
            frontImage.color = new Color(0.55f, 0.85f, 1f, 1f);

        // Etiket: "Vakum: 3/5" sayan JuicyCounter.
        JuicyCounter counter = null;
        Transform label = bar.transform.Find("Label");
        if (label != null && label.TryGetComponent(out TMP_Text labelText))
        {
            labelText.text = "Vakum: 0/5";
            counter = PestCoSetup.AddOrGet<JuicyCounter>(label.gameObject);
            var cso = new SerializedObject(counter);
            cso.FindProperty("format").stringValue = "Vakum: {0}/{1}";
            cso.FindProperty("maxValue").intValue = 5;
            // Azalma (boşaltma) kötü bir şey değil: kırmızı yanıp sönmesin.
            cso.FindProperty("useDecreaseFlash").boolValue = false;
            cso.ApplyModifiedProperties();
        }

        // Dolunca görünen uyarı: çubuğun sağında kırmızı yazı.
        var warning = new GameObject(WarningName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(warning, "Create " + WarningName);
        warning.layer = bar.layer;
        var wRect = (RectTransform)warning.transform;
        wRect.SetParent(bar.transform, false);
        wRect.anchorMin = new Vector2(1f, 0.5f);
        wRect.anchorMax = new Vector2(1f, 0.5f);
        wRect.pivot = new Vector2(0f, 0.5f);
        wRect.anchoredPosition = new Vector2(12f, 0f);
        wRect.sizeDelta = new Vector2(220f, 30f);
        var wText = warning.AddComponent<TextMeshProUGUI>();
        wText.text = "BOŞALT! (R)";
        wText.fontSize = 24;
        wText.fontStyle = FontStyles.Bold;
        wText.color = new Color32(0xFF, 0x4C, 0x4C, 0xFF);
        wText.alignment = TextAlignmentOptions.MidlineLeft;
        wText.raycastTarget = false;
        warning.SetActive(false);

        var hso = new SerializedObject(hud);
        hso.FindProperty("vacuumTool").objectReferenceValue = vacuum;
        hso.FindProperty("vacuumBar").objectReferenceValue = bar.GetComponent<JuicyBar>();
        hso.FindProperty("vacuumCounter").objectReferenceValue = counter;
        hso.FindProperty("vacuumFullWarning").objectReferenceValue = warning;
        hso.ApplyModifiedProperties();
        PestCoSetup.MarkDirty(hud.gameObject);
    }

    // Sonuç ekranı açıkken vakum da kapansın (yoksa sağ tık arkada böcek çeker).
    private static void AddToResultScreen(VacuumTool vacuum)
    {
        ResultScreen screen = Object.FindFirstObjectByType<ResultScreen>(FindObjectsInactive.Include);
        if (screen == null) return;

        var so = new SerializedObject(screen);
        SerializedProperty list = so.FindProperty("disableWhileOpen");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == vacuum) return;

        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = vacuum;
        so.ApplyModifiedProperties();
        PestCoSetup.MarkDirty(screen.gameObject);
    }

    // Sahnede (kapalı objeler dahil) verilen isimdeki ilk objeyi bulur.
    private static GameObject FindInScene(string ad)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == ad) return t.gameObject;
        return null;
    }

    // Layer isimlerinden maske üretir (olmayan isimleri atlar).
    private static int LayerMaskOf(params string[] names)
    {
        int mask = 0;
        foreach (string n in names)
        {
            int layer = LayerMask.NameToLayer(n);
            if (layer >= 0) mask |= 1 << layer;
        }
        return mask;
    }
}
#endif
