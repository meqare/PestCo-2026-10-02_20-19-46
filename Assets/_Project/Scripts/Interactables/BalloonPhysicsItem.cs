using UnityEngine;

// Balon gibi şişen eşya. VacuumTool nişan aldığı eşyayı TryAttach ile silahın ucuna (kinematik çapa) bağlar:
//  - Bağlıyken yavaşça şişer (maxBalloonSize'a kadar), yerçekimi kapanır, yukarı kaldırma kuvveti uygulanır.
//  - Bir ConfigurableJoint (yaylı ip) onu oyuncuya bağlar; oyuncu yürüdükçe arkasında sallanarak takip eder.
//  - Bağlıyken duvara/tavana/kapıya popSpeed'den hızlı çarparsa patlar: eşya yok olur, kazançtan ceza düşer.
//  - Vakum bırakılınca sönerek eski boyuna döner ve yere düşer.
[RequireComponent(typeof(Rigidbody))]
public class BalloonPhysicsItem : MonoBehaviour
{
    [Header("Şişme")]
    [Tooltip("En fazla kaç katı şişebilir.")]
    public float maxBalloonSize = 1.5f;
    [Tooltip("Saniyede eklenen boyut katı (1 = localScale += Vector3.one * dt).")]
    [Min(0f)] public float inflateRate = 0.3f;
    [Tooltip("Vakum bırakılınca saniyede küçülme katı.")]
    [Min(0f)] public float deflateRate = 1.5f;

    [Header("Uçuş")]
    [Tooltip("Yukarı doğru kaldırma kuvveti (ForceMode.Force, kütleyle bölünür).")]
    public float floatForce = 2.5f;
    [Tooltip("Havadaki yavaşlatma; balon sallanıp salınımı söndürür.")]
    [SerializeField, Min(0f)] private float airDrag = 1.2f;
    [SerializeField, Min(0f)] private float airAngularDrag = 0.8f;

    [Header("Bağ (ip)")]
    [Tooltip("Eşyanın kenarından itibaren ip payı (m).")]
    [SerializeField, Min(0.1f)] private float tetherSlack = 1.2f;
    [SerializeField, Min(0f)] private float tetherSpring = 40f;
    [SerializeField, Min(0f)] private float tetherDamper = 6f;

    [Header("Patlama")]
    [Tooltip("Bağlıyken bundan hızlı çarparsa patlar.")]
    public float popSpeed = 3f;
    [Tooltip("Patlayınca kazançtan düşen miktar ($).")]
    public int popPenalty = 50;
    [Tooltip("Yakalandıktan sonra bu kadar saniye çarpma sayılmaz.")]
    [SerializeField, Min(0f)] private float popGraceTime = 0.4f;
    [Tooltip("Boşsa sadece Console yazısı ve ekran sarsıntısı olur.")]
    [SerializeField] private ParticleSystem popEffect;

    private bool isAttached = false;

    public bool IsAttached => isAttached;

    private Rigidbody rb;
    private Collider[] colliders;
    private ConfigurableJoint joint;
    private Collider ignoredPlayerCollider;
    private Vector3 baseScale;
    private float baseRadius;
    private float baseDrag;
    private float baseAngularDrag;
    private float sizeFactor = 1f;
    private float attachTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        baseScale = transform.localScale;
        baseDrag = rb.linearDamping;
        baseAngularDrag = rb.angularDamping;
        rb.maxDepenetrationVelocity = 2f; // şişerken duvara gömülürse fırlamasın
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var c in colliders) bounds.Encapsulate(c.bounds);
        baseRadius = bounds.extents.magnitude;
    }

    // VacuumTool çağırır: eşyayı çapaya (silahın ucundaki kinematik Rigidbody) bağlar.
    public bool TryAttach(Rigidbody anchor, Collider playerCollider)
    {
        if (isAttached || anchor == null) return false;
        if (TryGetComponent(out GrabbableObject grabbable) && grabbable.IsHeld) return false; // elde tutulan eşya vakuma bağlanmaz

        isAttached = true;
        attachTime = Time.time;
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearDamping = airDrag;
        rb.angularDamping = airAngularDrag;
        rb.WakeUp();

        // Oyuncunun kapsülüyle çarpışmasın (balon oyuncuyu itip titretmesin).
        ignoredPlayerCollider = playerCollider;
        SetPlayerCollision(false);

        joint = gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = anchor;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedAnchor = Vector3.zero;
        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;
        joint.linearLimitSpring = new SoftJointLimitSpring { spring = tetherSpring, damper = tetherDamper };
        joint.enableCollision = false;
        UpdateTetherLimit();
        return true;
    }

    // Vakum bırakılınca: ip kopar, yerçekimi geri gelir, eşya sönmeye başlar.
    public void Release()
    {
        if (!isAttached) return;
        isAttached = false;

        if (joint != null) Destroy(joint);
        joint = null;
        SetPlayerCollision(true);
        rb.useGravity = true;
        rb.linearDamping = baseDrag;
        rb.angularDamping = baseAngularDrag;
        rb.WakeUp();
    }

    private void Update()
    {
        if (isAttached)
        {
            if (sizeFactor < maxBalloonSize)
            {
                sizeFactor = Mathf.Min(maxBalloonSize, sizeFactor + inflateRate * Time.deltaTime);
                ApplySize();
            }
        }
        else if (sizeFactor > 1f)
        {
            sizeFactor = Mathf.Max(1f, sizeFactor - deflateRate * Time.deltaTime);
            ApplySize();
        }
    }

    private void FixedUpdate()
    {
        if (!isAttached) return;
        // Uçan balon gibi sürekli yukarı kaldırma (yerçekimi kapalı).
        rb.AddForce(Vector3.up * floatForce);
    }

    private void ApplySize()
    {
        transform.localScale = baseScale * sizeFactor;
        if (joint != null) UpdateTetherLimit();
    }

    // İp boyu eşya büyüdükçe uzar; böylece şişmiş eşya oyuncunun içine girmez.
    private void UpdateTetherLimit()
    {
        joint.linearLimit = new SoftJointLimit { limit = baseRadius * sizeFactor + tetherSlack };
    }

    private void OnCollisionEnter(Collision col)
    {
        if (!isAttached || Time.time < attachTime + popGraceTime) return;
        if (col.relativeVelocity.magnitude <= popSpeed) return;
        if (col.collider.GetComponentInParent<CharacterController>() != null) return;

        Pop(col.relativeVelocity.magnitude);
    }

    // Patlama: uyarı, efekt, kazançtan ceza, eşya yok olur.
    private void Pop(float impactSpeed)
    {
        Debug.Log($"GÜÜÜM! Balon Patladı! ({name}, çarpma {impactSpeed:0.0} m/s, ceza -{popPenalty}$)", this);

        if (JobManager.Instance != null) JobManager.Instance.AddDamage(popPenalty);

        Vector3 pos = transform.position;
        Juice.Shake(0.06f, 0.3f);
        Juice.Hit(pos, Vector3.up);
        if (popEffect != null)
        {
            ParticleSystem fx = Instantiate(popEffect, pos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * sizeFactor;
            fx.Play(true);
            Destroy(fx.gameObject, 3f);
        }

        Release();
        Destroy(gameObject);
    }

    private void SetPlayerCollision(bool collide)
    {
        if (ignoredPlayerCollider == null) return;
        foreach (var c in colliders)
            if (c != null) Physics.IgnoreCollision(c, ignoredPlayerCollider, !collide);
    }

    private void OnDisable()
    {
        Release();
    }
}
