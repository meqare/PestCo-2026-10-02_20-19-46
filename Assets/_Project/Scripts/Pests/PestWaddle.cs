using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Yapay zekâlı (NavMeshAgent'lı) haşereye çizgi film yürüyüşü verir: koşarken sekme, yana sallanma,
/// öne eğilme ve ezilip uzama (squash &amp; stretch). Sadece görseli (modeli) oynatır, hareket mantığına dokunmaz.
/// </summary>
[DisallowMultipleComponent]
public class PestWaddle : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Oynatılacak görsel (model). Boşsa 'RoachModel' adlı çocuk aranır.")]
    [SerializeField] private Transform _visual;

    [Header("Sekme")]
    [Tooltip("Tam hızda zıplama yüksekliği (metre).")]
    [SerializeField, Min(0f)] private float _bobHeight = 0.03f;
    [Tooltip("Koşu hızıyla çarpılan adım frekansı.")]
    [SerializeField, Min(0.1f)] private float _stepsPerMeter = 4f;

    [Header("Salınım")]
    [Tooltip("Yana yatma derecesi (tam hızda).")]
    [SerializeField, Min(0f)] private float _swayDegrees = 9f;
    [Tooltip("Öne eğilme derecesi (tam hızda).")]
    [SerializeField, Min(0f)] private float _leanDegrees = 14f;

    [Header("Squash & Stretch")]
    [SerializeField, Range(0f, 0.4f)] private float _squashAmount = 0.12f;

    [Header("Hız")]
    [Tooltip("Bu hızda (m/s) efektler tam güçte olur.")]
    [SerializeField, Min(0.1f)] private float _fullEffectSpeed = 2.5f;
    [Tooltip("Efektin hıza yumuşakça uyma süresi (sn).")]
    [SerializeField, Min(0.01f)] private float _smoothTime = 0.12f;

    /// <summary>Başka bileşenlerin (çizgi film tepkileri) yürüyüşün üstüne ekleyeceği konum sapması (yerel).</summary>
    public Vector3 ExtraOffset { get; set; }

    /// <summary>Yürüyüşün üstüne eklenen dönüş (örn. şok sırasında geri yaslanma).</summary>
    public Quaternion ExtraRotation { get; set; } = Quaternion.identity;

    /// <summary>Yürüyüşün üstüne çarpılan ölçek (örn. şaşkınlıkla uzama).</summary>
    public Vector3 ExtraScale { get; set; } = Vector3.one;

    private NavMeshAgent _agent;
    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;
    private Vector3 _baseLocalScale;
    private float _phase;
    private float _intensity;
    private float _intensityVelocity;

    /// <summary>Görseli bulur ve başlangıç duruşunu saklar.</summary>
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_visual == null) _visual = transform.Find("RoachModel");
        if (_visual == null) return;

        _baseLocalPosition = _visual.localPosition;
        _baseLocalRotation = _visual.localRotation;
        _baseLocalScale = _visual.localScale;
        _phase = Random.value * Mathf.PI * 2f;
    }

    /// <summary>Havuzdan geri gelince efekt sıfırdan başlar (eski eğim kalmasın).</summary>
    private void OnEnable()
    {
        _intensity = 0f;
        _intensityVelocity = 0f;
        ExtraOffset = Vector3.zero;
        ExtraRotation = Quaternion.identity;
        ExtraScale = Vector3.one;
        ApplyRest();
    }

    /// <summary>Ajanın hızına göre sekme, salınım ve ezilmeyi uygular (animasyon sonrası, LateUpdate).</summary>
    private void LateUpdate()
    {
        if (_visual == null || _agent == null) return;

        float speed = _agent.enabled && _agent.isOnNavMesh ? _agent.velocity.magnitude : 0f;
        float target = Mathf.Clamp01(speed / _fullEffectSpeed);
        _intensity = Mathf.SmoothDamp(_intensity, target, ref _intensityVelocity, _smoothTime);

        // Durağan haldeyken hafif nefes alma; koşarken adım frekansı hızla artar.
        _phase += (speed * _stepsPerMeter + 1.2f) * Time.deltaTime * Mathf.PI;
        float bounce = Mathf.Abs(Mathf.Sin(_phase));
        float side = Mathf.Sin(_phase);

        Vector3 position = _baseLocalPosition + Vector3.up * (bounce * _bobHeight * _intensity);
        Quaternion tilt = Quaternion.Euler(_leanDegrees * _intensity, 0f, side * _swayDegrees * _intensity);

        // Yerdeyken (bounce düşük) ezilir, havada uzar; hacim korunur.
        float stretch = (bounce - 0.5f) * 2f * _squashAmount * _intensity;
        float y = 1f + stretch;
        float xz = 1f / Mathf.Sqrt(Mathf.Max(0.05f, y));

        _visual.localPosition = position + ExtraOffset;
        // Eğim ebeveyn eksenlerinde uygulanır (x = sağ, z = ileri); model FBX'ten gelen yerel dönüşünü korur.
        _visual.localRotation = tilt * ExtraRotation * _baseLocalRotation;
        _visual.localScale = Vector3.Scale(
            new Vector3(_baseLocalScale.x * xz, _baseLocalScale.y * y, _baseLocalScale.z * xz), ExtraScale);
    }

    /// <summary>Görseli başlangıç duruşuna geri koyar.</summary>
    private void ApplyRest()
    {
        if (_visual == null) return;

        _visual.localPosition = _baseLocalPosition;
        _visual.localRotation = _baseLocalRotation;
        _visual.localScale = _baseLocalScale;
    }
}
