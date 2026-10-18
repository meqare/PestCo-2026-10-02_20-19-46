using System.Collections;
using UnityEngine;

// E tuşuyla açılıp kapanan çekmece. Bu objeyi (ön yüz, tutamak ve iç kutu çocukları) openOffset kadar
// yerel uzayda kaydırır. Update'te iş yapmaz, sadece Interact() çağrılınca bir coroutine çalışır.
public class Drawer : MonoBehaviour, IInteractable, IOpenableFurniture
{
    [Header("Hareket")]
    [Tooltip("Açıkken, kapalı konuma göre yerel (parent) uzaydaki kayma miktarı.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 0f, -0.4f);
    [SerializeField, Min(0.05f)] private float animationDuration = 0.35f;
    [SerializeField] private bool isOpen = false;

    [Header("Ses")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.5f;
    [SerializeField, Min(0.1f)] private float maxDistance = 8f;

    [Header("Saklanma")]
    [Tooltip("Bu çekmecede haşere saklanabiliyorsa saklanma noktası (oyuncu açınca haşere fırlar).")]
    [SerializeField] private CabinetHideSpot hideSpot;

    private Vector3 closedPosition;
    private Coroutine animation;

    /// <inheritdoc/>
    public bool IsOpen => isOpen;

    /// <inheritdoc/>
    public float OpenDuration => animationDuration;

    /// <summary>Saklanma noktasını kurulum aracının bağlaması için verir.</summary>
    public void SetHideSpot(CabinetHideSpot spot) => hideSpot = spot;

    /// <inheritdoc/>
    public void Open() => SetOpen(true);

    /// <inheritdoc/>
    public void Close() => SetOpen(false);

    private void Awake()
    {
        KinematicBody.Ensure(gameObject); // kayan çekmece collider'ı statik sayılmasın (FPS)
        closedPosition = transform.localPosition - (isOpen ? openOffset : Vector3.zero);
    }

    public string GetPrompt() => isOpen ? "[E] Çekmeceyi Kapat" : "[E] Çekmeceyi Aç";

    public void Interact(PlayerInteraction player)
    {
        bool willOpen = !isOpen;
        SetOpen(willOpen);

        // İçinde saklanan haşere varsa oyuncu açınca fırlayıp kaçar.
        if (willOpen && hideSpot != null) hideSpot.ExposeOccupant();
    }

    // Durumu değiştirir, sesi çalar ve kayma animasyonunu başlatır (oyuncu ve haşere ortak kullanır).
    private void SetOpen(bool open)
    {
        if (isOpen == open) return;

        isOpen = open;
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
