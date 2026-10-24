using System.Collections.Generic;
using UnityEngine;

// Oyuncunun görmediği ışıkları kapatır, görünce tekrar açar (FPS için: Forward render'da her ışık piksel başına maliyet).
// Işık açık kalır eğer: kamera ışığın odasındaysa VEYA (menzil küresi görüş alanındaysa VE (kameraya yakınsa VEYA arada duvar yoksa)).
// Yürürken yanıp sönmesin diye: odadaki ışıklar hiç kesilmez, lamba gövdesi/oyuncu collider'ı "duvar" sayılmaz,
// kapanma gecikmeli (holdTime), açılma anında olur.
// Oda ışıkları (RoomLight) hariç: onları oda anahtarı yönetir, burada hiç kesilmezler.
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
    [Tooltip("Görünmez olan ışık bu süre (sn) boyunca açık kalır; kısa süreli engeller titreme yapmasın.")]
    [SerializeField, Min(0f)] private float holdTime = 0.5f;
    [Tooltip("Işığa bu mesafeden (m) yakın çarpmalar duvar sayılmaz (lamba gövdesi, tavan).")]
    [SerializeField, Min(0f)] private float ignoreHitNearLight = 1.2f;

    private struct Entry
    {
        public Light light;
        public RoomLight room;   // ışığın bulunduğu oda (yoksa null)
        public float hiddenSince; // -1 = görünür
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Plane[] planes = new Plane[6];
    private readonly RaycastHit[] hits = new RaycastHit[8];
    private float next;

    private void Start()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || IsIgnored(l)) continue;
            entries.Add(new Entry { light = l, room = FindRoom(l.transform.position), hiddenSince = -1f });
        }
    }

    private static RoomLight FindRoom(Vector3 p)
    {
        foreach (var r in RoomLight.All) if (r.Contains(p)) return r;
        return null;
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
        float now = Time.unscaledTime;
        next = now + checkInterval;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null) return;

        GeometryUtility.CalculateFrustumPlanes(viewCamera, planes);
        Vector3 camPos = viewCamera.transform.position;
        Transform camRoot = viewCamera.transform.root;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var l = e.light;
            if (l == null) continue;

            var owner = RoomLight.OwnerOf(l);
            if (owner != null)
            {
                // Oda ışığı: kapalı odanın ışığına dokunma. Açıksa yalnızca görüş alanı kontrolü yapılır;
                // menzil küresi görüş alanında değilse içindeki yüzeyler de görünmez, kesmek görüntüyü değiştirmez.
                if (!owner.IsOn) { if (l.enabled) l.enabled = false; continue; }
                float rm = l.range + rangeMargin;
                bool inView = GeometryUtility.TestPlanesAABB(planes, new Bounds(l.transform.position, Vector3.one * (rm * 2f)));
                SetCulled(ref e, l, inView, now);
                entries[i] = e;
                continue;
            }

            bool visible;
            if (e.room != null && e.room.Contains(camPos))
            {
                visible = true; // aynı odadayız: raycast'e gerek yok, asla kesme
            }
            else
            {
                float r = l.range + rangeMargin;
                Vector3 p = l.transform.position;
                visible = GeometryUtility.TestPlanesAABB(planes, new Bounds(p, Vector3.one * (r * 2f)));
                if (visible)
                {
                    float d = Vector3.Distance(camPos, p);
                    // Yakındaysak her zaman açık; uzaktaysak arada duvar varsa kapat.
                    visible = d <= r * 0.5f || !Blocked(camPos, p, d, camRoot);
                }
            }
            SetCulled(ref e, l, visible, now);

            if (visible)
            {
                e.hiddenSince = -1f;
                if (!l.enabled) l.enabled = true;
            }
            else
            {
                if (e.hiddenSince < 0f) e.hiddenSince = now;
                if (l.enabled && now - e.hiddenSince >= holdTime) l.enabled = false;
            }
            entries[i] = e;
        }
    }

    private void SetCulled(ref Entry e, Light l, bool visible, float now)
    {
        if (visible)
        {
            e.hiddenSince = -1f;
            if (!l.enabled) l.enabled = true;
        }
        else
        {
            if (e.hiddenSince < 0f) e.hiddenSince = now;
            if (l.enabled && now - e.hiddenSince >= holdTime) l.enabled = false;
        }
    }

    // Kamera ile ışık arasında gerçek bir engel var mı? Lamba gövdesi ve oyuncunun kendi collider'ı sayılmaz.
    private bool Blocked(Vector3 from, Vector3 to, float dist, Transform camRoot)
    {
        Vector3 dir = (to - from) / Mathf.Max(dist, 0.0001f);
        int n = Physics.RaycastNonAlloc(from, dir, hits, dist, occluderMask, QueryTriggerInteraction.Ignore);
        float limit = dist - ignoreHitNearLight;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance >= limit) continue;
            if (hits[i].collider.transform.root == camRoot) continue;
            return true;
        }
        return false;
    }

    // Çıkarken ışıkları geri aç ki editörde sahne karanlık kalmasın.
    private void OnDisable()
    {
        foreach (var e in entries) if (e.light != null) e.light.enabled = true;
    }
}
