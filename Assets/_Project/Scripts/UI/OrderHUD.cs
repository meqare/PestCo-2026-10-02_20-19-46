using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sipariş kartı: aktif siparişin adı/açıklaması, gerekli değerler, durum, kazanılan para, oyuncunun parası
// ve son teslimat mesajı. Mevcut Canvas'ın altındaki bir GameObject'e eklenir; görselini kendisi kurar.
[RequireComponent(typeof(RectTransform))]
public class OrderHUD : MonoBehaviour
{
    [Tooltip("Boşsa OrderManager.Instance kullanılır.")]
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private Vector2 cardSize = new Vector2(360f, 210f);
    [Tooltip("Kartın ekranın sol kenarından ve üstünden uzaklığı.")]
    [SerializeField] private Vector2 cardOffset = new Vector2(20f, -110f);
    [Min(0.5f)] [SerializeField] private float messageDuration = 3f;

    [Header("Renkler (retro çizgi film)")]
    [SerializeField] private Color inkColor = new Color(0.16f, 0.09f, 0.10f, 1f);
    [SerializeField] private Color paperColor = new Color(1f, 0.95f, 0.80f, 0.96f);
    [SerializeField] private Color successColor = new Color(0.10f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color failColor = new Color(0.80f, 0.15f, 0.15f, 1f);

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI messageText;
    private float messageUntil;
    private float lastMoney = float.NaN;
    private bool lastCompleted;
    private bool subscribed;

    private void Awake()
    {
        BuildCard();
    }

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        var m = Manager();
        if (subscribed && m != null) m.DeliveryResult -= OnDeliveryResult;
        subscribed = false;
    }

    private void Update()
    {
        if (!subscribed) TrySubscribe(); // OrderManager bu bileşenden sonra uyanmış olabilir

        var m = Manager();
        float money = GameManager.Instance != null ? GameManager.Instance.Data.money : 0f;
        bool completed = m != null && m.IsCompleted;
        if (!Mathf.Approximately(money, lastMoney) || completed != lastCompleted) Refresh();

        if (messageText.enabled && Time.unscaledTime > messageUntil) messageText.enabled = false;
    }

    private OrderManager Manager() => orderManager != null ? orderManager : OrderManager.Instance;

    private void TrySubscribe()
    {
        var m = Manager();
        if (m == null || subscribed) return;
        m.DeliveryResult += OnDeliveryResult;
        subscribed = true;
        Refresh();
    }

    private void OnDeliveryResult(bool success, string message)
    {
        messageText.text = message;
        messageText.color = success ? successColor : failColor;
        messageText.enabled = true;
        messageUntil = Time.unscaledTime + messageDuration;
        Refresh();
    }

    private void Refresh()
    {
        var m = Manager();
        float money = GameManager.Instance != null ? GameManager.Instance.Data.money : 0f;
        lastMoney = money;
        lastCompleted = m != null && m.IsCompleted;

        if (m == null)
        {
            titleText.text = "SİPARİŞ";
            bodyText.text = "Sahnede OrderManager yok.";
            return;
        }

        var o = m.ActiveOrder;
        titleText.text = "SİPARİŞ: " + o.title.ToUpperInvariant();
        string status = m.IsCompleted
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(successColor)}>TAMAMLANDI</color>"
            : "Bekliyor";
        string earned = m.IsCompleted ? $"{m.EarnedReward}$" : $"0$ (ödül {o.reward}$)";
        bodyText.text =
            $"{o.description}\n" +
            $"• Ağırlık > {o.minWeight:0.0} kg\n" +
            $"• Isı > {o.minHeat:0}\n" +
            $"• Ses < {o.maxSound:0}\n" +
            $"Durum: {status}   Kazanılan: {earned}\n" +
            $"Para: ${money:0}";
    }

    // Kart: koyu "mürekkep" kenarlık + krem kağıt zemin + metinler.
    private void BuildCard()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = cardOffset;
        rt.sizeDelta = cardSize;

        var border = gameObject.GetComponent<Image>();
        if (border == null) border = gameObject.AddComponent<Image>();
        border.color = inkColor;
        border.raycastTarget = false;

        var paper = NewChild("Paper", transform);
        Stretch(paper, 4f);
        var paperImg = paper.gameObject.AddComponent<Image>();
        paperImg.color = paperColor;
        paperImg.raycastTarget = false;

        titleText = NewText("Title", paper, 20, FontStyles.Bold);
        Place(titleText.rectTransform, 12f, 8f, 28f);

        bodyText = NewText("Body", paper, 15, FontStyles.Normal);
        Place(bodyText.rectTransform, 12f, 38f, 132f);

        messageText = NewText("Message", paper, 15, FontStyles.Bold);
        Place(messageText.rectTransform, 12f, 168f, 34f);
        messageText.enabled = false;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, float size, FontStyles style)
    {
        var t = NewChild(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.color = inkColor;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform NewChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    // Üstten 'top' kadar aşağıda, 'height' yüksekliğinde, yatayda kenarlardan 'side' içeride.
    private static void Place(RectTransform r, float side, float top, float height)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(side, -top - height);
        r.offsetMax = new Vector2(-side, -top);
    }
}
