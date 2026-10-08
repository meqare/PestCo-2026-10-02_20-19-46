// Tek bir iş (kontrat) için veri: nerede, ne kadar para, hangi haşere, hangi zorluk.
[System.Serializable]
public class ContractData
{
    public string contractId;       // "contract_001"
    public string title;            // "Daire 3B"
    public string district;         // "Eski Apartmanlar"
    public string address;          // "Atatürk Cad. No:12"
    public int baseReward = 100;
    public int difficulty = 1;      // 1-5
    public string pestSpecies;      // "Cockroach", "Mouse", "Unknown"
    public float requiredTime = 300f;
    public string modifier = "Normal"; // "Normal", "NightShift", "OwnerHome"
}
