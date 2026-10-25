using System.Collections;
using TMPro;
using UnityEngine;

public enum CageCriticalState
{
    Inactive,       // Kritik tehlike yok
    CountingDown,   // Sayaç çalışıyor
    BreachPending   // Sayaç bitti, kırılma talebi yayınlandı (iptal edilemez)
}

// Fanusun kritik baskı geri sayımı. CagePressureController Critical olunca sayaç başlar; baskı Critical'in
// altına inerse iptal edilir. Sayaç bitince BreachPending olur ve OnBreachRequested BİR KEZ yayınlanır.
// BU AŞAMADA KIRILMA YOKTUR: cam, yaratıklar ve satış değişmez; olayı 4D'deki kırılma sistemi dinleyecek.
//
// Zaman: sayaç Time.unscaledDeltaTime ile işler (Time.timeScale'e dokunulmaz). Böylece PestPC/iş panosu gibi
// oyunu durduran arayüzler sayacı dondurup tehlikeyi askıya alamaz; panel zaten oyunu durdurmaz.
// Sayaç bir coroutine ile yalnızca geri sayımı olan fanuslarda çalışır (boştaki fanuslarda Update yoktur).
[RequireComponent(typeof(CagePressureController))]
public class CageCriticalController : MonoBehaviour
{
    [Header("Süre")]
    [SerializeField, Min(0.5f)] private float countdownSeconds = 10f;

    [Header("Fanus üstü gösterge (isteğe bağlı)")]
    [Tooltip("Geri sayım sırasında açılan World Space Canvas'ın kökü (başlangıçta kapalı olabilir). Kameraya döner.")]
    [SerializeField] private GameObject worldRoot;
    [Tooltip("Gösterge üzerindeki süre yazısı.")]
    [SerializeField] private TMP_Text worldCountdownText;

    [Header("Durum ışığı (CagePressureController'ın ışığı yanıp söner)")]
    [SerializeField] private Color flashOnColor = new Color(0.95f, 0.1f, 0.08f);
    [SerializeField] private Color flashOffColor = new Color(0.2f, 0.02f, 0.02f);
    [Tooltip("Yanıp sönme periyodu (sn): x = geri sayımın başı, y = sonu (sona doğru hızlanır).")]
    [SerializeField] private Vector2 flashPeriod = new Vector2(0.5f, 0.15f);

    [Header("Alarm (isteğe bağlı)")]
    [Tooltip("3B ses kaynağı (Spatial Blend 1). Boşsa ya da ses atanmamışsa alarm çalmaz, hata vermez.")]
    [SerializeField] private AudioSource alarmSource;
    [SerializeField] private AudioClip alarmClip;
    [SerializeField, Range(0f, 1f)] private float alarmVolume = 0.5f;

    [Header("Telsiz")]
    [SerializeField] private string radioMessage = "ŞEFİM! O ŞEY CAMI KIRIYOR!";

    // Aynı anda çalan alarm sayısı: birden çok kritik fanusta ses toplamı şişmesin diye her biri kısılır.
    private static int activeAlarms;
    private static event System.Action AlarmCountChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeAlarms = 0;
        AlarmCountChanged = null;
    }

    private BugCage cage;
    private CagePressureController pressure;
    private Coroutine routine;
    private Camera cam;
    private CageCriticalState state;
    private float remaining;
    private bool breachRaised;
    private bool alarmPlaying;
    private int shownTenths = -1;

    public CageCriticalState State => state;
    public float RemainingSeconds => remaining;
    public float CountdownSeconds => countdownSeconds;
    // Kritik süreçte (sayaç ya da kırılma bekleniyor) fanustaki yaratıklar beslenemez.
    public bool IsFeedingBlocked => state != CageCriticalState.Inactive;
    // Kırılma beklenirken (ya da sayaç tam sıfırdayken) fanus satılamaz: "çok geç".
    public bool IsSaleBlocked => state == CageCriticalState.BreachPending
                                 || (state == CageCriticalState.CountingDown && remaining <= 0f);

    // Sayaç başladı (parametre: toplam süre).
    public event System.Action<float> OnCountdownStarted;
    // Sayaç her karede güncellenir (parametre: kalan saniye).
    public event System.Action<float> OnCountdownUpdated;
    // Baskı düştüğü için sayaç iptal edildi.
    public event System.Action OnCountdownCancelled;
    // Sayaç sıfıra ulaştı: kırılma talebi. Yalnızca bir kez yayınlanır.
    public event System.Action<BugCage> OnBreachRequested;

    private void Awake()
    {
        cage = GetComponent<BugCage>();
        pressure = GetComponent<CagePressureController>();
        if (worldRoot != null) worldRoot.SetActive(false);
    }

    private void OnEnable()
    {
        pressure.OnPressureStateChanged += HandlePressureState;
        HandlePressureState(pressure.State); // zaten Critical iken açıldıysa sayaç başlasın
    }

    private void OnDisable()
    {
        pressure.OnPressureStateChanged -= HandlePressureState;
        if (state == CageCriticalState.CountingDown) StopCountdown(false);
    }

    private void HandlePressureState(CagePressureState pressureState)
    {
        if (pressureState == CagePressureState.Critical)
        {
            if (state == CageCriticalState.Inactive) BeginCountdown();
        }
        else if (state == CageCriticalState.CountingDown)
        {
            StopCountdown(true); // BreachPending iptal edilemez
        }
    }

    // 4D onarımı/yenilemesi için: kırılma beklemesini sıfırlar ve baskıya göre yeniden değerlendirir.
    public void ResetAfterBreach()
    {
        if (state != CageCriticalState.BreachPending) return;
        state = CageCriticalState.Inactive;
        breachRaised = false;
        pressure.SetColorOverride(null);
        SetWorldVisible(false);
        HandlePressureState(pressure.State);
    }

    #region Geri sayım

    private void BeginCountdown()
    {
        state = CageCriticalState.CountingDown;
        remaining = countdownSeconds;
        shownTenths = -1;

        SetWorldVisible(true);
        UpdateWorldText();
        StartAlarm();
        if (RadioManager.Instance != null) RadioManager.Instance.PlayRadioSubtitle(radioMessage);

        OnCountdownStarted?.Invoke(countdownSeconds);
        routine = StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        float elapsed = 0f;
        bool lit = true;
        pressure.SetColorOverride(flashOnColor);
        float nextToggle = FlashHalfPeriod(0f);

        while (remaining > 0f)
        {
            // Büyük kare takılmaları (alt-tab, kesme noktası) sayacı atlatmasın.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            remaining -= dt;
            elapsed += dt;

            if (elapsed >= nextToggle)
            {
                lit = !lit;
                pressure.SetColorOverride(lit ? flashOnColor : flashOffColor);
                nextToggle = elapsed + FlashHalfPeriod(1f - Mathf.Clamp01(remaining / countdownSeconds));
            }

            FaceCamera();
            UpdateWorldText();
            OnCountdownUpdated?.Invoke(Mathf.Max(remaining, 0f));
            yield return null;
        }

        remaining = 0f;
        routine = null;
        EnterBreachPending();
    }

    private float FlashHalfPeriod(float progress)
    {
        return Mathf.Max(0.05f, Mathf.Lerp(flashPeriod.x, flashPeriod.y, progress) * 0.5f);
    }

    // Sayaç bitti: iptal edilemez duruma geçer ve kırılma talebini bir kez yayınlar. Burada hiçbir şey kırılmaz.
    private void EnterBreachPending()
    {
        state = CageCriticalState.BreachPending;
        StopAlarm();
        pressure.SetColorOverride(flashOnColor); // sabit kırmızı
        UpdateWorldText();
        OnCountdownUpdated?.Invoke(0f);

        if (breachRaised) return;
        breachRaised = true;
        OnBreachRequested?.Invoke(cage);
    }

    // Sayacı durdurur: iptal (olay yayınlı) ya da fanus kapanırken sessiz.
    private void StopCountdown(bool raiseEvent)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        state = CageCriticalState.Inactive;
        remaining = 0f;

        StopAlarm();
        pressure.SetColorOverride(null);
        SetWorldVisible(false);
        if (raiseEvent) OnCountdownCancelled?.Invoke();
    }

    #endregion

    #region Gösterge ve alarm

    private void SetWorldVisible(bool visible)
    {
        if (worldRoot != null) worldRoot.SetActive(visible);
    }

    // Gösterge kameraya bakar (yalnızca geri sayımda çalışır).
    private void FaceCamera()
    {
        if (worldRoot == null) return;
        if (cam == null) cam = Camera.main;
        if (cam != null) worldRoot.transform.rotation = cam.transform.rotation;
    }

    private void UpdateWorldText()
    {
        if (worldCountdownText == null) return;

        int tenths = Mathf.CeilToInt(Mathf.Max(remaining, 0f) * 10f);
        if (tenths == shownTenths) return;
        shownTenths = tenths;
        worldCountdownText.text = (tenths / 10f).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void StartAlarm()
    {
        if (alarmSource == null || alarmClip == null || alarmPlaying) return;

        alarmPlaying = true;
        activeAlarms++;
        AlarmCountChanged += UpdateAlarmVolume;
        AlarmCountChanged?.Invoke();

        alarmSource.clip = alarmClip;
        alarmSource.loop = true;
        alarmSource.Play();
    }

    private void StopAlarm()
    {
        if (!alarmPlaying) return;

        alarmPlaying = false;
        activeAlarms = Mathf.Max(0, activeAlarms - 1);
        AlarmCountChanged -= UpdateAlarmVolume;
        if (alarmSource != null) alarmSource.Stop();
        AlarmCountChanged?.Invoke();
    }

    // n kritik fanus aynı anda çalıyorsa her biri 1/√n ile kısılır (toplam ses şişmez).
    private void UpdateAlarmVolume()
    {
        if (alarmSource != null) alarmSource.volume = alarmVolume / Mathf.Sqrt(Mathf.Max(1, activeAlarms));
    }

    #endregion
}
