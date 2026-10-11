using UnityEngine;

// Duvar yakınlığında silahı geri ve aşağı çeker (weapon obstruction).
// Kameradan ileri SphereCast atılır; duvar eşik mesafesinin içindeyse silah, yakınlık oranında
// kamera uzayında geri (-Z) ve aşağı (-Y) çekilir, duvar uzaklaşınca SmoothDamp ile yerine döner.
// Sadece görsel konumu etkiler: atışlar kamera merkezinden yapılır, silahın konumundan değil.
// ToolHolder bu scriptin LocalOffset değerini kendi pozisyonuna ekler.
public class WeaponObstruction : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa Camera.main kullanılır. Işın kameradan atılır.")]
    [SerializeField] private Camera cam;

    [Header("Algılama")]
    [Tooltip("Sadece duvar/zemin layer'ları (Default ve duvar layer'ları). Weapon layer'ı dahil edilmemeli.")]
    [SerializeField] private LayerMask wallLayers = 1;
    [Tooltip("Duvar bu mesafeden (kameradan, metre) yakınsa silah geri çekilmeye başlar.")]
    [SerializeField, Min(0.05f)] private float thresholdDistance = 0.6f;
    [Tooltip("Duvar bu mesafeye kadar yaklaşınca geri çekilme tam seviyeye ulaşır (metre).")]
    [SerializeField, Min(0f)] private float fullRetractDistance = 0.2f;
    [Tooltip("SphereCast yarıçapı (0.1 - 0.15 önerilir).")]
    [SerializeField, Min(0.001f)] private float castRadius = 0.12f;
    [Tooltip("Duvar kontrolü saniyede kaç kez yapılsın. Düşük değer FPS'i korur.")]
    [SerializeField, Min(0.01f)] private float checkInterval = 0.03f;

    [Header("Geri Çekme")]
    [Tooltip("Tam yakınlıkta silahın geri gideceği miktar (metre, kamera -Z).")]
    [SerializeField, Min(0f)] private float backDistance = 0.3f;
    [Tooltip("Tam yakınlıkta silahın aşağı ineceği miktar (metre, kamera -Y).")]
    [SerializeField, Min(0f)] private float downDistance = 0.12f;
    [Tooltip("Duvara yaklaşırken SmoothDamp süresi (küçük = hızlı).")]
    [SerializeField, Min(0.01f)] private float pullTime = 0.06f;
    [Tooltip("Duvardan uzaklaşınca eski yere dönüş SmoothDamp süresi.")]
    [SerializeField, Min(0.01f)] private float returnTime = 0.15f;

    [Header("Animator (opsiyonel)")]
    [Tooltip("Varsa 'NearWall' bool parametresi buradan sürülür; silah animasyonunda Lowered pozuna blend edilebilir.")]
    [SerializeField] private Animator animator;
    [SerializeField] private string nearWallParam = "NearWall";

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = false;

    // ToolHolder bu değeri her kare okur. Socket'in parent'ının (kamera) yerel uzayında.
    public Vector3 LocalOffset { get; private set; }

    private Transform root;
    private RaycastHit[] hits;
    private float checkTimer;
    private float targetAmount;   // 0..1 yakınlık oranı
    private float amount;
    private float amountVelocity;
    private int nearWallHash;
    private bool lastHitValid;
    private RaycastHit lastHit;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        root = transform.root;
        hits = new RaycastHit[16];
        if (animator != null) nearWallHash = Animator.StringToHash(nearWallParam);
    }

    private void Update()
    {
        if (cam == null) return;

        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            targetAmount = ComputeTargetAmount();
            if (animator != null) animator.SetBool(nearWallHash, targetAmount > 0.01f);
        }

        float smooth = targetAmount > amount ? pullTime : returnTime;
        amount = Mathf.SmoothDamp(amount, targetAmount, ref amountVelocity, smooth);

        LocalOffset = new Vector3(0f, -downDistance, -backDistance) * amount;
    }

    // Kameradan ileri atılan SphereCast'e göre 0 (uzak) .. 1 (çok yakın) yakınlık oranı döner.
    private float ComputeTargetAmount()
    {
        Transform t = cam.transform;
        int count = Physics.SphereCastNonAlloc(t.position, castRadius, t.forward, hits,
            thresholdDistance, wallLayers, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        lastHitValid = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null) continue;
            if (h.collider.transform.root == root) continue; // oyuncu ve silah collider'ları hariç
            // Başlangıçta içinde kalınan collider'larda distance 0 gelir; bunlar da "çok yakın" sayılır.
            if (h.distance < nearest)
            {
                nearest = h.distance;
                lastHit = h;
                lastHitValid = true;
            }
        }

        if (!lastHitValid) return 0f;

        float range = Mathf.Max(0.001f, thresholdDistance - fullRetractDistance);
        return Mathf.Clamp01((thresholdDistance - nearest) / range);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || cam == null) return;
        Gizmos.color = lastHitValid ? Color.red : Color.green;
        Vector3 end = cam.transform.position + cam.transform.forward * thresholdDistance;
        Gizmos.DrawLine(cam.transform.position, end);
        Gizmos.DrawWireSphere(end, castRadius);
    }
}
