using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Görüş tabanlı çizim bütçesi (FPS için). Sadece RENDER tarafına dokunur:
// fizik, collider, Rigidbody, AI, NavMesh ve trigger'lar aynen çalışır.
//
// Yaptıkları:
//  1) Kamerada Occlusion Culling açık kalır (bake'li OcclusionArea duvar arkasını çizmez).
//  2) Evin içindeyken gölge mesafesini kısaltır -> gölge haritasına giren (kamera görmediği) obje sayısı düşer.
//     Dışarıdayken uzak değere döner. Geçiş yumuşaktır, ani sıçrama olmaz.
//  3) İstenirse kameranın arkasında kalan, gölge düşürmesi gereksiz yerleri hiç çizdirmez (frustum zaten bunu yapar).
//
// Kamera hızlı dönünce geç açılma: Occlusion/frustum kararı her karede kamera pozisyonundan hesaplanır,
// bu script ek bir gecikme eklemez. Işık kesme (LightCuller) 0.15 sn aralıklıdır; pay (rangeMargin) bunu tolere eder.
[DefaultExecutionOrder(100)]
public class ViewCullingManager : MonoBehaviour
{
    [Header("Kamera")]
    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private bool forceOcclusionCulling = true;
    [Tooltip("Eski OnMouseDown/OnMouseOver olayları için kameraların her karede attığı ışını kapatır (projede kullanılmıyor).")]
    [SerializeField] private bool disableLegacyMouseEvents = true;

    [Header("İç mekan tespiti")]
    [Tooltip("Boş bırakılırsa sahnedeki OcclusionArea kullanılır.")]
    [SerializeField] private OcclusionArea indoorArea;
    [Tooltip("Alanın kenarlarından içeri çekilen mesafe (m): evin hemen dışı 'dışarı' sayılsın.")]
    [SerializeField, Min(0f)] private float areaShrink = 2f;

    [Header("Gölge mesafesi")]
    [SerializeField] private bool adaptShadowDistance = true;
    [SerializeField, Min(1f)] private float indoorShadowDistance = 7f;
    [SerializeField, Min(1f)] private float outdoorShadowDistance = 12f;
    [Tooltip("Geçiş hızı (m/sn).")]
    [SerializeField, Min(1f)] private float blendSpeed = 20f;
    [SerializeField, Min(0.05f)] private float checkInterval = 0.2f;

    private UniversalRenderPipelineAsset urp;
    private float originalDistance;
    private float targetDistance;
    private float nextCheck;

    private void OnEnable()
    {
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            originalDistance = urp.shadowDistance;
            targetDistance = outdoorShadowDistance;
        }
        if (indoorArea == null) indoorArea = FindFirstObjectByType<OcclusionArea>();

        // eventMask 0: SendMouseEvents her karede bu kameralardan fizik ışını atmaz (UI etkilenmez, o GraphicRaycaster kullanır).
        if (disableLegacyMouseEvents)
            foreach (var cam in Camera.allCameras) cam.eventMask = 0;
    }

    // Çıkarken asset'i eski değerine döndür (editörde asset kalıcı değişmesin).
    private void OnDisable()
    {
        if (urp != null) urp.shadowDistance = originalDistance;
    }

    private void LateUpdate()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null) return;

        if (forceOcclusionCulling && !viewCamera.useOcclusionCulling) viewCamera.useOcclusionCulling = true;
        if (!adaptShadowDistance || urp == null) return;

        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + checkInterval;
            targetDistance = IsIndoors(viewCamera.transform.position) ? indoorShadowDistance : outdoorShadowDistance;
        }
        urp.shadowDistance = Mathf.MoveTowards(urp.shadowDistance, targetDistance, blendSpeed * Time.unscaledDeltaTime);
    }

    private bool IsIndoors(Vector3 p)
    {
        if (indoorArea == null) return false;
        // OcclusionArea dönmez; dünya sınırı = pozisyon + center, boyut = size (ölçek uygulanır).
        Transform t = indoorArea.transform;
        Vector3 size = Vector3.Scale(indoorArea.size, t.lossyScale) - new Vector3(areaShrink * 2f, 0f, areaShrink * 2f);
        var b = new Bounds(t.TransformPoint(indoorArea.center), size);
        return b.Contains(p);
    }
}
