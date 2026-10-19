using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Yem mağazası: oyuncu alana girip satış tuşuna basınca elindeki tüm yemekleri parayla takas eder.
/// Bu nesnede "Is Trigger" açık bir Collider bulunmalıdır.
/// </summary>
public class FoodShopZone : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Satış")]
    [SerializeField] private Key _sellKey = Key.E;

    [Header("Tabela")]
    [Tooltip("Tezgâh üstündeki 3B yazı (durum ve satış bilgisi buraya basılır). Boşsa sadece konsola yazılır.")]
    [SerializeField] private TMP_Text _signText;
    [SerializeField] private string _title = "YEM MAĞAZASI";

    /// <summary>Oyuncu mağaza alanının içinde mi?</summary>
    public bool IsPlayerInside { get; private set; }

    /// <summary>Tabelayı ilk haline getirir.</summary>
    private void Start()
    {
        ShowIdleSign();
    }

    /// <summary>Oyuncu girince elindeki yemeklerin değerini tabelada gösterir.</summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        IsPlayerInside = true;
        ShowIdleSign();
    }

    /// <summary>Oyuncu çıkınca satış tuşu dinlemesi kapanır.</summary>
    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        IsPlayerInside = false;
    }

    /// <summary>Oyuncu alandaysa satış tuşunu dinler.</summary>
    private void Update()
    {
        if (!IsPlayerInside) return;

        Keyboard keyboard = PestCo.Truck.PestPC.IsOpen ? null : Keyboard.current; // PC açıkken tuşlar oyuna gitmesin
        if (keyboard != null && keyboard[_sellKey].wasPressedThisFrame)
        {
            SellAllFood();
        }
    }

    /// <summary>Elindeki tüm yemekleri satar, kazancı oyuncuya ekler ve tabelayı günceller.</summary>
    public void SellAllFood()
    {
        FoodInventory foods = FoodInventory.Instance;
        if (foods == null)
        {
            Debug.LogWarning("FoodShopZone: Sahnede FoodInventory yok, satış yapılamadı.", this);
            return;
        }

        int earned = 0;
        int soldCount = 0;
        foreach (FoodType food in System.Enum.GetValues(typeof(FoodType)))
        {
            while (foods.TryConsume(food))
            {
                earned += food.SellPrice();
                soldCount++;
            }
        }

        if (soldCount == 0)
        {
            SetSign($"{_title}\nSatacak yemin yok");
            return;
        }

        PayPlayer(earned);
        Debug.Log($"[Mağaza] {soldCount} yem satıldı, kazanç: ${earned}");
        SetSign($"{_title}\nSatıldı: +${earned}");
    }

    /// <summary>Kazancı oyuncuya ekler; GameManager yoksa sadece uyarı verir.</summary>
    private void PayPlayer(int amount)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMoney(amount);
            return;
        }

        Debug.LogWarning("FoodShopZone: GameManager sahnede yok, para sadece loglandı.", this);
    }

    /// <summary>Tabelaya elindeki yemeklerin toplam değerini ve satış tuşunu yazar.</summary>
    private void ShowIdleSign()
    {
        int value = CalculateHeldFoodValue();
        string info = value > 0 ? $"[{_sellKey}] Yemleri Sat (${value})" : "Satacak yemin yok";
        SetSign($"{_title}\n{info}");
    }

    /// <summary>Oyuncunun elindeki tüm yemeklerin toplam satış değerini hesaplar.</summary>
    /// <returns>Toplam değer; envanter yoksa 0.</returns>
    private int CalculateHeldFoodValue()
    {
        FoodInventory foods = FoodInventory.Instance;
        if (foods == null) return 0;

        int total = 0;
        foreach (FoodType food in System.Enum.GetValues(typeof(FoodType)))
        {
            total += foods.GetCount(food) * food.SellPrice();
        }
        return total;
    }

    /// <summary>Tabela yazısını ayarlar (tabela atanmamışsa hiçbir şey yapmaz).</summary>
    private void SetSign(string message)
    {
        if (_signText != null) _signText.text = message;
    }

    /// <summary>Çarpan nesnenin oyuncu olup olmadığını (tag veya PlayerController ile) kontrol eder.</summary>
    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(PlayerTag) || other.GetComponentInParent<PlayerController>() != null;
    }
}
