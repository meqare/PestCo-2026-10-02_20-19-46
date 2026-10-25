using System;
using System.Collections.Generic;
using UnityEngine;

// Fanustaki yaratıkları besleme kuralları ve yem etkileri (tek veri kaynağı: aşağıdaki tablo).
// Arayüz (CageInspectPanel) yalnızca TryFeed ile istek gönderir; ileride fiziksel yem atma animasyonu da aynı
// çağrıyı kullanabilir. Yem stoğu mevcut FoodInventory'den düşer; BugData asset'lerine dokunulmaz.
// Not: FoodType.ValueMultiplier() (2/2/3) eski kamyon besleme hattına (TruckZone/BugInventory) aittir ve bu
// bileşenden bağımsızdır; fanus beslemesi yalnızca bu tabloyu kullanır.
public class CreatureFeeder : MonoBehaviour
{
    public enum FeedResult
    {
        Success,
        SuccessAtSizeLimit, // beslendi, ama mantıksal büyüme üst sınıra dayandı
        NoFood,
        Busy,               // yaratık hâlâ büyüyor
        NotInCage,
        Blocked,            // fanus kritik geri sayımda: beslenemez, yem harcanmaz
        Invalid
    }

    [Serializable]
    public class FoodEntry
    {
        public FoodType food;
        [Tooltip("Arayüzde görünen ad.")]
        public string label;
        [Tooltip("Boyutun kaç katına çıkacağı (1.10 = %10 büyür; her besleme önceki boyutla çarpılır).")]
        [Min(1f)] public float growthMultiplier = 1f;
        [Tooltip("Satış çarpanının kaç katına çıkacağı (1.25 = x1.25).")]
        [Min(1f)] public float valueMultiplier = 1f;
    }

    [Header("Yemler")]
    [SerializeField] private List<FoodEntry> foods = new List<FoodEntry>
    {
        new FoodEntry { food = FoodType.NuclearSoda, label = "Soda", growthMultiplier = 1.10f, valueMultiplier = 1.25f },
        new FoodEntry { food = FoodType.ExplosiveCandy, label = "Candy", growthMultiplier = 1.20f, valueMultiplier = 1.50f },
        new FoodEntry { food = FoodType.CursedCookie, label = "Cookie", growthMultiplier = 1.30f, valueMultiplier = 1.75f }
    };

    [Header("Büyüme")]
    [Tooltip("Bir yaratığın MANTIKSAL büyümesinin üst sınırı. Fanus içinde gösterilen boyut ayrıca BugCage'de sınırlanır.")]
    [SerializeField, Min(1f)] private float maxGrowth = 3f;
    [Tooltip("Büyüme animasyonunun süresi (sn).")]
    [SerializeField, Min(0.05f)] private float growDuration = 0.8f;

    // Besleme başarıyla yapıldığında tetiklenir (ileride ses/efekt dinleyebilir).
    public event Action<CageableBug, FoodType> Fed;

    public IReadOnlyList<FoodEntry> Foods => foods;

    // Bu yemin mevcut stoğu (FoodInventory yoksa 0).
    public int GetStock(FoodType food)
    {
        FoodInventory inventory = FoodInventory.Instance;
        return inventory != null ? inventory.GetCount(food) : 0;
    }

    // Yaratığın mantıksal büyümesinin üst sınırı (fanusa sığma sınırı değildir; o BugCage'de, gösterim içindir).
    public float MaxGrowth => maxGrowth;

    // Seçilen yaratığa yem verir. Tek çağrıda en fazla bir yem tüketir; kurallar sağlanmazsa hiçbir şey değişmez.
    public FeedResult TryFeed(BugCage cage, CageableBug bug, FoodType food)
    {
        FoodEntry entry = Find(food);
        if (entry == null || cage == null || bug == null) return FeedResult.Invalid;
        if (!cage.Contains(bug) || !bug.gameObject.activeInHierarchy) return FeedResult.NotInCage;
        CageCriticalController critical = cage.GetComponent<CageCriticalController>();
        if (critical != null && critical.IsFeedingBlocked) return FeedResult.Blocked;
        if (bug.IsGrowing) return FeedResult.Busy;

        FoodInventory inventory = FoodInventory.Instance;
        if (inventory == null || !inventory.TryConsume(food)) return FeedResult.NoFood;

        float wanted = bug.GrowthMultiplier * entry.growthMultiplier;
        float target = Mathf.Min(wanted, Mathf.Max(bug.GrowthMultiplier, maxGrowth));

        bug.ApplyFeeding(entry.valueMultiplier, target, growDuration);
        Fed?.Invoke(bug, food);

        return wanted > maxGrowth + 0.0001f ? FeedResult.SuccessAtSizeLimit : FeedResult.Success;
    }

    private FoodEntry Find(FoodType food)
    {
        for (int i = 0; i < foods.Count; i++)
            if (foods[i] != null && foods[i].food == food) return foods[i];
        return null;
    }
}
