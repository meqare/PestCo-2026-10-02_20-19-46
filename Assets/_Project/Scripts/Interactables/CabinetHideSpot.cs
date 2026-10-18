using UnityEngine;

/// <summary>
/// Haşerelerin bir dolap kapağının ya da çekmecenin içine saklanabildiği nokta (tag: HidingSpot).
/// Bu nesne mobilyanın önündeki yerdedir (NavMesh üzerinde); içeri girilecek konum ayrı bir noktadır.
/// </summary>
public class CabinetHideSpot : MonoBehaviour
{
    [Header("Bağlantılar")]
    [Tooltip("Açılıp kapanacak mobilya (CabinetDoor ya da Drawer).")]
    [SerializeField] private MonoBehaviour _furniture;
    [Tooltip("Haşerenin içine gireceği nokta (kapağın/çekmecenin içi).")]
    [SerializeField] private Transform _insidePoint;

    [Header("Giriş Hareketi")]
    [Tooltip("İçeri/dışarı sıçrarken ulaşılan zıplama yüksekliği (metre).")]
    [SerializeField, Min(0f)] private float _hopHeight = 0.25f;

    /// <summary>Açılıp kapanacak mobilya; atanmamışsa null.</summary>
    public IOpenableFurniture Furniture => _furniture as IOpenableFurniture;

    /// <summary>Haşerenin içine gireceği dünya konumu.</summary>
    public Vector3 InsidePosition => _insidePoint != null ? _insidePoint.position : transform.position;

    /// <summary>Sıçrama yüksekliği.</summary>
    public float HopHeight => _hopHeight;

    /// <summary>Bu noktayı ayıran (rezerve eden) haşere; yoksa null.</summary>
    public Pest Occupant { get; private set; }

    /// <summary>Ayıran haşere şu an gerçekten içeride (gizlenmiş) mi?</summary>
    public bool IsInside { get; set; }

    /// <summary>Kurulum aracı için bağlantıları verir.</summary>
    public void Configure(MonoBehaviour furniture, Transform insidePoint)
    {
        _furniture = furniture;
        _insidePoint = insidePoint;
    }

    /// <summary>Bu nokta verilen haşere için uygun mu (boş ya da zaten ona ayrılmış)?</summary>
    public bool IsAvailableFor(Pest pest) => Occupant == null || Occupant == pest;

    /// <summary>Noktayı haşere için ayırır; başkası ayırmışsa false döner.</summary>
    public bool TryReserve(Pest pest)
    {
        if (!IsAvailableFor(pest)) return false;

        Occupant = pest;
        return true;
    }

    /// <summary>Haşere ayrılırken noktayı boşaltır.</summary>
    public void Release(Pest pest)
    {
        if (Occupant != pest) return;

        Occupant = null;
        IsInside = false;
    }

    /// <summary>Oyuncu mobilyayı açtı: içeride saklanan haşere varsa onu ortaya çıkarıp kaçırır.</summary>
    public void ExposeOccupant()
    {
        if (Occupant != null && IsInside) Occupant.ExposeFromHiding();
    }
}
