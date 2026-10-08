using System.Collections.Generic;
using UnityEngine;

// Rastgele kontrat (iş) üretir ve oyuncunun seçtiği kontratı tutar.
public class ContractGenerator : MonoBehaviour
{
    public static ContractGenerator Instance { get; private set; }

    private static readonly string[] Titles = { "Daire 3B", "Müstakil Ev", "Depo", "Ofis" };
    private static readonly string[] Districts = { "Eski Apartmanlar", "Banliyö", "Depo Bölgesi" };
    private static readonly string[] Streets = { "Atatürk Cad.", "Gül Sok.", "İnönü Cad.", "Çınar Sok." };
    private static readonly string[] Pests = { "Cockroach", "Mouse", "Unknown" };

    private int nextId = 1;

    // Seçili kontrat.
    public ContractData CurrentContract { get; private set; }

    // Singleton'ı kurar.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Rastgele tek bir kontrat üretir.
    public ContractData GenerateRandomContract(int playerLevel = 1)
    {
        int difficulty = Random.Range(1, 4); // 1-3 (başlangıç)

        return new ContractData
        {
            contractId = $"contract_{nextId++:000}",
            title = Pick(Titles),
            district = Pick(Districts),
            address = $"{Pick(Streets)} No:{Random.Range(1, 60)}",
            difficulty = difficulty,
            baseReward = 100 * difficulty,
            pestSpecies = Pick(Pests),
            requiredTime = 300f,
            modifier = RollModifier()
        };
    }

    // 'count' kadar kontrat üretip liste döndürür.
    public List<ContractData> GenerateContracts(int count = 3)
    {
        var list = new List<ContractData>(count);
        for (int i = 0; i < count; i++)
            list.Add(GenerateRandomContract());
        return list;
    }

    // Seçili kontratı verir.
    public ContractData GetCurrentContract()
    {
        return CurrentContract;
    }

    // Kontratı seçer.
    public void SelectContract(ContractData c)
    {
        CurrentContract = c;
    }

    // Normal %70, NightShift %15, OwnerHome %15.
    private static string RollModifier()
    {
        float r = Random.value;
        if (r < 0.70f) return "Normal";
        if (r < 0.85f) return "NightShift";
        return "OwnerHome";
    }

    private static string Pick(string[] options)
    {
        return options[Random.Range(0, options.Length)];
    }
}
