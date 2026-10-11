using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// İş bitince açılan sonuç ekranı. Satırları sırayla gösterir, sayıları sayarak yazar, not harfini vurgular.
// Bu bileşen her zaman aktif olan objede durur; açılıp kapanan kısım "panel" objesidir.
// Tüm animasyonlar gerçek zamanla çalışır (açıkken Time.timeScale = 0).
public class ResultScreen : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Açılıp kapanan kök obje (arka plan ve tüm içerik bunun altında).")]
    [SerializeField] private GameObject panel;

    [Header("Yazılar")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text cleanedText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text earningsText;

    [Header("Butonlar")]
    [Tooltip("İki butonu birlikte tutan obje (en son belirir).")]
    [SerializeField] private GameObject buttonRow;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button retryButton;

    [Header("Panel Açıkken Kapanacaklar")]
    [Tooltip("Oyuncu hareketi, alet, el feneri vb. Kapatılmazsa butona tıklamak imleci kilitler ve sprey sıkar.")]
    [SerializeField] private Behaviour[] disableWhileOpen;

    [Header("Animasyon")]
    [SerializeField, Min(0f)] private float rowInterval = 0.15f;
    [SerializeField, Min(0.01f)] private float countDuration = 0.6f;
    [SerializeField, Min(1f)] private float gradePunchScale = 1.4f;
    [SerializeField, Min(0.01f)] private float gradePunchDuration = 0.3f;

    [Header("Not Renkleri")]
    [SerializeField] private Color colorS = new Color32(0x4C, 0xFF, 0x4C, 0xFF);
    [SerializeField] private Color colorA = new Color32(0xA0, 0xFF, 0x4C, 0xFF);
    [SerializeField] private Color colorB = new Color32(0xFF, 0xD4, 0x26, 0xFF);
    [SerializeField] private Color colorC = new Color32(0xFF, 0x8C, 0x1A, 0xFF);
    [SerializeField] private Color colorD = new Color32(0xFF, 0x4C, 0x4C, 0xFF);

    private bool isOpen;
    private Coroutine showRoutine;
    private WaitForSecondsRealtime rowWait;

    // Bekleme nesnesini hazırlar ve buton tıklamalarını bağlar.
    private void Awake()
    {
        rowWait = new WaitForSecondsRealtime(rowInterval);
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (retryButton != null) retryButton.onClick.AddListener(Retry);
    }

    // Oyun başında panel kapalı başlar.
    private void Start()
    {
        if (panel != null) panel.SetActive(false);
    }

    // İş bitişi olayına abone olur.
    private void OnEnable()
    {
        JobManager.OnJobComplete += Show;
    }

    // Aboneliği bırakır (sahne değişince eski ekran çağrılmasın).
    private void OnDisable()
    {
        JobManager.OnJobComplete -= Show;
    }

    // Panel açıkken R tuşunu dinler.
    private void Update()
    {
        if (!isOpen) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            Retry();
    }

    // Paneli açar, oyunu durdurur, imleci serbest bırakır ve animasyonu başlatır.
    public void Show(JobResult result)
    {
        if (panel == null)
        {
            Debug.LogError("ResultScreen: panel atanmamış!", this);
            return;
        }

        isOpen = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetGameplayEnabled(false);

        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowSequence(result));
    }

    // Satırları rowInterval arayla sırayla açar; sayılar ve not harfi kendi animasyonlarını oynatır.
    private IEnumerator ShowSequence(JobResult result)
    {
        HideAllRows();

        yield return Reveal(titleText);

        yield return Reveal(cleanedText);
        StartCoroutine(CountUp(cleanedText, result.CleanedPercent, v => $"Temizlenen: %{v}"));

        yield return Reveal(timeText);
        StartCoroutine(CountUp(timeText, Mathf.RoundToInt(result.DurationSeconds), v => $"Süre: {v / 60}:{v % 60:00}"));

        yield return Reveal(damageText);
        StartCoroutine(CountUp(damageText, result.Damage, v => $"Hasar: {v}$"));

        gradeText.text = result.Grade;
        gradeText.color = GradeColor(result.Grade);
        yield return Reveal(gradeText);
        StartCoroutine(PunchScale(gradeText.transform));

        yield return Reveal(earningsText);
        StartCoroutine(CountUp(earningsText, result.Earnings, v => $"Kazanç: {v}$"));

        if (buttonRow != null)
        {
            buttonRow.SetActive(true);
            yield return rowWait;
        }
        showRoutine = null;
    }

    // Animasyon başlamadan önce tüm satırları gizler.
    private void HideAllRows()
    {
        SetActive(titleText, false);
        SetActive(cleanedText, false);
        SetActive(timeText, false);
        SetActive(damageText, false);
        SetActive(gradeText, false);
        SetActive(earningsText, false);
        if (buttonRow != null) buttonRow.SetActive(false);
    }

    // Bir satırı açar ve sıradaki satır için bekler.
    private IEnumerator Reveal(TMP_Text text)
    {
        SetActive(text, true);
        yield return rowWait;
    }

    // Sayıyı 0'dan hedefe doğru sayarak yazar (sona doğru yavaşlar).
    private IEnumerator CountUp(TMP_Text text, int target, System.Func<int, string> format)
    {
        if (text == null) yield break;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / countDuration;
            float eased = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            text.text = format(Mathf.RoundToInt(target * eased));
            yield return null;
        }
        text.text = format(target);
    }

    // Ölçeği gradePunchScale'den 1'e küçültür (belirme vuruşu).
    private IEnumerator PunchScale(Transform target)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / gradePunchDuration;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            target.localScale = Vector3.one * Mathf.Lerp(gradePunchScale, 1f, eased);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    // DEVAM: karne kazancı JobManager'da zaten işlendi, burada tekrar işlenmez. Hub'a döner.
    public void Continue()
    {
        if (!isOpen) return;

        isOpen = false;
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
        panel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (SceneLoader.Instance == null)
        {
            Debug.LogError("ResultScreen: Sahnede SceneLoader yok, Hub'a dönülemedi.", this);
            SetGameplayEnabled(true);
            return;
        }
        SceneLoader.Instance.LoadHub();
    }

    // TEKRAR: zamanı normale döndürür ve aktif sahneyi baştan yükler.
    public void Retry()
    {
        isOpen = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Oyuncu kontrollerini topluca açar/kapatır.
    private void SetGameplayEnabled(bool enabled)
    {
        if (disableWhileOpen == null) return;
        for (int i = 0; i < disableWhileOpen.Length; i++)
        {
            if (disableWhileOpen[i] != null)
                disableWhileOpen[i].enabled = enabled;
        }
    }

    // Not harfine göre rengi döndürür.
    private Color GradeColor(string grade)
    {
        switch (grade)
        {
            case "S": return colorS;
            case "A": return colorA;
            case "B": return colorB;
            case "C": return colorC;
            default: return colorD;
        }
    }

    // Boş referansa karşı güvenli SetActive.
    private static void SetActive(TMP_Text text, bool active)
    {
        if (text != null) text.gameObject.SetActive(active);
    }
}
