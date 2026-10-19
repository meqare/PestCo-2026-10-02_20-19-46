using UnityEngine;

// Vakumla çekilebilen fizik nesnesi (Pullable layer). Kuvveti AddForce ile alır, tutulurken yumuşakça
// tutma noktasına oturur (parent yok, kinematic yok). Görsel squash & stretch sadece "visual" child'da olur.
[RequireComponent(typeof(Rigidbody))]
public class PullableObject : MonoBehaviour
{
    [Tooltip("VacuumSettings asset'i (VacuumGun ile aynı). Boşsa silah ilk çekişte atar.")]
    [SerializeField] private VacuumSettings settings;
    [Tooltip("Squash & stretch uygulanacak GÖRSEL child (collider'ı olan obje DEĞİL). Boşsa efekt yok.")]
    [SerializeField] private Transform visual;

    public bool IsHeld { get; private set; }
    public float LastPullTime { get; private set; } = -100f;
    // Aynı fizik adımında birden çok collider'dan tekrar işlenmesin diye.
    public int LastScanId { get; set; } = -1;
    public Rigidbody Body => rb;

    private Rigidbody rb;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool defaultGravity;
    private float defaultDrag;
    private float defaultAngularDrag;
    private bool modified;
    private Vector3 visualBaseScale = Vector3.one;
    private Vector3 currentScale = Vector3.one;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        defaultGravity = rb.useGravity;
        defaultDrag = rb.linearDamping;
        defaultAngularDrag = rb.angularDamping;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        if (visual != null)
        {
            visualBaseScale = visual.localScale;
            currentScale = visualBaseScale;
        }
    }

    public void Bind(VacuumSettings s)
    {
        if (settings == null) settings = s;
    }

    // Çekme: koni içindeyken her fizik adımında çağrılır. distance01 = mesafe / maxDistance.
    public void ApplyPull(Vector3 towards, float distance01, VacuumSettings s)
    {
        EnterModified(s);
        LastPullTime = Time.time;

        float falloff = Mathf.Max(0f, s.distanceFalloff.Evaluate(Mathf.Clamp01(distance01)));
        // massInfluence 0: kuvvet kütleyle çarpılır, ivme sabit. 1: ham kuvvet, ağır nesne yavaş.
        float massScale = Mathf.Lerp(rb.mass, 1f, s.massInfluence);
        if (s.horizontalOnly)
        {
            towards.y = 0f;
            towards.Normalize();
        }
        rb.AddForce(towards * (s.pullForce * falloff * massScale), ForceMode.Force);
        ClampSpeed(s.maxPullSpeed, s.horizontalOnly);
    }

    // Tutma: yay + sönümleme ile hedef noktaya yumuşakça oturur. Kütleden bağımsız ivme kullanır.
    public void ApplyHold(Vector3 target, VacuumSettings s)
    {
        EnterModified(s);
        LastPullTime = Time.time;
        rb.angularDamping = s.heldAngularDrag;

        Vector3 delta = target - rb.position;
        Vector3 vel = rb.linearVelocity;
        if (s.horizontalOnly)
        {
            // Dikey eksen serbest: yerçekimi nesneyi yere bastırır, sadece yatayda takip eder.
            delta.y = 0f;
            vel.y = 0f;
        }
        rb.AddForce(delta * s.holdSpring - vel * s.holdDamping, ForceMode.Acceleration);
        ClampSpeed(s.maxHoldSpeed, s.horizontalOnly);
    }

    public void SetHeld(bool held)
    {
        IsHeld = held;
        if (!held) LastPullTime = Time.time; // grace süresi boyunca düşmeden yavaşça normale döner
    }

    // Fırlatma: Impulse; ağır nesne daha yavaş uçar.
    public void Throw(Vector3 direction, VacuumSettings s)
    {
        Throw(direction, s.throwForce);
    }

    // Fırlatma, kuvveti dışarıdan verilir (şarjlı fırlatma).
    public void Throw(Vector3 direction, float force)
    {
        SetHeld(false);
        RestoreDefaults();
        rb.AddForce(direction * force, ForceMode.Impulse);
    }

    // Sınır dışına çıkınca spawn noktasına taşır.
    public void Respawn()
    {
        SetHeld(false);
        RestoreDefaults();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = spawnPosition;
        rb.rotation = spawnRotation;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
    }

    private void FixedUpdate()
    {
        if (settings != null && rb.position.y < settings.killY)
        {
            Respawn();
            return;
        }
        // Çekim/tutma bitince yerçekimi ve sürtünme eski haline döner.
        if (modified && !IsHeld && settings != null && Time.time - LastPullTime > settings.pullGraceTime)
            RestoreDefaults();
    }

    private void LateUpdate()
    {
        if (visual == null || settings == null) return;

        Vector3 target = visualBaseScale;
        Vector3 vel = rb.linearVelocity;
        float speed = vel.magnitude;
        if (speed > 0.05f && visual.parent != null)
        {
            float k = Mathf.Clamp01(speed / settings.speedForMaxStretch) * settings.stretchAmount;
            float along = 1f + k;
            float across = 1f / Mathf.Sqrt(along); // hacim yaklaşık korunur
            // Hız yönü visual'ın ebeveyn uzayında en baskın eksene yaslanır.
            Vector3 dir = visual.parent.InverseTransformDirection(vel / speed);
            Vector3 a = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
            Vector3 mul = new Vector3(across, across, across);
            if (a.x >= a.y && a.x >= a.z) mul.x = along;
            else if (a.y >= a.z) mul.y = along;
            else mul.z = along;
            target = Vector3.Scale(visualBaseScale, mul);
        }
        currentScale = Vector3.Lerp(currentScale, target, 1f - Mathf.Exp(-settings.squashLerpSpeed * Time.deltaTime));
        visual.localScale = currentScale;
    }

    private void EnterModified(VacuumSettings s)
    {
        if (settings == null) settings = s;
        if (modified) return;
        modified = true;
        if (s.disableGravityWhilePulled) rb.useGravity = false;
        rb.WakeUp();
    }

    private void RestoreDefaults()
    {
        if (!modified) return;
        modified = false;
        rb.useGravity = defaultGravity;
        rb.linearDamping = defaultDrag;
        rb.angularDamping = defaultAngularDrag;
    }

    private void ClampSpeed(float max, bool horizontalOnly)
    {
        Vector3 v = rb.linearVelocity;
        float y = v.y;
        if (horizontalOnly) v.y = 0f;
        if (v.sqrMagnitude > max * max)
            v = v.normalized * max;
        if (horizontalOnly) v.y = y;
        rb.linearVelocity = v;
    }

    private void OnDisable()
    {
        IsHeld = false;
        RestoreDefaults();
    }
}
