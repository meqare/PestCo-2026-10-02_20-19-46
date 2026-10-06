using UnityEngine;

// Sprey tabancasına sıkma hissi verir: tetikte kısa geri tepme, basılıyken hafif titreşim, bırakınca yumuşak dönüş.
public class SprayGunAnimator : MonoBehaviour
{
    [SerializeField] private SprayVisual sprayVisual;
    [Tooltip("Sıkmaya başlarken tek seferlik geri tepme (metre, geriye).")]
    [SerializeField, Min(0f)] private float kickDistance = 0.03f;
    [Tooltip("Sıkarken sürekli hafif geri durma (metre).")]
    [SerializeField, Min(0f)] private float holdRecoil = 0.012f;
    [Tooltip("Titreşim miktarı (metre).")]
    [SerializeField, Min(0f)] private float shakeAmount = 0.0025f;
    [Tooltip("Namlunun kalkma açısı (derece).")]
    [SerializeField, Min(0f)] private float pitchKick = 3f;
    [SerializeField, Min(1f)] private float returnSpeed = 14f;

    private Vector3 basePos;
    private Quaternion baseRot;
    private float kick;
    private bool wasSpraying;

    private void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        if (sprayVisual == null)
            sprayVisual = GetComponentInParent<SprayVisual>();
    }

    private void OnDisable()
    {
        transform.localPosition = basePos;
        transform.localRotation = baseRot;
        kick = 0f;
        wasSpraying = false;
    }

    private void Update()
    {
        bool spraying = sprayVisual != null && sprayVisual.IsSpraying;
        if (spraying && !wasSpraying)
            kick = 1f;
        wasSpraying = spraying;

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
    }
}
