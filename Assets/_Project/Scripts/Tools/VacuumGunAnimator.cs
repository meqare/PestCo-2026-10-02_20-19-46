using UnityEngine;

// Vakuma çizgi film canlılığı verir (SprayGunAnimator ile aynı yaklaşım):
//  - çalışmaya başlarken geri tepme + squash & stretch yayı, bırakınca ileri sallanma,
//  - çalışırken titreşim, hortum boynunun nefes alır gibi uzayıp kısalması, toz haznesinin zıplaması.
// Sadece görseli oynatır; VacuumTool'a dokunmaz. Bileşen kapatılırsa model sabit kalır.
public class VacuumGunAnimator : MonoBehaviour
{
    [SerializeField] private VacuumTool vacuum;
    [Tooltip("Oluklu hortum boynu (z'de uzayıp kısalır).")]
    [SerializeField] private Transform neck;
    [Tooltip("Şeffaf toz haznesi (çalışırken zıplar/sallanır).")]
    [SerializeField] private Transform bin;

    [Header("Geri tepme")]
    [SerializeField, Min(0f)] private float kickDistance = 0.04f;
    [SerializeField, Min(0f)] private float holdRecoil = 0.01f;
    [SerializeField, Min(0f)] private float shakeAmount = 0.004f;
    [SerializeField, Min(0f)] private float pitchKick = 4f;
    [SerializeField, Min(1f)] private float returnSpeed = 14f;

    [Header("Çizgi film yayı")]
    [SerializeField, Min(0f)] private float squashAmount = 0.22f;
    [SerializeField, Min(1f)] private float springStiffness = 260f;
    [SerializeField, Min(0.1f)] private float springDamping = 10f;
    [SerializeField, Min(0f)] private float startImpulse = 9f;
    [SerializeField, Min(0f)] private float endImpulse = 7f;
    [SerializeField, Range(0f, 1f)] private float endLurch = 0.7f;

    [Header("Çalışırken")]
    [Tooltip("Hortum boynunun nefes alma miktarı (0.15 = %15).")]
    [SerializeField, Range(0f, 0.5f)] private float neckPulse = 0.15f;
    [SerializeField, Min(0f)] private float neckPulseSpeed = 22f;
    [Tooltip("Toz haznesinin zıplama açısı (derece).")]
    [SerializeField, Min(0f)] private float binWobble = 6f;

    private Vector3 basePos, baseScale, neckBaseScale;
    private Quaternion baseRot, binBaseRot;
    private float kick, spring, springVel;
    private bool wasRunning;
    private float runBlend;

    private void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        baseScale = transform.localScale;
        if (neck != null) neckBaseScale = neck.localScale;
        if (bin != null) binBaseRot = bin.localRotation;
        if (vacuum == null) vacuum = GetComponentInParent<VacuumTool>();
    }

    private void OnDisable()
    {
        transform.localPosition = basePos;
        transform.localRotation = baseRot;
        transform.localScale = baseScale;
        if (neck != null) neck.localScale = neckBaseScale;
        if (bin != null) bin.localRotation = binBaseRot;
        kick = spring = springVel = runBlend = 0f;
        wasRunning = false;
    }

    private void Update()
    {
        bool running = vacuum != null && vacuum.IsRunning;
        if (running && !wasRunning) { kick = 1f; springVel += startImpulse; }
        else if (!running && wasRunning) { kick = -endLurch; springVel -= endImpulse; }
        wasRunning = running;

        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        springVel += (-springStiffness * spring - springDamping * springVel) * dt;
        spring += springVel * dt;
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-9f * Time.deltaTime));
        runBlend = Mathf.MoveTowards(runBlend, running ? 1f : 0f, Time.deltaTime * 8f);

        float back = kick * kickDistance + holdRecoil * runBlend;
        float pitch = kick * pitchKick + pitchKick * 0.3f * runBlend;

        float t = Time.time * 30f;
        Vector3 shake = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (shakeAmount * 2f * runBlend);

        float k = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
        transform.localPosition = Vector3.Lerp(transform.localPosition, basePos + shake + Vector3.back * back, k);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(-pitch, 0f, 0f) * baseRot, k);

        float s = spring * squashAmount;
        transform.localScale = new Vector3(baseScale.x * (1f + s * 0.5f), baseScale.y * (1f + s * 0.5f), baseScale.z * (1f - s));

        if (neck != null)
        {
            float pulse = Mathf.Sin(Time.time * neckPulseSpeed) * neckPulse * runBlend;
            neck.localScale = new Vector3(neckBaseScale.x * (1f - pulse * 0.5f), neckBaseScale.y * (1f - pulse * 0.5f), neckBaseScale.z * (1f + pulse));
        }
        if (bin != null)
        {
            float w = Mathf.Sin(Time.time * 17f) * binWobble * runBlend;
            bin.localRotation = binBaseRot * Quaternion.Euler(w * 0.5f, 0f, w);
        }
    }
}
