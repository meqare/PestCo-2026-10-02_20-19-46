using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Sprey aletinin üzerinde durur. Sol tık basılıyken partikül + ses, kapasite azalır. R tuşu kutuyu doldurur.
// Çizgi film hissi: ilk basışta büyük bir "puf" patlaması ve hızlı yükselen püskürtme, bırakınca kısa bir son püf ve yumuşak sönme.
public class SprayVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Sprey partikül sistem(ler)i. Boş bırakılırsa child objelerde aranır.")]
    [SerializeField] private ParticleSystem[] sprayLayers;
    [SerializeField] private AudioSource spraySound;

    [Header("Capacity")]
    [SerializeField, Min(1f)] private float maxCapacity = 100f;
    [SerializeField, Min(0f)] private float drainRate = 8f;

    [Header("Yoğunluk")]
    [Tooltip("Partikül sayısı çarpanı. 1 = ilk hâl.")]
    [SerializeField, Min(0.1f)] private float density = 2f;

    [Header("Çizgi film efekti")]
    [Tooltip("Basınca tek seferde çıkan puf sayısı.")]
    [SerializeField, Min(0)] private int startBurst = 90;
    [Tooltip("Bırakınca çıkan son puf sayısı.")]
    [SerializeField, Min(0)] private int endBurst = 45;
    [Tooltip("Puf partiküllerinin normal boyuta oranı.")]
    [SerializeField, Min(0.1f)] private float burstSizeScale = 1.9f;
    [Tooltip("Puf partiküllerinin hız çarpanı (başlangıç).")]
    [SerializeField, Min(0.1f)] private float startBurstSpeed = 1.15f;
    [Tooltip("Püskürtmenin tam yoğunluğa çıkma süresi (sn).")]
    [SerializeField, Min(0.01f)] private float rampUpTime = 0.12f;
    [Tooltip("Bırakınca püskürtmenin sönme süresi (sn).")]
    [SerializeField, Min(0.01f)] private float rampDownTime = 0.22f;
    [SerializeField, Min(0f)] private float startShake = 0.05f;
    [SerializeField, Min(0f)] private float endShake = 0.025f;

    public float CapacityPercent => currentCapacity / maxCapacity;
    public bool IsSpraying => isSpraying;

    // Çizgi film animasyonları (tabanca sıkışması vb.) için.
    public event Action SprayStarted;
    public event Action SprayStopped;

    private float currentCapacity;
    private bool isSpraying;
    private float emissionFactor;      // 0..1: şu anki püskürtme gücü
    private float[] baseRates;

    // Referansları alır, kapasiteyi doldurur ve başlangıçta emisyonu kapatır.
    private void Awake()
    {
        if (sprayLayers == null || sprayLayers.Length == 0)
            sprayLayers = GetComponentsInChildren<ParticleSystem>(true);

        baseRates = new float[sprayLayers.Length];
        for (int i = 0; i < sprayLayers.Length; i++)
        {
            var ps = sprayLayers[i];
            if (ps == null) continue;
            baseRates[i] = ps.emission.rateOverTime.constant * density;
            // Daha yoğun püskürtme için parçacık sınırı yetmeyebilir.
            var main = ps.main;
            int needed = Mathf.CeilToInt(baseRates[i] * main.startLifetime.constantMax * 1.2f + startBurst);
            if (main.maxParticles < needed) main.maxParticles = needed;
        }

        currentCapacity = maxCapacity;
        emissionFactor = 0f;
        ApplyEmission();
    }

    // Her kare: tuş basılı mı ve kapasite var mı, ona göre püskürtmeyi aç/kapat; gücü yumuşakça ayarla.
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            Refill();

        var mouse = Mouse.current;
        bool wantsToSpray = mouse != null && mouse.leftButton.isPressed;
        bool canSpray = wantsToSpray && currentCapacity > 0f;

        if (canSpray && !isSpraying)
            StartSpray();
        else if (!canSpray && isSpraying)
            StopSpray();

        if (isSpraying)
            currentCapacity = Mathf.Max(0f, currentCapacity - drainRate * Time.deltaTime);

        // Güç: basılıyken hızlı yüksel, bırakınca yavaş sön.
        float target = isSpraying ? 1f : 0f;
        float time = isSpraying ? rampUpTime : rampDownTime;
        emissionFactor = Mathf.MoveTowards(emissionFactor, target, Time.deltaTime / time);
        ApplyEmission();
    }

    // Alet değiştirilip obje kapanırsa püskürtme ve ses takılı kalmasın.
    private void OnDisable()
    {
        if (isSpraying)
            StopSpray();
        emissionFactor = 0f;
        ApplyEmission();
    }

    // Puf patlaması yapar ve sesi döngüde başlatır.
    private void StartSpray()
    {
        isSpraying = true;

        EmitBurst(startBurst, burstSizeScale, startBurstSpeed);
        Juice();

        if (spraySound != null)
        {
            spraySound.loop = true;
            if (!spraySound.isPlaying)
                spraySound.Play();
        }

        SprayStarted?.Invoke();
    }

    // Son pufu çıkarır; emisyon rampDownTime'da yumuşakça söner, havadaki partiküller doğal söner.
    private void StopSpray()
    {
        isSpraying = false;

        EmitBurst(endBurst, burstSizeScale * 0.85f, 0.6f);
        if (JuiceManager.Instance != null && endShake > 0f)
            JuiceManager.Instance.Shake(endShake, 0.12f);

        if (spraySound != null)
            spraySound.Stop();

        SprayStopped?.Invoke();
    }

    private void Juice()
    {
        if (JuiceManager.Instance != null && startShake > 0f)
            JuiceManager.Instance.Shake(startShake, 0.15f);
    }

    // Parçacıkları anlık olarak normalden büyük ve hızlı/yavaş çıkarır: karikatürize "puf".
    private void EmitBurst(int count, float sizeScale, float speedScale)
    {
        if (count <= 0 || sprayLayers == null) return;
        for (int i = 0; i < sprayLayers.Length; i++)
        {
            var ps = sprayLayers[i];
            if (ps == null) continue;
            if (!ps.isPlaying) ps.Play();
            // İlk katman ana bulut; diğerleri (balon, yıldız) daha az parçacıkla patlasın.
            int layerCount = i == 0 ? count : Mathf.CeilToInt(count * 0.25f);
            var main = ps.main;
            var p = new ParticleSystem.EmitParams();
            // Boyut, hız ve ömür her parçacıkta ayrı rastgele olsun: tek tek Emit.
            for (int n = 0; n < layerCount; n++)
            {
                p.startLifetime = UnityEngine.Random.Range(main.startLifetime.constantMin, main.startLifetime.constantMax) * 0.8f;
                p.startSize = UnityEngine.Random.Range(main.startSize.constantMin, main.startSize.constantMax) * sizeScale;
                var dir = ps.transform.forward + UnityEngine.Random.insideUnitSphere * 0.12f;
                p.velocity = dir.normalized * (UnityEngine.Random.Range(main.startSpeed.constantMin, main.startSpeed.constantMax) * speedScale);
                p.position = ps.transform.position;
                ps.Emit(p, 1);
            }
        }
    }

    // Emisyon gücünü uygular (rate = taban × güç). Güç sıfırsa emisyon kapalı.
    private void ApplyEmission()
    {
        if (sprayLayers == null) return;
        for (int i = 0; i < sprayLayers.Length; i++)
        {
            var ps = sprayLayers[i];
            if (ps == null) continue;
            var emission = ps.emission;
            bool on = emissionFactor > 0.001f;
            emission.enabled = on;
            if (on)
            {
                emission.rateOverTime = baseRates[i] * emissionFactor;
                if (!ps.isPlaying) ps.Play();
            }
        }
    }

    // Kutuyu tamamen doldurur.
    public void Refill()
    {
        currentCapacity = maxCapacity;
    }
}
