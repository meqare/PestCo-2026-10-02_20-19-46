using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Vakumun özellik haznesi: T ile mod (Ağırlık/Isı/Ses) seçilir, fare orta tuşu ile bakılan nesneden özellik çekilir
// veya haznedeki özellik bakılan nesneye aktarılır. Hazne tek bir özellik türü tutar; değerler KitchenObjectProperties'te.
// Tutma sistemi (PhysicsVacuumController) aktifken çalışmaz; tekerlek/tuşlar ona dokunmaz.
[DisallowMultipleComponent]
public class VacuumPropertyTank : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boşsa Camera.main kullanılır.")]
    [SerializeField] private Camera cam;

    [Header("Kontroller")]
    [Tooltip("Modu sırayla değiştirir (Ağırlık -> Isı -> Ses).")]
    [SerializeField] private Key modeKey = Key.T;
    [Tooltip("Bakılan nesneden çekmek / haznedeki özelliği aktarmak için fare orta tuşu.")]
    [SerializeField] private bool useMiddleMouse = true;

    [Header("Hedefleme")]
    [Min(0.5f)] [SerializeField] private float maxRange = 3f;
    [Tooltip("Hedef seçiminde dikkate alınan katmanlar (varsayılan: hepsi).")]
    [SerializeField] private LayerMask targetMask = ~0;

    [Header("Ağırlık (kg)")]
    [Tooltip("Tek kullanımda çekilen/aktarılan miktar.")]
    [Min(0.01f)] [SerializeField] private float weightPerUse = 1f;
    [Tooltip("Haznenin ağırlık kapasitesi (kg).")]
    [Min(0.01f)] [SerializeField] private float weightCapacity = 5f;

    [Header("Isı (derece)")]
    [Min(0.01f)] [SerializeField] private float heatPerUse = 20f;
    [Min(0.01f)] [SerializeField] private float heatCapacity = 60f;

    [Header("Ses (0-100)")]
    [Min(0.01f)] [SerializeField] private float soundPerUse = 15f;
    [Min(0.01f)] [SerializeField] private float soundCapacity = 50f;

    [Header("Geri Bildirim")]
    [Tooltip("Ekranda gösterilen mesajın süresi (sn).")]
    [Min(0.5f)] [SerializeField] private float messageDuration = 2f;
    [SerializeField] private bool showHud = true;

    public PropertyKind Mode { get; private set; } = PropertyKind.Weight;
    public bool TankHasItem => tankAmount > 0.0001f;
    public PropertyKind TankKind => tankKind;
    public float TankAmount => tankAmount;
    public float TankCapacity => CapacityOf(tankKind);

    private Transform camT;
    private Transform playerRoot;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private PropertyKind tankKind;
    private float tankAmount;
    private string message;
    private float messageUntil;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        camT = cam != null ? cam.transform : transform;
        var cc = GetComponentInParent<CharacterController>();
        playerRoot = cc != null ? cc.transform : transform.root;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (PhysicsVacuumController.IsAnyHolding) return; // tutarken mod/aktarım kapalı

        if (kb[modeKey].wasPressedThisFrame)
        {
            Mode = (PropertyKind)(((int)Mode + 1) % 3);
            Show("Mod: " + NameOf(Mode));
        }

        if (useMiddleMouse && mouse.middleButton.wasPressedThisFrame)
            Interact();
    }

    // Orta tık: hazne boşsa bakılan nesneden seçili özelliği çeker; doluysa haznedeki özelliği nesneye aktarır.
    private void Interact()
    {
        KitchenObjectProperties target = FindTarget();
        if (target == null)
        {
            Show("Hedef yok (özellik taşıyan bir nesneye bak)");
            return;
        }

        if (!TankHasItem)
        {
            float capacityLeft = CapacityOf(Mode);
            float request = Mathf.Min(PerUseOf(Mode), capacityLeft);
            float taken = target.Take(Mode, request);
            if (taken <= 0.0001f)
            {
                Show(NameOf(Mode) + ": bu nesnede çekilecek değer yok");
                return;
            }
            tankKind = Mode;
            tankAmount = taken;
            Show($"{NameOf(Mode)} çekildi: +{taken:0.0} (hazne {tankAmount:0.0}/{CapacityOf(tankKind):0.0})");
            return;
        }

        // Hazne dolu: haznedeki tür (modu ne olursa olsun) hedefe verilir.
        float give = Mathf.Min(PerUseOf(tankKind), tankAmount);
        float given = target.Give(tankKind, give);
        if (given <= 0.0001f)
        {
            Show(NameOf(tankKind) + ": hedef bunu kabul etmiyor (sınırda)");
            return;
        }
        tankAmount -= given; // yalnızca başarılı aktarımda azalır
        if (tankAmount < 0.0001f) tankAmount = 0f;
        Show($"{NameOf(tankKind)} aktarıldı: -{given:0.0} (hazne {tankAmount:0.0}/{CapacityOf(tankKind):0.0})");
    }

    // Bakış yönünde en yakın, özellik taşıyan nesne (oyuncunun kendisi hariç).
    private KitchenObjectProperties FindTarget()
    {
        int n = Physics.RaycastNonAlloc(camT.position, camT.forward, hits, maxRange, targetMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        KitchenObjectProperties result = null;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c == null || hits[i].distance <= 0f || c.transform.IsChildOf(playerRoot)) continue;
            if (hits[i].distance >= best) continue;
            var props = c.GetComponentInParent<KitchenObjectProperties>();
            if (props == null) continue;
            best = hits[i].distance;
            result = props;
        }
        return result;
    }

    private float PerUseOf(PropertyKind k) => k == PropertyKind.Weight ? weightPerUse : k == PropertyKind.Heat ? heatPerUse : soundPerUse;
    private float CapacityOf(PropertyKind k) => k == PropertyKind.Weight ? weightCapacity : k == PropertyKind.Heat ? heatCapacity : soundCapacity;
    private static string NameOf(PropertyKind k) => k == PropertyKind.Weight ? "Ağırlık" : k == PropertyKind.Heat ? "Isı" : "Ses";

    private void Show(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + messageDuration;
    }

    // ------------------------------------------------------------------ EKRAN KARTI
    // Sol altta PestHudTema kartı: mor başlık şeridinde seçili mod, altında hazne doluluk çubuğu ve kısa ipucu,
    // kartın üstünde son mesaj. Vakum eldeyken görünür (bileşen Tool_2 ile açılıp kapanır).

    private RectTransform hudRoot;
    private TMP_Text hudMode, hudTank, hudMessage;
    private Image hudFill;
    private PestHudTema.Panel hudMessagePlate;
    private string shownMode, shownTank, shownMessage;
    private float shownFill = -1f;

    private void OnEnable()
    {
        if (!showHud) return;
        if (hudRoot == null) BuildHud();
        if (hudRoot != null) hudRoot.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (hudRoot != null) hudRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (hudRoot != null) Destroy(hudRoot.gameObject);
    }

    private void LateUpdate()
    {
        if (hudRoot == null) return;

        string mode = $"<color=#{ColorUtility.ToHtmlStringRGB(PestHudTema.Hardal)}>[T]</color>  {NameOf(Mode)}";
        if (mode != shownMode) { shownMode = mode; hudMode.text = mode; }

        string tank = TankHasItem
            ? $"{NameOf(tankKind)}  <b>{tankAmount:0.0}</b> / {CapacityOf(tankKind):0.0}"
            : "Hazne boş";
        if (tank != shownTank) { shownTank = tank; hudTank.text = tank; }

        float fill = TankHasItem ? Mathf.Clamp01(tankAmount / CapacityOf(tankKind)) : 0f;
        if (!Mathf.Approximately(fill, shownFill))
        {
            shownFill = fill;
            hudFill.fillAmount = fill;
            hudFill.color = KindColor(tankKind);
        }

        string msg = Time.unscaledTime < messageUntil ? message : string.Empty;
        if (msg != shownMessage)
        {
            shownMessage = msg;
            bool show = !string.IsNullOrEmpty(msg);
            hudMessagePlate.kok.gameObject.SetActive(show);
            if (show)
            {
                hudMessage.text = msg;
                hudMessagePlate.kok.sizeDelta = new Vector2(hudMessage.GetPreferredValues(msg).x + 32f, 36f);
            }
        }
    }

    private static Color KindColor(PropertyKind k) =>
        k == PropertyKind.Weight ? PestHudTema.Soluk : k == PropertyKind.Heat ? PestHudTema.Turuncu : PestHudTema.Turkuaz;

    private void BuildHud()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        hudRoot = PestHudTema.Bos("VacuumTankCard", canvas.transform);
        PestHudTema.Yerlestir(hudRoot, Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(330f, 120f));

        var card = PestHudTema.PanelKur(hudRoot, PestHudTema.Kagit);
        var strip = PestHudTema.BaslikSeridi(card, PestHudTema.Mor, 34f);

        var tag = PestHudTema.Yazi("Tag", strip, 14f, PestHudTema.Krem, TextAlignmentOptions.Left);
        tag.fontStyle = FontStyles.Bold;
        tag.characterSpacing = 8f;
        tag.text = "HAZNE";
        PestHudTema.Doldur(tag.rectTransform);
        tag.rectTransform.offsetMin = new Vector2(14f, 0f);

        hudMode = PestHudTema.Yazi("Mode", strip, 20f, PestHudTema.Krem, TextAlignmentOptions.Right);
        PestHudTema.Doldur(hudMode.rectTransform);
        hudMode.rectTransform.offsetMax = new Vector2(-14f, 0f);

        hudTank = PestHudTema.Yazi("Tank", card.Icerik, 20f, PestHudTema.Murekkep, TextAlignmentOptions.Left);
        PestHudTema.Yerlestir(hudTank.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -40f), new Vector2(296f, 26f));

        var track = PestHudTema.Resim("Track", card.Icerik, PestHudTema.Yuvarlak(8), PestHudTema.Murekkep);
        PestHudTema.Yerlestir(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -70f), new Vector2(296f, 14f));
        hudFill = PestHudTema.Resim("Fill", track.rectTransform, null, PestHudTema.Soluk);
        PestHudTema.Doldur(hudFill.rectTransform, 2.5f);
        hudFill.type = Image.Type.Filled;
        hudFill.fillMethod = Image.FillMethod.Horizontal;
        hudFill.fillAmount = 0f;

        var hint = PestHudTema.Yazi("Hint", card.Icerik, 13f, PestHudTema.Soluk, TextAlignmentOptions.Left);
        hint.text = "Orta tık: bakılan nesneden çek / aktar";
        PestHudTema.Yerlestir(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -88f), new Vector2(296f, 16f));

        hudMessagePlate = PestHudTema.YeniPanel("Message", hudRoot, PestHudTema.Krem, 3f, 4f, 12);
        PestHudTema.Yerlestir(hudMessagePlate.kok, new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 12f), new Vector2(200f, 36f));
        hudMessage = PestHudTema.Yazi("Text", hudMessagePlate.Icerik, 18f, PestHudTema.Murekkep, TextAlignmentOptions.Center);
        PestHudTema.Doldur(hudMessage.rectTransform);
        hudMessagePlate.kok.gameObject.SetActive(false);
    }
}
