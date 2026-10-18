using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Kamyonet (van) satış alanı.
/// Oyuncu girince toplanan yemekler yakalanmış böceğe yedirilir; böcek kafeste yumuşakça şişer.
/// Besleme olduysa satış oyuncu satış tuşuna basınca yapılır (böcek kafesi kırmadan önce);
/// besleme olmadıysa girişte hemen satılır. Bu nesnede "Is Trigger" açık bir Collider bulunmalıdır.
/// </summary>
public class TruckZone : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Kafes")]
    [Tooltip("Kamyon kasasındaki zemin noktası (böcek burada durur). Boşsa bu nesnenin altı kullanılır.")]
    [FormerlySerializedAs("cagePoint")]
    [SerializeField] private Transform _cagePoint;

    [Tooltip("Kafeste gösterilecek böcek modeli (FBX/prefab).")]
    [FormerlySerializedAs("bugModelPrefab")]
    [SerializeField] private GameObject _bugModelPrefab;

    [Tooltip("Modelin dokulu malzemesi (boşsa modelin kendi malzemesi).")]
    [FormerlySerializedAs("bugModelMaterial")]
    [SerializeField] private Material _bugModelMaterial;

    [Tooltip("Model beslenmeden önce bu boyda (metre) başlar.")]
    [FormerlySerializedAs("bugModelHeight")]
    [SerializeField, Min(0.1f)] private float _bugModelHeight = 0.8f;

    [Header("Büyüme Animasyonu")]
    [Tooltip("Her beslemede ölçeğin yeni boyuna yumuşakça ulaşma süresi (sn).")]
    [SerializeField, Min(0.05f)] private float _growDuration = 0.6f;

    [Header("Satış")]
    [FormerlySerializedAs("sellKey")]
    [SerializeField] private Key _sellKey = Key.E;

    /// <summary>Kafeste o an gösterilen (önbellekteki) böcek görseli; yoksa null.</summary>
    private GameObject _spawnedBugVisual;
    private FedBugVisual _spawnedBugLogic;
    private BugData _cagedBugData;
    private Coroutine _scaleRoutine;

    /// <summary>Oyuncu satış alanının içinde mi?</summary>
    public bool IsPlayerInside { get; private set; }

    /// <summary>Kafeste şu an beslenen bir böcek var mı?</summary>
    public bool HasCagedBug => _spawnedBugVisual != null;

    #region Unity Yaşam Döngüsü

    /// <summary>Besleme olayına abone olur (kafes görseli bunu dinler).</summary>
    private void OnEnable()
    {
        BugInventory.BugFed += HandleBugFed;
    }

    /// <summary>Besleme olayından çıkar (yok olmuş nesneye olay gitmesin).</summary>
    private void OnDisable()
    {
        BugInventory.BugFed -= HandleBugFed;
    }

    /// <summary>
    /// Oyuncu girince yemekleri yedirir. Besleme yapılmadıysa böcekleri hemen satar,
    /// yapıldıysa oyuncudan satış tuşuna basmasını bekler.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        IsPlayerInside = true;

        int fedCount = FeedBugsWithCollectedFood();
        if (fedCount == 0)
        {
            SellCapturedBugs();
            return;
        }

        Debug.Log($"[Kamyonet] {fedCount} böcek beslendi. Satmak için {_sellKey} tuşuna bas (kafes kırılmadan önce!)");
    }

    /// <summary>Oyuncu alandan çıkınca satış tuşu dinlemesi kapanır.</summary>
    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        IsPlayerInside = false;
    }

    /// <summary>Oyuncu alandaysa satış tuşunu dinler.</summary>
    private void Update()
    {
        if (!IsPlayerInside) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[_sellKey].wasPressedThisFrame)
        {
            SellCapturedBugs();
        }
    }

    #endregion

    #region Besleme

    /// <summary>
    /// Toplanan her yemeği yakalanmış bir böceğe yedirir (yemek bitene ya da böcek kalmayana kadar).
    /// </summary>
    /// <returns>Yedirilen toplam yemek sayısı.</returns>
    private int FeedBugsWithCollectedFood()
    {
        BugInventory inventory = BugInventory.Instance;
        FoodInventory foods = FoodInventory.Instance;
        if (inventory == null || foods == null) return 0;

        int fedCount = 0;
        foreach (FoodType food in System.Enum.GetValues(typeof(FoodType)))
        {
            while (foods.GetCount(food) > 0)
            {
                BugData target = FindFeedableBug(inventory);
                if (target == null) return fedCount;

                int countBefore = foods.GetCount(food);
                inventory.FeedCapturedBug(target, food);

                // Yemek harcanmadıysa besleme başarısızdır; sonsuz döngüye girmemek için çık.
                if (foods.GetCount(food) == countBefore) break;
                fedCount++;
            }
        }
        return fedCount;
    }

    /// <summary>
    /// Envanterdeki ilk böcek türünü bulur (FeedCapturedBug zaten en çok beslenmiş olana yedirir).
    /// </summary>
    /// <returns>Beslenebilecek böcek verisi; yoksa null.</returns>
    private BugData FindFeedableBug(BugInventory inventory)
    {
        List<CapturedBugInfo> bugs = inventory.GetCapturedBugs();
        for (int i = 0; i < bugs.Count; i++)
        {
            if (bugs[i].data != null && bugs[i].count > 0) return bugs[i].data;
        }
        return null;
    }

    /// <summary>
    /// Besleme olayı gelince kafes görselini hazırlar (ilk beslemede oluşturur)
    /// ve yeni boyuna yumuşakça büyütür.
    /// </summary>
    private void HandleBugFed(BugData data, BugState state, FoodType food)
    {
        // Farklı türde bir böcek beslendiyse eski görsel kaldırılır (kafeste tek böcek durur).
        if (_spawnedBugVisual != null && _cagedBugData != data)
        {
            DestroyCagedVisual();
        }

        if (_spawnedBugVisual == null)
        {
            _spawnedBugVisual = SpawnBugVisual(data);
            if (_spawnedBugVisual == null) return;

            _cagedBugData = data;
        }

        Vector3 targetScale = _spawnedBugLogic.Grow();

        if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
        _scaleRoutine = StartCoroutine(SmoothScaleBug(_spawnedBugVisual.transform, targetScale));
    }

    /// <summary>
    /// Böcek modelini kafes noktasında oluşturur, başlangıç boyuna getirir ve mantık bileşenini ekler.
    /// </summary>
    /// <returns>Oluşturulan görsel; model atanmamışsa null.</returns>
    private GameObject SpawnBugVisual(BugData data)
    {
        if (_bugModelPrefab == null)
        {
            Debug.LogWarning("TruckZone: Böcek modeli (Bug Model Prefab) atanmamış, kafes görseli oluşturulamadı.", this);
            return null;
        }

        GameObject visual = Instantiate(_bugModelPrefab);
        visual.name = "FedBug_" + data.bugName;

        Renderer modelRenderer = visual.GetComponentInChildren<Renderer>();
        if (modelRenderer != null)
        {
            if (_bugModelMaterial != null) modelRenderer.sharedMaterial = _bugModelMaterial;
            NormalizeModelHeight(visual.transform, modelRenderer);
        }

        // Kafes noktası zemindir; böcek zeminde durur ve büyürken yukarı doğru şişer.
        Vector3 groundPoint = _cagePoint != null
            ? _cagePoint.position
            : new Vector3(transform.position.x, 0f, transform.position.z);

        _spawnedBugLogic = visual.AddComponent<FedBugVisual>();
        _spawnedBugLogic.Init(data, this, groundPoint);
        return visual;
    }

    /// <summary>Model ne kadar büyük olursa olsun başlangıç boyunu ayarlanan yüksekliğe eşitler.</summary>
    private void NormalizeModelHeight(Transform modelTransform, Renderer modelRenderer)
    {
        float currentHeight = modelRenderer.bounds.size.y;
        if (currentHeight <= 0.0001f) return;

        modelTransform.localScale *= _bugModelHeight / currentHeight;
    }

    /// <summary>
    /// Böceğin ölçeğini mevcut değerinden hedef değere yumuşak geçişle (Lerp + SmoothStep) büyütür.
    /// </summary>
    /// <param name="target">Ölçeklenecek böcek görseli.</param>
    /// <param name="targetScale">Ulaşılacak hedef ölçek.</param>
    private IEnumerator SmoothScaleBug(Transform target, Vector3 targetScale)
    {
        Vector3 startScale = target.localScale;

        for (float elapsed = 0f; elapsed < _growDuration; elapsed += Time.deltaTime)
        {
            if (target == null) yield break;

            float progress = Mathf.SmoothStep(0f, 1f, elapsed / _growDuration);
            target.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }

        if (target != null) target.localScale = targetScale;
        _scaleRoutine = null;
    }

    #endregion

    #region Kaçış

    /// <summary>
    /// Şişen böcek kafesi kırdığında çağrılır: envanterden ve vakum sayacından düşer,
    /// normal boyda çıldırmış bir düşman olarak kamyonda kalır.
    /// </summary>
    /// <param name="visual">Kaçan böceğin kafes mantığı.</param>
    public void HandleBugEscaped(FedBugVisual visual)
    {
        Debug.LogWarning($"[ESCAPE] {visual.Data.bugName} kafesi kırdı ve kaçtı! Çıldırmış halde kamyonun yanında.");

        // Düşmana dönüşen nesneyi artık büyütme; kafes referansını bırak.
        if (_scaleRoutine != null)
        {
            StopCoroutine(_scaleRoutine);
            _scaleRoutine = null;
        }
        _spawnedBugVisual = null;
        _spawnedBugLogic = null;
        _cagedBugData = null;

        if (BugInventory.Instance != null) BugInventory.Instance.RemoveCapturedBug(visual.Data);

        // "Vakum: X / 5" sayacı hemen bir azalır.
        VacuumTool vacuum = FindVacuum();
        if (vacuum != null) vacuum.ReleaseOne(visual.Data);

        visual.BecomeEnemy();
    }

    #endregion

    #region Satış

    /// <summary>
    /// Envanterdeki böcekleri satar. Değer: baseValue; Horror yakalandıysa dangerMultiplier ile,
    /// beslendiyse yemek çarpanı ile çarpılır. Satıştan sonra envanter, vakum ve görseller sıfırlanır.
    /// </summary>
    public void SellCapturedBugs()
    {
        BugInventory inventory = BugInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("TruckZone: Sahnede BugInventory yok, satış yapılamadı.", this);
            return;
        }

        DestroyCagedVisual();

        List<CapturedBugInfo> bugs = inventory.GetCapturedBugs();
        if (bugs.Count == 0) return;

        float totalValue = CalculateTotalValue(bugs, out int bugCount);
        int payout = Mathf.RoundToInt(totalValue);

        PayPlayer(payout);
        Debug.Log($"[Kamyonet] {bugCount} böcek satıldı, kazanç: ${payout}");

        ResetAfterSale(inventory);
    }

    /// <summary>Verilen böcek listesinin toplam satış değerini hesaplar.</summary>
    /// <param name="bugs">Satılacak yakalanmış böcekler.</param>
    /// <param name="bugCount">Toplam böcek adedi (çıktı).</param>
    /// <returns>Toplam satış değeri.</returns>
    private float CalculateTotalValue(List<CapturedBugInfo> bugs, out int bugCount)
    {
        float total = 0f;
        bugCount = 0;

        for (int i = 0; i < bugs.Count; i++)
        {
            CapturedBugInfo info = bugs[i];
            if (info.data == null) continue;

            float value = info.data.baseValue;
            if (info.stateAtCapture == BugState.Horror)
            {
                value *= info.data.dangerMultiplier;
            }

            total += value * info.valueMultiplier * info.count;
            bugCount += info.count;
        }
        return total;
    }

    /// <summary>Kazancı oyuncuya ekler; GameManager yoksa sadece uyarı verir.</summary>
    private void PayPlayer(int payout)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMoney(payout);
            return;
        }

        Debug.LogWarning("TruckZone: GameManager sahnede yok, para sadece loglandı.", this);
    }

    /// <summary>Satıştan sonra envanteri, vakum haznesini ve ev görünümünü sıfırlar.</summary>
    private void ResetAfterSale(BugInventory inventory)
    {
        inventory.ClearInventory();

        VacuumTool vacuum = FindVacuum();
        if (vacuum != null) vacuum.Empty();

        // Satıştan sonra ev tekrar parlak çizgi film renklerine döner.
        if (AnomalicVFXManager.Instance != null) AnomalicVFXManager.Instance.TriggerAnomalicVFX(false);
    }

    #endregion

    #region Yardımcılar

    /// <summary>Kafesteki görseli ve devam eden büyüme animasyonunu temizler.</summary>
    private void DestroyCagedVisual()
    {
        if (_scaleRoutine != null)
        {
            StopCoroutine(_scaleRoutine);
            _scaleRoutine = null;
        }

        if (_spawnedBugVisual != null) Destroy(_spawnedBugVisual);

        _spawnedBugVisual = null;
        _spawnedBugLogic = null;
        _cagedBugData = null;
    }

    /// <summary>Sahnedeki vakum aletini (kapalı olsa da) bulur.</summary>
    private VacuumTool FindVacuum()
    {
        return FindFirstObjectByType<VacuumTool>(FindObjectsInactive.Include);
    }

    /// <summary>Çarpan nesnenin oyuncu olup olmadığını (tag veya PlayerController ile) kontrol eder.</summary>
    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(PlayerTag) || other.GetComponentInParent<PlayerController>() != null;
    }

    #endregion
}
