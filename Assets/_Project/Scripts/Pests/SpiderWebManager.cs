using UnityEngine;

// Örümcek ağlarını havuzlar (en fazla maxWebs aynı anda), süresi dolanları kapatır ve oyuncu ağa değince yavaşlatır.
// Sahnede yoksa ilk ağ bırakıldığında kendiliğinden oluşur.
public class SpiderWebManager : MonoBehaviour
{
    private const int MaxWebs = 10;

    private static SpiderWebManager instance;

    private readonly GameObject[] webs = new GameObject[MaxWebs];
    private readonly float[] spawnTimes = new float[MaxWebs];
    private float lifetime = 30f;
    private float webRadius = 0.25f;
    private float slowMultiplier = 0.5f;
    private float slowDuration = 2f;
    private Transform player;
    private PlayerStatus playerStatus;
    private float checkTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    // Ağ bırakır. Havuz doluysa en eski ağ yeniden kullanılır.
    public static void Spawn(Vector3 position, Material material, Material rayMaterial, float lifetime, float slowMultiplier, float slowDuration)
    {
        if (instance == null)
        {
            var go = new GameObject("SpiderWebManager");
            instance = go.AddComponent<SpiderWebManager>();
        }
        instance.lifetime = lifetime;
        instance.slowMultiplier = slowMultiplier;
        instance.slowDuration = slowDuration;
        instance.Place(position, material, rayMaterial);
    }

    private void Place(Vector3 position, Material material, Material rayMaterial)
    {
        int index = -1;
        float oldest = float.MaxValue;
        for (int i = 0; i < MaxWebs; i++)
        {
            if (webs[i] == null || !webs[i].activeSelf) { index = i; break; }
            if (spawnTimes[i] < oldest) { oldest = spawnTimes[i]; index = i; }
        }

        if (webs[index] == null)
            webs[index] = BuildWeb(material, rayMaterial);

        webs[index].transform.position = position;
        webs[index].SetActive(true);
        spawnTimes[index] = Time.time;
    }

    // Ağ: yarı saydam tam küre + merkezden 6 yöne (±X, ±Y, ±Z) ince ışın.
    private GameObject BuildWeb(Material material, Material rayMaterial)
    {
        var web = new GameObject("SpiderWeb");
        web.transform.SetParent(transform, false);

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Orb";
        sphere.transform.SetParent(web.transform, false);
        sphere.transform.localScale = Vector3.one * (webRadius * 2f);
        PrepareRenderer(sphere, material);

        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 d in dirs)
        {
            var ray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ray.name = "Ray";
            ray.transform.SetParent(web.transform, false);
            // Silindir Y ekseninde 2 birim uzun: yarıçap kadar uzatıp yöne çevir, merkezden dışarı baksın.
            ray.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            ray.transform.localPosition = d * (webRadius * 0.5f);
            ray.transform.localScale = new Vector3(0.006f, webRadius * 0.5f, 0.006f);
            PrepareRenderer(ray, rayMaterial != null ? rayMaterial : material);
        }
        return web;
    }

    private static void PrepareRenderer(GameObject go, Material material)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (material != null) r.sharedMaterial = material;
    }

    // Her 0.1 sn'de: süresi dolanı kapat, oyuncu içindeyse yavaşlat (max 10 ağ, ucuz).
    private void Update()
    {
        // Hafif nabız: aktif ağlar (en çok 10) yavaşça büyüyüp küçülür.
        for (int i = 0; i < MaxWebs; i++)
        {
            GameObject web = webs[i];
            if (web == null || !web.activeSelf) continue;
            web.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 3f + i));
        }

        checkTimer += Time.deltaTime;
        if (checkTimer < 0.1f) return;
        checkTimer = 0f;

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                playerStatus = p.GetComponent<PlayerStatus>();
            }
        }

        float touchRadius = webRadius + 0.3f;
        float touchSqr = touchRadius * touchRadius;

        for (int i = 0; i < MaxWebs; i++)
        {
            GameObject web = webs[i];
            if (web == null || !web.activeSelf) continue;

            if (Time.time - spawnTimes[i] >= lifetime)
            {
                web.SetActive(false);
                continue;
            }

            if (playerStatus == null) continue;
            Vector3 d = web.transform.position - player.position;
            if (Mathf.Abs(d.y) > 1.3f) continue;
            d.y = 0f;
            if (d.sqrMagnitude <= touchSqr)
                playerStatus.ApplySlow(slowMultiplier, slowDuration);
        }
    }
}
