using System.Collections;
using UnityEngine;

// E tuşuyla açılıp kapanan kapı. Menteşe (doorPivot) etrafında Lerp ile döner; Update'te iş yapmaz,
// sadece Interact() çağrılınca bir coroutine çalışır.
public class Door : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("Kapının menteşe noktası. Panel bunun çocuğudur, bu obje Y ekseninde döner.")]
    [SerializeField] private Transform doorPivot;
    [SerializeField] private AudioSource audioSource;

    [Header("Animation")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField, Min(0.05f)] private float animationDuration = 0.5f;
    [Tooltip("Açıkken oyuncudan uzağa doğru açılsın (kapı oyuncunun üstüne gelmesin).")]
    [SerializeField] private bool swingAwayFromPlayer = true;

    [Header("State")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private bool isLocked = false;

    [Header("Sounds (boşsa yer tutucu ses çalar)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioClip lockedSound;

    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;

    private Coroutine animation;
    private float currentAngle;

    private void Awake()
    {
        if (doorPivot == null)
            doorPivot = transform;
        // Başlangıç açısı: açık başlıyorsa openAngle, değilse 0.
        currentAngle = isOpen ? openAngle : 0f;
        doorPivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
    }

    public void Interact(PlayerInteraction player)
    {
        if (isLocked)
        {
            Play(lockedSound, AnomalySounds.DoorLocked);
            return;
        }
        ToggleDoor(player);
    }

    public string GetPrompt()
    {
        if (isLocked) return "Kilitli";
        return isOpen ? "[E] Kapat" : "[E] Aç";
    }

    private void ToggleDoor(PlayerInteraction player)
    {
        isOpen = !isOpen;

        float target = 0f;
        if (isOpen)
        {
            float sign = 1f;
            // Oyuncu kapının hangi yüzündeyse ters yöne açılır (panel +X'e uzanır; +Y dönüşü -Z tarafına süpürür).
            if (swingAwayFromPlayer && player != null)
            {
                Vector3 local = transform.InverseTransformPoint(player.transform.position);
                sign = local.z > 0f ? 1f : -1f;
            }
            target = openAngle * sign;
        }

        Play(isOpen ? openSound : closeSound, isOpen ? AnomalySounds.DoorOpen : AnomalySounds.DoorClose);

        if (animation != null)
            StopCoroutine(animation);
        animation = StartCoroutine(AnimateDoor(target));
    }

    // Mevcut açıdan hedef açıya animationDuration sürede yumuşakça döner.
    private IEnumerator AnimateDoor(float targetAngle)
    {
        float start = currentAngle;
        for (float t = 0f; t < animationDuration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / animationDuration);
            currentAngle = Mathf.Lerp(start, targetAngle, k);
            doorPivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
            yield return null;
        }
        currentAngle = targetAngle;
        doorPivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
        animation = null;
    }

    // Verilen ses yoksa yer tutucu çalar; AudioSource yoksa konumda tek seferlik çalar. Hata vermez.
    private void Play(AudioClip clip, AudioClip fallback)
    {
        AudioClip c = clip != null ? clip : fallback;
        if (c == null) return;

        if (audioSource != null)
            audioSource.PlayOneShot(c);
        else
            AnomalySounds.PlayAt(c, doorPivot.position, 1f, 1f, 12f);
    }
}
