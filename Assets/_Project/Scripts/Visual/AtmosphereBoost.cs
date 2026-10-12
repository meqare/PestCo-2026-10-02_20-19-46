using UnityEngine;
using UnityEngine.Rendering;

// Ana Global Volume'un üstüne biner: daha yüksek kontrast/doygunluk, sıcak-soğuk split toning, biraz daha parlak bloom.
// Bileşen kapanırsa ağırlık 0'a iner ve orijinal görünüm geri gelir. Gölge/ışık eklemez, sadece post-process.
[RequireComponent(typeof(Volume))]
[DisallowMultipleComponent]
public class AtmosphereBoost : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float targetWeight = 1f;
    [Tooltip("Açılış/kapanış geçiş hızı (saniyede ağırlık).")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 2f;

    private Volume volume;
    private bool on;

    private void Awake() { volume = GetComponent<Volume>(); }

    private void OnEnable()
    {
        on = true;
        if (volume == null) volume = GetComponent<Volume>();
        volume.enabled = true;
        if (!Application.isPlaying) volume.weight = targetWeight;
    }

    private void OnDisable()
    {
        on = false;
        if (volume == null) volume = GetComponent<Volume>();
        if (volume != null) volume.weight = 0f;
    }

    private void Update()
    {
        if (!on || volume == null) return;
        volume.weight = Mathf.MoveTowards(volume.weight, targetWeight, fadeSpeed * Time.unscaledDeltaTime);
    }
}
