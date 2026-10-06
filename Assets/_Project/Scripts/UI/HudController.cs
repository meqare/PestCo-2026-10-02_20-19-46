using UnityEngine;

// HUD'u besler: böcek ve yuva sayılarını JobManager event'lerinden alır, sprey ve vakum kapasitesini her karede okur.
// Sayaçlar sadece değer değişince güncellenir (efektler gereksiz yere tetiklenmesin).
public class HudController : MonoBehaviour
{
    [SerializeField] private JuicyCounter killCounter;
    [SerializeField] private JuicyCounter nestCounter;
    [SerializeField] private JuicyBar sprayBar;
    [Tooltip("Sprey kapasitesini tutan bileşen (Tool_1 üzerinde).")]
    [SerializeField] private SprayVisual sprayVisual;

    [Header("Vakum")]
    [Tooltip("Vakum aleti (Tool_2 üzerinde).")]
    [SerializeField] private VacuumTool vacuumTool;
    [SerializeField] private JuicyBar vacuumBar;
    [Tooltip("\"Vakum: {0}/{1}\" formatlı sayaç.")]
    [SerializeField] private JuicyCounter vacuumCounter;
    [Tooltip("Hazne dolunca görünen \"BOŞALT\" uyarısı.")]
    [SerializeField] private GameObject vacuumFullWarning;

    private JobManager jobManager;
    // İlk gelen değer animasyonsuz yazılır (oyun başında 0'dan saymasın).
    private bool pestInitialized;
    private bool nestInitialized;
    private float lastSpray = -1f;
    private int lastVacuum = -1;

    // JobManager event'lerine abone olur ve sprey çubuğunun başlangıç değerini yazar.
    // Start'ta yapılır: JobManager.Instance Awake'te kurulur, burada hazırdır.
    private void Start()
    {
        jobManager = JobManager.Instance;
        if (jobManager != null)
        {
            jobManager.OnPestCountChanged += OnPestCountChanged;
            jobManager.OnNestCountChanged += OnNestCountChanged;
        }

        if (sprayVisual != null && sprayBar != null)
        {
            lastSpray = sprayVisual.CapacityPercent;
            sprayBar.SetValueImmediate(lastSpray);
        }

        if (vacuumTool != null)
        {
            lastVacuum = vacuumTool.StoredCount;
            if (vacuumBar != null) vacuumBar.SetValueImmediate((float)lastVacuum / vacuumTool.MaxCapacity);
            if (vacuumCounter != null)
            {
                vacuumCounter.SetMax(vacuumTool.MaxCapacity);
                vacuumCounter.SetValueImmediate(lastVacuum);
            }
            if (vacuumFullWarning != null) vacuumFullWarning.SetActive(vacuumTool.IsFull);
        }
    }

    // Abonelikten çıkar (yok olmuş HUD'a event gelmesin).
    private void OnDestroy()
    {
        if (jobManager != null)
        {
            jobManager.OnPestCountChanged -= OnPestCountChanged;
            jobManager.OnNestCountChanged -= OnNestCountChanged;
        }
    }

    // Öldürülen / toplam böcek değişti: sayaca yazar.
    private void OnPestCountChanged(int killed, int total)
    {
        if (killCounter == null) return;

        if (!pestInitialized)
        {
            pestInitialized = true;
            killCounter.SetMax(total);
            killCounter.SetValueImmediate(killed);
            return;
        }
        killCounter.SetValue(killed, total);
    }

    // Kalan / toplam yuva değişti: sayaca yazar (azalınca kırmızı yanıp söner).
    private void OnNestCountChanged(int alive, int total)
    {
        if (nestCounter == null) return;

        if (!nestInitialized)
        {
            nestInitialized = true;
            nestCounter.SetMax(total);
            nestCounter.SetValueImmediate(alive);
            return;
        }
        nestCounter.SetValue(alive, total);
    }

    // Her karede sprey ve vakum kapasitesini kontrol eder, değiştiyse ilgili parçaya bildirir.
    private void Update()
    {
        UpdateVacuum();

        if (sprayVisual != null && sprayBar != null)
        {
            float spray = sprayVisual.CapacityPercent;
            if (!Mathf.Approximately(spray, lastSpray))
            {
                lastSpray = spray;
                sprayBar.SetValue(spray);
            }
        }
    }

    // Vakum haznesi değiştiyse çubuğu, sayacı ve "BOŞALT" uyarısını günceller.
    private void UpdateVacuum()
    {
        if (vacuumTool == null) return;

        int stored = vacuumTool.StoredCount;
        if (stored == lastVacuum) return;
        lastVacuum = stored;

        if (vacuumBar != null) vacuumBar.SetValue((float)stored / vacuumTool.MaxCapacity);
        if (vacuumCounter != null) vacuumCounter.SetValue(stored);
        if (vacuumFullWarning != null) vacuumFullWarning.SetActive(vacuumTool.IsFull);
    }
}
