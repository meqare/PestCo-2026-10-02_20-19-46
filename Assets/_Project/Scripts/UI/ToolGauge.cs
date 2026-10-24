using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sprey/vakum göstergesinin süsü: solda silahın piksel simgesi, isteğe bağlı yüzde yazısı.
/// Eldeki silahın göstergesi tam parlak, diğerleri soluk ve hafif küçük durur.
/// Değer azalınca dolum rengi uyarı rengine kayar (sprey bitiyor gibi).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ToolGauge : MonoBehaviour
{
    [Header("Silah")]
    [Tooltip("ToolHolder'daki sırası (0 = 1 tuşu).")]
    [SerializeField] private int _toolIndex;
    [Tooltip("Simge için ToolData.toolId (spray, vacuum, trap, racket).")]
    [SerializeField] private string _toolId = "spray";
    [Tooltip("Modelden üretilmiş simge (ToolData.icon). Boşsa çizgi film yedeği çizilir.")]
    [SerializeField] private ToolData _toolData;

    [Header("Parçalar")]
    [SerializeField] private JuicyBar _bar;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _frontFill;
    [Tooltip("Doluysa yüzde yazılır (vakum kendi sayacını kullandığı için boş bırakılır).")]
    [SerializeField] private TMP_Text _percentText;

    [Header("Renkler")]
    [SerializeField] private Color _fillColor = new Color(0.35f, 0.8f, 1f);
    [SerializeField] private Color _lowColor = new Color(1f, 0.35f, 0.3f);
    [Tooltip("Bu değerin altında dolum uyarı rengine döner. Ters göstergede (vakum) üstünde.")]
    [SerializeField, Range(0f, 1f)] private float _warnThreshold = 0.2f;
    [Tooltip("Açıksa dolu olmak kötü demektir (vakum haznesi).")]
    [SerializeField] private bool _warnWhenHigh;

    [Header("Seçili / Seçili Değil")]
    [SerializeField, Range(0f, 1f)] private float _inactiveAlpha = 0.5f;
    [SerializeField, Range(0.5f, 1f)] private float _inactiveScale = 0.92f;
    [SerializeField] private float _blendSpeed = 10f;

    private CanvasGroup _group;
    private ToolHolder _toolHolder;
    private int _lastPercent = -1;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_icon != null)
            _icon.sprite = _toolData != null && _toolData.icon != null ? _toolData.icon : CartoonIcons.Tool(_toolId);
    }

    /// <summary>Tema renklerini verir (dolum ve uyarı).</summary>
    public void SetColors(Color fill, Color low)
    {
        _fillColor = fill;
        _lowColor = low;
    }

    private void Start()
    {
        _toolHolder = FindFirstObjectByType<ToolHolder>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        float value = _bar != null ? _bar.Value : 1f;

        UpdatePercent(value);
        UpdateFillColor(value);
        UpdateSelection();
    }

    private void UpdatePercent(float value)
    {
        if (_percentText == null) return;

        int percent = Mathf.RoundToInt(value * 100f);
        if (percent == _lastPercent) return;

        _lastPercent = percent;
        _percentText.text = $"%{percent}";
    }

    /// <summary>Eşiğe yaklaşınca dolum rengi yavaşça uyarı rengine kayar; eşikte nabız gibi atar.</summary>
    private void UpdateFillColor(float value)
    {
        if (_frontFill == null) return;

        bool warn = _warnWhenHigh ? value >= 1f - _warnThreshold : value <= _warnThreshold;
        if (!warn)
        {
            _frontFill.color = _fillColor;
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
        _frontFill.color = Color.Lerp(_fillColor, _lowColor, 0.5f + 0.5f * pulse);
    }

    /// <summary>Eldeki silahın göstergesini öne çıkarır.</summary>
    private void UpdateSelection()
    {
        bool active = _toolHolder == null || _toolHolder.CurrentIndex == _toolIndex;

        float k = 1f - Mathf.Exp(-_blendSpeed * Time.unscaledDeltaTime);
        _group.alpha = Mathf.Lerp(_group.alpha, active ? 1f : _inactiveAlpha, k);

        float scale = Mathf.Lerp(transform.localScale.x, active ? 1f : _inactiveScale, k);
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
