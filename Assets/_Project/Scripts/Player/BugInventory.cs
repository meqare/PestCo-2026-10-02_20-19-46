using System;
using System.Collections.Generic;
using UnityEngine;

// Yakalanan bir böcek türünün, yakalandığı durumdaki (Cute/Horror) adedi.
[Serializable]
public class CapturedBugInfo
{
    public BugData data;
    public BugState stateAtCapture;
    public int count;
    [Tooltip("Beslenen böceklerde satış değeri çarpanı (1 = beslenmemiş).")]
    public float valueMultiplier = 1f;
}

// Vakumla yakalanan böcekleri tür ve yakalanma durumuna göre sayar (sırt tankı).
// Horror böcekler sonra daha değerli olacağı için durum da saklanır.
public class BugInventory : MonoBehaviour
{
    public static BugInventory Instance { get; private set; }

    // Bir böcek yemek yediğinde tetiklenir (kamyondaki görsel bunu dinler).
    public static event Action<BugData, BugState, FoodType> BugFed;

    // Envanter içeriği değiştiğinde tetiklenir (ekrandaki envanter paneli bunu dinler).
    public event Action Changed;

    [SerializeField] private List<CapturedBugInfo> capturedBugs = new List<CapturedBugInfo>();

    public int TotalCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < capturedBugs.Count; i++)
                total += capturedBugs[i].count;
            return total;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Aynı tür ve durumdaki böceğin adedini artırır; yoksa yeni kayıt açar.
    public void AddBugToInventory(BugData data, BugState stateAtCapture)
    {
        if (data == null) return;

        for (int i = 0; i < capturedBugs.Count; i++)
        {
            CapturedBugInfo info = capturedBugs[i];
            if (info.data == data && info.stateAtCapture == stateAtCapture && info.valueMultiplier == 1f)
            {
                info.count++;
                Changed?.Invoke();
                return;
            }
        }

        capturedBugs.Add(new CapturedBugInfo { data = data, stateAtCapture = stateAtCapture, count = 1 });
        Changed?.Invoke();
    }

    // Yakalanmış bir böceği (o türden bir adet) yemekle besler: satış değeri yemeğin çarpanı kadar artar.
    // Yemek yoksa ya da o türden böcek yoksa hiçbir şey olmaz.
    public void FeedCapturedBug(BugData bug, FoodType food)
    {
        if (bug == null) return;

        FoodInventory foodInventory = FoodInventory.Instance;
        if (foodInventory == null || foodInventory.GetCount(food) <= 0) return;

        // Aynı böceği beslemeye devam eder: önce en çok beslenmiş olanı seçer (kafesteki şişen böcek).
        CapturedBugInfo source = null;
        for (int i = 0; i < capturedBugs.Count; i++)
        {
            CapturedBugInfo info = capturedBugs[i];
            if (info.data != bug || info.count <= 0) continue;
            if (source == null || info.valueMultiplier > source.valueMultiplier) source = info;
        }
        if (source == null) return;

        foodInventory.TryConsume(food);

        float newMultiplier = source.valueMultiplier * food.ValueMultiplier();
        BugState state = source.stateAtCapture;
        source.count--;
        if (source.count <= 0) capturedBugs.Remove(source);

        capturedBugs.Add(new CapturedBugInfo { data = bug, stateAtCapture = state, count = 1, valueMultiplier = newMultiplier });
        Debug.Log($"[Mutasyon] {bug.bugName} {food} yedi ve şişmanladı! Satış değeri x{newMultiplier:0.#}");
        Changed?.Invoke();
        BugFed?.Invoke(bug, state, food);
    }

    // Kafesten kaçan böceği envanterden çıkarır (en çok beslenmiş olanı).
    public void RemoveCapturedBug(BugData bug)
    {
        CapturedBugInfo target = null;
        for (int i = 0; i < capturedBugs.Count; i++)
        {
            CapturedBugInfo info = capturedBugs[i];
            if (info.data != bug || info.count <= 0) continue;
            if (target == null || info.valueMultiplier > target.valueMultiplier) target = info;
        }
        if (target == null) return;

        target.count--;
        if (target.count <= 0) capturedBugs.Remove(target);
        Changed?.Invoke();
    }

    // Envanterin canlı listesini verir (okumak için; değiştirmeyin).
    public List<CapturedBugInfo> GetCapturedBugs()
    {
        return capturedBugs;
    }

    // Satış veya boşaltmada envanteri temizler.
    public void ClearInventory()
    {
        capturedBugs.Clear();
        Changed?.Invoke();
    }
}
