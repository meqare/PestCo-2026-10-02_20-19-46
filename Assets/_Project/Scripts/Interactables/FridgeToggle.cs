using System.Collections;
using UnityEngine;

// E tuşuyla buzdolabı kapısını açıp kapatır. Kapı (doorPivot) menteşe noktasında Y ekseninde döner.
// Update'te iş yapmaz, sadece Interact() çağrılınca bir coroutine çalışır. Açıkken içerideki ışık yanar.
public class FridgeToggle : MonoBehaviour, IInteractable
{
    [Header("Kapı")]
    [Tooltip("Menteşe noktasındaki obje. Kapı mesh'i bunun çocuğudur; bu obje Y ekseninde döner.")]
    [SerializeField] private Transform doorPivot;
    [Tooltip("Açıkken yerel Y dönüşü (derece). İşaret, kapının hangi yöne açılacağını belirler.")]
    [SerializeField] private float openAngle = 100f;
    [SerializeField, Min(0.05f)] private float animationDuration = 0.55f;
    [SerializeField] private bool isOpen = false;

    [Header("İç ışık (isteğe bağlı)")]
    [Tooltip("Kapı açılınca aktif olan obje (LightCuller aktif olmayan ışıkları yönetmez).")]
    [SerializeField] private GameObject innerLight;

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
        if (doorPivot != null) KinematicBody.Ensure(doorPivot); // dönen kapak (FPS)
        currentAngle = isOpen ? openAngle : 0f;
        ApplyAngle();
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
            ApplyAngle();
            yield return null;
        }
        currentAngle = target;
        ApplyAngle();
        animation = null;
    }

    private void ApplyAngle()
    {
        if (doorPivot != null) doorPivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
        if (innerLight != null) innerLight.SetActive(Mathf.Abs(currentAngle) > 3f);
    }
}
