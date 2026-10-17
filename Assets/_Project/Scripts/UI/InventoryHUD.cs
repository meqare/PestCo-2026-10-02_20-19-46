using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Ekrandaki envanter paneli: yakalanan böcekleri (tür, durum, adet, değer çarpanı)
/// ve toplanan yemekleri gösterir. Envanterler değiştiğinde kendini yeniler.
/// </summary>
public class InventoryHUD : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Envanter yazısının basılacağı TextMeshPro.")]
    [SerializeField] private TMP_Text _text;

    [Header("Renkler")]
    [SerializeField] private Color _titleColor = new Color(1f, 0.92f, 0.4f);
    [SerializeField] private Color _horrorColor = new Color(0.8f, 0.5f, 1f);

    private readonly StringBuilder _builder = new StringBuilder();
    private BugInventory _bugInventory;
    private FoodInventory _foodInventory;

    /// <summary>Envanterlere abone olur ve ilk görünümü çizer (Instance'lar Awake'te hazır olur).</summary>
    private void Start()
    {
        _bugInventory = BugInventory.Instance;
        _foodInventory = FoodInventory.Instance;

        if (_bugInventory != null) _bugInventory.Changed += Refresh;
        if (_foodInventory != null) _foodInventory.Changed += Refresh;

        Refresh();
    }

    /// <summary>Aboneliklerden çıkar (yok olmuş panele olay gitmesin).</summary>
    private void OnDestroy()
    {
        if (_bugInventory != null) _bugInventory.Changed -= Refresh;
        if (_foodInventory != null) _foodInventory.Changed -= Refresh;
    }

    /// <summary>Envanter içeriğini baştan yazıya döker.</summary>
    private void Refresh()
    {
        if (_text == null) return;

        _builder.Clear();
        AppendBugs();
        _builder.AppendLine();
        AppendFoods();
        _text.text = _builder.ToString();
    }

    /// <summary>Yakalanan böcekleri listeler.</summary>
    private void AppendBugs()
    {
        AppendTitle("BÖCEKLER");

        var bugs = _bugInventory != null ? _bugInventory.GetCapturedBugs() : null;
        bool any = false;

        if (bugs != null)
        {
            for (int i = 0; i < bugs.Count; i++)
            {
                CapturedBugInfo info = bugs[i];
                if (info.data == null || info.count <= 0) continue;

                any = true;
                bool horror = info.stateAtCapture == BugState.Horror;
                string state = horror ? $"<color=#{ColorUtility.ToHtmlStringRGB(_horrorColor)}>Horror</color>" : "Cute";
                string fed = info.valueMultiplier > 1f ? $"  (x{info.valueMultiplier:0.#} değer)" : string.Empty;
                _builder.AppendLine($" {info.data.bugName} [{state}] x{info.count}{fed}");
            }
        }

        if (!any) _builder.AppendLine(" -");
    }

    /// <summary>Toplanan yemekleri listeler.</summary>
    private void AppendFoods()
    {
        AppendTitle("YEMEKLER");

        bool any = false;
        if (_foodInventory != null)
        {
            foreach (FoodType food in System.Enum.GetValues(typeof(FoodType)))
            {
                int count = _foodInventory.GetCount(food);
                if (count <= 0) continue;

                any = true;
                _builder.AppendLine($" {food} x{count}");
            }
        }

        if (!any) _builder.AppendLine(" -");
    }

    /// <summary>Renkli bölüm başlığı ekler.</summary>
    private void AppendTitle(string title)
    {
        _builder.AppendLine($"<b><color=#{ColorUtility.ToHtmlStringRGB(_titleColor)}>{title}</color></b>");
    }
}
