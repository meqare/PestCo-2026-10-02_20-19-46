using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

// NavMesh üzerinde dolaşan basit haşere. Object pool'a hazır: her OnEnable'da baştan kurulur.
[RequireComponent(typeof(NavMeshAgent))]
public class Pest : MonoBehaviour, IDamageable
{
    // Tür: davranış farkları (sprey direnci, köşeye kaçma) buna göre seçilir.
    public enum PestSpecies { Cockroach, Mouse }

    // Idle = dolaşma + bekleme (WANDER/IDLE). CornerFlee = farenin en yakın köşeye kaçışı.
    private enum PestState { Idle, Flee, Hide, CornerFlee, Dead }

    private const string HidingSpotTag = "HidingSpot";
    private const string PlayerTag = "Player";
    // Kaçış yönü geçersizse sırayla denenecek açılar. Köşeye sıkışınca yanlara, en son oyuncunun yanından sıyrılmayı dener.
    private static readonly float[] FleeAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
    // Farenin köşe aradığı 4 çapraz yön: kuzeybatı, kuzeydoğu, güneydoğu, güneybatı (dünya ekseni).
    private static readonly Vector3[] CornerDirections =
    {
        new Vector3(-1f, 0f, 1f).normalized,
        new Vector3(1f, 0f, 1f).normalized,
        new Vector3(1f, 0f, -1f).normalized,
        new Vector3(-1f, 0f, -1f).normalized
    };

    [Header("Species")]
    [SerializeField] private string speciesName = "Hamambocegi";
    [Tooltip("Mouse: kaçarken en yakın köşeye koşar (CornerFlee).")]
    [SerializeField] private PestSpecies species = PestSpecies.Cockroach;

    [Header("Spray Resistance")]
    [Tooltip("Açıksa ilk sprey canı yarıya indirip yavaşlatır, öldürmez (fare).")]
    [SerializeField] private bool isSprayResistant = false;
    [Tooltip("İlk spreyden sonra yavaşlama süresi (saniye).")]
    [SerializeField, Min(0f)] private float spraySlowDuration = 2f;
    [Tooltip("Yavaşlarken hızdan düşülen oran (0.3 = %30 yavaş).")]
    [SerializeField, Range(0f, 1f)] private float spraySlowAmount = 0.3f;
    [Tooltip("İlk spreyden sonra hasar almadığı süre. Sprey 0.1 sn'de bir vurur; bu olmazsa " +
             "'ikinci sprey' hemen gelir ve direnç hissedilmez.")]
    [SerializeField, Min(0f)] private float resistGraceTime = 0.6f;

    [Header("Corner Flee (Mouse)")]
    [Tooltip("Köşe/duvar ararken her çapraz yönde bakılan en uzak mesafe.")]
    [SerializeField, Min(0.5f)] private float cornerSearchDistance = 6f;
    [Tooltip("Bundan yakın duvarlar köşe sayılmaz (zaten oradayken aynı yeri seçmesin).")]
    [SerializeField, Min(0f)] private float cornerMinDistance = 0.75f;
    [Tooltip("Köşeye varınca saklanmış gibi bekleme süresi.")]
    [SerializeField, Min(0f)] private float cornerWaitTime = 2f;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float wanderRadius = 3f;
    [SerializeField, Min(0f)] private float minIdleWait = 0.5f;
    [SerializeField, Min(0f)] private float maxIdleWait = 2f;
    [SerializeField, Min(0f)] private float moveSpeed = 1.2f;
    [Tooltip("Hedefe bu kadar yaklaşınca varmış sayılır.")]
    [SerializeField, Min(0.01f)] private float arriveDistance = 0.15f;
    [Tooltip("Bir hedefe bu süreden uzun gidemezse yeni hedef seçer (takılma önlemi).")]
    [SerializeField, Min(0.5f)] private float giveUpTime = 6f;

    [Header("Flee")]
    [SerializeField, Min(0.1f)] private float detectRadius = 4f;
    [SerializeField, Min(0f)] private float fleeSpeed = 2.85f;
    [SerializeField, Min(0.1f)] private float fleeDistance = 3f;
    [Tooltip("Oyuncu detectRadius*1.5'ten uzakta bu kadar kalırsa sakinleşir.")]
    [SerializeField, Min(0f)] private float calmDownTime = 2f;

    [Header("Flee - Panik / Zigzag / Duraklama")]
    [Tooltip("Oyuncuyu fark edince bu kadar sn donar (şok), sonra aniden kaçar.")]
    [SerializeField, Min(0f)] private float freezeDuration = 0.3f;
    [Tooltip("Kaçış başında rastgele yöne kaç kez sıçrar.")]
    [SerializeField, Min(0)] private int panicHops = 3;
    [Tooltip("Panik sıçramalarının toplam süresi (sn). Düşünme 0.2 sn'de bir olduğu için yaklaşık uygulanır.")]
    [SerializeField, Min(0f)] private float panicDuration = 0.5f;
    [SerializeField, Min(0.1f)] private float panicHopRadius = 1f;
    [Tooltip("Zigzag ara hedefinin yenilenme aralığı (sn). Düşünme aralığına (0.2) yuvarlanır.")]
    [SerializeField, Min(0.05f)] private float zigzagInterval = 0.2f;
    [Tooltip("Yan salınım hızı: yan yön sin(time * bu değer) ile yumuşakça sağ-sol değişir.")]
    [SerializeField, Min(0.1f)] private float zigzagSpeed = 8f;
    [Tooltip("0 = düz, 1 = çok zigzag.")]
    [SerializeField, Range(0f, 1f)] private float zigzagAmount = 0.6f;
    [Tooltip("Zigzag ara hedefinin ne kadar ileride seçileceği (m).")]
    [SerializeField, Min(0.2f)] private float zigzagLookAhead = 1f;
    [Tooltip("Bu aralıkla (sn) kısa bir duraklama yapar.")]
    [SerializeField, Min(0.1f)] private float pauseInterval = 1.5f;
    [SerializeField, Min(0f)] private float pauseDuration = 0.2f;
    [Tooltip("Kaçış hızı fleeSpeed ± bu kadar dalgalanır (Perlin noise).")]
    [SerializeField, Min(0f)] private float speedVariation = 0.7f;
    [Tooltip("Panik başında en yüksek hız; bu sürede (sn) hız %20 azalır.")]
    [SerializeField, Min(0.1f)] private float panicSpeedDecayTime = 6f;

    [Header("Hide")]
    [SerializeField, Min(0f)] private float hideSearchRadius = 2f;
    [SerializeField, Min(0f)] private float hideMinTime = 5f;
    [SerializeField, Min(0f)] private float hideMaxTime = 10f;
    [Tooltip("Saklanma noktasına bu kadar yaklaşınca varmış sayılır. Birkaç böcek aynı noktaya üşüşünce " +
             "birbirini iter; küçük olursa hiçbiri varamaz ve takılır.")]
    [SerializeField, Min(0.05f)] private float hideArriveDistance = 0.5f;
    [Tooltip("Kendi yuvası bu mesafedeyse kaçarken önce oraya saklanır.")]
    [SerializeField, Min(0f)] private float homeSearchRadius = 8f;
    [Tooltip("Saklanmaktan vazgeçince bu kadar saniye tekrar saklanmayı denemez (aynı yere takılmasın).")]
    [SerializeField, Min(0f)] private float hideRetryDelay = 3f;

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 20f;
    [Tooltip("Her hasarda geriye itilme mesafesi (metre).")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.05f;

    [Header("Death")]
    [Tooltip("Vuruş anında bu kadar sn donar (şok etkisi).")]
    [SerializeField, Min(0f)] private float hitFreezeDuration = 0.1f;
    [Tooltip("Yerde kasılma/titreme süresi (sn). Böcek havaya uçmaz, yerinde çırpınır.")]
    [SerializeField, Min(0f)] private float convulsionDuration = 1.5f;
    [Tooltip("Titremede en fazla sapma (metre). Çok küçük tut: 0.02.")]
    [SerializeField, Min(0f)] private float convulsionIntensity = 0.02f;
    [Tooltip("Yeni titreme hedefinin seçilme aralığı (sn). Hedefler arası yumuşak geçilir.")]
    [SerializeField, Min(0.02f)] private float twitchInterval = 0.15f;
    [Tooltip("Bacak seğirmesi: ölçekte en fazla ±bu oran (0.04 = %4).")]
    [SerializeField, Min(0f)] private float legTwitchScale = 0.04f;
    [Tooltip("Sırtüstü dönme süresi (sn): X ekseninde 180°.")]
    [SerializeField, Min(0.05f)] private float flipDuration = 0.5f;
    [Tooltip("Sırtüstü döndükten sonraki hafif titreme süresi (sn).")]
    [SerializeField, Min(0f)] private float finalTwitchDuration = 0.5f;
    [Tooltip("Ceset sönmeye başlamadan önce yerde kalma süresi (saniye).")]
    [FormerlySerializedAs("corpseFadeDelay")]
    [FormerlySerializedAs("corpseDuration")]
    [SerializeField, Min(0f)] private float corpseLifetime = 30f;
    [FormerlySerializedAs("corpseFadeDuration")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 1.5f;
    [Tooltip("Sönme için şeffaf (Transparent) materyal. Sadece sönerken takılır; boşsa alpha görünmez.")]
    [SerializeField] private Material corpseFadeMaterial;
    [Tooltip("Aynı anda en fazla bu kadar ceset. Aşılırsa en eskisi anında havuza döner.")]
    [SerializeField, Min(1)] private int maxCorpses = 40;
    [Tooltip("GEÇİCİ: her ölümde canlı/ceset sayısını Console'a yazar. Test bitince kapat.")]
    [SerializeField] private bool debugCorpseLogs = false;
    [Tooltip("Ölünce çalan ses (fare için). Boşsa ses çalmaz.")]
    [SerializeField] private AudioClip mouseDeathSound;
    [SerializeField] private UnityEvent onDeath;

    [Tooltip("Ölünce geçilen layer: sprey/vakum taramalarına girmez.")]
    [SerializeField] private string corpseLayerName = "Corpse";

    [Header("Performance")]
    [SerializeField, Min(0.05f)] private float thinkInterval = 0.2f;

    [Header("Agent Çevikliği (çizgi film: ani kalkış, keskin dönüş)")]
    [Tooltip("Hızlanma (m/s²). Yüksek = 'tekerlek yakıp' ani fırlama.")]
    [SerializeField, Min(1f)] private float agentAcceleration = 45f;
    [Tooltip("Dönüş hızı (derece/sn). Yüksek = keskin, kaymalı dönüşler.")]
    [SerializeField, Min(90f)] private float agentAngularSpeed = 900f;

    [Header("Akıllı Kaçış")]
    [Tooltip("Oyuncunun bu kadar sn sonra olacağı yer tahmin edilir; böcek ona göre kaçar.")]
    [SerializeField, Min(0f)] private float lookAheadTime = 0.6f;
    [Tooltip("Oyuncu bu kadar yakında ve üstüne geliyorsa son anda yana atlar (dodge).")]
    [SerializeField, Min(0.5f)] private float dodgeRadius = 2.2f;
    [SerializeField, Min(0.2f)] private float dodgeCooldown = 1.1f;
    [SerializeField, Min(0.5f)] private float dodgeDistance = 1.6f;
    [Tooltip("Dodge sırasında hedef/zigzag güncellenmez (sn).")]
    [SerializeField, Min(0.05f)] private float dodgeDuration = 0.35f;
    [SerializeField, Min(1f)] private float dodgeSpeedMultiplier = 1.5f;
    [Tooltip("Oyuncunun üstüne geliş hızı (m/s) bunu geçerse dodge tetiklenir.")]
    [SerializeField, Min(0f)] private float dodgeMinClosingSpeed = 1.2f;
    [Tooltip("Bir kaçışta dolap/çekmeceye saklanma ihtimali (0-1). Düşük = çoğunlukla sadece kaçar.")]
    [SerializeField, Range(0f, 1f)] private float cabinetHideChance = 0.3f;
    [Tooltip("Bir dolaba saklandıktan sonra bu kadar sn tekrar dolaba girmez.")]
    [SerializeField, Min(0f)] private float cabinetCooldown = 25f;
    [Tooltip("Saklanmak için önce bu kadar sn koşmuş olmalı (hemen kaybolmasın, önce kaçsın).")]
    [SerializeField, Min(0f)] private float minFleeTimeBeforeHide = 3.5f;
    [Tooltip("Saklanmaya ancak oyuncu bu kadar (m) uzaktayken başlar; yakınken koşmaya devam eder.")]
    [SerializeField, Min(0f)] private float hideSafeDistance = 3.5f;

    [Header("Yakalama / Vakum Direnci (kolay yakalanmasın)")]
    [Tooltip("Vakumla yakalanınca envantere eklenecek böcek verisi (tür adı, satış değeri).")]
    [SerializeField] private BugData bugData;
    [Tooltip("Vakum tutarken saniyede kaybettiği can (öldürmez, zayıflatır).")]
    [SerializeField, Min(0f)] private float vacuumDrainPerSecond = 16f;
    [Tooltip("Can bu orana inince yutulabilir (0.3 = %30). Daha sağlamsa vakum yutamaz.")]
    [SerializeField, Range(0.05f, 0.9f)] private float swallowHealthFraction = 0.3f;
    [Tooltip("Sapasağlamken vakumun çekme gücü çarpanı (zayıfladıkça 1'e çıkar).")]
    [SerializeField, Range(0.05f, 1f)] private float healthyPullMultiplier = 0.35f;
    [Tooltip("Vakumdayken bu aralıkla (sn) kurtulmayı dener.")]
    [SerializeField, Min(0.2f)] private float struggleInterval = 1.1f;
    [SerializeField, Range(0f, 1f)] private float breakFreeChance = 0.3f;
    [Tooltip("Kurtulduktan sonra bu kadar sn vakum çekemez.")]
    [SerializeField, Min(0f)] private float breakFreeImmunity = 1.6f;
    [Tooltip("Hasarsız kalınca saniyede kazandığı can.")]
    [SerializeField, Min(0f)] private float healthRegenPerSecond = 3f;
    [SerializeField, Min(0f)] private float regenDelay = 2.5f;

    /// <summary>Oyuncuyu fark etti (parametre: şok/hazırlık süresi, sn). Çizgi film tepkileri bunu dinler.</summary>
    public event System.Action<float> Alerted;
    /// <summary>Son anda yana atladı (parametre: dünya yönünde sıçrama yönü).</summary>
    public event System.Action<Vector3> Dodged;
    /// <summary>Gizlendi (true) ya da ortaya çıktı (false).</summary>
    public event System.Action<bool> ConcealedChanged;

    public bool IsAlive => state != PestState.Dead && currentHealth > 0f;

    /// <summary>Yakalanınca envantere eklenecek böcek verisi (boş olabilir).</summary>
    public BugData BugData => bugData;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    /// <summary>0-1 arası kalan can oranı.</summary>
    public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    /// <summary>Can değişti (parametre: 0-1 oran). Can barı bunu dinler.</summary>
    public event System.Action<float> HealthChanged;
    /// <summary>Yeterince zayıfladı mı, vakum yutabilir mi?</summary>
    public bool IsWeakEnoughToSwallow => currentHealth <= maxHealth * swallowHealthFraction;
    /// <summary>Vakumun çekme gücü çarpanı: sağlamken düşük, zayıflayınca 1.</summary>
    public float VacuumPullMultiplier => Mathf.Lerp(1f, healthyPullMultiplier,
        Mathf.InverseLerp(swallowHealthFraction, 1f, HealthFraction));
    public string SpeciesName => speciesName;
    public PestSpecies Species => species;
    public UnityEvent OnDeath => onDeath;
    // Oyun başından beri öldürülen toplam böcek (HUD okur).
    public static int TotalKills { get; private set; }

    // Tüm haşereler aynı oyuncuyu ve saklanma noktalarını paylaşır (300 kez aramamak için).
    private static Transform player;
    private static Transform[] hidingSpots;
    // Yerde bekleyen (henüz sönmeyen) cesetler, eskiden yeniye.
    private static readonly List<Pest> corpses = new List<Pest>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static MaterialPropertyBlock propertyBlock;

    private NavMeshAgent agent;
    // Sadece bu böceğin ajan tipine ait NavMesh'te nokta arar (sahnede birden fazla NavMesh var).
    private NavMeshQueryFilter navFilter;
    private Collider bodyCollider;
    private Renderer[] renderers;
    private WaitForSeconds thinkWait;
    private WaitForSeconds corpseWait;
    private WaitForSeconds hitFreezeWait;
    private int aliveLayer;
    private int corpseLayer;
    private Coroutine thinkRoutine;
    private Coroutine deathRoutine;

    // Havuzdan dönüşte geri yüklenecek orijinal görünüm.
    private Vector3 baseScale;
    // Dev böcek anomalisi için orijinal değerler (havuzdan her çıkışta geri yüklenir).
    private Vector3 originalScale;
    private float originalMaxHealth;
    private float originalFleeSpeed;
    private Material[] originalMaterials;
    private ShadowCastingMode[] originalShadows;
    private bool[] originalReceiveShadows;
    private LightProbeUsage[] originalLightProbes;
    private ReflectionProbeUsage[] originalReflectionProbes;
    private MotionVectorGenerationMode[] originalMotionVectors;
    private Color[] originalColors;
    private bool isFading;

    private PestState state;
    private float currentHealth;
    private bool isWaiting;
    private float waitUntil;
    private float moveStartTime;

    // Kaçış: ana hedef (zigzag bunun etrafında salınır), panik, zigzag ve duraklama zamanlayıcıları.
    private Vector3 fleeTarget;
    private int panicHopsLeft;
    private float nextPanicHopTime;
    private float nextZigzagTime;
    private float panicStartTime;
    private float nextPauseTime;
    private float pauseUntil;
    private bool isFleePaused;
    private float speedNoiseSeed;

    private float detectRadiusSqr;
    private float calmRadiusSqr;
    private float hideSearchRadiusSqr;
    private bool hasFleeTarget;
    private float calmStartTime;
    private bool isConcealed;
    // Doğduğu yuva (yuva atar); kaçınca önce oraya saklanır.
    private PestNest homeNest;
    private bool hidingInNest;
    private float nextHideAllowedTime;
    private float hideUntil;

    // Hız: durumların istediği temel hız; yavaşlama bunun üstüne çarpan olarak uygulanır.
    private float baseSpeed;
    private bool isSlowed;
    private float slowUntil;
    // Sprey direnci: ilk isabet kullanıldı mı, ve hasar almadığı süre.
    private bool resistanceUsed;
    private float damageImmuneUntil;
    // Köşede bekleme (CornerFlee).
    private bool isCornerWaiting;
    private float cornerWaitUntil;
    // Vakum: tutma/kurtulma/can yenilenmesi zamanlayıcıları.
    private float vacuumGrabUntil;
    private bool wasGrabbed;
    private float vacuumImmuneUntil;
    private float nextStruggleTime;
    private float lastDamageTime;
    private float lastRegenTime;
    // Dolap/çekmece saklanması: ayrılan nokta, animasyon durumu ve çalışan coroutine.
    private CabinetHideSpot hideSpot;
    private bool isCabinetBusy;
    private Coroutine cabinetRoutine;
    private bool cabinetAllowedThisFlee;
    private float nextCabinetAllowedTime;
    // Akıllı kaçış: oyuncunun hızı tahmini ve dodge zamanlayıcıları.
    private Vector3 lastPlayerPos;
    private float lastPlayerSampleTime;
    private Vector3 playerVelocity;
    private float nextDodgeTime;
    private float dodgeUntil;
    // Ölüm sesi Juice'a dizi olarak verilir; her ölümde new olmasın diye bir kere kurulur.
    private AudioClip[] deathSoundClips;

    // Domain reload kapalıyken eski sahnenin referansları kalmasın.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        player = null;
        hidingSpots = null;
        corpses.Clear();
        TotalKills = 0;
    }

    // Referansları, bekleme nesnelerini ve mesafe karelerini bir kere oluşturur.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // Çevik kalkış ve keskin dönüş (varsayılan 8 / 120 çok hantal hissettiriyor).
        agent.acceleration = Mathf.Max(agent.acceleration, agentAcceleration);
        agent.angularSpeed = Mathf.Max(agent.angularSpeed, agentAngularSpeed);
        navFilter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        bodyCollider = GetComponentInChildren<Collider>();
        KinematicBody.Ensure(gameObject); // her karede hareket eden collider: statik sayılmasın (FPS)
        renderers = GetComponentsInChildren<Renderer>(true);
        SetBaseSpeed(moveSpeed);
        if (mouseDeathSound != null)
            deathSoundClips = new[] { mouseDeathSound };

        thinkWait = new WaitForSeconds(thinkInterval);
        corpseWait = new WaitForSeconds(corpseLifetime);
        hitFreezeWait = new WaitForSeconds(hitFreezeDuration);

        aliveLayer = gameObject.layer;
        corpseLayer = LayerMask.NameToLayer(corpseLayerName);

        CacheOriginalLook();

        detectRadiusSqr = detectRadius * detectRadius;
        float calmRadius = detectRadius * 1.5f;
        calmRadiusSqr = calmRadius * calmRadius;
        hideSearchRadiusSqr = hideSearchRadius * hideSearchRadius;

        CacheSceneReferences();
    }

    // Ölçeği, materyali, gölge ayarını ve rengi saklar (ölümde değişip geri yüklenecek).
    private void CacheOriginalLook()
    {
        baseScale = transform.localScale;
        originalScale = baseScale;
        originalMaxHealth = maxHealth;
        originalFleeSpeed = fleeSpeed;
        originalMaterials = new Material[renderers.Length];
        originalShadows = new ShadowCastingMode[renderers.Length];
        originalReceiveShadows = new bool[renderers.Length];
        originalLightProbes = new LightProbeUsage[renderers.Length];
        originalReflectionProbes = new ReflectionProbeUsage[renderers.Length];
        originalMotionVectors = new MotionVectorGenerationMode[renderers.Length];
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            originalMaterials[i] = mat;
            originalShadows[i] = renderers[i].shadowCastingMode;
            originalReceiveShadows[i] = renderers[i].receiveShadows;
            originalLightProbes[i] = renderers[i].lightProbeUsage;
            originalReflectionProbes[i] = renderers[i].reflectionProbeUsage;
            originalMotionVectors[i] = renderers[i].motionVectorGenerationMode;
            originalColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
        }
    }

    // Oyuncuyu ve HidingSpot'ları sadece ilk haşere arar, diğerleri paylaşır.
    private static void CacheSceneReferences()
    {
        if (player == null)
        {
            GameObject tagged = GameObject.FindWithTag(PlayerTag);
            if (tagged != null)
            {
                player = tagged.transform;
            }
            else
            {
                PlayerController controller = FindFirstObjectByType<PlayerController>();
                if (controller != null)
                    player = controller.transform;
            }
        }

        if (hidingSpots == null || (hidingSpots.Length > 0 && hidingSpots[0] == null))
        {
            GameObject[] spots = GameObject.FindGameObjectsWithTag(HidingSpotTag);
            hidingSpots = new Transform[spots.Length];
            for (int i = 0; i < spots.Length; i++)
                hidingSpots[i] = spots[i].transform;
        }
    }

    // Havuzdan her çıkışta haşereyi canlı ve temiz hale getirir (ölü/gizli çıkmasın).
    private void OnEnable()
    {
        state = PestState.Idle;
        // Dev böcek anomalisinden kalan değerler sıfırlanır.
        baseScale = originalScale;
        maxHealth = originalMaxHealth;
        fleeSpeed = originalFleeSpeed;
        currentHealth = maxHealth;
        vacuumGrabUntil = 0f;
        wasGrabbed = false;
        vacuumImmuneUntil = 0f;
        nextStruggleTime = 0f;
        lastDamageTime = -100f;
        lastRegenTime = Time.time;
        HealthChanged?.Invoke(1f);
        hasFleeTarget = false;
        calmStartTime = -1f;
        hideUntil = 0f;
        isFading = false;
        homeNest = null;
        hidingInNest = false;
        nextHideAllowedTime = 0f;
        deathRoutine = null;
        isSlowed = false;
        slowUntil = 0f;
        resistanceUsed = false;
        damageImmuneUntil = 0f;
        isCornerWaiting = false;
        isFleePaused = false;
        panicHopsLeft = 0;
        nextDodgeTime = 0f;
        dodgeUntil = 0f;
        isCabinetBusy = false;
        playerVelocity = Vector3.zero;
        lastPlayerSampleTime = -1f;
        speedNoiseSeed = Random.Range(0f, 100f);

        // Ölünce ters dönmüş ve sıkışmış olabilir: sadece Y yönünü koru, ölçeği geri al.
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        transform.localScale = baseScale;

        // Materyal, alpha ve gölge ilk haline dönsün.
        RestoreLook();

        if (bodyCollider != null)
            bodyCollider.isTrigger = false;

        gameObject.layer = aliveLayer;

        // Agent'ı kapatıp açarak eski yol/hız kalıntılarını sil, yeni konuma oturt.
        agent.enabled = false;
        agent.enabled = true;
        if (agent.isOnNavMesh || agent.Warp(transform.position))
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        SetBaseSpeed(moveSpeed);

        // Renderer + collider açık, agent hareketli başlasın.
        SetConcealed(false);

        // Hemen bir hedef seçmesin, rastgele kısa bir süre dursun.
        StartWaiting();
        thinkRoutine = StartCoroutine(ThinkLoop());
    }

    // Kapanınca çalışan tüm coroutine'leri durdurur ve ceset listesinden çıkar.
    private void OnDisable()
    {
        StopAllCoroutines();
        thinkRoutine = null;
        deathRoutine = null;
        cabinetRoutine = null;
        isCabinetBusy = false;
        ReleaseHideSpot();
        corpses.Remove(this);
    }

    // Orijinal materyali, gölge/probe/motion vector ayarlarını geri takar ve alpha'yı (property block) temizler.
    private void RestoreLook()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            r.sharedMaterial = originalMaterials[i];
            r.shadowCastingMode = originalShadows[i];
            r.receiveShadows = originalReceiveShadows[i];
            r.lightProbeUsage = originalLightProbes[i];
            r.reflectionProbeUsage = originalReflectionProbes[i];
            r.motionVectorGenerationMode = originalMotionVectors[i];
            r.SetPropertyBlock(null);
        }
    }

    // Ceset görünümünü ucuzlatır: gölge atmaz/almaz, probe ve motion vector hesabına girmez.
    private void SetCheapCorpseLook()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }

    // Her karede değil, thinkInterval'da bir karar verir.
    private IEnumerator ThinkLoop()
    {
        // 300 haşere aynı karede düşünmesin diye başlangıcı dağıt.
        yield return new WaitForSeconds(Random.Range(0f, thinkInterval));

        while (true)
        {
            Think();
            yield return thinkWait;
        }
    }

    // Duruma göre ne yapılacağına karar verir.
    private void Think()
    {
        UpdateSlow();
        UpdatePlayerVelocity();
        UpdateHealthRegen();

        // Vakum tutuyorsa karar vermez; bırakınca agent tekrar yürümeye başlar.
        if (Time.time < vacuumGrabUntil) return;
        if (wasGrabbed)
        {
            wasGrabbed = false;
            if (agent.isOnNavMesh && !isConcealed && !isFleePaused) agent.isStopped = false;
        }

        switch (state)
        {
            case PestState.Idle:
                if (PlayerDistanceSqr() <= detectRadiusSqr)
                    EnterFlee();
                else
                    UpdateIdle();
                break;
            case PestState.Flee:
                UpdateFlee();
                break;
            case PestState.Hide:
                UpdateHide();
                break;
            case PestState.CornerFlee:
                UpdateCornerFlee();
                break;
            case PestState.Dead:
                break;
        }
    }

    // Hasar almadan regenDelay sn geçince canı yavaşça yeniler (zayıflatılan böcek toparlanır).
    private void UpdateHealthRegen()
    {
        float dt = Time.time - lastRegenTime;
        lastRegenTime = Time.time;

        if (!IsAlive || healthRegenPerSecond <= 0f || currentHealth >= maxHealth) return;
        if (Time.time - lastDamageTime < regenDelay) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + healthRegenPerSecond * dt);
        HealthChanged?.Invoke(HealthFraction);
    }

    // Oyuncunun yatay hızını iki düşünme arasındaki konum farkından tahmin eder (yumuşatılmış).
    private void UpdatePlayerVelocity()
    {
        if (player == null) return;

        Vector3 pos = player.position;
        pos.y = 0f;
        if (lastPlayerSampleTime >= 0f)
        {
            float dt = Time.time - lastPlayerSampleTime;
            if (dt > 0.01f)
            {
                Vector3 raw = (pos - lastPlayerPos) / dt;
                // Işınlanma gibi uç sıçramalar yok sayılır (10 m/s üstü).
                if (raw.magnitude < 10f)
                    playerVelocity = Vector3.Lerp(playerVelocity, raw, 0.6f);
            }
        }
        lastPlayerPos = pos;
        lastPlayerSampleTime = Time.time;
    }

    // Oyuncuya yatay uzaklığın karesi; oyuncu yoksa sonsuz döner.
    private float PlayerDistanceSqr()
    {
        if (player == null) return float.MaxValue;
        Vector3 diff = transform.position - player.position;
        diff.y = 0f;
        return diff.sqrMagnitude;
    }

    // Bekliyorsa süreyi kontrol eder, yürüyorsa varıp varmadığına bakar.
    private void UpdateIdle()
    {
        if (!agent.isOnNavMesh) return;

        if (isWaiting)
        {
            if (Time.time >= waitUntil)
                TryPickNewDestination();
            return;
        }

        if (agent.pathPending) return;

        bool arrived = HasArrived();
        bool stuck = Time.time - moveStartTime > giveUpTime;
        bool badPath = agent.pathStatus == NavMeshPathStatus.PathInvalid;

        if (arrived || stuck || badPath)
            StartWaiting();
    }

    // Agent'ın hedefine yatayda yeterince (arriveDistance) yaklaşıp yaklaşmadığını söyler.
    private bool HasArrived() => HasArrived(arriveDistance);

    // Agent'ın hedefine yatayda verilen mesafe kadar yaklaşıp yaklaşmadığını söyler.
    private bool HasArrived(float distance)
    {
        Vector3 toTarget = agent.destination - transform.position;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude <= distance * distance;
    }

    // Etrafta NavMesh üzerinde geçerli rastgele bir nokta bulup oraya yürür.
    private void TryPickNewDestination()
    {
        Vector3 randomPoint = transform.position + Random.insideUnitSphere * wanderRadius;

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, navFilter))
        {
            agent.SetDestination(hit.position);
            isWaiting = false;
            moveStartTime = Time.time;
        }
        // Bulamazsa bekleme modunda kalır, bir sonraki düşünmede tekrar dener.
    }

    // Durur ve minIdleWait-maxIdleWait arası rastgele bir süre bekler.
    private void StartWaiting()
    {
        isWaiting = true;
        waitUntil = Time.time + Random.Range(minIdleWait, Mathf.Max(minIdleWait, maxIdleWait));
        if (agent.isOnNavMesh)
            agent.ResetPath();
    }

    // Doğduğu yuvayı ayarlar (yuva, böceği havuzdan aldıktan sonra çağırır).
    // Dev böcek anomalisi: ölçek, can ve kaçış hızı değişir; lifetime sn sonra (hâlâ yaşıyorsa) kaybolur.
    // Havuzdan çıkıp aktif olduktan sonra çağrılmalı; havuza dönünce değerler OnEnable'da geri yüklenir.
    public void ApplyGiant(float scaleMultiplier, float health, float speed, float lifetime)
    {
        baseScale = originalScale * scaleMultiplier;
        transform.localScale = baseScale;
        maxHealth = health;
        currentHealth = health;
        fleeSpeed = speed;
        if (lifetime > 0f)
            StartCoroutine(GiantExpire(lifetime));
    }

    private IEnumerator GiantExpire(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (state != PestState.Dead)
            ReturnToPool();
    }

    public void SetHomeNest(PestNest nest)
    {
        homeNest = nest;
    }

    // Dışarıdan kaçmaya zorlar (ör. yuva yıkılınca). Ölü ya da saklanan böceği etkilemez.
    public void ForceFlee()
    {
        if (!IsAlive || state == PestState.Hide || !agent.isOnNavMesh) return;
        EnterFlee();
    }

    // Kaçmaya başlar: hız artar, ilk kaçış hedefi seçilir. Fare bunun yerine köşeye kaçar.
    private void EnterFlee()
    {
        if (species == PestSpecies.Mouse)
        {
            EnterCornerFlee();
            return;
        }

        state = PestState.Flee;
        isWaiting = false;
        hasFleeTarget = false;
        calmStartTime = -1f;
        dodgeUntil = 0f;
        nextDodgeTime = Time.time + freezeDuration + 0.4f;
        // Her kaçışta bir kez zar atılır: çoğu zaman dolaba girmeden sadece kaçar (hep saklanmasın).
        cabinetAllowedThisFlee = Time.time >= nextCabinetAllowedTime && Random.value < cabinetHideChance;
        ResumeFromPause();
        Alerted?.Invoke(freezeDuration);

        // Panik: önce şok donması, sonra birkaç rastgele sıçrama, ana kaçış hedefi en son seçilir.
        panicHopsLeft = panicHops;
        panicStartTime = Time.time;
        nextPanicHopTime = Time.time + freezeDuration;
        nextPauseTime = Time.time + freezeDuration + pauseInterval;
        SetBaseSpeed(fleeSpeed + speedVariation);

        if (freezeDuration > 0f && agent.isOnNavMesh)
        {
            isFleePaused = true;
            pauseUntil = Time.time + freezeDuration;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return;
        }

        if (panicHopsLeft > 0)
            PanicHop();
        else
            PickFleeTarget();
    }

    // Kaçarken: panik -> duraklama -> saklanma -> sakinleşme -> hedef/zigzag sırasıyla karar verir.
    private void UpdateFlee()
    {
        if (!agent.isOnNavMesh) return;

        // 0) Son an dodge'u sürerken hedef/zigzag/duraklama karışmaz.
        if (Time.time < dodgeUntil) return;

        // 1) Şok donması ve nefes alır gibi kısa duraklamalar.
        if (UpdateFleePause()) return;

        // 2) Panik sıçramaları (donmadan hemen sonra aniden hızlı kaçış).
        if (panicHopsLeft > 0)
        {
            if (Time.time >= nextPanicHopTime)
                PanicHop();
            if (panicHopsLeft == 0)
                PickFleeTarget();
            return;
        }

        if (TryDodge()) return;

        // Akıllı: önce gerçekten kaçar; ancak yeterince koştuktan ve oyuncudan uzaklaştıktan sonra saklanır.
        bool safeToHide = Time.time - panicStartTime >= minFleeTimeBeforeHide
            && PlayerDistanceSqr() >= hideSafeDistance * hideSafeDistance;
        if (safeToHide && TryStartHiding()) return;

        float distSqr = PlayerDistanceSqr();
        UpdateFleeSpeed(distSqr);

        // Sakinleşme: oyuncu yeterince uzakta calmDownTime kadar kalmalı.
        if (distSqr > calmRadiusSqr)
        {
            if (calmStartTime < 0f)
                calmStartTime = Time.time;
            else if (Time.time - calmStartTime >= calmDownTime)
            {
                ReturnToWander();
                return;
            }
        }
        else
        {
            calmStartTime = -1f;
        }

        if (agent.pathPending) return;

        // Hedef sadece gerektiğinde yenilenir; her düşünmede değil (titreme önlemi).
        bool needNewTarget = !hasFleeTarget
            || ReachedFleeTarget()
            || agent.pathStatus == NavMeshPathStatus.PathInvalid
            || Time.time - moveStartTime > giveUpTime
            || TargetPointsTowardPlayer();

        // Sakinleşme mesafesine (detectRadius*1.5) kadar kaçmaya devam eder;
        // eskiden detectRadius'ta duruyordu ve 4-6 m arasında donup kalıyordu.
        if (needNewTarget && distSqr <= calmRadiusSqr)
        {
            PickFleeTarget();
            return;
        }

        // 3) Zigzag: ana hedefe giderken yan yön düzenli aralıkla tersine döner.
        if (hasFleeTarget && Time.time >= nextZigzagTime)
            ZigzagStep();
    }

    // Oyuncu üstüne koşuyorsa son anda yana (oyuncunun hareket yönüne dik) atlar; iki yandan açık olanı seçer.
    // Çizgi filmlerdeki "tam yakalanacakken sıyrılma" hamlesi. Atlayınca kısa süre hızlanır.
    private bool TryDodge()
    {
        if (player == null || Time.time < nextDodgeTime) return false;

        Vector3 myPos = transform.position;
        Vector3 fromPlayer = myPos - player.position;
        fromPlayer.y = 0f;
        float dist = fromPlayer.magnitude;
        if (dist > dodgeRadius || dist < 0.05f) return false;

        Vector3 away = fromPlayer / dist;
        // Oyuncu bize doğru ne kadar hızla geliyor?
        float closingSpeed = Vector3.Dot(playerVelocity, away);
        if (closingSpeed < dodgeMinClosingSpeed) return false;

        Vector3 side = Vector3.Cross(Vector3.up, away);
        Vector3 best = Vector3.zero;
        float bestReach = 0.4f;
        bool found = false;

        // İki yan yönü dene (önce rastgele olan); duvara daha az çarpan kazanır.
        float firstSign = Random.value < 0.5f ? 1f : -1f;
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? firstSign : -firstSign;
            Vector3 dir = (side * sign * 0.9f + away * 0.4f).normalized;
            Vector3 candidate = myPos + dir * dodgeDistance;
            if (NavMesh.Raycast(myPos, candidate, out NavMeshHit edge, navFilter))
                candidate = edge.position;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.5f, navFilter)) continue;

            Vector3 move = hit.position - myPos;
            move.y = 0f;
            if (move.magnitude <= bestReach) continue;
            bestReach = move.magnitude;
            best = hit.position;
            found = true;
        }
        if (!found) return false;

        nextDodgeTime = Time.time + dodgeCooldown;
        dodgeUntil = Time.time + dodgeDuration;
        hasFleeTarget = false;
        ResumeFromPause();
        SetBaseSpeed(fleeSpeed * dodgeSpeedMultiplier);
        agent.SetDestination(best);
        moveStartTime = Time.time;

        Vector3 jump = best - myPos;
        jump.y = 0f;
        Dodged?.Invoke(jump.normalized);
        return true;
    }

    // Panik sıçraması: yakında rastgele bir NavMesh noktasına koşar.
    private void PanicHop()
    {
        panicHopsLeft--;
        nextPanicHopTime = Time.time + panicDuration / Mathf.Max(1, panicHops);

        Vector2 r = Random.insideUnitCircle.normalized * panicHopRadius;
        Vector3 candidate = transform.position + new Vector3(r.x, 0f, r.y);
        if (NavMesh.Raycast(transform.position, candidate, out NavMeshHit edge, navFilter))
            candidate = edge.position;
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.5f, navFilter))
        {
            agent.SetDestination(hit.position);
            moveStartTime = Time.time;
        }
    }

    // Duraklama zamanlayıcısı: duruyorsa true döner (bu düşünmede başka bir şey yapılmaz).
    private bool UpdateFleePause()
    {
        if (isFleePaused)
        {
            if (Time.time < pauseUntil) return true;
            ResumeFromPause();
            nextPauseTime = Time.time + pauseInterval;
            return false;
        }

        if (pauseDuration > 0f && Time.time >= nextPauseTime)
        {
            isFleePaused = true;
            pauseUntil = Time.time + pauseDuration;
            agent.isStopped = true;
            return true;
        }
        return false;
    }

    // Duraklamadan çıkar (agent tekrar yürür). Durumdan çıkarken de çağrılır, takılı kalmasın.
    private void ResumeFromPause()
    {
        if (!isFleePaused) return;
        isFleePaused = false;
        if (agent.isOnNavMesh)
            agent.isStopped = false;
    }

    // Kaçış hızını Perlin noise ile dalgalandırır; oyuncu algılama mesafesinden çıkınca düşer.
    private void UpdateFleeSpeed(float playerDistSqr)
    {
        float wave = Mathf.PerlinNoise(Time.time * 0.8f, speedNoiseSeed) * 2f - 1f; // -1..1
        // Panik başında max hız, zamanla %20'ye kadar yavaşlar (yoruluyor).
        float fade = Mathf.Lerp(1f, 0.8f, (Time.time - panicStartTime) / panicSpeedDecayTime);
        float speed = (fleeSpeed + wave * speedVariation) * fade;
        if (playerDistSqr > detectRadiusSqr)
            speed -= speedVariation * 0.5f; // sakinleşiyor
        SetBaseSpeed(Mathf.Max(0.1f, speed));
    }

    // Ana hedefe doğru 1 m ilerideki ara noktayı yan yönle kaydırır; yan yön her adımda tersine döner.
    private void ZigzagStep()
    {
        nextZigzagTime = Time.time + zigzagInterval;

        Vector3 myPos = transform.position;
        Vector3 toMain = fleeTarget - myPos;
        toMain.y = 0f;
        float lookSqr = zigzagLookAhead * zigzagLookAhead;

        // Ana hedefe yakınsa ya da zigzag kapalıysa düz gider.
        if (zigzagAmount <= 0f || toMain.sqrMagnitude <= lookSqr)
        {
            agent.SetDestination(fleeTarget);
            return;
        }

        Vector3 dir = toMain.normalized;
        // Yan yön sin ile yumuşakça sağa-sola salınır (ani sıçrama yok), böcek kıvrılarak kaçar.
        Vector3 side = Vector3.Cross(Vector3.up, dir) * Mathf.Sin(Time.time * zigzagSpeed);
        Vector3 point = myPos + (dir + side * zigzagAmount) * zigzagLookAhead;

        if (NavMesh.Raycast(myPos, point, out NavMeshHit edge, navFilter))
            point = edge.position;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 0.5f, navFilter))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(fleeTarget);
    }

    // Mevcut kaçış hedefi oyuncuya doğru kaldıysa true döner.
    private bool TargetPointsTowardPlayer()
    {
        if (!hasFleeTarget || player == null) return false;
        Vector3 toTarget = fleeTarget - transform.position;
        Vector3 away = transform.position - player.position;
        toTarget.y = 0f;
        away.y = 0f;
        return Vector3.Dot(toTarget, away) < 0f;
    }

    // Ana kaçış hedefine (zigzag'sız) yeterince yaklaştı mı?
    private bool ReachedFleeTarget()
    {
        Vector3 d = fleeTarget - transform.position;
        d.y = 0f;
        float r = Mathf.Max(arriveDistance, zigzagLookAhead * 0.5f);
        return d.sqrMagnitude <= r * r;
    }

    // 8 yönü (45° arayla, oyuncunun tersinden başlayarak) NavMesh.Raycast ile tarar.
    // Puan = yönün açıklığı (duvara mesafe) x oyuncudan uzaklaştırma; oyuncuya doğru yönler elenir.
    // En iyi nokta SamplePosition ile doğrulanır. Böylece açık alana ya da köşeye de kaçabilir.
    private void PickFleeTarget()
    {
        if (player == null) return;

        Vector3 myPos = transform.position;
        // Oyuncunun şimdiki değil, birazdan olacağı yerden kaçar (tahmin).
        Vector3 threat = player.position + playerVelocity * lookAheadTime;
        Vector3 away = myPos - threat;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = transform.forward;
        away.Normalize();

        // Duvar dibine yapışıp birkaç santim hedef seçmesin.
        const float MinMove = 0.3f;
        float bestScore = 0f;
        Vector3 best = Vector3.zero;
        bool found = false;

        for (int i = 0; i < FleeAngles.Length; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(FleeAngles[i], Vector3.up) * away;
            float awayDot = Vector3.Dot(dir, away); // 1 = tam ters yön, -1 = oyuncuya doğru
            if (awayDot < -0.25f) continue;

            Vector3 candidate = myPos + dir * fleeDistance;
            // Arada duvar varsa en fazla duvara kadar (duvarın öbür tarafındaki odayı seçmesin).
            if (NavMesh.Raycast(myPos, candidate, out NavMeshHit edge, navFilter))
                candidate = edge.position;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, navFilter))
                continue;

            Vector3 move = hit.position - myPos;
            move.y = 0f;
            float reach = move.magnitude;
            if (reach < MinMove) continue;

            float score = reach * (1.5f + awayDot);
            if (score <= bestScore) continue;
            bestScore = score;
            best = hit.position;
            found = true;
        }

        if (!found)
        {
            // Hiçbiri olmadı: olduğu yerde kalır, yolu bir kere temizler.
            hasFleeTarget = false;
            if (agent.hasPath)
                agent.ResetPath();
            return;
        }

        fleeTarget = best;
        hasFleeTarget = true;
        moveStartTime = Time.time;
        agent.SetDestination(best);
        // İlk zigzag adımı bir aralık sonra; önce ana yöne doğru çıkış yapsın.
        nextZigzagTime = Time.time + zigzagInterval;
    }

    // Önce kendi yuvasına, olmazsa yakındaki HidingSpot'a yönelir ve HIDE durumuna geçer.
    private bool TryStartHiding()
    {
        if (Time.time < nextHideAllowedTime) return false;
        if (TryHideInNest()) return true;
        if (hidingSpots == null || hidingSpots.Length == 0) return false;

        Vector3 myPos = transform.position;
        Transform best = null;
        CabinetHideSpot bestCabinet = null;
        float bestSqr = hideSearchRadiusSqr;

        for (int i = 0; i < hidingSpots.Length; i++)
        {
            Transform spot = hidingSpots[i];
            if (spot == null || !spot.gameObject.activeInHierarchy) continue;

            // Dolap/çekmece noktası başka bir haşere tarafından doluysa atlanır.
            spot.TryGetComponent(out CabinetHideSpot cabinet);
            if (cabinet != null && (!cabinetAllowedThisFlee || !cabinet.IsAvailableFor(this))) continue;

            float sqr = (spot.position - myPos).sqrMagnitude;
            // Akıllı: oyuncunun daha yakın olduğu (üstüne koşacağı) saklanma yerini seçmez.
            if (player != null && (spot.position - player.position).sqrMagnitude < sqr) continue;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = spot;
                bestCabinet = cabinet;
            }
        }

        if (best == null) return false;
        if (!NavMesh.SamplePosition(best.position, out NavMeshHit hit, 1f, navFilter)) return false;

        ReleaseHideSpot();
        if (bestCabinet != null && bestCabinet.Furniture != null && bestCabinet.TryReserve(this))
        {
            hideSpot = bestCabinet;
            nextCabinetAllowedTime = Time.time + cabinetCooldown;
            cabinetAllowedThisFlee = false;
        }

        state = PestState.Hide;
        isConcealed = false;
        hidingInNest = false;
        agent.SetDestination(hit.position);
        moveStartTime = Time.time;
        return true;
    }

    // Kendi yuvası (yoksa en yakın yuva) yaşıyor, yeterince yakın ve oyuncu yolu kesmiyorsa yuvanın girişine koşar.
    private bool TryHideInNest()
    {
        PestNest nest = homeNest != null && homeNest.IsAlive ? homeNest : FindNearestNest();
        if (nest == null) return false;

        Vector3 entrance = nest.EntrancePosition;
        float mySqr = (entrance - transform.position).sqrMagnitude;
        if (mySqr > homeSearchRadius * homeSearchRadius) return false;

        // Oyuncu yuvaya böcekten daha yakınsa oyuncunun üstüne koşmasın.
        if (player != null && (entrance - player.position).sqrMagnitude < mySqr) return false;

        if (!NavMesh.SamplePosition(entrance, out NavMeshHit hit, 1f, navFilter)) return false;

        homeNest = nest; // yuvasız böcek bu yuvayı benimser
        state = PestState.Hide;
        isConcealed = false;
        hidingInNest = true;
        agent.SetDestination(hit.position);
        moveStartTime = Time.time;
        return true;
    }

    // Saklanma yerine yürür, varınca gizlenir; süre dolunca ortaya çıkar.
    private void UpdateHide()
    {
        // Dolaba girip çıkma animasyonu sürerken başka karar verilmez.
        if (isCabinetBusy) return;

        // Yuva yıkıldıysa içindeki/yoldaki böcek fırlayıp kaçar.
        if (hidingInNest && (homeNest == null || !homeNest.IsAlive))
        {
            hidingInNest = false;
            if (isConcealed)
                SetConcealed(false);
            if (agent.isOnNavMesh)
                EnterFlee();
            return;
        }

        if (isConcealed)
        {
            if (Time.time >= hideUntil)
            {
                // Dolabın içindeyse kapağı açıp dışarı sıçrar.
                if (hideSpot != null)
                {
                    cabinetRoutine = StartCoroutine(ExitCabinetRoutine());
                    return;
                }

                SetConcealed(false);
                hidingInNest = false;
                ReturnToWander();
            }
            return;
        }

        if (!agent.isOnNavMesh || agent.pathPending) return;

        // Yol bozulduysa ya da takıldıysa saklanmaktan vazgeçip kaçmaya devam eder.
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid || Time.time - moveStartTime > giveUpTime)
        {
            state = PestState.Flee;
            hasFleeTarget = false;
            hidingInNest = false;
            ReleaseHideSpot();
            nextHideAllowedTime = Time.time + hideRetryDelay;
            return;
        }

        if (HasArrived(hideArriveDistance))
        {
            // Dolap/çekmece noktasıysa kapağı açıp içine zıplar; değilse eskisi gibi görünmez olur.
            if (hideSpot != null)
            {
                cabinetRoutine = StartCoroutine(EnterCabinetRoutine());
                return;
            }

            SetConcealed(true);
            hideUntil = Time.time + Random.Range(hideMinTime, Mathf.Max(hideMinTime, hideMaxTime));
        }
    }

    // Dolaba girer: kapağa döner, kapağı açar, içeri sıçrar, gizlenir ve kapağı kapatır.
    private IEnumerator EnterCabinetRoutine()
    {
        isCabinetBusy = true;
        CabinetHideSpot spot = hideSpot;
        IOpenableFurniture furniture = spot.Furniture;

        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        FaceTowards(spot.InsidePosition);
        furniture.Open();
        yield return new WaitForSeconds(furniture.OpenDuration * 0.8f);

        // Kapağı açık görünce içeri sıçrar; küçülerek kaybolur.
        agent.enabled = false;
        yield return HopRoutine(transform.position, spot.InsidePosition, spot.HopHeight, 1f, 0.3f);

        SetConcealed(true);
        transform.localScale = baseScale;
        spot.IsInside = true;
        hideUntil = Time.time + Random.Range(hideMinTime, Mathf.Max(hideMinTime, hideMaxTime));

        yield return new WaitForSeconds(0.2f);
        furniture.Close();
        isCabinetBusy = false;
        cabinetRoutine = null;
    }

    // Dolaptan çıkar: kapağı açar, dışarı sıçrar, sakin dolaşmaya döner; kapağı bir süre sonra kapatır.
    private IEnumerator ExitCabinetRoutine()
    {
        isCabinetBusy = true;
        CabinetHideSpot spot = hideSpot;
        IOpenableFurniture furniture = spot.Furniture;

        furniture.Open();
        yield return new WaitForSeconds(furniture.OpenDuration * 0.9f);

        Vector3 outPos = spot.transform.position;
        transform.position = spot.InsidePosition;
        SetConcealed(false);
        yield return HopRoutine(spot.InsidePosition, outPos, spot.HopHeight, 0.3f, 0.3f);

        agent.enabled = true;
        agent.Warp(outPos);
        ReleaseHideSpot();
        hidingInNest = false;
        ReturnToWander();
        isCabinetBusy = false;
        cabinetRoutine = null;

        yield return new WaitForSeconds(1f);
        furniture.Close();
    }

    // İki nokta arasında yay çizerek sıçrar; ölçek startScale'den (1 = normal) hedefe yumuşakça değişir.
    private IEnumerator HopRoutine(Vector3 from, Vector3 to, float height, float startScaleMul, float duration)
    {
        float endScaleMul = startScaleMul >= 1f ? 0.3f : 1f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            Vector3 pos = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * height);
            transform.position = pos;
            transform.localScale = baseScale * Mathf.Lerp(startScaleMul, endScaleMul, k);
            yield return null;
        }
        transform.position = to;
        transform.localScale = baseScale * endScaleMul;
    }

    // Hedef noktaya yatayda döner (dolaba bakarak girsin).
    private void FaceTowards(Vector3 worldPoint)
    {
        Vector3 dir = worldPoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    // Oyuncu saklandığı dolabı açtı: haşere ortaya çıkar ve paniğe kapılıp kaçar.
    public void ExposeFromHiding()
    {
        if (!IsAlive || hideSpot == null || !hideSpot.IsInside) return;

        if (cabinetRoutine != null)
        {
            StopCoroutine(cabinetRoutine);
            cabinetRoutine = null;
        }
        isCabinetBusy = false;

        Vector3 outPos = hideSpot.transform.position;
        agent.enabled = true;
        agent.Warp(outPos);
        transform.localScale = baseScale;
        ReleaseHideSpot();
        SetConcealed(false);
        hidingInNest = false;
        state = PestState.Idle;
        if (agent.isOnNavMesh) EnterFlee();
    }

    // Ayırdığı dolap/çekmece noktasını bırakır.
    private void ReleaseHideSpot()
    {
        if (hideSpot == null) return;

        hideSpot.Release(this);
        hideSpot = null;
    }

    // Ayakta olan en yakın yuvayı döner (yuva sayısı az, döngü ucuz).
    private PestNest FindNearestNest()
    {
        IReadOnlyList<PestNest> nests = PestNest.ActiveNests;
        PestNest best = null;
        float bestSqr = float.MaxValue;
        Vector3 myPos = transform.position;

        for (int i = 0; i < nests.Count; i++)
        {
            PestNest nest = nests[i];
            if (nest == null || !nest.IsAlive) continue;
            float sqr = (nest.EntrancePosition - myPos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = nest;
            }
        }
        return best;
    }

    // Fare kaçışı: hız artar, en yakın köşe hedeflenir.
    private void EnterCornerFlee()
    {
        state = PestState.CornerFlee;
        isWaiting = false;
        isCornerWaiting = false;
        hasFleeTarget = false;
        calmStartTime = -1f;
        SetBaseSpeed(fleeSpeed);
        PickCornerTarget();
    }

    // Köşeye koşar, varınca cornerWaitTime bekler, sonra tekrar kaçar; oyuncu uzaklaşırsa sakinleşir.
    private void UpdateCornerFlee()
    {
        if (!agent.isOnNavMesh) return;

        // Sakinleşme: Flee ile aynı kural (oyuncu calmDownTime boyunca uzakta kalmalı).
        if (PlayerDistanceSqr() > calmRadiusSqr)
        {
            if (calmStartTime < 0f)
                calmStartTime = Time.time;
            else if (Time.time - calmStartTime >= calmDownTime)
            {
                ReturnToWander();
                return;
            }
        }
        else
        {
            calmStartTime = -1f;
        }

        if (isCornerWaiting)
        {
            if (Time.time >= cornerWaitUntil)
            {
                isCornerWaiting = false;
                PickCornerTarget();
            }
            return;
        }

        if (agent.pathPending) return;

        // Köşeye vardı: saklanmış gibi durur.
        if (hasFleeTarget && HasArrived())
        {
            isCornerWaiting = true;
            cornerWaitUntil = Time.time + cornerWaitTime;
            agent.ResetPath();
            return;
        }

        bool needNewTarget = !hasFleeTarget
            || agent.pathStatus == NavMeshPathStatus.PathInvalid
            || Time.time - moveStartTime > giveUpTime
            || TargetPointsTowardPlayer();
        if (needNewTarget)
            PickCornerTarget();
    }

    // 4 çapraz yönü NavMesh.Raycast ile tarar; oyuncuya doğru olmayan en yakın duvar noktasını hedef alır.
    // Uygun köşe yoksa normal kaçış hedefine düşer.
    private void PickCornerTarget()
    {
        Vector3 myPos = transform.position;
        Vector3 away = player != null ? myPos - player.position : Vector3.zero;
        away.y = 0f;
        bool hasAway = away.sqrMagnitude > 0.0001f;

        float minSqr = cornerMinDistance * cornerMinDistance;
        float bestSqr = float.MaxValue;
        Vector3 best = Vector3.zero;
        bool found = false;

        for (int i = 0; i < CornerDirections.Length; i++)
        {
            Vector3 dir = CornerDirections[i];
            // Oyuncunun olduğu tarafa koşmasın.
            if (hasAway && Vector3.Dot(dir, away) < 0f) continue;

            // NavMesh kenarı = duvar. Bu yönde duvar yoksa (açık alan) köşe sayılmaz.
            if (!NavMesh.Raycast(myPos, myPos + dir * cornerSearchDistance, out NavMeshHit hit, navFilter))
                continue;

            Vector3 move = hit.position - myPos;
            move.y = 0f;
            float sqr = move.sqrMagnitude;
            if (sqr < minSqr || sqr >= bestSqr) continue;

            bestSqr = sqr;
            best = hit.position;
            found = true;
        }

        if (!found)
        {
            PickFleeTarget();
            return;
        }

        agent.SetDestination(best);
        hasFleeTarget = true;
        moveStartTime = Time.time;
    }

    // Temel hızı ayarlar; yavaşlatılmışsa çarpanı da uygular.
    private void SetBaseSpeed(float speed)
    {
        baseSpeed = speed;
        agent.speed = isSlowed ? speed * (1f - spraySlowAmount) : speed;
    }

    // spraySlowDuration boyunca hızı spraySlowAmount oranında düşürür.
    private void ApplySlow()
    {
        if (spraySlowDuration <= 0f) return;
        isSlowed = true;
        slowUntil = Time.time + spraySlowDuration;
        agent.speed = baseSpeed * (1f - spraySlowAmount);
    }

    // Yavaşlama süresi dolduysa normal hıza döner (düşünme döngüsünde çağrılır).
    private void UpdateSlow()
    {
        if (!isSlowed || Time.time < slowUntil) return;
        isSlowed = false;
        agent.speed = baseSpeed;
    }

    // Görünürlüğü, çarpışmayı ve agent hareketini birlikte açar/kapatır.
    private void SetConcealed(bool concealed)
    {
        bool changed = isConcealed != concealed;
        isConcealed = concealed;
        if (changed) ConcealedChanged?.Invoke(concealed);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = !concealed;

        if (bodyCollider != null)
            bodyCollider.enabled = !concealed;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = concealed;
            if (concealed)
                agent.ResetPath();
        }
    }

    // Normal hıza döner ve dolaşmaya (Idle) geri geçer.
    private void ReturnToWander()
    {
        ResumeFromPause();
        state = PestState.Idle;
        hasFleeTarget = false;
        calmStartTime = -1f;
        isCornerWaiting = false;
        SetBaseSpeed(moveSpeed);
        StartWaiting();
    }

    // Canı azaltır, hafifçe geri iter, can biterse öldürür. Saklanırken hasar almaz.
    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsAlive || isConcealed || amount <= 0f) return;
        // İlk spreyden sonraki kısa koruma süresi.
        if (Time.time < damageImmuneUntil) return;

        if (isSprayResistant && !resistanceUsed)
        {
            // Dirençli tür: ilk sprey öldürmez, canı yarıya indirir ve yavaşlatır.
            resistanceUsed = true;
            currentHealth = Mathf.Min(currentHealth, maxHealth * 0.5f);
            damageImmuneUntil = Time.time + resistGraceTime;
            ApplySlow();
        }
        else
        {
            currentHealth -= amount;
        }
        lastDamageTime = Time.time;
        HealthChanged?.Invoke(HealthFraction);

        // Ölümcül vuruşta itme yok: böcek vurulduğu yerde kasılır.
        if (currentHealth <= 0f)
        {
            Die(hitDirection);
            return;
        }

        // Agent açıkken transform'u elle kaydırmak yerine agent.Move: NavMesh dışına itilmez.
        Vector3 push = hitDirection;
        push.y = 0f;
        if (push.sqrMagnitude > 0.0001f && agent.isOnNavMesh)
            agent.Move(push.normalized * knockbackDistance);

        // Spreylenen böcek panikle kaçar; zaten kaçıyor ama duruyorsa yeni hedef seçer.
        if (state == PestState.Idle && agent.isOnNavMesh)
            EnterFlee();
        else if (state == PestState.Flee && agent.isOnNavMesh && !agent.pathPending && !agent.hasPath)
            PickFleeTarget();
        // Köşede bekleyen fare vurulunca beklemeyi bırakıp yeni köşeye koşar.
        else if (state == PestState.CornerFlee && isCornerWaiting && agent.isOnNavMesh)
        {
            isCornerWaiting = false;
            PickCornerTarget();
        }
    }

    // Ölüm: his efekti, agent/collider/gölge kapanır, olay tetiklenir, ölüm animasyonu başlar.
    private void Die(Vector3 hitDirection)
    {
        state = PestState.Dead;
        currentHealth = 0f;
        TotalKills++;
        ReleaseHideSpot();

        if (thinkRoutine != null)
        {
            StopCoroutine(thinkRoutine);
            thinkRoutine = null;
        }

        // Hitstop vuruş anında olmalı; animasyon sonunda gecikmiş hissettirir.
        Juice.PestDeath(transform.position, hitDirection);
        if (deathSoundClips != null)
            Juice.PlaySound(deathSoundClips, transform.position);

        // Agent hemen durur ve kapanır (yol bulma biter). Fizik yok: ceset sadece transform ile oynar.
        if (agent.isOnNavMesh)
            agent.velocity = Vector3.zero;
        agent.enabled = false;
        if (bodyCollider != null)
            bodyCollider.enabled = false;
        // Sprey/vakum taramalarına girmesin.
        if (corpseLayer >= 0)
            gameObject.layer = corpseLayer;
        // Ceset ışık/gölge hesabına girmesin.
        SetCheapCorpseLook();

        onDeath?.Invoke();
        RegisterCorpse();
        deathRoutine = StartCoroutine(DeathSequence());

        if (debugCorpseLogs)
        {
            int active = PestPool.Instance != null ? PestPool.Instance.ActiveCount : 0;
            Debug.Log($"Canli bocek: {active - corpses.Count}, Ceset: {corpses.Count}");
        }
    }

    // Vakum çekişi: böceği NavMesh üzerinde kaydırır (Rigidbody yok; agent.Move duvardan geçirmez).
    // Kendi hızı sıfırlanır ki çekime karşı koşamasın; vakum bırakınca normal hareketine döner.
    public void VacuumPull(Vector3 delta)
    {
        if (!IsAlive || isConcealed || !agent.isOnNavMesh) return;
        // Kurtulduktan sonra kısa süre vakum tutamaz.
        if (Time.time < vacuumImmuneUntil) return;

        // Vakum tutarken yürümez (aksi halde kaçış hızı çekimi yener).
        vacuumGrabUntil = Time.time + 0.25f;
        wasGrabbed = true;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.Move(delta);
    }

    // Vakum tuttuğu sürece canı saniyede vacuumDrainPerSecond azaltır (en az 1 can kalır; öldürmez).
    // Aralıklarla kurtulmayı dener; kurtulursa kaçar ve bir süre vakuma yakalanmaz.
    public void DrainByVacuum(float dt)
    {
        if (!IsAlive || isConcealed || Time.time < vacuumImmuneUntil) return;

        lastDamageTime = Time.time;
        currentHealth = Mathf.Max(1f, currentHealth - vacuumDrainPerSecond * dt);
        HealthChanged?.Invoke(HealthFraction);

        if (Time.time >= nextStruggleTime)
        {
            nextStruggleTime = Time.time + struggleInterval;
            if (!IsWeakEnoughToSwallow && Random.value < breakFreeChance)
                BreakFreeFromVacuum();
        }
    }

    // Vakumdan kurtulur: tutulma biter, panikle kaçar, kısa süre dokunulmaz.
    private void BreakFreeFromVacuum()
    {
        vacuumImmuneUntil = Time.time + breakFreeImmunity;
        vacuumGrabUntil = 0f;
        wasGrabbed = false;
        if (agent.isOnNavMesh) agent.isStopped = false;

        if (state == PestState.Flee) PickFleeTarget();
        else if (agent.isOnNavMesh) EnterFlee();
        SetBaseSpeed((fleeSpeed + speedVariation) * dodgeSpeedMultiplier);
        dodgeUntil = Time.time + dodgeDuration;
    }

    // Vakuma yutuldu: ceset bırakmadan ölür (öldürme sayılır, yuvası haberdar olur) ve havuza döner.
    public void Vacuumed()
    {
        if (!IsAlive) return;

        state = PestState.Dead;
        currentHealth = 0f;
        TotalKills++;
        onDeath?.Invoke();
        ReturnToPool();
    }

    // Ceset listesine eklenir; sınır aşılırsa en eski ceset anında havuza döner.
    private void RegisterCorpse()
    {
        corpses.Add(this);
        while (corpses.Count > maxCorpses)
        {
            Pest oldest = corpses[0];
            corpses.RemoveAt(0);
            if (oldest != null)
                oldest.RemoveCorpseNow();
        }
    }

    // Şok donması -> yerinde kasılma -> sırtüstü dönme -> son titreme -> hareketsiz bekleme -> sönme -> havuza dönüş.
    // Fizik yok, havaya uçma yok: sadece transform çok küçük adımlarla oynatılır.
    private IEnumerator DeathSequence()
    {
        // 1) Vuruş anı: bir an donar (şok).
        yield return hitFreezeWait;

        // 2) Yerinde kasılma: ±convulsionIntensity sapma, bacak seğirmesi için hafif ölçek oynaması.
        yield return Twitch(transform.position, convulsionDuration, 1f);

        // 3) Yavaşça sırtüstü döner.
        yield return FlipOnBack();

        // 4) Sırtüstü yatarken son hafif titreme, sonra tamamen durur.
        yield return Twitch(transform.position, finalTwitchDuration, 0.5f);

        // 5) Hareketsiz ceset bekler, sonra söner.
        yield return corpseWait;
        corpses.Remove(this);
        yield return FadeAndReturn();
    }

    // basePos çevresinde yumuşak rastgele titreme. Her twitchInterval'da yeni hedef seçilir, aradaki geçiş yumuşaktır.
    // Y'ye hiç dokunulmaz (havaya kalkmaz). Süre bitince tam başlangıç konumu/ölçeğine döner.
    private IEnumerator Twitch(Vector3 basePos, float duration, float strength)
    {
        Vector3 offset = Vector3.zero;
        Vector3 targetOffset = Vector3.zero;
        float scaleOffset = 0f;
        float targetScale = 0f;
        float nextPick = 0f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (t >= nextPick)
            {
                nextPick = t + twitchInterval;
                Vector2 r = Random.insideUnitCircle * (convulsionIntensity * strength);
                targetOffset = new Vector3(r.x, 0f, r.y);
                targetScale = Random.Range(-legTwitchScale, legTwitchScale) * strength;
            }

            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            offset = Vector3.Lerp(offset, targetOffset, k);
            scaleOffset = Mathf.Lerp(scaleOffset, targetScale, k);

            transform.position = basePos + offset;
            transform.localScale = baseScale * (1f + scaleOffset);
            yield return null;
        }

        transform.position = basePos;
        transform.localScale = baseScale;
    }

    // X ekseninde 180° (flipDuration, yumuşak). Pivot nerede olursa olsun zemine oturması için
    // her karede en alt noktayı başlangıçtaki zemin seviyesine eşitler.
    private IEnumerator FlipOnBack()
    {
        Quaternion startRot = transform.rotation;
        Vector3 axis = transform.right;
        float floorY = GetMinY();

        for (float t = 0f; t < flipDuration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / flipDuration);
            transform.rotation = Quaternion.AngleAxis(180f * k, axis) * startRot;
            transform.position += Vector3.up * (floorY - GetMinY());
            yield return null;
        }

        transform.rotation = Quaternion.AngleAxis(180f, axis) * startRot;
        transform.position += Vector3.up * (floorY - GetMinY());
    }

    // Görünür mesh'lerin en alt Y'si (dünya). Partikül renderer'ları sayılmaz.
    private float GetMinY()
    {
        float minY = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is ParticleSystemRenderer) continue;
            minY = Mathf.Min(minY, renderers[i].bounds.min.y);
        }
        return minY == float.MaxValue ? transform.position.y : minY;
    }

    // Ceset sınırı aşılınca çağrılır: sönmeden, anında havuza döner.
    private void RemoveCorpseNow()
    {
        if (!gameObject.activeInHierarchy) return;
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }
        ReturnToPool();
    }

    // fadeDuration boyunca alpha'yı 1'den 0'a indirir (MaterialPropertyBlock), sonra havuza döner.
    private IEnumerator FadeAndReturn()
    {
        isFading = true;
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        // Opaque materyalde alpha görünmez: sönerken şeffaf ortak materyale geç (kopya oluşmaz).
        if (corpseFadeMaterial != null)
        {
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = corpseFadeMaterial;
        }

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            SetAlpha(1f - t / fadeDuration);
            yield return null;
        }
        SetAlpha(0f);

        // Fizik kapanır, havuza döner.
        ReturnToPool();
    }

    // Tüm renderer'ların alpha'sını property block ile ayarlar (materyal kopyalanmaz).
    private void SetAlpha(float alpha)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = originalColors[i];
            c.a *= alpha;
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, c);
            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    // Havuza geri verir (havuz yoksa sadece kapatır).
    private void ReturnToPool()
    {
        if (PestPool.Instance != null)
            PestPool.Instance.Return(gameObject);
        else
            gameObject.SetActive(false);
    }

    // Seçiliyken algılama yarıçapını Scene görünümünde kırmızı küre olarak çizer.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
