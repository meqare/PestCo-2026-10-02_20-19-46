using UnityEngine;
using UnityEngine.UI;

// İki katmanlı çubuk (dövüş oyunu can barı gibi):
// önde beyaz çubuk değeri anında gösterir, arkadaki kırmızı çubuk gecikmeyle takip eder.
// İki Image'ın da Image Type'ı "Filled" olmalı.
public class JuicyBar : MonoBehaviour
{
    [Header("Çubuklar")]
    [Tooltip("Öndeki anlık (beyaz) çubuk.")]
    [SerializeField] private Image frontFill;
    [Tooltip("Arkadaki gecikmeli (kırmızı) çubuk.")]
    [SerializeField] private Image backFill;

    [Header("Davranış")]
    [Tooltip("Değer düşünce kırmızı çubuğun beklediği süre.")]
    [SerializeField, Min(0f)] private float backDelay = 0.5f;
    [Tooltip("Kırmızı çubuğun aşağı inme hızı (büyük = hızlı).")]
    [SerializeField, Min(0.1f)] private float backFollowSpeed = 4f;
    [Tooltip("Değer artınca beyaz çubuğun yükselme hızı (büyük = hızlı).")]
    [SerializeField, Min(0.1f)] private float frontRiseSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float startValue = 1f;

    private float target;
    private float delayTimer;

    // Çubuğun gösterdiği son değer (0-1).
    public float Value => target;

    // Başlangıç doluluğunu ayarlar ve Image ayarını kontrol eder.
    private void Awake()
    {
        WarnIfNotFilled(frontFill);
        WarnIfNotFilled(backFill);
        SetValueImmediate(startValue);
    }

    // Değeri 0-1 arası ayarlar. Düşüşte beyaz hemen iner, kırmızı gecikmeyle takip eder.
    public void SetValue(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        if (normalized < target)
        {
            if (frontFill != null) frontFill.fillAmount = normalized;
            delayTimer = backDelay;
        }
        else if (normalized > target)
        {
            // Dolarken kırmızı hemen yeni seviyeye çıkar, beyaz yumuşakça yükselir.
            if (backFill != null) backFill.fillAmount = normalized;
        }

        target = normalized;
    }

    // Mevcut / maksimum şeklinde ayarlar (ör: sprey 37 / 100).
    public void SetValue(float current, float max)
    {
        SetValue(max > 0f ? current / max : 0f);
    }

    // Animasyonsuz, iki çubuğu da anında ayarlar.
    public void SetValueImmediate(float normalized)
    {
        target = Mathf.Clamp01(normalized);
        delayTimer = 0f;
        if (frontFill != null) frontFill.fillAmount = target;
        if (backFill != null) backFill.fillAmount = target;
    }

    // Her karede: beyaz yükseliyorsa yaklaştırır, kırmızı gecikme bitince aşağı süzülür.
    private void Update()
    {
        float dt = Time.unscaledDeltaTime; // hitstop'ta da akıcı

        if (frontFill != null && frontFill.fillAmount < target)
        {
            frontFill.fillAmount = Mathf.Lerp(frontFill.fillAmount, target, frontRiseSpeed * dt);
            if (target - frontFill.fillAmount < 0.001f)
                frontFill.fillAmount = target;
        }

        if (backFill == null || backFill.fillAmount <= target) return;

        if (delayTimer > 0f)
        {
            delayTimer -= dt;
            return;
        }

        backFill.fillAmount = Mathf.Lerp(backFill.fillAmount, target, backFollowSpeed * dt);
        // Lerp sonsuza kadar yaklaşır; çok yaklaşınca tam değere oturt.
        if (backFill.fillAmount - target < 0.001f)
            backFill.fillAmount = target;
    }

    // Image "Filled" değilse fillAmount işe yaramaz; uyarı verir.
    private void WarnIfNotFilled(Image image)
    {
        if (image != null && image.type != Image.Type.Filled)
            Debug.LogWarning($"JuicyBar: '{image.name}' Image Type'ı Filled değil, çubuk hareket etmez.", this);
    }
}
