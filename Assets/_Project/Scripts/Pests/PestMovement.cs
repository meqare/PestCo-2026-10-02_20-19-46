using UnityEngine;
using UnityEngine.InputSystem;

// Oynanabilir "Anomali Haşere": WASD yürüme, Shift koşu, abartılı çizgi film zıplaması ve duvara yapışıp tırmanma.
// Hareket kameraya göre yapılır (PestCamera.transform). CharacterController kullanır (projedeki PlayerController ile aynı yapı).
[RequireComponent(typeof(CharacterController))]
public class PestMovement : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Hareket yönünü belirleyen kamera. Boşsa Camera.main kullanılır.")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Dönen görsel model. Boşsa bu nesnenin kendisi döner.")]
    [SerializeField] private Transform visual;

    [Header("Yürüme / Koşma")]
    // Varsayılanlar ~10 cm'lik minik haşere içindir (insan ölçeği için hız x10, boyutlar x10 yapılmalı).
    [SerializeField] private float walkSpeed = 0.6f;
    [SerializeField] private float runSpeed = 1.8f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField, Min(1f)] private float turnSpeed = 14f;

    [Header("Zıplama (çizgi film)")]
    [SerializeField] private float jumpHeight = 0.45f;
    [SerializeField] private float gravity = -14f;
    [Tooltip("Düşerken yerçekimi çarpanı: hızlı iner, 'ağır' hissettirir.")]
    [SerializeField] private float fallMultiplier = 1.8f;
    [Tooltip("Zıplama tuşu bu kadar süre erken basılsa da kabul edilir.")]
    [SerializeField] private float jumpBuffer = 0.12f;
    [Tooltip("Zeminden ayrıldıktan sonra bu süre içinde hâlâ zıplanabilir.")]
    [SerializeField] private float coyoteTime = 0.1f;

    [Header("Çizgi Film: Havada Asılı Kalma")]
    [Tooltip("Kenardan yürüyerek çıkınca yerçekimi bu kadar süre uygulanmaz.")]
    [SerializeField] private float hangTime = 0.5f;
    [Tooltip("Havada asılıyken hız çarpanı (1.2 = %20 hızlı).")]
    [SerializeField] private float hangSpeedMultiplier = 1.2f;

    [Header("Çizgi Film: Göz Fırlaması")]
    [SerializeField] private Transform solGoz;
    [SerializeField] private Transform sagGoz;
    [Tooltip("Koşarken gözlerin yerel Z'de ileri fırlama miktarı.")]
    [SerializeField] private float eyePopDistance = 0.03f;
    [Tooltip("Fırlama hızı (düşük = yavaş uzar).")]
    [SerializeField] private float eyePopOutSpeed = 12f;
    [Tooltip("Geri dönüş hızı (yüksek = jet gibi geri çekilir).")]
    [SerializeField] private float eyePopReturnSpeed = 60f;

    [Header("Çizgi Film: Squash & Stretch")]
    [Tooltip("Zıplarken dikeyde uzama miktarı (0.4 = %40).")]
    [SerializeField] private float stretchAmount = 0.4f;
    [Tooltip("Sert düşünce yassılma miktarı (0.5 = %50).")]
    [SerializeField] private float squashAmount = 0.5f;
    [Tooltip("Bu düşme hızının altındaki inişlerde ezilme olmaz.")]
    [SerializeField] private float squashMinFallSpeed = 1.5f;
    [Tooltip("Hedef şekle geçiş hızı.")]
    [SerializeField] private float shapeSpeed = 18f;
    [Tooltip("Ezilmeden normale dönüş hızı (düşük = daha uzun 'boing').")]
    [SerializeField] private float squashRecoverSpeed = 9f;

    [Header("Duvar Tırmanma (prototip)")]
    [SerializeField] private bool allowClimbing = true;
    [Tooltip("Hangi katmanlar tırmanılabilir (dolap, tezgah, duvar).")]
    [SerializeField] private LayerMask climbableLayers = ~0;
    [SerializeField] private float climbSpeed = 0.5f;
    [SerializeField] private float wallCheckDistance = 0.12f;
    [SerializeField] private float wallCheckRadius = 0.03f;
    [Tooltip("Tepeye çıkınca ileri-yukarı kısa adım hızı (yukarı, ileri).")]
    [SerializeField] private Vector2 mantleSpeed = new Vector2(0.5f, 0.4f);
    [SerializeField] private float mantleTime = 0.3f;
    [Tooltip("Duvara bastırma kuvveti (yapışık kalmak için).")]
    [SerializeField] private float wallStickForce = 0.2f;
    [Tooltip("Yüzey normalinin Y bileşeni bundan küçükse 'dikey' sayılır.")]
    [SerializeField, Range(0f, 1f)] private float maxWallNormalY = 0.35f;
    [Tooltip("Duvardan zıplarken itme gücü.")]
    [SerializeField] private float wallJumpPush = 1.2f;

    private CharacterController controller;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;
    private Vector3 baseVisualScale = Vector3.one;
    private Vector3 baseVisualPos;
    private float shape;           // + = dikeyde uzama, - = ezilme
    private bool wasGrounded;
    private float hangTimer;
    private Vector3 solGozBase, sagGozBase;
    private bool eyesCached;

    private bool climbing;
    private Vector3 wallNormal;
    private float climbReleaseTime = -10f;

    // Görsel efektler (PestCartoonFX) için salt-okunur bilgi ve olaylar; oynanışı etkilemez.
    public event System.Action Jumped;
    public event System.Action<float> Landed;   // parametre: iniş hızı (m/s, pozitif)
    public bool IsSprinting { get; private set; }
    public float PlanarSpeed => planarVelocity.magnitude;

    public bool IsClimbing => climbing;
    public bool IsGrounded => controller != null && controller.isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (visual == null) visual = transform;
        baseVisualScale = visual.localScale;
        baseVisualPos = visual.localPosition;
        CacheEyes();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    private void OnDisable()
    {
        if (visual != null)
        {
            visual.localScale = baseVisualScale;
            if (visual != transform) visual.localPosition = baseVisualPos;
        }
        if (eyesCached)
        {
            if (solGoz != null) solGoz.localPosition = solGozBase;
            if (sagGoz != null) sagGoz.localPosition = sagGozBase;
        }
        shape = 0f;
    }

    // Göz nesnelerinin başlangıç yerel konumunu saklar (geri dönecekleri nokta).
    private void CacheEyes()
    {
        if (solGoz != null) solGozBase = solGoz.localPosition;
        if (sagGoz != null) sagGozBase = sagGoz.localPosition;
        eyesCached = true;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // İmleç kilitli değilse (menü/pause) girdi kabul etme.
        bool acceptInput = Cursor.lockState == CursorLockMode.Locked;

        Vector2 input = Vector2.zero;
        if (acceptInput)
        {
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);
        }
        bool run = acceptInput && kb.leftShiftKey.isPressed;
        bool jumpPressed = acceptInput && kb.spaceKey.wasPressedThisFrame;
        if (jumpPressed) lastJumpPressedTime = Time.time;

        if (climbing)
        {
            hangTimer = 0f;
            wasGrounded = false;
            UpdateClimb(input, jumpPressed);
        }
        else
        {
            UpdateGround(input, run);
            TryStartClimb(input);
        }

        bool sprinting = run && input.sqrMagnitude > 0.01f && !climbing;
        IsSprinting = sprinting;
        UpdateEyes(sprinting);
        UpdateShape();
    }

    // Normal yürüme/koşma + zıplama + yerçekimi.
    private void UpdateGround(Vector2 input, bool run)
    {
        bool grounded = controller.isGrounded;
        if (grounded)
        {
            // Yeni indiysek: sert düştüyse ez
            if (!wasGrounded)
            {
                Landed?.Invoke(-verticalVelocity);
                TriggerSquash(verticalVelocity);
            }
            hangTimer = 0f;
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }
        else if (wasGrounded && verticalVelocity <= 0.01f && !GroundNearby())
        {
            // Zıplamadan kenardan çıktı: havada asılı kal (yerçekimi bekler)
            hangTimer = hangTime;
            verticalVelocity = 0f;
        }
        wasGrounded = grounded;

        bool hanging = hangTimer > 0f;
        if (hanging) hangTimer -= Time.deltaTime;

        // Kameraya göre yön
        Vector3 fwd = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : transform.forward;
        Vector3 right = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : transform.right;
        Vector3 wish = (fwd * input.y + right * input.x);
        float speed = run ? runSpeed : walkSpeed;
        if (hanging) speed *= hangSpeedMultiplier;

        planarVelocity = Vector3.MoveTowards(planarVelocity, wish * speed, acceleration * Time.deltaTime);

        if (wish.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(wish, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        // Zıplama (buffer + coyote)
        bool canJump = hanging || Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBuffer;
        if (canJump && wantsJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = -10f;
            lastGroundedTime = -10f;
            hangTimer = 0f;
            hanging = false;
            Jumped?.Invoke();
        }

        // Asılı kalırken yerçekimi yok; süre bitince normal düşüş
        if (!hanging)
        {
            float g = gravity * (verticalVelocity < 0f ? fallMultiplier : 1f);
            verticalVelocity += g * Time.deltaTime;
        }

        Vector3 move = planarVelocity;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    // Havadayken veya yerdeyken duvara doğru ilerleniyorsa yapış.
    private void TryStartClimb(Vector2 input)
    {
        if (!allowClimbing || Time.time - climbReleaseTime < 0.3f) return;
        if (input.sqrMagnitude < 0.01f || planarVelocity.sqrMagnitude < 0.5f) return;

        Vector3 dir = planarVelocity.normalized;
        if (FindWall(dir, out RaycastHit hit))
        {
            climbing = true;
            wallNormal = hit.normal;
            verticalVelocity = 0f;
            planarVelocity = Vector3.zero;
        }
    }

    // Duvarda: W/S yukarı-aşağı, A/D yanlara. Space ile duvardan zıpla.
    private void UpdateClimb(Vector2 input, bool jumpPressed)
    {
        // Mevcut duvarı yeniden bul (köşelerde normal değişebilir)
        if (!FindWall(-wallNormal, out RaycastHit hit))
        {
            // Duvarın tepesine ulaştık: yukarı basıyorsak üstüne tırman, yoksa bırak.
            if (input.y > 0.1f) StartCoroutine(MantleRoutine());
            else StopClimb();
            return;
        }
        wallNormal = hit.normal;

        // Duvara dön
        Quaternion face = Quaternion.LookRotation(-wallNormal, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, face, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));

        if (jumpPressed)
        {
            Vector3 push = wallNormal * wallJumpPush;
            planarVelocity = new Vector3(push.x, 0f, push.z);
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity) * 0.8f;
            StopClimb();
            return;
        }

        Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;
        Vector3 move = (Vector3.up * input.y + wallRight * -input.x) * climbSpeed;
        move += -wallNormal * wallStickForce; // duvara bastır
        CollisionFlags flags = controller.Move(move * Time.deltaTime);

        // Aşağı inerken yere değdiyse bırak
        if (input.y < -0.1f && (flags & CollisionFlags.Below) != 0) StopClimb();
    }

    private System.Collections.IEnumerator MantleRoutine()
    {
        // Kısa bir ileri-yukarı hareketle tezgah/dolap üstüne çık.
        climbing = false;
        float t = 0f;
        Vector3 forward = -wallNormal;
        while (t < mantleTime)
        {
            t += Time.deltaTime;
            controller.Move((Vector3.up * mantleSpeed.x + forward * mantleSpeed.y) * Time.deltaTime);
            yield return null;
        }
        verticalVelocity = 0f;
        climbReleaseTime = Time.time;
    }

    private void StopClimb()
    {
        climbing = false;
        climbReleaseTime = Time.time;
    }

    // Gövde hizasından yöne SphereCast atar; dikey bir yüzeye çarparsa true.
    private bool FindWall(Vector3 dir, out RaycastHit hit)
    {
        Vector3 origin = transform.position + controller.center;
        if (Physics.SphereCast(origin, wallCheckRadius, dir, out hit, wallCheckDistance, climbableLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform)
            && Mathf.Abs(hit.normal.y) <= maxWallNormalY)
            return true;
        return false;
    }

    // Düzlükte bozuk zemin/halı kenarında sahte "havada asılma" olmasın: altta yakın zemin varsa asılma.
    private bool GroundNearby()
    {
        Vector3 origin = transform.position + controller.center;
        float dist = controller.center.y + controller.height * 0.5f + 0.06f;
        return Physics.Raycast(origin, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
    }

    // Sert düşüşte ezilme: düşme hızına göre bir anda yassılır, sonra Lerp ile geri esner.
    private void TriggerSquash(float fallVelocity)
    {
        float fall = -fallVelocity;
        if (fall < squashMinFallSpeed) return;
        float k = Mathf.Clamp01(fall / 8f);
        shape = -squashAmount * Mathf.Lerp(0.5f, 1f, k);
    }

    // Dikeyde uzama (zıplarken) / ezilme (inişte) şeklini Transform.localScale'e yazar.
    private void UpdateShape()
    {
        if (visual == null) return;

        // Hedef: yükselirken uzar, düşerken az uzar, yerde 0
        float target = 0f;
        if (!climbing && !controller.isGrounded)
        {
            float jumpVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (verticalVelocity > 0.05f) target = stretchAmount * Mathf.Clamp01(verticalVelocity / jumpVel);
            else if (verticalVelocity < -0.5f) target = stretchAmount * 0.5f * Mathf.Clamp01(-verticalVelocity / jumpVel);
        }

        // Ezilmeden dönüş yavaş (boing), diğer geçişler hızlı
        float speed = shape < 0f && target >= 0f ? squashRecoverSpeed : shapeSpeed;
        shape = Mathf.Lerp(shape, target, 1f - Mathf.Exp(-speed * Time.deltaTime));

        // Hacim yaklaşık korunur: Y artarken X-Z azalır.
        float y = 1f + shape;
        float xz = 1f - shape * 0.5f;
        visual.localScale = new Vector3(baseVisualScale.x * xz, baseVisualScale.y * y, baseVisualScale.z * xz);

        // Orta pivotlu modellerde ezilirken zeminde kalsın (ayak pivotlu modelde baseY=0, kayma olmaz).
        if (visual != transform)
        {
            Vector3 p = baseVisualPos;
            p.y = baseVisualPos.y * y;
            visual.localPosition = p;
        }
    }

    // Koşarken gözler yerel Z'de ileri fırlar, Shift bırakılınca jet hızıyla geri döner.
    private void UpdateEyes(bool sprinting)
    {
        if (!eyesCached) CacheEyes();
        float speed = sprinting ? eyePopOutSpeed : eyePopReturnSpeed;
        MoveEye(solGoz, solGozBase, sprinting, speed);
        MoveEye(sagGoz, sagGozBase, sprinting, speed);
    }

    private void MoveEye(Transform eye, Vector3 basePos, bool pop, float speed)
    {
        if (eye == null) return;
        Vector3 target = basePos + (pop ? Vector3.forward * eyePopDistance : Vector3.zero);
        eye.localPosition = Vector3.MoveTowards(eye.localPosition, target, speed * eyePopDistance * Time.deltaTime);
    }
}
