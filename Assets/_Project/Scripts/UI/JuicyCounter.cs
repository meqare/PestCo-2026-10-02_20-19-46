using TMPro;
using UnityEngine;

// TMP_Text'e takılır: sayıyı aniden değil, sayarak gösterir.
// Artışta büyüyüp küçülür ve renk vurgusu yapar, azalışta kırmızı yanıp söner.
// Gerçek zaman kullanır: hitstop sırasında da akıcı çalışır.
[RequireComponent(typeof(TMP_Text))]
public class JuicyCounter : MonoBehaviour
{
    [Header("Sayma")]
    [Tooltip("Saniyede kaç sayı ilerlesin.")]
    [SerializeField, Min(0.1f)] private float countSpeed = 30f;
    [Tooltip("{0} = gösterilen sayı, {1} = maksimum. Örn: \"{0}/{1}\" -> 14/300")]
    [SerializeField] private string format = "{0}/{1}";
    [SerializeField] private int maxValue = 300;

    [Header("Büyüyüp Küçülme (artışta)")]
    [SerializeField, Min(1f)] private float punchScale = 1.25f;
    [SerializeField, Min(0.01f)] private float punchDuration = 0.15f;
    [Tooltip("0 = normal boyut, 1 = punchScale. Başta ve sonda 0 olmalı.")]
    [SerializeField] private AnimationCurve punchCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));

    [Header("Renk")]
    [Tooltip("Efekt yokken yazının rengi.")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private bool useColorFlash = true;
    [SerializeField] private Color increaseColor = new Color32(0x7F, 0xFF, 0x7F, 0xFF);
    [SerializeField, Min(0.01f)] private float increaseFlashDuration = 0.25f;
    [SerializeField] private bool useDecreaseFlash = true;
    [SerializeField] private Color decreaseColor = new Color32(0xFF, 0x4C, 0x4C, 0xFF);
    [SerializeField, Min(0.01f)] private float decreaseFlashDuration = 0.3f;
    [Tooltip("Azalışta kaç kez yanıp sönsün.")]
    [SerializeField, Min(1)] private int decreaseBlinkCount = 3;

    private TMP_Text text;
    private Vector3 baseScale;

    private int targetValue;
    private float displayedValue;
    private int lastShownValue = int.MinValue;
    private int lastShownMax = int.MinValue;

    private float punchTimer = -1f;   // -1 = punch yok
    private float colorTimer = -1f;   // -1 = renk efekti yok
    private bool colorIsDecrease;

    // Bileşeni ve başlangıç ölçeğini saklar (Canvas ölçeği 0.5 vb. olsa da efekt bunun üstüne uygulanır).
    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        baseScale = transform.localScale;
        text.color = normalColor;
        RefreshText(true);
    }

    // Hedef değeri ayarlar; sayı oraya doğru sayarak gider. Artış/azalışa göre efekt oynatır.
    public void SetValue(int newValue)
    {
        if (newValue == targetValue) return;

        if (newValue > targetValue)
        {
            punchTimer = 0f;
            if (useColorFlash) StartColor(false);
        }
        else if (useDecreaseFlash)
        {
            StartColor(true);
        }

        targetValue = newValue;
    }

    // Hem değeri hem maksimumu birlikte ayarlar (ör: yuva sayısı değişince).
    public void SetValue(int newValue, int newMax)
    {
        maxValue = newMax;
        SetValue(newValue);
    }

    // Animasyonsuz, anında ayarlar (oyun başında kullanışlı).
    public void SetValueImmediate(int newValue)
    {
        targetValue = newValue;
        displayedValue = newValue;
        RefreshText(true);
    }

    // Tema renklerini verir (ör: kağıt panel üstünde mürekkep yazı).
    public void SetColors(Color normal, Color increase, Color decrease)
    {
        normalColor = normal;
        increaseColor = increase;
        decreaseColor = decrease;
        if (text != null && colorTimer < 0f) text.color = normalColor;
    }

    // Sadece maksimumu değiştirir.
    public void SetMax(int newMax)
    {
        maxValue = newMax;
        RefreshText(false);
    }

    // Her karede: sayıyı hedefe yaklaştırır, büyüme ve renk efektlerini günceller.
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        displayedValue = Mathf.MoveTowards(displayedValue, targetValue, countSpeed * dt);
        RefreshText(false);

        UpdatePunch(dt);
        UpdateColor(dt);
    }

    // Gösterilen tam sayı ya da maksimum değiştiyse metni yazar (her karede yazıp çöp üretmesin).
    private void RefreshText(bool force)
    {
        int shown = Mathf.RoundToInt(displayedValue);
        if (!force && shown == lastShownValue && maxValue == lastShownMax) return;

        lastShownValue = shown;
        lastShownMax = maxValue;
        text.text = string.Format(format, shown, maxValue);
    }

    // Eğriye göre ölçeği 1 -> punchScale -> 1 yapar.
    private void UpdatePunch(float dt)
    {
        if (punchTimer < 0f) return;

        punchTimer += dt;
        float t = punchTimer / punchDuration;
        if (t >= 1f)
        {
            punchTimer = -1f;
            transform.localScale = baseScale;
            return;
        }

        float amount = punchCurve.Evaluate(t);
        transform.localScale = baseScale * (1f + (punchScale - 1f) * amount);
    }

    // Renk efektini başlatır (artış = vurgu rengi, azalış = kırmızı yanıp sönme).
    private void StartColor(bool decrease)
    {
        colorIsDecrease = decrease;
        colorTimer = 0f;
    }

    // Artışta vurgu renginden normale yumuşakça döner; azalışta kırmızı yanıp söner.
    private void UpdateColor(float dt)
    {
        if (colorTimer < 0f) return;

        colorTimer += dt;
        float duration = colorIsDecrease ? decreaseFlashDuration : increaseFlashDuration;
        float t = colorTimer / duration;
        if (t >= 1f)
        {
            colorTimer = -1f;
            text.color = normalColor;
            return;
        }

        if (colorIsDecrease)
        {
            // Süreyi decreaseBlinkCount parçaya böl: her parçanın ilk yarısı kırmızı.
            bool on = Mathf.Repeat(t * decreaseBlinkCount, 1f) < 0.5f;
            text.color = on ? decreaseColor : normalColor;
        }
        else
        {
            text.color = Color.Lerp(increaseColor, normalColor, t);
        }
    }

    // Kapanırsa yarım kalan efektleri temizler (büyük ya da renkli kalmasın).
    private void OnDisable()
    {
        punchTimer = -1f;
        colorTimer = -1f;
        if (text != null)
        {
            transform.localScale = baseScale;
            text.color = normalColor;
        }
    }
}
