using PestCo.Truck;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sahnede hazır kurulu HUD parçalarını (sayaçlar, sprey/vakum göstergeleri, para, telsiz altyazısı,
/// iş panosu ve sonuç ekranı) çalışırken PestHudTema stiline giydirir. Kendi kendini kuran parçalar
/// (envanter çubuğu, sipariş kartı, etkileşim yazısı) temayı zaten kendileri kullanır.
/// Canvas'a eklenir; kapatılırsa eski düz görünüm kalır.
/// </summary>
[DefaultExecutionOrder(-50)]
public class PestHudGorunum : MonoBehaviour
{
    [Header("Sol Üst Sayaçlar")]
    [SerializeField] private JuicyCounter _killCounter;
    [SerializeField] private JuicyCounter _nestCounter;
    [SerializeField] private Vector2 _statsOffset = new Vector2(24f, -24f);
    [SerializeField] private Vector2 _statsSize = new Vector2(380f, 94f);

    [Header("Göstergeler")]
    [SerializeField] private ToolGauge _sprayGauge;
    [SerializeField] private ToolGauge _vacuumGauge;
    [SerializeField] private Color _sprayColor = new Color(0.45f, 0.75f, 0.7f);
    [SerializeField] private Color _vacuumColor = new Color(0.93f, 0.68f, 0.22f);

    [Header("Para Rozeti")]
    [SerializeField] private TMP_Text _moneyText;
    [SerializeField] private TMP_Text _jobsText;

    [Header("Diğer")]
    [SerializeField] private TMP_Text _radioSubtitle;
    [SerializeField] private GameObject _jobBoardPanel;
    [SerializeField] private GameObject _resultPanel;

    private PestHudTema.Panel _subtitlePlate;
    private string _shownSubtitle;

    private void Awake()
    {
        GiydirSayaclar();
        GiydirGosterge(_sprayGauge, _sprayColor);
        GiydirGosterge(_vacuumGauge, _vacuumColor);
        GiydirPara();
        GiydirAltyazi();
        GiydirPano(_jobBoardPanel);
        GiydirPano(_resultPanel);
    }

    /// <summary>Açık pano HUD'un üstünde kalır; altyazı etiketi yazıya göre büyür, yazı boşken gizlenir.</summary>
    private void LateUpdate()
    {
        BringToFront(_jobBoardPanel);
        BringToFront(_resultPanel);

        if (_radioSubtitle == null || _subtitlePlate == null) return;

        string text = _radioSubtitle.text;
        if (text == _shownSubtitle) return;
        _shownSubtitle = text;

        bool show = !string.IsNullOrEmpty(text) && _radioSubtitle.gameObject.activeInHierarchy;
        _subtitlePlate.kok.gameObject.SetActive(show);
        if (!show) return;

        Vector2 size = _radioSubtitle.GetPreferredValues(text, _radioSubtitle.rectTransform.rect.width, 0f);
        _subtitlePlate.kok.sizeDelta = new Vector2(Mathf.Min(size.x, _radioSubtitle.rectTransform.rect.width) + 48f, size.y + 26f);
    }

    /// <summary>Panonun Canvas altındaki kökünü en öne alır (envanter çubuğu düğmeleri örtmesin).</summary>
    private void BringToFront(GameObject panel)
    {
        if (panel == null || !panel.activeInHierarchy) return;

        Transform root = panel.transform;
        while (root.parent != null && root.parent != transform) root = root.parent;
        if (root.parent == transform && root.GetSiblingIndex() != transform.childCount - 1) root.SetAsLastSibling();
    }

    #region Sayaçlar

    /// <summary>Öldürülen / yuva sayaçlarını sol üstte tek bir kağıt kartta, simgeli iki satır olarak toplar.</summary>
    private void GiydirSayaclar()
    {
        if (_killCounter == null && _nestCounter == null) return;

        Transform parent = (_killCounter != null ? _killCounter : _nestCounter).transform.parent;
        var card = PestHudTema.YeniPanel("StatsCard", parent, PestHudTema.Kagit);
        PestHudTema.Yerlestir(card.kok, new Vector2(0f, 1f), new Vector2(0f, 1f), _statsOffset, _statsSize);
        card.kok.SetAsFirstSibling();

        // Satırları ayıran ince mürekkep çizgi.
        var line = PestHudTema.Resim("Line", card.Icerik, null, new Color(PestHudTema.Murekkep.r, PestHudTema.Murekkep.g, PestHudTema.Murekkep.b, 0.25f));
        line.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        line.rectTransform.offsetMin = new Vector2(12f, -1f);
        line.rectTransform.offsetMax = new Vector2(-12f, 1f);

        SayacSatiri(card, _killCounter, PestOSCizim.HamamBocegi(), PestHudTema.Kirmizi, 1f);
        SayacSatiri(card, _nestCounter, PestOSCizim.Bocek(PestHudTema.Mor), PestHudTema.Mor, 0f);
    }

    private static void SayacSatiri(PestHudTema.Panel card, JuicyCounter counter, Sprite icon, Color accent, float top)
    {
        if (counter == null) return;

        var row = PestHudTema.Bos(counter.name + "Row", card.Icerik);
        row.anchorMin = new Vector2(0f, top > 0.5f ? 0.5f : 0f);
        row.anchorMax = new Vector2(1f, top > 0.5f ? 1f : 0.5f);
        row.offsetMin = row.offsetMax = Vector2.zero;

        var badge = PestHudTema.Rozet("Icon", row, Color.Lerp(accent, Color.white, 0.55f), icon, 34f);
        PestHudTema.Yerlestir((RectTransform)badge.transform.parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(34f, 34f));

        var text = counter.GetComponent<TMP_Text>();
        counter.transform.SetParent(row, false);
        var r = (RectTransform)counter.transform;
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = new Vector2(52f, 0f);
        r.offsetMax = new Vector2(-10f, 0f);

        PestHudTema.YaziUygula(text, 24f, PestHudTema.Murekkep);
        text.alignment = TextAlignmentOptions.Left;
        counter.SetColors(PestHudTema.Murekkep, PestHudTema.Yesil, PestHudTema.Kirmizi);
    }

    #endregion

    #region Göstergeler

    /// <summary>Sprey/vakum göstergesi: kağıt kart, simge rozeti, mürekkep konturlu yuvarlak dolum yolu.</summary>
    private static void GiydirGosterge(ToolGauge gauge, Color fill)
    {
        if (gauge == null) return;

        Transform t = gauge.transform;
        var card = PestHudTema.YeniPanel("Card", t, PestHudTema.Kagit, PestHudTema.Kontur, 4f, 14);
        PestHudTema.Doldur(card.kok);
        card.kok.SetAsFirstSibling();

        foreach (Transform child in t)
        {
            var img = child.GetComponent<Image>();
            switch (child.name)
            {
                case "Background":
                    img.enabled = false;
                    break;
                case "IconBox":
                    img.sprite = PestHudTema.Daire;
                    img.type = Image.Type.Simple;
                    img.color = PestHudTema.Murekkep;
                    var inner = PestHudTema.Resim("Inner", child, PestHudTema.Daire, Color.Lerp(fill, Color.white, 0.55f));
                    PestHudTema.Doldur(inner.rectTransform, 2.5f);
                    inner.transform.SetAsFirstSibling();
                    break;
                case "Track":
                    img.sprite = PestHudTema.Yuvarlak(8);
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = 1f;
                    img.color = PestHudTema.Murekkep;
                    break;
                case "BackFill_Red":
                    img.color = Color.Lerp(PestHudTema.Kirmizi, Color.white, 0.25f);
                    break;
                case "FrontFill_White":
                    img.color = fill;
                    break;
                case "Title":
                    var title = child.GetComponent<TMP_Text>();
                    PestHudTema.YaziUygula(title, 19f, PestHudTema.Murekkep);
                    break;
                case "Label":
                    var label = child.GetComponent<TMP_Text>();
                    PestHudTema.YaziUygula(label, 21f, PestHudTema.Murekkep);
                    var counter = child.GetComponent<JuicyCounter>();
                    if (counter != null) counter.SetColors(PestHudTema.Murekkep, PestHudTema.Yesil, PestHudTema.Kirmizi);
                    break;
                case "VacuumFullWarning":
                    var warn = child.GetComponent<TMP_Text>();
                    if (warn != null) PestHudTema.KonturluYazi(warn, 24f, PestHudTema.Kirmizi, 0.3f);
                    break;
            }
        }

        gauge.SetColors(fill, PestHudTema.Kirmizi);
    }

    #endregion

    #region Para

    /// <summary>Sağ üstte para rozeti: madeni para simgesi, büyük tutar, altında tamamlanan iş.</summary>
    private void GiydirPara()
    {
        if (_moneyText == null) return;

        var card = PestHudTema.YeniPanel("MoneyBadge", _moneyText.transform.parent, PestHudTema.Kagit);
        PestHudTema.Yerlestir(card.kok, Vector2.one, Vector2.one, new Vector2(-24f, -24f), new Vector2(240f, 80f));
        card.kok.SetAsFirstSibling();

        var strip = PestHudTema.Resim("Accent", card.Icerik, PestHudTema.Yuvarlak(11), PestHudTema.Yesil);
        strip.rectTransform.anchorMin = Vector2.zero;
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = new Vector2(0f, 0.5f);
        strip.rectTransform.sizeDelta = new Vector2(74f, 0f);
        strip.rectTransform.anchoredPosition = Vector2.zero;
        var stripLine = PestHudTema.Resim("AccentLine", card.Icerik, null, PestHudTema.Murekkep);
        PestHudTema.Yerlestir(stripLine.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(74f, 0f), new Vector2(2.5f, 0f));
        stripLine.rectTransform.anchorMin = new Vector2(0f, 0f);
        stripLine.rectTransform.anchorMax = new Vector2(0f, 1f);

        var coin = PestHudTema.Rozet("Coin", card.Icerik, PestHudTema.YesilAcik, PestOSCizim.Ikon("banka"), 52f);
        PestHudTema.Yerlestir((RectTransform)coin.transform.parent, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(37f, 0f), new Vector2(52f, 52f));

        _moneyText.transform.SetParent(card.Icerik, false);
        PestHudTema.YaziUygula(_moneyText, 34f, PestHudTema.Murekkep);
        _moneyText.alignment = TextAlignmentOptions.Left;
        var mr = _moneyText.rectTransform;
        mr.anchorMin = new Vector2(0f, 0.38f);
        mr.anchorMax = Vector2.one;
        mr.offsetMin = new Vector2(88f, 0f);
        mr.offsetMax = new Vector2(-10f, -2f);

        if (_jobsText == null) return;
        _jobsText.transform.SetParent(card.Icerik, false);
        PestHudTema.YaziUygula(_jobsText, 15f, PestHudTema.Soluk);
        _jobsText.alignment = TextAlignmentOptions.TopLeft;
        var jr = _jobsText.rectTransform;
        jr.anchorMin = Vector2.zero;
        jr.anchorMax = new Vector2(1f, 0.38f);
        jr.offsetMin = new Vector2(89f, 0f);
        jr.offsetMax = new Vector2(-10f, 0f);
    }

    #endregion

    #region Altyazı

    /// <summary>Telsiz altyazısının arkasına kağıt etiket koyar (boyutu LateUpdate'te yazıya uyar).</summary>
    private void GiydirAltyazi()
    {
        if (_radioSubtitle == null) return;

        var r = _radioSubtitle.rectTransform;
        _subtitlePlate = PestHudTema.YeniPanel("RadioPlate", r.parent, PestHudTema.Kagit);
        PestHudTema.Yerlestir(_subtitlePlate.kok, r.anchorMin, new Vector2(0.5f, 0.5f), r.anchoredPosition + (Vector2.one * 0.5f - r.pivot) * r.sizeDelta, r.sizeDelta);
        _subtitlePlate.kok.anchorMax = r.anchorMax;
        _subtitlePlate.kok.SetSiblingIndex(r.GetSiblingIndex());
        _subtitlePlate.kok.gameObject.SetActive(false);

        var strip = PestHudTema.Resim("Accent", _subtitlePlate.Icerik, PestHudTema.Yuvarlak(11), PestHudTema.Mor);
        strip.rectTransform.anchorMin = Vector2.zero;
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = new Vector2(0f, 0.5f);
        strip.rectTransform.sizeDelta = new Vector2(10f, 0f);

        PestHudTema.YaziUygula(_radioSubtitle, 26f, PestHudTema.Murekkep);
        _radioSubtitle.textWrappingMode = TextWrappingModes.Normal;
    }

    #endregion

    #region Panolar

    /// <summary>
    /// İş panosu ve sonuç ekranı: koyu mor perde, kartlar kağıt + mürekkep kontur + sert gölge,
    /// düğmeler canlı renk + kontur, yazılar Underdog.
    /// </summary>
    private static void GiydirPano(GameObject root)
    {
        if (root == null) return;

        var overlay = root.GetComponent<Image>();
        if (overlay != null) overlay.color = new Color(PestHudTema.Gece.r, PestHudTema.Gece.g, PestHudTema.Gece.b, 0.88f);

        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject == root) continue;

            bool button = img.GetComponent<Button>() != null;
            bool card = img.name.StartsWith("Card");
            if (!button && !card) continue;

            img.sprite = PestHudTema.Yuvarlak(14);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = card ? PestHudTema.Kagit : ButtonColor(img.name, img.color);
            Kontur(img.gameObject, card ? 7f : 5f);

            if (button)
            {
                var b = img.GetComponent<Button>();
                var colors = b.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
                colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
                colors.selectedColor = Color.white;
                b.colors = colors;
            }
        }

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            bool onCard = text.GetComponentInParent<Button>(true) == null && InCard(text.transform, root.transform);
            float size = Mathf.Max(text.fontSize, 16f);
            if (onCard) PestHudTema.YaziUygula(text, size, PestHudTema.Murekkep);
            else PestHudTema.KonturluYazi(text, size, PestHudTema.Krem, 0.22f);
        }
    }

    private static bool InCard(Transform t, Transform stop)
    {
        for (; t != null && t != stop; t = t.parent)
            if (t.name.StartsWith("Card")) return true;
        return false;
    }

    private static Color ButtonColor(string name, Color old)
    {
        if (name.Contains("Start") || name.Contains("Continue")) return PestHudTema.Yesil;
        if (name.Contains("Close")) return PestHudTema.Kirmizi;
        if (name.Contains("Retry")) return PestHudTema.Turuncu;
        return PestHudTema.Mor;
    }

    /// <summary>Var olan bir Image'a mürekkep kontur ve sert gölge efekti ekler.</summary>
    private static void Kontur(GameObject go, float shadow)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = PestHudTema.Murekkep;
        outline.effectDistance = new Vector2(3f, -3f);
        var drop = go.AddComponent<Shadow>();
        drop.effectColor = PestHudTema.Golge;
        drop.effectDistance = new Vector2(shadow, -shadow);
    }

    #endregion
}
