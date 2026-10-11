using UnityEngine;

// Alet listesini (ToolData) tutar; hangi aletin açık olduğunu söyler ve alet satın almayı yapar.
public class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }

    [SerializeField] private ToolData[] allTools;

    public ToolData[] AllTools => allTools;

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

    // Id'ye göre alet verisini bulur; yoksa null.
    public ToolData GetTool(string toolId)
    {
        if (allTools == null) return null;
        for (int i = 0; i < allTools.Length; i++)
            if (allTools[i] != null && allTools[i].toolId == toolId) return allTools[i];
        return null;
    }

    // Alet varsayılan olarak açıksa veya satın alınmışsa true.
    public bool IsToolOwned(string toolId)
    {
        ToolData tool = GetTool(toolId);
        if (tool != null && tool.isOwnedByDefault) return true;

        PlayerData data = GameManager.Instance != null ? GameManager.Instance.Data : null;
        return data != null && data.ownedTools != null && data.ownedTools.Contains(toolId);
    }

    // Aleti satın alır: parayı düşer, ownedTools'a ekler, kaydeder. Başarılıysa true.
    public bool PurchaseTool(string toolId)
    {
        ToolData tool = GetTool(toolId);
        if (tool == null)
        {
            Debug.LogError($"ToolManager: '{toolId}' id'li alet listede yok.", this);
            return false;
        }
        if (GameManager.Instance == null)
        {
            Debug.LogError("ToolManager: Sahnede GameManager yok, satın alınamadı.", this);
            return false;
        }
        if (IsToolOwned(toolId))
        {
            Debug.Log($"[TOOL] Zaten sahipsin: {tool.toolName}");
            return false;
        }
        if (tool.price > 0 && !GameManager.Instance.SpendMoney(tool.price))
        {
            Debug.Log($"[TOOL] Para yetersiz: {tool.toolName} ({tool.price}$)");
            return false;
        }

        PlayerData data = GameManager.Instance.Data;
        if (data.ownedTools == null) data.ownedTools = new System.Collections.Generic.List<string>();
        data.ownedTools.Add(toolId);
        GameManager.Instance.SaveNow();
        Debug.Log($"[TOOL] Satın alındı: {tool.toolName}");
        return true;
    }
}
