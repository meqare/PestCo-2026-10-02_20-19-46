using UnityEngine;
using UnityEngine.AI;

// Tavanda gezen örümcek. NavMeshAgent kullanmaz: tavanın alt yüzüne NavMesh bake edilemediği için
// zemindeki NavMesh oda sınırı olarak kullanılır ve örümcek tavan yüksekliğinde (raycast ile bulunur) yürür.
// Oyuncu yaklaşınca ip ile sarkıp ısırır ve hızla yukarı çıkar. Belirli aralıkla zemine ağ bırakır.
public class Spider : MonoBehaviour, IDamageable
{
    private enum State { Wander, Drop, Retreat, Flee, Dead }

    public static int TotalKills { get; private set; }

    private static Transform playerRoot;
    private static Transform head;
    private static PlayerStatus playerStatus;

    [Header("Ceiling")]
    [Tooltip("Tavan raycast ile bulunamazsa kullanılan yedek yükseklik (m).")]
    [SerializeField, Min(0.5f)] private float spiderCeilingHeight = 2.5f;
    [Tooltip("Gövde merkezinin tavandan ne kadar aşağıda durduğu (m).")]
    [SerializeField, Min(0f)] private float ceilingOffset = 0.07f;
    [SerializeField, Min(0.1f)] private float wanderSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;

    [Header("Drop Attack")]
    [Tooltip("Oyuncu bu mesafeye (m) girince ip ile sarkar.")]
    [SerializeField, Min(0.5f)] private float spiderDropRange = 3f;
    [SerializeField, Min(0.5f)] private float spiderDropSpeed = 5f;
    [SerializeField, Min(0f)] private float spiderBiteDamage = 10f;
    [SerializeField, Min(0.5f)] private float retreatSpeed = 7f;
    [Tooltip("Oyuncuya bu kadar yaklaşınca ısırır (m).")]
    [SerializeField, Min(0.1f)] private float biteRange = 0.6f;
    [Tooltip("Sarkarken oyuncuya doğru yatay sallanma hızı (m/s).")]
    [SerializeField, Min(0f)] private float swingSpeed = 2.5f;
    [SerializeField, Min(0f)] private float dropCooldown = 4f;

    [Header("Jumpscare")]
    [Tooltip("Sarkma başladığında yüze doğru fırlama olasılığı (0-1).")]
    [SerializeField, Range(0f, 1f)] private float jumpscareChance = 0.35f;
    [Tooltip("Oyuncuya bu mesafeye (m) yaklaşınca yüze fırlar.")]
    [SerializeField, Min(0.5f)] private float jumpscareTriggerRange = 1.6f;
    [SerializeField, Min(1f)] private float jumpscareLungeSpeed = 12f;
    [Tooltip("Fırlarken örümcek bu kadar katına büyür.")]
    [SerializeField, Min(1f)] private float jumpscareScale = 1.8f;
    [SerializeField, Min(0f)] private float jumpscareShake = 0.6f;

    [Header("Web")]
    [SerializeField, Min(0.5f)] private float spiderWebInterval = 3f;
    [SerializeField, Min(0f)] private float webSlowDuration = 2f;
    [SerializeField, Range(0.1f, 1f)] private float webSlowMultiplier = 0.5f;
    [SerializeField, Min(1f)] private float webLifetime = 30f;
    [SerializeField] private Material webMaterial;
    [SerializeField] private Material webRayMaterial;

    [Header("Flee (hasar alınca)")]
    [SerializeField, Min(0.1f)] private float fleeSpeed = 4.5f;
    [SerializeField, Min(0.1f)] private float fleeDuration = 3f;
    [SerializeField, Min(0.1f)] private float zigzagSpeed = 6f;
    [SerializeField, Range(0f, 1f)] private float zigzagAmount = 0.6f;

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 20f;
    [Tooltip("Ceset yerde bu kadar sn kalır, sonra küçülüp kaybolur.")]
    [SerializeField, Min(0f)] private float corpseTime = 20f;

    [Header("References")]
    [SerializeField] private Transform visual;
    [SerializeField] private LineRenderer thread;
    [SerializeField] private Collider bodyCollider;
    [SerializeField] private Transform[] legs;

    public bool IsAlive => state != State.Dead;

    private State state = State.Wander;
    private float health;
    private float floorY;
    private float ceilingY;
    private bool initialized;
    private NavMeshQueryFilter filter;
    private Quaternion[] legBaseRot;

    private Vector3 target;
    private bool hasTarget;
    private float idleTimer;
    private float thinkTimer;
    private float nextDropTime;
    private float nextWebTime;
    private float stateTimer;
    private Vector3 anchor;
    private Vector3 hangPos;
    private float retreatStartY;
    private bool jumpscare;
    private bool lunging;
    private float scaleMul = 1f;
    private float tiltX;
    private float tiltZ;
    private bool fleeAfterRetreat;
    private float fleeSide = 1f;
    private float blockedUntil;
    private float roll = 180f;
    private float walkPhase;
    private bool moving;
    private float verticalVelocity;
    private float deadTimer;
    private Vector3 baseScale;

    private void Awake()
    {
        health = maxHealth;
        baseScale = transform.localScale;
        if (legs != null)
        {
            legBaseRot = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++)
                legBaseRot[i] = legs[i].localRotation;
        }
        if (thread != null)
        {
            thread.useWorldSpace = true;
            thread.positionCount = 2;
            thread.enabled = false;
        }
    }

    private void Start()
    {
        CachePlayer();
        if (!initialized)
        {
            // Sahneye elle koyulduysa: bulunduğu noktanın altındaki zemini bul.
            Vector3 p = transform.position;
            Vector3 floorPoint = Physics.Raycast(p, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point : new Vector3(p.x, 0f, p.z);
            int agentType = PestPool.Instance != null ? PestPool.Instance.AgentTypeID : 0;
            Init(floorPoint, new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas });
        }
    }

    private static void CachePlayer()
    {
        if (playerRoot == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerRoot = p.transform;
                playerStatus = p.GetComponent<PlayerStatus>();
            }
        }
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
    }

    // Spawner çağırır: zemin noktasının üstündeki tavanı bulur ve örümceği tavana yerleştirir.
    public void Init(Vector3 floorPoint, NavMeshQueryFilter navFilter)
    {
        initialized = true;
        filter = navFilter;
        floorY = floorPoint.y;

        float ceiling = floorPoint.y + spiderCeilingHeight + ceilingOffset;
        if (Physics.Raycast(floorPoint + Vector3.up * 0.3f, Vector3.up, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            ceiling = hit.point.y;
        ceilingY = ceiling - ceilingOffset;

        transform.position = new Vector3(floorPoint.x, ceilingY, floorPoint.z);
        roll = 180f;
        ApplyRoll();
        nextWebTime = Time.time + Random.Range(0f, spiderWebInterval);
        nextDropTime = Time.time + Random.Range(1f, 3f);
        idleTimer = Random.Range(0.2f, 1.5f);
    }

    private void Update()
    {
        if (!initialized) return;
        float dt = Time.deltaTime;
        moving = false;

        switch (state)
        {
            case State.Wander: UpdateWander(dt); break;
            case State.Flee: UpdateFlee(dt); break;
            case State.Drop: UpdateDrop(dt); break;
            case State.Retreat: UpdateRetreat(dt); break;
            case State.Dead: UpdateDead(dt); break;
        }

        if (state != State.Dead)
        {
            scaleMul = Mathf.MoveTowards(scaleMul, lunging ? jumpscareScale : 1f, (lunging ? 14f : 4f) * dt);
            transform.localScale = baseScale * scaleMul;
        }

        bool hanging = state == State.Drop || state == State.Retreat;
        if (!hanging)
        {
            tiltX = 0f;
            tiltZ = 0f;
        }
        float targetRoll = hanging ? 0f : 180f;
        roll = Mathf.MoveTowards(roll, targetRoll, 540f * dt);
        ApplyRoll();
        AnimateLegs();
    }

    // ---------- Gezinme ----------

    private void UpdateWander(float dt)
    {
        if (!hasTarget)
        {
            idleTimer -= dt;
            if (idleTimer <= 0f) PickTarget();
        }
        else
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.1f)
            {
                hasTarget = false;
                idleTimer = Random.Range(0.4f, 1.6f);
            }
            else
            {
                Step(to / dist, wanderSpeed, dt);
            }
        }

        TickWeb();

        // Düşünme 0.2 sn'de bir: sarkma kararı (ucuz).
        thinkTimer += dt;
        if (thinkTimer >= 0.2f)
        {
            thinkTimer = 0f;
            TryStartDrop();
        }
    }

    // Zemin NavMesh'i oda sınırı: geçerli bir zemin noktasından NavMesh.Raycast ile duvara kadar gidilebilir hedef seçer.
    private void PickTarget()
    {
        Vector3 from = new Vector3(transform.position.x, floorY, transform.position.z);
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, filter))
        {
            idleTimer = 0.5f;
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            float angle = Random.Range(0f, 360f);
            float dist = Random.Range(1f, 3f);
            Vector3 cand = start.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
            if (NavMesh.Raycast(start.position, cand, out NavMeshHit edge, filter))
                cand = edge.position;

            Vector3 delta = cand - start.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.4f * 0.4f) continue;

            target = new Vector3(cand.x, ceilingY, cand.z);
            hasTarget = true;
            return;
        }
        idleTimer = 0.5f;
    }

    // Tavan düzleminde bir adım atar ve gövdeyi yürüdüğü yöne çevirir.
    private void Step(Vector3 dir, float speed, float dt)
    {
        Vector3 p = transform.position + dir * (speed * dt);
        p.y = ceilingY;
        transform.position = p;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), turnSpeed * dt);
        walkPhase += speed * dt * 9f;
        moving = true;
    }

    private void TickWeb()
    {
        if (Time.time < nextWebTime) return;
        nextWebTime = Time.time + spiderWebInterval;
        Vector3 p = transform.position;
        SpiderWebManager.Spawn(new Vector3(p.x, floorY + 0.25f, p.z), webMaterial, webRayMaterial, webLifetime, webSlowMultiplier, webSlowDuration);
    }

    // ---------- Saldırı ----------

    private void TryStartDrop()
    {
        if (Time.time < nextDropTime || head == null) return;
        Vector3 toHead = head.position - transform.position;
        if (toHead.sqrMagnitude > spiderDropRange * spiderDropRange) return;

        // Arada duvar/eşya varsa sarkma (oyuncunun kendi collider'ı engel sayılmaz).
        if (Physics.Linecast(transform.position, head.position, out RaycastHit hit, 1, QueryTriggerInteraction.Ignore)
            && (playerRoot == null || !hit.collider.transform.IsChildOf(playerRoot)))
            return;

        state = State.Drop;
        stateTimer = 0f;
        hasTarget = false;
        anchor = new Vector3(transform.position.x, ceilingY + ceilingOffset, transform.position.z);
        hangPos = transform.position;
        jumpscare = Random.value < jumpscareChance;
        lunging = false;
        if (thread != null)
        {
            thread.enabled = true;
            SetThreadAlpha(1f);
        }
    }

    private void UpdateDrop(float dt)
    {
        stateTimer += dt;
        if (head == null) { StartRetreat(false); return; }

        Vector3 p = hangPos;
        Vector3 hp = head.position;

        // Jumpscare: yeterince yaklaşınca aniden yüze doğru fırlar ve büyür.
        if (jumpscare && !lunging && (hangPos - hp).sqrMagnitude < jumpscareTriggerRange * jumpscareTriggerRange)
            lunging = true;
        if (lunging)
        {
            hangPos = Vector3.MoveTowards(hangPos, hp + head.forward * 0.3f, jumpscareLungeSpeed * dt);
            ApplyHangSway();
            UpdateThread();
            Vector3 face = hp - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            if ((transform.position - hp).sqrMagnitude <= biteRange * biteRange)
                Bite();
            return;
        }

        float targetY = Mathf.Max(floorY + 0.3f, hp.y - 0.15f);
        p.y = Mathf.MoveTowards(p.y, targetY, spiderDropSpeed * dt);
        Vector3 flat = new Vector3(hp.x - p.x, 0f, hp.z - p.z);
        float flatDist = flat.magnitude;
        if (flatDist > 0.05f)
            p += flat / flatDist * (Mathf.Min(swingSpeed * dt, flatDist));
        hangPos = p;
        ApplyHangSway();
        UpdateThread();

        p = transform.position;
        if ((p - hp).sqrMagnitude <= biteRange * biteRange)
        {
            Bite();
            return;
        }

        float giveUp = spiderDropRange * 1.6f;
        if (stateTimer > 4f || (p - hp).sqrMagnitude > giveUp * giveUp)
            StartRetreat(false);
    }

    private void Bite()
    {
        if (playerStatus != null)
            playerStatus.TakeDamage(spiderBiteDamage, head.position, Vector3.down);
        Juice.PlayerHurt();
        if (lunging)
            Juice.Shake(jumpscareShake, 0.4f);
        StartRetreat(false);
    }

    private void StartRetreat(bool thenFlee)
    {
        state = State.Retreat;
        fleeAfterRetreat = thenFlee;
        retreatStartY = Mathf.Max(hangPos.y, ceilingY + 0.01f);
        lunging = false;
        nextDropTime = Time.time + dropCooldown;
    }

    // Hızla ipten yukarı çıkar; tavana varınca ipi toplar.
    private void UpdateRetreat(float dt)
    {
        Vector3 p = hangPos;
        p.y = Mathf.MoveTowards(p.y, ceilingY, retreatSpeed * dt);
        Vector3 flat = new Vector3(anchor.x - p.x, 0f, anchor.z - p.z);
        float flatDist = flat.magnitude;
        if (flatDist > 0.02f)
            p += flat / flatDist * Mathf.Min(retreatSpeed * dt, flatDist);
        hangPos = p;
        ApplyHangSway();
        UpdateThread();

        // Yukarı çıktıkça ip solar.
        SetThreadAlpha(Mathf.InverseLerp(ceilingY, retreatStartY, p.y));

        if (Mathf.Abs(p.y - ceilingY) < 0.02f && flatDist < 0.05f)
        {
            transform.position = new Vector3(p.x, ceilingY, p.z);
            tiltX = 0f;
            tiltZ = 0f;
            if (thread != null) thread.enabled = false;
            hasTarget = false;
            idleTimer = 0.3f;
            if (fleeAfterRetreat) EnterFlee();
            else state = State.Wander;
        }
    }

    // İpin ucunda sarkan sarkaç hareketi: derinlik arttıkça salınım büyür, gövde de sallanır.
    private void ApplyHangSway()
    {
        float depth = Mathf.Clamp01((ceilingY - hangPos.y) / Mathf.Max(0.5f, ceilingY - floorY));
        float amp = lunging ? 0f : 0.01f + 0.05f * depth;
        float sx = Mathf.Sin(Time.time * 3.1f);
        float sz = Mathf.Sin(Time.time * 2.3f + 1f);
        transform.position = hangPos + new Vector3(sx, 0f, sz) * amp;
        float tilt = lunging ? 0f : 6f + 16f * depth;
        tiltX = sz * tilt;
        tiltZ = -sx * tilt;
    }

    private void SetThreadAlpha(float a)
    {
        if (thread == null) return;
        Color c = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        thread.startColor = c;
        thread.endColor = c;
    }

    private void UpdateThread()
    {
        if (thread == null || !thread.enabled) return;
        thread.SetPosition(0, anchor);
        thread.SetPosition(1, transform.position + Vector3.up * 0.04f);
    }

    // ---------- Kaçış ----------

    private void EnterFlee()
    {
        state = State.Flee;
        stateTimer = 0f;
        hasTarget = false;
    }

    // Oyuncudan uzağa, sağ-sola salınarak ve ileri-geri atılarak kaçar (zigzag).
    private void UpdateFlee(float dt)
    {
        stateTimer += dt;
        if (stateTimer >= fleeDuration)
        {
            state = State.Wander;
            idleTimer = 0.3f;
            return;
        }

        Vector3 away = head != null ? transform.position - head.position : transform.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = transform.forward;
        away.Normalize();
        Vector3 perp = Vector3.Cross(Vector3.up, away);

        Vector3 baseDir = Time.time < blockedUntil ? perp * fleeSide : away;
        float wave = Mathf.Sin(Time.time * zigzagSpeed);
        Vector3 dir = (baseDir + perp * (wave * zigzagAmount)).normalized;

        // İleri-geri atılma: hız dalgalanır.
        float speed = fleeSpeed * (0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * zigzagSpeed * 0.5f)));

        thinkTimer += dt;
        if (thinkTimer >= 0.2f)
        {
            thinkTimer = 0f;
            Vector3 probe = transform.position + dir * 0.8f;
            if (!NavMesh.SamplePosition(new Vector3(probe.x, floorY, probe.z), out _, 0.3f, filter))
            {
                fleeSide = -fleeSide;
                blockedUntil = Time.time + 0.6f;
            }
        }

        Step(dir, speed, dt);
        TickWeb();
    }

    // ---------- Hasar / ölüm ----------

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsAlive || amount <= 0f) return;

        health -= amount;
        if (health <= 0f)
        {
            Die(hitDirection);
            return;
        }

        Juice.Hit(hitPoint, hitDirection);
        if (state == State.Drop)
            StartRetreat(true);
        else if (state == State.Wander)
            EnterFlee();
    }

    private void Die(Vector3 hitDirection)
    {
        state = State.Dead;
        TotalKills++;
        verticalVelocity = 0f;
        deadTimer = 0f;
        if (thread != null) thread.enabled = false;
        if (bodyCollider != null) bodyCollider.enabled = false;
        Juice.PestDeath(transform.position, hitDirection);
    }

    // Tavandan düşer, sırtüstü yatar, bir süre bekler, küçülüp kaybolur.
    private void UpdateDead(float dt)
    {
        Vector3 p = transform.position;
        float groundY = floorY + 0.03f;
        if (p.y > groundY)
        {
            verticalVelocity -= 12f * dt;
            p.y = Mathf.Max(groundY, p.y + verticalVelocity * dt);
            transform.position = p;
            return;
        }

        deadTimer += dt;
        if (deadTimer > corpseTime)
        {
            float k = 1f - (deadTimer - corpseTime);
            if (k <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = baseScale * k;
        }
    }

    // ---------- Görsel ----------

    // Tavanda ayaklar tavana bakar (roll 180); sarkarken dik durur (roll 0).
    private void ApplyRoll()
    {
        if (visual != null)
            visual.localRotation = Quaternion.Euler(tiltX, 0f, roll + tiltZ);
    }

    // Yürürken bacaklar sinüs dalgasıyla yan salınır ve kalkar; her bacak 45° (π/4) kaymış fazdadır
    // (komşu bacaklar zıt yönde: yürüyüş düzeni). Dururken hafif titrer.
    private void AnimateLegs()
    {
        if (legs == null || legBaseRot == null) return;

        float swing = moving ? 15f : 0f;
        float lift = moving ? 8f : 0f;
        for (int i = 0; i < legs.Length; i++)
        {
            float phase = walkPhase + i * (Mathf.PI * 0.25f);
            float yaw = Mathf.Sin(phase) * swing;
            float pitch = Mathf.Cos(phase) * lift;
            if (!moving)
            {
                // Durma titremesi: küçük, hızlı, bacak başına farklı.
                yaw = (Mathf.PerlinNoise(Time.time * 9f, i * 7.3f) - 0.5f) * 5f;
                pitch = (Mathf.PerlinNoise(i * 3.1f, Time.time * 9f) - 0.5f) * 3f;
            }
            legs[i].localRotation = legBaseRot[i] * Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
