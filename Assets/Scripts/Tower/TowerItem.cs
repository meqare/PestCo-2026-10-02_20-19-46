using UnityEngine;

/// <summary>
/// Vakum ile çekilebilen ve kuleye eklenebilen taşınabilir eşyalar için temel script.
/// Rigidbody ve Collider gerektirir.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class TowerItem : MonoBehaviour
{
    [Header("Eşya Ayarları")]
    [Tooltip("Eşyanın antika değeri. Teslimatta puana dönüşür.")]
    [SerializeField] private float itemValue = 100f;

    [Tooltip("Optional item type data. When set, price and mass come from it.")]
    [SerializeField] private ItemData itemData;

    [Header("Durum")]
    [Tooltip("Eşya kuleye eklendi mi?")]
    [SerializeField] private bool isStacked = false;

    private Rigidbody _rigidbody;

    /// <summary>Eşyanın antika değeri.</summary>
    public float ItemValue => itemData != null ? itemData.price : itemValue;

    /// <summary>Item type data (null for legacy items without one).</summary>
    public ItemData Data => itemData;

    /// <summary>Eşya şu an kulenin bir parçası mı?</summary>
    public bool IsStacked => isStacked;

    /// <summary>Set when the item fell out of the van after being part of the tower.</summary>
    public bool HasFallen { get; private set; }

    public void MarkFallen()
    {
        HasFallen = true;
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (itemData != null)
        {
            _rigidbody.mass = itemData.mass;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isStacked || WobblyTowerManager.Instance == null)
        {
            return;
        }

        WobblyTowerManager.Instance.NotifyItemCollision(collision.collider, collision.relativeVelocity.magnitude);
    }

    /// <summary>
    /// Eşyayı kuleye eklerken çağrılır. Yerçekimini kapatır ve
    /// Rigidbody'yi kinematik yapar; böylece hiyerarşiye sorunsuz katılır.
    /// </summary>
    public void DisablePhysics()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = true;
        isStacked = true;
    }

    /// <summary>
    /// Kule çöktüğünde veya eşya kuleden ayrıldığında fiziği geri açar.
    /// </summary>
    public void EnablePhysics()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        isStacked = false;
    }
}
