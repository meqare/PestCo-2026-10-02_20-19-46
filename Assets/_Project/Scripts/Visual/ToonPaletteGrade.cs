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
    [Tooltip("Temiz/canlı görünüm: gölgeler gri-mavi yerine renkli kalır, leke ve köşe kararması azalır (0 = eski).")]
    [SerializeField, Range(0f, 1f)] private float cleanLook = 1f;

    [Header("Gerçeklik ve derinlik")]
    [Tooltip("Tüm toon materyalleri için en düşük gerçeklik (PBR ışık/yansıma karışımı). Materyalin kendi değeri yüksekse o kullanılır (0 = eski).")]
    [SerializeField, Range(0f, 1f)] private float realismFloor = 0.35f;
    [Tooltip("Hava perspektifi: uzak yüzeyler hafifçe soluklaşır (sis değil, derinlik ipucu). 0 = kapalı.")]
    [SerializeField, Range(0f, 1f)] private float depthCue = 0.5f;
    [Tooltip("Uzaktaki konturlar bu orana kadar incelir (0 = kapalı, 0.5 = uzakta yarı kalınlık).")]
    [SerializeField, Range(0f, 1f)] private float farOutline = 0.5f;

    [Header("Düz PBR (gerçekçi)")]
    [Tooltip("Açıkken tüm PestCo/Toon materyalleri URP Lit ışığıyla çizilir: kontur, fırça, tarama, bükülme ve palet tonları kapanır. Yukarıdaki ayarlar yalnızca kapalıyken etkilidir.")]
    [SerializeField] private bool pureLit = true;

    private static readonly int OutlineCutId = Shader.PropertyToID("_PC_OutlineCut");
    private static readonly int SatId = Shader.PropertyToID("_PC_SatBoost");
    private static readonly int BrushCutId = Shader.PropertyToID("_PC_BrushCut");
    private static readonly int TintId = Shader.PropertyToID("_PC_ShadowTint");
    private static readonly int TintAmtId = Shader.PropertyToID("_PC_ShadowTintAmt");
    private static readonly int CutId = Shader.PropertyToID("_PC_YellowCut");
    private static readonly int CleanId = Shader.PropertyToID("_PC_Clean");
    private static readonly int PureLitId = Shader.PropertyToID("_PC_PureLit");
    private const string OutlinePass = "SRPDefaultUnlit";
    private static readonly int RealismId = Shader.PropertyToID("_PC_Realism");
    private static readonly int DepthId = Shader.PropertyToID("_PC_Depth");
    private static readonly int OutlineFarId = Shader.PropertyToID("_PC_OutlineFar");

    private void OnEnable() { Push(); }
    private void OnValidate() { if (isActiveAndEnabled) Push(); }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(TintAmtId, 0f);
        Shader.SetGlobalFloat(CutId, 0f);
        Shader.SetGlobalFloat(OutlineCutId, 0f);
        Shader.SetGlobalFloat(SatId, 0f);
        Shader.SetGlobalFloat(BrushCutId, 0f);
        Shader.SetGlobalFloat(CleanId, 0f);
        Shader.SetGlobalFloat(RealismId, 0f);
        Shader.SetGlobalFloat(DepthId, 0f);
        Shader.SetGlobalFloat(OutlineFarId, 0f);
        Shader.SetGlobalFloat(PureLitId, 0f);
        SetOutlinePasses(true);
    }

    // Kontur pass'i kapatılınca çizim çağrısı hiç yapılmaz (shader'da gizlemekten ucuz).
    private static void SetOutlinePasses(bool enabled)
    {
        var toon = Shader.Find("PestCo/Toon");
        if (toon == null) return;
        foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            if (m.shader == toon && m.GetShaderPassEnabled(OutlinePass) != enabled)
                m.SetShaderPassEnabled(OutlinePass, enabled);
    }

    private void Push()
    {
        Shader.SetGlobalColor(TintId, shadowTint);
        Shader.SetGlobalFloat(TintAmtId, shadowTintAmount);
        Shader.SetGlobalFloat(CutId, yellowCut);
        Shader.SetGlobalFloat(OutlineCutId, outlineThinning);
        Shader.SetGlobalFloat(SatId, saturationBoost);
        Shader.SetGlobalFloat(BrushCutId, flatColors);
        Shader.SetGlobalFloat(CleanId, cleanLook);
        Shader.SetGlobalFloat(RealismId, realismFloor);
        Shader.SetGlobalFloat(DepthId, depthCue);
        Shader.SetGlobalFloat(OutlineFarId, farOutline);
        Shader.SetGlobalFloat(PureLitId, pureLit ? 1f : 0f);
        SetOutlinePasses(!pureLit);
    }
}
