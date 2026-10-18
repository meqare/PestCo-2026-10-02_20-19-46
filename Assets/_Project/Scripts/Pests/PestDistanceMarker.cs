using TMPro;
using UnityEngine;

/// <summary>
/// Haşere uzaktayken başının üstünde parlak bir ok (▼) gösterir; uzaklaştıkça büyür, yaklaşınca kaybolur.
/// Böylece küçük ve koyu bir hamam böceği evin öbür ucundan da fark edilir.
/// Duvar arkasındaki haşereyi göstermez (3B yazı derinlik testine tabidir).
/// </summary>
[RequireComponent(typeof(Pest))]
[DisallowMultipleComponent]
public class PestDistanceMarker : MonoBehaviour
{
    [Header("Görünürlük")]
    [Tooltip("Bu mesafeden (m) yakında işaret görünmez.")]
    [SerializeField, Min(0f)] private float _showDistance = 4f;
    [Tooltip("Bu mesafede (m) işaret tam görünür.")]
    [SerializeField, Min(0.1f)] private float _fullDistance = 6.5f;

    [Header("Biçim")]
    [SerializeField] private string _symbol = "▼";
    [SerializeField] private Color _color = new Color(1f, 0.55f, 0.05f);
    [SerializeField, Min(0.1f)] private float _fontSize = 4f;
    [Tooltip("Haşere kökünden yukarı uzaklık (metre).")]
    [SerializeField, Min(0f)] private float _heightOffset = 0.85f;
    [Tooltip("Mesafe başına büyüme (ekranda sabit boyda görünmesi için).")]
    [SerializeField, Min(0f)] private float _scalePerMeter = 0.14f;
    [SerializeField, Min(0.05f)] private float _minScale = 0.7f;
    [SerializeField, Min(0.05f)] private float _maxScale = 3.5f;

    [Header("Hareket")]
    [SerializeField, Min(0f)] private float _bobHeight = 0.04f;
    [SerializeField, Min(0.1f)] private float _bobSpeed = 4f;

    private Pest _pest;
    private Transform _marker;
    private TextMeshPro _text;
    private Camera _camera;
    private bool _concealed;

    /// <summary>Haşereyi bulur.</summary>
    private void Awake()
    {
        _pest = GetComponent<Pest>();
    }

    /// <summary>Gizlenme olayına abone olur.</summary>
    private void OnEnable()
    {
        _pest.ConcealedChanged += OnConcealedChanged;
        _concealed = false;
        if (_marker != null) _marker.gameObject.SetActive(false);
    }

    /// <summary>Abonelikten çıkar ve işareti gizler (havuza dönünce kalmasın).</summary>
    private void OnDisable()
    {
        _pest.ConcealedChanged -= OnConcealedChanged;
        if (_marker != null) _marker.gameObject.SetActive(false);
    }

    /// <summary>Mesafeye göre işareti gösterir, büyütür, kameraya çevirir ve hafifçe zıplatır.</summary>
    private void LateUpdate()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        float distance = Vector3.Distance(_camera.transform.position, transform.position);
        bool show = _pest.IsAlive && !_concealed && distance > _showDistance;
        if (!show)
        {
            if (_marker != null && _marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
            return;
        }

        if (_marker == null) CreateMarker();
        if (!_marker.gameObject.activeSelf) _marker.gameObject.SetActive(true);

        float fade = Mathf.InverseLerp(_showDistance, _fullDistance, distance);
        Color color = _color;
        color.a = fade;
        _text.color = color;

        float bob = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        _marker.position = transform.position + Vector3.up * (_heightOffset + bob);
        _marker.rotation = _camera.transform.rotation;
        _marker.localScale = Vector3.one * Mathf.Clamp(distance * _scalePerMeter, _minScale, _maxScale);
    }

    /// <summary>Gizlenince (dolap/yuva) işaret de gizlenir.</summary>
    private void OnConcealedChanged(bool concealed)
    {
        _concealed = concealed;
    }

    /// <summary>Kalın, kontürlü 3B ok yazısını (ilk kullanımda) oluşturur.</summary>
    private void CreateMarker()
    {
        var go = new GameObject("DistanceMarker");
        go.transform.SetParent(transform, false);

        _text = go.AddComponent<TextMeshPro>();
        _text.text = _symbol;
        _text.fontSize = _fontSize;
        _text.fontStyle = FontStyles.Bold;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = _color;
        _text.rectTransform.sizeDelta = new Vector2(1f, 1f);
        _text.outlineWidth = 0.3f;
        _text.outlineColor = Color.white;
        _text.raycastTarget = false;

        _marker = go.transform;
    }
}
