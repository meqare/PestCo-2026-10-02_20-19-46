using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Belirli aralıklarla havuzdan böcek doğuran, spreyle yok edilebilen yuva.
public class PestNest : MonoBehaviour, IDamageable
{
    // Doğma noktası NavMesh'e en fazla bu kadar uzaktan yapıştırılır.
    private const float SnapDistance = 1f;

    [Header("Spawning")]
    [SerializeField, Min(0.1f)] private float spawnInterval = 4f;
    [SerializeField, Min(0)] private int maxActivePests = 12;
    [Tooltip("Yuvanın ~0.3 m önünde boş bir obje. Boşsa yuvanın kendisi kullanılır.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Böcekler üst üste doğmasın diye spawnPoint etrafındaki rastgele kayma.")]
    [SerializeField, Min(0f)] private float spawnJitter = 0.2f;

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 60f;
    [Tooltip("Vuruşta ölçeğin ne kadar büyüyeceği (0.1 = %10).")]
    [SerializeField, Min(0f)] private float punchAmount = 0.1f;
    [SerializeField, Min(0.01f)] private float punchDuration = 0.05f;

    [Header("Death")]
    [SerializeField, Min(0)] private int pestsReleasedOnDeath = 3;
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField, Min(0f)] private float disableDelay = 2f;
    [SerializeField] private UnityEvent onNestDestroyed;

    public bool IsAlive => currentHealth > 0f;
    public UnityEvent OnNestDestroyed => onNestDestroyed;
    // Böceklerin saklanmak için koştuğu giriş noktası (spawnPoint).
    public Vector3 EntrancePosition => spawnPoint != null ? spawnPoint.position : transform.position;
    // Sahnede hâlâ ayakta olan yuva sayısı (JobManager okur).
    public static int ActiveNestCount { get; private set; }
    // Herhangi bir yuva yok edilince tetiklenir (yuvanın giriş konumu). AnomalyManager dinler.
    public static event System.Action<Vector3> OnAnyNestDestroyed;
    // Ayakta olan yuvalar (yuvasız böcekler en yakınına saklanır).
    public static IReadOnlyList<PestNest> ActiveNests => activeNests;
    private static readonly List<PestNest> activeNests = new List<PestNest>();

    // Bu yuvadan çıkmış, hâlâ yaşayan böcekler ve OnDeath'e eklenen dinleyicileri.
    private readonly Dictionary<Pest, UnityAction> livingPests = new Dictionary<Pest, UnityAction>();

    private Renderer[] renderers;
    private Collider[] colliders;
    private WaitForSeconds spawnWait;
    private WaitForSeconds punchWait;
    private WaitForSeconds disableWait;
    private Coroutine spawnRoutine;
    private Coroutine punchRoutine;
    private Vector3 baseScale;
    private NavMeshQueryFilter navFilter;
    private float currentHealth;
    private bool countedAsActive;

    // Domain reload kapalıyken sayaç eski oyundan kalmasın.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveNestCount = 0;
        activeNests.Clear();
        OnAnyNestDestroyed = null;
    }

    // Bileşenleri ve bekleme nesnelerini bir kere hazırlar.
    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        spawnWait = new WaitForSeconds(spawnInterval);
        punchWait = new WaitForSeconds(punchDuration);
        disableWait = new WaitForSeconds(disableDelay);
        baseScale = transform.localScale;

        if (spawnPoint == null)
            spawnPoint = transform;
    }

    // Yuvayı canlı hale getirir, sayaca ekler ve doğurmaya başlar.
    private void OnEnable()
    {
        currentHealth = maxHealth;
        transform.localScale = baseScale;
        SetVisible(true);

        if (!countedAsActive)
        {
            countedAsActive = true;
            ActiveNestCount++;
            activeNests.Add(this);
        }

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    // Kapanırken coroutine'leri durdurur; canlıyken kapandıysa sayaçtan düşer.
    private void OnDisable()
    {
        StopAllCoroutines();
        spawnRoutine = null;
        punchRoutine = null;
        transform.localScale = baseScale;
        RemoveFromActiveCount();
    }

    // Yuva yok olurken böceklerin OnDeath'inden dinleyicileri temizler.
    private void OnDestroy()
    {
        foreach (KeyValuePair<Pest, UnityAction> pair in livingPests)
        {
            if (pair.Key != null)
                pair.Key.OnDeath.RemoveListener(pair.Value);
        }
        livingPests.Clear();
    }

    // Her spawnInterval'da bir, sınır dolmadıysa bir böcek doğurur.
    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return spawnWait;

            if (livingPests.Count < maxActivePests)
            {
                Pest pest = SpawnPest();
                if (pest != null)
                    TrackPest(pest);
            }
        }
    }

    // spawnPoint yakınında NavMesh'e oturan bir noktada havuzdan böcek alır.
    private Pest SpawnPest()
    {
        PestPool pool = PestPool.Instance;
        if (pool == null)
        {
            Debug.LogError("PestNest: sahnede PestPool yok!", this);
            return null;
        }

        Vector2 jitter = Random.insideUnitCircle * spawnJitter;
        Vector3 candidate = spawnPoint.position + new Vector3(jitter.x, 0f, jitter.y);

        navFilter.agentTypeID = pool.AgentTypeID;
        navFilter.areaMask = NavMesh.AllAreas;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, SnapDistance, navFilter))
            return null;

        GameObject obj = pool.Get(hit.position);
        if (obj == null) return null;

        if (!obj.TryGetComponent(out Pest pest))
        {
            Debug.LogError("PestNest: havuzdaki prefabda Pest bileşeni yok!", obj);
            return null;
        }
        return pest;
    }

    // Böceğin ölümünü dinler (ölünce bu yuvanın sayacından düşer) ve ona evini söyler.
    private void TrackPest(Pest pest)
    {
        UnityAction handler = null;
        handler = () =>
        {
            pest.OnDeath.RemoveListener(handler);
            livingPests.Remove(pest);
        };

        pest.OnDeath.AddListener(handler);
        livingPests[pest] = handler;
        pest.SetHomeNest(this);
    }

    // Canı azaltır, kısa bir titreşim oynatır, can biterse yuvayı yok eder.
    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsAlive || amount <= 0f) return;

        currentHealth -= amount;

        if (punchRoutine != null)
            StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(Punch());

        if (currentHealth <= 0f)
        {
            Juice.NestDeath(transform.position);
            Die();
        }
    }

    // Ölçeği kısa süreliğine büyütüp geri getirir (vuruş hissi).
    private IEnumerator Punch()
    {
        transform.localScale = baseScale * (1f + punchAmount);
        yield return punchWait;
        transform.localScale = baseScale;
        punchRoutine = null;
    }

    // Doğurmayı durdurur, son böcekleri kaçırır, efekti oynatır ve yuvayı kapatır.
    private void Die()
    {
        currentHealth = 0f;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
            punchRoutine = null;
        }
        transform.localScale = baseScale;

        // Yuvadan son kez fırlayan böcekler: takip edilmez, hemen kaçar.
        for (int i = 0; i < pestsReleasedOnDeath; i++)
        {
            Pest pest = SpawnPest();
            if (pest != null)
                pest.ForceFlee();
        }

        if (deathEffect != null)
            deathEffect.Play(true);

        SetVisible(false);
        RemoveFromActiveCount();
        onNestDestroyed?.Invoke();
        OnAnyNestDestroyed?.Invoke(EntrancePosition);

        StartCoroutine(DisableAfterDelay());
    }

    // Yuvanın mesh'lerini ve collider'larını birlikte açar/kapatır.
    private void SetVisible(bool visible)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            // Partikül efektinin kendi renderer'ına dokunma, yoksa patlama görünmez.
            if (renderers[i] is ParticleSystemRenderer) continue;
            renderers[i].enabled = visible;
        }

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = visible;
    }

    // Yuva sayacından bir kere düşer (hem ölümde hem kapanışta çağrılabilir).
    private void RemoveFromActiveCount()
    {
        if (!countedAsActive) return;
        countedAsActive = false;
        ActiveNestCount--;
        activeNests.Remove(this);
    }

    // Efekt bitsin diye biraz bekleyip objeyi kapatır.
    private IEnumerator DisableAfterDelay()
    {
        yield return disableWait;
        gameObject.SetActive(false);
    }
}
