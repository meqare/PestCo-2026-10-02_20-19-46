using UnityEngine;
using UnityEngine.InputSystem;

// Böcek, örümcek ve yuva doğumlarını tek yerden açıp kapatır.
// Sahnede bu bileşen yoksa doğumlar her zaman açıktır. Açıkken ya da kapalıyken Play modunda
// Inspector'daki "Spawns Enabled" kutusu da canlı çalışır; oyun içinde toggleKey (varsayılan F10) ile de değişir.
public class SpawnSwitch : MonoBehaviour
{
    // Bileşen yoksa doğumlar açık kalsın.
    public static bool Enabled { get; private set; } = true;

    [SerializeField] private bool spawnsEnabled = false;
    [SerializeField] private Key toggleKey = Key.F10;

    [Header("Mutfak hamamböcekleri (sahnedeki HouseRoaches)")]
    [SerializeField] private bool houseRoachesEnabled = true;
    [SerializeField] private Key roachToggleKey = Key.F8;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Enabled = true;
    }

    private GameObject houseRoaches;

    private void Awake()
    {
        Enabled = spawnsEnabled;
        houseRoaches = GameObject.Find("HouseRoaches"); // pasif olunca bulunamaz; o yüzden burada önbelleğe alınır
        ApplyHouseRoaches();
    }

    private void OnDestroy()
    {
        Enabled = true;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Enabled = spawnsEnabled;
            ApplyHouseRoaches();
        }
    }

    private void ApplyHouseRoaches()
    {
        if (houseRoaches != null)
            houseRoaches.SetActive(houseRoachesEnabled);
    }

    private void Update()
    {
        var keyboard = PestCo.Truck.PestPC.IsOpen ? null : Keyboard.current; // PC açıkken tuşlar oyuna gitmesin
        if (keyboard == null) return;

        if (keyboard[roachToggleKey].wasPressedThisFrame)
        {
            houseRoachesEnabled = !houseRoachesEnabled;
            ApplyHouseRoaches();
            Debug.Log("[ROACH] Mutfak hamamböcekleri " + (houseRoachesEnabled ? "açıldı" : "kapatıldı"));
        }

        if (!keyboard[toggleKey].wasPressedThisFrame) return;

        spawnsEnabled = !spawnsEnabled;
        Enabled = spawnsEnabled;
        Debug.Log("[SPAWN] Doğumlar " + (Enabled ? "açıldı" : "durduruldu"));
    }
}
