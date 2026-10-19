using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vakum silahının ucunda oluşan fiziksel eşya kulesini yönetir.
/// Eşyaları üst üste dizer ve oyuncunun hareketine göre kuleyi yalpalatır.
/// Kolay erişim için Singleton olarak tasarlanmıştır.
/// </summary>
public class WobblyTowerManager : MonoBehaviour
{
    public static WobblyTowerManager Instance { get; private set; }

    [Header("Kule Referansları")]
    [Tooltip("Kulenin başlayacağı, vakum ucundaki havada asılı nokta.")]
    [SerializeField] private Transform towerBasePoint;

    [Tooltip("Hareketi ölçülecek nesne. Boşsa bu nesnenin Transform'u kullanılır.")]
    [SerializeField] private Transform playerTransform;

    [Header("Yerleşim Ayarları")]
    [Tooltip("Üst üste binen eşyalar arasındaki dikey mesafe.")]
    [SerializeField] private float yOffsetBetweenItems = 0.5f;

    [Header("Yalpalama (Wobble) Ayarları")]
    [Tooltip("Oyuncu hareket ettikçe kulenin sallanma çarpanı.")]
    [SerializeField] private float wobbleIntensity = 5f;

    [Tooltip("Fare ile dönüşün kuleye yaptığı ek sallanma çarpanı.")]
    [SerializeField] private float turnWobbleMultiplier = 1.5f;

    [Tooltip("Kuledeki her eşya için sallanmayı artıran oran (0.1 = eşya başına %10).")]
    [SerializeField] private float stackWobbleMultiplier = 0.1f;

    [Tooltip("Kulenin maksimum eğilme açısı (derece).")]
    [SerializeField] private float maxTiltAngle = 45f;

    [Tooltip("Sallanmanın ne kadar hızlı yumuşayacağı. Küçük değer = daha sert tepki.")]
    [SerializeField] private float tiltSmoothTime = 0.15f;

    [Header("Yıkılma Ayarları")]
    [Tooltip("Kulenin eğilme açısı bu değeri aşarsa kule devrilir (derece).")]
    public float maxWobbleAngle = 35f;

    [Tooltip("Kilitli bir eşya katı bir nesneye bu hızın (m/s) üzerinde çarparsa kule devrilir.")]
    [SerializeField] private float collapseImpactSpeed = 2f;

    [Tooltip("Yıkılırken eşyalara verilen rastgele saçılma itkisi.")]
    [SerializeField] private float scatterImpulse = 1.5f;

    [Tooltip("Eşya eklendikten sonra çarpışma kontrolünün başlamadan önce beklediği süre (sn).")]
    [SerializeField] private float collisionGracePeriod = 0.3f;

    [Header("Physical Tower (Delivery Zone)")]
    [Tooltip("Items inside this zone form the physical tower. Leave empty to use only the old vacuum-tip tower.")]
    [SerializeField] private Collider deliveryZone;

    [Tooltip("Tower counts as toppled when its axis tilts more than this angle (degrees).")]
    [SerializeField] private float topplingAngleThreshold = 35f;

    [Tooltip("When assigned, the toppling angle is read from this config instead of the field above.")]
    [SerializeField] private GameConfig config;

    private float ToppleAngle => config != null ? config.topplingAngleThreshold : topplingAngleThreshold;

    [Tooltip("How far above the zone floor items are still considered part of the tower.")]
    [SerializeField] private float stackSearchHeight = 6f;

    [Tooltip("An item must stay in the zone this long (s) before it counts as part of the tower.")]
    [SerializeField] private float stackSettleTime = 0.5f;

    [Tooltip("A tower item further than this outside the zone footprint counts as fallen.")]
    [SerializeField] private float fallMargin = 0.6f;

    [Tooltip("Tower center of mass must be at least this high above the base to measure an angle.")]
    [SerializeField] private float minTowerHeight = 0.5f;

    [Tooltip("Items within this height of the lowest item form the tower base.")]
    [SerializeField] private float bottomLayerHeight = 0.5f;

    [SerializeField] private bool showDebugText = true;

    /// <summary>Fired once when the physical tower topples (angle too large or an item fell out).</summary>
    public event System.Action OnTowerToppled;

    public bool IsToppled => _toppled;
    public float CurrentTowerAngle { get; private set; }
    public Vector3 TowerCenterOfMass { get; private set; }
    public int PhysicalStackCount { get; private set; }

    /// <summary>Items currently counted as part of the physical tower (read-only snapshot of the last physics step).</summary>
    public IReadOnlyList<TowerItem> SettledItems => _settledItems;

    private readonly HashSet<TowerItem> _ignoredItems = new HashSet<TowerItem>();

    /// <summary>Points of items that broke (e.g. cursed vase) during the current round. Reset every round.</summary>
    public int BankedScore { get; private set; }

    public void AddBankedScore(int points)
    {
        BankedScore += points;
        BankedItemCount++;
    }

    /// <summary>Number of items that broke (and banked points) during the current round.</summary>
    public int BankedItemCount { get; private set; }

    /// <summary>Called by a cursed item whenever it breaks, even if it belongs to an earlier round.</summary>
    public void NotifyCursedBreak()
    {
        _lastCursedBreakTime = Time.time;
    }

    /// <summary>True if a cursed item broke within the last few seconds (used to label topple causes).</summary>
    public bool RecentlyBrokeCursedItem => Time.time - _lastCursedBreakTime < 5f;

    private float _lastCursedBreakTime = -100f;

    /// <summary>True if the item belongs to an earlier round (it stays in the van but no longer scores).</summary>
    public bool IsIgnored(TowerItem item)
    {
        return _ignoredItems.Contains(item);
    }

    private readonly Dictionary<TowerItem, float> _firstSeenInZone = new Dictionary<TowerItem, float>();
    private readonly List<TowerItem> _removeBuffer = new List<TowerItem>();
    private readonly List<TowerItem> _settledItems = new List<TowerItem>();
    private readonly Collider[] _zoneBuffer = new Collider[64];
    private bool _toppled;
    private string _toppleReason = "";

    private readonly List<TowerItem> _currentStack = new List<TowerItem>();
    private readonly Collider[] _overlapBuffer = new Collider[16];

    private Vector3 _lastBasePosition;
    private Vector3 _baseVelocity;
    private float _lastAddTime = -10f;

    private Vector3 _lastPlayerPosition;
    private float _tiltX;
    private float _tiltZ;
    private float _tiltVelocityX;
    private float _tiltVelocityZ;

    /// <summary>Kuledeki eşya sayısı.</summary>
    public int StackCount => _currentStack.Count;

    /// <summary>Kulenin başladığı nokta (vakum çekim hedefi).</summary>
    public Transform TowerBasePoint => towerBasePoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (towerBasePoint == null && deliveryZone == null)
        {
            Debug.LogError("[WobblyTowerManager] towerBasePoint atanmamış!", this);
        }

        if (playerTransform == null)
        {
            playerTransform = transform;
        }
    }

    private void Start()
    {
        _lastPlayerPosition = playerTransform.position;
        _lastBasePosition = towerBasePoint != null ? towerBasePoint.position : Vector3.zero;
    }

    private void Update()
    {
        if (towerBasePoint == null)
        {
            return;
        }

        ApplyWobble(Time.deltaTime);

        if (_currentStack.Count > 0 &&
            Quaternion.Angle(Quaternion.identity, towerBasePoint.localRotation) > maxWobbleAngle)
        {
            CollapseTower();
        }
    }

    private void FixedUpdate()
    {
        UpdatePhysicalTower();

        if (towerBasePoint == null)
        {
            return;
        }

        _baseVelocity = (towerBasePoint.position - _lastBasePosition) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        _lastBasePosition = towerBasePoint.position;

        // Kinematik eşyalar statik nesnelere çarpınca OnCollisionEnter tetiklenmez;
        // bu yüzden kilitli eşyaların çevresindeki katı nesneler ayrıca taranır.
        if (_currentStack.Count == 0 || _baseVelocity.magnitude < collapseImpactSpeed)
        {
            return;
        }

        for (int i = 0; i < _currentStack.Count; i++)
        {
            if (IsTouchingSolid(_currentStack[i]))
            {
                Debug.Log($"[KULE] '{_currentStack[i].name}' sert bir nesneye çarptı.", _currentStack[i]);
                CollapseTower();
                return;
            }
        }
    }

    /// <summary>Clears the toppled state so a new tower can be tracked (e.g. next round).</summary>
    public void ResetToppled()
    {
        _toppled = false;
        _toppleReason = "";
        _firstSeenInZone.Clear();
    }

    /// <summary>
    /// Starts a fresh tower: items currently tracked stay in the van physically but are ignored from now on
    /// (they neither score again nor affect the new tower's angle).
    /// </summary>
    public void StartNewRound()
    {
        foreach (TowerItem item in _firstSeenInZone.Keys)
        {
            if (item != null)
            {
                _ignoredItems.Add(item);
            }
        }

        _settledItems.Clear();
        PhysicalStackCount = 0;
        CurrentTowerAngle = 0f;
        BankedScore = 0;
        BankedItemCount = 0;
        ResetToppled();
    }

    private Bounds GetSearchBounds()
    {
        Bounds zb = deliveryZone.bounds;
        Vector3 min = zb.min;
        Vector3 max = new Vector3(zb.max.x, zb.min.y + stackSearchHeight, zb.max.z);
        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    /// <summary>
    /// Physics-step tower tracking: collects items resting in the delivery zone, computes their
    /// mass-weighted center of mass and the tower axis tilt, and detects toppling once.
    /// </summary>
    private void UpdatePhysicalTower()
    {
        if (deliveryZone == null || _toppled)
        {
            return;
        }

        Bounds search = GetSearchBounds();
        int count = Physics.OverlapBoxNonAlloc(search.center, search.extents, _zoneBuffer, Quaternion.identity,
            ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            TowerItem item = _zoneBuffer[i].GetComponentInParent<TowerItem>();
            if (item != null && !item.HasFallen && !_ignoredItems.Contains(item) && !_firstSeenInZone.ContainsKey(item))
            {
                _firstSeenInZone[item] = Time.time;
            }
        }

        Bounds fallBounds = search;
        fallBounds.Expand(new Vector3(fallMargin * 2f, 0f, fallMargin * 2f));
        fallBounds.min = new Vector3(fallBounds.min.x, search.min.y - fallMargin, fallBounds.min.z);

        _removeBuffer.Clear();
        _settledItems.Clear();
        bool itemFell = false;

        foreach (KeyValuePair<TowerItem, float> entry in _firstSeenInZone)
        {
            TowerItem item = entry.Key;
            if (item == null)
            {
                _removeBuffer.Add(item);
                continue;
            }

            bool settled = Time.time - entry.Value >= stackSettleTime;
            if (!fallBounds.Contains(item.transform.position))
            {
                // Left the van area: it only matters if it was already part of the tower.
                if (settled)
                {
                    item.MarkFallen();
                    itemFell = true;
                }

                _removeBuffer.Add(item);
            }
            else if (settled)
            {
                _settledItems.Add(item);
            }
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            _firstSeenInZone.Remove(_removeBuffer[i]);
        }

        PhysicalStackCount = _settledItems.Count;
        EvaluateTower();

        if (itemFell)
        {
            Topple("item fell out of the van");
        }
        else if (PhysicalStackCount >= 2 && CurrentTowerAngle > ToppleAngle)
        {
            Topple($"tower tilt {CurrentTowerAngle:0}° > {ToppleAngle:0}°");
        }
    }

    // Mass-weighted center of mass of the tower, and the angle between the vertical and the line
    // from the tower base (bottom layer center of mass) to that center of mass.
    private void EvaluateTower()
    {
        CurrentTowerAngle = 0f;
        if (_settledItems.Count == 0)
        {
            return;
        }

        float lowestY = float.MaxValue;
        float totalMass = 0f;
        Vector3 weightedSum = Vector3.zero;

        for (int i = 0; i < _settledItems.Count; i++)
        {
            if (!_settledItems[i].TryGetComponent(out Rigidbody rb))
            {
                continue;
            }

            Vector3 com = rb.worldCenterOfMass;
            weightedSum += com * rb.mass;
            totalMass += rb.mass;
            lowestY = Mathf.Min(lowestY, com.y);
        }

        if (totalMass <= 0f)
        {
            return;
        }

        Vector3 towerCom = weightedSum / totalMass;
        TowerCenterOfMass = towerCom;

        float baseMass = 0f;
        Vector3 baseSum = Vector3.zero;
        for (int i = 0; i < _settledItems.Count; i++)
        {
            if (!_settledItems[i].TryGetComponent(out Rigidbody rb))
            {
                continue;
            }

            Vector3 com = rb.worldCenterOfMass;
            if (com.y <= lowestY + bottomLayerHeight)
            {
                baseSum += com * rb.mass;
                baseMass += rb.mass;
            }
        }

        Vector3 towerBase = baseSum / baseMass;
        Vector3 axis = towerCom - towerBase;
        if (axis.y >= minTowerHeight)
        {
            CurrentTowerAngle = Vector3.Angle(Vector3.up, axis);
        }
    }

    private void Topple(string reason)
    {
        if (_toppled)
        {
            return;
        }

        _toppled = true;
        _toppleReason = reason;
        OnTowerToppled?.Invoke();
    }

    private void OnGUI()
    {
        if (!showDebugText || deliveryZone == null)
        {
            return;
        }

        string state = _toppled ? $"TOPPLED ({_toppleReason})" : "standing";
        GUI.Label(new Rect(10f, 10f, 520f, 24f),
            $"Tower: {PhysicalStackCount} items | tilt {CurrentTowerAngle:0}° / {ToppleAngle:0}° | {state}");
    }

    private void OnDrawGizmos()
    {
        if (deliveryZone == null)
        {
            return;
        }

        Bounds search = GetSearchBounds();
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireCube(search.center, search.size);

        if (Application.isPlaying && PhysicalStackCount > 0)
        {
            Gizmos.color = _toppled ? Color.red : Color.yellow;
            Gizmos.DrawSphere(TowerCenterOfMass, 0.15f);
        }
    }

    /// <summary>
    /// Kilitli bir eşya bir şeye çarptığında TowerItem tarafından çağrılır.
    /// </summary>
    public void NotifyItemCollision(Collider other, float impactSpeed)
    {
        if (_currentStack.Count == 0 || impactSpeed < collapseImpactSpeed || IsIgnoredCollider(other))
        {
            return;
        }

        CollapseTower();
    }

    /// <summary>
    /// Kuleyi yıkar: tüm eşyalar kuleden koparılır ve fiziği geri açılır.
    /// </summary>
    public void CollapseTower()
    {
        if (_currentStack.Count == 0)
        {
            return;
        }

        Vector3 inheritedVelocity = _baseVelocity;

        for (int i = 0; i < _currentStack.Count; i++)
        {
            TowerItem item = _currentStack[i];
            if (item == null)
            {
                continue;
            }

            item.transform.SetParent(null, true);
            item.EnablePhysics();

            if (item.TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = inheritedVelocity;
                rb.AddForce(Random.insideUnitSphere * scatterImpulse, ForceMode.Impulse);
            }
        }

        _currentStack.Clear();

        _tiltX = _tiltZ = 0f;
        _tiltVelocityX = _tiltVelocityZ = 0f;
        towerBasePoint.localRotation = Quaternion.identity;

        Debug.LogWarning("[KULE YIKILDI] Denge kaybedildi, tüm ganimetler devrildi!");
    }

    private bool IsTouchingSolid(TowerItem item)
    {
        if (item == null || Time.time - _lastAddTime < collisionGracePeriod ||
            !item.TryGetComponent(out Collider col))
        {
            return false;
        }

        Bounds b = col.bounds;
        int count = Physics.OverlapBoxNonAlloc(b.center, b.extents, _overlapBuffer, Quaternion.identity,
            ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (!IsIgnoredCollider(_overlapBuffer[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Oyuncu, silah, kulenin kendi eşyaları ve diğer TowerItem'lar çarpışma sayılmaz.</summary>
    private bool IsIgnoredCollider(Collider other)
    {
        if (other == null || other.isTrigger)
        {
            return true;
        }

        Transform t = other.transform;
        return t.IsChildOf(playerTransform.root) || other.GetComponentInParent<TowerItem>() != null;
    }

    /// <summary>
    /// Vakumla çekilen eşyayı kulenin en üstüne ekler.
    /// </summary>
    public void AddItemToTower(TowerItem newItem)
    {
        if (newItem == null || newItem.IsStacked || _currentStack.Contains(newItem))
        {
            return;
        }

        newItem.DisablePhysics();
        _currentStack.Add(newItem);
        _lastAddTime = Time.time;

        Transform itemTransform = newItem.transform;
        itemTransform.SetParent(towerBasePoint, true);
        itemTransform.localRotation = Quaternion.identity;
        itemTransform.localPosition = CalculateNextLocalPosition();
    }

    /// <summary>
    /// Yeni eşyanın yerel konumunu hesaplar. İlk eşya taban noktasına,
    /// diğerleri bir öncekinin üstüne yerleşir.
    /// </summary>
    private Vector3 CalculateNextLocalPosition()
    {
        if (_currentStack.Count <= 1)
        {
            return Vector3.zero;
        }

        TowerItem topItem = _currentStack[_currentStack.Count - 2];
        return topItem.transform.localPosition + Vector3.up * yOffsetBetweenItems;
    }

    /// <summary>
    /// Oyuncunun hızına ve fare hareketine göre kuleyi yumuşak bir şekilde eğer.
    /// Kule ne kadar yüksekse sallanma o kadar güçlü olur.
    /// </summary>
    private void ApplyWobble(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        Vector3 playerVelocity = (playerTransform.position - _lastPlayerPosition) / deltaTime;
        _lastPlayerPosition = playerTransform.position;

        Vector3 localVelocity = playerTransform.InverseTransformDirection(playerVelocity);
        float turnInput = Input.GetAxis("Mouse X");

        float stackFactor = 1f + _currentStack.Count * stackWobbleMultiplier;

        float targetZ = (-localVelocity.x * wobbleIntensity - turnInput * turnWobbleMultiplier) * stackFactor;
        float targetX = (localVelocity.z * wobbleIntensity * 0.5f) * stackFactor;

        targetZ = Mathf.Clamp(targetZ, -maxTiltAngle, maxTiltAngle);
        targetX = Mathf.Clamp(targetX, -maxTiltAngle, maxTiltAngle);

        _tiltZ = Mathf.SmoothDampAngle(_tiltZ, targetZ, ref _tiltVelocityZ, tiltSmoothTime);
        _tiltX = Mathf.SmoothDampAngle(_tiltX, targetX, ref _tiltVelocityX, tiltSmoothTime);

        towerBasePoint.localRotation = Quaternion.Euler(_tiltX, 0f, _tiltZ);
    }
}
