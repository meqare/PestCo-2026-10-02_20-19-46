using System.Collections.Generic;
using UnityEngine;

// Böcekleri beslemekte kullanılan yemek türleri.
public enum FoodType
{
    NuclearSoda,
    ExplosiveCandy,
    CursedCookie
}

public static class FoodTypeExtensions
{
    // Bu yemek yenen böceğin satış değerini kaç kat artırır.
    public static float ValueMultiplier(this FoodType food)
    {
        switch (food)
        {
            case FoodType.NuclearSoda: return 2f;
            case FoodType.ExplosiveCandy: return 2f;
            case FoodType.CursedCookie: return 3f;
            default: return 1f;
        }
    }

    // Yemek mağazasında bu yemeğin tanesi kaç paraya satılır.
    public static int SellPrice(this FoodType food)
    {
        switch (food)
        {
            case FoodType.NuclearSoda: return 15;
            case FoodType.ExplosiveCandy: return 20;
            case FoodType.CursedCookie: return 35;
            default: return 0;
        }
    }
}

// Oyuncunun topladığı yemekleri sayar.
public class FoodInventory : MonoBehaviour
{
    public static FoodInventory Instance { get; private set; }

    // Yemek adedi değiştiğinde tetiklenir (ekrandaki envanter paneli bunu dinler).
    public event System.Action Changed;

    private readonly Dictionary<FoodType, int> foods = new Dictionary<FoodType, int>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddFood(FoodType food)
    {
        foods.TryGetValue(food, out int count);
        foods[food] = count + 1;
        Changed?.Invoke();
        Debug.Log($"[Yemek] {food} toplandı (elinde {count + 1} adet).");
    }

    public int GetCount(FoodType food)
    {
        foods.TryGetValue(food, out int count);
        return count;
    }

    // Varsa bir adet harcar ve true döner.
    public bool TryConsume(FoodType food)
    {
        if (GetCount(food) <= 0) return false;
        foods[food]--;
        Changed?.Invoke();
        return true;
    }
}
