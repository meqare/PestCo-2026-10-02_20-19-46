using System.Collections;
using UnityEngine;

// E tuşuyla açılıp kapanan çekmece. Bu objeyi (ön yüz, tutamak ve iç kutu çocukları) openOffset kadar
// yerel uzayda kaydırır. Update'te iş yapmaz, sadece Interact() çağrılınca bir coroutine çalışır.
public class Drawer : MonoBehaviour, IInteractable
{
    [Header("Hareket")]
    [Tooltip("Açıkken, kapalı konuma göre yerel (parent) uzaydaki kayma miktarı.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 0f, -0.4f);
    [SerializeField, Min(0.05f)] private float animationDuration = 0.35f;
    [SerializeField] private bool isOpen = false;

    [Header("Ses")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.5f;
    [SerializeField, Min(0.1f)] private float maxDistance = 8f;

    private Vector3 closedPosition;
    private Coroutine animation;

    private void Awake()
    {
        closedPosition = transform.localPosition - (isOpen ? openOffset : Vector3.zero);
    }

    public string GetPrompt() => isOpen ? "[E] Çekmeceyi Kapat" : "[E] Çekmeceyi Aç";

    public void Interact(PlayerInteraction player)
    {
        isOpen = !isOpen;
        AnomalySounds.PlayAt(isOpen ? AnomalySounds.DoorOpen : AnomalySounds.DoorClose,
            transform.position, soundVolume, isOpen ? 1.35f : 1.2f, maxDistance);

        if (animation != null) StopCoroutine(animation);
        animation = StartCoroutine(Slide(closedPosition + (isOpen ? openOffset : Vector3.zero)));
    }

    private IEnumerator Slide(Vector3 target)
    {
        Vector3 start = transform.localPosition;
        for (float t = 0f; t < animationDuration; t += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / animationDuration));
            yield return null;
        }
        transform.localPosition = target;
        animation = null;
    }
}
