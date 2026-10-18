using UnityEngine;

// Hacmi koruyan squash & stretch: zıplamadan önce çömelme (anticipation), havada uzama, inişte ezilme, sonra overshoot.
// Ölçek 'Pivot' (zemin hizalı) üzerinde animasyonun üstüne çarpılır; kapatınca ölçek tekrar klibe kalır.
// Başka scriptler (Hit/Death) Squash(...) ile de tetikleyebilir.
[DisallowMultipleComponent]
public class PestSquashStretch : MonoBehaviour
{
    [Tooltip("Ölçeklenecek obje (zemin hizalı Pivot). Boşsa 'Pivot' aranır.")]
    [SerializeField] private Transform target;
    [SerializeField] private PestMovement movement;

    [Header("Yay")]
    [SerializeField, Min(1f)] private float stiffness = 220f;
    [SerializeField, Min(0f)] private float damping = 11f;
    [SerializeField, Range(0f, 0.6f)] private float maxSquash = 0.35f;
    [SerializeField, Range(0f, 0.6f)] private float maxStretch = 0.30f;

    [Header("Zıplama / iniş")]
    [Tooltip("Zıplama anında hemen uygulanan çömelme (anticipation) - eksi = ezilme.")]
    [SerializeField] private float jumpAnticipation = -0.22f;
    [Tooltip("Çömelmeden kaç sn sonra uzama (stretch) gelsin.")]
    [SerializeField, Min(0f)] private float stretchDelay = 0.07f;
    [SerializeField] private float jumpStretch = 0.26f;
    [Tooltip("İniş hızının 1 m/s'si kaç ezilme verir.")]
    [SerializeField] private float landSquashPerSpeed = 0.06f;

    // s: ezilme(-)/uzama(+) miktarı. Y = 1+s, XZ = 1/sqrt(1+s) -> hacim sabit.
    private float s, sv, goal;
    private float pendingStretchAt = -1f, pendingStretch;

    private void Awake()
    {
        if (movement == null) movement = GetComponentInParent<PestMovement>();
        if (target == null)
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Pivot") { target = t; break; }
    }

    private void OnEnable()
    {
        if (movement != null) { movement.Jumped += OnJumped; movement.Landed += OnLanded; }
    }

    private void OnDisable()
    {
        if (movement != null) { movement.Jumped -= OnJumped; movement.Landed -= OnLanded; }
        s = sv = goal = 0f; pendingStretchAt = -1f;
    }

    private void OnJumped()
    {
        Squash(jumpAnticipation);
        pendingStretchAt = Time.time + stretchDelay; pendingStretch = jumpStretch;
    }

    private void OnLanded(float speed) => Squash(-Mathf.Min(maxSquash, speed * landSquashPerSpeed));

    // Dışarıdan çağır: amount < 0 ezilme, > 0 uzama. Ani itme verir, yay overshoot ile geri oturtur.
    public void Squash(float amount)
    {
        float a = Mathf.Clamp(amount, -maxSquash, maxStretch);
        s = Mathf.Clamp(a * 0.6f, -maxSquash, maxStretch);
        sv += a * 14f;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (pendingStretchAt >= 0f && Time.time >= pendingStretchAt) { Squash(pendingStretch); pendingStretchAt = -1f; }

        float acc = stiffness * (goal - s) - damping * sv;
        sv += acc * dt;
        s = Mathf.Clamp(s + sv * dt, -maxSquash, maxStretch);

        float y = 1f + s;
        float xz = 1f / Mathf.Sqrt(Mathf.Max(0.05f, y));
        Vector3 anim = target.localScale; // Animator'ın bu karedeki değeri
        target.localScale = new Vector3(anim.x * xz, anim.y * y, anim.z * xz);
    }
}
