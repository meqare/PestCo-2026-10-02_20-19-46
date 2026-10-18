using System.Collections;
using UnityEngine;

// E tuşuyla açılıp kapanan buzdolabı kapısı. Bu objeyi menteşe noktası olarak Y ekseninde döndürür.
// Update'te iş yapmaz, sadece Interact() çağrılınca bir coroutine çalışır.
public class FridgeDoor : MonoBehaviour, IInteractable
{
    [Header("Hareket")]
    [Tooltip("Açıkken Y ekseni etrafında dönülecek açı (derece). İşaret, kapının hangi yöne açılacağını belirler.")]
    [SerializeField] private float openAngle = -100f;
    [SerializeField, Min(0.05f)] private float animationDuration = 0.5f;
    [SerializeField] private bool isOpen = false;

    [Header("Metin")]
    [SerializeField] private string openPrompt = "[E] Buzdolabını Aç";
    [SerializeField] private string closePrompt = "[E] Buzdolabını Kapat";

    [Header("Ses")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.6f;
    [SerializeField, Min(0.1f)] private float maxDistance = 8f;

    private Coroutine animation;
    private float currentAngle;

    private void Awake()
    {
        KinematicBody.Ensure(gameObject); // dönen kapak collider'ı statik sayılmasın (FPS)
        currentAngle = isOpen ? openAngle : 0f;
        transform.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
    }

    public string GetPrompt() => isOpen ? closePrompt : openPrompt;

    public void Interact(PlayerInteraction player)
    {
        isOpen = !isOpen;
        AnomalySounds.PlayAt(isOpen ? AnomalySounds.DoorOpen : AnomalySounds.DoorClose,
            transform.position, soundVolume, isOpen ? 0.9f : 0.8f, maxDistance);

        if (animation != null) StopCoroutine(animation);
        animation = StartCoroutine(Swing(isOpen ? openAngle : 0f));
    }

    private IEnumerator Swing(float target)
    {
        float start = currentAngle;
        for (float t = 0f; t < animationDuration; t += Time.deltaTime)
        {
            currentAngle = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / animationDuration));
            transform.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
            yield return null;
        }
        currentAngle = target;
        transform.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
        animation = null;
    }
}
