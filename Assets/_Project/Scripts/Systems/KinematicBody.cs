using UnityEngine;

// FPS: kodla hareket ettirilen (transform ile) collider'lar kinematik Rigidbody taşımalı.
// Rigidbody'siz collider fizik motorunda "statik" sayılır; her hareketinde 1000+ statik collider'lı
// sahne ağacı yeniden düzenlenir ve kare yüzlerce ms takılabilir. Kinematik gövde bunu önler;
// yerçekimi/çarpışma tepkisi yoktur, nesne eskisi gibi transform ile hareket eder.
public static class KinematicBody
{
    public static void Ensure(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponentInParent<Rigidbody>() != null) return;

        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    public static void Ensure(Component component)
    {
        if (component != null) Ensure(component.gameObject);
    }
}
