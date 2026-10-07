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
        if (moneyText != null) moneyText.text = $"Para: ${data.money:0}";
        if (jobsText != null) jobsText.text = $"Is: {data.completedJobs}";
    }
}
