using UnityEngine;

// Yerdeki toplanabilir yemek: oyuncu değince yemek envanterine eklenir ve nesne yok olur.
// Bu nesnede Is Trigger açık bir collider olmalı (Reset ile otomatik kurulur).
public class CollectibleFood : MonoBehaviour
{
    public FoodType foodType = FoodType.NuclearSoda;

    private const string PlayerTag = "Player";

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(PlayerTag) && other.GetComponentInParent<PlayerController>() == null) return;

        if (FoodInventory.Instance == null)
        {
            Debug.LogWarning("CollectibleFood: Sahnede FoodInventory yok, yemek toplanamadı.", this);
            return;
        }

        FoodInventory.Instance.AddFood(foodType);
        Destroy(gameObject);
    }
}
