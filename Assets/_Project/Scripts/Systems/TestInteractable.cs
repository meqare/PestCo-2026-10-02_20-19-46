using UnityEngine;

public class TestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Test Küpü";

    public void Interact(PlayerInteraction oyuncu)
    {
        Debug.Log($"{name} ile etkileşime girildi ({oyuncu.name})", this);
    }

    public string GetPrompt() => prompt;
}
