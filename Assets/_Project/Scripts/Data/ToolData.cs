using UnityEngine;

// Bir aletin sabit bilgileri (ad, fiyat, açıklama). Oyuncunun sahip olduğu aletler PlayerData.ownedTools'ta tutulur.
[CreateAssetMenu(fileName = "Tool_", menuName = "PEST CO/Tool Data")]
public class ToolData : ScriptableObject
{
    public string toolId;           // "spray", "vacuum", "trap", "racket"
    public string toolName;         // "Sprey", "Vakum", "Tuzak", "Elektrikli Raket"
    [TextArea] public string description;
    public Sprite icon;
    public int price = 0;           // 0 = bedava
    public int unlockLevel = 1;
    public bool isOwnedByDefault = false;
}
