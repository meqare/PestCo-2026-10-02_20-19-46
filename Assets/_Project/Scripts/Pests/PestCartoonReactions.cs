using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Haşerenin çizgi film tepkileri: oyuncuyu görünce "!" işareti, şaşkınlıkla zıplayıp uzama, geri yaslanıp
/// ezilerek kaçışa hazırlanma (anticipation), toz bulutuyla fırlama, koşarken toz izi, son anda yana sıçrarken
/// yatma ve saklanırken/çıkarken "puf". Sadece görseli oynatır, yapay zekâ mantığına dokunmaz.
/// </summary>
[RequireComponent(typeof(Pest))]
[DisallowMultipleComponent]
public class PestCartoonReactions : MonoBehaviour
{
    [Header("Ünlem İşareti")]
    [SerializeField, Min(0.05f)] private float _alertMarkHeight = 0.55f;
    [SerializeField, Min(0.5f)] private float _alertMarkFontSize = 3.2f;
    [SerializeField] private Color _alertMarkColor = new Color(1f, 0.15f, 0.1f);
    [Tooltip("İşaretin ekranda kalma süresi (sn).")]
    [SerializeField, Min(0.1f)] private float _alertMarkDuration = 0.9f;

    [Header("Şok ve Hazırlık")]
    [Tooltip("Şaşkınlıkla havaya sıçrama yüksekliği (metre).")]
    [SerializeField, Min(0f)] private float _surpriseHop = 0.14f;
    [Tooltip("Şaşkınlıkta dikeyde uzama oranı.")]
    [SerializeField, Range(0f, 0.6f)] private float _surpriseStretch = 0.3f;
    [Tooltip("Hazırlıkta geri yaslanma açısı (derece).")]
    [SerializeField, Range(0f, 45f)] private float _windupLeanBack = 20f;
    [Tooltip("Hazırlıkta ezilme oranı.")]
    [SerializeField, Range(0f, 0.5f)] private float _windupSquash = 0.22f;

    [Header("Sıçrama (Dodge)")]
    [SerializeField, Range(0f, 60f)] private float _dodgeRollDegrees = 30f;
    [SerializeField, Min(0.05f)] private float _dodgeDuration = 0.35f;

    [Header("Toz")]
    [Tooltip("Toz bulutu malzemesi (boşsa basit bir yedek üretilir).")]
    [SerializeField] private Material _dustMaterial;
    [Tooltip("Bu hızın (m/s) üstünde koşarken toz bırakır.")]
    [SerializeField, Min(0.5f)] private float _dustTrailSpeed = 2.4f;
    [SerializeField, Min(1)] private int _puffCount = 12;

    private Pest _pest;
    private PestWaddle _waddle;
    private NavMeshAgent _agent;
    private Transform _alertMark;
    private TextMeshPro _alertText;
    private ParticleSystem _dust;
    private Camera _camera;
    private Coroutine _poseRoutine;
    private Coroutine _markRoutine;
    private bool _isConcealed;

    /// <summary>Bileşenleri bulur.</summary>
    private void Awake()
    {
        _pest = GetComponent<Pest>();
        _waddle = GetComponent<PestWaddle>();
        _agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>Haşere olaylarına abone olur.</summary>
    private void OnEnable()
    {
        _pest.Alerted += OnAlerted;
        _pest.Dodged += OnDodged;
        _pest.ConcealedChanged += OnConcealedChanged;
        _isConcealed = false;
    }

    /// <summary>Aboneliği bırakır, devam eden efektleri temizler (havuza dönerken kalıntı kalmasın).</summary>
    private void OnDisable()
    {
        _pest.Alerted -= OnAlerted;
        _pest.Dodged -= OnDodged;
        _pest.ConcealedChanged -= OnConcealedChanged;

        StopAllCoroutines();
        _poseRoutine = null;
        _markRoutine = null;
        ResetPose();
        if (_alertMark != null) _alertMark.gameObject.SetActive(false);
    }

    /// <summary>Koşarken toz izini hıza göre açar/kapatır; "!" işaretini kameraya çevirir.</summary>
    private void LateUpdate()
    {
        if (_dust != null)
        {
            var emission = _dust.emission;
            emission.enabled = !_isConcealed && _agent.enabled && _agent.isOnNavMesh
                               && _agent.velocity.magnitude > _dustTrailSpeed;
        }

        if (_alertMark != null && _alertMark.gameObject.activeSelf)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _alertMark.rotation = _camera.transform.rotation;
        }
    }

    // ---------- Olaylar ----------

    /// <summary>Oyuncu fark edildi: "!" çıkar, şaşkınlık + hazırlık animasyonu oynar, sonunda toz bulutuyla fırlar.</summary>
    private void OnAlerted(float duration)
    {
        if (!isActiveAndEnabled) return;

        ShowAlertMark();
        RestartPose(WindUpRoutine(Mathf.Max(0.1f, duration)));
    }

    /// <summary>Son anda yana sıçradı: sıçrama yönüne yatar, uzar ve toz bırakır.</summary>
    private void OnDodged(Vector3 direction)
    {
        if (!isActiveAndEnabled) return;

        float side = Vector3.Dot(direction, transform.right) >= 0f ? 1f : -1f;
        Puff(_puffCount / 2);
        RestartPose(DodgeRoutine(side));
    }

    /// <summary>Gizlenirken ya da çıkarken "puf" bulutu; çıkışta küçük bir zıplama.</summary>
    private void OnConcealedChanged(bool concealed)
    {
        _isConcealed = concealed;
        if (!isActiveAndEnabled) return;

        Puff(_puffCount + 4);
        if (!concealed) RestartPose(PopOutRoutine());
    }

    // ---------- Animasyonlar ----------

    /// <summary>
    /// Şok: ilk yarıda şaşkınlıkla uzayıp zıplar; ikinci yarıda geri yaslanıp ezilir (kaçmadan önce yay gibi kurulur).
    /// Bitince toz bulutuyla fırlar.
    /// </summary>
    private IEnumerator WindUpRoutine(float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            if (k < 0.5f)
            {
                float u = Mathf.Sin(k / 0.5f * Mathf.PI);       // 0 -> 1 -> 0
                SetPose(Vector3.up * (_surpriseHop * u), 0f, _surpriseStretch * u);
            }
            else
            {
                float u = Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f);
                SetPose(Vector3.zero, -_windupLeanBack * u, -_windupSquash * u);
            }
            yield return null;
        }

        // Fırlama: toz bulutu + uzayarak çıkış.
        Puff(_puffCount);
        yield return Spring(0.25f, 0.35f);
        ResetPose();
        _poseRoutine = null;
    }

    /// <summary>Sıçrama: yana yatar, hafifçe uzar, sonra yaylanarak toparlanır.</summary>
    private IEnumerator DodgeRoutine(float side)
    {
        for (float t = 0f; t < _dodgeDuration; t += Time.deltaTime)
        {
            float u = Mathf.Sin(t / _dodgeDuration * Mathf.PI);
            float roll = -side * _dodgeRollDegrees * u;
            SetPose(Vector3.zero, 0f, 0.2f * u, roll);
            yield return null;
        }
        ResetPose();
        _poseRoutine = null;
    }

    /// <summary>Saklandığı yerden çıkarken küçükten büyüyerek zıplar (pop).</summary>
    private IEnumerator PopOutRoutine()
    {
        const float duration = 0.3f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float scale = k < 0.6f ? Mathf.Lerp(0.2f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
            SetUniformScale(scale);
            yield return null;
        }
        ResetPose();
        _poseRoutine = null;
    }

    /// <summary>Kısa bir süre (yay gibi) uzayıp normale döner: fırlama hissi verir.</summary>
    private IEnumerator Spring(float duration, float stretch)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float u = Mathf.Sin(t / duration * Mathf.PI) * Mathf.Exp(-3f * t / duration);
            SetPose(Vector3.zero, _windupLeanBack * 0.3f * u, stretch * u);
            yield return null;
        }
    }

    // ---------- Poz yardımcıları ----------

    /// <summary>Önceki pozu bırakıp yenisini başlatır.</summary>
    private void RestartPose(IEnumerator routine)
    {
        if (_poseRoutine != null) StopCoroutine(_poseRoutine);
        _poseRoutine = StartCoroutine(routine);
    }

    /// <summary>Konum sapması, öne/arkaya eğim (derece), dikey ezilme/uzama (hacim korunur) ve yan yatma uygular.</summary>
    private void SetPose(Vector3 offset, float pitchDegrees, float stretch, float rollDegrees = 0f)
    {
        if (_waddle == null) return;

        float y = 1f + stretch;
        float xz = 1f / Mathf.Sqrt(Mathf.Max(0.05f, y));
        _waddle.ExtraOffset = offset;
        _waddle.ExtraRotation = Quaternion.Euler(pitchDegrees, 0f, rollDegrees);
        _waddle.ExtraScale = new Vector3(xz, y, xz);
    }

    /// <summary>Her eksende aynı ölçek uygular (pop için).</summary>
    private void SetUniformScale(float scale)
    {
        if (_waddle == null) return;

        _waddle.ExtraOffset = Vector3.zero;
        _waddle.ExtraRotation = Quaternion.identity;
        _waddle.ExtraScale = Vector3.one * scale;
    }

    /// <summary>Ek pozu sıfırlar (yürüyüş animasyonu normal devam eder).</summary>
    private void ResetPose()
    {
        if (_waddle == null) return;

        _waddle.ExtraOffset = Vector3.zero;
        _waddle.ExtraRotation = Quaternion.identity;
        _waddle.ExtraScale = Vector3.one;
    }

    // ---------- Ünlem ----------

    /// <summary>"!" işaretini oluşturur (ilk seferde) ve büyüyerek (overshoot) gösterir.</summary>
    private void ShowAlertMark()
    {
        if (_alertMark == null) CreateAlertMark();

        _alertMark.gameObject.SetActive(true);
        if (_markRoutine != null) StopCoroutine(_markRoutine);
        _markRoutine = StartCoroutine(AlertMarkRoutine());
    }

    /// <summary>İşareti 0 → 1.5 → 1 ölçeğinde zıplatır, bekler ve gizler.</summary>
    private IEnumerator AlertMarkRoutine()
    {
        const float popTime = 0.2f;
        for (float t = 0f; t < popTime; t += Time.deltaTime)
        {
            float k = t / popTime;
            float scale = k < 0.6f ? Mathf.Lerp(0f, 1.5f, k / 0.6f) : Mathf.Lerp(1.5f, 1f, (k - 0.6f) / 0.4f);
            _alertMark.localScale = Vector3.one * scale;
            yield return null;
        }
        _alertMark.localScale = Vector3.one;

        yield return new WaitForSeconds(Mathf.Max(0f, _alertMarkDuration - popTime));
        _alertMark.gameObject.SetActive(false);
        _markRoutine = null;
    }

    /// <summary>Başın üstünde kırmızı kalın bir "!" (3B yazı) kurar.</summary>
    private void CreateAlertMark()
    {
        var go = new GameObject("AlertMark");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * _alertMarkHeight;

        _alertText = go.AddComponent<TextMeshPro>();
        _alertText.text = "!";
        _alertText.fontSize = _alertMarkFontSize;
        _alertText.fontStyle = FontStyles.Bold;
        _alertText.alignment = TextAlignmentOptions.Center;
        _alertText.color = _alertMarkColor;
        _alertText.rectTransform.sizeDelta = new Vector2(1f, 1f);
        _alertText.outlineWidth = 0.25f;
        _alertText.outlineColor = Color.white;
        _alertText.raycastTarget = false;

        _alertMark = go.transform;
    }

    // ---------- Toz ----------

    /// <summary>Ayak hizasında rastgele yönlere dağılan toz bulutu (puf) bırakır.</summary>
    private void Puff(int count)
    {
        EnsureDust();

        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            emit.position = transform.position + new Vector3(dir.x, 0.05f, dir.y) * Random.Range(0.02f, 0.1f);
            emit.velocity = new Vector3(dir.x, Random.Range(0.1f, 0.4f), dir.y) * Random.Range(0.4f, 1.1f);
            emit.startSize = Random.Range(0.12f, 0.26f);
            emit.startLifetime = Random.Range(0.35f, 0.6f);
            _dust.Emit(emit, 1);
        }
    }

    /// <summary>Toz parçacık sistemini (ilk kullanımda) oluşturur: koşarken mesafeye göre iz bırakır.</summary>
    private void EnsureDust()
    {
        if (_dust != null) return;

        var go = new GameObject("Dust");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.03f;

        _dust = go.AddComponent<ParticleSystem>();
        _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _dust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.45f;
        main.startSpeed = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = new Color(0.93f, 0.88f, 0.78f, 0.75f);
        main.gravityModifier = -0.05f;
        main.maxParticles = 80;

        var emission = _dust.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 8f;
        emission.enabled = false;

        var shape = _dust.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.04f;

        var colorOverLifetime = _dust.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = _dust.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));

        var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = _dustMaterial != null ? _dustMaterial : CreateFallbackMaterial();
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        _dust.Play();
    }

    /// <summary>Malzeme atanmadıysa URP parçacık shader'ıyla basit bir toz malzemesi üretir.</summary>
    private static Material CreateFallbackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        return new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
    }
}
