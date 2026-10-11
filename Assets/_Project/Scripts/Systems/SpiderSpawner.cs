using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Oyun başında örümcekleri zeminde geçerli bir NavMesh noktasının üstündeki tavana yerleştirir.
// Hamamböceği/fare havuzundan (PestPool) bağımsızdır.
public class SpiderSpawner : MonoBehaviour
{
    private const int MaxSpiders = 15;
    private const int MaxTries = 10;

    [SerializeField] private Spider spiderPrefab;
    [SerializeField, Range(0, MaxSpiders)] private int spawnCount = 4;
    [SerializeField, Min(0.1f)] private float spawnAreaRadius = 10f;
    [Tooltip("Boşsa bu objenin konumu merkez alınır.")]
    [SerializeField] private Transform spawnCenter;
    [Header("Başlangıç")]
    [Tooltip("Oyun başladıktan sonra örümceklerin ilk yerleşmesine kadar beklenen süre (sn).")]
    [SerializeField, Min(0f)] private float startDelay = 5f;

    public int ActiveCount { get; private set; }

    private void Start()
    {
        if (spiderPrefab == null)
        {
            Debug.LogError("SpiderSpawner: spiderPrefab atanmamış!", this);
            return;
        }

        StartCoroutine(SpawnRoutine());
    }

    // startDelay kadar bekler, sonra her karede en fazla bir örümcek yerleştirir (takılmayı önler).
    private IEnumerator SpawnRoutine()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);
        while (!SpawnSwitch.Enabled)
            yield return null;

        int agentType = PestPool.Instance != null ? PestPool.Instance.AgentTypeID : 0;
        var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;

        int count = Mathf.Min(spawnCount, MaxSpiders);
        for (int i = 0; i < count; i++)
        {
            if (TryGetFloorPoint(center, filter, out Vector3 floorPoint))
            {
                Spider s = Instantiate(spiderPrefab, floorPoint, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                s.Init(floorPoint, filter);
                ActiveCount++;
            }
            yield return null;
        }

        if (ActiveCount < count)
            Debug.LogWarning($"SpiderSpawner: {count} örümcekten {ActiveCount} tanesi yerleşti.", this);
    }

    private bool TryGetFloorPoint(Vector3 center, NavMeshQueryFilter filter, out Vector3 point)
    {
        for (int i = 0; i < MaxTries; i++)
        {
            Vector2 o = Random.insideUnitCircle * spawnAreaRadius;
            Vector3 cand = new Vector3(center.x + o.x, center.y, center.z + o.y);
            if (NavMesh.SamplePosition(cand, out NavMeshHit hit, 2f, filter))
            {
                point = hit.position;
                return true;
            }
        }
        point = Vector3.zero;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;
        Gizmos.color = new Color(0.6f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(center, spawnAreaRadius);
    }
}
