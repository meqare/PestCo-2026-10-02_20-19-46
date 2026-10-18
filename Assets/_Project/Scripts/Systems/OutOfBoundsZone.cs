using UnityEngine;

// Sahne sınırını çevreleyen trigger collider: içinden çıkan PullableObject / GrabbableObject spawn noktasına geri taşınır.
// Zemin/duvar yerine "güvenli alan" kutusuna konur (Is Trigger açık). Çıkış yönü fark etmez.
[RequireComponent(typeof(Collider))]
public class OutOfBoundsZone : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null) return;
        if (body.TryGetComponent(out PullableObject p))
            p.Respawn();
        if (body.TryGetComponent(out GrabbableObject g))
            g.Respawn();
    }
}
