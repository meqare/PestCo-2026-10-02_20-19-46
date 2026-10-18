using UnityEngine;

// Oyuncu girince telsizden bir mesaj çaldırır (bir kez). Is Trigger açık bir collider gerekir.
public class RadioTrigger : MonoBehaviour
{
    [TextArea(2, 4)]
    [SerializeField] private string message = "Okay Cemil, you are in. Find the bug, spray it, and don't touch the cursed cookies.";

    private const string PlayerTag = "Player";
    private bool played;

    private void OnTriggerEnter(Collider other)
    {
        if (played) return;
        if (!other.CompareTag(PlayerTag) && other.GetComponentInParent<PlayerController>() == null) return;

        played = true;
        if (RadioManager.Instance != null) RadioManager.Instance.PlayRadioSubtitle(message);
    }
}
