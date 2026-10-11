using UnityEngine;

// Oyuncu E'ye basınca iş panosu arayüzünü açar.
public class JobBoardInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "[E] İş Panosu";

    public void Interact(PlayerInteraction oyuncu)
    {
        if (JobBoardUI.Instance == null)
        {
            Debug.LogError("JobBoardInteractable: Sahnede JobBoardUI yok!", this);
            return;
        }
        if (JobBoardUI.IsOpen) return; // açıkken tekrar E kontratları yenilemesin
        JobBoardUI.Instance.Open();
    }

    public string GetPrompt() => prompt;
}
