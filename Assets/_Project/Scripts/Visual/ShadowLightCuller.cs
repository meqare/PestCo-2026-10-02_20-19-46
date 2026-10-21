using System.Collections.Generic;
using UnityEngine;

// Performans: gölge atan nokta/spot ışıkların gölgesini yalnızca kameraya yakın olanlarda açık tutar.
// Nokta ışığı gölgesi 6 yüz render eder; uzaktaki odaların lamba gölgeleri görünmediği halde her kare çizilir.
// Yönlü ışık ve kameranın altındaki ışıklar (el feneri) hiç ellenmez. Bileşen kapanınca orijinal gölge ayarı geri gelir.
[DisallowMultipleComponent]
public class ShadowLightCuller : MonoBehaviour
{
    [Tooltip("Aynı anda gölge atabilecek en fazla nokta/spot ışık sayısı.")]
    [SerializeField, Range(0, 8)] private int maxShadowLights = 2;
    [Tooltip("Bu mesafeden (ışık menzili dahil değil) uzaktaki ışıkların gölgesi kapanır (m).")]
    [SerializeField] private float shadowDistance = 9f;
    [Tooltip("Kaç saniyede bir yeniden seçilir.")]
    [SerializeField] private float interval = 0.25f;

    private struct Entry { public Light light; public LightShadows original; }
    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<int> order = new List<int>();
    private float nextTime;
    private Camera cam;

    private void OnEnable()
    {
        cam = Camera.main;
        entries.Clear();
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || l.shadows == LightShadows.None) continue;
            if (cam != null && l.transform.IsChildOf(cam.transform)) continue; // el feneri vb. oyuncuya bağlı ışık
            entries.Add(new Entry { light = l, original = l.shadows });
        }
        nextTime = 0f;
    }

    private void OnDisable()
    {
        foreach (var e in entries) if (e.light != null) e.light.shadows = e.original;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextTime) return;
        nextTime = Time.unscaledTime + interval;
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Vector3 p = cam.transform.position;
        order.Clear();
        for (int i = 0; i < entries.Count; i++) if (entries[i].light != null) order.Add(i);
        order.Sort((a, b) => Sq(entries[a].light, p).CompareTo(Sq(entries[b].light, p)));

        int on = 0;
        foreach (int i in order)
        {
            var e = entries[i];
            bool near = on < maxShadowLights && e.light.isActiveAndEnabled
                        && Vector3.Distance(e.light.transform.position, p) - e.light.range < shadowDistance;
            if (near) on++;
            var want = near ? e.original : LightShadows.None;
            if (e.light.shadows != want) e.light.shadows = want;
        }
    }

    private static float Sq(Light l, Vector3 p) => (l.transform.position - p).sqrMagnitude;
}
