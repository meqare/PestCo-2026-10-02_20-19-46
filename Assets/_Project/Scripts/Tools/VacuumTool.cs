using UnityEngine;
using UnityEngine.InputSystem;

// Vakum aleti: sağ tık basılıyken önündeki konideki böcekleri kendine çeker, yeterince yaklaşanı yutar.
// Kapasite dolunca çalışmaz; boşaltma tuşuyla (varsayılan R) boşaltılır.
// Aletin üzerinde durur (ToolHolder seçince açılır).
[RequireComponent(typeof(AudioSource))]
public class VacuumTool : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    [SerializeField] private Camera cam;
    [Tooltip("Sadece çekerken oynayan partikül (Play On Awake kapalı olmalı).")]
    [SerializeField] private ParticleSystem vacuumFX;

    [Header("Çekme")]
    [Tooltip("Yatay menzil (oyuncunun etrafında, yerdeki böcekler için).")]
    [SerializeField, Min(0.1f)] private float range = 4f;
    [Tooltip("Önündeki yatay açı. Yukarı/aşağı bakış sayılmaz: yerdeki böcekler bakış çizgisinin altında kalır.")]
    [SerializeField, Range(1f, 359f)] private float coneAngle = 90f;
    [Tooltip("Kameradan bu kadar aşağıya kadar taranır (zemin dahil).")]
    [SerializeField, Min(0.1f)] private float scanDepth = 2f;
    [Tooltip("Çekme hızı (m/s). Böceklerin kaçış hızından (2.8-3.5) büyük olmalı.")]
    [SerializeField, Min(0f)] private float pullSpeed = 4f;
    [Tooltip("Oyuncuya yatayda bu kadar yaklaşan böcek yutulur.")]
    [SerializeField, Min(0.1f)] private float suckDistance = 1f;
    [Tooltip("BugController böceklerinin çekilme hızı (çıldırınca kendi hızları 5 m/s).")]
    [SerializeField, Min(0f)] private float bugPullSpeed = 7f;
    [Tooltip("Hedef taraması bu aralıkla yapılır (çekme her karede uygulanır).")]
    [SerializeField, Min(0.01f)] private float scanInterval = 0.05f;
    [SerializeField, Min(1)] private int maxTargets = 16;

    [Header("Kapasite")]
    [SerializeField, Min(1)] private int maxCapacity = 5;
    [Tooltip("Haznede biriken böcekleri boşaltır. F el feneri olduğu için varsayılan R.")]
    [SerializeField] private Key emptyKey = Key.R;

    [Header("Layers")]
    [Tooltip("Çekilecek böceklerin layer'ları (Bug + Pest).")]
    [SerializeField] private LayerMask targetLayers;
    [Tooltip("Çekmeyi engelleyen duvar vb. Böcek layer'larını buraya EKLEME.")]
    [SerializeField] private LayerMask obstacleLayers = 1; // Default
    [SerializeField, Min(8)] private int colliderBufferSize = 64;

    [Header("Balon (BalloonPhysicsItem)")]
    [Tooltip("Eşyanın bağlanacağı silah ucu. Boşsa VacuumFX, o da yoksa kamera kullanılır.")]
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0.5f)] private float balloonRange = 6f;
    [SerializeField, Min(0.05f)] private float balloonAimRadius = 0.35f;
    [SerializeField] private LayerMask balloonLayers = ~0;

    [Header("Kule (TowerItem)")]
    [Tooltip("TowerItem'ın kuleye eklendiği mesafe.")]
    [SerializeField, Min(0.1f)] private float towerAttachDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float towerPullSpeed = 6f;
    [Tooltip("Boşsa WobblyTowerManager.Instance Start'ta otomatik bulunur.")]
    [SerializeField] private WobblyTowerManager towerManager;

    [Header("Ses")]
    [Tooltip("Çekerken döngüde çalar. Boşsa ses yok.")]
    [SerializeField] private AudioClip vacuumLoopSound;

    [Header("Debug (GEÇİCİ)")]
    [Tooltip("Açıkken Console'a sağ tık ve hedef sayısı yazar. Test bitince kapat.")]
    [SerializeField] private bool debugLogs = true;

    // Haznedeki böcek sayısı ve sınır (HUD okur).
    public int StoredCount { get; private set; }
    public int MaxCapacity => maxCapacity;
    public bool IsFull => StoredCount >= maxCapacity;
    public bool IsRunning { get; private set; }

    private Transform camTransform;
    private AudioSource loopSource;
    private Collider[] hitBuffer;
    private Pest[] targets;
    private int targetCount;
    private BugController[] bugTargets;
    private int bugTargetCount;
    // Spreyle bayılmış kafes böcekleri: üstteki kapsüle alınır.
    private CageableBug[] cageTargets;
    private int cageTargetCount;
    private VacuumCapsule capsule;
    private float scanTimer;
    private float nextDebugLogTime;

    // Balon: silah ucundaki kinematik çapa ve şu an bağlı eşya.
    private Rigidbody balloonAnchor;
    private BalloonPhysicsItem heldBalloon;
    private CharacterController playerController;
    private RaycastHit[] balloonHits;

    // Kule: şu an kuleye doğru çekilen eşya.
    private TowerItem pulledTowerItem;

    // Referansları bulur, dizileri ve sesi bir kere hazırlar (Update'te hiç new yok).
    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;
        camTransform = cam != null ? cam.transform : transform;

        hitBuffer = new Collider[colliderBufferSize];
        targets = new Pest[maxTargets];
        bugTargets = new BugController[maxTargets];
        cageTargets = new CageableBug[maxTargets];
        capsule = GetComponentInChildren<VacuumCapsule>(true);
        balloonHits = new RaycastHit[16];
        playerController = GetComponentInParent<CharacterController>();

        loopSource = GetComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.clip = vacuumLoopSound;
    }

    private void Start()
    {
        if (towerManager == null)
            towerManager = WobblyTowerManager.Instance != null ? WobblyTowerManager.Instance : FindFirstObjectByType<WobblyTowerManager>();
    }

    // Alet değiştirilince çalışma durur (partikül ve ses takılı kalmasın).
    private void OnDisable()
    {
        SetRunning(false);
        ClearTargets();
        ReleaseBalloon();
        pulledTowerItem = null;
    }

    // Girişi okur: boşaltma, çalıştırma; çalışırken tarar ve çeker.
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && !PestCo.Truck.PestPC.IsOpen && keyboard[emptyKey].wasPressedThisFrame)
            Empty();

        var mouse = Mouse.current;
        bool running = mouse != null && mouse.rightButton.isPressed && !IsFull;
        SetRunning(running);
        UpdateBalloon(running);
        UpdateTowerPull(running);

        // GEÇİCİ: canlı test logları.
        if (debugLogs && mouse != null && mouse.rightButton.wasPressedThisFrame)
            Debug.Log(IsFull ? "[Vakum] Sag tik algilandi ama hazne DOLU (R ile bosalt)" : "[Vakum] Sag tik algilandi", this);
        if (debugLogs && running && Time.unscaledTime >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.unscaledTime + 0.5f;
            Debug.Log($"[Vakum] Aktif, {targetCount} hedef menzilde, hazne {StoredCount}/{maxCapacity}", this);
        }

        if (!running)
        {
            ClearTargets();
            // Bir sonraki basışta ilk tarama hemen olsun.
            scanTimer = scanInterval;
            return;
        }

        scanTimer += Time.deltaTime;
        if (scanTimer >= scanInterval)
        {
            scanTimer = 0f;
            ScanTargets();
        }

        PullTargets(Time.deltaTime);
    }

    // Vakum çalışırken nişandaki BalloonPhysicsItem'ı silah ucuna bağlar; bırakılınca / eşya yok olunca serbest bırakır.
    private void UpdateBalloon(bool running)
    {
        if (!running)
        {
            ReleaseBalloon();
            return;
        }
        if (heldBalloon != null || !IsAimingAtBalloon(out BalloonPhysicsItem item)) return;

        EnsureBalloonAnchor();
        if (balloonAnchor != null && item.TryAttach(balloonAnchor, playerController))
            heldBalloon = item;
    }

    // Nişandaki TowerItem'ı kule tabanına çeker; yeterince yaklaşınca kuleye ekler. Bırakınca fizik serbest kalır.
    private void UpdateTowerPull(bool running)
    {
        if (!running)
        {
            pulledTowerItem = null;
            return;
        }

        if (towerManager == null) towerManager = WobblyTowerManager.Instance;
        if (towerManager == null || towerManager.TowerBasePoint == null) return;

        if (pulledTowerItem != null && pulledTowerItem.IsStacked) pulledTowerItem = null;
        if (pulledTowerItem == null && !IsAimingAtTowerItem(out pulledTowerItem)) return;

        Vector3 target = towerManager.TowerBasePoint.position;
        Vector3 toTarget = target - pulledTowerItem.transform.position;
        float dist = toTarget.magnitude;

        if (dist <= towerAttachDistance)
        {
            TowerItem item = pulledTowerItem;
            pulledTowerItem = null;
            towerManager.AddItemToTower(item);
            Debug.Log($"[KULE] '{item.name}' başarıyla istiflendi! Toplam Eşya: {towerManager.StackCount}", item);
            return;
        }

        // Menzil dışına çıkan eşya bırakılır.
        if ((pulledTowerItem.transform.position - camTransform.position).sqrMagnitude > balloonRange * balloonRange * 2.25f)
        {
            pulledTowerItem = null;
            return;
        }

        if (pulledTowerItem.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
            rb.linearVelocity = toTarget / dist * Mathf.Min(towerPullSpeed, dist / Mathf.Max(Time.deltaTime, 0.0001f));
    }

    // Kameradan ileri küre taraması: en yakın engel stack'lenmemiş bir TowerItem ise hedeftir.
    private bool IsAimingAtTowerItem(out TowerItem item)
    {
        item = null;
        int count = Physics.SphereCastNonAlloc(camTransform.position, balloonAimRadius, camTransform.forward,
            balloonHits, balloonRange, balloonLayers, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = balloonHits[i];
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
            if (playerController != null && hit.collider.transform.IsChildOf(playerController.transform)) continue;
            if (hit.distance >= nearest) continue;

            nearest = hit.distance;
            item = hit.collider.GetComponentInParent<TowerItem>();
        }

        if (item != null && item.IsStacked) item = null;
        return item != null;
    }

    private void ReleaseBalloon()
    {
        if (heldBalloon != null) heldBalloon.Release();
        heldBalloon = null;
    }

    // Kameradan ileri küre taraması: en yakın engel bir balon eşyasıysa o hedeftir (duvar arkasındakini almaz).
    private bool IsAimingAtBalloon(out BalloonPhysicsItem item)
    {
        item = null;
        int count = Physics.SphereCastNonAlloc(camTransform.position, balloonAimRadius, camTransform.forward,
            balloonHits, balloonRange, balloonLayers, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = balloonHits[i];
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // başlangıçta içinde kalınan collider
            if (playerController != null && hit.collider.transform.IsChildOf(playerController.transform)) continue;
            if (hit.distance >= nearest) continue;

            nearest = hit.distance;
            item = hit.collider.GetComponentInParent<BalloonPhysicsItem>();
        }
        return item != null;
    }

    // Silah ucuna yapışık kinematik Rigidbody: ConfigurableJoint buna bağlanır.
    private void EnsureBalloonAnchor()
    {
        if (balloonAnchor != null) return;

        Transform parent = firePoint != null ? firePoint : (vacuumFX != null ? vacuumFX.transform : camTransform);
        var go = new GameObject("BalloonAnchor", typeof(Rigidbody));
        go.transform.SetParent(parent, false);
        balloonAnchor = go.GetComponent<Rigidbody>();
        balloonAnchor.isKinematic = true;
        balloonAnchor.useGravity = false;
    }

    // Kapsül (kameradan zemine) -> yatay açı -> engel sırasıyla eler; geçen canlı böcekleri hedef listesine yazar.
    // Açı yatayda ölçülür: kamera 1.6 m yukarıda, böcek yerde; 3D açıyla yaklaşan böcek bakışın altına düşüp kaçardı.
    private void ScanTargets()
    {
        ClearTargets();

        Vector3 origin = camTransform.position;
        Vector3 flatForward = camTransform.forward;
        flatForward.y = 0f;
        bool checkAngle = flatForward.sqrMagnitude > 0.0001f; // tam aşağı bakıyorsa her yön geçerli
        float halfAngle = coneAngle * 0.5f;

        int count = Physics.OverlapCapsuleNonAlloc(origin, origin + Vector3.down * scanDepth, range,
            hitBuffer, targetLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count && (targetCount < targets.Length || bugTargetCount < bugTargets.Length); i++)
        {
            Collider col = hitBuffer[i];
            Vector3 targetPos = col.bounds.center;

            Vector3 flatTo = targetPos - origin;
            flatTo.y = 0f;
            if (flatTo.sqrMagnitude > range * range) continue;
            if (checkAngle && flatTo.sqrMagnitude > 0.0001f && Vector3.Angle(flatForward, flatTo) > halfAngle) continue;
            if (Physics.Linecast(origin, targetPos, obstacleLayers, QueryTriggerInteraction.Ignore)) continue;

            // Kafes böceği sadece baygınken çekilir (uyanıkken önce spreyle bayıltılmalı).
            if (col.TryGetComponent(out CageableBug cageBug))
            {
                if (capsule != null && cageBug.IsStunned && cageTargetCount < cageTargets.Length)
                    cageTargets[cageTargetCount++] = cageBug;
                continue;
            }

            if (col.TryGetComponent(out BugController bug))
            {
                if (bugTargetCount < bugTargets.Length) bugTargets[bugTargetCount++] = bug;
                continue;
            }

            if (targetCount >= targets.Length) continue;
            if (!col.TryGetComponent(out Pest pest) || !pest.IsAlive) continue;

            targets[targetCount++] = pest;
        }
    }

    // Hedefleri oyuncuya doğru çeker; yeterince yaklaşanı yutar.
    private void PullTargets(float dt)
    {
        Vector3 origin = camTransform.position;

        for (int i = 0; i < targetCount; i++)
        {
            Pest pest = targets[i];
            if (pest == null || !pest.IsAlive) continue;

            // Böcekler yerde, kamera yukarıda: sadece yatay mesafe ölçülür.
            Vector3 toPlayer = origin - pest.transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Vakum tuttuğu sürece böceğin canı azalır; sağlamken çekim zayıf, yutulamaz.
            pest.DrainByVacuum(dt);
            bool weak = pest.IsWeakEnoughToSwallow;

            if (dist <= suckDistance && weak)
            {
                Swallow(pest);
                targets[i] = null;
                if (IsFull) return;
                continue;
            }

            // Henüz zayıflamadıysa yutma mesafesinde tutulur (debelenir); zayıflayınca tam çekilir.
            float holdDistance = weak ? 0f : suckDistance;
            float step = Mathf.Min(pullSpeed * pest.VacuumPullMultiplier * dt, Mathf.Max(0f, dist - holdDistance));
            pest.VacuumPull(dist > 0.0001f ? toPlayer / dist * step : Vector3.zero);
        }

        PullBugs(dt);
        PullCageBugs(dt);
    }

    // Baygın kafes böceklerini çeker; yaklaşanı vakumun üstündeki kapsüle alır (kapsül doluysa yakında bekletir).
    private void PullCageBugs(float dt)
    {
        Vector3 origin = camTransform.position;

        for (int i = 0; i < cageTargetCount; i++)
        {
            CageableBug bug = cageTargets[i];
            if (bug == null || !bug.gameObject.activeInHierarchy || !bug.IsStunned) continue;

            Vector3 toPlayer = origin - bug.transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist <= suckDistance && capsule.TryStore(bug))
            {
                cageTargets[i] = null;
                continue;
            }

            float hold = capsule.IsFull ? suckDistance : 0f;
            float step = Mathf.Min(bugPullSpeed * dt, Mathf.Max(0f, dist - hold));
            bug.VacuumPull(dist > 0.0001f ? toPlayer / dist * step : Vector3.zero);
        }
    }

    // BugController böceklerini çeker: ilk temasta çıldırtır, yaklaşanı yutar.
    private void PullBugs(float dt)
    {
        Vector3 origin = camTransform.position;

        for (int i = 0; i < bugTargetCount; i++)
        {
            BugController bug = bugTargets[i];
            if (bug == null || !bug.gameObject.activeInHierarchy) continue;

            if (bug.currentState != BugState.Horror)
                bug.TriggerHorrorMode();

            // Pest'lerdeki gibi sadece yatay mesafe; böcek yüksekliğinde kalır.
            Vector3 toPlayer = origin - bug.transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist <= suckDistance)
            {
                SwallowBug(bug);
                bugTargets[i] = null;
                if (IsFull) return;
                continue;
            }

            float step = Mathf.Min(bugPullSpeed * dt, dist);
            bug.transform.position += toPlayer / dist * step;
        }
    }

    // BugController böceğini yutar: hazne artar, sayaç güncellenir, böcek kapanır.
    private void SwallowBug(BugController bug)
    {
        if (IsFull) return;
        StoredCount++;
        if (UIManager.Instance != null) UIManager.Instance.AddBugToVakum(bug.bugData, bug.currentState);
        if (BugInventory.Instance != null) BugInventory.Instance.AddBugToInventory(bug.bugData, bug.currentState);
        bug.gameObject.SetActive(false);
    }

    // Böceği yutar: ölür ve havuza döner, hazne bir artar.
    private void Swallow(Pest pest)
    {
        if (IsFull) return;

        // Türü biliniyorsa envantere (kamyonet satışı, ekrandaki liste) ve vakum yazısına eklenir.
        BugData data = pest.BugData;
        pest.Vacuumed();
        StoredCount++;
        if (data == null) return;

        if (UIManager.Instance != null) UIManager.Instance.AddBugToVakum(data, BugState.Cute);
        if (BugInventory.Instance != null) BugInventory.Instance.AddBugToInventory(data, BugState.Cute);
    }

    // Haznedeki bir böcek dışarı çıktı (kamyonda kafesten kaçtı): sayaç bir azalır.
    public void ReleaseOne(BugData data)
    {
        if (StoredCount > 0) StoredCount--;
        if (UIManager.Instance != null) UIManager.Instance.RemoveBugFromVakum(data);
    }

    // Hazneyi boşaltır (ileride para/ödül buraya eklenebilir).
    public void Empty()
    {
        StoredCount = 0;
        if (UIManager.Instance != null) UIManager.Instance.TakeAllBugs();
    }

    // Partikülü ve döngü sesini çalışma durumuna göre açar/kapatır (sadece değişince).
    private void SetRunning(bool running)
    {
        if (running == IsRunning) return;
        IsRunning = running;

        if (vacuumFX != null)
        {
            if (running) vacuumFX.Play(true);
            else vacuumFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (loopSource != null && loopSource.clip != null)
        {
            if (running) loopSource.Play();
            else loopSource.Stop();
        }
    }

    // Hedef listesini temizler (ölü/havuza dönmüş böceklere referans kalmasın).
    private void ClearTargets()
    {
        for (int i = 0; i < targetCount; i++)
            targets[i] = null;
        targetCount = 0;

        for (int i = 0; i < bugTargetCount; i++)
            bugTargets[i] = null;
        bugTargetCount = 0;

        for (int i = 0; i < cageTargetCount; i++)
            cageTargets[i] = null;
        cageTargetCount = 0;
    }

    // Seçiliyken Scene penceresinde menzili ve yutma mesafesini çizer.
    private void OnDrawGizmosSelected()
    {
        Transform t = cam != null ? cam.transform : (Camera.main != null ? Camera.main.transform : transform);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(t.position, range);
        Gizmos.DrawLine(t.position, t.position + t.forward * range);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(new Vector3(t.position.x, t.position.y - 1.6f, t.position.z), suckDistance);
    }
}
