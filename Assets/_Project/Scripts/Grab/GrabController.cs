using UnityEngine;
using UnityEngine.InputSystem;

// Oyuncu tarafı: tutma noktasını (hold point) hesaplar, bırakma/fırlatma girdisini okur, taşınan kütleye göre
// hız çarpanı verir. Tutma E ile PlayerInteraction -> GrabbableObject.Interact üzerinden başlar.
// Tutma noktası GrabbableObject.FixedUpdate'ten önce hesaplansın diye erken çalışır.
[DefaultExecutionOrder(-50)]
public class GrabController : MonoBehaviour
{
    [SerializeField] private GrabSettings settings;
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Tutma noktasının duvar kontrolü bu layer'lara bakar (Grabbable ve oyuncu hariç tutulur).")]
    [SerializeField] private LayerMask holdPointObstacles = ~0;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    public GrabSettings Settings => settings;
    public GrabbableObject Held { get; private set; }
    public bool IsHolding => Held != null;
    // Bu karede E tuşunu tutma sistemi kullandı: PlayerInteraction aynı tuşla başka şeyi tetiklemesin.
    public bool ConsumesInteract => IsHolding || releaseFrame == Time.frameCount;
    // PlayerController hızını bununla çarpar.
    public float SpeedMultiplier => settings != null ? settings.SpeedMultiplierFor(CarriedMass) : 1f;
    public float CarriedMass => Held != null ? Held.Mass : 0f;

    public Vector3 HoldPosition { get; private set; }
    public Vector3 HoldVelocity { get; private set; }
    public Quaternion HoldYaw { get; private set; } = Quaternion.identity;
    public Collider[] PlayerColliders { get; private set; }

    private Quaternion smoothedLook = Quaternion.identity;
    private float charge; // 0..1 gerilmeli fırlatma
    private bool charging;
    private int grabFrame = -1;
    private int releaseFrame = -1;
    private readonly RaycastHit[] probeHits = new RaycastHit[16];

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        PlayerColliders = GetComponentsInChildren<Collider>(true);
        if (settings == null)
            Debug.LogWarning("GrabController: GrabSettings atanmamış, tutma çalışmaz.", this);
    }

    public bool TryGrab(GrabbableObject target)
    {
        if (settings == null || cameraTransform == null || target == null || target.IsHeld) return false;
        if (IsHolding || releaseFrame == Time.frameCount) return false;
        if (!HasLineOfSight(target)) return false;

        smoothedLook = cameraTransform.rotation;
        HoldYaw = YawOf(smoothedLook);
        HoldPosition = ComputeHoldPoint(target);
        HoldVelocity = Vector3.zero;

        Held = target;
        grabFrame = Time.frameCount;
        target.BeginHold(this, settings, HoldYaw);
        return true;
    }

    public void Release()
    {
        if (Held == null) return;
        var item = Held;
        Held = null;
        releaseFrame = Time.frameCount;
        ClearCharge(item);
        item.EndHold();
    }

    private void ClearCharge(GrabbableObject item)
    {
        charging = false;
        charge = 0f;
        if (item != null && item.Cartoon != null) item.Cartoon.SetCharge(0f);
    }

    private void Update()
    {
        if (!IsHolding || settings == null) return;
        var kb = Keyboard.current;
        if (kb == null || Cursor.lockState != CursorLockMode.Locked) return;

        if (grabFrame != Time.frameCount && kb[settings.releaseKey].wasPressedThisFrame)
        {
            Release();
        }
        else
        {
            HandleThrow(kb);
        }
    }

    // Çizgi film fiziği varsa: Q basılı tut = gerilir, bırak = fırlat. Yoksa tek basışta fırlatır.
    private void HandleThrow(Keyboard kb)
    {
        var key = kb[settings.throwKey];
        var cartoon = Held.Cartoon;
        var cs = cartoon != null ? cartoon.Settings : null;
        bool chargedMode = cs != null && cs.chargedThrow;

        if (!chargedMode)
        {
            if (key.wasPressedThisFrame) ThrowHeld(1f);
            return;
        }

        if (key.wasPressedThisFrame) charging = true;
        if (!charging) return;

        if (key.isPressed)
        {
            charge = Mathf.Min(1f, charge + Time.deltaTime / cs.chargeTime);
            cartoon.SetCharge(charge);
        }
        if (key.wasReleasedThisFrame)
            ThrowHeld(Mathf.Lerp(1f, cs.maxChargeMultiplier, charge));
    }

    private void ThrowHeld(float power)
    {
        var item = Held;
        Held = null;
        releaseFrame = Time.frameCount;
        ClearCharge(item);
        item.Throw(cameraTransform.forward, settings, power);
    }

    private void FixedUpdate()
    {
        if (!IsHolding || cameraTransform == null) return;
        float dt = Time.fixedDeltaTime;

        // Bakış yönü gecikmeli takip edilir: oyuncu hızlı dönünce eşya arkadan yumuşakça gelir.
        smoothedLook = Quaternion.Slerp(smoothedLook, cameraTransform.rotation, 1f - Mathf.Exp(-settings.holdPointFollowSharpness * dt));
        HoldYaw = YawOf(smoothedLook);

        Vector3 previous = HoldPosition;
        HoldPosition = ComputeHoldPoint(Held);
        // Tutma noktasının hızı (yürüme, zıplama, dönme): eşya bunu yay-sönümle izler.
        // Duvar taraması noktayı aniden kaydırırsa sıçrama olmasın diye sınırlanır.
        HoldVelocity = Vector3.ClampMagnitude((HoldPosition - previous) / dt, settings.maxSpeed * 2f);
    }

    // Kameradan bakış yönünde holdDistance; duvar varsa önünde durur (nokta duvarın arkasına geçmez).
    private Vector3 ComputeHoldPoint(GrabbableObject item)
    {
        Vector3 origin = cameraTransform.position;
        Vector3 dir = smoothedLook * Vector3.forward;
        float dist = settings.holdDistance;

        int count = Physics.SphereCastNonAlloc(origin, settings.holdPointProbeRadius, dir, probeHits, dist,
            holdPointObstacles, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = probeHits[i];
            if (hit.distance <= 0f) continue; // başlangıçta içinde olduğu collider (ör. oyuncu)
            Rigidbody body = hit.collider.attachedRigidbody;
            if (item != null && body == item.Body) continue;
            if (body != null && body.GetComponent<GrabbableObject>() != null) continue; // diğer eşyalar fizikle itilir
            if (hit.collider.transform.IsChildOf(transform)) continue;
            // Eşyanın merkezi duvardan yarı boyu kadar önde dursun: yüzüne yaslanır, içine bastırılmaz.
            float itemRadius = item != null ? item.Radius : 0f;
            dist = Mathf.Min(dist, Mathf.Max(0.3f, hit.distance - itemRadius));
        }
        // Gerilirken eşya oyuncuya doğru geri çekilir (sapan gibi), bırakınca fırlar.
        if (charge > 0f && item != null && item.Cartoon != null && item.Cartoon.Settings != null)
            dist = Mathf.Max(0.3f, dist - charge * item.Cartoon.Settings.chargeBackDistance);
        return origin + dir * dist + Vector3.up * settings.holdHeightOffset;
    }

    // PlayerInteraction duvarları görmez; duvarın arkasındaki eşya tutulamasın.
    private bool HasLineOfSight(GrabbableObject item)
    {
        Vector3 origin = cameraTransform.position;
        Vector3 to = item.Body.worldCenterOfMass - origin;
        int count = Physics.RaycastNonAlloc(origin, to.normalized, probeHits, to.magnitude, holdPointObstacles, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var col = probeHits[i].collider;
            if (col.attachedRigidbody != null && col.attachedRigidbody.GetComponent<GrabbableObject>() != null) continue;
            if (col.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    private static Quaternion YawOf(Quaternion rotation) => Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);

    // CharacterController rigidbody'leri kendiliğinden itmez: tutulmayan eşyaya yürüyünce hafifçe it.
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (settings == null || settings.playerPushStrength <= 0f) return;
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (!body.TryGetComponent(out GrabbableObject item) || item.IsHeld) return;
        if (hit.moveDirection.y < -0.3f) return; // üstüne basıyor

        Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        body.AddForceAtPosition(push * settings.playerPushStrength, hit.point, ForceMode.Impulse);
    }

    private void OnDisable() => Release();

    private void OnDrawGizmos()
    {
        if (!showGizmos || settings == null) return;
        Transform cam = cameraTransform != null ? cameraTransform : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null) return;

        Vector3 hold = Application.isPlaying && IsHolding
            ? HoldPosition
            : cam.position + cam.forward * settings.holdDistance + Vector3.up * settings.holdHeightOffset;

        // Tutma noktası
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(hold, 0.1f);
        Gizmos.DrawLine(cam.position, hold);
        // Kopma mesafesi: eşya bu kürenin dışına çıkarsa bırakılır.
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(hold, settings.maxDistance);

        if (Application.isPlaying && IsHolding)
        {
            // Eşyadan tutma noktasına çekme çizgisi: yeşil = yakın, kırmızı = kopmaya yakın.
            Vector3 p = Held.Body.worldCenterOfMass;
            float t = Mathf.Clamp01(Vector3.Distance(p, hold) / settings.maxDistance);
            Gizmos.color = Color.Lerp(Color.green, Color.red, t);
            Gizmos.DrawLine(p, hold);
        }
    }
}
