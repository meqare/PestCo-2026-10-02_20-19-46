using UnityEngine;
using UnityEngine.InputSystem;

// Sprey aletinin üzerinde durur. Sol tık basılıyken partikül + ses, kapasite azalır. R tuşu kutuyu doldurur.
public class SprayVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Sprey partikül sistem(ler)i. Boş bırakılırsa child objelerde aranır.")]
    [SerializeField] private ParticleSystem[] sprayLayers;
    [SerializeField] private AudioSource spraySound;

    [Header("Capacity")]
    [SerializeField, Min(1f)] private float maxCapacity = 100f;
    [SerializeField, Min(0f)] private float drainRate = 8f;

    public float CapacityPercent => currentCapacity / maxCapacity;
    public bool IsSpraying => isSpraying;

    private float currentCapacity;
    private bool isSpraying;

    // Referansları alır, kapasiteyi doldurur ve başlangıçta emisyonu kapatır.
    private void Awake()
    {
        if (sprayLayers == null || sprayLayers.Length == 0)
            sprayLayers = GetComponentsInChildren<ParticleSystem>(true);

        currentCapacity = maxCapacity;

        SetEmission(false);
    }

    // Her kare: tuş basılı mı ve kapasite var mı, ona göre püskürtmeyi aç/kapat.
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            Refill();

        var mouse = Mouse.current;
        bool wantsToSpray = mouse != null && mouse.leftButton.isPressed;
        bool canSpray = wantsToSpray && currentCapacity > 0f;

        if (canSpray && !isSpraying)
            StartSpray();
        else if (!canSpray && isSpraying)
            StopSpray();

        if (isSpraying)
            currentCapacity = Mathf.Max(0f, currentCapacity - drainRate * Time.deltaTime);
    }

    // Alet değiştirilip obje kapanırsa püskürtme ve ses takılı kalmasın.
    private void OnDisable()
    {
        if (isSpraying)
            StopSpray();
    }

    // Emisyonu açar, gerekirse sistemi oynatır ve sesi döngüde başlatır.
    private void StartSpray()
    {
        isSpraying = true;

        SetEmission(true);

        if (spraySound != null)
        {
            spraySound.loop = true;
            if (!spraySound.isPlaying)
                spraySound.Play();
        }
    }

    // Sadece emisyonu kapatır (Stop() değil), havadaki partiküller doğal söner.
    private void StopSpray()
    {
        isSpraying = false;

        SetEmission(false);

        if (spraySound != null)
            spraySound.Stop();
    }

    // Sprey katmanlarının emisyonunu açar/kapatır; açarken gerekirse oynatır.
    private void SetEmission(bool on)
    {
        if (sprayLayers != null)
        {
            for (int i = 0; i < sprayLayers.Length; i++)
                SetEmission(sprayLayers[i], on);
        }
    }

    private static void SetEmission(ParticleSystem ps, bool on)
    {
        if (ps == null) return;
        var emission = ps.emission;
        emission.enabled = on;
        if (on && !ps.isPlaying)
            ps.Play();
    }

    // Kutuyu tamamen doldurur.
    public void Refill()
    {
        currentCapacity = maxCapacity;
    }
}
