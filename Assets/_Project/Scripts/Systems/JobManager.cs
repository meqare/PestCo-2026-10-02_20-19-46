using System;
using System.Collections;
using UnityEngine;

// Bir işin sonucunu taşıyan veri (sonuç ekranı okur).
public struct JobResult
{
    public int CleanedPercent;   // 0-100
    public float DurationSeconds;
    public int Damage;           // $
    public int Earnings;         // $
    public string Grade;         // S, A, B, C, D
}

// İşin başlangıcını ve bitişini takip eder. Tüm yuvalar yok olup yeterli böcek öldürülünce
// sonucu hesaplayıp OnJobComplete'i tetikler.
public class JobManager : MonoBehaviour
{
    public static JobManager Instance { get; private set; }
    // İş bitince tetiklenir (ResultScreen dinler).
    public static event Action<JobResult> OnJobComplete;
    // İş başlayınca (Start sonunda) tetiklenir (AnomalyManager dinler).
    public static event Action OnJobStarted;
    // Öldürülen veya toplam böcek değişince tetiklenir: (öldürülen, toplam). HUD dinler.
    public event Action<int, int> OnPestCountChanged;
    // Kalan yuva sayısı değişince tetiklenir: (kalan, toplam). HUD dinler.
    public event Action<int, int> OnNestCountChanged;

    [Header("Bitiş")]
    [Tooltip("İşin bitmesi için öldürülmesi gereken böcek oranı (0.9 = %90). Yuvalar da yok edilmiş olmalı.")]
    [SerializeField, Range(0f, 1f)] private float requiredPestPercent = 0.9f;
    [Tooltip("İş bittikten sonra sonuç ekranına kadar beklenen gerçek süre (efekt ve hitstop bitsin).")]
    [SerializeField, Min(0f)] private float completeDelay = 1.5f;

    [Header("Ödeme")]
    [Tooltip("%100 temizlikte ödenen ücret ($).")]
    [SerializeField, Min(0)] private int basePay = 300;

    [Header("Not")]
    [Tooltip("Bu süreyi (sn) aşınca not puanı düşmeye başlar.")]
    [SerializeField, Min(1f)] private float parTime = 300f;
    [Tooltip("parTime'ı aşan her dakika için puandan düşülen miktar.")]
    [SerializeField, Min(0f)] private float penaltyPerExtraMinute = 5f;
    [Tooltip("Her 10$ hasar için puandan düşülen miktar.")]
    [SerializeField, Min(0f)] private float penaltyPer10Damage = 1f;

    public float ElapsedTime => (isFinished ? finishTime : Time.time) - startTime;
    // Bu işte öldürülen böcek sayısı.
    public int PestsKilled => Pest.TotalKills - killsAtStart;
    // Bu işte ortaya çıkan toplam böcek (başta yerleşenler + yuvalardan doğanlar). Oyun boyunca artar.
    public int TotalPestTarget => PestPool.Instance != null ? PestPool.Instance.TotalSpawned : 0;
    // Temizlik oranı 0-1: öldürülen / toplam. Hiç böcek yoksa 1.
    public float CleanPercent => TotalPestTarget > 0 ? Mathf.Clamp01((float)PestsKilled / TotalPestTarget) : 1f;

    private float startTime;
    private float finishTime;
    private int totalNests;
    private int killsAtStart;
    private bool startLogged;
    private bool waitingForPestsLogged;
    // Son bildirilen değerler (-1 = henüz bildirilmedi; ilk karede mutlaka gönderilir).
    private int lastKilled = -1;
    private int lastPestTotal = -1;
    private int lastNestsAlive = -1;
    private int damage;
    private bool isFinished;

    // Domain reload kapalıyken eski oyundan kalan abonelikler ve referans temizlensin.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnJobComplete = null;
        OnJobStarted = null;
    }

    // Singleton'ı kurar.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Sahnede birden fazla JobManager var, fazlası siliniyor.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Sahne kapanınca singleton referansını temizler.
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // İşi başlatır: süreyi ve toplam yuva sayısını kaydeder.
    private void Start()
    {
        startTime = Time.time;
        totalNests = PestNest.ActiveNestCount;
        // TotalKills sahne yeniden yüklenince sıfırlanmaz; bu işin öldürmelerini farkla buluruz.
        killsAtStart = Pest.TotalKills;
        // Kaç yuva sayıldığını yaz: 0 ise yuvalarda PestNest eksik ya da kapalı demektir.
        if (totalNests == 0)
            Debug.LogError("JobManager: hic yuva bulunamadi, is bitmeyecek! 'PEST CO/Fix - Tum Yuvalari Duzelt' menusunu calistir.", this);
        else
            Debug.Log($"Toplam yuva bulundu: {totalNests}", this);

        OnJobStarted?.Invoke();
    }

    // Tur başında hedefi yazar; tüm yuvalar yok olduysa ve yeterli böcek öldürüldüyse işi bitirir.
    private void Update()
    {
        // İlk kare: tüm Start'lar (PestSpawner dahil) çalıştı, başlangıç böcekleri sayıldı.
        if (!startLogged)
        {
            startLogged = true;
            Debug.Log($"JobManager: tur basladi. totalPestTarget = {TotalPestTarget}, yuva = {totalNests}, " +
                      $"gereken oran = %{requiredPestPercent * 100f:0}", this);
        }

        NotifyCountChanges();

        if (isFinished || totalNests == 0) return;
        if (PestNest.ActiveNestCount > 0) return;

        // Koşul: pestsKilled / totalPestTarget >= requiredPestPercent
        if (CleanPercent < requiredPestPercent)
        {
            if (!waitingForPestsLogged)
            {
                waitingForPestsLogged = true;
                Debug.Log($"JobManager: tum yuvalar yok edildi ama temizlik yetersiz " +
                          $"({PestsKilled}/{TotalPestTarget} = %{CleanPercent * 100f:0.0}). Kalan bocekleri temizle.", this);
            }
            return;
        }

        isFinished = true;
        finishTime = Time.time;
        StartCoroutine(CompleteAfterDelay());
    }

    // Böcek ve yuva sayıları değiştiyse ilgili event'i tetikler (değişmeyen karede bir şey yapmaz).
    private void NotifyCountChanges()
    {
        int killed = PestsKilled;
        int pestTotal = TotalPestTarget;
        if (killed != lastKilled || pestTotal != lastPestTotal)
        {
            lastKilled = killed;
            lastPestTotal = pestTotal;
            OnPestCountChanged?.Invoke(killed, pestTotal);
        }

        int nestsAlive = PestNest.ActiveNestCount;
        if (nestsAlive != lastNestsAlive)
        {
            lastNestsAlive = nestsAlive;
            OnNestCountChanged?.Invoke(nestsAlive, totalNests);
        }
    }

    // Eşyaya verilen hasarı ekler (kazançtan düşülür).
    public void AddDamage(int amount)
    {
        if (amount > 0 && !isFinished)
            damage += amount;
    }

    // Efektlerin bitmesini gerçek zamanla bekler, sonra sonucu yayınlar.
    private IEnumerator CompleteAfterDelay()
    {
        yield return new WaitForSecondsRealtime(completeDelay);

        JobResult result = CalculateResult();
        Debug.Log($"Is bitti: {PestsKilled}/{TotalPestTarget} bocek = %{result.CleanedPercent}, " +
                  $"{result.DurationSeconds:0}s, not {result.Grade}", this);
        // Kazancı ve öldürülen böcekleri oyuncu verisine işle (kaydeder).
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterJobComplete(result.Earnings, PestsKilled);
        else
            Debug.LogWarning("JobManager: GameManager sahnede yok, kazanc kaydedilmedi.", this);
        OnJobComplete?.Invoke(result);
    }

    // Temizlik oranını, kazancı ve not harfini hesaplar.
    private JobResult CalculateResult()
    {
        // cleanPercent = (float)pestsKilled / totalPestTarget. Aşağı yuvarlanır: %89.6 ekranda %90 görünmesin.
        int cleaned = Mathf.FloorToInt(CleanPercent * 100f);

        float duration = finishTime - startTime;
        int earnings = Mathf.Max(0, Mathf.RoundToInt(basePay * cleaned / 100f) - damage);

        float extraMinutes = Mathf.Max(0f, duration - parTime) / 60f;
        float score = cleaned - extraMinutes * penaltyPerExtraMinute - damage / 10f * penaltyPer10Damage;

        return new JobResult
        {
            CleanedPercent = cleaned,
            DurationSeconds = duration,
            Damage = damage,
            Earnings = earnings,
            Grade = ScoreToGrade(score)
        };
    }

    // Puanı harfe çevirir.
    private static string ScoreToGrade(float score)
    {
        if (score >= 95f) return "S";
        if (score >= 85f) return "A";
        if (score >= 70f) return "B";
        if (score >= 50f) return "C";
        return "D";
    }
}
