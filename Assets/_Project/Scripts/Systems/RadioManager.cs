using System.Collections;
using TMPro;
using UnityEngine;

// Telsiz: mızmız sevkiyat operatörünün (patron) konuşmalarını gösterir.
// Henüz ses dosyası yok: metin konsola yazılır ve atanmışsa ekrandaki altyazıya basılır.
public class RadioManager : MonoBehaviour
{
    public static RadioManager Instance { get; private set; }

    [Header("Altyazı")]
    [Tooltip("Ekrandaki altyazı yazısı (boşsa sadece konsola yazılır).")]
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField, Min(0.5f)] private float displayDuration = 5f;

    private Coroutine hideRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (subtitleText != null) subtitleText.text = string.Empty;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Telsiz mesajını gösterir; yeni mesaj eskisinin yerini alır.
    public void PlayRadioSubtitle(string message)
    {
        Debug.Log($"[RADIO - BOSS]: '{message}'");

        if (subtitleText == null) return;

        subtitleText.text = "BOSS: " + message;
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(displayDuration);
        subtitleText.text = string.Empty;
        hideRoutine = null;
    }
}
