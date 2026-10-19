using UnityEngine;

namespace PestCo.Truck
{
    /// <summary>
    /// Perlin gürültüsüyle yumuşak ışık titremesi + rastgele kısa kesintiler.
    /// Yalnızca 1-2 lambaya eklenmesi önerilir. (Projedeki StealthGame.LightFlicker'dan bağımsızdır.)
    /// </summary>
    [DisallowMultipleComponent]
    public class LightFlicker : MonoBehaviour
    {
        [Header("Hedef")]
        [SerializeField] private Light hedefIsik;

        [Tooltip("İsteğe bağlı: lamba gövdesinin emissive renderer'ı (materyal kopyalanmaz, PropertyBlock kullanılır).")]
        [SerializeField] private Renderer emissiveRenderer;

        [Header("Yoğunluk")]
        [SerializeField] private float minIntensity = 0.6f;
        [SerializeField] private float maxIntensity = 1.4f;

        [Header("Titreme")]
        [Tooltip("Perlin gürültüsünün hızı (büyük = daha hızlı titreme).")]
        [SerializeField] private float noiseHizi = 1.5f;

        [Tooltip("Hedef değere yaklaşma hızı. Büyük = keskin, küçük = yumuşak.")]
        [SerializeField] private float gecisHizi = 8f;

        [Header("Kesintiler")]
        [Tooltip("Saniye başına kesinti olasılığı (0.1 = ortalama 10 saniyede bir).")]
        [Range(0f, 2f)] [SerializeField] private float dropoutChance = 0.15f;

        [SerializeField] private Vector2 dropoutSuresi = new Vector2(0.05f, 0.25f);

        [Tooltip("Kesinti sırasında yoğunluğun çarpanı (0 = tamamen söner).")]
        [Range(0f, 1f)] [SerializeField] private float dropoutCarpani = 0.05f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock blok;
        private Color temelEmission = Color.white;
        private float seed;
        private float mevcut;
        private float kesintiBitis = -1f;

        private void Reset() => hedefIsik = GetComponent<Light>();

        private void Awake()
        {
            if (hedefIsik == null) hedefIsik = GetComponent<Light>();
            seed = Random.value * 100f;
            mevcut = maxIntensity;

            if (emissiveRenderer != null)
            {
                blok = new MaterialPropertyBlock();
                var mat = emissiveRenderer.sharedMaterial;
                if (mat != null && mat.HasProperty(EmissionId))
                    temelEmission = mat.GetColor(EmissionId);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Rastgele kısa kesinti tetikle (kare hızından bağımsız).
            if (Time.time >= kesintiBitis && Random.value < dropoutChance * dt)
                kesintiBitis = Time.time + Random.Range(dropoutSuresi.x, dropoutSuresi.y);

            float gurultu = Mathf.PerlinNoise(seed, Time.time * noiseHizi);
            float hedef = Mathf.Lerp(minIntensity, maxIntensity, gurultu);
            if (Time.time < kesintiBitis) hedef *= dropoutCarpani;

            // Kare hızından bağımsız üstel yumuşatma (Lerp).
            mevcut = Mathf.Lerp(mevcut, hedef, 1f - Mathf.Exp(-gecisHizi * dt));

            if (hedefIsik != null) hedefIsik.intensity = mevcut;

            if (emissiveRenderer != null && blok != null)
            {
                float oran = maxIntensity > 0.0001f ? mevcut / maxIntensity : 0f;
                emissiveRenderer.GetPropertyBlock(blok);
                blok.SetColor(EmissionId, temelEmission * oran);
                emissiveRenderer.SetPropertyBlock(blok);
            }
        }

        private void OnValidate()
        {
            if (maxIntensity < minIntensity) maxIntensity = minIntensity;
            if (dropoutSuresi.y < dropoutSuresi.x) dropoutSuresi.y = dropoutSuresi.x;
        }
    }
}
