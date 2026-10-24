using TMPro;
using UnityEngine;

// Geliştirme için: oyuncunun parasını ve tamamlanan iş sayısını ekranda gösterir.
public class DebugHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI jobsText;

    // Her karede GameManager'daki güncel veriyi yazılara basar.
    private void Update()
    {
        if (GameManager.Instance == null) return;
        PlayerData data = GameManager.Instance.Data;
        // Para rozeti (PestHudGorunum) başlığı kendisi yazar; burada sadece değerler.
        if (moneyText != null) moneyText.SetText("${0}", Mathf.Floor(data.money));
        if (jobsText != null) jobsText.SetText("Tamamlanan iş: {0}", data.completedJobs);
    }
}
