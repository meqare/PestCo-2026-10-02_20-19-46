using UnityEngine;

// Sprey tabancasına çizgi film tarzı sıkma hissi verir:
//  - ilk basışta kuvvetli geri tepme + "squash & stretch" (yay gibi sıkışıp uzama),
//  - basılıyken hafif titreşim,
//  - bırakınca ileri doğru bir sallanma ve yumuşak yaylanarak yerine dönüş.
public class SprayGunAnimator : MonoBehaviour
{
    [SerializeField] private SprayVisual sprayVisual;
    [Tooltip("Sıkmaya başlarken tek seferlik geri tepme (metre, geriye).")]
    [SerializeField, Min(0f)] private float kickDistance = 0.045f;
    [Tooltip("Sıkarken sürekli hafif geri durma (metre).")]
    [SerializeField, Min(0f)] private float holdRecoil = 0.012f;
    [Tooltip("Titreşim miktarı (metre).")]
    [SerializeField, Min(0f)] private float shakeAmount = 0.0025f;
    [Tooltip("Namlunun kalkma açısı (derece).")]
    [SerializeField, Min(0f)] private float pitchKick = 5f;
    [SerializeField, Min(1f)] private float returnSpeed = 14f;

    [Header("Çizgi film yayı")]
    [Tooltip("Sıkışma/uzama (squash & stretch) miktarı.")]
    [SerializeField, Min(0f)] private float squashAmount = 0.22f;
    [Tooltip("Yay sertliği: yüksek = hızlı titreşir.")]
    [SerializeField, Min(1f)] private float springStiffness = 260f;
    [Tooltip("Sönüm: düşük = daha çok yaylanır.")]
    [SerializeField, Min(0.1f)] private float springDamping = 11f;
    [Tooltip("Başlangıçtaki yay itme gücü.")]
    [SerializeField, Min(0f)] private float startImpulse = 9f;
    [Tooltip("Bırakınca ters yöndeki yay itme gücü.")]
    [SerializeField, Min(0f)] private float endImpulse = 7f;
    [Tooltip("Bırakınca ileri doğru sallanma (geri tepmenin ters yönü).")]
    [SerializeField, Range(0f, 1f)] private float endLurch = 0.7f;

    private Vector3 basePos;
    private Quaternion baseRot;
    private Vector3 baseScale;
    private float kick;
    private bool wasSpraying;
    private float spring;      // squash & stretch ofseti
    private float springVel;

    private void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        baseScale = transform.localScale;
        if (sprayVisual == null)
            sprayVisual = GetComponentInParent<SprayVisual>();
    }

    private void OnDisable()
    {
        transform.localPosition = basePos;
        transform.localRotation = baseRot;
        transform.localScale = baseScale;
        kick = 0f;
        spring = springVel = 0f;
        wasSpraying = false;
    }

    private void Update()
    {
        bool spraying = sprayVisual != null && sprayVisual.IsSpraying;
        if (spraying && !wasSpraying)
        {
            kick = 1f;
            springVel += startImpulse;     // önce sıkış, sonra uza
        }
        else if (!spraying && wasSpraying)
        {
            kick = -endLurch;              // ileri doğru hafif sallanma
            springVel -= endImpulse;       // ters yönde yaylan
        }
        wasSpraying = spraying;

        // Yay: x'' = -k x - c x'
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        springVel += (-springStiffness * spring - springDamping * springVel) * dt;
        spring += springVel * dt;

        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-9f * Time.deltaTime));

        float back = kick * kickDistance + (spraying ? holdRecoil : 0f);
        float pitch = kick * pitchKick + (spraying ? pitchKick * 0.3f : 0f);

        Vector3 shake = Vector3.zero;
        if (spraying)
        {
            float t = Time.time * 28f;
            shake = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (shakeAmount * 2f);
        }

        // Model +Z'ye bakıyor: geri = -Z, namlu kalkması = -X ekseninde dönüş.
        Vector3 targetPos = basePos + shake + Vector3.back * back;
        Quaternion targetRot = Quaternion.Euler(-pitch, 0f, 0f) * baseRot;

        float k = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, k);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, k);

        // Hacmi koruyan squash & stretch: pozitif = namlu yönünde sıkış, yanlarda şiş.
        float s = spring * squashAmount;
        transform.localScale = new Vector3(baseScale.x * (1f + s * 0.5f), baseScale.y * (1f + s * 0.5f), baseScale.z * (1f - s));
    }
}
