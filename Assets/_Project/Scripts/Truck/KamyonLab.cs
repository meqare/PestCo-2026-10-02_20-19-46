using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PestCo.Truck
{
    /// <summary>
    /// Kamyon kasasındaki laboratuvar.
    /// - Oyuncu kamyona yaklaşınca lab ışıkları açılır, uzaklaşınca kapanır (onlarca realtime ışık evde maliyet olmasın).
    ///   Objeler açık kalır: fanus camlarının collider'ları kaybolursa içerideki böcekler düşüp kaçar.
    /// - Kamera kasanın içine girince ortam ışığı/sis yumuşakça koyulaşır ve kontur (DepthInk) kapanır; çıkınca geri gelir.
    /// Bu bileşen her zaman aktif olan kök objede durur; iç alan kökün yerel uzayında tanımlıdır.
    /// </summary>
    public class KamyonLab : MonoBehaviour
    {
        [Header("Lab")]
        [Tooltip("Lab'ın tüm içeriği (ışıkları yaklaşınca açılır).")]
        [SerializeField] private GameObject icKok;
        [SerializeField] private ReflectionProbe probe;

        [Header("Alanlar (yerel uzay)")]
        [Tooltip("Kasanın iç hacmi: kamera bunun içindeyken 'içeride' sayılır.")]
        [SerializeField] private Vector3 icMerkez = new Vector3(0f, 1.28f, 0f);
        [SerializeField] private Vector3 icBoyut = new Vector3(3.1f, 2.56f, 7f);
        [Tooltip("İç hacmin her yönde bu kadar dışına gelince lab içeriği açılır (metre).")]
        [SerializeField] private float yakinlikPayi = 6f;
        [Tooltip("İç hacmin bu kadar dışından uzaktaysa lab içeriği hiç çizilmez (sadece Renderer kapanır, collider'lar kalır).")]
        [SerializeField] private float cizimPayi = 10f;

        [Header("İçerideyken ortam")]
        [SerializeField] private Color icAmbient = new Color(0.055f, 0.078f, 0.09f);
        [SerializeField] private float icSisYogunlugu = 0.012f;
        [SerializeField] private Color icSisRengi = new Color(0.035f, 0.05f, 0.06f);
        [Tooltip("Geçiş hızı (1/sn). Kapıdan girerken ortam bu hızla değişir.")]
        [SerializeField] private float gecisHizi = 2.5f;
        [Tooltip("İçeride kapatılacak renderer özellikleri (örn. DepthInk kontur çizgisi).")]
        [SerializeField] private ScriptableRendererFeature[] icerdeKapat;

        private float karisim;      // 0 = dışarı, 1 = tamamen içeri
        private bool kaydedildi;    // dışarının ortam ayarları saklandı mı
        private bool ozellikKapali;
        private bool[] ozellikBaslangic; // dışarıda geri dönülecek durum: başta kapalı olan özellik açılmasın
        private bool probeCizildi;
        private Light[] isiklar;
        private bool isiklarAcik = true;
        private Renderer[] cizimler;
        private bool[] cizimBaslangic;
        private bool cizimAcik = true;

        // Dışarının ortam ayarları (geri yüklemek için)
        private AmbientMode eskiMod;
        private Color eskiAmbient, eskiGok, eskiUfuk, eskiYer, eskiSisRengi;
        private float eskiAmbYogunluk, eskiSis;
        private bool eskiSisAcik;
        private FogMode eskiSisModu;

        public bool Icerde { get; private set; }

        private void Awake()
        {
            if (icerdeKapat != null)
            {
                ozellikBaslangic = new bool[icerdeKapat.Length];
                for (int i = 0; i < icerdeKapat.Length; i++)
                    ozellikBaslangic[i] = icerdeKapat[i] != null && icerdeKapat[i].isActive;
            }
            if (icKok == null) return;
            icKok.SetActive(true);
            isiklar = icKok.GetComponentsInChildren<Light>(true);
            IsiklariAyarla(false);
            cizimler = icKok.GetComponentsInChildren<Renderer>(true);
            cizimBaslangic = new bool[cizimler.Length];
        }

        // Uzaktayken lab objeleri çizilmesin (pencereden/kapıdan görünse bile ışıklar kapalı, içerisi karanlık).
        // Sadece Renderer.enabled değişir: fizik, collider ve böcekler etkilenmez. Kapatırken kapalı olan renderer geri açılmaz.
        private void CizimiAyarla(bool acik)
        {
            if (cizimAcik == acik || cizimler == null) return;
            for (int i = 0; i < cizimler.Length; i++)
            {
                if (cizimler[i] == null) continue;
                if (!acik) cizimBaslangic[i] = cizimler[i].enabled; // kapatmadan önceki durumu sakla (başka script kapatmış olabilir)
                cizimler[i].enabled = acik && cizimBaslangic[i];
            }
            cizimAcik = acik;
        }

        private void IsiklariAyarla(bool acik)
        {
            if (isiklarAcik == acik || isiklar == null) return;
            foreach (var l in isiklar) if (l != null) l.enabled = acik;
            isiklarAcik = acik;
            probeCizildi = false;
        }

        // Editor'de Play kapanırsa renderer asset'i ve ortam kalıcı değişmesin
        private void OnDisable()
        {
            karisim = 0f;
            GeriYukle();
            CizimiAyarla(true);
        }

        private void Update()
        {
            var kam = Camera.main;
            if (kam == null) return;

            Vector3 yerel = transform.InverseTransformPoint(kam.transform.position) - icMerkez;
            Vector3 yari = icBoyut * 0.5f;
            Icerde = Mathf.Abs(yerel.x) <= yari.x && Mathf.Abs(yerel.y) <= yari.y && Mathf.Abs(yerel.z) <= yari.z;
            bool yakin = Mathf.Abs(yerel.x) <= yari.x + yakinlikPayi && Mathf.Abs(yerel.y) <= yari.y + yakinlikPayi
                         && Mathf.Abs(yerel.z) <= yari.z + yakinlikPayi;

            IsiklariAyarla(yakin);
            CizimiAyarla(Mathf.Abs(yerel.x) <= yari.x + cizimPayi && Mathf.Abs(yerel.y) <= yari.y + cizimPayi
                         && Mathf.Abs(yerel.z) <= yari.z + cizimPayi);

            karisim = Mathf.MoveTowards(karisim, Icerde ? 1f : 0f, gecisHizi * Time.deltaTime);
            OrtamiUygula();

            // Realtime probe: ilk girişte, iç ortam oturduktan sonra bir kez
            if (!probeCizildi && karisim >= 1f && probe != null && isiklarAcik)
            {
                probe.RenderProbe();
                probeCizildi = true;
            }
        }

        private void OrtamiUygula()
        {
            if (karisim <= 0f) { GeriYukle(); return; }

            if (!kaydedildi)
            {
                eskiMod = RenderSettings.ambientMode;
                eskiAmbient = RenderSettings.ambientLight;
                eskiGok = RenderSettings.ambientSkyColor;
                eskiUfuk = RenderSettings.ambientEquatorColor;
                eskiYer = RenderSettings.ambientGroundColor;
                eskiAmbYogunluk = RenderSettings.ambientIntensity;
                eskiSis = RenderSettings.fogDensity;
                eskiSisRengi = RenderSettings.fogColor;
                eskiSisAcik = RenderSettings.fog;
                eskiSisModu = RenderSettings.fogMode;
                kaydedildi = true;
            }

            float t = Mathf.SmoothStep(0f, 1f, karisim);
            switch (eskiMod)
            {
                case AmbientMode.Trilight:
                    RenderSettings.ambientSkyColor = Color.Lerp(eskiGok, icAmbient, t);
                    RenderSettings.ambientEquatorColor = Color.Lerp(eskiUfuk, icAmbient, t);
                    RenderSettings.ambientGroundColor = Color.Lerp(eskiYer, icAmbient, t);
                    break;
                case AmbientMode.Flat:
                    RenderSettings.ambientLight = Color.Lerp(eskiAmbient, icAmbient, t);
                    break;
                default: // Skybox: rengi değiştirilemez, yoğunluğu düşür
                    RenderSettings.ambientIntensity = Mathf.Lerp(eskiAmbYogunluk, 0.15f, t);
                    break;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Color.Lerp(eskiSisRengi, icSisRengi, t);
            RenderSettings.fogDensity = Mathf.Lerp(eskiSisAcik && eskiSisModu == FogMode.Exponential ? eskiSis : 0f, icSisYogunlugu, t);

            OzellikleriAyarla(karisim > 0.5f);
        }

        private void GeriYukle()
        {
            OzellikleriAyarla(false);
            if (!kaydedildi) return;

            RenderSettings.ambientMode = eskiMod;
            RenderSettings.ambientLight = eskiAmbient;
            RenderSettings.ambientSkyColor = eskiGok;
            RenderSettings.ambientEquatorColor = eskiUfuk;
            RenderSettings.ambientGroundColor = eskiYer;
            RenderSettings.ambientIntensity = eskiAmbYogunluk;
            RenderSettings.fogDensity = eskiSis;
            RenderSettings.fogColor = eskiSisRengi;
            RenderSettings.fog = eskiSisAcik;
            RenderSettings.fogMode = eskiSisModu;
            kaydedildi = false;
        }

        private void OzellikleriAyarla(bool kapali)
        {
            if (ozellikKapali == kapali || icerdeKapat == null) return;
            for (int i = 0; i < icerdeKapat.Length; i++)
                if (icerdeKapat[i] != null)
                    icerdeKapat[i].SetActive(!kapali && (ozellikBaslangic == null || ozellikBaslangic[i]));
            ozellikKapali = kapali;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.9f, 0.8f, 0.8f);
            Gizmos.DrawWireCube(icMerkez, icBoyut);
            Gizmos.color = new Color(0.3f, 0.9f, 0.8f, 0.25f);
            Gizmos.DrawWireCube(icMerkez, icBoyut + Vector3.one * (yakinlikPayi * 2f));
        }
    }
}
