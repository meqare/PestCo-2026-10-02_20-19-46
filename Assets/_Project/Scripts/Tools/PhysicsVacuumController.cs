using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Yeni fizik vakum: sol tık basılı = nişandaki Vacuumable nesneyi kameranın önündeki tutma noktasına çeker ve tutar,
// sol tık bırak = serbest bırak (normal fizik), tutarken sağ tık = kamera yönünde fırlat.
// Nesne ASLA Transform ile taşınmaz: yalnızca Rigidbody ivmesi (hız hedefli yay-sönümleme) kullanılır.
// Girdi Update'te okunur, kuvvet FixedUpdate'te uygulanır.
public class PhysicsVacuumController : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boşsa Camera.main kullanılır.")]
    [SerializeField] private Camera cam;
    [Tooltip("Işının çıktığı silah ucu (ör. VacuumFX). Boşsa kameranın sağ-alt önü.")]
    [SerializeField] private Transform muzzle;
    [Tooltip("Aynı tuşları dinleyen eski vakum bileşenleri (ör. VacuumGun). Oyun başında otomatik kapatılır.")]
    [SerializeField] private Behaviour[] legacySystemsToDisable;
    [Tooltip("Nesne tutulurken geçici kapatılacak bileşenler (ör. böcek vakumu VacuumTool: sağ tık çakışmasın).")]
    [SerializeField] private Behaviour[] suppressWhileHolding;

    [Header("Hedefleme")]
    [Min(0.5f)] [SerializeField] private float maxPullDistance = 8f;
    [Tooltip("Yardımcı nişan küresinin yarıçapı: küçük nesneyi tam ortalamadan da seçer.")]
    [Min(0.01f)] [SerializeField] private float aimRadius = 0.12f;
    [Tooltip("Işın bir yüzeye (tezgah) çarparken, bu mesafe içindeki çekilebilir nesne yine de seçilir.")]
    [Min(0f)] [SerializeField] private float aimAssistSlack = 0.5f;
    [Tooltip("Nişan ve duvar kontrolünün göreceği katmanlar. Duvar/dolap bu maskede kalmalı ki hedefi engellesin.")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("Bu kütleden ağır nesneler hedef alınmaz.")]
    [Min(0.1f)] [SerializeField] private float maxMass = 30f;
    [Tooltip("E ile tutma sistemindeki (GrabbableObject) nesneler hedef alınmaz: iki sistem aynı nesneyi sürmesin.")]
    [SerializeField] private bool ignoreGrabbables = true;

    [Header("Çekim ve Tutma")]
    [Tooltip("Tutma noktasının kameraya uzaklığı (başlangıç). Fare tekerleğiyle tutarken değişir.")]
    [Min(0.3f)] [SerializeField] private float holdDistance = 1.8f;
    [Tooltip("Tekerlekle yakınlaştırma: en yakın mesafe.")]
    [Min(0.3f)] [SerializeField] private float holdDistanceMin = 0.8f;
    [Tooltip("Tekerlekle uzaklaştırma: en uzak mesafe.")]
    [Min(0.3f)] [SerializeField] private float holdDistanceMax = 3.5f;
    [Tooltip("Tekerlek bir çentik başına mesafe değişimi (m).")]
    [Min(0.01f)] [SerializeField] private float scrollStep = 0.2f;
    [Tooltip("Duvar yaklaşınca tutma noktası bu mesafenin altına inmez.")]
    [Min(0.2f)] [SerializeField] private float minHoldDistance = 0.7f;
    [SerializeField] private float holdHeightOffset = -0.05f;
    [Min(0.01f)] [SerializeField] private float holdProbeRadius = 0.15f;
    [Tooltip("Oyuncu dönünce tutma noktasının bakışı ne kadar hızlı izlediği (düşük = daha yumuşak/gecikmeli).")]
    [Min(0.1f)] [SerializeField] private float holdFollowSharpness = 14f;
    [Tooltip("Nesnenin en yüksek hızı (m/s, hafif nesne için).")]
    [Min(0.5f)] [SerializeField] private float maxSpeed = 10f;
    [Tooltip("Hedefe uzaklık × bu değer = istenen hız. Küçük = yaklaşırken daha yumuşak yavaşlar.")]
    [Min(0.1f)] [SerializeField] private float speedGain = 5f;
    [Tooltip("İstenen hıza ulaşma sertliği (1/sn).")]
    [Min(0.1f)] [SerializeField] private float responsiveness = 14f;
    [Min(1f)] [SerializeField] private float maxAcceleration = 80f;
    [Tooltip("Tutarken dönüş sürtünmesi: nesne savrulup dönmesin.")]
    [Min(0f)] [SerializeField] private float heldAngularDamping = 6f;
    [Tooltip("Nesne tutma noktasından bu kadar uzaklaşırsa bırakılır.")]
    [Min(1f)] [SerializeField] private float breakDistance = 12f;
    [Tooltip("Kamerayla nesne arasına duvar girerse bu süre sonra bırakılır.")]
    [Min(0.05f)] [SerializeField] private float lostSightTime = 0.4f;

    [Header("Kütle Direnci")]
    [Tooltip("Bu kütleye kadar çekim direnci yok.")]
    [Min(0f)] [SerializeField] private float lightMass = 1f;
    [Tooltip("Hafif eşiğin üstündeki her kg için direnç artışı.")]
    [Min(0f)] [SerializeField] private float massResistance = 0.12f;
    [Range(0.02f, 1f)] [SerializeField] private float minMassFactor = 0.15f;

    [Header("Fırlatma")]
    [Tooltip("Hafif nesne için fırlatma hızı (m/s). Ağır nesne daha yavaş uçar.")]
    [Min(0f)] [SerializeField] private float throwSpeed = 14f;

    [Header("Görsel Geri Bildirim")]
    [Tooltip("Boşsa Sprites/Default denenir. Önerilen: M_VacuumBeam.")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private Gradient beamColor = new Gradient();
    [Min(0.001f)] [SerializeField] private float beamWidth = 0.06f;
    [Range(4, 40)] [SerializeField] private int beamPoints = 16;
    [Min(0f)] [SerializeField] private float beamWaveAmplitude = 0.1f;
    [Min(0f)] [SerializeField] private float beamWaveFrequency = 3f;
    [Min(0f)] [SerializeField] private float beamWaveSpeed = 8f;
    [Tooltip("Nişandaki çekilebilir nesnenin etrafındaki halka rengi.")]
    [SerializeField] private Color reticleColor = new Color(0.6f, 0.95f, 1f, 0.9f);
    [Min(0.001f)] [SerializeField] private float reticleWidth = 0.012f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    public Vacuumable Held => held;
    public bool IsHolding => held != null;
    // Sahnedeki herhangi bir vakum tutuyor mu (tekerlek slot seçimi gibi diğer sistemler bunu okur).
    public static bool IsAnyHolding { get; private set; }
    public Vacuumable AimTarget => aimTarget;

    // Gelecek özellik sistemleri (Heat/Sound aktarımı vb.) buraya bağlanabilir.
    public event Action<Vacuumable> Grabbed;
    public event Action<Vacuumable> Released;
    public event Action<Vacuumable> Thrown;

    private const int ReticleSegments = 28;

    private Vacuumable held;
    private Vacuumable aimTarget;
    private Transform camT;
    private Transform playerRoot;
    private Collider[] playerColliders;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly RaycastHit[] sightHits = new RaycastHit[16]; // IsSightBlocked, hedefleme döngüsü içinden de çağrılır
    private Vector3 smoothedDir;
    private Vector3 lastHoldPoint;
    private float lostTimer;
    private bool needRepress;
    private bool suppressActive;
    private bool restorePending;
    private bool[] suppressedWasEnabled;
    private LineRenderer beam;
    private LineRenderer reticle;
    private Vector3[] beamPositions;
    private Vector3[] reticlePositions;

    private void Reset()
    {
        beamColor = new Gradient();
        beamColor.SetKeys(
            new[] { new GradientColorKey(new Color(0.55f, 0.85f, 1f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 1f) });
    }

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("PhysicsVacuumController: Kamera bulunamadı. Inspector'da 'Cam' alanına Main Camera'yı ata.", this);
            enabled = false;
            return;
        }
        camT = cam.transform;

        var cc = GetComponentInParent<CharacterController>();
        playerRoot = cc != null ? cc.transform : transform.root;
        playerColliders = playerRoot.GetComponentsInChildren<Collider>(true);

        suppressedWasEnabled = new bool[suppressWhileHolding != null ? suppressWhileHolding.Length : 0];
        beamPositions = new Vector3[beamPoints];
        reticlePositions = new Vector3[ReticleSegments];
        BuildVisuals();
    }

    private void OnEnable()
    {
        // Aynı tuşları dinleyen eski vakum aynı anda çalışmasın.
        if (legacySystemsToDisable == null) return;
        for (int i = 0; i < legacySystemsToDisable.Length; i++)
        {
            var b = legacySystemsToDisable[i];
            if (b == null || !b.enabled) continue;
            b.enabled = false;
            Debug.LogWarning($"PhysicsVacuumController: eski sistem '{b.GetType().Name}' kapatıldı (aynı fare tuşlarını dinliyordu).", this);
        }
    }

    private void OnDisable()
    {
        ReleaseHeld(false);
        IsAnyHolding = false;
        aimTarget = null;
        RestoreSuppressed();
        if (beam != null) beam.enabled = false;
        if (reticle != null) reticle.enabled = false;
    }

    // ---------- Girdi (Update) ----------

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        bool leftDown = locked && mouse.leftButton.isPressed;
        if (!mouse.leftButton.isPressed) needRepress = false;

        if (held != null)
        {
            if (!HeldStillValid()) ReleaseHeld(true);
            else if (!leftDown) ReleaseHeld(true);
            else
            {
                // Tekerlek: tutulan nesneyi yakınlaştır (yukarı) / uzaklaştır (aşağı).
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float notches = Mathf.Sign(scroll) * Mathf.Max(1f, Mathf.Round(Mathf.Abs(scroll) / 120f));
                    holdDistance = Mathf.Clamp(holdDistance + notches * scrollStep, holdDistanceMin, holdDistanceMax);
                }
                if (mouse.rightButton.wasPressedThisFrame) ThrowHeld();
            }
        }

        aimTarget = null;
        if (held == null && locked)
        {
            aimTarget = FindTarget();
            // Fırlatmadan sonra sol tık bırakılıp yeniden basılana dek tekrar yakalama yok.
            if (aimTarget != null && leftDown && !needRepress) Grab(aimTarget);
        }

        if (restorePending && !mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
            RestoreSuppressed();
    }

    // ---------- Fizik (FixedUpdate) ----------

    private void FixedUpdate()
    {
        if (held == null) return;
        if (!HeldStillValid()) { ReleaseHeld(true); return; }

        float dt = Time.fixedDeltaTime;
        Rigidbody rb = held.Body;

        // Oyuncu dönünce tutma noktası bakışı gecikmeli (yumuşak) izler.
        smoothedDir = Vector3.Slerp(smoothedDir, camT.forward, 1f - Mathf.Exp(-holdFollowSharpness * dt));
        Vector3 hold = ComputeHoldPoint();
        Vector3 holdVelocity = Vector3.ClampMagnitude((hold - lastHoldPoint) / dt, maxSpeed * 2f);
        lastHoldPoint = hold;

        Vector3 pos = rb.worldCenterOfMass;
        Vector3 delta = hold - pos;
        float dist = delta.magnitude;

        if (dist > breakDistance) { ReleaseHeld(true); return; }

        // Duvar nesneyle kamera arasına girdiyse (ışınlanmadan) bırak.
        if (IsSightBlocked(pos, rb)) lostTimer += dt; else lostTimer = 0f;
        if (lostTimer > lostSightTime) { ReleaseHeld(true); return; }

        float massFactor = MassFactor(held);
        float speedCap = maxSpeed * Mathf.Sqrt(massFactor);
        Vector3 desired = dist > 0.0001f ? delta / dist * Mathf.Min(speedCap, dist * speedGain) : Vector3.zero;
        desired += holdVelocity; // oyuncu yürürken/dönerken nesne gerimde kalmasın

        Vector3 accel = (desired - rb.linearVelocity) * responsiveness;
        accel = Vector3.ClampMagnitude(accel, maxAcceleration * massFactor);
        rb.AddForce(accel, ForceMode.Acceleration);
    }

    // ---------- Tutma / bırakma / fırlatma ----------

    private void Grab(Vacuumable v)
    {
        held = v;
        IsAnyHolding = true;
        lostTimer = 0f;
        smoothedDir = camT.forward;
        v.BeginHold(heldAngularDamping, playerColliders);
        lastHoldPoint = ComputeHoldPoint();
        SuppressOthers();
        if (debugLogs) Debug.Log($"[PhysicsVacuum] Tutuldu: {v.name} ({v.Body.mass:0.##} kg)", this);
        Grabbed?.Invoke(v);
    }

    private void ReleaseHeld(bool notify)
    {
        var v = held;
        held = null;
        IsAnyHolding = false;
        lostTimer = 0f;
        if (v != null)
        {
            v.EndHold();
            if (notify)
            {
                if (debugLogs) Debug.Log($"[PhysicsVacuum] Bırakıldı: {v.name}", this);
                Released?.Invoke(v);
            }
        }
        restorePending = suppressActive;
    }

    private void ThrowHeld()
    {
        var v = held;
        Rigidbody rb = v.Body;
        float factor = Mathf.Sqrt(MassFactor(v));
        Vector3 dir = camT.forward;

        ReleaseHeld(true);
        needRepress = true;
        if (rb != null)
        {
            rb.linearVelocity = dir * (throwSpeed * factor);
            rb.WakeUp();
        }
        if (debugLogs) Debug.Log($"[PhysicsVacuum] Fırlatıldı: {v.name}", this);
        Thrown?.Invoke(v);
    }

    private bool HeldStillValid()
    {
        // Yok edilmiş (Unity null), devre dışı, Rigidbody'si kaybolmuş veya kinematic olmuşsa tutma biter.
        return held != null && held.Body != null && held.CanBeVacuumed && held.IsHeld;
    }

    // ---------- Hedefleme ----------

    // Önce kamera merkezinden ince ışın: tam nişandaki en yakın katı şey karar verir (duvar arkasındaki nesne seçilmez).
    // Işın nesneyi ıskalarsa küre taraması yardımcı olur: yüzeyde yatan ince nesne (tabak) kolay seçilsin,
    // ama nesne ile kamera arasına katı bir şey girmişse seçilmez.
    private Vacuumable FindTarget()
    {
        Vector3 origin = camT.position;
        Vector3 dir = camT.forward;

        // 1) İnce ışın
        int n = Physics.RaycastNonAlloc(origin, dir, hits, maxPullDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        Collider nearestCol = null;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c == null || c.transform.IsChildOf(playerRoot)) continue; // oyuncu ve silahı seçilmez
            if (hits[i].distance < nearest) { nearest = hits[i].distance; nearestCol = c; }
        }
        if (nearestCol != null && TryGetTargetable(nearestCol, out Vacuumable direct)) return direct;

        // 2) Küre yardımı
        n = Physics.SphereCastNonAlloc(origin, aimRadius, dir, hits, maxPullDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        Vacuumable assist = null;
        float assistDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            var c = h.collider;
            if (c == null || h.distance <= 0f || h.distance >= assistDist) continue; // başlangıçta içinde kalınan collider
            if (c.transform.IsChildOf(playerRoot)) continue;
            if (nearestCol != null && h.distance > nearest + aimAssistSlack) continue;
            if (!TryGetTargetable(c, out Vacuumable v)) continue;
            if (IsSightBlocked(v.Body.worldCenterOfMass, v.Body)) continue;
            assist = v;
            assistDist = h.distance;
        }
        return assist;
    }

    private bool TryGetTargetable(Collider c, out Vacuumable v)
    {
        v = null;
        Rigidbody rb = c.attachedRigidbody;
        if (rb == null || !rb.TryGetComponent(out v)) return false;
        return IsTargetable(v);
    }

    private bool IsTargetable(Vacuumable v)
    {
        if (!v.CanBeVacuumed || v.IsHeld) return false;
        if (v.Body.mass > maxMass) return false;
        if (ignoreGrabbables && v.TryGetComponent(out GrabbableObject _)) return false;
        return true;
    }

    // Kameradan bakış yönünde holdDistance; duvar varsa önünde durur (nokta duvarın arkasına geçmez).
    private Vector3 ComputeHoldPoint()
    {
        Vector3 origin = camT.position;
        float dist = holdDistance;
        Bounds b = held.GetWorldBounds();
        Vector3 e = b.extents;
        float heldRadius = Mathf.Max(e.x, Mathf.Max(e.y, e.z));

        int n = Physics.SphereCastNonAlloc(origin, holdProbeRadius, smoothedDir, hits, holdDistance,
            obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.collider == null || h.distance <= 0f) continue;
            if (IsNonBlocking(h.collider, held.Body)) continue;
            dist = Mathf.Min(dist, Mathf.Max(minHoldDistance, h.distance - heldRadius));
        }
        return origin + smoothedDir * dist + Vector3.up * holdHeightOffset;
    }

    private bool IsSightBlocked(Vector3 target, Rigidbody ignoreBody)
    {
        Vector3 origin = camT.position;
        Vector3 to = target - origin;
        float d = to.magnitude;
        if (d < 0.05f) return false;
        int n = Physics.RaycastNonAlloc(origin, to / d, sightHits, d, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = sightHits[i].collider;
            if (c == null || IsNonBlocking(c, ignoreBody)) continue;
            return true;
        }
        return false;
    }

    // Engel sayılmayanlar: tutulan nesnenin kendisi, oyuncu ve diğer serbest fizik nesneleri (fizikle itilir).
    // Yalnızca tutulan nesne ve oyuncu engel değildir. Diğer fizikli nesneler (kase, şişe) da engel sayılır:
    // tutma noktası onların içine girmez, nesne onların arkasından geçmez.
    private bool IsNonBlocking(Collider c, Rigidbody ignoreBody)
    {
        if (c.transform.IsChildOf(playerRoot)) return true;
        Rigidbody rb = c.attachedRigidbody;
        return rb != null && rb == ignoreBody;
    }

    private float MassFactor(Vacuumable v)
    {
        float extra = Mathf.Max(0f, v.Body.mass - lightMass);
        float f = 1f / (1f + extra * massResistance * v.PullResistance);
        return Mathf.Max(minMassFactor, f);
    }

    // ---------- Diğer sistemleri geçici sustur ----------

    private void SuppressOthers()
    {
        restorePending = false;
        if (suppressActive || suppressWhileHolding == null) return;
        suppressActive = true;
        for (int i = 0; i < suppressWhileHolding.Length; i++)
        {
            var b = suppressWhileHolding[i];
            suppressedWasEnabled[i] = b != null && b.enabled;
            if (suppressedWasEnabled[i]) b.enabled = false;
        }
    }

    private void RestoreSuppressed()
    {
        restorePending = false;
        if (!suppressActive) return;
        suppressActive = false;
        for (int i = 0; i < suppressWhileHolding.Length; i++)
        {
            var b = suppressWhileHolding[i];
            if (b != null && suppressedWasEnabled[i]) b.enabled = true;
            suppressedWasEnabled[i] = false;
        }
    }

    // ---------- Görsel geri bildirim ----------

    private void BuildVisuals()
    {
        Material mat = beamMaterial;
        if (mat == null)
        {
            Shader fallback = Shader.Find("Sprites/Default");
            if (fallback != null) mat = new Material(fallback);
            else Debug.LogWarning("PhysicsVacuumController: Beam Material atanmamış; ışın görünmeyebilir.", this);
        }

        beam = CreateLine("VacuumSuctionBeam", mat, beamPoints, false);
        beam.widthMultiplier = beamWidth;
        beam.colorGradient = beamColor;

        reticle = CreateLine("VacuumTargetRing", mat, ReticleSegments, true);
        reticle.widthMultiplier = reticleWidth;
        reticle.startColor = reticleColor;
        reticle.endColor = reticleColor;
    }

    private LineRenderer CreateLine(string objName, Material mat, int points, bool loop)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.positionCount = points;
        lr.sharedMaterial = mat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.numCapVertices = 4;
        lr.enabled = false;
        return lr;
    }

    private void LateUpdate()
    {
        if (camT == null) return;
        UpdateBeam();
        UpdateReticle();
    }

    // Işın yalnızca nesne tutulurken görünür; bırakınca/fırlatınca kapanır.
    private void UpdateBeam()
    {
        if (held == null || held.Body == null)
        {
            if (beam.enabled) beam.enabled = false;
            return;
        }
        beam.enabled = true;

        Vector3 start = muzzle != null ? muzzle.position : camT.position + camT.forward * 0.4f + camT.right * 0.15f - camT.up * 0.2f;
        Vector3 end = held.Body.worldCenterOfMass;
        Vector3 axis = end - start;
        Vector3 side = Vector3.Cross(axis, camT.up);
        side = side.sqrMagnitude > 0.0001f ? side.normalized : camT.right;
        Vector3 up = Vector3.Cross(side, axis).normalized;

        int n = beamPositions.Length;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float phase = t * beamWaveFrequency * Mathf.PI * 2f - Time.time * beamWaveSpeed;
            Vector3 offset = (side * Mathf.Sin(phase) + up * Mathf.Cos(phase)) * (beamWaveAmplitude * envelope);
            beamPositions[i] = Vector3.Lerp(start, end, t) + offset;
        }
        beam.SetPositions(beamPositions);
    }

    // Nişandaki çekilebilir nesnenin etrafında kameraya dönük halka.
    private void UpdateReticle()
    {
        if (aimTarget == null || held != null || aimTarget.Body == null)
        {
            if (reticle.enabled) reticle.enabled = false;
            return;
        }
        reticle.enabled = true;

        Bounds b = aimTarget.GetWorldBounds();
        Vector3 e = b.extents;
        float radius = Mathf.Max(e.x, Mathf.Max(e.y, e.z)) * 1.15f + 0.04f;
        Vector3 center = b.center;
        Vector3 r = camT.right;
        Vector3 u = camT.up;
        for (int i = 0; i < ReticleSegments; i++)
        {
            float a = i / (float)ReticleSegments * Mathf.PI * 2f;
            reticlePositions[i] = center + (r * Mathf.Cos(a) + u * Mathf.Sin(a)) * radius;
        }
        reticle.SetPositions(reticlePositions);
    }

    private void OnDrawGizmosSelected()
    {
        Transform t = cam != null ? cam.transform : (Camera.main != null ? Camera.main.transform : null);
        if (t == null) return;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        Gizmos.DrawLine(t.position, t.position + t.forward * maxPullDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(t.position + t.forward * holdDistance, 0.12f);
    }
}
