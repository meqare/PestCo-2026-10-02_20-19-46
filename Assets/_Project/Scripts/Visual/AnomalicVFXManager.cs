using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

/// <summary>
/// "Kontrast korku" yöneticisi: bir böcek Horror moda geçince ev anında soluk, mor ve glitch'li bir hale bürünür
/// ve kamera kısa bir kalp atışı sarsıntısı yaşar.
/// Mevcut Volume'lara dokunmaz: kendi (yüksek öncelikli) global Volume'unu kurar ve ağırlığını 0-1 arasında kaydırır.
/// Horror böceklerin hepsi yakalanınca ya da satış yapılınca <see cref="TriggerAnomalicVFX"/>(false) ile normale döner.
/// </summary>
public class AnomalicVFXManager : MonoBehaviour
{
    /// <summary>Sahnedeki tek yönetici örneği.</summary>
    public static AnomalicVFXManager Instance { get; private set; }

    [Header("Geçiş")]
    [FormerlySerializedAs("fadeInTime")]
    [SerializeField, Min(0.01f)] private float _fadeInTime = 0.15f;
    [FormerlySerializedAs("fadeOutTime")]
    [SerializeField, Min(0.01f)] private float _fadeOutTime = 1.5f;
    [Tooltip("Mevcut Volume'lardan (0 ve 10) yüksek olmalı.")]
    [FormerlySerializedAs("volumePriority")]
    [SerializeField] private float _volumePriority = 20f;

    [Header("Renk")]
    [Tooltip("Doygunluk (-100 = tamamen siyah-beyaz).")]
    [FormerlySerializedAs("saturation")]
    [SerializeField, Range(-100f, 0f)] private float _saturation = -92f;
    [FormerlySerializedAs("contrast")]
    [SerializeField, Range(-50f, 50f)] private float _contrast = 30f;
    [FormerlySerializedAs("postExposure")]
    [SerializeField] private float _postExposure = -0.35f;
    [Tooltip("Tüm görüntüye binen mor ton.")]
    [FormerlySerializedAs("colorFilter")]
    [SerializeField] private Color _colorFilter = new Color(0.62f, 0.38f, 1f);
    [FormerlySerializedAs("vignetteColor")]
    [SerializeField] private Color _vignetteColor = new Color(0.25f, 0f, 0.4f);
    [FormerlySerializedAs("vignetteIntensity")]
    [SerializeField, Range(0f, 1f)] private float _vignetteIntensity = 0.5f;
    [FormerlySerializedAs("grainIntensity")]
    [SerializeField, Range(0f, 1f)] private float _grainIntensity = 0.6f;

    [Header("Glitch")]
    [FormerlySerializedAs("baseChromatic")]
    [SerializeField, Range(0f, 1f)] private float _baseChromatic = 0.3f;
    [FormerlySerializedAs("glitchChromatic")]
    [SerializeField, Range(0f, 1f)] private float _glitchChromatic = 1f;
    [FormerlySerializedAs("glitchDistortion")]
    [SerializeField, Range(0f, 0.6f)] private float _glitchDistortion = 0.3f;
    [Tooltip("Glitch'ler arası rastgele bekleme aralığı (sn): x = en az, y = en çok.")]
    [FormerlySerializedAs("glitchInterval")]
    [SerializeField] private Vector2 _glitchInterval = new Vector2(0.6f, 2f);
    [Tooltip("Tek glitch'in rastgele süre aralığı (sn): x = en az, y = en çok.")]
    [FormerlySerializedAs("glitchDuration")]
    [SerializeField] private Vector2 _glitchDuration = new Vector2(0.05f, 0.15f);

    [Header("Kamera Sarsıntısı (Horror anı)")]
    [Tooltip("En güçlü andaki yerel sapma (metre).")]
    [SerializeField, Min(0f)] private float _cameraShakeIntensity = 0.08f;
    [Tooltip("Sarsıntının toplam süresi (sn).")]
    [SerializeField, Min(0.05f)] private float _cameraShakeDuration = 0.5f;
    [Tooltip("Saniyedeki titreşim hızı (yüksek = daha sık titrer).")]
    [SerializeField, Min(1f)] private float _cameraShakeFrequency = 28f;
    [Tooltip("Sönme eğrisinin üssü: 1 = doğrusal, 2+ = başta sert, sonra yumuşakça söner.")]
    [SerializeField, Min(1f)] private float _cameraShakeDecayPower = 2f;

    private Volume _volume;
    private VolumeProfile _profile;
    private ChromaticAberration _chromatic;
    private LensDistortion _lensDistortion;

    private int _horrorBugCount;
    private float _nextGlitchTime;
    private float _glitchEndTime;

    private Transform _shakeCamera;
    private Vector3 _cameraOriginalLocalPosition;
    private Coroutine _shakeRoutine;

    /// <summary>Anomalik görünüm şu an açık mı?</summary>
    public bool IsActive { get; private set; }

    #region Unity Yaşam Döngüsü

    /// <summary>Editörde "Play" modları arası kalan statik referansı temizler.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    /// <summary>Tekil örneği kurar ve post-process Volume'unu hazırlar.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildVolume();
    }

    /// <summary>Kamera sarsıntıda kapanırsa orijinal konumuna döndürür.</summary>
    private void OnDisable()
    {
        StopCameraShake();
    }

    /// <summary>Referansı bırakır ve çalışma anında üretilen profili temizler.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_profile != null) Destroy(_profile);
    }

    /// <summary>Volume ağırlığını hedefe kaydırır ve aktifken glitch'i işletir.</summary>
    private void Update()
    {
        if (_volume == null) return;

        float targetWeight = IsActive ? 1f : 0f;
        float fadeTime = IsActive ? _fadeInTime : _fadeOutTime;
        _volume.weight = Mathf.MoveTowards(_volume.weight, targetWeight, Time.unscaledDeltaTime / fadeTime);

        if (_volume.weight <= 0.001f) return;
        UpdateGlitch();
    }

    #endregion

    #region Genel Arayüz

    /// <summary>
    /// Evin görünümünü değiştirir. true: lanetli mod (soluk + mor + glitch) ve kamera sarsıntısı.
    /// false: parlak çizgi film renklerine dönüş.
    /// </summary>
    /// <param name="state">Anomalik görünüm açılsın mı?</param>
    public void TriggerAnomalicVFX(bool state)
    {
        IsActive = state;
        if (!state) _horrorBugCount = 0;

        _nextGlitchTime = Time.unscaledTime;

        // Korku anında kısa, sert bir kalp atışı sarsıntısı.
        if (state) StartCameraShake();
    }

    /// <summary>Bir böcek Horror moda geçti (BugController çağırır): görünümü açar.</summary>
    public void NotifyHorrorBugAdded()
    {
        _horrorBugCount++;
        TriggerAnomalicVFX(true);
    }

    /// <summary>
    /// Horror moddaki bir böcek yakalandı ya da yok oldu (BugController çağırır).
    /// Hiç Horror böcek kalmadıysa görünüm normale döner.
    /// </summary>
    public void NotifyHorrorBugRemoved()
    {
        _horrorBugCount = Mathf.Max(0, _horrorBugCount - 1);
        if (_horrorBugCount == 0 && IsActive) TriggerAnomalicVFX(false);
    }

    #endregion

    #region Post-Process

    /// <summary>Çalışma anında kendi profilini ve global Volume'unu kurar (proje asset'i gerekmez).</summary>
    private void BuildVolume()
    {
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var colorAdjustments = _profile.Add<ColorAdjustments>(true);
        colorAdjustments.saturation.Override(_saturation);
        colorAdjustments.contrast.Override(_contrast);
        colorAdjustments.postExposure.Override(_postExposure);
        colorAdjustments.colorFilter.Override(_colorFilter);

        var vignette = _profile.Add<Vignette>(true);
        vignette.color.Override(_vignetteColor);
        vignette.intensity.Override(_vignetteIntensity);
        vignette.smoothness.Override(0.6f);

        var filmGrain = _profile.Add<FilmGrain>(true);
        filmGrain.type.Override(FilmGrainLookup.Thin1);
        filmGrain.intensity.Override(_grainIntensity);

        _chromatic = _profile.Add<ChromaticAberration>(true);
        _chromatic.intensity.Override(_baseChromatic);

        _lensDistortion = _profile.Add<LensDistortion>(true);
        _lensDistortion.intensity.Override(0f);

        _volume = gameObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = _volumePriority;
        _volume.weight = 0f;
        _volume.sharedProfile = _profile;
    }

    /// <summary>Rastgele aralıklarla kısa süreli renk kayması ve ekran bozulması (glitch) uygular.</summary>
    private void UpdateGlitch()
    {
        float now = Time.unscaledTime;
        if (now >= _nextGlitchTime)
        {
            _glitchEndTime = now + Random.Range(_glitchDuration.x, _glitchDuration.y);
            _nextGlitchTime = now + Random.Range(_glitchInterval.x, _glitchInterval.y);
        }

        bool isGlitching = now < _glitchEndTime;
        _chromatic.intensity.value = isGlitching ? _glitchChromatic : _baseChromatic;
        _lensDistortion.intensity.value = isGlitching ? Random.Range(-_glitchDistortion, _glitchDistortion) : 0f;
    }

    #endregion

    #region Kamera Sarsıntısı

    /// <summary>
    /// Kamera sarsıntısını başlatır. Devam eden sarsıntı varsa önce kamerayı orijinal yerine
    /// döndürür, sonra yenisini başlatır (orijinal konum asla kaybolmaz).
    /// </summary>
    private void StartCameraShake()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        StopCameraShake();

        _shakeCamera = mainCamera.transform;
        _cameraOriginalLocalPosition = _shakeCamera.localPosition;
        _shakeRoutine = StartCoroutine(ShakeCameraRoutine());
    }

    /// <summary>
    /// Sarsıntıyı hemen durdurur ve kamerayı kaydedilen orijinal yerel konumuna geri koyar.
    /// Sarsıntı yoksa hiçbir şey yapmaz.
    /// </summary>
    private void StopCameraShake()
    {
        if (_shakeRoutine == null) return;

        StopCoroutine(_shakeRoutine);
        _shakeRoutine = null;
        RestoreCameraPosition();
    }

    /// <summary>Kamerayı sarsıntı öncesi kaydedilen yerel konumuna döndürür.</summary>
    private void RestoreCameraPosition()
    {
        if (_shakeCamera != null) _shakeCamera.localPosition = _cameraOriginalLocalPosition;
    }

    /// <summary>
    /// Kamerayı Perlin gürültüsüyle titretir; şiddet zamanla üstel olarak söner
    /// (decay = (1 - t)^güç), bitince kamera orijinal konumuna döner.
    /// Zaman ölçeğinden etkilenmez (hitstop sırasında da çalışır).
    /// </summary>
    private IEnumerator ShakeCameraRoutine()
    {
        // Her sarsıntıda farklı bir gürültü bölgesi kullanılır (aynı titreşim tekrar etmesin).
        float seedX = Random.value * 100f;
        float seedY = Random.value * 100f;

        for (float elapsed = 0f; elapsed < _cameraShakeDuration; elapsed += Time.unscaledDeltaTime)
        {
            if (_shakeCamera == null) break;

            float normalizedTime = elapsed / _cameraShakeDuration;
            float decay = Mathf.Pow(1f - normalizedTime, _cameraShakeDecayPower);

            float noiseTime = elapsed * _cameraShakeFrequency;
            float offsetX = (Mathf.PerlinNoise(seedX, noiseTime) - 0.5f) * 2f;
            float offsetY = (Mathf.PerlinNoise(seedY, noiseTime) - 0.5f) * 2f;

            Vector3 offset = new Vector3(offsetX, offsetY, 0f) * (_cameraShakeIntensity * decay);
            _shakeCamera.localPosition = _cameraOriginalLocalPosition + offset;

            yield return null;
        }

        RestoreCameraPosition();
        _shakeRoutine = null;
    }

    #endregion
}
