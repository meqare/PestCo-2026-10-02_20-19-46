using UnityEngine;

// Bir odanın (alt nesneleriyle) PestCo/Toon materyallerine yükseklik gradyanı uygular.
// MaterialPropertyBlock kullanır: materyal asset'leri değişmez, bileşen kapanınca her şey eski haline döner.
// Kontur pass'i gradyandan etkilenmez.
[DisallowMultipleComponent]
public class RoomColorGradient : MonoBehaviour
{
    [Header("Genel")]
    [SerializeField, Range(0f, 1f)] private float amount = 0.6f;
    [Tooltip("Taban yüksekliği boşsa odanın en alt noktası alınır.")]
    [SerializeField] private bool autoHeight = true;
    [SerializeField] private float baseY = 0f;
    [SerializeField] private float height = 2.6f;

    [Header("Duvar / zemin / genel")]
    [SerializeField] private Color bottomTint = new Color(0.80f, 0.86f, 1.00f);
    [SerializeField] private Color topTint = new Color(1.08f, 1.05f, 0.96f);

    [Header("Dolap ve fayans (soğuk turkuaz)")]
    [Tooltip("Materyal adında bu kelimelerden biri geçenler soğuk gradyan alır.")]
    [SerializeField] private string[] coolKeywords = { "Cabinet", "Mint", "Tile", "Fayans" };
    [SerializeField, Range(0f, 1f)] private float coolAmount = 0.8f;
    [SerializeField] private Color coolBottom = new Color(0.62f, 0.82f, 0.90f);
    [SerializeField] private Color coolTop = new Color(1.05f, 1.12f, 1.10f);

    private static readonly int AmountId = Shader.PropertyToID("_GradAmount");
    private static readonly int TopId = Shader.PropertyToID("_GradTop");
    private static readonly int BottomId = Shader.PropertyToID("_GradBottom");
    private static readonly int Y0Id = Shader.PropertyToID("_GradY0");
    private static readonly int HId = Shader.PropertyToID("_GradH");

    private MaterialPropertyBlock block;

    private void OnEnable() { Apply(); }
    private void OnDisable() { Clear(); }
    private void OnValidate() { if (isActiveAndEnabled) Apply(); }

    // Odanın tüm renderer'larına gradyan blokunu yazar.
    [ContextMenu("Uygula")]
    public void Apply()
    {
        block ??= new MaterialPropertyBlock();
        var renderers = GetComponentsInChildren<Renderer>(true);

        float y0 = baseY, h = height;
        if (autoHeight && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            y0 = b.min.y;
            h = Mathf.Max(0.5f, b.size.y);
        }

        foreach (var r in renderers)
        {
            var mat = r.sharedMaterial;
            if (mat == null || mat.shader == null || mat.shader.name != "PestCo/Toon") continue;

            bool cool = IsCool(mat.name);
            r.GetPropertyBlock(block);
            block.SetFloat(AmountId, cool ? coolAmount : amount);
            block.SetColor(TopId, cool ? coolTop : topTint);
            block.SetColor(BottomId, cool ? coolBottom : bottomTint);
            block.SetFloat(Y0Id, y0);
            block.SetFloat(HId, h);
            r.SetPropertyBlock(block);
        }
    }

    // Blokları temizler (gradyan kapanır).
    [ContextMenu("Temizle")]
    public void Clear()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mat = r.sharedMaterial;
            if (mat == null || mat.shader == null || mat.shader.name != "PestCo/Toon") continue;
            r.SetPropertyBlock(null);
        }
    }

    private bool IsCool(string materialName)
    {
        foreach (var k in coolKeywords)
            if (!string.IsNullOrEmpty(k) && materialName.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
