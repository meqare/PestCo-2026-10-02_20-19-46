using System.Collections.Generic;
using UnityEngine;

// Oyuncunun görmediği ışıkları kapatır, görünce tekrar açar (FPS için: Forward render'da her ışık piksel başına maliyet).
// Işık açık kalır eğer: menzil küresi kamera görüş alanına giriyorsa VE (kameraya yakınsa VEYA kamerayla arasında duvar yoksa).
// Sadece Light bileşenini açıp kapatır; fizik, AI, NavMesh ve trigger'lara dokunmaz.
public class LightCuller : MonoBehaviour
{
    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(0.05f)] private float checkInterval = 0.15f;
    [Tooltip("Menzile eklenen pay: ışık tam sınırda yanıp sönmesin.")]
    [SerializeField, Min(0f)] private float rangeMargin = 1.5f;
    [Tooltip("Duvar kontrolü için kullanılan katmanlar (varsayılan: hepsi).")]
    [SerializeField] private LayerMask occluderMask = ~0;
    [Tooltip("Bu isimleri içeren ışıklara dokunulmaz (el feneri vb.).")]
    [SerializeField] private string[] ignoreNames = { "Flashlight", "NearLight" };

    private readonly List<Light> lights = new List<Light>();
    private readonly Plane[] planes = new Plane[6];
    private float next;

    private void Start()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || IsIgnored(l)) continue;
            lights.Add(l);
        }
    }

    private bool IsIgnored(Light l)
    {
        if (l.GetComponentInParent<CharacterController>() != null) return true; // oyuncuya bağlı ışıklar
        foreach (var n in ignoreNames) if (l.name.Contains(n)) return true;
        return false;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + checkInterval;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null) return;

        GeometryUtility.CalculateFrustumPlanes(viewCamera, planes);
        Vector3 camPos = viewCamera.transform.position;

        foreach (var l in lights)
        {
            if (l == null) continue;
            // Anahtarla kapatılan ışığı yeniden açma.
            if (RoomLight.IsSwitchedOff(l)) { if (l.enabled) l.enabled = false; continue; }
            float r = l.range + rangeMargin;
            Vector3 p = l.transform.position;
            bool visible = GeometryUtility.TestPlanesAABB(planes, new Bounds(p, Vector3.one * (r * 2f)));
            if (visible)
            {
                float d = Vector3.Distance(camPos, p);
                // Yakındaysak her zaman açık; uzaktaysak arada duvar varsa kapat.
                visible = d <= r * 0.5f || !Physics.Linecast(camPos, p, occluderMask, QueryTriggerInteraction.Ignore);
            }
            if (l.enabled != visible) l.enabled = visible;
        }
    }

    // Çıkarken ışıkları geri aç ki editörde sahne karanlık kalmasın.
    private void OnDisable()
    {
        foreach (var l in lights) if (l != null) l.enabled = true;
    }
}
