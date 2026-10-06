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
    private float scanTimer;
    private float nextDebugLogTime;

    // Referansları bulur, dizileri ve sesi bir kere hazırlar (Update'te hiç new yok).
    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;
        camTransform = cam != null ? cam.transform : transform;

        hitBuffer = new Collider[colliderBufferSize];
        targets = new Pest[maxTargets];

        loopSource = GetComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.clip = vacuumLoopSound;
    }

    // Alet değiştirilince çalışma durur (partikül ve ses takılı kalmasın).
    private void OnDisable()
    {
        SetRunning(false);
        ClearTargets();
    }

    // Girişi okur: boşaltma, çalıştırma; çalışırken tarar ve çeker.
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[emptyKey].wasPressedThisFrame)
            Empty();

        var mouse = Mouse.current;
        bool running = mouse != null && mouse.rightButton.isPressed && !IsFull;
        SetRunning(running);

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
        for (int i = 0; i < count && targetCount < targets.Length; i++)
        {
            Collider col = hitBuffer[i];
            Vector3 targetPos = col.bounds.center;

            Vector3 flatTo = targetPos - origin;
            flatTo.y = 0f;
            if (flatTo.sqrMagnitude > range * range) continue;
            if (checkAngle && flatTo.sqrMagnitude > 0.0001f && Vector3.Angle(flatForward, flatTo) > halfAngle) continue;
            if (Physics.Linecast(origin, targetPos, obstacleLayers, QueryTriggerInteraction.Ignore)) continue;
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

            if (dist <= suckDistance)
            {
                Swallow(pest);
                targets[i] = null;
                if (IsFull) return;
                continue;
            }

            float step = Mathf.Min(pullSpeed * dt, dist);
            pest.VacuumPull(toPlayer / dist * step);
        }
    }

    // Böceği yutar: ölür ve havuza döner, hazne bir artar.
    private void Swallow(Pest pest)
    {
        if (IsFull) return;
        pest.Vacuumed();
        StoredCount++;
    }

    // Hazneyi boşaltır (ileride para/ödül buraya eklenebilir).
    public void Empty()
    {
        StoredCount = 0;
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
