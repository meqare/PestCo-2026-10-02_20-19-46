using System.Collections;
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
    [Tooltip("Bu yaratığa özel satış değeri çarpanı (1 = normal). 1'in altı ve geçersiz sayılar 1 sayılır.")]
    [SerializeField] private float valueMultiplier = 1f;
    [Tooltip("Beslenme büyümesi: sahnedeki boyutunun kaç katı (1 = hiç büyümemiş). Besleme artırır.")]
    [SerializeField, Min(1f)] private float growthMultiplier = 1f;
    [Tooltip("Gösterilen boyut değişirken (fanusa girince/çıkınca) geçiş süresi (sn).")]
    [SerializeField, Min(0f)] private float displayTransitionSeconds = 0.5f;
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

    [Header("Kaçak saldırısı (fanus kırılınca)")]
    [Tooltip("Kaçtıktan sonra bu kadar saniye oyuncuyu kovalar, sonra bırakır (sn).")]
    [SerializeField, Min(0f)] private float rampageSeconds = 12f;
    [Tooltip("Kovalarken oyuncu bu mesafeden uzaklaşırsa kovalamayı bırakır (m).")]
    [SerializeField, Min(0f)] private float chaseRange = 12f;
    [Tooltip("Kovalarken yatay zıplama hızı (m/sn).")]
    [SerializeField, Min(0f)] private float chaseSpeed = 3.5f;
    [Tooltip("Kovalarken iki zıplama arasındaki rastgele süre (sn).")]
    [SerializeField] private Vector2 chaseInterval = new Vector2(0.35f, 0.7f);
    [Tooltip("Oyuncuya bu kadar yaklaşınca daha hızlı, hedefli atılır (m).")]
    [SerializeField, Min(0f)] private float lungeRange = 2f;
    [SerializeField, Min(0f)] private float lungeSpeed = 5f;
    [Tooltip("Çarpınca oyuncuya verilen hasar (PlayerStatus). Bekleme süresi 1 sn.")]
    [SerializeField, Min(0f)] private float attackDamage = 5f;

    [Header("Kaçak sakinleşince (kovalamayı bıraktıktan sonra)")]
    [Tooltip("Oyuncu bu mesafeye yaklaşırsa zigzaglı kaçar (m); uzaktaysa rastgele zıplar.")]
    [SerializeField, Min(0f)] private float calmFleeRange = 6f;
    [SerializeField, Min(0f)] private float calmFleeSpeed = 2.5f;
    [SerializeField] private Vector2 calmInterval = new Vector2(0.5f, 1.1f);
    [Tooltip("Kaçarken her zıplamada ±bu açı kadar sapar (derece).")]
    [SerializeField, Range(0f, 80f)] private float zigzagAngle = 35f;
    [Tooltip("Kovalamayı bırakınca bir kez telsiz uyarısı + kısa korkutucu ses + hafif sarsıntı.")]
    [SerializeField] private string fearMessage = "Şefim, o şey hâlâ dışarıda bir yerde...";
    [SerializeField, Min(0f)] private float fearShake = 0.03f;

    // Vakum bu kadar süre çekmezse fizik geri açılır.
    private const float PullTimeout = 0.15f;

    public BugData Data => data;
    public BugState State => state;
    public Transform Visual => visual;
    // Bu yaratığın satış çarpanı: hiçbir zaman 1'in altına düşmez; NaN/Infinity 1 sayılır.
    public float ValueMultiplier => float.IsNaN(valueMultiplier) || float.IsInfinity(valueMultiplier)
        ? 1f
        : Mathf.Max(1f, valueMultiplier);
    // MANTIKSAL büyüme (yaratığın gerçek büyüklüğü, >= 1). Beslemeler bunu artırır; değer ve ileride fanus
    // baskısı buna bağlıdır. Ekranda görünen boyut için DisplayGrowth'a bakın.
    public float GrowthMultiplier => float.IsNaN(growthMultiplier) || float.IsInfinity(growthMultiplier)
        ? 1f
        : Mathf.Max(1f, growthMultiplier);
    public float LogicalGrowth => GrowthMultiplier;
    // Mantıksal büyüme besleme ile arttığında tetiklenir (fanus baskısı bunu dinler).
    public event System.Action<CageableBug> GrowthChanged;
    // GÖSTERİLEN büyüme: transform'un gerçekten aldığı ölçek katı. Fanus içindeyken fanusun güvenli sınırıyla
    // kısıtlanır (mantıksal büyüme daha büyük olabilir); fanus dışında mantıksal büyümeye eşittir.
    public float DisplayGrowth => displayGrowth;
    // Büyüklük animasyonu sürerken true (bu sırada yeniden beslenemez).
    public bool IsGrowing { get; private set; }
    public bool IsStunned { get; private set; }
    // Fanus kırılınca kaçtı; kapsüle alınana ya da sağlam bir fanusa girene kadar true (oyuncuya çarpınca iter).
    public bool IsEscaped { get; private set; }
    // Sprey baygın böceğe tekrar hasar vermesin.
    public bool IsAlive => !IsStunned;
    // Kafes doldurur; kafeste sayılıyorsa o kafes, değilse null. Değişince gösterilen boyut yeni duruma geçer
    // (fanusa girince güvenli boyuta küçülür, çıkınca mantıksal boyuta açılır).
    public BugCage Cage
    {
        get => cage;
        set
        {
            if (cage == value) return;
            cage = value;
            if (cage != null)
            {
                IsEscaped = false;
                rampaging = false;
            }
            if (baseScale != Vector3.zero) AnimateDisplayTo(DesiredDisplay(cage), displayTransitionSeconds);
        }
    }

    // Büyüme gövdesi: dik duran collider'ın dünya ölçüsü, gösterilen büyüme 1 iken (fanus sığma hesabı için).
    public Vector3 BaseBodySize
    {
        get
        {
            if (box == null) return Vector3.zero;
            Vector3 lossy = transform.lossyScale;
            return new Vector3(standBoxSize.x * Mathf.Abs(lossy.x), standBoxSize.y * Mathf.Abs(lossy.y), standBoxSize.z * Mathf.Abs(lossy.z))
                   / Mathf.Max(displayGrowth, 0.0001f);
        }
    }

    private Rigidbody rb;
    private BoxCollider box;
    private PestWaddle waddle;
    private float health;
    private float stunEnd;
    private float nextHop;
    private float lastPullTime = -1f;
    private bool pulled;

    private Vector3 standVisualPos;
    private Quaternion standVisualRot;
    private Vector3 standBoxCenter;
    private Vector3 standBoxSize;

    // Besleme büyümesi: sahnedeki boyut (çarpan 1) ve çalışan animasyon.
    private const float MaxValueMultiplier = 1000000f;
    private Vector3 baseScale;
    private float displayGrowth = 1f;
    private BugCage cage;
    private Coroutine growRoutine;

    private void Awake()
    {
        baseScale = transform.localScale;
        displayGrowth = GrowthMultiplier;
        if (displayGrowth > 1f) transform.localScale = baseScale * displayGrowth;

        rb = GetComponent<Rigidbody>();
        box = GetComponent<BoxCollider>();
        waddle = GetComponent<PestWaddle>(); // isteğe bağlı: eski haşere yürüyüş efekti (sekme, yalpa, ezilme)
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

    // Besleme: değer çarpanını valueFactor kadar katlar, boyutu animasyonla targetGrowth katına çıkarır
    // (daha küçük/eşit hedefte boyut değişmez). Karar CreatureFeeder'dadır; bu yalnızca yaratığın durumunu günceller.
    public void ApplyFeeding(float valueFactor, float targetGrowth, float duration)
    {
        if (!float.IsNaN(valueFactor) && !float.IsInfinity(valueFactor) && valueFactor > 1f)
            valueMultiplier = Mathf.Min(ValueMultiplier * valueFactor, MaxValueMultiplier);

        if (float.IsNaN(targetGrowth) || float.IsInfinity(targetGrowth)) return;
        // Mantıksal büyüme hiçbir zaman küçülmez; gösterilen boyut kafes durumuna göre ayrıca hesaplanır.
        float previousGrowth = GrowthMultiplier;
        growthMultiplier = Mathf.Max(GrowthMultiplier, targetGrowth);
        AnimateDisplayTo(DesiredDisplay(cage), duration);
        if (GrowthMultiplier > previousGrowth + 0.0001f) GrowthChanged?.Invoke(this);
    }

    // Fanusa bırakılmadan önce (henüz Cage atanmamışken) gösterilen boyutu o fanusun güvenli sınırına anında
    // getirir; böylece yaratık fanusun içinde mantıksal boyutta belirmez.
    public void SnapDisplayFor(BugCage targetCage)
    {
        if (baseScale == Vector3.zero) return;
        AnimateDisplayTo(DesiredDisplay(targetCage), 0f);
    }

    // Gösterilecek büyüme: fanus yoksa mantıksal büyüme; fanustaysa fanusun güvenli sınırını geçmez.
    private float DesiredDisplay(BugCage forCage)
    {
        float logical = GrowthMultiplier;
        if (forCage == null) return logical;
        return Mathf.Max(1f, Mathf.Min(logical, forCage.GetDisplayGrowthLimit(this)));
    }

    // Transform ölçeğini baseScale * target'a yumuşakça (duration > 0 ve nesne aktifse) götürür.
    private void AnimateDisplayTo(float target, float duration)
    {
        if (Mathf.Abs(target - displayGrowth) < 0.0001f) return;

        displayGrowth = target;
        Vector3 goal = baseScale * target;
        if (growRoutine != null)
        {
            StopCoroutine(growRoutine);
            growRoutine = null;
        }
        if (duration <= 0f || !isActiveAndEnabled)
        {
            FinishGrowth(goal);
            return;
        }
        growRoutine = StartCoroutine(GrowRoutine(goal, duration));
    }

    private IEnumerator GrowRoutine(Vector3 goal, float duration)
    {
        IsGrowing = true;
        Vector3 start = transform.localScale;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(start, goal, t * t * (3f - 2f * t));
            yield return null;
        }
        FinishGrowth(goal);
    }

    private void FinishGrowth(Vector3 goal)
    {
        transform.localScale = goal;
        IsGrowing = false;
        growRoutine = null;
    }

    // Kapsüle alınırken (SetActive false) yarım kalan büyüme hedef boyutta biter.
    private void OnDisable()
    {
        if (IsGrowing) FinishGrowth(baseScale * displayGrowth);
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
        if (IsEscaped)
        {
            EscapedHop();
            return;
        }
        if (rb.linearVelocity.sqrMagnitude > 0.05f) return; // havada / kayarken zıplamasın

        RandomHop();
    }

    private void RandomHop()
    {
        Vector2 side = Random.insideUnitCircle * 0.5f;
        rb.AddForce(new Vector3(side.x, 1f, side.y) * hopSpeed, ForceMode.VelocityChange);
        rb.AddTorque(Vector3.up * Random.Range(-4f, 4f), ForceMode.VelocityChange);
    }

    private Transform playerTransform;
    private PlayerStatus playerStatus;
    private bool rampaging;     // kovalama aşaması (Escape ile başlar, süre/uzaklık/yakalanma ile biter)
    private float rampageEnd;
    private bool fearSpread;
    private static AudioClip fearClip;

    private bool FindPlayer()
    {
        if (playerTransform != null) return true;
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc == null) return false;
        playerTransform = pc.transform;
        playerStatus = pc.GetComponent<PlayerStatus>();
        return true;
    }

    // Kaçak yaratığın zıplaması (yol bulma yok): önce oyuncuyu kovalar (hızlı, yakında atılır), süre dolunca ya da
    // oyuncu uzaklaşınca bırakıp korku salar; sonra sakinleşir: oyuncu yaklaşırsa zigzaglı kaçar, yoksa rastgele zıplar.
    private void EscapedHop()
    {
        if (Mathf.Abs(rb.linearVelocity.y) > 0.3f) // havadayken zıplamaz, yere inince hemen dener
        {
            nextHop = Time.time + 0.1f;
            return;
        }
        if (!FindPlayer())
        {
            RandomHop();
            return;
        }

        Vector3 toPlayer = playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;
        toPlayer.y = 0f;
        bool alive = playerStatus == null || playerStatus.IsAlive;
        Vector3 toward = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;

        if (rampaging)
        {
            if (alive && Time.time < rampageEnd && distance <= chaseRange)
            {
                bool lunge = distance <= lungeRange;
                rb.MoveRotation(Quaternion.LookRotation(toward, Vector3.up));
                rb.AddForce(toward * (lunge ? lungeSpeed : chaseSpeed) + Vector3.up * (hopSpeed * (lunge ? 1.2f : 1f)), ForceMode.VelocityChange);
                nextHop = Time.time + Random.Range(chaseInterval.x, chaseInterval.y) * (lunge ? 0.7f : 1f);
                return;
            }
            rampaging = false; // kovalamayı bıraktı
            SpreadFear();
        }

        nextHop = Time.time + Random.Range(calmInterval.x, calmInterval.y);
        if (alive && distance < calmFleeRange)
        {
            Vector3 away = Quaternion.AngleAxis(Random.Range(-zigzagAngle, zigzagAngle), Vector3.up) * -toward;
            rb.MoveRotation(Quaternion.LookRotation(away, Vector3.up));
            rb.AddForce(away * calmFleeSpeed + Vector3.up * hopSpeed, ForceMode.VelocityChange);
        }
        else
        {
            RandomHop();
        }
    }

    // Yaratık kovalamayı bırakınca bir kez: telsiz uyarısı, kısa alçak hırıltı ve hafif ekran sarsıntısı.
    private void SpreadFear()
    {
        if (fearSpread) return;
        fearSpread = true;

        if (RadioManager.Instance != null && !string.IsNullOrEmpty(fearMessage))
            RadioManager.Instance.PlayRadioSubtitle(fearMessage);
        if (fearClip == null) fearClip = MakeFearClip();
        AnomalySounds.PlayAt(fearClip, transform.position, 1f, Random.Range(0.85f, 1f), 30f);
        if (fearShake > 0f) Juice.Shake(fearShake, 0.5f);
    }

    // Alçak, titreyen bir hırıltı (kod ile üretilir).
    private static AudioClip MakeFearClip()
    {
        const int hz = 44100;
        int n = (int)(hz * 0.9f);
        var s = new float[n];
        var r = new System.Random(13);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)hz;
            float env = Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t / 0.35f);
            float growl = Mathf.Sin(2f * Mathf.PI * (62f + 14f * Mathf.Sin(t * 19f)) * t);
            lp += ((float)r.NextDouble() * 2f - 1f - lp) * 0.08f;
            s[i] = (growl * 0.7f + lp * 1.4f) * env * 0.6f;
        }
        var clip = AudioClip.Create("BugFear", n, 1, hz, false);
        clip.SetData(s, 0);
        return clip;
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
        IsEscaped = false;
        rampaging = false;
        pulled = false;
        rb.isKinematic = false;
        gameObject.SetActive(false);
    }

    // Mutfak böceği (Pest) spreyle yakalandı: bu yaratık baygın başlar (stunDuration kadar baygın kalır).
    public void BecomeStunned()
    {
        if (!IsStunned) Stun();
    }

    // Fanus kırıldı: yaratık kaçar (baygınsa uyanır) ve verilen hızla fırlar. Boyut Cage=null ile açılır.
    public void Escape(Vector3 velocity)
    {
        IsEscaped = true;
        rampaging = true;
        rampageEnd = Time.time + rampageSeconds;
        fearSpread = false;
        if (IsStunned) stunEnd = Time.time; // Update bir sonraki karede uyandırır
        pulled = false;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(velocity, ForceMode.VelocityChange);
    }

    // Kaçan yaratık oyuncuya çarparsa küçük bir itme ve kısa kamera sarsıntısı (can/ölüm yok).
    private const float PushCooldown = 1f;
    private const float PushDistance = 0.6f;
    private const float PushSeconds = 0.15f;
    private float nextPush;

    private void OnCollisionEnter(Collision collision)
    {
        TryAttack(collision);
    }

    // Oyuncuya yapışık kalan yaratık da bekleme süresi dolunca tekrar saldırır.
    private void OnCollisionStay(Collision collision)
    {
        TryAttack(collision);
    }

    private void TryAttack(Collision collision)
    {
        if (!IsEscaped || !rampaging || IsStunned || Time.time < nextPush) return;
        CharacterController player = collision.collider.GetComponent<CharacterController>();
        if (player == null) return;

        nextPush = Time.time + PushCooldown;
        Vector3 away = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
        if (away.sqrMagnitude < 0.0001f) away = player.transform.forward;
        Juice.Shake(0.04f, 0.2f);
        PlayerStatus status = player.GetComponent<PlayerStatus>();
        if (status != null && status.IsAlive && attackDamage > 0f)
        {
            status.TakeDamage(attackDamage, collision.GetContact(0).point, away.normalized);
            Juice.PlayerHurt();
        }
        StartCoroutine(PushPlayer(player, away.normalized));
    }

    private IEnumerator PushPlayer(CharacterController player, Vector3 direction)
    {
        for (float t = 0f; t < PushSeconds && player != null && player.enabled; t += Time.deltaTime)
        {
            player.Move(direction * (PushDistance / PushSeconds) * Time.deltaTime);
            yield return null;
        }
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
        if (waddle != null) waddle.Suppressed = true; // yatış duruşuna dokunmasın

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
        if (waddle != null) waddle.Suppressed = false;
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
