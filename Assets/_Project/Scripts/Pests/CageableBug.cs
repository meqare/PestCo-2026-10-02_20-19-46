using UnityEngine;

// Kafese konabilen böcek. Elle tutulmaz: sprey (IDamageable) bayıltır, bayılmışken vakum çekip
// vakumun üstündeki kapsüle (VacuumCapsule) alır, kafese bakıp E ile kafese bırakılır.
// Uyanıkken arada bir zıplar; bayılınca sırt üstü yatıp seğirir, bir süre sonra uyanır.
[RequireComponent(typeof(Rigidbody))]
public class CageableBug : MonoBehaviour, IDamageable
{
    [Header("Veri")]
    [SerializeField] private BugData data;
    [SerializeField] private BugState state = BugState.Cute;
    [Tooltip("Model (kapsülde küçük kopyası gösterilir). Boşsa ilk çocuk.")]
    [SerializeField] private Transform visual;

    [Header("Bayılma")]
    [Tooltip("Bayılması için gereken toplam sprey hasarı.")]
    [SerializeField, Min(1f)] private float stunHealth = 30f;
    [Tooltip("Baygın kalma süresi (sn).")]
    [SerializeField, Min(0.5f)] private float stunDuration = 12f;
    [Tooltip("Kafese bırakıldıktan sonra uyanma süresi (sn).")]
    [SerializeField, Min(0f)] private float wakeAfterRelease = 2f;

    [Header("Kıpırdanma")]
    [Tooltip("Zıplama hızı (m/sn). 0 = hiç kıpırdamaz.")]
    [SerializeField, Min(0f)] private float hopSpeed = 1.6f;
    [Tooltip("İki zıplama arasındaki rastgele süre aralığı (sn).")]
    [SerializeField] private Vector2 hopInterval = new Vector2(1.5f, 4f);

    // Vakum bu kadar süre çekmezse fizik geri açılır.
    private const float PullTimeout = 0.15f;

    public BugData Data => data;
    public BugState State => state;
    public Transform Visual => visual;
    public bool IsStunned { get; private set; }
    // Sprey baygın böceğe tekrar hasar vermesin.
    public bool IsAlive => !IsStunned;
    // Kafes doldurur; kafeste sayılıyorsa o kafes, değilse null.
    public BugCage Cage { get; set; }

    private Rigidbody rb;
    private BoxCollider box;
    private float health;
    private float stunEnd;
    private float nextHop;
    private float lastPullTime = -1f;
    private bool pulled;

    private Vector3 standVisualPos;
    private Quaternion standVisualRot;
    private Vector3 standBoxCenter;
    private Vector3 standBoxSize;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        box = GetComponent<BoxCollider>();
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual != null)
        {
            standVisualPos = visual.localPosition;
            standVisualRot = visual.localRotation;
        }
        if (box != null)
        {
            standBoxCenter = box.center;
            standBoxSize = box.size;
        }
        health = stunHealth;
        rb.maxDepenetrationVelocity = 1f; // cama gömülürse fırlayıp dışarı kaçmasın
        ScheduleHop();
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (IsStunned) return;
        health -= amount;
        if (health <= 0f) Stun();
    }

    private void Update()
    {
        if (!IsStunned) return;

        if (Time.time >= stunEnd && !pulled)
        {
            Wake();
            return;
        }

        // Sırt üstü yatarken seğirme
        if (visual != null)
            visual.localRotation = LyingRotation() * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 18f) * 6f);
    }

    private void FixedUpdate()
    {
        if (pulled && Time.time - lastPullTime > PullTimeout)
        {
            pulled = false;
            rb.isKinematic = false;
        }

        if (IsStunned || pulled || hopSpeed <= 0f || Time.time < nextHop) return;
        ScheduleHop();
        if (rb.linearVelocity.sqrMagnitude > 0.05f) return; // havada / kayarken zıplamasın

        Vector2 side = Random.insideUnitCircle * 0.5f;
        rb.AddForce(new Vector3(side.x, 1f, side.y) * hopSpeed, ForceMode.VelocityChange);
        rb.AddTorque(Vector3.up * Random.Range(-4f, 4f), ForceMode.VelocityChange);
    }

    // Vakum her karede çağırır: böcek fiziği kapatılıp oyuncuya doğru kaydırılır.
    public void VacuumPull(Vector3 delta)
    {
        if (!pulled)
        {
            pulled = true;
            rb.isKinematic = true;
        }
        lastPullTime = Time.time;
        transform.position += delta;
    }

    // Kapsüle alınınca sahneden gizlenir (baygınlık süresi işlemez).
    public void OnStoredInCapsule()
    {
        Cage = null;
        pulled = false;
        rb.isKinematic = false;
        gameObject.SetActive(false);
    }

    // Kapsülden kafese bırakılır: baygın düşer, kısa süre sonra uyanır.
    public void ReleaseAt(Vector3 position)
    {
        ReleaseAt(position, Random.Range(0f, 360f));
    }

    // yaw: baygın gövdenin uzanacağı yön (dar bölmede cama gömülmesin diye bölme verir).
    public void ReleaseAt(Vector3 position, float yaw)
    {
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        gameObject.SetActive(true);
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        if (!IsStunned) Stun();
        stunEnd = Time.time + wakeAfterRelease;
    }

    private void Stun()
    {
        IsStunned = true;
        stunEnd = Time.time + stunDuration;

        // Dik duran model sırt üstü yatar: model ve collider yere uzanır, gövde pivotun ortasında kalır.
        float halfDepth = standBoxSize.z * 0.5f;
        float halfLength = standBoxSize.y * 0.5f;
        if (visual != null)
        {
            visual.localRotation = LyingRotation();
            visual.localPosition = standVisualPos + new Vector3(0f, halfDepth, halfLength);
        }
        if (box != null)
        {
            box.center = new Vector3(standBoxCenter.x, halfDepth, 0f);
            box.size = new Vector3(standBoxSize.x, standBoxSize.z, standBoxSize.y);
        }
    }

    private void Wake()
    {
        IsStunned = false;
        health = stunHealth;
        if (visual != null)
        {
            visual.localRotation = standVisualRot;
            visual.localPosition = standVisualPos;
        }
        if (box != null)
        {
            box.center = standBoxCenter;
            box.size = standBoxSize;
        }
        rb.AddForce(Vector3.up * hopSpeed, ForceMode.VelocityChange); // ayağa fırlar
        ScheduleHop();
    }

    private Quaternion LyingRotation()
    {
        return Quaternion.Euler(-90f, 0f, 0f) * standVisualRot;
    }

    private void ScheduleHop()
    {
        nextHop = Time.time + Random.Range(hopInterval.x, hopInterval.y);
    }
}
