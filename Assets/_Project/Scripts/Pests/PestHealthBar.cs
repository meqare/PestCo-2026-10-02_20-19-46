using UnityEngine;

/// <summary>
/// Haşerenin başının üstünde, kameraya dönük küçük bir can barı gösterir.
/// Yalnızca can tam dolu değilken (hasar aldı ya da vakum zayıflattı) görünür; gizlenince ya da ölünce kaybolur.
/// Bar, çalışma anında iki quad'dan (arka plan + dolgu) kurulur; paylaşılan malzemeler kullanılır.
/// </summary>
[RequireComponent(typeof(Pest))]
[DisallowMultipleComponent]
public class PestHealthBar : MonoBehaviour
{
    [Header("Boyut ve Konum")]
    [SerializeField, Min(0.05f)] private float _width = 0.36f;
    [SerializeField, Min(0.01f)] private float _height = 0.055f;
    [Tooltip("Haşere kökünden yukarı uzaklık (metre).")]
    [SerializeField, Min(0f)] private float _heightOffset = 0.42f;

    [Header("Renkler")]
    [SerializeField] private Color _backgroundColor = new Color(0.08f, 0.05f, 0.05f, 1f);
    [SerializeField] private Color _fullColor = new Color(0.25f, 0.9f, 0.3f);
    [SerializeField] private Color _midColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color _lowColor = new Color(0.95f, 0.2f, 0.15f);

    private static Material s_backgroundMaterial;
    private static Material s_fillMaterial;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Pest _pest;
    private Transform _root;
    private Transform _fill;
    private Renderer _fillRenderer;
    private MaterialPropertyBlock _block;
    private Camera _camera;
    private float _fraction = 1f;
    private bool _concealed;

    /// <summary>Haşereyi bulur.</summary>
    private void Awake()
    {
        _pest = GetComponent<Pest>();
    }

    /// <summary>Can ve gizlenme olaylarına abone olur.</summary>
    private void OnEnable()
    {
        _pest.HealthChanged += OnHealthChanged;
        _pest.ConcealedChanged += OnConcealedChanged;
        _fraction = 1f;
        _concealed = false;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    /// <summary>Abonelikten çıkar ve barı gizler (havuza dönünce kalmasın).</summary>
    private void OnDisable()
    {
        _pest.HealthChanged -= OnHealthChanged;
        _pest.ConcealedChanged -= OnConcealedChanged;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    /// <summary>Barı kameraya çevirir; ölü/gizli/tam canlı haşerede gizler.</summary>
    private void LateUpdate()
    {
        if (_root == null) return;

        bool show = _pest.IsAlive && !_concealed && _fraction < 0.999f;
        if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
        if (!show) return;

        if (_camera == null) _camera = Camera.main;
        if (_camera != null) _root.rotation = _camera.transform.rotation;
        // Haşere dönse de bar hep başının üstünde, dünya yukarı ekseninde durur.
        _root.position = transform.position + Vector3.up * _heightOffset;
    }

    /// <summary>Can değişti: dolgu genişliğini ve rengini günceller (ilk hasarda barı oluşturur).</summary>
    private void OnHealthChanged(float fraction)
    {
        _fraction = Mathf.Clamp01(fraction);
        if (_fraction >= 0.999f && _root == null) return;

        EnsureBar();
        float width = _width * _fraction;
        _fill.localScale = new Vector3(Mathf.Max(0.0001f, width), _height * 0.7f, 1f);
        _fill.localPosition = new Vector3(-_width * 0.5f + width * 0.5f, 0f, -0.004f);

        Color color = _fraction > 0.5f
            ? Color.Lerp(_midColor, _fullColor, (_fraction - 0.5f) * 2f)
            : Color.Lerp(_lowColor, _midColor, _fraction * 2f);
        _block.SetColor(BaseColorId, color);
        _fillRenderer.SetPropertyBlock(_block);
    }

    /// <summary>Gizlenince (dolap/yuva) bar da gizlenir.</summary>
    private void OnConcealedChanged(bool concealed)
    {
        _concealed = concealed;
    }

    /// <summary>Bar nesnelerini (ilk kullanımda) oluşturur.</summary>
    private void EnsureBar()
    {
        if (_root != null) return;

        _block = new MaterialPropertyBlock();

        _root = new GameObject("HealthBar").transform;
        _root.SetParent(transform, false);

        Transform background = CreateQuad("Background", GetBackgroundMaterial());
        background.localScale = new Vector3(_width + 0.02f, _height, 1f);

        _fill = CreateQuad("Fill", GetFillMaterial());
        _fillRenderer = _fill.GetComponent<Renderer>();
    }

    /// <summary>Çarpıştırıcısız, gölgesiz bir quad üretir.</summary>
    private Transform CreateQuad(string quadName, Material material)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = quadName;
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(_root, false);

        var quadRenderer = quad.GetComponent<Renderer>();
        quadRenderer.sharedMaterial = material;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        return quad.transform;
    }

    /// <summary>Tüm barların paylaştığı koyu arka plan malzemesi.</summary>
    private Material GetBackgroundMaterial()
    {
        if (s_backgroundMaterial == null)
            s_backgroundMaterial = CreateUnlit("PestHealthBar_BG", _backgroundColor);
        return s_backgroundMaterial;
    }

    /// <summary>Tüm barların paylaştığı dolgu malzemesi (renk property block ile verilir).</summary>
    private Material GetFillMaterial()
    {
        if (s_fillMaterial == null)
            s_fillMaterial = CreateUnlit("PestHealthBar_Fill", Color.white);
        return s_fillMaterial;
    }

    private static Material CreateUnlit(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = materialName };
        material.SetColor(BaseColorId, color);
        return material;
    }
}
