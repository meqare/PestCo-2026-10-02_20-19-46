using UnityEngine;
using UnityEngine.InputSystem;

// Sprey aletinin üzerinde durur. Sol tık basılıyken partikül + ses, kapasite azalır.
public class SprayVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Boş bırakılırsa child objelerde aranır.")]
    [SerializeField] private ParticleSystem sprayParticles;
    [SerializeField] private AudioSource spraySound;

    [Header("Capacity")]
    [SerializeField, Min(1f)] private float maxCapacity = 100f;
    [SerializeField, Min(0f)] private float drainRate = 8f;

    public float CapacityPercent => currentCapacity / maxCapacity;

    private float currentCapacity;
    private bool isSpraying;

    // Referansları alır, kapasiteyi doldurur ve başlangıçta emisyonu kapatır.
    private void Awake()
    {
        if (sprayParticles == null)
            sprayParticles = GetComponentInChildren<ParticleSystem>();

        currentCapacity = maxCapacity;

        if (sprayParticles != null)
        {
            var emission = sprayParticles.emission;
            emission.enabled = false;
        }
    }

    // Her kare: tuş basılı mı ve kapasite var mı, ona göre püskürtmeyi aç/kapat.
    private void Update()
    {
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

        if (sprayParticles != null)
        {
            var emission = sprayParticles.emission;
            emission.enabled = true;
            if (!sprayParticles.isPlaying)
                sprayParticles.Play();
        }

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

        if (sprayParticles != null)
        {
            var emission = sprayParticles.emission;
            emission.enabled = false;
        }

        if (spraySound != null)
            spraySound.Stop();
    }

    // Kutuyu tamamen doldurur.
    public void Refill()
    {
        currentCapacity = maxCapacity;
    }
}
