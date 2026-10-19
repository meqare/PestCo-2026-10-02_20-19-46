using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Kamyondaki cam fanus rafı: altındaki bölmelerin (BugCage) toplamını gösterir, oyuncu yakındayken
// satış tuşuyla hepsini birden satar.
public class BugShelf : MonoBehaviour
{
    [SerializeField] private Key sellKey = Key.B;
    [Tooltip("Oyuncu rafa bu kadar yakınken (m, yatay) satış tuşu çalışır.")]
    [SerializeField, Min(0.5f)] private float sellRadius = 2.5f;
    [Tooltip("Rafın üst tabelası (boş bırakılabilir).")]
    [SerializeField] private TMP_Text sign;
    [SerializeField] private string title = "BÖCEK FANUSLARI";

    private BugCage[] cages;
    private Transform player;
    private bool playerNear;
    private int shownTotal = -1;
    private bool shownNear;

    private void Start()
    {
        cages = GetComponentsInChildren<BugCage>();
        var controller = FindFirstObjectByType<PlayerController>();
        if (controller != null) player = controller.transform;
    }

    private void Update()
    {
        if (player != null)
        {
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            playerNear = d.sqrMagnitude <= sellRadius * sellRadius;
        }

        int total = Mathf.RoundToInt(TotalValue());
        if (total != shownTotal || playerNear != shownNear)
        {
            shownTotal = total;
            shownNear = playerNear;
            if (sign != null)
                sign.text = playerNear && total > 0 ? title + "   " + sellKey + ": Hepsini sat ($" + total + ")" : title;
        }

        Keyboard keyboard = PestCo.Truck.PestPC.IsOpen ? null : Keyboard.current; // PC açıkken tuşlar oyuna gitmesin
        if (playerNear && total > 0 && keyboard != null && keyboard[sellKey].wasPressedThisFrame)
            SellAll();
    }

    private float TotalValue()
    {
        float total = 0f;
        if (cages == null) return total;
        foreach (BugCage cage in cages)
            if (cage != null) total += cage.TotalValue();
        return total;
    }

    public void SellAll()
    {
        int payout = Mathf.RoundToInt(TotalValue());
        int count = 0;
        foreach (BugCage cage in cages)
            if (cage != null) count += cage.SellContents();

        if (GameManager.Instance != null) GameManager.Instance.AddMoney(payout);
        else Debug.LogWarning("BugShelf: GameManager sahnede yok, para sadece loglandı.", this);

        Debug.Log($"[Fanus] {count} böcek satıldı, kazanç: ${payout}");
    }
}
