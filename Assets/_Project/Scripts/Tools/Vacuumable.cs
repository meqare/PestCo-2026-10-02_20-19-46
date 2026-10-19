using UnityEngine;

// Yeni vakum (PhysicsVacuumController) ile çekilebilen/tutulabilen nesne işaretçisi.
// Sorumluluğu: nesnenin fizik durumunu (yerçekimi, sürtünme, çarpışma) tutarken değiştirip bırakınca geri vermek.
// Çekim/tutma kuvvetini kendisi uygulamaz; o PhysicsVacuumController'ın işidir.
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class Vacuumable : MonoBehaviour
{
    [Tooltip("Kapalıysa vakum bu nesneyi hedef almaz.")]
    [SerializeField] private bool canBeVacuumed = true;
    [Tooltip("1 = normal. Büyük değer = nesne daha zor çekilir (kütleye ek direnç).")]
    [SerializeField, Min(0.1f)] private float pullResistance = 1f;

    public Rigidbody Body { get; private set; }
    public bool IsHeld { get; private set; }
    public float PullResistance => pullResistance;
    // Rigidbody kaybolduysa veya fizikten çıktıysa (kinematic) nesne çekilemez.
    public bool CanBeVacuumed => canBeVacuumed && isActiveAndEnabled && Body != null && !Body.isKinematic;

    private Collider[] colliders;
    private Collider[] ignoredAgainst;
    private bool savedGravity;
    private float savedLinearDamping;
    private float savedAngularDamping;
    private RigidbodyInterpolation savedInterpolation;
    private CollisionDetectionMode savedCollisionMode;

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>(true);
    }

    // Tutma başlar: yerçekimi kapanır (yay kuvveti nesneyi taşır), ince nesne duvardan geçmesin diye sürekli çarpışma.
    public void BeginHold(float angularDamping, Collider[] ignoreCollisionWith)
    {
        if (IsHeld || Body == null) return;
        savedGravity = Body.useGravity;
        savedLinearDamping = Body.linearDamping;
        savedAngularDamping = Body.angularDamping;
        savedInterpolation = Body.interpolation;
        savedCollisionMode = Body.collisionDetectionMode;

        Body.useGravity = false;
        Body.angularDamping = angularDamping;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Body.WakeUp();

        // Oyuncu kapsülüyle çarpışma kapanır: nesne oyuncuyu itmesin, sıkışmasın.
        ignoredAgainst = ignoreCollisionWith;
        SetIgnore(true);
        IsHeld = true;
    }

    // Bırakma: fizik durumu eski haline döner, hız korunur (nesne doğal şekilde düşer/uçar).
    public void EndHold()
    {
        if (!IsHeld) return;
        IsHeld = false;
        SetIgnore(false);
        ignoredAgainst = null;
        if (Body == null) return;
        Body.useGravity = savedGravity;
        Body.linearDamping = savedLinearDamping;
        Body.angularDamping = savedAngularDamping;
        Body.interpolation = savedInterpolation;
        Body.collisionDetectionMode = savedCollisionMode;
        Body.WakeUp();
    }

    // Tüm collider'ların birleşik sınırı: tutma noktası ve gösterge boyutu için.
    public Bounds GetWorldBounds()
    {
        Bounds b = default;
        bool first = true;
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null || !c.enabled) continue;
            if (first) { b = c.bounds; first = false; }
            else b.Encapsulate(c.bounds);
        }
        if (first) b = new Bounds(transform.position, Vector3.one * 0.2f);
        return b;
    }

    private void SetIgnore(bool ignore)
    {
        if (ignoredAgainst == null || colliders == null) return;
        for (int i = 0; i < colliders.Length; i++)
        {
            var own = colliders[i];
            if (own == null || !own.enabled || !own.gameObject.activeInHierarchy) continue;
            for (int j = 0; j < ignoredAgainst.Length; j++)
            {
                var other = ignoredAgainst[j];
                if (other == null || !other.enabled || !other.gameObject.activeInHierarchy) continue;
                Physics.IgnoreCollision(own, other, ignore);
            }
        }
    }

    // Nesne devre dışı kalır/yok edilirse fizik durumu geri verilir.
    private void OnDisable() => EndHold();
}
