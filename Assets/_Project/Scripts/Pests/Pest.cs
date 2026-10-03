using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// NavMesh üzerinde dolaşan basit haşere. Object pool'a hazır: her OnEnable'da baştan kurulur.
[RequireComponent(typeof(NavMeshAgent))]
public class Pest : MonoBehaviour, IDamageable
{
    private enum PestState { Idle, Dead }

    [Header("Species")]
    [SerializeField] private string speciesName = "Hamambocegi";

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float wanderRadius = 3f;
    [SerializeField, Min(0f)] private float minIdleWait = 0.5f;
    [SerializeField, Min(0f)] private float maxIdleWait = 2f;
    [SerializeField, Min(0f)] private float moveSpeed = 1.2f;
    [Tooltip("Hedefe bu kadar yaklaşınca varmış sayılır.")]
    [SerializeField, Min(0.01f)] private float arriveDistance = 0.15f;
    [Tooltip("Bir hedefe bu süreden uzun gidemezse yeni hedef seçer (takılma önlemi).")]
    [SerializeField, Min(0.5f)] private float giveUpTime = 6f;

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 20f;
    [Tooltip("Her hasarda geriye itilme mesafesi (metre).")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.05f;

    [Header("Death")]
    [SerializeField, Min(0f)] private float corpseDuration = 10f;
    [Tooltip("Pivot zemindeyse ters dönünce gövde yere gömülür. Gövde yüksekliği kadar ver.")]
    [SerializeField, Min(0f)] private float deathLiftHeight = 0f;
    [SerializeField] private UnityEvent onDeath;

    [Header("Performance")]
    [SerializeField, Min(0.05f)] private float thinkInterval = 0.2f;

    public bool IsAlive => state != PestState.Dead && currentHealth > 0f;
    public string SpeciesName => speciesName;
    public UnityEvent OnDeath => onDeath;

    private NavMeshAgent agent;
    private Collider bodyCollider;
    private WaitForSeconds thinkWait;
    private WaitForSeconds corpseWait;
    private Coroutine thinkRoutine;

    private PestState state;
    private float currentHealth;
    private bool isWaiting;
    private float waitUntil;
    private float moveStartTime;

    // Referansları ve bekleme nesnelerini bir kere oluşturur.
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        bodyCollider = GetComponentInChildren<Collider>();
        agent.speed = moveSpeed;

        thinkWait = new WaitForSeconds(thinkInterval);
        corpseWait = new WaitForSeconds(corpseDuration);
    }

    // Havuzdan her çıkışta haşereyi canlı ve temiz hale getirir.
    private void OnEnable()
    {
        state = PestState.Idle;
        currentHealth = maxHealth;

        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

        if (bodyCollider != null)
            bodyCollider.isTrigger = false;
        agent.enabled = true;

        // Hemen bir hedef seçmesin, rastgele kısa bir süre dursun.
        StartWaiting();
        thinkRoutine = StartCoroutine(ThinkLoop());
    }

    // Kapanınca çalışan tüm coroutine'leri durdurur.
    private void OnDisable()
    {
        StopAllCoroutines();
        thinkRoutine = null;
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
        switch (state)
        {
            case PestState.Idle:
                UpdateIdle();
                break;
            case PestState.Dead:
                break;
        }
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

        Vector3 toTarget = agent.destination - transform.position;
        toTarget.y = 0f;
        bool arrived = toTarget.sqrMagnitude <= arriveDistance * arriveDistance;
        bool stuck = Time.time - moveStartTime > giveUpTime;
        bool badPath = agent.pathStatus == NavMeshPathStatus.PathInvalid;

        if (arrived || stuck || badPath)
            StartWaiting();
    }

    // Etrafta NavMesh üzerinde geçerli rastgele bir nokta bulup oraya yürür.
    private void TryPickNewDestination()
    {
        Vector3 randomPoint = transform.position + Random.insideUnitSphere * wanderRadius;

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
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

    // Canı azaltır, hafifçe geri iter, can biterse öldürür.
    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsAlive || amount <= 0f) return;

        currentHealth -= amount;

        // Agent açıkken transform'u elle kaydırmak yerine agent.Move: NavMesh dışına itilmez.
        Vector3 push = hitDirection;
        push.y = 0f;
        if (push.sqrMagnitude > 0.0001f && agent.isOnNavMesh)
            agent.Move(push.normalized * knockbackDistance);

        if (currentHealth <= 0f)
            Die();
    }

    // Ölüm: agent kapanır, ters döner, engel olmaz, olay tetiklenir, süre sonra kapanır.
    private void Die()
    {
        state = PestState.Dead;
        currentHealth = 0f;

        if (thinkRoutine != null)
        {
            StopCoroutine(thinkRoutine);
            thinkRoutine = null;
        }

        agent.enabled = false;
        transform.Rotate(180f, 0f, 0f, Space.Self);
        transform.position += Vector3.up * deathLiftHeight;
        if (bodyCollider != null)
            bodyCollider.isTrigger = true;

        onDeath?.Invoke();
        StartCoroutine(DisableAfterDelay());
    }

    // Cesedi bir süre bırakır, sonra objeyi kapatır (Destroy değil, pool için).
    private IEnumerator DisableAfterDelay()
    {
        yield return corpseWait;
        gameObject.SetActive(false);
    }
}
