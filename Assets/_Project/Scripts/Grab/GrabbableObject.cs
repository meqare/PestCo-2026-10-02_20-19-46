using UnityEngine;

// Repo tarzı tutulabilir fizik eşyası (Grabbable layer). Tutulurken GrabController'ın tutma noktasına
// yay + sönümleme kuvvetiyle (AddForce) çekilir, rotasyonu torque ile hizalanır. Transform hiç doğrudan
// taşınmaz, kinematic/parent yok: duvara çarpınca durur, kayar, itilir.
[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : MonoBehaviour, IInteractable
{
    [SerializeField] private string displayName = "Eşya";
    [Tooltip("Boşsa GrabController ilk tutuşta kendi ayarını atar.")]
    [SerializeField] private GrabSettings settings;
    [Tooltip("Sahne dışına düşünce döneceği nokta. Boşsa sahnedeki başlangıç pozisyonu.")]
    [SerializeField] private Transform spawnPoint;

    private const float FallbackKillY = -20f;
    // Açık Euler entegrasyonu için güvenli sınır: ω·dt bunu aşarsa yay sabiti kırpılır (titreme olmasın).
    private const float MaxOmegaDt = 0.8f;
    // Uzaktan çekilirken eşya en yakın olduğu mesafeden bu kadar geri kaçarsa kopar.
    private const float BreakSlack = 0.75f;

    public GrabController Holder { get; private set; }
    public bool IsHeld => Holder != null;
    public Rigidbody Body => rb;
    public float Mass => rb.mass;
    // Eşyanın en büyük yarı boyutu: tutma noktası duvara bu kadar yaklaşmaz.
    public float Radius { get; private set; }
    // Varsa çizgi film fiziği (boing, asılı kalma, gerilmeli fırlatma, jöle).
    public CartoonPhysics Cartoon { get; private set; }
    // Varsa hasar/değer takibi.
    public PropDurability Durability { get; private set; }

    private Rigidbody rb;
    private Collider[] colliders;
    private Collider[] ignoredPlayerColliders;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool defaultGravity;
    private float defaultAngularDamping;
    private float defaultMaxAngularVelocity;
    // Tutuş anındaki rotasyon, oyuncunun yaw'ına göre: eşya oyuncuyla birlikte döner, kendi duruşunu korur.
    private Quaternion holdRotationOffset = Quaternion.identity;
    private float closestHoldDistance;
    private float holdStartTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Cartoon = GetComponent<CartoonPhysics>();
        Durability = GetComponent<PropDurability>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxDepenetrationVelocity = 3f; // duvara gömülürse fırlamasın, yavaşça çıksın
        colliders = GetComponentsInChildren<Collider>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    public string GetPrompt()
    {
        if (IsHeld) return string.Empty;
        string text = "E - " + displayName + " tut";
        if (Durability != null)
            text += Durability.IsBroken ? "  (bozuk)" : "  ($" + Durability.Value + "/" + Durability.BaseValue + ")";
        return text;
    }

    public void Interact(PlayerInteraction oyuncu)
    {
        if (IsHeld) return; // bırakmayı GrabController yapar
        // Vakumla balon gibi bağlıyken tutulmaz (ikisi aynı Rigidbody'yi sürmesin).
        if (TryGetComponent(out BalloonPhysicsItem balloon) && balloon.IsAttached) return;
        var grabber = oyuncu.GetComponentInParent<GrabController>();
        if (grabber != null) grabber.TryGrab(this);
    }

    // GrabController çağırır.
    public void BeginHold(GrabController holder, GrabSettings s, Quaternion holderYaw)
    {
        if (settings == null) settings = s;
        CancelInvoke(nameof(RestorePlayerCollision));
        RestorePlayerCollision(); // önceki tutuşun bekleyen işi varsa temizle

        // Havada asılıyken yakalanırsa yerçekimi geri gelsin (defaultGravity doğru okunsun).
        if (Cartoon != null) Cartoon.Cancel();

        Holder = holder;
        holdRotationOffset = Quaternion.Inverse(holderYaw) * rb.rotation;
        closestHoldDistance = float.PositiveInfinity;
        holdStartTime = Time.time;
        Radius = 0f;
        foreach (var c in colliders)
            if (c != null && !c.isTrigger)
                Radius = Mathf.Max(Radius, c.bounds.extents.x, c.bounds.extents.y, c.bounds.extents.z);

        defaultGravity = rb.useGravity;
        defaultAngularDamping = rb.angularDamping;
        defaultMaxAngularVelocity = rb.maxAngularVelocity;
        rb.useGravity = false;
        rb.angularDamping = settings.heldAngularDamping;
        rb.maxAngularVelocity = settings.maxAngularSpeed;
        rb.WakeUp();

        // Tutulan eşya oyuncunun gövdesine takılıp onu itmesin / oyuncunun içinde titremesin.
        ignoredPlayerColliders = holder.PlayerColliders;
        SetPlayerCollision(true);
    }

    public void EndHold()
    {
        if (!IsHeld) return;
        Holder = null;
        rb.useGravity = defaultGravity;
        rb.angularDamping = defaultAngularDamping;
        rb.maxAngularVelocity = defaultMaxAngularVelocity;
        // Oyuncuyla çarpışma biraz sonra açılır: bırakıldığı an oyuncunun içindeyse fırlamasın.
        float delay = settings != null ? settings.restoreCollisionDelay : 0.4f;
        Invoke(nameof(RestorePlayerCollision), delay);
    }

    // Fırlatma: kütleye göre impulse; ağır eşya daha yavaş uçar.
    // power: gerilmeli fırlatma çarpanı (1 = normal).
    public void Throw(Vector3 direction, GrabSettings s, float power = 1f)
    {
        EndHold();
        Vector3 keep = rb.linearVelocity * s.keepVelocityOnThrow;
        rb.linearVelocity = keep;
        float impulse = s.throwImpulse * power * Mathf.Pow(rb.mass / s.referenceMass, s.massSpringExponent);
        rb.AddForce(direction.normalized * impulse, ForceMode.Impulse);
        if (Cartoon != null) Cartoon.OnThrown();
    }

    public void Respawn()
    {
        if (IsHeld) Holder.Release();
        if (Cartoon != null) Cartoon.Cancel();
        Vector3 pos = spawnPoint != null ? spawnPoint.position : spawnPosition;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : spawnRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = pos;
        rb.rotation = rot;
        transform.SetPositionAndRotation(pos, rot);
    }

    private void FixedUpdate()
    {
        float killY = settings != null ? settings.killY : FallbackKillY;
        if (rb.position.y < killY)
        {
            Respawn();
            return;
        }
        if (!IsHeld) return;

        GrabSettings s = settings;
        Vector3 target = Holder.HoldPosition;

        // Bir şeye takılıp tutma noktasından çok uzak kaldı: bırak. Uzaktan yakalanan eşya henüz yaklaşmadığı
        // için kopmasın diye sınır, o ana kadarki en yakın mesafeden ölçülür (yaklaştıkça maxDistance'a iner).
        float distance = Vector3.Distance(target, rb.worldCenterOfMass);
        closestHoldDistance = Mathf.Min(closestHoldDistance, distance);
        bool snagged = distance > Mathf.Max(s.maxDistance, closestHoldDistance + BreakSlack);
        bool neverArrived = distance > s.maxDistance && Time.time - holdStartTime > s.pullTimeout;
        if (snagged || neverArrived)
        {
            Holder.Release();
            return;
        }

        ApplySpringDamper(target, Holder.HoldVelocity, s);
        ApplyRotationAlign(Holder.HoldYaw * holdRotationOffset, s);
    }

    // F = k·(hedef - konum) + c·(hedefHızı - hız). Hedef hızı (oyuncu yürür/zıplarken tutma noktasının hızı)
    // sönümlemeye girer: eşya oyuncuyu takip eder ama ivmesi yine yayla sınırlı, ışınlanmaz.
    private void ApplySpringDamper(Vector3 target, Vector3 targetVelocity, GrabSettings s)
    {
        float m = rb.mass;
        float k = s.SpringFor(m);
        float dt = Time.fixedDeltaTime;
        float kMax = m * (MaxOmegaDt / dt) * (MaxOmegaDt / dt);
        if (k > kMax) k = kMax;
        float c = s.DamperFor(m, k);

        // Kütle merkezi hedefe çekilir: pivotu zeminde olan mobilya (sandalye, masa) gövdesinden tutulur.
        Vector3 force = k * (target - rb.worldCenterOfMass) + c * (targetVelocity - rb.linearVelocity);
        force = Vector3.ClampMagnitude(force, s.MaxForceFor(m));
        rb.AddForce(force, ForceMode.Force);

        float maxSpeed = s.MaxSpeedFor(m);
        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude > maxSpeed * maxSpeed)
            rb.linearVelocity = v.normalized * maxSpeed;
    }

    // Hedef rotasyona açısal yay + sıkı sönüm (Acceleration: eylemsizlikten bağımsız, şekle göre tutarlı his).
    private void ApplyRotationAlign(Quaternion targetRotation, GrabSettings s)
    {
        Quaternion delta = targetRotation * Quaternion.Inverse(rb.rotation);
        if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w); // kısa yoldan dön
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (float.IsNaN(axis.x) || angleDeg < 0.01f) axis = Vector3.zero;

        Vector3 accel = axis * (angleDeg * Mathf.Deg2Rad * s.rotationSpring) - rb.angularVelocity * s.rotationDamping;
        rb.AddTorque(accel, ForceMode.Acceleration);
    }

    private void SetPlayerCollision(bool ignore)
    {
        if (ignoredPlayerColliders == null) return;
        foreach (var c in colliders)
        {
            if (c == null) continue;
            foreach (var p in ignoredPlayerColliders)
                if (p != null) Physics.IgnoreCollision(c, p, ignore);
        }
    }

    private void RestorePlayerCollision()
    {
        if (IsHeld) return;
        SetPlayerCollision(false);
        ignoredPlayerColliders = null;
    }

    private void OnDisable()
    {
        if (IsHeld) Holder.Release();
    }
}
