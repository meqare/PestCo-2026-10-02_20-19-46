using System.Collections;
using UnityEngine;

// E tuşuyla açılıp kapanan kamyonet arka kapısı (rulo kepenk). Lameller birlikte yukarı kalkar,
// tavana varınca üst üste yığılır. Etkileşim collider'ı (trigger) her zaman açık kalır;
// böylece kapı açıkken de içeriden ve dışarıdan kapatılabilir.
public class RollUpDoor : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("Lameller, alttan üste sırayla. Kapalı konumları sahnede verilen yerel Y değerleridir.")]
    [SerializeField] private Transform[] slats;
    [Tooltip("Kapalıyken geçişi engelleyen collider; açıkken kapatılır.")]
    [SerializeField] private Collider blocker;
    [SerializeField] private AudioSource audioSource;

    [Header("Animation")]
    [SerializeField, Min(0.1f)] private float animationDuration = 2.2f;
    [Tooltip("Bir lamelin yüksekliği.")]
    [SerializeField, Min(0.01f)] private float slatHeight = 0.19f;
    [Tooltip("Yığılan lameller arasındaki dikey boşluk.")]
    [SerializeField, Min(0f)] private float stackSpacing = 0.02f;

    [Header("State")]
    [SerializeField] private bool isOpen = false;

    [Header("Sounds (boşsa yer tutucu ses çalar)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    public bool IsOpen => isOpen;

    private float[] closedY;
    private float[] stackY;
    private float lift;          // 0 = kapalı, travel = tamamen açık
    private float travel;
    private Coroutine animation;

    // Kapalı ve yığılmış Y konumlarını hesaplar, başlangıç durumunu uygular.
    private void Awake()
    {
        int n = slats != null ? slats.Length : 0;
        closedY = new float[n];
        stackY = new float[n];
        for (int i = 0; i < n; i++)
        {
            KinematicBody.Ensure(slats[i]); // kayan lamel collider'ı statik sayılmasın (FPS)
            closedY[i] = slats[i].localPosition.y;
            // En üst lamel tavana değer, alttakiler altında yığılır.
            stackY[i] = -slatHeight * 0.5f - (n - 1 - i) * stackSpacing;
        }
        // Tüm lamellerin yığına varması için gereken kalkış: en alttaki lamelin yolu.
        travel = n > 0 ? stackY[0] - closedY[0] : 0f;

        lift = isOpen ? travel : 0f;
        Apply();
        if (blocker != null) blocker.enabled = !isOpen;
    }

    // E'ye basınca kapıyı açar veya kapatır.
    public void Interact(PlayerInteraction player)
    {
        isOpen = !isOpen;
        Play(isOpen ? openSound : closeSound, isOpen ? AnomalySounds.DoorOpen : AnomalySounds.DoorClose);

        // Kapanırken engel hemen devreye girer; açılırken animasyon bitince kalkar.
        if (!isOpen && blocker != null) blocker.enabled = true;

        if (animation != null) StopCoroutine(animation);
        animation = StartCoroutine(Animate(isOpen ? travel : 0f));
    }

    // Ekranda gösterilen etkileşim yazısı.
    public string GetPrompt()
    {
        return isOpen ? "[E] Kapıyı Kapat" : "[E] Kapıyı Aç";
    }

    // Kalkış miktarını hedefe yumuşakça götürür.
    private IEnumerator Animate(float target)
    {
        float start = lift;
        float duration = Mathf.Max(0.1f, animationDuration * Mathf.Abs(target - start) / Mathf.Max(0.01f, travel));
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            lift = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
            Apply();
            yield return null;
        }
        lift = target;
        Apply();
        if (isOpen && blocker != null) blocker.enabled = false;
        animation = null;
    }

    // Her lamel kalkışla yükselir, yığın seviyesine varınca durur.
    private void Apply()
    {
        for (int i = 0; i < slats.Length; i++)
        {
            Vector3 p = slats[i].localPosition;
            p.y = Mathf.Min(closedY[i] + lift, stackY[i]);
            slats[i].localPosition = p;
        }
    }

    // Verilen ses yoksa yer tutucu çalar.
    private void Play(AudioClip clip, AudioClip fallback)
    {
        AudioClip c = clip != null ? clip : fallback;
        if (c == null) return;
        if (audioSource != null) audioSource.PlayOneShot(c);
        else AnomalySounds.PlayAt(c, transform.position, 1f, 1f, 12f);
    }
}
