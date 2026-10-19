using System;
using System.Text;
using UnityEngine;

// Tek bir sabit siparişin bilgisi ve gereksinim kontrolü. Değerler KitchenObjectProperties'ten okunur, kopyalanmaz.
[Serializable]
public class KitchenOrder
{
    public string title = "Şefin Tabağı";
    [TextArea] public string description = "Şefe sessiz, sıcak ve ağır bir tabak teslim et.";
    [Tooltip("Weight (kg) bu değerden YÜKSEK olmalı.")]
    public float minWeight = 2f;
    [Tooltip("Heat bu değerden YÜKSEK olmalı.")]
    public float minHeat = 50f;
    [Tooltip("Sound bu değerden DÜŞÜK olmalı.")]
    public float maxSound = 10f;
    [Tooltip("Başarılı teslimatta verilecek para.")]
    [Min(0)] public int reward = 150;

    // Tüm gereksinimler karşılanıyorsa true; değilse eksikleri 'missing' içine yazar.
    public bool Check(KitchenObjectProperties p, StringBuilder missing)
    {
        bool ok = true;
        if (!(p.Weight > minWeight)) { ok = false; Append(missing, $"Ağırlık {p.Weight:0.0} (gerekli > {minWeight:0.0})"); }
        if (!(p.Heat > minHeat)) { ok = false; Append(missing, $"Isı {p.Heat:0} (gerekli > {minHeat:0})"); }
        if (!(p.Sound < maxSound)) { ok = false; Append(missing, $"Ses {p.Sound:0} (gerekli < {maxSound:0})"); }
        return ok;
    }

    private static void Append(StringBuilder sb, string s)
    {
        if (sb.Length > 0) sb.Append(", ");
        sb.Append(s);
    }
}

// Aktif siparişi yönetir: teslimatı değerlendirir, siparişi yalnızca bir kez tamamlar ve ödülü
// merkezî para sistemine (GameManager) verir. Teslimat bölgesi ve arayüz bunu kullanır.
public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance { get; private set; }

    [SerializeField] private KitchenOrder order = new KitchenOrder();

    public KitchenOrder ActiveOrder => order;
    public bool IsCompleted { get; private set; }
    public int EarnedReward { get; private set; }

    // (başarılı mı, oyuncuya gösterilecek mesaj)
    public event Action<bool, string> DeliveryResult;

    private readonly StringBuilder missing = new StringBuilder(128);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("OrderManager: sahnede ikinci bir OrderManager var, bu kapatıldı.", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Teslimat bölgesi çağırır. Ödül yalnızca ilk başarılı teslimatta verilir.
    public bool TryDeliver(KitchenObjectProperties item)
    {
        if (item == null) return false;

        if (IsCompleted)
        {
            Report(false, "Bu sipariş zaten tamamlandı.");
            return false;
        }

        missing.Clear();
        if (!order.Check(item, missing))
        {
            Report(false, $"Teslimat reddedildi ({item.name}). Eksik: {missing}");
            return false;
        }

        IsCompleted = true;
        EarnedReward = order.reward;
        if (GameManager.Instance != null) GameManager.Instance.AddMoney(order.reward);
        else Debug.LogWarning("OrderManager: GameManager sahnede yok, ödül sadece arayüzde gösterildi.", this);

        Report(true, $"Sipariş tamamlandı! +{order.reward}$");
        return true;
    }

    private void Report(bool success, string message)
    {
        DeliveryResult?.Invoke(success, message);
    }
}
