using UnityEngine;

// "Cesur Korkak Köpek" görünümü bir kutu içinde: soluk/tozlu 90'lar paleti (mor-kahve gölge, krem-hardal ışık),
// yumuşak airbrush gölge, elle boyanmış guaj alacası + kağıt greni, köşe/tavan kararması, ince koyu kahve titrek kontur
// ve hafif eğri büğrü perspektif. PestCo/Toon ve PestCo/DepthInk bu global'leri okur.
// Bileşen kapanınca/silinince tüm sahne eski görünüme döner.
// Not: NoirZone / InkZone / CartoonWonky da aynı global'lerin bir kısmını yazar; aynı anda yalnız birini kullan.
[ExecuteAlways]
[DisallowMultipleComponent]
public class CourageZone : MonoBehaviour
{
    [Header("Bölge")]
    [Tooltip("Kutu merkezi (dünya uzayı).")]
    [SerializeField] private Vector3 center = new Vector3(42.63f, 1.4f, 5.12f);
    [Tooltip("Kutunun yarı boyutları (metre). Köşe kararması kutunun duvarlarına göre hesaplanır.")]
    [SerializeField] private Vector3 extents = new Vector3(1.9f, 1.45f, 3.8f);
    [Tooltip("Kutu dışında geçişin yumuşak olduğu mesafe (m).")]
    [SerializeField, Min(0.01f)] private float softEdge = 0.6f;
    [Tooltip("Genel miktar: 0 = eski görünüm, 1 = tam Cesur Korkak Köpek.")]
    [SerializeField, Range(0f, 1f)] private float amount = 1f;

    [Header("Boya")]
    [Tooltip("Renkleri soldurma (0 = canlı, 1 = siyah-beyaz). Çizgi filmin arka planları tozlu ve soluktur.")]
    [SerializeField, Range(0f, 1f)] private float desaturate = 0.22f;
    [Tooltip("Elle boyanmış guaj/suluboya alacası (leke) şiddeti.")]
    [SerializeField, Range(0f, 0.6f)] private float paintMottle = 0.4f;
    [Tooltip("Kağıt greni (sabit, titremez).")]
    [SerializeField, Range(0f, 0.2f)] private float paperGrain = 0.05f;
    [Tooltip("Gölgeleri sert toon basamağı yerine yumuşak airbrush geçişe çevirme.")]
    [SerializeField, Range(0f, 1f)] private float softShading = 0.8f;
    [Tooltip("Duvar dipleri, köşeler ve tavanın airbrush kararması.")]
    [SerializeField, Range(0f, 1f)] private float cornerDark = 0.32f;

    [Header("Renk")]
    [Tooltip("Gölge tonu (mor-kahve).")]
    [SerializeField] private Color shadowTint = new Color(0.86f, 0.74f, 0.9f, 1f);
    [Tooltip("Işık tonu (krem-hardal).")]
    [SerializeField] private Color lightTint = new Color(1.16f, 1.06f, 0.84f, 1f);

    [Header("Çizgiler")]
    [Tooltip("Kontur rengi (koyu kahve mürekkep).")]
    [SerializeField] private Color lineColor = new Color(0.17f, 0.1f, 0.08f, 1f);
    [Tooltip("Toon kontur kalınlık çarpanı (Courage çizgileri incedir).")]
    [SerializeField, Range(0f, 2f)] private float outlineWidth = 0.7f;
    [Tooltip("Ekran-uzayı mürekkep çizgisinin titremesi (piksel). 0 = düz çizgi.")]
    [SerializeField, Range(0f, 6f)] private float lineWobble = 1.2f;
    [Tooltip("Ekran-uzayı mürekkep çizgisi kalınlık çarpanı.")]
    [SerializeField, Range(0.5f, 3f)] private float lineWidth = 1.1f;

    [Header("Perspektif")]
    [Tooltip("Eğri büğrü, çarpık çizgi film perspektifi (0 = düz).")]
    [SerializeField, Range(0f, 1.2f)] private float wonky = 0.5f;

    private static readonly int CId = Shader.PropertyToID("_PC_CourC");
    private static readonly int EId = Shader.PropertyToID("_PC_CourE");
    private static readonly int PId = Shader.PropertyToID("_PC_CourP");
    private static readonly int SId = Shader.PropertyToID("_PC_CourS");
    private static readonly int LId = Shader.PropertyToID("_PC_CourL");
    private static readonly int OId = Shader.PropertyToID("_PC_CourO");
    private static readonly int InkCenterId = Shader.PropertyToID("_PC_InkZoneCenter");
    private static readonly int InkExtentsId = Shader.PropertyToID("_PC_InkZoneExtents");
    private static readonly int InkWobbleId = Shader.PropertyToID("_PC_InkWobble");
    private static readonly int InkTintId = Shader.PropertyToID("_PC_InkTint");
    private static readonly int WonkyCId = Shader.PropertyToID("_PC_WonkyC");
    private static readonly int WonkyEId = Shader.PropertyToID("_PC_WonkyE");
    private static readonly int WonkyPId = Shader.PropertyToID("_PC_WonkyP");

    private void OnEnable() { Push(); }
    private void OnValidate() { if (isActiveAndEnabled) Push(); }
    private void Update() { Push(); }

    private void OnDisable()
    {
        Shader.SetGlobalVector(CId, Vector4.zero);
        Shader.SetGlobalVector(InkExtentsId, Vector4.zero);
        Shader.SetGlobalVector(InkWobbleId, Vector4.zero);
        Shader.SetGlobalVector(InkTintId, Vector4.zero);
        Shader.SetGlobalVector(WonkyCId, Vector4.zero);
    }

    private void Push()
    {
        Shader.SetGlobalVector(CId, new Vector4(center.x, center.y, center.z, amount));
        Shader.SetGlobalVector(EId, new Vector4(extents.x, extents.y, extents.z, softEdge));
        Shader.SetGlobalVector(PId, new Vector4(desaturate, paintMottle, paperGrain, outlineWidth));
        Shader.SetGlobalVector(SId, new Vector4(shadowTint.r, shadowTint.g, shadowTint.b, softShading));
        Shader.SetGlobalVector(LId, new Vector4(lightTint.r, lightTint.g, lightTint.b, cornerDark));
        Shader.SetGlobalVector(OId, new Vector4(lineColor.r, lineColor.g, lineColor.b, amount));

        // DepthInk: bölgede tam şiddet, titrek, koyu kahve mürekkep
        Shader.SetGlobalVector(InkCenterId, new Vector4(center.x, center.y, center.z, 1f));
        Shader.SetGlobalVector(InkExtentsId, new Vector4(extents.x, extents.y, extents.z, softEdge));
        Shader.SetGlobalVector(InkWobbleId, new Vector4(lineWobble, 6f, amount, lineWidth));
        Shader.SetGlobalVector(InkTintId, new Vector4(lineColor.r, lineColor.g, lineColor.b, 1f));

        // Eğri büğrü perspektif (titreme yok: Courage arka planları sabit boyalı resimdir)
        Shader.SetGlobalVector(WonkyCId, new Vector4(center.x, center.y, center.z, wonky * amount));
        Shader.SetGlobalVector(WonkyEId, new Vector4(extents.x, extents.y, extents.z, softEdge));
        Shader.SetGlobalVector(WonkyPId, Vector4.zero);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.85f, 0.6f, 0.9f, 0.9f);
        Gizmos.DrawWireCube(center, extents * 2f);
    }
}
