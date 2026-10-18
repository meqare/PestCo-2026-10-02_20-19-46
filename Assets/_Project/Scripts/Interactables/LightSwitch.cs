using UnityEngine;

// Duvardaki ışık düğmesi: E ile odanın ışığını açar/kapatır.
public class LightSwitch : MonoBehaviour, IInteractable
{
    [SerializeField] private RoomLight room;
    [SerializeField] private string onPrompt = "[E] Işığı Kapat";
    [SerializeField] private string offPrompt = "[E] Işığı Aç";
    [Tooltip("Basılınca eğilen düğme parçası (yerel X ekseninde döner).")]
    [SerializeField] private Transform rocker;
    [SerializeField] private float rockerAngle = 14f;
    [SerializeField] private AudioClip clickSound;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.6f;

    private void Start() => UpdateVisual();

    public void Interact(PlayerInteraction oyuncu)
    {
        if (room == null)
        {
            Debug.LogError("LightSwitch: oda (RoomLight) atanmamış!", this);
            return;
        }
        room.Toggle();
        UpdateVisual();
        if (clickSound != null) AudioSource.PlayClipAtPoint(clickSound, transform.position, clickVolume);
    }

    public string GetPrompt() => room != null && room.IsOn ? onPrompt : offPrompt;

    private void UpdateVisual()
    {
        if (rocker == null || room == null) return;
        rocker.localRotation = Quaternion.Euler(room.IsOn ? -rockerAngle : rockerAngle, 0f, 0f);
    }
}
