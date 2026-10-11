using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Oyun başında böcekleri havuzdan alıp NavMesh üzerinde rastgele noktalara dağıtır.
public class PestSpawner : MonoBehaviour
{
    private const int MaxTries = 10;
    // Rastgele noktanın NavMesh'e en fazla bu kadar uzaktan yapıştırılmasına izin verilir.
    private const float SnapDistance = 2f;

    [SerializeField, Min(0)] private int spawnCount = 30;
    [SerializeField, Min(0.1f)] private float spawnAreaRadius = 10f;
    [Tooltip("Boşsa bu objenin konumu merkez alınır.")]
    [SerializeField] private Transform spawnCenter;
    [Header("Başlangıç")]
    [Tooltip("Oyun başladıktan sonra böceklerin ilk yerleşmesine kadar beklenen süre (sn).")]
    [SerializeField, Min(0f)] private float startDelay = 5f;
    [Tooltip("Yerleşim bir karede takılmasın diye her karede en fazla bu kadar böcek yerleşir.")]
    [SerializeField, Min(1)] private int spawnsPerFrame = 5;

    // Sahnede havuzdan çıkmış aktif böcek sayısı.
    public int ActivePestCount => PestPool.Instance != null ? PestPool.Instance.ActiveCount : 0;

    // Sadece böceğin ajan tipine ait NavMesh'te nokta aranır.
    private NavMeshQueryFilter navFilter;

    // startDelay kadar bekler, sonra spawnCount kadar böceği kareye yayarak yerleştirir; nokta bulunamayanı atlar.
    private void Start()
    {
        if (PestPool.Instance == null)
        {
            Debug.LogError("PestSpawner: sahnede PestPool yok!", this);
            return;
        }

        navFilter.agentTypeID = PestPool.Instance.AgentTypeID;
        navFilter.areaMask = NavMesh.AllAreas;

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);
        while (!SpawnSwitch.Enabled)
            yield return null;

        int spawned = 0;
        for (int i = 0; i < spawnCount; i++)
        {
            if (TryGetRandomPoint(out Vector3 point) && SpawnOne(point))
                spawned++;

            if ((i + 1) % spawnsPerFrame == 0)
                yield return null;
        }

        if (spawned < spawnCount)
            Debug.LogWarning($"PestSpawner: {spawnCount} böcekten {spawned} tanesi yerleşti, gerisi için NavMesh noktası bulunamadı.", this);
    }

    // Merkez etrafında NavMesh üzerinde geçerli bir nokta arar, en fazla 10 deneme.
    private bool TryGetRandomPoint(out Vector3 point)
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;

        for (int i = 0; i < MaxTries; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnAreaRadius;
            Vector3 candidate = new Vector3(center.x + offset.x, center.y, center.z + offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, SnapDistance, navFilter))
            {
                point = hit.position;
                return true;
            }
        }

        point = Vector3.zero;
        return false;
    }

    // Verilen konuma tek böcek ekler (yuva sistemi kullanır). Konum NavMesh'e yapıştırılır.
    public bool SpawnOne(Vector3 pos)
    {
        if (PestPool.Instance == null)
        {
            Debug.LogError("PestSpawner: sahnede PestPool yok!", this);
            return false;
        }

        // Filtre her seferinde güncellenir: prefab değişirse de doğru NavMesh kullanılır (struct, çöp üretmez).
        navFilter.agentTypeID = PestPool.Instance.AgentTypeID;
        navFilter.areaMask = NavMesh.AllAreas;

        if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, SnapDistance, navFilter))
            return false;

        return PestPool.Instance.Get(hit.position) != null;
    }

    // Seçiliyken yerleştirme alanını Scene görünümünde sarı daire olarak çizer.
    private void OnDrawGizmosSelected()
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, spawnAreaRadius);
    }
}
