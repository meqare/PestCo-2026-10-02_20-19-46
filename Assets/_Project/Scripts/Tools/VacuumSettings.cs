using UnityEngine;

public enum VacuumMouseButton { Left, Right, Middle }

// Fizik vakum silahının tüm ayarları. VacuumGun ve PullableObject buradan okur; kodda sabit değer yok.
[CreateAssetMenu(fileName = "VacuumSettings", menuName = "PEST CO/Vacuum Settings")]
public class VacuumSettings : ScriptableObject
{
    [Header("Girdi")]
    [Tooltip("Basılı tutunca nesneleri çeker. Eski böcek vakumuyla aynı tuş (sağ tık).")]
    public VacuumMouseButton pullButton = VacuumMouseButton.Right;
    [Tooltip("Tutulan nesneyi fırlatır (tek tıkta bir nesne).")]
    public VacuumMouseButton throwButton = VacuumMouseButton.Left;
    [Tooltip("Tutulan tüm nesneleri bırakır.")]
    public UnityEngine.InputSystem.Key dropKey = UnityEngine.InputSystem.Key.G;

    [Header("Koni ve Çekme")]
    [Min(0.5f)] public float maxDistance = 8f;
    [Range(5f, 170f)] public float coneAngle = 50f;
    [Tooltip("Temel çekme kuvveti (Newton).")]
    [Min(0f)] public float pullForce = 60f;
    [Tooltip("X: mesafe / maxDistance (0 yakın, 1 uzak). Y: kuvvet çarpanı.")]
    public AnimationCurve distanceFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0.1f);
    [Tooltip("0: ivme kütleden bağımsız. 1: ağır nesne (F/m) kadar yavaş gelir.")]
    [Range(0f, 1f)] public float massInfluence = 0.8f;
    [Min(0.1f)] public float maxPullSpeed = 12f;
    [Tooltip("Çekilmeyen nesne bu süre sonra normal fiziğe (yerçekimi, sürtünme) döner.")]
    [Min(0.02f)] public float pullGraceTime = 0.15f;
    [Tooltip("Çekim sırasında yerçekimi kapansın mı? (Açıksa nesne havaya yükselir.)")]
    public bool disableGravityWhilePulled = false;
    [Tooltip("Açıkken çekme ve tutma sadece yatay olur: nesne yerde sürünerek oyuncuyu takip eder, yükselmez.")]
    public bool horizontalOnly = true;
    [Tooltip("Silahla nesne arasına bu katmanlar girerse çekmez (duvar vb.).")]
    public LayerMask obstacleLayers = 1;
    [Tooltip("Pullable katman(lar)ı. Sadece bunlarla etkileşir.")]
    public LayerMask pullableLayers;
    [Min(8)] public int queryBufferSize = 64;

    [Header("Tutma")]
    [Min(1)] public int maxHeldObjects = 3;
    [Tooltip("Kameraya göre tutma noktası (sağ, yukarı, ileri).")]
    public Vector3 holdOffset = new Vector3(0.35f, -0.15f, 2.2f);
    [Tooltip("Birden çok nesne varsa yan yana dizilme aralığı.")]
    [Min(0f)] public float holdSpacing = 0.8f;
    [Tooltip("Nesne bu mesafeye gelince tutulmuş sayılır.")]
    [Min(0.05f)] public float holdSnapDistance = 1.2f;
    [Tooltip("Tutma noktasına çeken yay sertliği (ivme, kütleden bağımsız).")]
    [Min(0f)] public float holdSpring = 60f;
    [Tooltip("Tutarken hızı sönümleyen katsayı.")]
    [Min(0f)] public float holdDamping = 9f;
    [Min(0f)] public float heldAngularDrag = 4f;
    [Tooltip("Tutarken hız sınırı.")]
    [Min(0.1f)] public float maxHoldSpeed = 10f;

    [Header("Fırlatma")]
    [Tooltip("Impulse kuvveti; ağır nesne daha yavaş uçar.")]
    [Min(0f)] public float throwForce = 18f;

    [Header("Sınır (Kaybolmama)")]
    [Tooltip("Nesne bu Y'nin altına düşerse spawn noktasına döner.")]
    public float killY = -20f;

    [Header("Çizgi (LineRenderer)")]
    public Material beamMaterial;
    [Range(4, 40)] public int beamPoints = 16;
    [Min(0.001f)] public float beamWidth = 0.06f;
    public Gradient beamColor = new Gradient();
    [Min(0f)] public float beamWaveAmplitude = 0.12f;
    [Min(0f)] public float beamWaveFrequency = 3f;
    [Min(0f)] public float beamWaveSpeed = 8f;
    [Min(1)] public int maxBeams = 8;

    [Header("Squash & Stretch (görsel child)")]
    [Tooltip("Max hızda uzama miktarı (0.5 = %50).")]
    [Min(0f)] public float stretchAmount = 0.5f;
    [Min(0.1f)] public float speedForMaxStretch = 10f;
    [Min(0.1f)] public float squashLerpSpeed = 14f;

    [Header("Debug")]
    [Tooltip("Gizmo koni ve çekme vektörleri (Scene görünümü).")]
    public bool debugDraw = true;
    public Color debugConeColor = new Color(0.2f, 0.9f, 1f, 0.9f);
    public Color debugPullColor = Color.yellow;
    public Color debugHeldColor = Color.green;

    public void Reset()
    {
        // Yeni asset'in varsayılan gradyanı: açık mavi -> beyaz.
        beamColor = new Gradient();
        beamColor.SetKeys(
            new[] { new GradientColorKey(new Color(0.55f, 0.85f, 1f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 1f) });
    }
}
