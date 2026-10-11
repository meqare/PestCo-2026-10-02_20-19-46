using UnityEngine;
using UnityEngine.Rendering.Universal;

// ViewModel kamerası: silah modelleri dünya kamerasından ayrı, "Weapon" layer'ında ve ayrı bir
// Overlay kamerada çizilir. Böylece silah derinlik olarak duvarla hiç yarışmaz, duvarın içine giremez.
// Ana kameranın Culling Mask'inden Weapon çıkarılır, silah kamerası sadece Weapon'ı çizer.
// Ayarlar Inspector'dan değiştirilebilir. Atış / nişan hesabı bu scriptten etkilenmez (hepsi ana kameradan).
[DefaultExecutionOrder(-50)]
public class WeaponCameraSync : MonoBehaviour
{
    [Header("Kameralar")]
    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("Ana kameranın altındaki Overlay silah kamerası.")]
    [SerializeField] private Camera weaponCamera;

    [Header("Silah Layer'ı")]
    [Tooltip("Silah modellerinin ait olacağı layer adı.")]
    [SerializeField] private string weaponLayerName = "Weapon";
    [Tooltip("Silah renderer'ları bu objenin altında aranır (ToolSocket). Boşsa kendi objesi.")]
    [SerializeField] private Transform weaponRoot;

    [Header("Silah Kamerası")]
    [Tooltip("Silahın görüş açısı; ana kameradan bağımsızdır (55-70 arası iyi görünür).")]
    [SerializeField, Range(40f, 90f)] private float weaponFov = 60f;
    [SerializeField, Min(0.001f)] private float nearClip = 0.01f;
    [SerializeField, Min(1f)] private float farClip = 15f;

    [Header("Gölge")]
    [Tooltip("Kapalıysa silah modeli gölge düşürmez (ucuz ve temiz görünür).")]
    [SerializeField] private bool weaponCastsShadows = false;

    private int weaponLayer = -1;

    public float WeaponFov
    {
        get => weaponFov;
        set { weaponFov = Mathf.Clamp(value, 40f, 90f); ApplyCameraSettings(); }
    }

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (weaponRoot == null) weaponRoot = transform;

        weaponLayer = LayerMask.NameToLayer(weaponLayerName);
        if (weaponLayer < 0)
        {
            Debug.LogWarning("WeaponCameraSync: '" + weaponLayerName + "' layer'ı yok, Tags & Layers'a ekle.", this);
            enabled = false;
            return;
        }
        if (mainCamera == null || weaponCamera == null)
        {
            Debug.LogWarning("WeaponCameraSync: kamera referansı eksik.", this);
            enabled = false;
            return;
        }

        SetupCameras();
        RefreshLayers();
    }

    // Ana kameradan Weapon'ı çıkarır, silah kamerasını Overlay yapıp ana kameranın stack'ine ekler.
    private void SetupCameras()
    {
        mainCamera.cullingMask &= ~(1 << weaponLayer);

        weaponCamera.cullingMask = 1 << weaponLayer;
        weaponCamera.clearFlags = CameraClearFlags.Depth;
        weaponCamera.tag = "Untagged"; // Camera.main bu kamera olmasın
        ApplyCameraSettings();

        var overlayData = weaponCamera.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = true;

        var baseData = mainCamera.GetUniversalAdditionalCameraData();
        if (!baseData.cameraStack.Contains(weaponCamera))
            baseData.cameraStack.Add(weaponCamera);
    }

    private void ApplyCameraSettings()
    {
        if (weaponCamera == null) return;
        weaponCamera.fieldOfView = weaponFov;
        weaponCamera.nearClipPlane = nearClip;
        weaponCamera.farClipPlane = farClip;
    }

    // Silah modellerinin mesh renderer'larını (pasif olanlar dahil) Weapon layer'ına alır.
    // Partikül efektleri (sprey, vakum) dünyada kalır; onlar dünya kamerasından çizilmeli.
    public void RefreshLayers()
    {
        if (weaponLayer < 0) return;
        var renderers = weaponRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            r.gameObject.layer = weaponLayer;
            if (!weaponCastsShadows)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

#if UNITY_EDITOR
    // Inspector'da FOV slider'ı oynatılınca oyun çalışırken anında yansır.
    private void OnValidate()
    {
        if (Application.isPlaying) ApplyCameraSettings();
    }
#endif
}
