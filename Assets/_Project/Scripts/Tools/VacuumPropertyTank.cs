using UnityEngine;
using UnityEngine.InputSystem;

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
    private GUIStyle labelStyle;

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

    // Basit ekran yazısı: seçili mod, hazne durumu ve son mesaj (sol alt).
    private void OnGUI()
    {
        if (!showHud) return;
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;
        }
        float x = 20f;
        float y = Screen.height - 110f;
        string tank = TankHasItem
            ? $"Hazne: {NameOf(tankKind)} {tankAmount:0.0}/{CapacityOf(tankKind):0.0}"
            : "Hazne: boş";
        GUI.Label(new Rect(x, y, 420, 24), $"Mod: {NameOf(Mode)}  (T ile değiştir)", labelStyle);
        GUI.Label(new Rect(x, y + 26, 420, 24), tank + "  (orta tık: çek / aktar)", labelStyle);
        if (Time.unscaledTime < messageUntil)
            GUI.Label(new Rect(x, y + 52, 600, 24), message, labelStyle);
    }
}
