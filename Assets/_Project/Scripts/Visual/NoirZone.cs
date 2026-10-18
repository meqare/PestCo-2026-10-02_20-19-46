using UnityEngine;

// "Noir dedektif" görünümü (Shadow of Doubt tarzı) bir kutu içinde: dither'lı ışık/gölge, posterize palet,
// loş ortam + sıcak ışık havuzları, soluk renkler, teal gölge, tane ve kontursuz yüzeyler.
// PestCo/Toon shader'ı bu global'leri okur. Bileşen kapanınca/silinince tüm sahne eski görünüme döner.
// Not: aynı sahnede InkZone varsa o da DepthInk global'lerini yazar; ikisinden birini kullan.
[ExecuteAlways]
[DisallowMultipleComponent]
public class NoirZone : MonoBehaviour
{
    [Header("Bölge")]
    [Tooltip("Kutu merkezi (dünya uzayı).")]
    [SerializeField] private Vector3 center = new Vector3(42.66f, 1.31f, 5.10f);
    [Tooltip("Kutunun yarı boyutları (metre).")]
    [SerializeField] private Vector3 extents = new Vector3(2.15f, 1.65f, 4.4f);
    [Tooltip("Kutu dışında geçişin yumuşak olduğu mesafe (m).")]
    [SerializeField, Min(0.01f)] private float softEdge = 0.6f;
    [Tooltip("Genel miktar: 0 = eski görünüm, 1 = tam noir.")]
    [SerializeField, Range(0f, 1f)] private float amount = 1f;

    [Header("Piksel / dither")]
    [Tooltip("Dither ve tane blok boyutu (ekran pikseli). Büyük = daha kaba, retro.")]
    [SerializeField, Range(1f, 8f)] private float pixelSize = 3f;
    [Tooltip("Kanal başına renk seviyesi (posterize). Düşük = daha sınırlı palet.")]
    [SerializeField, Range(3f, 24f)] private float colorLevels = 10f;
    [Tooltip("Film tanesi miktarı.")]
    [SerializeField, Range(0f, 0.3f)] private float grain = 0.035f;

    [Header("Renk")]
    [Tooltip("Renkleri soldurma (0 = canlı, 1 = siyah-beyaz).")]
    [SerializeField, Range(0f, 1f)] private float desaturate = 0.55f;
    [Tooltip("Lamba/el feneri olmayan yerlerin karartması (0 = değişmez, 1 = siyah).")]
    [SerializeField, Range(0f, 0.9f)] private float ambientDim = 0.68f;
    [Tooltip("Gölgelerin tonu (soluk teal/yeşilimsi mavi).")]
    [SerializeField] private Color shadowTint = new Color(0.8f, 0.92f, 0.9f, 1f);
    [Tooltip("Aydınlık yerlerin tonu (sıcak amber).")]
    [SerializeField] private Color lightTint = new Color(1.18f, 0.98f, 0.74f, 1f);

    [Header("Çizgiler")]
    [Tooltip("Toon konturunu ve DepthInk çizgisini sil (1 = çizgi yok).")]
    [SerializeField, Range(0f, 1f)] private float removeOutlines = 1f;

    private static readonly int CenterId = Shader.PropertyToID("_PC_NoirC");
    private static readonly int ExtentsId = Shader.PropertyToID("_PC_NoirE");
    private static readonly int ParamsId = Shader.PropertyToID("_PC_NoirP");
    private static readonly int ShadowId = Shader.PropertyToID("_PC_NoirT");
    private static readonly int LightId = Shader.PropertyToID("_PC_NoirL");
    private static readonly int InkCenterId = Shader.PropertyToID("_PC_InkZoneCenter");
    private static readonly int InkExtentsId = Shader.PropertyToID("_PC_InkZoneExtents");

    private void OnEnable() { Push(); }
    private void OnValidate() { if (isActiveAndEnabled) Push(); }
    private void Update() { Push(); }

    private void OnDisable()
    {
        Shader.SetGlobalVector(CenterId, Vector4.zero);
        Shader.SetGlobalVector(InkExtentsId, Vector4.zero);
    }

    private void Push()
    {
        Shader.SetGlobalVector(CenterId, new Vector4(center.x, center.y, center.z, amount));
        Shader.SetGlobalVector(ExtentsId, new Vector4(extents.x, extents.y, extents.z, softEdge));
        Shader.SetGlobalVector(ParamsId, new Vector4(pixelSize, colorLevels, desaturate, ambientDim));
        Shader.SetGlobalVector(ShadowId, new Vector4(shadowTint.r, shadowTint.g, shadowTint.b, removeOutlines));
        Shader.SetGlobalVector(LightId, new Vector4(lightTint.r, lightTint.g, lightTint.b, grain));

        // DepthInk (ekran-uzayı kalem çizgisi): kutu içinde çarpan = 1 - removeOutlines (1 = çizgi tamamen silinir).
        Shader.SetGlobalVector(InkCenterId, new Vector4(center.x, center.y, center.z, 1f - removeOutlines));
        Shader.SetGlobalVector(InkExtentsId, new Vector4(extents.x, extents.y, extents.z, softEdge));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 0.9f, 0.9f);
        Gizmos.DrawWireCube(center, extents * 2f);
    }
}
