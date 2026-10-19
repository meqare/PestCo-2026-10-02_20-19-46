using UnityEngine;

/// <summary>
/// Cursed item behaviour: once the item is delivered (counted in the physical tower) a timer starts.
/// During the last seconds the visual shakes as a warning; when the timer ends the item breaks:
/// its points are banked for the round, nearby bodies get a small push and it is destroyed.
/// </summary>
[RequireComponent(typeof(TowerItem))]
public class CursedItem : MonoBehaviour
{
    [Header("Balance")]
    [Tooltip("When assigned, break delay and push force come from the config instead of the fields below.")]
    [SerializeField] private GameConfig config;

    [Header("Timing")]
    [Tooltip("Seconds between delivery and breaking (used when no GameConfig is assigned).")]
    [SerializeField] private float breakDelay = 15f;
    [Tooltip("The visual shakes during the last N seconds.")]
    [SerializeField] private float warningDuration = 3f;

    [Header("Shake")]
    [SerializeField] private float shakeAmplitude = 0.02f;
    [SerializeField] private float shakeFrequency = 60f;
    [Tooltip("Child transform that shakes (physics body is never moved). Defaults to the first child.")]
    [SerializeField] private Transform visual;

    [Header("Break")]
    [Tooltip("Impulse applied to nearby rigidbodies when the item breaks (used when no GameConfig is assigned).")]
    [SerializeField] private float pushForceFallback = 2f;
    [SerializeField] private float pushRadius = 1.8f;
    [SerializeField] private int shardCount = 8;
    [SerializeField] private float shardLifetime = 3f;

    private TowerItem _item;
    private Vector3 _visualBasePosition;
    private bool _delivered;
    private bool _broken;
    private float _timer;

    public bool IsDelivered => _delivered;
    public float TimeUntilBreak => Mathf.Max(0f, BreakDelay - _timer);

    private float BreakDelay => config != null ? config.vaseBreakDelay : breakDelay;
    private float PushForce => config != null ? config.vasePushForce : pushForceFallback;

    private void Awake()
    {
        _item = GetComponent<TowerItem>();
        if (visual == null && transform.childCount > 0)
        {
            visual = transform.GetChild(0);
        }

        if (visual != null)
        {
            _visualBasePosition = visual.localPosition;
        }
    }

    private void Update()
    {
        if (_broken)
        {
            return;
        }

        if (!_delivered)
        {
            _delivered = IsInTower();
            return;
        }

        _timer += Time.deltaTime;
        float remaining = BreakDelay - _timer;

        if (remaining <= 0f)
        {
            Break();
        }
        else if (remaining <= warningDuration)
        {
            Shake();
        }
    }

    // Delivered = the tower manager counts this item as settled in the delivery zone.
    private bool IsInTower()
    {
        WobblyTowerManager tower = WobblyTowerManager.Instance;
        if (tower == null)
        {
            return false;
        }

        for (int i = 0; i < tower.SettledItems.Count; i++)
        {
            if (tower.SettledItems[i] == _item)
            {
                return true;
            }
        }

        return false;
    }

    private void Shake()
    {
        if (visual == null)
        {
            return;
        }

        float t = Time.time * shakeFrequency;
        visual.localPosition = _visualBasePosition +
            new Vector3(Mathf.Sin(t), 0f, Mathf.Sin(t * 1.3f + 1f)) * shakeAmplitude;
    }

    private void Break()
    {
        _broken = true;
        Vector3 center = transform.position;

        WobblyTowerManager tower = WobblyTowerManager.Instance;
        // Only items delivered in the current round bank points (a vase from an earlier round does not).
        if (tower != null)
        {
            tower.NotifyCursedBreak();
            if (!tower.IsIgnored(_item))
            {
                tower.AddBankedScore(Mathf.RoundToInt(_item.ItemValue));
            }
        }

        Collider[] hits = Physics.OverlapSphere(center, pushRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Rigidbody body = hits[i].attachedRigidbody;
            if (body != null && body.gameObject != gameObject)
            {
                body.AddExplosionForce(PushForce, center, pushRadius, 0.3f, ForceMode.Impulse);
            }
        }

        SpawnShards(center);
        Destroy(gameObject);
    }

    private void SpawnShards(Vector3 center)
    {
        Material shardMaterial = null;
        if (visual != null && visual.TryGetComponent(out Renderer visualRenderer))
        {
            shardMaterial = visualRenderer.sharedMaterial;
        }

        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "VaseShard";
            shard.transform.position = center + Random.insideUnitSphere * 0.25f;
            shard.transform.rotation = Random.rotation;
            shard.transform.localScale = Vector3.one * Random.Range(0.06f, 0.14f);

            if (shardMaterial != null)
            {
                shard.GetComponent<Renderer>().sharedMaterial = shardMaterial;
            }

            Rigidbody body = shard.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.AddExplosionForce(PushForce * 0.15f, center, pushRadius, 0.5f, ForceMode.Impulse);
            Destroy(shard, shardLifetime);
        }
    }
}
