using System.Collections;
using UnityEngine;

namespace PestCo.Truck
{
    /// <summary>
    /// Fanustaki uyanık böcekler ara ara cama atılır: cama çarpınca "tok" sesi, cam titrer, içerideki ışık seğirir.
    /// Oyuncu yaklaştıkça daha sık vururlar; bazen art arda 2-3 kez.
    /// Aynı objede BugCage olmalı. Cam silindirinin ekseni bu objenin yerel Y eksenidir.
    /// </summary>
    [RequireComponent(typeof(BugCage))]
    public class FanusVurma : MonoBehaviour
    {
        [SerializeField] private Transform cam;
        [SerializeField] private Light icIsik;
        [Tooltip("Camın iç yarıçapı (m). Böcek gövdesi bu mesafeye ulaşınca çarpmış sayılır.")]
        [SerializeField] private float icYaricap = 0.235f;

        [Header("Sıklık")]
        [SerializeField] private Vector2 uzakAralik = new Vector2(5f, 12f);
        [SerializeField] private Vector2 yakinAralik = new Vector2(1.2f, 3.5f);
        [SerializeField] private float yakinMesafe = 2.2f;

        [Header("Baskı (aynı nesnedeki CagePressureController, yoksa etkisiz)")]
        [Tooltip("Vuruşlar arası bekleme bu çarpanla kısalır (0.7 = %30 daha sık). Yalnızca zamanlamayı etkiler; atılma hızı/fizik aynı.")]
        [Range(0.1f, 1f)] [SerializeField] private float uyariAralikCarpani = 0.7f;
        [Range(0.1f, 1f)] [SerializeField] private float tehlikeAralikCarpani = 0.5f;
        [Range(0.1f, 1f)] [SerializeField] private float kritikAralikCarpani = 0.35f;

        [Header("Atılma")]
        [SerializeField] private float atilmaHizi = 1.7f;
        [Range(0f, 1f)] [SerializeField] private float seriOlasiligi = 0.35f;

        private static AudioClip vurmaSesi;
        private BugCage kafes;
        private CagePressureController baski;
        private Transform oyuncu;
        private Vector3 camTemelPoz;
        private float sonraki;
        private bool vuruyor;

        private void Awake()
        {
            kafes = GetComponent<BugCage>();
            baski = GetComponent<CagePressureController>();
            if (cam != null) camTemelPoz = cam.localPosition;
            Planla(false);
        }

        private void OnEnable()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) oyuncu = pc.transform;
        }

        private void Update()
        {
            if (vuruyor || Time.time < sonraki || kafes.Count == 0) return;

            bool yakin = oyuncu != null && (oyuncu.position - transform.position).sqrMagnitude < yakinMesafe * yakinMesafe;
            Planla(yakin);

            // Uyanık bir böcek seç
            CageableBug secilen = null;
            int bakilan = 0;
            foreach (var b in kafes.Caged)
            {
                if (b == null || b.IsStunned || !b.gameObject.activeInHierarchy) continue;
                bakilan++;
                if (Random.Range(0, bakilan) == 0) secilen = b; // rastgele seçim
            }
            if (secilen != null) StartCoroutine(Vur(secilen, yakin));
        }

        private void Planla(bool yakin)
        {
            var a = yakin ? yakinAralik : uzakAralik;
            sonraki = Time.time + Random.Range(a.x, a.y) * AralikCarpani();
        }

        // Fanus baskısı arttıkça yaratıklar cama daha sık vurur (yalnızca bekleme süresi kısalır).
        private float AralikCarpani()
        {
            if (baski == null) return 1f;
            switch (baski.State)
            {
                case CagePressureState.Warning: return uyariAralikCarpani;
                case CagePressureState.Danger: return tehlikeAralikCarpani;
                case CagePressureState.Critical: return kritikAralikCarpani;
                default: return 1f;
            }
        }

        private IEnumerator Vur(CageableBug bocek, bool yakin)
        {
            vuruyor = true;
            var rb = bocek.GetComponent<Rigidbody>();
            int seri = Random.value < (yakin ? seriOlasiligi * 1.6f : seriOlasiligi) ? Random.Range(2, 4) : 1;

            // Oyuncu yakınsa çoğunlukla ona doğru atılır
            Vector3 yon;
            if (yakin && Random.value < 0.75f) yon = oyuncu.position - transform.position;
            else yon = Random.insideUnitSphere;
            yon = Vector3.ProjectOnPlane(yon, transform.up);
            if (yon.sqrMagnitude < 0.001f) yon = transform.right;
            yon.Normalize();

            var eskiMod = rb.collisionDetectionMode;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // ince camdan geçmesin

            for (int i = 0; i < seri && bocek != null && kafes.Count > 0; i++)
            {
                // Cama dön ve atıl
                Vector3 bakis = yon; bakis.y = 0f;
                if (bakis.sqrMagnitude > 0.001f) rb.MoveRotation(Quaternion.LookRotation(bakis, Vector3.up));
                rb.linearVelocity = Vector3.zero;
                rb.AddForce(yon * atilmaHizi + transform.up * 0.5f, ForceMode.VelocityChange);

                // Gövde cama varana kadar bekle (en fazla 0.5 sn)
                float bitis = Time.time + 0.5f;
                while (bocek != null && Time.time < bitis)
                {
                    Vector3 yerel = transform.InverseTransformPoint(bocek.transform.position);
                    if (new Vector2(yerel.x, yerel.z).magnitude >= icYaricap - 0.09f) break;
                    yield return new WaitForFixedUpdate();
                }
                if (bocek == null) break;

                Carp(bocek.transform.position, i);
                rb.AddForce(-yon * 0.6f, ForceMode.VelocityChange); // camdan seker
                yield return new WaitForSeconds(Random.Range(0.28f, 0.42f));
            }

            if (rb != null) rb.collisionDetectionMode = eskiMod;
            vuruyor = false;
        }

        private void Carp(Vector3 nokta, int sira)
        {
            if (vurmaSesi == null) vurmaSesi = SesUret();
            float perde = Random.Range(0.9f, 1.12f) + sira * 0.04f;
            AnomalySounds.PlayAt(vurmaSesi, nokta, Random.Range(0.75f, 1f), perde, 10f);
            StartCoroutine(Titret());
        }

        private IEnumerator Titret()
        {
            float bas = Time.time;
            float temelIsik = icIsik != null ? icIsik.intensity : 0f;
            while (Time.time - bas < 0.22f)
            {
                float k = 1f - (Time.time - bas) / 0.22f;
                if (cam != null) cam.localPosition = camTemelPoz + Random.insideUnitSphere * 0.004f * k;
                if (icIsik != null) icIsik.intensity = temelIsik * (1f + Random.Range(-0.6f, 0.9f) * k);
                yield return null;
            }
            if (cam != null) cam.localPosition = camTemelPoz;
            if (icIsik != null) icIsik.intensity = temelIsik;
        }

        // Cama vuruş: kısa kitin "tık" + cam çınlaması + alçak gövde sesi
        private static AudioClip SesUret()
        {
            const int hz = 44100;
            int n = (int)(hz * 0.35f);
            var s = new float[n];
            var r = new System.Random(77);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)hz;
                float g = (float)r.NextDouble() * 2f - 1f;
                lp += (g - lp) * 0.3f;
                float tik = lp * Mathf.Exp(-t / 0.004f) * 1.2f;
                float cinlama = (Mathf.Sin(2f * Mathf.PI * 2350f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 3870f * t) * 0.35f
                                 + Mathf.Sin(2f * Mathf.PI * 5420f * t) * 0.2f) * Mathf.Exp(-t / 0.07f);
                float tok = Mathf.Sin(2f * Mathf.PI * (180f + 120f * Mathf.Exp(-t / 0.01f)) * t) * Mathf.Exp(-t / 0.035f);
                s[i] = (tik + cinlama * 0.45f + tok * 0.7f) * 0.6f;
            }
            var clip = AudioClip.Create("FanusVurma", n, 1, hz, false);
            clip.SetData(s, 0);
            return clip;
        }
    }
}
