using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Böcekleri baştan üretip saklayan basit nesne havuzu. Her yerden PestPool.Instance ile erişilir.
public class PestPool : MonoBehaviour
{
    public static PestPool Instance { get; private set; }

    [SerializeField] private GameObject pestPrefab;
    [SerializeField, Min(0)] private int poolSize = 50;
    [Tooltip("Havuzdaki böcekler bunun altında durur. Boşsa bu obje kullanılır.")]
    [SerializeField] private Transform poolParent;

    // Prefabdaki NavMeshAgent'ın ajan tipi; spawner doğru NavMesh'te nokta arasın diye.
    public int AgentTypeID
    {
        get
        {
            if (pestPrefab != null && pestPrefab.TryGetComponent(out NavMeshAgent agent))
                return agent.agentTypeID;
            return 0;
        }
    }

    // Şu an sahnede aktif olan (havuzdan çıkmış) böcek sayısı.
    public int ActiveCount => owned.Count - available.Count;
    // Bu sahnede havuzdan çıkan toplam böcek (JobManager temizlik oranı için okur).
    public int TotalSpawned { get; private set; }

    private readonly Queue<GameObject> available = new Queue<GameObject>();
    // Kuyrukta olanlar: aynı böcek iki kez geri verilirse yakalamak için.
    private readonly HashSet<GameObject> inQueue = new HashSet<GameObject>();
    // Havuza ait olan tüm böcekler (aktif + pasif).
    private readonly HashSet<GameObject> owned = new HashSet<GameObject>();
    // Kapalı ara obje: altında üretilen böceğin Awake/OnEnable'ı erkenden çalışmasın.
    private Transform inactiveHolder;

    // Domain reload kapalıyken eski sahnenin havuzu kalmasın.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    // Singleton'ı kurar ve havuzu poolSize kadar doldurur.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Sahnede birden fazla PestPool var, fazlası siliniyor.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (poolParent == null)
            poolParent = transform;

        GameObject holder = new GameObject("PoolHolder (inactive)");
        holder.SetActive(false);
        inactiveHolder = holder.transform;
        inactiveHolder.SetParent(transform, false);

        if (pestPrefab == null)
        {
            Debug.LogError("PestPool: pestPrefab atanmamış!", this);
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            GameObject pest = CreatePest();
            available.Enqueue(pest);
            inQueue.Add(pest);
        }
    }

    // Sahne kapanınca singleton referansını temizler.
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Kapalı halde yeni bir böcek üretir (sadece Awake'te ya da havuz boşken çağrılır).
    private GameObject CreatePest()
    {
        // Kapalı ebeveyn altında üretilince Awake/OnEnable çalışmaz, NavMesh uyarısı da çıkmaz.
        GameObject pest = Instantiate(pestPrefab, inactiveHolder);
        pest.SetActive(false);
        pest.transform.SetParent(poolParent, false);
        owned.Add(pest);
        return pest;
    }

    // Havuzdan bir böcek alır, verilen konuma koyar ve açar. Havuz boşsa yenisini üretir.
    public GameObject Get(Vector3 position)
    {
        GameObject pest;
        if (available.Count > 0)
        {
            pest = available.Dequeue();
            inQueue.Remove(pest);
        }
        else
        {
            if (pestPrefab == null) return null;
            Debug.LogWarning($"PestPool boşaldı, yeni böcek üretiliyor. poolSize'ı artır (şu an toplam {owned.Count}).", this);
            pest = CreatePest();
        }

        // Konum açmadan önce verilir ki OnEnable'da agent doğru yerde başlasın.
        pest.transform.position = position;
        pest.SetActive(true);
        // Ortaya çıkan her böcek temizlik hedefine eklenir.
        TotalSpawned++;
        return pest;
    }

    // Böceği kapatıp havuza geri koyar. Havuzdan gelmemiş böcekleri de kabul eder.
    public void Return(GameObject obj)
    {
        if (obj == null || inQueue.Contains(obj)) return;

        if (owned.Add(obj))
            obj.transform.SetParent(poolParent, true);

        obj.SetActive(false);
        available.Enqueue(obj);
        inQueue.Add(obj);
    }
}
