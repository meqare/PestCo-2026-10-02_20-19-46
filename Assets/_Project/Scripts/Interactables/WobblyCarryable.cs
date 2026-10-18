using UnityEngine;
using UnityEngine.InputSystem;

// Çılgın çizgi film taşıma fiziği: E ile eşyayı kaldırırsın ama eşya ağır, sallanan ve kaygan.
//  - Elinde yaylı/gecikmeli durur; hızlı dönünce savrulur, koşunca sallanır.
//  - Tutuş (grip) zamanla ve koşunca/zıplayınca azalır; sıfırlanınca elinden kayar.
//  - Duvara/eşyaya yan yan çarparsan "BONK": sert çarpmada elinden fırlar.
//  - Sol tık: fırlat.
[RequireComponent(typeof(Rigidbody))]
public class WobblyCarryable : MonoBehaviour, IInteractable
{
    [Header("İsim")]
    [SerializeField] private string displayName = "Sandalye";

    [Header("Ağırlık")]
    [SerializeField, Min(0.1f)] private float mass = 6f;
    [Tooltip("Taşırken oyuncu hızı bununla çarpılır.")]
    [SerializeField, Range(0.2f, 1f)] private float carrySpeedMultiplier = 0.6f;

    [Header("Tutma (yay)")]
    [SerializeField] private float holdDistance = 1.5f;
    [SerializeField] private float holdHeight = -0.35f;
    [SerializeField] private float spring = 55f;
    [SerializeField] private float damper = 7f;
    [SerializeField] private float maxPullSpeed = 5.5f;
    [Tooltip("Yüksek = eşya yaw'da daha geç döner (savrulma).")]
    [SerializeField] private float turnLag = 3.5f;
    [SerializeField] private float wobbleTorque = 1.5f;
    [SerializeField] private float breakDistance = 3.2f;
    [Tooltip("Duvar yüzünden bu mesafeden yakına sıkışırsa...")]
    [SerializeField] private float squeezeDistance = 0.8f;
    [Tooltip("...bu kadar saniye sonra elinden kayar.")]
    [SerializeField] private float squeezeTime = 0.25f;

    [Header("Tutuş (grip)")]
    [SerializeField] private float gripDrainIdle = 0.03f;
    [SerializeField] private float gripDrainRun = 0.18f;
    [SerializeField] private float gripLossOnJump = 0.25f;
    [SerializeField] private float gripLossOnTurn = 0.002f;

    [Header("Çarpma (BONK)")]
    [Tooltip("Bunun altındaki çarpma hızı yok sayılır.")]
    [SerializeField] private float bonkMinSpeed = 1.6f;
    [Tooltip("Bunun üstündeki çarpmada eşya hemen elinden fırlar.")]
    [SerializeField] private float bonkDropSpeed = 4.2f;
    [SerializeField] private float gripLossPerBonkSpeed = 0.14f;
    [SerializeField] private float bonkCooldown = 0.15f;

    [Header("Fırlatma")]
    [SerializeField] private float throwSpeed = 11f;

    public static WobblyCarryable Held { get; private set; }
    public bool IsHeld => holder != null;

    private Rigidbody rb;
    private Collider[] colliders;
    private Collider[] playerColliders;
    private Transform holder;       // oyuncu kamerası
    private PlayerController player;
    private PlayerStatus status;
    private float grip = 1f;
    private float lastBonkTime = -99f;
    private float dropFrame = -1;
    private float lastYaw;
    private float squash;           // 0..1 cartoon ezilme
    private Vector3 baseScale;
    private float noiseSeed;
    private bool jumpWasHeld;
    private float squeezeTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxDepenetrationVelocity = 2f; // duvara gömülürse fırlamasın, yavaşça çıksın
        rb.maxLinearVelocity = 14f;
        colliders = GetComponentsInChildren<Collider>();
        baseScale = transform.localScale;
        noiseSeed = Random.value * 100f;
    }

    public string GetPrompt()
    {
        if (IsHeld) return "E - Bırak   |   Sol tık - Fırlat   |   Tutuş: " + Mathf.CeilToInt(grip * 100f) + "%";
        return "E - " + displayName + " taşı";
    }

    public void Interact(PlayerInteraction oyuncu)
    {
        if (IsHeld) { Drop(false); return; }
        if (Held != null || dropFrame == Time.frameCount) return;
        var cam = Camera.main != null ? Camera.main.transform : oyuncu.transform;
        PickUp(oyuncu, cam);
    }

    private void PickUp(PlayerInteraction oyuncu, Transform cam)
    {
        CancelInvoke(nameof(RestorePlayerCollision));
        holder = cam;
        player = oyuncu.GetComponentInParent<PlayerController>();
        status = oyuncu.GetComponentInParent<PlayerStatus>();
        grip = 1f;
        Held = this;
        lastYaw = holder.eulerAngles.y;
        rb.isKinematic = false;
        rb.useGravity = false;      // havada yayla dengelenir; yerde sürtünme için kapalı tutmuyoruz
        rb.linearDamping = 0f;
        rb.angularDamping = 1.2f;

        // Oyuncuyla çarpışmasın (CharacterController eşyayı itip titretmesin).
        var cc = oyuncu.GetComponentInParent<CharacterController>();
        if (cc != null)
        {
            playerColliders = new Collider[] { cc };
            foreach (var c in colliders) Physics.IgnoreCollision(c, cc, true);
        }
        Squash(0.5f);
    }

    private void RestorePlayerCollision()
    {
        if (IsHeld || playerColliders == null) return;
        foreach (var c in colliders) if (c != null) foreach (var p in playerColliders) if (p != null) Physics.IgnoreCollision(c, p, false);
    }

    private void Drop(bool lostGrip)
    {
        if (!IsHeld) return;
        // Oyuncuyla çarpışma biraz sonra geri açılır (elinden düşerken oyuncunun içinde sıkışıp fırlamasın).
        Invoke(nameof(RestorePlayerCollision), 0.6f);
        squeezeTimer = 0f;
        holder = null;
        Held = null;
        dropFrame = Time.frameCount;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        if (lostGrip)
        {
            rb.AddTorque(Random.onUnitSphere * 6f, ForceMode.VelocityChange);
            Squash(1f);
        }
    }

    private void Update()
    {
        if (!IsHeld) { AnimateSquash(); return; }

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null && dropFrame != Time.frameCount && kb.eKey.wasPressedThisFrame) { Drop(false); return; }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector3 dir = holder.forward;
            Drop(false);
            rb.linearVelocity = dir * throwSpeed;
            rb.AddTorque(Random.onUnitSphere * 5f, ForceMode.VelocityChange);
            return;
        }

        UpdateGrip(kb);
        AnimateSquash();
    }

    private void UpdateGrip(Keyboard kb)
    {
        float dt = Time.deltaTime;
        bool running = kb != null && kb.leftShiftKey.isPressed &&
                       (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed);
        grip -= (running ? gripDrainRun : gripDrainIdle) * dt;

        bool jump = kb != null && kb.spaceKey.isPressed;
        if (jump && !jumpWasHeld) grip -= gripLossOnJump;
        jumpWasHeld = jump;

        // Hızlı dönmek tutuşu bozar.
        float yaw = holder.eulerAngles.y;
        float yawSpeed = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
        lastYaw = yaw;
        grip -= yawSpeed * gripLossOnTurn;

        if (status != null) status.ApplySlow(carrySpeedMultiplier, 0.2f);

        if (grip <= 0f)
        {
            Juice.Shake(0.03f, 0.2f);
            Drop(true);
        }
    }

    private void FixedUpdate()
    {
        if (!IsHeld) return;

        // Hedef noktası duvarın içine/arkasına geçmesin: kameradan hedefe küre taraması, çarparsa önünde dur.
        Vector3 camPos = holder.position;
        float dist = holdDistance;
        Vector3 fwd = holder.forward;
        var hits = Physics.SphereCastAll(camPos, 0.3f, fwd, holdDistance, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.attachedRigidbody == rb) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            // Duvara yakınken sandalye kameraya yaklaşır (asla duvarın içine itilmez).
            dist = Mathf.Min(dist, Mathf.Max(0.1f, h.distance));
        }
        // Duvara sıkıştı (sandalye kameranın içine girip görünmez olur): kısa süre sonra elinden kayar.
        squeezeTimer = dist < squeezeDistance ? squeezeTimer + Time.fixedDeltaTime : 0f;
        if (squeezeTimer > squeezeTime)
        {
            squeezeTimer = 0f;
            Juice.Hit(rb.position, -fwd);
            Juice.Shake(0.02f, 0.15f);
            Drop(true);
            rb.linearVelocity = -fwd * 1.2f + Vector3.up * 2.5f;
            return;
        }
        Vector3 target = camPos + fwd * dist + Vector3.up * holdHeight;
        Vector3 toTarget = target - rb.position;

        if (toTarget.magnitude > breakDistance)
        {
            // Bir şeye takıldı ve uzakta kaldı: tutuş kopar.
            Drop(true);
            return;
        }

        // Yay + damper; hız sınırlı => ağır ve gecikmeli hissi.
        Vector3 desiredVel = Vector3.ClampMagnitude(toTarget * spring * 0.1f, maxPullSpeed);
        Vector3 accel = (desiredVel - rb.linearVelocity) * damper;
        rb.AddForce(accel, ForceMode.Acceleration);
        // Zayıf tutuşta eşya yere doğru sarkar.
        rb.AddForce(Physics.gravity * (1f - grip) * 0.8f, ForceMode.Acceleration);

        // Yaw: kameraya doğru gecikmeli dönsün (geri kalınca savrulur).
        float targetYaw = holder.eulerAngles.y;
        float yawErr = Mathf.DeltaAngle(rb.rotation.eulerAngles.y, targetYaw);
        rb.AddTorque(Vector3.up * yawErr * turnLag * Mathf.Deg2Rad, ForceMode.Acceleration);

        // Dik durmaya çalış (ama ağırlığı hissettir).
        Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
        rb.AddTorque(axis * 6f - rb.angularVelocity * 0.5f, ForceMode.Acceleration);

        // Zayıf grip + koşma = sağa sola yalpalama (Perlin).
        float shaky = 1f - grip;
        float t = Time.time * 6f + noiseSeed;
        Vector3 wob = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f);
        rb.AddTorque(wob * wobbleTorque * (0.3f + shaky * 3f), ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision col)
    {
        if (!IsHeld) { if (col.relativeVelocity.magnitude > 2f) Squash(Mathf.Clamp01(col.relativeVelocity.magnitude / 8f)); return; }
        if (Time.time < lastBonkTime + bonkCooldown) return;

        float speed = col.relativeVelocity.magnitude;
        if (speed < bonkMinSpeed) return;

        // Yere sürtünmek BONK sayılmaz (sadece sert iniş).
        if (col.contactCount > 0 && col.GetContact(0).normal.y > 0.7f && speed < bonkDropSpeed) return;

        lastBonkTime = Time.time;
        Vector3 point = col.contactCount > 0 ? col.GetContact(0).point : transform.position;
        Squash(Mathf.Clamp01(speed / 8f));
        Juice.Hit(point, col.contactCount > 0 ? col.GetContact(0).normal : Vector3.up);
        Juice.Shake(0.01f * speed, 0.15f);

        grip -= speed * gripLossPerBonkSpeed;
        if (speed >= bonkDropSpeed || grip <= 0f)
        {
            // Elinden fırla: çarptığı yüzeyden geri sek, dön.
            Vector3 bounce = col.contactCount > 0 ? col.GetContact(0).normal : -holder.forward;
            Drop(true);
            rb.linearVelocity = bounce * speed * 0.8f + Vector3.up * 2.5f;
        }
    }

    // Cartoon ezilme/uzama: scale'e kısa darbe.
    private void Squash(float amount) => squash = Mathf.Max(squash, amount);

    private void AnimateSquash()
    {
        if (squash <= 0.001f) { if (transform.localScale != baseScale) transform.localScale = baseScale; return; }
        squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 2.5f);
        float wave = Mathf.Sin(squash * Mathf.PI * 3f) * squash * 0.25f;
        transform.localScale = new Vector3(baseScale.x * (1f + wave), baseScale.y * (1f - wave), baseScale.z * (1f + wave));
    }

    private void OnDisable()
    {
        if (IsHeld) Drop(false);
    }
}
