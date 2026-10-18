using UnityEngine;

// Çizgi film "eğri büğrü" dünya: PestCo/Toon shader'ının bükme alanını bir kutu içinde açar.
// Alan dünya uzayında sürekli olduğu için nesneler arasında boşluk açılmaz; kutunun dışına doğru yumuşakça sönümlenir.
// Çarpışmalar (collider) değişmez, sadece görüntü bükülür. Bileşen kapanınca her şey düzleşir.
[ExecuteAlways]
[DisallowMultipleComponent]
public class CartoonWonky : MonoBehaviour
{
    [SerializeField] private Vector3 center = new Vector3(42.66f, 1.31f, 5.10f);
    [SerializeField] private Vector3 extents = new Vector3(2.15f, 1.65f, 4.4f);
    [Tooltip("Bükülme miktarı: 0 = düz, 1 = belirgin çizgi film, 2 = abartılı.")]
    [SerializeField, Range(0f, 2f)] private float amount = 0.9f;
    [Tooltip("Kutu dışında sönümlenme mesafesi (m).")]
    [SerializeField, Min(0.01f)] private float softEdge = 0.6f;
    [Tooltip("Elle çizim titremesi (line boil) genliği, metre. 0 = titreme yok.")]
    [SerializeField, Range(0f, 0.02f)] private float boil = 0.004f;
    [Tooltip("Titreme kare hızı (düşük = daha çizgi film).")]
    [SerializeField, Range(1f, 24f)] private float boilFps = 8f;

    [Header("Elle çizim")]
    [Tooltip("Gölgelerde kalem taraması (hatching) şiddeti. 0 = yok.")]
    [SerializeField, Range(0f, 1f)] private float hatch = 0.55f;
    [Tooltip("Tarama çizgilerinin aralığı (piksel).")]
    [SerializeField, Range(3f, 20f)] private float hatchSpacing = 7f;
    [Tooltip("Kağıt greni şiddeti.")]
    [SerializeField, Range(0f, 0.2f)] private float paperGrain = 0.05f;

    [Tooltip("Aydınlık yüzeylerde bile hafif renkli kalem dolgusu (0 = sadece gölgelerde tarama).")]
    [SerializeField, Range(0f, 0.5f)] private float fill = 0.22f;

    private static readonly int HId = Shader.PropertyToID("_PC_HatchP");
    private static readonly int CId = Shader.PropertyToID("_PC_WonkyC");
    private static readonly int EId = Shader.PropertyToID("_PC_WonkyE");
    private static readonly int PId = Shader.PropertyToID("_PC_WonkyP");

    private void OnEnable() { Push(); }
    private void OnValidate() { if (isActiveAndEnabled) Push(); }
    private void Update() { Push(); }
    private void OnDisable() { Shader.SetGlobalVector(CId, Vector4.zero); Shader.SetGlobalVector(HId, Vector4.zero); }

    private void Push()
    {
        Shader.SetGlobalVector(CId, new Vector4(center.x, center.y, center.z, amount));
        Shader.SetGlobalVector(EId, new Vector4(extents.x, extents.y, extents.z, softEdge));
        Shader.SetGlobalVector(PId, new Vector4(boil, boilFps, 0, 0));
        Shader.SetGlobalVector(HId, new Vector4(hatch, hatchSpacing, paperGrain, fill));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(center, extents * 2f);
    }
}
