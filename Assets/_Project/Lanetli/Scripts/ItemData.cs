using UnityEngine;

/// <summary>
/// Data for one deliverable item type (box, book, lamp). Prefabs and price lookups read from this asset.
/// </summary>
[CreateAssetMenu(fileName = "ItemData", menuName = "Lanetli/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName = "Item";

    [Tooltip("Rigidbody mass in kg.")]
    [Min(0.01f)] public float mass = 1f;

    [Tooltip("Overall size of the item in world units (x, y, z).")]
    public Vector3 size = Vector3.one;

    [Min(0)] public int price = 10;

    [Tooltip("Cursed items carry a CursedItem component that breaks them after a delay.")]
    public bool isCursed = false;

    [Tooltip("Prefab spawned for this item type.")]
    public GameObject prefab;
}
