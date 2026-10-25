using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum BugCageState
{
    Intact,  // Sağlam (varsayılan)
    Broken   // Kırık: yaratık kabul etmez, satış/besleme yok; yalnızca onarılabilir
}

// Kamyondaki cam fanus rafının bir bölmesi. Her bölme tek bir böcek türü içindir: bölmeye bakıp E'ye basınca
// vakumun üstündeki kapsülden o türün baygın böcekleri bölmenin içine bırakılır; cam kaçmalarına izin vermez.
// Tür boşsa bölme, içine ilk bırakılan türü sahiplenir (boşalınca serbest kalır).
// Bölmedeki böcekler BugInventory'ye girmez; satışı raf (BugShelf) yapar.
// Bu nesnede bölmenin iç hacmini kaplayan "Is Trigger" açık bir BoxCollider olmalıdır. E ile etkileşim için
// Interactable layer'ında ayrı bir çocuk collider gerekir (PlayerInteraction onun üstünden bu bileşeni bulur).
[RequireComponent(typeof(BoxCollider))]
public class BugCage : MonoBehaviour, IInteractable
{
    [Tooltip("Bu bölmenin böcek türü. Boşsa içine ilk bırakılan tür bölmeyi sahiplenir.")]
    [SerializeField] private BugData bugType;
    [SerializeField, Min(1)] private int capacity = 4;
    [Tooltip("Bölmenin isim/sayaç yazısı (boş bırakılabilir).")]
    [SerializeField] private TMP_Text label;

    [Header("Yaratık boyutu (gösterilen büyüme)")]
    [Tooltip("Fanus içinde yaratığın gösterilebileceği en büyük boyut katı. Gerçek sınır ayrıca fanusun fiziksel " +
             "iç ölçülerinden (cam halkası, zemin, kapak) hesaplanır; ikisinden küçüğü geçerlidir.")]
    [SerializeField, Min(1f)] private float safeDisplayGrowth = 1.10f;
    [Tooltip("Yaratık ile cam/kapak arasında bırakılacak boşluk (m).")]
    [SerializeField, Min(0f)] private float interiorClearance = 0.01f;

    [Header("Kırılma ve onarım (4D)")]
    [Tooltip("Kırılınca kapanacak cam görselleri. Boşsa çocuklardan adı \"Cam\" olan renderer'lar bulunur.")]
    [SerializeField] private Renderer[] glassRenderers;
    [Tooltip("Kırılınca kapanacak cam duvarı/kapak collider'ları (trigger değil). Boşsa çocuklardan adı \"Duvar\" ya da \"Kapak\" olanlar bulunur. Zemin açık kalır.")]
    [SerializeField] private Collider[] breachColliders;
    [Tooltip("İsteğe bağlı: kırılma anında fanusun konumunda oluşturulan parçacık efekti.")]
    [SerializeField] private GameObject breachVfxPrefab;
    [SerializeField, Min(0)] private int repairCost = 50;
    [Tooltip("Kırılınca yaratıkların ışınlandığı nokta (fanusun YEREL ekseninde). X: açık yüze uzaklık (işareti yönü belirler; " +
             "sahnede açık yüz yerel -X), Y: yükseklik. Büyük yaratıklar için gövde yarı genişliği kadar ek uzaklık otomatik eklenir.")]
    [SerializeField] private Vector3 breachEscapeOffset = new Vector3(-0.3f, 0.2f, 0f);
    [Tooltip("Kırılınca yaratıkların fanus ekseninden dışarı aldığı hız (m/sn).")]
    [SerializeField, Min(0f)] private float breachPushOut = 1.8f;
    [Tooltip("Kırılınca yaratıkların yukarı aldığı hız (m/sn).")]
    [SerializeField, Min(0f)] private float breachPushUp = 2.2f;

    private readonly List<Renderer> disabledRenderers = new List<Renderer>();
    private readonly List<Collider> disabledColliders = new List<Collider>();
    private CageCriticalController critical;
    private BugCageState cageState = BugCageState.Intact; // yalnızca çalışma anında değişir

    private readonly HashSet<CageableBug> inside = new HashSet<CageableBug>();
    private readonly List<CageableBug> caged = new List<CageableBug>();
    private BoxCollider area;
    private VacuumCapsule capsule;
    private CageInspectPanel inspectPanel;
    private bool claimed; // tür bu oyunda sahiplenildi (Inspector'dan atanmadı)

    // Bölmenin içeriği değişince tetiklenir: yaratık eklendi/çıktı/satıldı/yok oldu ya da içerideki biri beslenip
    // büyüdü (CagePressureController baskıyı bununla yeniden hesaplar).
    public event System.Action ContentsChanged;

    public BugData BugType => bugType;
    public int Count => caged.Count;
    public int Capacity => capacity;
    // İçerideki böcekler (cama vurma efekti okur).
    public IReadOnlyList<CageableBug> Caged => caged;
    public BugCageState State => cageState;
    public bool IsBroken => cageState == BugCageState.Broken;
    public int RepairCost => repairCost;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        area = GetComponent<BoxCollider>();
        critical = GetComponent<CageCriticalController>();
    }

    private void OnEnable()
    {
        if (critical != null) critical.OnBreachRequested += HandleBreachRequested;
    }

    private void OnDisable()
    {
        if (critical != null) critical.OnBreachRequested -= HandleBreachRequested;
    }

    private void Start()
    {
        capsule = FindFirstObjectByType<VacuumCapsule>(FindObjectsInactive.Include);
        UpdateLabel();
    }

    public string GetPrompt()
    {
        if (IsBroken) return "[E] Fanusu Onar — $" + repairCost;

        // Aktarılacak bir şey yoksa ve fanusta yaratık varsa E incelemeyi açar/kapatır.
        if (!CanTransfer() && caged.Count > 0)
            return "[E] Fanusu İncele (" + caged.Count + "/" + capacity + ")";

        if (capsule == null) return TypeName();

        if (bugType == null)
        {
            BugData first = capsule.FirstType();
            return first != null ? "E - Boş bölmeye koy (" + first.bugName + ")" : "Boş bölme";
        }

        int matching = capsule.CountOf(bugType);
        if (matching > 0)
            return caged.Count >= capacity
                ? bugType.bugName + " bölmesi dolu (" + capacity + ")"
                : "E - Kapsüldekileri koy (" + matching + " " + bugType.bugName + ")";
        if (capsule.Count > 0) return "Bu bölme sadece " + bugType.bugName + " için";
        return bugType.bugName + "  " + caged.Count + "/" + capacity;
    }

    public void Interact(PlayerInteraction oyuncu)
    {
        if (IsBroken)
        {
            TryRepair();
            return;
        }

        if (capsule == null) capsule = FindFirstObjectByType<VacuumCapsule>(FindObjectsInactive.Include);

        // Kapsülden aktarılacak bir şey yoksa: fanusta yaratık varsa inceleme paneli açılır.
        if (!CanTransfer())
        {
            if (caged.Count > 0) OpenInspect();
            return;
        }

        if (bugType == null)
        {
            bugType = capsule.FirstType();
            if (bugType == null) return;
            claimed = true;
        }

        // Baygın böcek bölmenin genişliği boyunca yatırılır (derinlik dar, cama gömülmesin).
        List<CageableBug> bugs = capsule.Take(bugType, capacity - caged.Count);
        float halfX = Mathf.Max(0f, area.size.x * 0.5f - 0.2f);
        float halfZ = Mathf.Max(0f, area.size.z * 0.5f - 0.1f);
        for (int i = 0; i < bugs.Count; i++)
        {
            Vector3 local = area.center + new Vector3(Random.Range(-halfX, halfX), 0f, Random.Range(-halfZ, halfZ));
            float yaw = transform.eulerAngles.y + (Random.value < 0.5f ? 90f : -90f) + Random.Range(-15f, 15f);
            bugs[i].SnapDisplayFor(this); // fanusa mantıksal boyutta değil, güvenli gösterim boyutunda girer
            bugs[i].ReleaseAt(transform.TransformPoint(local), yaw);
        }
        UpdateLabel();
    }

    // Kapsülde bu bölmeye girebilecek böcek var ve yer var mı? (Interact'in eski aktarma koşulu.)
    private bool CanTransfer()
    {
        if (IsBroken || capsule == null || caged.Count >= capacity) return false;
        if (bugType == null) return capsule.FirstType() != null;
        return capsule.CountOf(bugType) > 0;
    }

    // Sahnedeki inceleme panelini bulur ve bu bölme için açar. Panel sahnede yoksa hata verir (kendisi kurulmaz).
    private void OpenInspect()
    {
        if (inspectPanel == null) inspectPanel = FindFirstObjectByType<CageInspectPanel>(FindObjectsInactive.Include);
        if (inspectPanel == null)
        {
            Debug.LogError("BugCage: Sahnede CageInspectPanel bileşeni yok. Fanus inceleme paneli açılamadı.", this);
            return;
        }
        inspectPanel.Open(this);
    }

    // Verilen noktanın bölmenin iç hacmine uzaklığı (panel uzaklaşınca kapansın diye).
    public float DistanceFrom(Vector3 point)
    {
        return Vector3.Distance(point, area.ClosestPoint(point));
    }

    // Panelin okuyacağı, bir yaratığın salt okunur özeti.
    public struct CreatureInfo
    {
        public string name;          // "Hamam Böceği #1"
        public string species;       // BugData.bugName
        public BugState state;
        public float baseValue;
        public float valueMultiplier;
        public float saleValue;      // TotalValue'daki formülle aynı
        public float growth;         // MANTIKSAL büyüme katı (gerçek büyüklük)
        public float displayGrowth;  // fanus içinde gösterilen büyüme katı
        public CageableBug bug;      // besleme gibi işlemlerin hedefi (yaratığın kendisi)
    }

    // Bu yaratık şu an bu bölmede mi?
    public bool Contains(CageableBug bug)
    {
        return bug != null && caged.Contains(bug);
    }

    // Fanusun fiziksel iç ölçüleri: tetikleyiciden değil, cam halkasını oluşturan, zemin ve kapak collider'larından.
    private struct Interior
    {
        public bool valid;
        public float radius;   // en yakın cam yüzeyine yatay mesafe (m)
        public float height;   // zemin üstü ile kapak altı arası (m)
    }

    private Interior interior;
    private bool interiorComputed;

    public float InteriorRadius { get { EnsureInterior(); return interior.radius; } }
    public float InteriorHeight { get { EnsureInterior(); return interior.height; } }

    // Tetikleyici olmayan alt collider'ları gezer: yassı levhalar zemin/kapak, diğerleri cam duvardır.
    // Fanus yalnızca Y ekseninde dönük varsayılır.
    private void EnsureInterior()
    {
        if (interiorComputed) return;
        interiorComputed = true;

        float midY = area.center.y;
        float floorTop = float.NegativeInfinity, lidBottom = float.PositiveInfinity, radius = float.PositiveInfinity;

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger || !col.enabled) continue;

            Vector3 center = transform.InverseTransformPoint(col.bounds.center);
            Vector3 ext = col.bounds.extents;
            bool flat = ext.y < Mathf.Min(ext.x, ext.z) * 0.5f;
            if (flat)
            {
                if (center.y < midY) floorTop = Mathf.Max(floorTop, center.y + ext.y);
                else lidBottom = Mathf.Min(lidBottom, center.y - ext.y);
                continue;
            }

            Vector3 axisPoint = transform.TransformPoint(new Vector3(0f, center.y, 0f));
            Vector3 local = transform.InverseTransformPoint(col.ClosestPoint(axisPoint));
            radius = Mathf.Min(radius, new Vector2(local.x, local.z).magnitude);
        }

        interior.valid = !float.IsInfinity(floorTop) && !float.IsInfinity(lidBottom) && !float.IsInfinity(radius) && lidBottom > floorTop;
        interior.radius = radius;
        interior.height = lidBottom - floorTop;
        if (!interior.valid)
            Debug.LogWarning("BugCage: Fanusun fiziksel iç ölçüleri bulunamadı (cam, zemin, kapak collider'ı yok); yaratık büyütülmeyecek.", this);
    }

    // Verilen yaratığın bu fanusta gösterilebileceği en büyük boyut katı: Inspector sınırı ile fiziksel sığmanın
    // küçüğü. Gövde ölçüsü yaratığın dik duran collider'ıdır; hem ayaktayken (yükseklik) hem baygın yatarken
    // (dikdörtgenin köşeleri cam yarıçapında) sığmalıdır.
    public float GetDisplayGrowthLimit(CageableBug bug)
    {
        EnsureInterior();
        if (!interior.valid || bug == null) return 1f;

        Vector3 body = bug.BaseBodySize;
        if (body.x <= 0.0001f || body.y <= 0.0001f || body.z <= 0.0001f) return 1f;

        float usableRadius = Mathf.Max(0f, interior.radius - interiorClearance);
        float usableHeight = Mathf.Max(0f, interior.height - interiorClearance);

        float standing = usableHeight / body.y;                                                    // ayaktayken boy
        float standingFootprint = usableRadius / (0.5f * Mathf.Sqrt(body.x * body.x + body.z * body.z)); // ayak izi köşegeni
        float lying = usableRadius / (0.5f * Mathf.Sqrt(body.y * body.y + body.x * body.x));      // yatarken (boy x genişlik)
        float lyingHeight = usableHeight / Mathf.Max(body.x, body.z);                              // yatarken kalınlık

        float physical = Mathf.Min(Mathf.Min(standing, standingFootprint), Mathf.Min(lying, lyingHeight));
        return Mathf.Max(1f, Mathf.Min(safeDisplayGrowth, physical));
    }

    // Bölmenin iç hacmi (dünya ölçüsü, m): yaratığın sığabileceği alan.
    public Vector3 InnerSize
    {
        get
        {
            Vector3 s = transform.lossyScale;
            return new Vector3(area.size.x * Mathf.Abs(s.x), area.size.y * Mathf.Abs(s.y), area.size.z * Mathf.Abs(s.z));
        }
    }

    // Bölmedeki yaratıkların özetini listeye doldurur (liste önce temizlenir).
    public void GetCreatureInfos(List<CreatureInfo> result)
    {
        result.Clear();
        foreach (CageableBug bug in caged)
        {
            if (bug == null || bug.Data == null) continue;

            float sale = CreatureValue(bug);
            if (float.IsNaN(sale) || float.IsInfinity(sale)) sale = 0f;
            result.Add(new CreatureInfo
            {
                name = bug.Data.bugName + " #" + (result.Count + 1),
                species = bug.Data.bugName,
                state = bug.State,
                baseValue = bug.Data.baseValue,
                valueMultiplier = bug.ValueMultiplier,
                saleValue = sale,
                growth = bug.GrowthMultiplier,
                displayGrowth = bug.DisplayGrowth,
                bug = bug
            });
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var bug = other.GetComponentInParent<CageableBug>();
        if (bug != null) inside.Add(bug);
    }

    private void OnTriggerExit(Collider other)
    {
        var bug = other.GetComponentInParent<CageableBug>();
        if (bug == null) return;
        inside.Remove(bug);
        Release(bug);
    }

    private void FixedUpdate()
    {
        // Kapsüle alınan (kapanan) ya da yok olan böcek bölmeden düşer.
        inside.RemoveWhere(b => b == null || !b.gameObject.activeInHierarchy);
        for (int i = caged.Count - 1; i >= 0; i--)
        {
            CageableBug bug = caged[i];
            if (bug != null && bug.gameObject.activeInHierarchy) continue;
            if (bug != null)
            {
                bug.GrowthChanged -= OnCagedBugGrowthChanged;
                bug.Cage = null;
            }
            caged.RemoveAt(i);
            UpdateLabel();
            ContentsChanged?.Invoke();
        }

        if (IsBroken) return; // kırık fanus yeni yaratık almaz

        foreach (CageableBug bug in inside)
        {
            if (caged.Count >= capacity) break;
            if (bug.Cage != null || bug.IsEscaped) continue; // kaçan yaratık kapsülden geçmeden bir fanusa girmez
            caged.Add(bug);
            bug.Cage = this;
            bug.GrowthChanged += OnCagedBugGrowthChanged;
            UpdateLabel();
            ContentsChanged?.Invoke();
        }
    }

    // Bölmedeki bir yaratık beslenip büyüdü (baskı yeniden hesaplanır).
    private void OnCagedBugGrowthChanged(CageableBug bug)
    {
        ContentsChanged?.Invoke();
    }

    private void Release(CageableBug bug)
    {
        if (!caged.Remove(bug)) return;
        bug.GrowthChanged -= OnCagedBugGrowthChanged;
        bug.Cage = null;
        UpdateLabel();
        ContentsChanged?.Invoke();
    }

    // Bir bölmenin döndürebileceği en büyük değer (BugShelf toplamı int'e yuvarlar; 16 bölmede bile taşmaz).
    private const float MaxTotalValue = 100000000f;

    // Tek bir yaratığın satış değeri (TotalValue ve inceleme paneli bunu kullanır): baseValue, Horror ise
    // dangerMultiplier, sonra yaratığın kendi ValueMultiplier'ı ile çarpılır. Data'sı null olmamalı.
    private static float CreatureValue(CageableBug bug)
    {
        float value = bug.Data.baseValue;
        if (bug.State == BugState.Horror) value *= bug.Data.dangerMultiplier;
        return value * bug.ValueMultiplier;
    }

    // Bölmedeki böceklerin satış değeri: baseValue, Horror ise dangerMultiplier ile, her böceğin kendi
    // ValueMultiplier'ı ile çarpılır (çarpan 1 iken eski değerle birebir aynı).
    public float TotalValue()
    {
        float total = 0f;
        foreach (CageableBug bug in caged)
        {
            if (bug == null || bug.Data == null) continue;
            float value = CreatureValue(bug);
            if (float.IsNaN(value) || float.IsInfinity(value)) continue;
            total = Mathf.Min(total + value, MaxTotalValue);
        }
        return total;
    }

    // Raf satış yapınca çağırır: böcekleri yok eder, satılan adedi döner. Para işini raf yapar.
    // Satış sırasında true (aynı bölmede iç içe/çifte satış işlemi başlamasın diye). Yalnızca SellContents açar.
    public bool IsSelling { get; private set; }

    public int SellContents()
    {
        if (IsSelling || IsBroken) return 0;
        IsSelling = true;

        int count = 0;
        foreach (CageableBug bug in caged)
        {
            if (bug == null) continue;
            bug.GrowthChanged -= OnCagedBugGrowthChanged;
            inside.Remove(bug);
            bug.gameObject.SetActive(false); // Destroy kare sonunda işler; o ana kadar çarpışma/etkileşim olmasın
            Destroy(bug.gameObject);
            count++;
        }
        caged.Clear();
        if (claimed)
        {
            bugType = null;
            claimed = false;
        }
        UpdateLabel();
        IsSelling = false;
        ContentsChanged?.Invoke();
        return count;
    }

    #region Kırılma ve onarım

    private void HandleBreachRequested(BugCage breached)
    {
        Break();
    }

    // Kritik sayaç bitince bir kez çalışır: cam ve duvar kapanır, yaratıklar serbest kalır.
    private void Break()
    {
        if (IsBroken) return;

        EnsureInterior(); // iç ölçüler, cam kapanmadan önce hesaplanıp saklanır
        CollectBreakParts();
        cageState = BugCageState.Broken;

        foreach (Renderer r in glassRenderers)
            if (r != null && r.enabled)
            {
                r.enabled = false;
                disabledRenderers.Add(r);
            }
        foreach (Collider c in breachColliders)
            if (c != null && c.enabled)
            {
                c.enabled = false;
                disabledColliders.Add(c);
            }

        foreach (Collider c in breachColliders)
            if (c != null && c.enabled)
                Debug.LogWarning("BugCage: Kırılma sonrası collider hâlâ açık: " + c.name, c);

        if (breachVfxPrefab != null)
            Destroy(Instantiate(breachVfxPrefab, transform.position + transform.up * 0.3f, transform.rotation), 6f);

        ReleaseAllBugs();
        UpdateLabel();
        ContentsChanged?.Invoke();
    }

    // Serbest bırakılan yaratıklar fanustan ayrılır, mantıksal boyutuna açılır ve dışarı/yukarı itilir.
    private void ReleaseAllBugs()
    {
        var released = new List<CageableBug>(caged);
        caged.Clear();
        if (claimed)
        {
            bugType = null;
            claimed = false;
        }

        // Çıkış yönü: yerel offset'in yatay bileşeni (sahnede fanusun açık yüzü yerel -X, yani dünya +X).
        Vector3 front = transform.TransformDirection(new Vector3(breachEscapeOffset.x, 0f, 0f)).normalized;
        if (front.sqrMagnitude < 0.0001f) front = transform.forward;

        for (int i = 0; i < released.Count; i++)
        {
            CageableBug bug = released[i];
            if (bug == null) continue;
            bug.GrowthChanged -= OnCagedBugGrowthChanged;
            bug.Cage = null;
            if (!bug.gameObject.activeInHierarchy) continue;

            // Büyümüş yaratığın gövdesi çıkış noktasında da cama/rafa gömülmesin: yarım ayak izi kadar ek uzaklık.
            Vector3 body = bug.BaseBodySize * bug.GrowthMultiplier;
            float halfFootprint = 0.5f * Mathf.Max(body.x, body.z);
            float side = released.Count > 1 ? (i - (released.Count - 1) * 0.5f) * 0.4f : 0f; // birden çok yaratık üst üste binmesin
            Vector3 escapePos = transform.TransformPoint(new Vector3(0f, breachEscapeOffset.y, side))
                                + front * (Mathf.Abs(breachEscapeOffset.x) + halfFootprint);

            StartCoroutine(TeleportAndPush(bug, escapePos, front * breachPushOut + transform.up * breachPushUp));
        }
    }

    // Yaratığı tek kareliğine kinematik yapıp güvenli çıkış noktasına ışınlar (overlap'ten kurtarır), sonra fırlatır.
    private IEnumerator TeleportAndPush(CageableBug bug, Vector3 position, Vector3 velocity)
    {
        Rigidbody rb = bug.GetComponent<Rigidbody>();
        bug.Escape(Vector3.zero); // kaçak bayrağı, uyanma, hız sıfırlama
        rb.isKinematic = true;
        bug.transform.position = position;
        Physics.SyncTransforms();

        yield return new WaitForFixedUpdate();

        if (bug == null) yield break;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(velocity, ForceMode.VelocityChange);
    }

    // Bulunmayan parça listeleri için çocuklardan ad ile toplar (sahne düzeni: Cam, Duvar x12, Kapak).
    private void CollectBreakParts()
    {
        if (glassRenderers == null || glassRenderers.Length == 0)
        {
            var list = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name == "Cam") list.Add(r);
            glassRenderers = list.ToArray();
        }
        if (breachColliders == null || breachColliders.Length == 0)
        {
            var list = new List<Collider>();
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && (c.gameObject.name == "Duvar" || c.gameObject.name == "Kapak")) list.Add(c);
            breachColliders = list.ToArray();
        }
    }

    // Broken iken E: parası yetiyorsa onarır (GameManager.SpendMoney), yetmiyorsa telsizle haber verir.
    private void TryRepair()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.SpendMoney(repairCost))
        {
            if (RadioManager.Instance != null)
                RadioManager.Instance.PlayRadioSubtitle("Onarım için $" + repairCost + " gerekli!");
            return;
        }
        Repair();
    }

    private void Repair()
    {
        foreach (Renderer r in disabledRenderers)
            if (r != null) r.enabled = true;
        foreach (Collider c in disabledColliders)
            if (c != null) c.enabled = true;
        disabledRenderers.Clear();
        disabledColliders.Clear();

        cageState = BugCageState.Intact;
        UpdateLabel();
        ContentsChanged?.Invoke(); // baskı 0'a ve Safe'e döner
        if (critical != null) critical.ResetAfterBreach();
    }

    #endregion

    private string TypeName()
    {
        return bugType != null ? bugType.bugName : "Boş bölme";
    }

    private void UpdateLabel()
    {
        if (label == null) return;
        label.text = bugType != null ? bugType.bugName + "  " + caged.Count + "/" + capacity : "Boş bölme";
    }
}
