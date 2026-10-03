using UnityEngine;
using UnityEngine.InputSystem;

// Sprey aletinin üzerinde durur. Sol tık basılıyken kameranın önündeki konide kalan IDamageable'lara hasar verir.
public class SprayTool : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    [SerializeField] private Camera cam;
    [Tooltip("Doluysa kapasite bitince hasar da durur.")]
    [SerializeField] private SprayVisual sprayVisual;

    [Header("Cone")]
    [SerializeField, Min(0.1f)] private float range = 3.5f;
    [SerializeField, Range(1f, 179f)] private float coneAngle = 25f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damagePerTick = 10f;
    [SerializeField, Min(0.01f)] private float tickInterval = 0.1f;
    [SerializeField, Min(1)] private int maxTargetsPerTick = 20;

    [Header("Layers")]
    [Tooltip("Böceklerin layer'ı.")]
    [SerializeField] private LayerMask targetLayers;
    [Tooltip("Spreyi engelleyen duvar vb. Böcek layer'ını buraya EKLEME.")]
    [SerializeField] private LayerMask obstacleLayers;
    [Tooltip("OverlapSphere'in tek seferde bulabileceği en fazla collider.")]
    [SerializeField, Min(8)] private int colliderBufferSize = 128;

    private Collider[] hitBuffer;
    private IDamageable[] damagedThisTick;
    private Transform camTransform;
    private float tickTimer;

    // Kamerayı bulur ve dizileri bir kere oluşturur (Update'te hiç new yok).
    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;
        if (sprayVisual == null)
            sprayVisual = GetComponent<SprayVisual>();

        camTransform = cam != null ? cam.transform : transform;
        hitBuffer = new Collider[colliderBufferSize];
        damagedThisTick = new IDamageable[maxTargetsPerTick];
    }

    // Tuş basılıysa sayacı ilerletir, her tickInterval'da bir hasar uygular.
    private void Update()
    {
        var mouse = Mouse.current;
        bool spraying = mouse != null && mouse.leftButton.isPressed;
        if (spraying && sprayVisual != null && sprayVisual.CapacityPercent <= 0f)
            spraying = false;

        if (!spraying)
        {
            // Bir sonraki basışta ilk hasar hemen gelsin.
            tickTimer = tickInterval;
            return;
        }

        tickTimer += Time.deltaTime;
        if (tickTimer < tickInterval) return;
        tickTimer -= tickInterval;

        DamageTargetsInCone();
    }

    // Küre -> açı -> engel sırasıyla eler, geçenlere hasar verir.
    private void DamageTargetsInCone()
    {
        Vector3 origin = camTransform.position;
        Vector3 forward = camTransform.forward;
        float halfAngle = coneAngle * 0.5f;

        int count = Physics.OverlapSphereNonAlloc(origin, range, hitBuffer, targetLayers, QueryTriggerInteraction.Collide);
        int damagedCount = 0;

        for (int i = 0; i < count && damagedCount < maxTargetsPerTick; i++)
        {
            Collider col = hitBuffer[i];
            Vector3 targetPos = col.bounds.center;
            Vector3 toTarget = targetPos - origin;

            // 2. Koninin içinde mi?
            if (Vector3.Angle(forward, toTarget) > halfAngle) continue;

            // 3. Arada duvar var mı?
            if (Physics.Linecast(origin, targetPos, obstacleLayers, QueryTriggerInteraction.Ignore)) continue;

            // 4. Hasar alabiliyor mu ve bu tick'te zaten vurulmadı mı? (birden fazla collider'ı olan böcek için)
            if (!col.TryGetComponent(out IDamageable target) || !target.IsAlive) continue;
            if (AlreadyDamaged(target, damagedCount)) continue;

            damagedThisTick[damagedCount++] = target;
            target.TakeDamage(damagePerTick, targetPos, toTarget.normalized);
        }

        // Ölen objelere referans tutmayalım.
        for (int i = 0; i < damagedCount; i++)
            damagedThisTick[i] = null;
    }

    // Bu tick'te aynı hedefe zaten hasar verildi mi kontrol eder.
    private bool AlreadyDamaged(IDamageable target, int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
            if (damagedThisTick[i] == target) return true;
        return false;
    }

    // Seçiliyken Scene penceresinde koniyi sarı çizgilerle çizer.
    private void OnDrawGizmosSelected()
    {
        Transform t = cam != null ? cam.transform : (Camera.main != null ? Camera.main.transform : transform);
        Vector3 origin = t.position;
        Vector3 forward = t.forward;
        float halfRad = coneAngle * 0.5f * Mathf.Deg2Rad;

        // Koninin ucundaki çember: merkez ve yarıçap
        Vector3 endCenter = origin + forward * (range * Mathf.Cos(halfRad));
        float endRadius = range * Mathf.Sin(halfRad);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + forward * range);

        const int segments = 24;
        Vector3 prev = endCenter + t.up * endRadius;
        for (int i = 1; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 point = endCenter + (t.up * Mathf.Cos(a) + t.right * Mathf.Sin(a)) * endRadius;
            Gizmos.DrawLine(prev, point);
            if (i % 6 == 0)
                Gizmos.DrawLine(origin, point);
            prev = point;
        }
        
    }
}
