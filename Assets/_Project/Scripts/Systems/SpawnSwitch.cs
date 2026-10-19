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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Enabled = true;
    }

    private void Awake()
    {
        Enabled = spawnsEnabled;
    }

    private void OnDestroy()
    {
        Enabled = true;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            Enabled = spawnsEnabled;
    }

    private void Update()
    {
        var keyboard = PestCo.Truck.PestPC.IsOpen ? null : Keyboard.current; // PC açıkken tuşlar oyuna gitmesin
        if (keyboard == null || !keyboard[toggleKey].wasPressedThisFrame) return;

        spawnsEnabled = !spawnsEnabled;
        Enabled = spawnsEnabled;
        Debug.Log("[SPAWN] Doğumlar " + (Enabled ? "açıldı" : "durduruldu"));
    }
}
