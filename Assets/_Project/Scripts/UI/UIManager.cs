using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public struct CapturedBug
    {
        public BugData data;
        public BugState state;
    }

    [Header("UI Referansları")]
    public TMPro.TextMeshProUGUI vakumText;

    [Header("Vakum Ayarları")]
    public int maxCapacity = 5;

    private readonly List<CapturedBug> capturedBugs = new List<CapturedBug>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        UpdateVakumUI();
    }

    public void UpdateVakumUI()
    {
        if (vakumText == null) return;
        // Başlık ("VAKUM") göstergede ayrı yazıyor; burada sadece adet.
        vakumText.text = $"{capturedBugs.Count}/{maxCapacity}";
    }

    public bool AddBugToVakum(BugData data, BugState state)
    {
        if (data == null || capturedBugs.Count >= maxCapacity) return false;

        capturedBugs.Add(new CapturedBug { data = data, state = state });
        UpdateVakumUI();
        return true;
    }

    // Kafesten kaçan böcek hazneden çıkar (o türden en son eklenen silinir).
    public bool RemoveBugFromVakum(BugData data)
    {
        for (int i = capturedBugs.Count - 1; i >= 0; i--)
        {
            if (capturedBugs[i].data != data) continue;

            capturedBugs.RemoveAt(i);
            UpdateVakumUI();
            return true;
        }
        return false;
    }

    public List<CapturedBug> TakeAllBugs()
    {
        var result = new List<CapturedBug>(capturedBugs);
        capturedBugs.Clear();
        UpdateVakumUI();
        return result;
    }
}
