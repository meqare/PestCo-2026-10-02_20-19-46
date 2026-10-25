using UnityEngine;

public enum CagePressureState
{
    Safe,
    Warning,
    Danger,
    Critical
}

// Bir fanusun içindeki yaratıkların MANTIKSAL büyüklüğünden doğan baskıyı hesaplar ve yayınlar.
// Her fanusun kendi bileşeni vardır (BugCage ile aynı nesne); fanuslar birbirinden bağımsızdır.
// Hesap olay tabanlıdır: BugCage.ContentsChanged (yaratık eklendi/çıktı/satıldı/yok oldu/beslendi) gelince
// yeniden yapılır; her karede çalışmaz.
//
//   yaratık katkısı = Max(0, LogicalGrowth - 1) * pressurePerGrowth
//   baskı           = fanustaki katkıların toplamı (100'ün üstüne çıkabilir)
//
// Bu aşamada baskının yalnızca göstergesi vardır: kırılma, geri sayım ve kaçış YOKTUR.
// İleride mutasyonlar baskıyı etkileyecekse ContributionFor() ezilebilir ya da katsayı fanus seviyesine göre
// Inspector'dan değiştirilebilir.
[RequireComponent(typeof(BugCage))]
public class CagePressureController : MonoBehaviour
{
    // Hesapta kullanılan en büyük baskı (taşma ve bozuk veri koruması).
    private const float MaxPressure = 100000f;

    [Header("Hesap")]
    [Tooltip("Mantıksal büyüme 1'in üstündeki her 1.0 için eklenen baskı. Fanus seviyesine göre değiştirilebilir.")]
    [SerializeField, Min(0f)] private float pressurePerGrowth = 70f;

    [Header("Eşikler (baskı değeri)")]
    [SerializeField, Min(0f)] private float warningThreshold = 40f;
    [SerializeField, Min(0f)] private float dangerThreshold = 70f;
    [Tooltip("Bu değer ve üstü Critical. Yüzde göstergesi de buna göre hesaplanır (varsayılan 100 = %100).")]
    [SerializeField, Min(1f)] private float criticalThreshold = 100f;

    [Header("Durum ışığı (isteğe bağlı)")]
    [Tooltip("Fanusun üstündeki küçük gösterge gövdesi (ör. küçük küre). Rengi MaterialPropertyBlock ile verilir; " +
             "materyal URP Unlit gibi _BaseColor kullanmalıdır.")]
    [SerializeField] private Renderer statusRenderer;
    [Tooltip("İsteğe bağlı ışık: durum rengini alır. Sahnede çok sayıda gerçek ışık olmaması için genelde boş bırakın.")]
    [SerializeField] private Light statusLight;
    [SerializeField] private Color safeColor = new Color(0.2f, 0.85f, 0.25f);
    [SerializeField] private Color warningColor = new Color(1f, 0.9f, 0.15f);
    [SerializeField] private Color dangerColor = new Color(1f, 0.5f, 0.1f);
    [SerializeField] private Color criticalColor = new Color(0.95f, 0.15f, 0.12f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private BugCage cage;
    private MaterialPropertyBlock block;
    private float pressure;
    private CagePressureState state;

    // Ham baskı: 0 veya daha büyük, 100'ün üstünde olabilir.
    public float Pressure => pressure;
    // Eşik değere göre yüzde: 125 = %125 (kritik eşiğin %125'i). Çubuk için Normalized kullanın.
    public float PressurePercent => pressure / criticalThreshold * 100f;
    // 0-1 aralığında doluluk (çubuk için); baskı 100'ü aşsa da 1'de kalır.
    public float Normalized => Mathf.Clamp01(pressure / criticalThreshold);
    public CagePressureState State => state;

    // Baskı değeri değişince tetiklenir (yeni baskı).
    public event System.Action<float> OnPressureChanged;
    // Durum (Safe/Warning/Danger/Critical) değişince tetiklenir (yeni durum).
    public event System.Action<CagePressureState> OnPressureStateChanged;

    public Color ColorFor(CagePressureState s)
    {
        switch (s)
        {
            case CagePressureState.Warning: return warningColor;
            case CagePressureState.Danger: return dangerColor;
            case CagePressureState.Critical: return criticalColor;
            default: return safeColor;
        }
    }

    private void Awake()
    {
        cage = GetComponent<BugCage>();
    }

    private void OnEnable()
    {
        cage.ContentsChanged += Recalculate;
        Recalculate();
    }

    private void OnDisable()
    {
        cage.ContentsChanged -= Recalculate;
    }

    // Fanustaki yaratıkların katkılarını toplayıp baskıyı ve durumu günceller; değişiklik varsa olay yayınlar.
    public void Recalculate()
    {
        float total = 0f;
        if (!cage.IsBroken) // kırık fanusta baskı yoktur
            foreach (CageableBug bug in cage.Caged)
            {
                if (bug == null) continue;
                total += ContributionFor(bug);
            }

        if (float.IsNaN(total) || total < 0f) total = 0f;
        total = Mathf.Min(total, MaxPressure);

        bool changed = !Mathf.Approximately(total, pressure);
        pressure = total;

        CagePressureState newState = StateFor(pressure);
        bool stateChanged = newState != state;
        state = newState;

        if (changed || stateChanged || !visualInitialized) ApplyVisual();
        if (changed) OnPressureChanged?.Invoke(pressure);
        if (stateChanged) OnPressureStateChanged?.Invoke(state);
    }

    // Bir yaratığın baskı katkısı: Max(0, mantıksal büyüme - 1) * katsayı. NaN/Infinity 0 sayılır.
    // İleride mutasyonlar katkıyı değiştirecekse buradan genişletilir.
    protected virtual float ContributionFor(CageableBug bug)
    {
        float growth = bug.GrowthMultiplier;
        if (float.IsNaN(growth) || float.IsInfinity(growth)) return 0f;

        float contribution = Mathf.Max(0f, growth - 1f) * pressurePerGrowth;
        return float.IsNaN(contribution) || float.IsInfinity(contribution) ? 0f : contribution;
    }

    public CagePressureState StateFor(float value)
    {
        if (value >= criticalThreshold) return CagePressureState.Critical;
        if (value >= dangerThreshold) return CagePressureState.Danger;
        if (value >= warningThreshold) return CagePressureState.Warning;
        return CagePressureState.Safe;
    }

    private bool visualInitialized;
    private Color? colorOverride;

    // Durum ışığını geçici olarak başka bir renge alır (ör. kritik geri sayımda yanıp sönme); null = normal renk.
    public void SetColorOverride(Color? color)
    {
        colorOverride = color;
        ApplyVisual();
    }

    // Durum ışığının rengini günceller (ortak materyali bozmamak için MaterialPropertyBlock).
    private void ApplyVisual()
    {
        visualInitialized = true;
        Color color = colorOverride ?? ColorFor(state);

        if (statusRenderer != null)
        {
            if (block == null) block = new MaterialPropertyBlock();
            statusRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            statusRenderer.SetPropertyBlock(block);
        }
        if (statusLight != null) statusLight.color = color;
    }
}
