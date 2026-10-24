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
    [SerializeField] private Vector2 cardSize = new Vector2(380f, 146f);
    [Tooltip("Kartın ekranın sol kenarından ve üstünden uzaklığı.")]
    [SerializeField] private Vector2 cardOffset = new Vector2(24f, -132f);
    [Min(0.5f)] [SerializeField] private float messageDuration = 3f;

    [Header("Renkler (retro çizgi film)")]
    [SerializeField] private Color inkColor = new Color(0.16f, 0.09f, 0.10f, 1f);
    [SerializeField] private Color paperColor = new Color(0.96f, 0.91f, 0.78f, 1f);
    [SerializeField] private Color headerColor = new Color(0.45f, 0.32f, 0.55f, 1f);
    [SerializeField] private Color successColor = new Color(0.10f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color failColor = new Color(0.80f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color waitColor = new Color(0.71f, 0.46f, 0.1f, 1f);

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
            titleText.text = "-";
            bodyText.text = "Sahnede OrderManager yok.";
            return;
        }

        var o = m.ActiveOrder;
        titleText.text = o.title;
        string status = m.IsCompleted
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(successColor)}><b>TAMAMLANDI</b></color>"
            : $"<color=#{ColorUtility.ToHtmlStringRGB(waitColor)}><b>BEKLİYOR</b></color>";
        string earned = m.IsCompleted ? $"Kazanılan <b>{m.EarnedReward}$</b>" : $"Ödül <b>{o.reward}$</b>";
        string dim = ColorUtility.ToHtmlStringRGB(PestHudTema.Soluk);
        bodyText.text =
            $"<color=#{dim}>{o.description}</color>\n" +
            $"<b>Ağırlık</b> > {o.minWeight:0.0} kg   <b>Isı</b> > {o.minHeat:0}   <b>Ses</b> < {o.maxSound:0}\n" +
            $"{status}   <color=#{dim}>·</color>   {earned}";
    }

    // Kart: PestHudTema paneli (sert gölge + mürekkep kontur + krem kağıt), mor başlık şeridi, altında metinler.
    private void BuildCard()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = cardOffset;
        rt.sizeDelta = cardSize;

        // Eski sürümden sahnede kalan düz kenarlık görünmesin.
        var old = gameObject.GetComponent<Image>();
        if (old != null) old.enabled = false;

        var panel = PestHudTema.YeniPanel("Card", transform, paperColor);
        PestHudTema.Doldur(panel.kok);
        var paper = panel.Icerik;

        var strip = PestHudTema.BaslikSeridi(panel, headerColor, HeaderHeight);
        var tag = PestHudTema.Yazi("Tag", strip, 14f, PestHudTema.Krem, TextAlignmentOptions.Left);
        tag.fontStyle = FontStyles.Bold;
        tag.characterSpacing = 8f;
        tag.text = "SİPARİŞ";
        Place(tag.rectTransform, 14f, 0f, HeaderHeight);

        titleText = PestHudTema.Yazi("Title", strip, 22f, PestHudTema.Krem, TextAlignmentOptions.Right);
        PestHudTema.KonturluYazi(titleText, 22f, PestHudTema.Krem, 0.2f);
        Place(titleText.rectTransform, 14f, 0f, HeaderHeight);

        bodyText = NewText("Body", paper, 17, FontStyles.Normal);
        bodyText.lineSpacing = 6f;
        Place(bodyText.rectTransform, 16f, HeaderHeight + 10f, 90f);

        messageText = PestHudTema.Yazi("Message", paper, 18f, inkColor, TextAlignmentOptions.BottomLeft);
        messageText.textWrappingMode = TextWrappingModes.Normal;
        Place(messageText.rectTransform, 16f, cardSize.y - 46f, 34f);
        messageText.enabled = false;
    }

    private const float HeaderHeight = 38f;

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
