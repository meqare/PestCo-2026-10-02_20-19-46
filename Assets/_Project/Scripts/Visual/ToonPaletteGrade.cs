using UnityEngine;

// Tüm PestCo/Toon materyalleri için küresel palet ayarı: gölgelere mor/lacivert ton verir, sarılığı azaltır.
// Shader global'lerini yazar; bileşen kapanınca global'ler 0'lanır ve eski görünüm geri gelir.
[ExecuteAlways]
[DisallowMultipleComponent]
public class ToonPaletteGrade : MonoBehaviour
{
    [Header("Gölge paleti")]
    [Tooltip("Gölgedeki alanın çarpılacağı ton (mor/lacivert).")]
    [SerializeField] private Color shadowTint = new Color(0.70f, 0.62f, 1.00f);
    [SerializeField, Range(0f, 1f)] private float shadowTintAmount = 0.7f;

    [Header("Sarılık azaltma")]
    [Tooltip("Güneş tonu, lamba katkısı ve sis sıcaklığını azaltır (0 = eski, 1 = nötr/soğuk).")]
    [SerializeField, Range(0f, 1f)] private float yellowCut = 0.5f;

    [Header("Çizgi film görünümü")]
    [Tooltip("Tüm kontur kalınlıklarını bu oranda inceltir (0 = eski, 0.4 = %40 ince).")]
    [SerializeField, Range(0f, 0.8f)] private float outlineThinning = 0.4f;
    [Tooltip("Renk doygunluğu artışı (0 = eski).")]
    [SerializeField, Range(0f, 1f)] private float saturationBoost = 0.35f;
    [Tooltip("Fırça izi dokusunu azaltır: 1 = tam düz renk.")]
    [SerializeField, Range(0f, 1f)] private float flatColors = 0.5f;

    private static readonly int OutlineCutId = Shader.PropertyToID("_PC_OutlineCut");
    private static readonly int SatId = Shader.PropertyToID("_PC_SatBoost");
    private static readonly int BrushCutId = Shader.PropertyToID("_PC_BrushCut");
    private static readonly int TintId = Shader.PropertyToID("_PC_ShadowTint");
    private static readonly int TintAmtId = Shader.PropertyToID("_PC_ShadowTintAmt");
    private static readonly int CutId = Shader.PropertyToID("_PC_YellowCut");

    private void OnEnable() { Push(); }
    private void OnValidate() { if (isActiveAndEnabled) Push(); }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(TintAmtId, 0f);
        Shader.SetGlobalFloat(CutId, 0f);
        Shader.SetGlobalFloat(OutlineCutId, 0f);
        Shader.SetGlobalFloat(SatId, 0f);
        Shader.SetGlobalFloat(BrushCutId, 0f);
    }

    private void Push()
    {
        Shader.SetGlobalColor(TintId, shadowTint);
        Shader.SetGlobalFloat(TintAmtId, shadowTintAmount);
        Shader.SetGlobalFloat(CutId, yellowCut);
        Shader.SetGlobalFloat(OutlineCutId, outlineThinning);
        Shader.SetGlobalFloat(SatId, saturationBoost);
        Shader.SetGlobalFloat(BrushCutId, flatColors);
    }
}
