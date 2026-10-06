#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Sonuç ekranını (ResultPanel) Canvas altına kodla kurar ve ResultScreen referanslarını bağlar.
// Tekrar çalıştırılabilir: eski ResultPanel'i silip yeniden kurar. Sahnede JobManager yoksa onu da ekler.
public static class ResultPanelBuilder
{
    private const string PanelName = "ResultPanel";

    // Menü komutu: paneli kurar, JobManager'ı ekler, sahneyi build listesine koyar.
    [MenuItem("PEST CO/Sonuç Paneli Kur", priority = 20)]
    private static void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("PEST CO: Sonuç paneli Play modunda kurulamaz, önce Play'i durdur.");
            return;
        }

        Canvas canvas = FindRootCanvas();
        if (canvas == null)
        {
            Debug.LogError("PEST CO: Sahnede Canvas bulunamadı.");
            return;
        }

        Transform old = canvas.transform.Find(PanelName);
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);

        // Kök: her zaman aktif, ResultScreen burada durur.
        RectTransform root = CreateRect(PanelName, canvas.transform);
        Stretch(root);
        root.SetAsLastSibling();
        ResultScreen screen = Undo.AddComponent<ResultScreen>(root.gameObject);

        // Açılıp kapanan panel: yarı saydam siyah arka plan, tüm ekran.
        RectTransform panel = CreateRect("Panel", root);
        Stretch(panel);
        Image background = Undo.AddComponent<Image>(panel.gameObject);
        background.color = new Color(0f, 0f, 0f, 0.8f);

        TMP_Text title = CreateText("Title", panel, "İŞ TAMAMLANDI", 60, new Vector2(0f, 260f), 90f, FontStyles.Bold);
        TMP_Text cleaned = CreateText("Cleaned", panel, "Temizlenen: %94", 36, new Vector2(0f, 160f), 50f);
        TMP_Text time = CreateText("Time", panel, "Süre: 7:12", 36, new Vector2(0f, 110f), 50f);
        TMP_Text damage = CreateText("Damage", panel, "Hasar: 40$", 36, new Vector2(0f, 60f), 50f);
        TMP_Text grade = CreateText("Grade", panel, "A", 120, new Vector2(0f, -60f), 150f, FontStyles.Bold);
        grade.color = new Color32(0xA0, 0xFF, 0x4C, 0xFF);
        TMP_Text earnings = CreateText("Earnings", panel, "Kazanç: 260$", 44, new Vector2(0f, -170f), 60f, FontStyles.Bold);

        RectTransform buttonRow = CreateRect("Buttons", panel);
        buttonRow.anchoredPosition = new Vector2(0f, -260f);
        buttonRow.sizeDelta = new Vector2(500f, 70f);
        Button continueButton = CreateButton("ContinueButton", buttonRow, "DEVAM", new Vector2(-130f, 0f));
        Button retryButton = CreateButton("RetryButton", buttonRow, "TEKRAR (R)", new Vector2(130f, 0f));

        panel.gameObject.SetActive(false);

        // Referansları bağla.
        var so = new SerializedObject(screen);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("cleanedText").objectReferenceValue = cleaned;
        so.FindProperty("timeText").objectReferenceValue = time;
        so.FindProperty("damageText").objectReferenceValue = damage;
        so.FindProperty("gradeText").objectReferenceValue = grade;
        so.FindProperty("earningsText").objectReferenceValue = earnings;
        so.FindProperty("buttonRow").objectReferenceValue = buttonRow.gameObject;
        so.FindProperty("continueButton").objectReferenceValue = continueButton;
        so.FindProperty("retryButton").objectReferenceValue = retryButton;

        List<Behaviour> gameplay = FindGameplayBehaviours();
        SerializedProperty list = so.FindProperty("disableWhileOpen");
        list.arraySize = gameplay.Count;
        for (int i = 0; i < gameplay.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = gameplay[i];
        so.ApplyModifiedProperties();

        EnsureJobManager();
        EnsureSceneInBuild();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log($"PEST CO: ResultPanel kuruldu ({gameplay.Count} oyuncu bileşeni bağlandı). Ctrl+S ile kaydet.", root);
    }

    // Sahnedeki ilk kök Canvas'ı bulur.
    private static Canvas FindRootCanvas()
    {
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.isRootCanvas)
                return c;
        }
        return null;
    }

    // Panel açıkken kapatılacak oyuncu bileşenlerini toplar (hareket, etkileşim, fener, aletler).
    private static List<Behaviour> FindGameplayBehaviours()
    {
        var result = new List<Behaviour>();
        AddAll<PlayerController>(result);
        AddAll<PlayerInteraction>(result);
        AddAll<Flashlight>(result);
        AddAll<ToolHolder>(result);
        AddAll<SprayTool>(result);
        AddAll<SprayVisual>(result);
        return result;
    }

    // Belirli türdeki tüm bileşenleri listeye ekler.
    private static void AddAll<T>(List<Behaviour> list) where T : Behaviour
    {
        list.AddRange(Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    // Sahnede JobManager yoksa ekler.
    private static void EnsureJobManager()
    {
        if (Object.FindFirstObjectByType<JobManager>(FindObjectsInactive.Include) != null) return;

        var obj = new GameObject("JobManager");
        Undo.RegisterCreatedObjectUndo(obj, "Create JobManager");
        Undo.AddComponent<JobManager>(obj);
        Debug.Log("PEST CO: Sahneye JobManager eklendi.", obj);
    }

    // TEKRAR butonu sahneyi build index ile yükler; sahne listede değilse ekler.
    private static void EnsureSceneInBuild()
    {
        string path = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(path)) return;

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in scenes)
        {
            if (s.path == path)
            {
                if (!s.enabled) s.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("PEST CO: Sahne Build Settings listesine eklendi: " + path);
    }

    // Boş bir UI objesi oluşturur (ortaya sabitli).
    private static RectTransform CreateRect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    // RectTransform'u ebeveynini tamamen kaplayacak şekilde gerer.
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Ortalanmış bir TMP yazısı oluşturur.
    private static TMP_Text CreateText(string name, Transform parent, string content, float size,
        Vector2 position, float height, FontStyles style = FontStyles.Normal)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(900f, height);

        var text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // Yazılı, koyu gri bir buton oluşturur.
    private static Button CreateButton(string name, Transform parent, string label, Vector2 position)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(220f, 64f);

        var image = Undo.AddComponent<Image>(rect.gameObject);
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        var button = Undo.AddComponent<Button>(rect.gameObject);
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;

        TMP_Text text = CreateText("Label", rect, label, 28, Vector2.zero, 64f, FontStyles.Bold);
        Stretch((RectTransform)text.transform);
        return button;
    }
}
#endif
