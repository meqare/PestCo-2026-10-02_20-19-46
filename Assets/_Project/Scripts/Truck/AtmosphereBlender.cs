using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PestCo.Truck
{
    /// <summary>
    /// "Korku" Volume'unun weight değerini Lerp ile yukarı/aşağı geçirir.
    /// Taban Volume (weight 1) sürekli açık kalır; korku Volume'u (weight 0, daha yüksek priority) üstüne karışır.
    /// </summary>
    public class AtmosphereBlender : MonoBehaviour
    {
        [SerializeField] private Volume korkuVolume;

        [Header("Süreler (saniye)")]
        [SerializeField] private float girisSuresi = 0.6f;
        [SerializeField] private float beklemeSuresi = 1.5f;
        [SerializeField] private float cikisSuresi = 3f;

        [Header("Hedef")]
        [Range(0f, 1f)] [SerializeField] private float korkuAgirligi = 1f;
        [SerializeField] private bool zamanOlceginiYoksay = false;

        private Coroutine calisan;

        public float Agirlik => korkuVolume != null ? korkuVolume.weight : 0f;

        private void Awake()
        {
            if (korkuVolume != null) korkuVolume.weight = 0f;
        }

        /// <summary>UnityEvent'ten çağrılabilir: girişte yüksel, bekle, çıkışta sön.</summary>
        public void PulseScare() => PulseScare(korkuAgirligi, girisSuresi, beklemeSuresi, cikisSuresi);

        public void PulseScare(float hedef, float giris, float bekle, float cikis)
        {
            Durdur();
            calisan = StartCoroutine(PulseRutini(hedef, giris, bekle, cikis));
        }

        /// <summary>Weight'i verilen süre içinde hedef değere Lerp ile götürür.</summary>
        public void BlendTo(float hedef, float sure)
        {
            Durdur();
            calisan = StartCoroutine(GecisRutini(Mathf.Clamp01(hedef), sure));
        }

        public void BlendToScared() => BlendTo(korkuAgirligi, girisSuresi);
        public void BlendToCalm() => BlendTo(0f, cikisSuresi);

        private IEnumerator PulseRutini(float hedef, float giris, float bekle, float cikis)
        {
            yield return GecisRutini(Mathf.Clamp01(hedef), giris);
            float t = 0f;
            while (t < bekle) { t += Dt(); yield return null; }
            yield return GecisRutini(0f, cikis);
            calisan = null;
        }

        private IEnumerator GecisRutini(float hedef, float sure)
        {
            if (korkuVolume == null) yield break;

            float baslangic = korkuVolume.weight;
            if (sure <= 0f) { korkuVolume.weight = hedef; yield break; }

            float gecen = 0f;
            while (gecen < sure)
            {
                gecen += Dt();
                korkuVolume.weight = Mathf.Lerp(baslangic, hedef, Mathf.SmoothStep(0f, 1f, gecen / sure));
                yield return null;
            }
            korkuVolume.weight = hedef;
        }

        private float Dt() => zamanOlceginiYoksay ? Time.unscaledDeltaTime : Time.deltaTime;

        private void Durdur()
        {
            if (calisan != null) { StopCoroutine(calisan); calisan = null; }
        }
    }
}
