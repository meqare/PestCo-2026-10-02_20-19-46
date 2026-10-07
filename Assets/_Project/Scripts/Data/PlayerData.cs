// Oyuncunun kaydedilen tüm verisini tutan tek sınıf.
[System.Serializable]
public class PlayerData
{
    public float money = 500f;        // Başlangıç parası
    public int reputation = 0;        // İtibar puanı
    public int completedJobs = 0;     // Toplam iş sayısı
    public int currentRegion = 0;     // Hangi bölgedesin (0-4)

    // Hangi aletler açık?
    public bool hasSpray = true;
    public bool hasVacuum = false;
    public bool hasTrap = false;
    public bool hasRacket = false;
    public bool hasFlamethrower = false;

    // İstatistikler (Steam achievement için)
    public int totalBugsKilled = 0;
    public int anomaliesFound = 0;
    public int wrongDiagnoses = 0;
}
