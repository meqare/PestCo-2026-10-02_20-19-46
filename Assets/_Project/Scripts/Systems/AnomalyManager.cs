using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Her işin başında %anomalyChance ihtimalle bir anomali seçer ve o iş boyunca tek anomaliyi aktif tutar.
// Zamanlama tamamen coroutine ile yapılır (Update yok). İş bitince her şey normale döner.
public class AnomalyManager : MonoBehaviour
{
    public static AnomalyManager Instance { get; private set; }

    // Şu an aktif (seçilmiş) anomali; yoksa null.
    public AnomalyData ActiveAnomaly { get; private set; }
    // Anomali gerçekten tetiklendi mi (seçilmek ile tetiklenmek ayrıdır).
    public bool IsTriggered { get; private set; }

    [Header("Anomaliler")]
    [Tooltip("6 anomali asset'i (Assets/_Project/Data/Anomalies).")]
    [SerializeField] private List<AnomalyData> anomalies = new List<AnomalyData>();
    [SerializeField, Range(0f, 1f)] private float anomalyChance = 0.12f;
    [Tooltip("TEST: açıkken her iş anomaliyle başlar.")]
    [SerializeField] private bool forceAnomaly = false;
    [Tooltip("TEST: forceAnomaly açıkken sadece aşağıdaki türü seçer.")]
    [SerializeField] private bool useForcedType = false;
    [SerializeField] private AnomalyType forcedType;
    [Tooltip("triggerDelay 0 olan anomaliler için rastgele gecikme aralığı (sn).")]
    [SerializeField, Min(0f)] private float minAnomalyDelay = 15f;
    [SerializeField, Min(0f)] private float maxAnomalyDelay = 90f;

    [Header("Duvarda Nefes")]
    [SerializeField, Min(0.5f)] private float pulseInterval = 3f;
    [SerializeField, Min(1f)] private float pulseScale = 1.05f;
    [SerializeField, Min(0.2f)] private float pulseDuration = 1.2f;

    [Header("Dev Hamamböceği")]
    [SerializeField, Min(1f)] private float giantScale = 3f;
    [SerializeField, Min(1f)] private float giantHealth = 100f;
    [SerializeField, Min(0.1f)] private float giantSpeed = 3f;
    [SerializeField, Min(1f)] private float giantLifetime = 15f;

    [Header("Bodrum Siluet")]
    [Tooltip("Oyuncunun Y'si bunun altına inince 'bodruma indi' sayılır. Projede bodrum yoksa tetiklenmez.")]
    [SerializeField] private float basementTriggerY = -1.5f;
    [SerializeField, Min(0f)] private float shadowRespawnDelay = 5f;
    [SerializeField, Min(0.5f)] private float shadowDisappearDistance = 4f;

    [Header("Kapı")]
    [SerializeField, Min(0f)] private float doorCloseDelay = 2f;
    [SerializeField, Min(1f)] private float doorLockDuration = 10f;

    [Header("Fenerin Göstermediği Şey")]
    [SerializeField, Min(0.1f)] private float lightTrickDuration = 0.3f;

    [Header("Post-Process (anomali aktifken)")]
    [SerializeField, Range(0f, 1f)] private float targetVignette = 0.5f;
    [Tooltip("Doygunluğa eklenen değer (negatif = daha az renk).")]
    [SerializeField] private float saturationDelta = -15f;
    [SerializeField] private float contrastDelta = 15f;
    [SerializeField, Range(0f, 1f)] private float targetGrain = 0.5f;
    [SerializeField, Min(0.1f)] private float postFadeTime = 2f;

    private Camera cam;
    private Transform player;
    private Flashlight flashlight;
    private readonly List<GameObject> spawned = new List<GameObject>();

    // Duvar anomalisi
    private Transform wall;
    private Vector3 wallOriginalScale;

    // Dev böcek anomalisi
    private bool giantArmed;

    // Post-process
    private Volume volume;
    private bool ppCaptured;
    private Vignette vignette;
    private ColorAdjustments colorAdj;
    private FilmGrain grain;
    private DepthOfField dof;
    private bool dofAdded;
    private float origVignette, origSat, origContrast, origGrain;
    private bool origGrainActive, origDofActive;

    private struct DoorInfo
    {
        public Bounds bounds;
        public bool alongZ;      // geçiş ekseni Z mi (ince eksen Z ise)
        public int lastSide;     // -1 / +1 / 0 (henüz bilinmiyor)
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Sahnede birden fazla AnomalyManager var, fazlası siliniyor.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        JobManager.OnJobStarted += HandleJobStarted;
        JobManager.OnJobComplete += HandleJobComplete;
    }

    private void OnDisable()
    {
        JobManager.OnJobStarted -= HandleJobStarted;
        JobManager.OnJobComplete -= HandleJobComplete;
        EndAnomaly();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    // ---------- Seçim ----------

    private void HandleJobStarted()
    {
        EndAnomaly();
        if (anomalies == null || anomalies.Count == 0) return;

        float roll = Random.value;
        if (!forceAnomaly && roll >= anomalyChance)
        {
            Debug.Log($"[ANOMALY] Yok (zar {roll:0.00} >= %{anomalyChance * 100f:0})");
            return;
        }

        AnomalyData data = PickAnomaly();
        if (data == null) return;

        ActiveAnomaly = data;
        IsTriggered = false;
        CacheReferences();
        Debug.Log($"[ANOMALY] Aktif: {data.anomalyName}");
        StartCoroutine(Run(data));
    }

    private AnomalyData PickAnomaly()
    {
        if (forceAnomaly && useForcedType)
        {
            for (int i = 0; i < anomalies.Count; i++)
                if (anomalies[i] != null && anomalies[i].type == forcedType) return anomalies[i];
        }

        float sum = 0f;
        for (int i = 0; i < anomalies.Count; i++)
            if (anomalies[i] != null) sum += anomalies[i].rarity;

        if (sum <= 0f)
            return anomalies[Random.Range(0, anomalies.Count)];

        float r = Random.value * sum;
        for (int i = 0; i < anomalies.Count; i++)
        {
            if (anomalies[i] == null) continue;
            r -= anomalies[i].rarity;
            if (r <= 0f) return anomalies[i];
        }
        return anomalies[anomalies.Count - 1];
    }

    private void CacheReferences()
    {
        cam = Camera.main;
        var p = GameObject.FindGameObjectWithTag("Player");
        player = p != null ? p.transform : null;
        flashlight = FindFirstObjectByType<Flashlight>();
        volume = FindFirstObjectByType<Volume>();
    }

    [ContextMenu("TEST: Anomaliyi şimdi başlat")]
    private void DebugStartNow()
    {
        bool prev = forceAnomaly;
        forceAnomaly = true;
        HandleJobStarted();
        forceAnomaly = prev;
    }

    // ---------- Bitiş ----------

    private void HandleJobComplete(JobResult result)
    {
        EndAnomaly();
    }

    // Aktif anomaliyi durdurur, geçici objeleri siler, değişen her şeyi geri alır.
    private void EndAnomaly()
    {
        StopAllCoroutines();
        PestNest.OnAnyNestDestroyed -= OnNestDestroyed;
        giantArmed = false;

        if (wall != null) wall.localScale = wallOriginalScale;
        wall = null;

        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();

        RestorePostProcess();
        if (ActiveAnomaly != null)
            Debug.Log($"[ANOMALY] Bitti: {ActiveAnomaly.anomalyName}");
        ActiveAnomaly = null;
        IsTriggered = false;
    }

    // ---------- Ana akış ----------

    private IEnumerator Run(AnomalyData data)
    {
        // Doğumlar kapalıyken anomali de başlamaz; SpawnSwitch açılınca zamanlayıcılar çalışmaya başlar.
        while (!SpawnSwitch.Enabled)
            yield return null;

        switch (data.type)
        {
            case AnomalyType.WallBreathing: yield return WallBreathing(data); break;
            case AnomalyType.GiantRoach: yield return GiantRoach(data); break;
            case AnomalyType.BasementShadow: yield return BasementShadow(data); break;
            case AnomalyType.HumanVoice: yield return HumanVoice(data); break;
            case AnomalyType.DoorClose: yield return DoorClose(data); break;
            case AnomalyType.LightTrick: yield return LightTrick(data); break;
        }
    }

    private float RollDelay(AnomalyData data)
    {
        if (data.triggerDelay <= 0f)
            return Random.Range(minAnomalyDelay, Mathf.Max(minAnomalyDelay, maxAnomalyDelay));
        if (data.triggerDelayMax > data.triggerDelay)
            return Random.Range(data.triggerDelay, data.triggerDelayMax);
        return data.triggerDelay;
    }

    // İlk tetiklemede: istatistik kaydı, log ve post-process geçişi.
    private void Trigger(AnomalyData data)
    {
        if (IsTriggered) return;
        IsTriggered = true;
        SaveSystem.RegisterAnomaly(data.anomalyName);
        Debug.Log($"[ANOMALY] Tetiklendi: {data.anomalyName}");
        StartCoroutine(FadePostProcess(true));
    }

    private AudioClip SoundOrDefault(AnomalyData data, AudioClip fallback)
    {
        return data.triggerSound != null ? data.triggerSound : fallback;
    }

    // ---------- 1) Duvarda nefes ----------

    private IEnumerator WallBreathing(AnomalyData data)
    {
        yield return new WaitForSeconds(RollDelay(data));

        var walls = new List<Renderer>();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.name.StartsWith("Wall_")) walls.Add(r);
        if (walls.Count == 0)
        {
            Debug.LogWarning("[ANOMALY] Duvar bulunamadı (Wall_* adlı renderer yok).");
            yield break;
        }

        Renderer wallRenderer = walls[Random.Range(0, walls.Count)];
        wall = wallRenderer.transform;
        wallOriginalScale = wall.localScale;
        Trigger(data);

        var wait = new WaitForSeconds(pulseInterval);
        while (true)
        {
            Vector3 center = wallRenderer.bounds.center;
            AnomalySounds.PlayAt(SoundOrDefault(data, AnomalySounds.Breath), center, 0.8f, 0.8f, 14f);

            // Oyuncu o duvara bakıyorsa hafif kamera sarsıntısı.
            if (cam != null && Vector3.Angle(cam.transform.forward, center - cam.transform.position) < 35f
                && (center - cam.transform.position).sqrMagnitude < 12f * 12f)
                Juice.Shake(0.15f, 0.4f);

            for (float t = 0f; t < pulseDuration; t += Time.deltaTime)
            {
                float k = Mathf.Sin(Mathf.PI * t / pulseDuration);
                wall.localScale = wallOriginalScale * (1f + (pulseScale - 1f) * k);
                yield return null;
            }
            wall.localScale = wallOriginalScale;
            yield return wait;
        }
    }

    // ---------- 2) Dev hamamböceği ----------

    private IEnumerator GiantRoach(AnomalyData data)
    {
        // Yuva yıkılınca (OnAnyNestDestroyed) tetiklenir; bekleme süresi yok.
        giantArmed = true;
        PestNest.OnAnyNestDestroyed += OnNestDestroyed;
        yield break;
    }

    private void OnNestDestroyed(Vector3 position)
    {
        if (!giantArmed || ActiveAnomaly == null || ActiveAnomaly.type != AnomalyType.GiantRoach) return;
        giantArmed = false;
        PestNest.OnAnyNestDestroyed -= OnNestDestroyed;
        StartCoroutine(SpawnGiant(ActiveAnomaly, position));
    }

    private IEnumerator SpawnGiant(AnomalyData data, Vector3 nestPos)
    {
        yield return new WaitForSeconds(0.6f);
        if (PestPool.Instance == null) yield break;

        var filter = new NavMeshQueryFilter { agentTypeID = PestPool.Instance.AgentTypeID, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(nestPos, out NavMeshHit hit, 2f, filter)) yield break;

        GameObject go = PestPool.Instance.Get(hit.position);
        if (go != null && go.TryGetComponent(out Pest pest))
        {
            pest.ApplyGiant(giantScale, giantHealth, giantSpeed, giantLifetime);
            pest.ForceFlee();
            Trigger(data);
            AnomalySounds.PlayAt(SoundOrDefault(data, AnomalySounds.Hiss), hit.position, 1f, 0.7f, 16f);
        }
    }

    // ---------- 3) Bodrum siluet ----------

    private IEnumerator BasementShadow(AnomalyData data)
    {
        if (player == null) yield break;

        // Oyuncu bodruma inene kadar bekle (0.25 sn'de bir kontrol).
        var poll = new WaitForSeconds(0.25f);
        while (player.position.y > basementTriggerY)
            yield return poll;

        Trigger(data);
        var shortWait = new WaitForSeconds(0.1f);

        int failures = 0;
        for (int appearance = 0; appearance < 8; appearance++)
        {
            // Üç başarısız denemeden sonra "oyuncu görebilsin" şartı gevşetilir (dar/kapalı yerlerde de belirsin).
            if (!TryFindShadowPoint(failures < 3, out Vector3 point))
            {
                failures++;
                yield return new WaitForSeconds(2f);
                appearance--;
                if (failures > 20) yield break;
                continue;
            }
            failures = 0;

            GameObject shadow = CreateShadow(point);
            AnomalySounds.PlayAt(SoundOrDefault(data, AnomalySounds.Wind), point, 0.6f, 1f, 20f);

            // Oyuncu yaklaşınca (ya da 25 sn sonra) kaybolur.
            float life = 0f;
            float limitSqr = shadowDisappearDistance * shadowDisappearDistance;
            while (life < 25f && (player.position - point).sqrMagnitude > limitSqr)
            {
                life += 0.1f;
                yield return shortWait;
            }

            // Işınlanma efekti: hızla incelip kaybolur.
            Vector3 startScale = shadow.transform.localScale;
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                shadow.transform.localScale = new Vector3(startScale.x * (1f - t / 0.15f), startScale.y * (1f + t / 0.15f), startScale.z * (1f - t / 0.15f));
                yield return null;
            }
            spawned.Remove(shadow);
            Destroy(shadow);

            yield return new WaitForSeconds(shadowRespawnDelay);
        }
    }

    private bool TryFindShadowPoint(bool requireVisible, out Vector3 point)
    {
        for (int i = 0; i < 12; i++)
        {
            float angle = Random.Range(0f, 360f);
            Vector3 cand = player.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(8f, 14f);
            if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            if ((hit.position - player.position).sqrMagnitude < 6f * 6f) continue;
            // Oyuncu görebilsin: arada engel olmasın.
            if (requireVisible && cam != null && Physics.Linecast(cam.transform.position, hit.position + Vector3.up, 1, QueryTriggerInteraction.Ignore)) continue;
            point = hit.position;
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    private GameObject CreateShadow(Vector3 floorPoint)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "AnomalyShadow";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.position = floorPoint + Vector3.up * 0.95f;
        go.transform.localScale = new Vector3(0.6f, 1.9f, 0.4f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = BlackMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        spawned.Add(go);
        return go;
    }

    private Material blackMat;
    private Material BlackMaterial()
    {
        if (blackMat == null)
        {
            blackMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            blackMat.SetColor("_BaseColor", Color.black);
        }
        return blackMat;
    }

    // ---------- 4) İnsan sesi ----------

    private IEnumerator HumanVoice(AnomalyData data)
    {
        yield return new WaitForSeconds(RollDelay(data));

        var nests = PestNest.ActiveNests;
        if (nests.Count == 0)
        {
            Debug.LogWarning("[ANOMALY] Fısıltı için aktif yuva yok.");
            yield break;
        }

        PestNest nest = nests[Random.Range(0, nests.Count)];
        Trigger(data);
        // 3D ses: yuvanın giriş konumundan, ~5 sn.
        AnomalySounds.PlayAt(SoundOrDefault(data, AnomalySounds.Whisper), nest.EntrancePosition, 1f, 0.85f, 14f);
        yield return new WaitForSeconds(5f);
    }

    // ---------- 5) Kendi kendine kapanan kapı ----------

    private IEnumerator DoorClose(AnomalyData data)
    {
        if (player == null) yield break;

        // Kapılar: Frame_A çocuğu olan "Door_*" grupları. Geçiş ekseni, sınırların ince ekseni.
        var doors = new List<DoorInfo>();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Door_") || t.Find("Frame_A") == null) continue;
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            doors.Add(new DoorInfo { bounds = b, alongZ = b.size.z < b.size.x, lastSide = 0 });
        }
        if (doors.Count == 0)
        {
            Debug.LogWarning("[ANOMALY] Kapı bulunamadı (Door_* + Frame_A).");
            yield break;
        }

        var poll = new WaitForSeconds(0.1f);
        while (true)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                DoorInfo d = doors[i];
                Vector3 rel = player.position - d.bounds.center;
                float along = d.alongZ ? rel.z : rel.x;
                float lateral = d.alongZ ? rel.x : rel.z;
                float halfWidth = (d.alongZ ? d.bounds.extents.x : d.bounds.extents.z) - 0.1f;
                int side = along > 0.35f ? 1 : (along < -0.35f ? -1 : 0);

                if (side != 0)
                {
                    bool passed = d.lastSide != 0 && side != d.lastSide && Mathf.Abs(lateral) < halfWidth + 0.3f;
                    d.lastSide = side;
                    doors[i] = d;
                    if (passed)
                    {
                        yield return CloseDoor(data, d);
                        yield break;
                    }
                }
            }
            yield return poll;
        }
    }

    private IEnumerator CloseDoor(AnomalyData data, DoorInfo door)
    {
        Trigger(data);
        yield return new WaitForSeconds(doorCloseDelay);

        // Oyuncu hâlâ kapı aralığındaysa bekle (içine kapanmasın).
        var poll = new WaitForSeconds(0.1f);
        while (true)
        {
            Vector3 rel = player.position - door.bounds.center;
            float along = door.alongZ ? rel.z : rel.x;
            if (Mathf.Abs(along) > 0.9f) break;
            yield return poll;
        }

        float innerWidth = (door.alongZ ? door.bounds.size.x : door.bounds.size.z) - 0.16f;
        var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = "AnomalyDoor";
        Vector3 center = new Vector3(door.bounds.center.x, 1.15f, door.bounds.center.z);
        leaf.transform.localScale = door.alongZ ? new Vector3(innerWidth, 2.3f, 0.12f) : new Vector3(0.12f, 2.3f, innerWidth);
        var r = leaf.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.29f, 0.19f, 0.12f));
        mat.SetFloat("_Smoothness", 0.2f);
        r.sharedMaterial = mat;
        spawned.Add(leaf);

        // Hızla kayarak kapanır.
        Vector3 slide = door.alongZ ? new Vector3(innerWidth, 0f, 0f) : new Vector3(0f, 0f, innerWidth);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            leaf.transform.position = center + slide * (1f - t / 0.12f);
            yield return null;
        }
        leaf.transform.position = center;
        AnomalySounds.PlayAt(SoundOrDefault(data, AnomalySounds.Lock), center, 1f, 1f, 18f);
        Juice.Shake(0.25f, 0.25f);

        // 10 sn kilitli, sonra tekrar açılır.
        yield return new WaitForSeconds(doorLockDuration);
        AnomalySounds.PlayAt(AnomalySounds.Lock, center, 0.5f, 1.4f, 14f);
        spawned.Remove(leaf);
        Destroy(leaf);
        Destroy(mat);
    }

    // ---------- 6) Fenerin göstermediği şey ----------

    private IEnumerator LightTrick(AnomalyData data)
    {
        yield return new WaitForSeconds(RollDelay(data));

        for (int count = 0; count < 4; count++)
        {
            // Sadece fener kapalıyken.
            var poll = new WaitForSeconds(0.5f);
            while (flashlight != null && flashlight.IsOn)
                yield return poll;

            Trigger(data);
            yield return ShadowPass(data);
            yield return new WaitForSeconds(Random.Range(20f, 40f));
        }
    }

    // Kamera kenarında 0.3 sn'lik hızlı bir gölge geçer, çok hafif çıtırtı çalar.
    private IEnumerator ShadowPass(AnomalyData data)
    {
        if (cam == null) yield break;

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "AnomalyPassingShadow";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = BlackMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        spawned.Add(go);

        float side = Random.value < 0.5f ? -1f : 1f;
        float startX = side > 0f ? 1.08f : -0.08f;
        float endX = side > 0f ? 0.78f : 0.22f;
        const float depth = 3.2f;

        AnomalySounds.PlayAt(SoundOrDefault(data, AnomalySounds.Crackle), cam.transform.position, 0.35f, 1f, 10f, false);

        for (float t = 0f; t < lightTrickDuration; t += Time.deltaTime)
        {
            float x = Mathf.Lerp(startX, endX, t / lightTrickDuration);
            go.transform.position = cam.ViewportToWorldPoint(new Vector3(x, 0.45f, depth));
            yield return null;
        }
        spawned.Remove(go);
        Destroy(go);
    }

    // ---------- Post-process ----------

    // Anomali aktifken vignette koyulaşır, renk azalır, kontrast ve grain artar, uzak bulanıklaşır.
    // volume.profile (çalışma anı kopyası) kullanılır: proje asset'i değişmez.
    private IEnumerator FadePostProcess(bool toAnomaly)
    {
        if (volume == null || volume.profile == null) yield break;
        CapturePostProcess();

        float fromV = vignette != null ? vignette.intensity.value : 0f;
        float fromS = colorAdj != null ? colorAdj.saturation.value : 0f;
        float fromC = colorAdj != null ? colorAdj.contrast.value : 0f;
        float fromG = grain != null ? grain.intensity.value : 0f;

        float toV = toAnomaly ? targetVignette : origVignette;
        float toS = toAnomaly ? origSat + saturationDelta : origSat;
        float toC = toAnomaly ? origContrast + contrastDelta : origContrast;
        float toG = toAnomaly ? targetGrain : origGrain;

        if (toAnomaly)
        {
            if (grain != null) grain.active = true;
            if (dof != null)
            {
                dof.active = true;
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(6f);
                dof.gaussianEnd.Override(22f);
                dof.gaussianMaxRadius.Override(1f);
            }
        }

        for (float t = 0f; t < postFadeTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / postFadeTime);
            if (vignette != null) vignette.intensity.Override(Mathf.Lerp(fromV, toV, k));
            if (colorAdj != null)
            {
                colorAdj.saturation.Override(Mathf.Lerp(fromS, toS, k));
                colorAdj.contrast.Override(Mathf.Lerp(fromC, toC, k));
            }
            if (grain != null) grain.intensity.Override(Mathf.Lerp(fromG, toG, k));
            yield return null;
        }
    }

    // Orijinal değerleri bir kere kaydeder; DepthOfField yoksa profile ekler.
    private void CapturePostProcess()
    {
        if (ppCaptured) return;
        ppCaptured = true;
        VolumeProfile profile = volume.profile;

        if (profile.TryGet(out vignette)) origVignette = vignette.intensity.value;
        if (profile.TryGet(out colorAdj)) { origSat = colorAdj.saturation.value; origContrast = colorAdj.contrast.value; }
        if (profile.TryGet(out grain)) { origGrain = grain.intensity.value; origGrainActive = grain.active; }
        if (profile.TryGet(out dof))
        {
            origDofActive = dof.active;
        }
        else
        {
            dof = profile.Add<DepthOfField>(true);
            dofAdded = true;
        }
    }

    // Anomali bitince her şeyi eski değerine döndürür.
    private void RestorePostProcess()
    {
        if (!ppCaptured) return;
        ppCaptured = false;

        if (vignette != null) vignette.intensity.Override(origVignette);
        if (colorAdj != null)
        {
            colorAdj.saturation.Override(origSat);
            colorAdj.contrast.Override(origContrast);
        }
        if (grain != null)
        {
            grain.intensity.Override(origGrain);
            grain.active = origGrainActive;
        }
        if (dof != null)
        {
            if (dofAdded && volume != null && volume.profile != null)
                volume.profile.Remove<DepthOfField>();
            else
                dof.active = origDofActive;
        }
        dofAdded = false;
        vignette = null; colorAdj = null; grain = null; dof = null;
    }
}
