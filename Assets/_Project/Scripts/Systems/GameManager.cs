using UnityEngine;

// Oyuncu verisini (PlayerData) tutar, para/iş işlemlerini yapar ve PestCoSaveSystem ile kaydeder.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerData Data { get; private set; } = new PlayerData();

    // Singleton'ı kurar.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Kayıt varsa yükler (PestCoSaveSystem Awake'i bitmiş olur).
    private void Start()
    {
        if (PestCoSaveSystem.Instance != null && PestCoSaveSystem.Instance.HasSave())
            Data = PestCoSaveSystem.Instance.Load();
    }

    // Oyuncuya para ekler ve kaydeder.
    public void AddMoney(int amount)
    {
        Data.money += amount;
        SaveNow();
    }

    // Yeterli para varsa harcar, yoksa false döner.
    public bool SpendMoney(int amount)
    {
        if (Data.money < amount) return false;
        Data.money -= amount;
        SaveNow();
        return true;
    }

    // İş tamamlandığında kazancı ve istatistikleri işler, kaydeder.
    public void RegisterJobComplete(int earnedMoney, int pestsKilled)
    {
        Data.money += earnedMoney;
        Data.completedJobs++;
        Data.totalBugsKilled += pestsKilled;
        SaveNow();
    }

    // Güncel veriyi diske yazar.
    public void SaveNow()
    {
        if (PestCoSaveSystem.Instance != null)
            PestCoSaveSystem.Instance.Save(Data);
        else
            Debug.LogError("GameManager: PestCoSaveSystem sahnede yok, kaydedilemedi.");
    }
}
