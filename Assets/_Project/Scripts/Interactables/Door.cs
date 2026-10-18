using System.Collections;
using UnityEngine;

// E tuşuyla açılıp kapanan kapı. Menteşe (doorPivot) etrafında Lerp ile döner; Update'te iş yapmaz,
// sadece Interact() çağrılınca bir coroutine çalışır.
// Kapı durumu fizik (blockingCollider) ve occlusion (OcclusionPortal) ile senkron tutulur:
//  kapalı -> collider aktif, portal kapalı | açık -> collider pasif, portal açık.
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

    [Header("Sabit Açılar (isteğe bağlı)")]
    [Tooltip("Açıksa swingAwayFromPlayer yok sayılır; kapı closedAngle ile openedAngle arasında döner. Sahnede hazır yerleştirilmiş kapılar için.")]
    [SerializeField] private bool useExplicitAngles = false;
    [SerializeField] private float closedAngle = 0f;
    [SerializeField] private float openedAngle = 90f;

    [Header("Fizik / Occlusion (boşsa atlanır)")]
    [Tooltip("Sadece kapalıyken yolu kapatan collider. Etkileşim için ayrı bir trigger collider kullanın.")]
    [SerializeField] private Collider blockingCollider;
    [Tooltip("Kapı açıklığındaki OcclusionPortal. Açıkken arka alan çizilir, kapalıyken kesilir.")]
    [SerializeField] private OcclusionPortal portal;
    [Tooltip("Kapanırken oyuncu bu alandaysa kapı kapanmaz (içinde sıkışmasın).")]
    [SerializeField] private float clearanceMargin = 0.25f;

    [Header("State")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private bool isLocked = false;

    [Header("Sounds (boşsa yer tutucu ses çalar)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioClip lockedSound;

    [Header("Ses Ayarları")]
    [Tooltip("Kapı seslerinin çıkış seviyesi.")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
    [Tooltip("Her çalışta pitch bu aralıkta rastgele seçilir; tekrar eden sesler robotik durmasın.")]
    [SerializeField, Range(0.5f, 1.5f)] private float pitchMin = 0.95f;
    [SerializeField, Range(0.5f, 1.5f)] private float pitchMax = 1.05f;
    [Tooltip("Bu mesafenin içinde ses tam duyulur.")]
    [SerializeField, Min(0f)] private float minDistance = 1f;
    [Tooltip("Oyuncu bu mesafeden uzaktayken ses sıfıra iner (3D Linear rolloff).")]
    [SerializeField, Min(0.1f)] private float maxDistance = 12f;

    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;

    private Coroutine animation;
    private float currentAngle;

    private void Awake()
    {
        EnsureAudioSource();
        if (doorPivot == null)
            doorPivot = transform;
        KinematicBody.Ensure(doorPivot); // dönen kapı collider'ı statik sayılmasın (FPS)
        // Başlangıç açısı: açık başlıyorsa açık açı, değilse kapalı açı.
        float open = useExplicitAngles ? openedAngle : openAngle;
        float closed = useExplicitAngles ? closedAngle : 0f;
        currentAngle = isOpen ? open : closed;
        doorPivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
        ApplyState(isOpen);
    }

    // Fizik ve occlusion durumunu kapı durumuna eşitler.
    private void ApplyState(bool open)
    {
        if (blockingCollider != null) blockingCollider.enabled = !open;
        if (portal != null) portal.open = open;
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
        // Kapatmaya çalışırken oyuncu kapının süpürme alanındaysa iptal et.
        if (isOpen && PlayerInTheWay()) return;

        isOpen = !isOpen;

        float target = useExplicitAngles ? closedAngle : 0f;
        if (isOpen)
        {
            if (useExplicitAngles)
            {
                target = openedAngle;
            }
            else
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
        }

        Play(isOpen ? openSound : closeSound, isOpen ? AnomalySounds.DoorOpen : AnomalySounds.DoorClose);

        // Açılırken portal ve geçiş hemen açılır; kapanırken animasyon bitince kapanır.
        if (isOpen) ApplyState(true);

        if (animation != null)
            StopCoroutine(animation);
        animation = StartCoroutine(AnimateDoor(target));
    }

    // Kapalı pozdaki blockingCollider kutusunda oyuncu var mı? (kapı kapanmadan önce kontrol)
    private bool PlayerInTheWay()
    {
        if (blockingCollider is not BoxCollider box) return false;

        float closed = useExplicitAngles ? closedAngle : 0f;
        Transform parent = doorPivot.parent;
        Matrix4x4 parentToWorld = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 m = parentToWorld * Matrix4x4.TRS(doorPivot.localPosition, Quaternion.Euler(0f, closed, 0f), doorPivot.localScale);

        Vector3 center = m.MultiplyPoint3x4(box.center);
        Vector3 half = Vector3.Scale(box.size, m.lossyScale) * 0.5f + Vector3.one * clearanceMargin;
        var hits = Physics.OverlapBox(center, half, m.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.GetComponentInParent<CharacterController>() != null) return true;
        return false;
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
        if (!isOpen) ApplyState(false);
    }

    // AudioSource yoksa ekler ve ayarlarını kodda zorlar (3D, Play On Awake kapalı). Editör kurulumu da aynı alanları yazar.
    private void EnsureAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
    }

    // Verilen ses yoksa yer tutucu çalar. Önceki kapı sesi durdurulur (üst üste binmez), pitch her seferinde rastgele.
    private void Play(AudioClip clip, AudioClip fallback)
    {
        AudioClip c = clip != null ? clip : fallback;
        if (c == null) return;

        if (audioSource == null)
        {
            AnomalySounds.PlayAt(c, doorPivot.position, soundVolume, 1f, maxDistance);
            return;
        }

        audioSource.Stop();
        audioSource.clip = c;
        audioSource.volume = soundVolume;
        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.Play();
    }
}
