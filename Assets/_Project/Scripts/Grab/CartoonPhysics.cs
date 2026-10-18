using UnityEngine;

// Çizgi film fiziği: GrabbableObject'in yanına eklenir. Dört absürt kural:
//  1) Boing: sert çarpan eşya enerji KAZANARAK sekiyor (sınırlı sayıda).
//  2) Coyote asılı kalma: fırlatılan/sekmiş eşya zirvede havada donup titriyor, sonra örs gibi çakılıyor.
//  3) Gerilmeli fırlatma: GrabController şarj değerini SetCharge ile verir; eşya ezilip titrer.
//  4) Jöle: hız ve çarpmaya göre gerçek bir yayla ezilip uzar (sadece "visual" child'da, collider'a dokunmaz).
// Fizik hep Rigidbody üzerinden (hız/AddForce); transform taşınmaz.
[RequireComponent(typeof(Rigidbody))]
public class CartoonPhysics : MonoBehaviour
{
    [SerializeField] private CartoonPhysicsSettings settings;
    [Tooltip("Ezilip uzayacak GÖRSEL child (collider'ı olan obje DEĞİL). Boşsa görsel efekt yok, fizik yine çalışır.")]
    [SerializeField] private Transform visual;

    public CartoonPhysicsSettings Settings => settings;

    // Tek bir darbenin yaya verebileceği en yüksek hız (aşırı büyük çarpmada yay patlamasın).
    private const float MaxKick = 25f;

    private enum Phase { None, Armed, Hanging, Slamming }

    private Rigidbody rb;
    private GrabbableObject grabbable;
    private Phase phase;
    private float airTime;
    private float hangTimer;
    private bool savedGravity;
    private Vector3 lastVelocity;
    private int boingCount;
    private float lastBoingTime = -99f;
    private float restTimer;
    private float charge;

    // Jöle yayı: s > 0 uzama, s < 0 ezilme, axis = deformasyon ekseni (visual ebeveyn uzayında).
    private float s, sVel;
    private int axis = 1;
    private Vector3 visualBaseScale = Vector3.one;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<GrabbableObject>();
        if (visual != null) visualBaseScale = visual.localScale;
    }

    // GrabbableObject çağırır: tutulurken kural yok, durum sıfırlanır.
    public void Cancel()
    {
        if (phase == Phase.Hanging) rb.useGravity = savedGravity;
        phase = Phase.None;
        boingCount = 0;
        charge = 0f;
    }

    // Fırlatıldı: zirvede donma için hazırla.
    public void OnThrown()
    {
        phase = Phase.Armed;
        airTime = 0f;
        boingCount = 0;
        charge = 0f;
    }

    public void SetCharge(float value01) => charge = Mathf.Clamp01(value01);

    private void FixedUpdate()
    {
        if (settings == null) return;
        if (grabbable != null && grabbable.IsHeld)
        {
            Cancel();
            lastVelocity = rb.linearVelocity;
            return;
        }

        float dt = Time.fixedDeltaTime;
        Vector3 vel = rb.linearVelocity;

        switch (phase)
        {
            case Phase.Armed:
                airTime += dt;
                if (settings.hangEnabled && airTime > settings.hangMinAirTime &&
                    lastVelocity.y > settings.hangApexSpeed && vel.y <= settings.hangApexSpeed)
                    StartHang();
                break;

            case Phase.Hanging:
                hangTimer -= dt;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity *= 0.85f;
                if (hangTimer <= 0f) StartSlam();
                break;

            case Phase.Slamming:
                rb.AddForce(Physics.gravity * (settings.slamGravityMultiplier - 1f), ForceMode.Acceleration);
                break;
        }

        // Durgunlaşınca sekme sayacı sıfırlanır, tekrar enerji kazanabilir.
        if (phase != Phase.Hanging)
        {
            restTimer = vel.sqrMagnitude < 0.09f ? restTimer + dt : 0f;
            if (restTimer > 0.5f)
            {
                boingCount = 0;
                if (phase == Phase.Armed) phase = Phase.None;
            }
        }
        lastVelocity = rb.linearVelocity;
    }

    private void StartHang()
    {
        phase = Phase.Hanging;
        hangTimer = settings.hangTime;
        savedGravity = rb.useGravity;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
    }

    private void StartSlam()
    {
        phase = Phase.Slamming;
        rb.useGravity = savedGravity;
        rb.linearVelocity = Vector3.down * settings.slamInitialSpeed;
        axis = 1;
        sVel += settings.maxStretch * 6f; // dikey uzama darbesi
    }

    private void OnCollisionEnter(Collision col)
    {
        if (settings == null || col.contactCount == 0) return;
        if (grabbable != null && grabbable.IsHeld) return;

        Vector3 n = col.GetContact(0).normal;
        if (Vector3.Dot(lastVelocity, n) > 0f) n = -n; // her zaman yüzeyden dışarı bakan normal
        float speedAlong = -Vector3.Dot(lastVelocity, n);
        if (speedAlong < 0.5f) return;

        Vector3 point = col.GetContact(0).point;
        KickSquash(n, speedAlong);

        if (phase == Phase.Slamming)
        {
            // SPLAT: örs gibi çakıldı, ezildi, hafifçe seğirdi.
            phase = Phase.None;
            axis = DominantAxis(n);
            sVel -= speedAlong * settings.impactSquashPerSpeed * 1.5f;
            rb.linearVelocity = n * settings.splatBounceSpeed;
            Juice.Shake(settings.slamShake, 0.25f);
            Juice.Hit(point, n);
            boingCount = 0;
            return;
        }

        if (!settings.boingEnabled || speedAlong < settings.boingMinSpeed) return;
        if (boingCount >= settings.maxBoings || Time.time < lastBoingTime + settings.boingCooldown) return;

        // Normal bileşeni enerji kazanarak yansır, teğet bileşen aynen kalır.
        Vector3 tangent = lastVelocity + n * speedAlong;
        Vector3 v = tangent + n * (speedAlong * settings.boingGain);
        rb.linearVelocity = Vector3.ClampMagnitude(v, settings.maxBounceSpeed);
        boingCount++;
        lastBoingTime = Time.time;
        Juice.Hit(point, n);
        Juice.Shake(0.01f * speedAlong, 0.12f);

        // Sekme yukarı çıkıyorsa zirvede yine donabilir.
        if (rb.linearVelocity.y > 1f) { phase = Phase.Armed; airTime = 0f; }
    }

    private void KickSquash(Vector3 normal, float speed)
    {
        axis = DominantAxis(normal);
        sVel -= speed * settings.impactSquashPerSpeed * 0.1f;
    }

    // Normalin ebeveyn uzayındaki en baskın ekseni.
    private int DominantAxis(Vector3 worldDir)
    {
        Vector3 d = visual != null && visual.parent != null ? visual.parent.InverseTransformDirection(worldDir) : worldDir;
        float x = Mathf.Abs(d.x), y = Mathf.Abs(d.y), z = Mathf.Abs(d.z);
        if (x >= y && x >= z) return 0;
        return y >= z ? 1 : 2;
    }

    private void LateUpdate()
    {
        if (visual == null || settings == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        sVel = Mathf.Clamp(sVel, -MaxKick, MaxKick);

        float target = 0f;
        if (phase == Phase.Hanging)
        {
            // Zirvede gerilip titriyor: hafif ezik + hızlı titreme.
            target = -0.3f + Mathf.Sin(Time.time * 55f) * 0.1f;
            axis = 1;
        }
        else if (charge > 0f)
        {
            target = -charge * settings.chargeSquash + Mathf.Sin(Time.time * 60f) * settings.chargeQuiver * charge;
            axis = 1;
        }
        else
        {
            Vector3 v = rb.linearVelocity;
            float speed = v.magnitude;
            if (speed > 1f)
            {
                axis = DominantAxis(v / speed);
                target = Mathf.Min(speed * settings.stretchPerSpeed, settings.maxStretch);
            }
        }

        // Yarı-örtük Euler, iki alt adım.
        for (int i = 0; i < 2; i++)
        {
            float h = dt * 0.5f;
            sVel += (-settings.jellyStiffness * (s - target) - settings.jellyDamping * sVel) * h;
            float next = s + sVel * h;
            s = Mathf.Clamp(next, -settings.maxSquash, settings.maxStretch);
            // Sınıra çarpınca hız sıfırlanır: aksi halde büyük darbe yayı uzun süre sınıra yapıştırır.
            if (!Mathf.Approximately(next, s)) sVel = 0f;
        }

        float along = 1f + s;
        float across = 1f / Mathf.Sqrt(Mathf.Max(0.2f, along)); // hacim yaklaşık korunur
        Vector3 mul = new Vector3(across, across, across);
        mul[axis] = along;
        visual.localScale = Vector3.Scale(visualBaseScale, mul);
    }

    private void OnDisable()
    {
        if (phase == Phase.Hanging && rb != null) rb.useGravity = savedGravity;
        phase = Phase.None;
        if (visual != null) visual.localScale = visualBaseScale;
    }
}
