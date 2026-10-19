using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

    private readonly HashSet<CageableBug> inside = new HashSet<CageableBug>();
    private readonly List<CageableBug> caged = new List<CageableBug>();
    private BoxCollider area;
    private VacuumCapsule capsule;
    private bool claimed; // tür bu oyunda sahiplenildi (Inspector'dan atanmadı)

    public BugData BugType => bugType;
    public int Count => caged.Count;
    public int Capacity => capacity;
    // İçerideki böcekler (cama vurma efekti okur).
    public IReadOnlyList<CageableBug> Caged => caged;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        area = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        capsule = FindFirstObjectByType<VacuumCapsule>(FindObjectsInactive.Include);
        UpdateLabel();
    }

    public string GetPrompt()
    {
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
        if (capsule == null) capsule = FindFirstObjectByType<VacuumCapsule>(FindObjectsInactive.Include);
        if (capsule == null) return;

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
            bugs[i].ReleaseAt(transform.TransformPoint(local), yaw);
        }
        UpdateLabel();
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
            if (bug != null) bug.Cage = null;
            caged.RemoveAt(i);
            UpdateLabel();
        }

        foreach (CageableBug bug in inside)
        {
            if (caged.Count >= capacity) break;
            if (bug.Cage != null) continue;
            caged.Add(bug);
            bug.Cage = this;
            UpdateLabel();
        }
    }

    private void Release(CageableBug bug)
    {
        if (!caged.Remove(bug)) return;
        bug.Cage = null;
        UpdateLabel();
    }

    // Bölmedeki böceklerin satış değeri: baseValue, Horror ise dangerMultiplier ile çarpılır.
    public float TotalValue()
    {
        float total = 0f;
        foreach (CageableBug bug in caged)
        {
            if (bug == null || bug.Data == null) continue;
            float value = bug.Data.baseValue;
            if (bug.State == BugState.Horror) value *= bug.Data.dangerMultiplier;
            total += value;
        }
        return total;
    }

    // Raf satış yapınca çağırır: böcekleri yok eder, satılan adedi döner. Para işini raf yapar.
    public int SellContents()
    {
        int count = 0;
        foreach (CageableBug bug in caged)
        {
            if (bug == null) continue;
            inside.Remove(bug);
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
        return count;
    }

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
