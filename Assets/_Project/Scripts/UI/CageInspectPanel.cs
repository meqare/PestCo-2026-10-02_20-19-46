using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Fanus inceleme paneli. Görünüm tamamen sahnedeki Canvas nesnelerinden gelir (Inspector referansları);
// kod hiçbir UI oluşturmaz. Referans eksikse Console'a hata yazar ve panel açılmaz.
// Bu bileşen her zaman aktif bir yönetici nesnede durur; görsel panel (panelRoot) ayrı bir nesnedir ve
// başlangıçta kapalıdır. Panel açıkken: imleç serbest, oyuncu hareketi/bakışı durur (PlayerController imleç
// kilidine bağlı), PlayerInteraction etkileşim yapmaz, listedeki girdi bileşenleri (vakum, sprey, el feneri...)
// geçici kapatılır. Time.timeScale'e dokunulmaz: yaratıklar fanusta hareket etmeye devam eder.
public class CageInspectPanel : MonoBehaviour
{
    // Panel açıkken true; PlayerController/PlayerInteraction okur (PestPC.IsOpen gibi).
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
    }

    [Header("Panel (başlangıçta kapalı, bu bileşenin nesnesinden ayrı olmalı)")]
    [SerializeField] private GameObject panelRoot;

    [Header("Yaratık bilgisi")]
    [SerializeField] private TMP_Text creatureNameText;
    [SerializeField] private TMP_Text creatureTypeText;
    [SerializeField] private TMP_Text creatureStateText;
    [SerializeField] private TMP_Text baseValueText;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private TMP_Text currentValueText;
    [SerializeField] private TMP_Text occupancyText;

    [Header("İsteğe bağlı (boş bırakılabilir)")]
    [Tooltip("Şimdilik sabit: \"Mutasyon: Yok\".")]
    [SerializeField] private TMP_Text mutationText;
    [Tooltip("Şimdilik sabit: \"Stres: —\".")]
    [SerializeField] private TMP_Text stressText;
    [Tooltip("Fanusta birden fazla yaratık varken \"1/2\" gibi sıra yazısı.")]
    [SerializeField] private TMP_Text indexText;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    [Header("Butonlar")]
    [SerializeField] private Button closeButton;
    [Tooltip("BESLE yem seçim bölümünü açar. Mutasyon / Sat / Sakla şimdilik çalışmaz: kod bunları tıklanamaz yapar.")]
    [SerializeField] private Button feedButton;
    [SerializeField] private Button experimentButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button keepButton;

    [System.Serializable]
    public class FoodButtonView
    {
        public FoodType food;
        public Button button;
        [Tooltip("Buton yazısı: \"Soda (3)\" biçiminde yazılır.")]
        public TMP_Text label;
    }

    [Header("Besleme")]
    [Tooltip("Yem kurallarını yöneten bileşen (arayüz yalnızca ondan besleme ister).")]
    [SerializeField] private CreatureFeeder feeder;
    [Tooltip("Yem seçim alt bölümü: başlangıçta kapalı, açılınca ana bilginin üstünü örter.")]
    [SerializeField] private GameObject foodPanel;
    [SerializeField] private FoodButtonView[] foodButtons;
    [SerializeField] private Button foodBackButton;
    [Tooltip("İsteğe bağlı: yem ekranının başlığı (hangi yaratık beslenecek).")]
    [SerializeField] private TMP_Text foodTitleText;
    [Tooltip("İsteğe bağlı: yem ekranındaki uyarı (stok yok, meşgul...).")]
    [SerializeField] private TMP_Text foodMessageText;
    [Tooltip("İsteğe bağlı: ana ekranda besleme sonucu mesajı.")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("İsteğe bağlı: \"Boyut: x1.10\" satırı.")]
    [SerializeField] private TMP_Text growthText;
    [SerializeField, Min(0.5f)] private float messageSeconds = 3f;

    [Header("Baskı (fanus doluluğu)")]
    [Tooltip("Örn. \"BASKI: 125%\". Yüzde 100'ü aşınca gerçek değer yazılır; çubuk dolu kalır.")]
    [SerializeField] private TMP_Text pressureText;
    [SerializeField] private TMP_Text pressureStateText;
    [Tooltip("Etkileşimsiz Slider (0-1). Dolgu rengi baskı durumuna göre değişir.")]
    [SerializeField] private Slider pressureBar;
    [SerializeField] private string pressureLabel = "BASKI";
    [Tooltip("Safe, Warning, Danger, Critical sırasıyla.")]
    [SerializeField] private string[] stateLabels = { "GÜVENLİ", "UYARI", "TEHLİKE", "KRİTİK" };

    [Header("Kritik geri sayım")]
    [Tooltip("Geri sayım başlayınca (ve kırılma beklenirken) açılan kırmızı uyarı bloğu; başlangıçta kapalı.")]
    [SerializeField] private GameObject criticalBanner;
    [Tooltip("\"KRİTİK TEHLİKE!\" / \"FANUS KIRILMAK ÜZERE!\"")]
    [SerializeField] private TMP_Text criticalTitleText;
    [Tooltip("\"KIRILMAYA KALAN: 09.4 SANİYE\"")]
    [SerializeField] private TMP_Text criticalCountdownText;
    [SerializeField] private Color criticalTextColor = new Color(0.95f, 0.15f, 0.12f);
    [Tooltip("Satış sonrası boş fanus mesajının açık kalma süresi (sn).")]
    [SerializeField, Min(0.5f)] private float soldMessageSeconds = 2f;
    [SerializeField] private Color successMessageColor = new Color(0.3f, 0.56f, 0.18f);

    [Header("Durum renkleri")]
    [SerializeField] private Color cuteColor = new Color(0.3f, 0.56f, 0.18f);
    [SerializeField] private Color horrorColor = new Color(0.82f, 0.3f, 0.24f);

    [Header("Oyuncu girdisi")]
    [Tooltip("Panel açıkken geçici kapatılacak, kendi girdisini okuyan bileşenler (vakum, sprey, alet seçici, el feneri, " +
             "envanter çubuğu, raf, satış alanları...). Kapanınca açık olanlar geri açılır.")]
    [SerializeField] private Behaviour[] disableWhileOpen;
    [Tooltip("Kameradan fanusa bu mesafeden uzaklaşırsa panel kapanır (m).")]
    [SerializeField, Min(1f)] private float closeDistance = 4.5f;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private readonly List<BugCage.CreatureInfo> infos = new List<BugCage.CreatureInfo>();
    private readonly List<Behaviour> disabledByPanel = new List<Behaviour>();

    private BugCage target;
    private int selected;
    private float nextRefresh;
    private Camera cam;
    private Coroutine restoreRoutine;
    private float messageClearTime;
    private CagePressureController pressureController;
    private CageCriticalController criticalController;
    private int shownCriticalTenths = -1;
    private TMP_Text sellLabel;
    private float soldCloseTime; // satış sonrası boş fanusta mesajın görünmesi için paneli kısa süre açık tutar

    // İncelenen fanus (kapalıyken null).
    public BugCage Target => target;

    private void Awake()
    {
        if (!ValidateReferences()) return;

        panelRoot.SetActive(false);
        closeButton.onClick.AddListener(Close);
        if (prevButton != null) prevButton.onClick.AddListener(SelectPrevious);
        if (nextButton != null) nextButton.onClick.AddListener(SelectNext);

        feedButton.onClick.AddListener(OpenFoodPanel);
        foodBackButton.onClick.AddListener(CloseFoodPanel);
        foreach (FoodButtonView view in foodButtons)
        {
            FoodType food = view.food;
            view.button.onClick.AddListener(() => Feed(food));
        }
        foodPanel.SetActive(false);
        if (messageText != null) messageText.text = string.Empty;
        if (foodMessageText != null) foodMessageText.text = string.Empty;

        // Henüz bağlı olmayan işlemler: tıklanamaz.
        feedButton.interactable = false;
        experimentButton.interactable = false;
        sellButton.onClick.AddListener(Sell);
        sellLabel = sellButton.GetComponentInChildren<TMP_Text>(true);
        sellButton.interactable = false;
        keepButton.interactable = false;

        if (mutationText != null) mutationText.text = "Mutasyon: Yok";
        if (stressText != null) stressText.text = "Stres: —";
    }

    private void OnDisable()
    {
        if (IsOpen) CloseInternal(false);
    }

    // Eksik referansları tek bir hata satırında listeler.
    private bool ValidateReferences()
    {
        var missing = new List<string>();
        Check(panelRoot, "panelRoot", missing);
        Check(creatureNameText, "creatureNameText", missing);
        Check(creatureTypeText, "creatureTypeText", missing);
        Check(creatureStateText, "creatureStateText", missing);
        Check(baseValueText, "baseValueText", missing);
        Check(multiplierText, "multiplierText", missing);
        Check(currentValueText, "currentValueText", missing);
        Check(occupancyText, "occupancyText", missing);
        Check(closeButton, "closeButton", missing);
        Check(feedButton, "feedButton", missing);
        Check(experimentButton, "experimentButton", missing);
        Check(sellButton, "sellButton", missing);
        Check(keepButton, "keepButton", missing);
        Check(criticalBanner, "criticalBanner", missing);
        Check(criticalTitleText, "criticalTitleText", missing);
        Check(criticalCountdownText, "criticalCountdownText", missing);
        Check(pressureText, "pressureText", missing);
        Check(pressureStateText, "pressureStateText", missing);
        Check(pressureBar, "pressureBar", missing);
        if (pressureBar != null && pressureBar.fillRect == null) missing.Add("pressureBar.fillRect");
        Check(feeder, "feeder", missing);
        Check(foodPanel, "foodPanel", missing);
        Check(foodBackButton, "foodBackButton", missing);
        if (foodButtons == null || foodButtons.Length == 0) missing.Add("foodButtons");
        else
            for (int i = 0; i < foodButtons.Length; i++)
                if (foodButtons[i] == null || foodButtons[i].button == null || foodButtons[i].label == null)
                    missing.Add("foodButtons[" + i + "] (button/label)");

        if (missing.Count > 0)
        {
            Debug.LogError("CageInspectPanel: Inspector'da atanmamış referanslar var: " + string.Join(", ", missing) +
                           ". Panel açılmayacak. UI nesnelerini kurup bu alanlara sürükleyin.", this);
            return false;
        }

        if (!foodPanel.transform.IsChildOf(panelRoot.transform) || foodPanel == panelRoot)
        {
            Debug.LogError("CageInspectPanel: foodPanel, panelRoot'un çocuğu olmalı (yem ekranı panelin içinde açılır).", this);
            return false;
        }

        if (transform.IsChildOf(panelRoot.transform))
        {
            Debug.LogError("CageInspectPanel: panelRoot bu bileşenin nesnesiyle aynı ya da üstü olamaz " +
                           "(panel kapanınca bileşen de kapanırdı). Yönetici nesne panelden ayrı olmalı.", this);
            return false;
        }
        return true;
    }

    private static void Check(Object reference, string name, List<string> missing)
    {
        if (reference == null) missing.Add(name);
    }

    #region Aç / Kapat

    // BugCage çağırır: bu fanus için paneli açar. Başka bir arayüz açıksa ya da referans eksikse açmaz.
    public void Open(BugCage cage)
    {
        if (cage == null || IsOpen) return;
        if (!ValidateReferences()) return;
        if (PestCo.Truck.PestPC.IsOpen || JobBoardUI.IsOpen) return;

        target = cage;
        selected = 0;
        pressureController = cage.GetComponent<CagePressureController>();
        if (pressureController == null)
            Debug.LogError("CageInspectPanel: Bu fanusta CagePressureController yok: " + cage.name, cage);
        else
        {
            pressureController.OnPressureChanged += HandlePressureChanged;
            pressureController.OnPressureStateChanged += HandlePressureStateChanged;
        }
        cage.ContentsChanged += HandleContentsChanged;
        soldCloseTime = 0f;
        criticalController = cage.GetComponent<CageCriticalController>();
        if (criticalController != null)
        {
            criticalController.OnCountdownStarted += HandleCountdownStarted;
            criticalController.OnCountdownUpdated += HandleCountdownUpdated;
            criticalController.OnCountdownCancelled += HandleCountdownCancelled;
            criticalController.OnBreachRequested += HandleBreachRequested;
        }
        panelRoot.SetActive(true);

        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        DisableInput();
        Refresh();
    }

    // Kapat butonu ve Esc: paneli kapatır, kontrolleri ve imleç kilidini geri verir.
    public void Close()
    {
        if (IsOpen) CloseInternal(true);
    }

    // relockCursor: PestPC gibi başka bir arayüz imleci devraldıysa false.
    private void CloseInternal(bool relockCursor)
    {
        IsOpen = false;
        if (target != null) target.ContentsChanged -= HandleContentsChanged;
        target = null;
        soldCloseTime = 0f;
        UnsubscribePressure();
        UnsubscribeCritical();
        if (foodPanel != null) foodPanel.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(false);

        // Kontroller, Kapat tıkı vakum/spreye geçmesin diye tuş bırakılınca geri açılır.
        if (isActiveAndEnabled) restoreRoutine = StartCoroutine(RestoreInputAfterRelease());
        else RestoreInput();

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        if (!relockCursor) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // PlayerController aynı karede Esc'yi görüp imleci serbest bırakabilir: bir kare sonra yeniden kilitle.
        if (isActiveAndEnabled) StartCoroutine(RelockNextFrame());
    }

    private IEnumerator RelockNextFrame()
    {
        yield return null;
        if (IsOpen) yield break;
        if (PestCo.Truck.PestPC.IsOpen || JobBoardUI.IsOpen) yield break;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator RestoreInputAfterRelease()
    {
        yield return null;
        Mouse mouse = Mouse.current;
        float end = Time.unscaledTime + 1f;
        while (mouse != null && Time.unscaledTime < end && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            yield return null;

        RestoreInput();
        restoreRoutine = null;
    }

    // Daha önce kapatılıp henüz geri açılmamış bileşenler listede kalır (hızlı kapa-aç durumunda kaybolmasın).
    private void UnsubscribePressure()
    {
        if (pressureController == null) return;
        pressureController.OnPressureChanged -= HandlePressureChanged;
        pressureController.OnPressureStateChanged -= HandlePressureStateChanged;
        pressureController = null;
    }

    private void UnsubscribeCritical()
    {
        if (criticalController == null) return;
        criticalController.OnCountdownStarted -= HandleCountdownStarted;
        criticalController.OnCountdownUpdated -= HandleCountdownUpdated;
        criticalController.OnCountdownCancelled -= HandleCountdownCancelled;
        criticalController.OnBreachRequested -= HandleBreachRequested;
        criticalController = null;
    }

    private void HandleCountdownStarted(float duration)
    {
        shownCriticalTenths = -1;
        UpdateCriticalUI();
    }

    private void HandleCountdownUpdated(float remaining)
    {
        UpdateCriticalUI();
    }

    private void HandleCountdownCancelled()
    {
        UpdateCriticalUI();
    }

    private void HandleBreachRequested(BugCage breachedCage)
    {
        UpdateCriticalUI();
    }

    private void HandleContentsChanged()
    {
        Refresh();
    }

    // SAT butonu: "SAT — $XXX" (CageSaleService ile aynı değer). Boş ya da kırılma beklerken pasif.
    private void RefreshSellButton()
    {
        bool has = target != null && target.Count > 0;
        bool blocked = has && CageSaleService.IsBlocked(target);
        int payout = has ? CageSaleService.PreviewPayout(target) : 0;

        sellButton.interactable = has && !blocked;
        if (sellLabel != null) sellLabel.text = blocked ? "SAT — ÇOK GEÇ" : "SAT — $" + payout;
    }

    // SAT: seçili fanustaki bütün yaratıkları ortak satış işlemiyle satar. Panel, fanus boşalınca mesaj
    // görünsün diye kısa süre açık kalır. Aynı karede ikinci tık satacak bir şey bulamaz (çifte ödeme yok).
    private void Sell()
    {
        if (!IsOpen || target == null) return;

        soldCloseTime = Time.unscaledTime + soldMessageSeconds; // ContentsChanged paneli erken kapatmasın
        CageSaleResult result = CageSaleService.SellCage(target);
        if (result.status != CageSaleStatus.Success) soldCloseTime = 0f;

        switch (result.status)
        {
            case CageSaleStatus.Success:
                CloseFoodPanel();
                SetMessage("+" + result.payout + "$ KAZANDIN!", successMessageColor, soldMessageSeconds);
                break;
            case CageSaleStatus.TooLate:
                SetMessage("SATIŞ İÇİN ÇOK GEÇ!", criticalTextColor, messageSeconds);
                break;
            case CageSaleStatus.NothingToSell:
                SetMessage("Satılacak yaratık yok.", criticalTextColor, messageSeconds);
                break;
            default:
                SetMessage("Satış yapılamadı.", criticalTextColor, messageSeconds);
                break;
        }
        RefreshSellButton();
    }

    // Satıştan sonra boşalan fanus: bilgi alanı sıfırlanır, mesaj kısa süre görünür, sonra panel kapanır.
    private void ShowSoldState()
    {
        creatureNameText.text = "Fanus boşaltıldı";
        creatureTypeText.text = string.Empty;
        creatureStateText.text = string.Empty;
        baseValueText.text = string.Empty;
        multiplierText.text = string.Empty;
        currentValueText.text = string.Empty;
        if (growthText != null) growthText.text = string.Empty;
        if (indexText != null) indexText.text = string.Empty;
        occupancyText.text = "Doluluk: 0/" + target.Capacity;
        feedButton.interactable = false;
        UpdatePressureUI();
        UpdateCriticalUI();
        RefreshSellButton();
    }

    // Panel açıkken fanus kırıldı: yaratık bilgisi yerine "Kırık" gösterilir; panel açık kalır, onarım E ile yapılır.
    private void ShowBrokenState()
    {
        infos.Clear();
        creatureNameText.text = "Fanus KIRIK";
        creatureTypeText.text = string.Empty;
        creatureStateText.text = "Durum: Kırık";
        creatureStateText.color = criticalTextColor;
        baseValueText.text = string.Empty;
        multiplierText.text = string.Empty;
        currentValueText.text = string.Empty;
        if (growthText != null) growthText.text = string.Empty;
        if (indexText != null) indexText.text = string.Empty;
        occupancyText.text = "Doluluk: 0/" + target.Capacity;
        if (prevButton != null) prevButton.interactable = false;
        if (nextButton != null) nextButton.interactable = false;
        feedButton.interactable = false;
        CloseFoodPanel();
        UpdatePressureUI();
        UpdateCriticalUI();
    }

    // Kritik uyarı bloğu: yalnızca CountingDown/BreachPending'de görünür. Metin ondalık değiştikçe yazılır.
    private void UpdateCriticalUI()
    {
        if (target != null && target.IsBroken)
        {
            if (!criticalBanner.activeSelf) criticalBanner.SetActive(true);
            criticalTitleText.color = criticalTextColor;
            criticalCountdownText.color = criticalTextColor;
            criticalTitleText.text = "FANUS KIRILDI!";
            criticalCountdownText.text = "ONARMAK İÇİN KAPAT, E — $" + target.RepairCost;
            shownCriticalTenths = -1;
            RefreshSellButton();
            return;
        }

        CageCriticalState state = criticalController != null ? criticalController.State : CageCriticalState.Inactive;
        bool visible = state != CageCriticalState.Inactive;
        if (criticalBanner.activeSelf != visible) criticalBanner.SetActive(visible);
        RefreshSellButton();
        if (!visible)
        {
            shownCriticalTenths = -1;
            return;
        }

        criticalTitleText.color = criticalTextColor;
        criticalCountdownText.color = criticalTextColor;

        if (state == CageCriticalState.BreachPending)
        {
            criticalTitleText.text = "FANUS KIRILMAK ÜZERE!";
            criticalCountdownText.text = "SATIŞ İÇİN ÇOK GEÇ!";
            shownCriticalTenths = -1;
        }
        else
        {
            int tenths = Mathf.CeilToInt(Mathf.Max(criticalController.RemainingSeconds, 0f) * 10f);
            if (tenths != shownCriticalTenths || criticalTitleText.text != "KRİTİK TEHLİKE!")
            {
                shownCriticalTenths = tenths;
                criticalTitleText.text = "KRİTİK TEHLİKE!";
                criticalCountdownText.text = "KIRILMAYA KALAN: " + (tenths / 10f).ToString("00.0", CultureInfo.InvariantCulture) + " SANİYE";
            }
        }

        // Besleme kilitlendiyse açık yem ekranı kapanır.
        if (foodPanel.activeSelf)
        {
            CloseFoodPanel();
            SetMessage("Fanus kritik durumda, beslenemez.");
        }
    }

    private void HandlePressureChanged(float value)
    {
        UpdatePressureUI();
    }

    private void HandlePressureStateChanged(CagePressureState newState)
    {
        UpdatePressureUI();
    }

    // Fanusun gerçek baskısını yazar; çubuk 0-1 ile sınırlı, yazıda gerçek yüzde görünür.
    private void UpdatePressureUI()
    {
        if (pressureController == null)
        {
            pressureText.text = pressureLabel + ": —";
            pressureStateText.text = string.Empty;
            pressureBar.value = 0f;
            return;
        }

        CagePressureState state = pressureController.State;
        Color color = pressureController.ColorFor(state);
        int index = (int)state;

        pressureText.text = pressureLabel + ": " + Mathf.RoundToInt(pressureController.PressurePercent) + "%";
        pressureStateText.text = stateLabels != null && index < stateLabels.Length ? stateLabels[index] : state.ToString();
        pressureStateText.color = color;
        pressureBar.value = pressureController.Normalized;
        Graphic fill = pressureBar.fillRect.GetComponent<Graphic>();
        if (fill != null) fill.color = color;
    }

    private void DisableInput()
    {
        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }
        if (disableWhileOpen == null) return;

        foreach (Behaviour behaviour in disableWhileOpen)
        {
            if (behaviour == null || !behaviour.enabled) continue;
            behaviour.enabled = false;
            disabledByPanel.Add(behaviour);
        }
    }

    private void RestoreInput()
    {
        foreach (Behaviour behaviour in disabledByPanel)
            if (behaviour != null) behaviour.enabled = true;
        disabledByPanel.Clear();
    }

    #endregion

    #region Güncelleme

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        // Başka bir arayüz (PC, iş panosu) imleci devraldı: panel imlece dokunmadan kapanır.
        if (PestCo.Truck.PestPC.IsOpen || JobBoardUI.IsOpen)
        {
            CloseInternal(false);
            return;
        }

        if (target == null || (target.Count == 0 && !target.IsBroken && Time.unscaledTime >= soldCloseTime) || IsTooFar())
        {
            CloseInternal(true);
            return;
        }

        // Başka bir sistem imleci kilitlerse panel tıklanabilir kalsın.
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Time.unscaledTime >= nextRefresh) Refresh();
    }

    private bool IsTooFar()
    {
        if (cam == null) cam = Camera.main;
        return cam != null && target.DistanceFrom(cam.transform.position) > closeDistance;
    }

    private void SelectNext()
    {
        if (infos.Count > 0) selected = (selected + 1) % infos.Count;
        Refresh();
    }

    private void SelectPrevious()
    {
        if (infos.Count > 0) selected = (selected - 1 + infos.Count) % infos.Count;
        Refresh();
    }

    // Fanustan güncel özeti okuyup metinlere yazar.
    private void Refresh()
    {
        nextRefresh = Time.unscaledTime + refreshInterval;
        if (target == null) return;

        if (target.IsBroken)
        {
            ShowBrokenState();
            return;
        }

        target.GetCreatureInfos(infos);
        if (infos.Count == 0)
        {
            if (Time.unscaledTime < soldCloseTime) ShowSoldState();
            else CloseInternal(true);
            return;
        }

        selected = Mathf.Clamp(selected, 0, infos.Count - 1);
        BugCage.CreatureInfo info = infos[selected];
        bool horror = info.state == BugState.Horror;

        creatureNameText.text = info.name;
        creatureTypeText.text = "Tür: " + info.species;
        creatureStateText.text = "Durum: " + (horror ? "Korkunç (Horror)" : "Şirin (Cute)");
        creatureStateText.color = horror ? horrorColor : cuteColor;
        baseValueText.text = "Taban Değer: $" + Format(info.baseValue);
        multiplierText.text = "Değer Çarpanı: x" + Format(info.valueMultiplier);
        currentValueText.text = "Güncel Satış Değeri: $" + Format(info.saleValue);
        occupancyText.text = "Doluluk: " + target.Count + "/" + target.Capacity;
        UpdatePressureUI();
        UpdateCriticalUI();

        if (growthText != null)
        {
            string shown = Mathf.Abs(info.displayGrowth - info.growth) > 0.005f ? " (fanusta x" + Format(info.displayGrowth) + ")" : string.Empty;
            growthText.text = "Boyut: x" + Format(info.growth) + shown;
        }
        if (indexText != null) indexText.text = (selected + 1) + "/" + infos.Count;
        bool several = infos.Count > 1;
        if (prevButton != null) prevButton.interactable = several;
        if (nextButton != null) nextButton.interactable = several;

        // Büyüme animasyonu sürerken yeniden beslenemez.
        feedButton.interactable = info.bug != null && !info.bug.IsGrowing && !IsFeedingLocked();
        if (foodPanel.activeSelf) RefreshFood(info);
        if (messageText != null && messageClearTime > 0f && Time.unscaledTime >= messageClearTime)
        {
            messageText.text = string.Empty;
            messageClearTime = 0f;
        }
    }

    #endregion

    #region Besleme

    // BESLE: yem seçim bölümünü açar (seçili yaratık korunur).
    private void OpenFoodPanel()
    {
        if (!IsOpen || infos.Count == 0 || infos[selected].bug == null) return;

        if (foodMessageText != null) foodMessageText.text = string.Empty;
        foodPanel.SetActive(true);
        RefreshFood(infos[selected]);
    }

    private void CloseFoodPanel()
    {
        foodPanel.SetActive(false);
    }

    // Yem butonlarına "Soda (3)" yazar; stok 0 ya da yaratık büyüyorsa pasif yapar.
    private void RefreshFood(BugCage.CreatureInfo info)
    {
        if (foodTitleText != null) foodTitleText.text = info.name + " için yem seç";

        bool busy = info.bug == null || info.bug.IsGrowing || IsFeedingLocked();
        foreach (FoodButtonView view in foodButtons)
        {
            int stock = feeder.GetStock(view.food);
            view.label.text = FoodLabel(view.food) + " (" + stock + ")";
            view.button.interactable = stock > 0 && !busy;
        }
    }

    private string FoodLabel(FoodType food)
    {
        foreach (CreatureFeeder.FoodEntry entry in feeder.Foods)
            if (entry != null && entry.food == food && !string.IsNullOrEmpty(entry.label)) return entry.label;
        return food.ToString();
    }

    // Yem butonu: yalnızca besleme isteği gönderir; kurallar CreatureFeeder'dadır.
    private void Feed(FoodType food)
    {
        if (!IsOpen || target == null || infos.Count == 0) return;
        selected = Mathf.Clamp(selected, 0, infos.Count - 1);
        CageableBug bug = infos[selected].bug;

        CreatureFeeder.FeedResult result = feeder.TryFeed(target, bug, food);
        switch (result)
        {
            case CreatureFeeder.FeedResult.Success:
            case CreatureFeeder.FeedResult.SuccessAtSizeLimit:
                CloseFoodPanel();
                Refresh();
                SetMessage(result == CreatureFeeder.FeedResult.Success
                    ? FoodLabel(food) + " verildi!"
                    : FoodLabel(food) + " verildi (en büyük boyuta ulaştı).");
                break;
            case CreatureFeeder.FeedResult.NoFood:
                SetFoodMessage("Stokta bu yemden yok.");
                break;
            case CreatureFeeder.FeedResult.Blocked:
                CloseFoodPanel();
                SetMessage("Fanus kritik durumda, beslenemez.");
                break;
            case CreatureFeeder.FeedResult.Busy:
                SetFoodMessage("Yaratık hâlâ büyüyor, bekle.");
                break;
            case CreatureFeeder.FeedResult.NotInCage:
                CloseFoodPanel();
                Refresh();
                SetMessage("Bu yaratık artık fanusta değil.");
                break;
            default:
                SetFoodMessage("Beslenemedi.");
                break;
        }
        if (foodPanel.activeSelf) RefreshFood(infos[Mathf.Clamp(selected, 0, infos.Count - 1)]);
    }

    private bool IsFeedingLocked()
    {
        return criticalController != null && criticalController.IsFeedingBlocked;
    }

    private void SetMessage(string text)
    {
        SetMessage(text, successMessageColor, messageSeconds);
    }

    private void SetMessage(string text, Color color, float seconds)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageText.color = color;
        messageClearTime = Time.unscaledTime + seconds;
    }

    private void SetFoodMessage(string text)
    {
        if (foodMessageText != null) foodMessageText.text = text;
    }

    private static string Format(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    #endregion
}
