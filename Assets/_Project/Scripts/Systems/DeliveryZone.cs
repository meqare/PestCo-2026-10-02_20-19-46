using System.Collections.Generic;
using UnityEngine;

// Teslimat noktası: trigger Collider'a giren KitchenObjectProperties taşıyan nesneyi OrderManager'a teslim eder.
// Birden çok Collider'ı olan nesne tek nesne sayılır; nesne bölgeden tamamen çıkmadan tekrar değerlendirilmez.
[RequireComponent(typeof(Collider))]
public class DeliveryZone : MonoBehaviour
{
    [Tooltip("Boşsa OrderManager.Instance kullanılır.")]
    [SerializeField] private OrderManager orderManager;

    // Bölgedeki her nesnenin içeride kaç Collider'ı var.
    private readonly Dictionary<KitchenObjectProperties, int> inside = new Dictionary<KitchenObjectProperties, int>();

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning("DeliveryZone: Collider trigger değildi, trigger yapıldı.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var item = FindItem(other);
        if (item == null) return;

        inside.TryGetValue(item, out int count);
        inside[item] = count + 1;
        if (count > 0) return; // aynı nesnenin ikinci Collider'ı: yeniden teslim etme

        var manager = orderManager != null ? orderManager : OrderManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("DeliveryZone: sahnede OrderManager yok.", this);
            return;
        }
        manager.TryDeliver(item);
    }

    private void OnTriggerExit(Collider other)
    {
        var item = FindItem(other);
        if (item == null || !inside.TryGetValue(item, out int count)) return;
        if (count <= 1) inside.Remove(item);
        else inside[item] = count - 1;
    }

    // Oyuncu, zemin vb. özellik taşımayan nesneler yok sayılır.
    private static KitchenObjectProperties FindItem(Collider other)
    {
        if (other == null) return null;
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && rb.TryGetComponent(out KitchenObjectProperties onBody)) return onBody;
        return other.GetComponentInParent<KitchenObjectProperties>();
    }

    private void OnDisable() => inside.Clear();
}
