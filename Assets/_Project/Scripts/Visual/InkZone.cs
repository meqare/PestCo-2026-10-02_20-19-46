using UnityEngine;

// DepthInk (ekran-uzayı kalem çizgisi) şiddetini bir kutu içinde azaltır; kutunun dışı değişmez.
// Bileşen kapanınca etkisi sıfırlanır (tüm sahne eski görünüme döner).
[ExecuteAlways]
[DisallowMultipleComponent]
public class InkZone : MonoBehaviour
{
    [Tooltip("Kutu merkezi (dünya uzayı).")]
    [SerializeField] private Vector3 center = new Vector3(42.66f, 1.31f, 5.10f);
    [Tooltip("Kutunun yarı boyutları (metre).")]
    [SerializeField] private Vector3 extents = new Vector3(2.15f, 1.65f, 4.4f);
    [Tooltip("İçeride çizgi şiddeti çarpanı (1 = değişmez, 0 = çizgi yok).")]
    [SerializeField, Range(0f, 1f)] private float insideStrength = 0.25f;
    [Tooltip("Kutu dışında geçişin yumuşak olduğu mesafe (m).")]
    [SerializeField, Min(0.01f)] private float softEdge = 0.4f;

    [Header("Elle çizim")]
    [Tooltip("Çizgilerin dalgalanma/kayma genliği (piksel). 0 = düz çizgi.")]
    [SerializeField, Range(0f, 6f)] private float wobble = 2.5f;
    [Tooltip("Çizgi titreme kare hızı (düşük = daha çizgi film).")]
    [SerializeField, Range(1f, 24f)] private float wobbleFps = 8f;

    [Tooltip("Çizgi rengini kurşun kalem grisine kaydırma (0 = koyu mürekkep, 1 = kurşun kalem).")]
    [SerializeField, Range(0f, 1f)] private float pencil = 0.7f;

    [Tooltip("Bölge içinde çizgi kalınlığı çarpanı (1 = normal, 2 = iki kat kalın kalem).")]
    [SerializeField, Range(0.5f, 4f)] private float lineWidth = 1f;

    private static readonly int WobbleId = Shader.PropertyToID("_PC_InkWobble");
    private static readonly int CenterId = Shader.PropertyToID("_PC_InkZoneCenter");
    private static readonly int ExtentsId = Shader.PropertyToID("_PC_InkZoneExtents");

    private void OnEnable() { Push(); }
    private void OnValidate() { if (isActiveAndEnabled) Push(); }
    private void Update() { Push(); }
    private void OnDisable() { Shader.SetGlobalVector(ExtentsId, Vector4.zero); Shader.SetGlobalVector(WobbleId, Vector4.zero); }

    private void Push()
    {
        Shader.SetGlobalVector(CenterId, new Vector4(center.x, center.y, center.z, insideStrength));
        Shader.SetGlobalVector(ExtentsId, new Vector4(extents.x, extents.y, extents.z, softEdge));
        Shader.SetGlobalVector(WobbleId, new Vector4(wobble, wobbleFps, pencil, lineWidth));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(center, extents * 2f);
    }
}
