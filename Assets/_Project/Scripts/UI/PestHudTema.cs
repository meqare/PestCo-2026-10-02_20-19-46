using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun içi HUD'un ortak görünümü: PestOS ile aynı Cesur Korkak Köpek stili.
/// Krem kağıt panel + kahve mürekkep kontur + sağ-alta kaymış sert mor gölge, Underdog fontu (17 punto ve üstü).
/// HUD parçaları (envanter, sipariş kartı, göstergeler...) panellerini buradan kurar ki hepsi aynı dili konuşsun.
/// </summary>
public static class PestHudTema
{
    #region Palet

    public static readonly Color Murekkep = new Color(0.17f, 0.1f, 0.08f);
    public static readonly Color Kagit = new Color(0.96f, 0.91f, 0.78f);
    public static readonly Color Kart = new Color(0.91f, 0.84f, 0.68f);
    public static readonly Color KartKoyu = new Color(0.85f, 0.77f, 0.6f);
    public static readonly Color KartSecili = new Color(0.99f, 0.82f, 0.42f);
    public static readonly Color Krem = new Color(0.98f, 0.94f, 0.84f);
    public static readonly Color Gece = new Color(0.14f, 0.09f, 0.15f);
    public static readonly Color Golge = new Color(0.08f, 0.03f, 0.09f, 0.55f);
    public static readonly Color Hardal = new Color(0.93f, 0.68f, 0.22f);
    public static readonly Color Mor = new Color(0.45f, 0.32f, 0.55f);
    public static readonly Color Soluk = new Color(0.47f, 0.38f, 0.31f);
    public static readonly Color Kirmizi = new Color(0.82f, 0.3f, 0.24f);
    public static readonly Color Yesil = new Color(0.3f, 0.56f, 0.18f);
    public static readonly Color YesilAcik = new Color(0.58f, 0.8f, 0.4f);
    public static readonly Color Turuncu = new Color(0.95f, 0.55f, 0.24f);
    public static readonly Color Turkuaz = new Color(0.45f, 0.75f, 0.7f);

    public const float Kontur = 3f;
    public const float GolgeKayma = 5f;

    #endregion

    #region Font ve Sprite

    private static TMP_FontAsset cizgiFont;
    private static bool fontDenendi;
    private static Sprite daire;

    /// <summary>Underdog (Türkçe karakterli çizgi film fontu); bulunamazsa null (TMP varsayılanı kalır).</summary>
    public static TMP_FontAsset CizgiFont
    {
        get
        {
            if (cizgiFont != null || fontDenendi) return cizgiFont;
            fontDenendi = true;

            var f = Resources.Load<Font>("PestOS/Underdog-Regular");
            if (f == null) return null;
            cizgiFont = TMP_FontAsset.CreateFontAsset(f);
            if (cizgiFont != null && TMP_Settings.defaultFontAsset != null)
                cizgiFont.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            return cizgiFont;
        }
    }

    public static Sprite Yuvarlak(int yaricap = 14) => CartoonIcons.RoundedPanel(yaricap);

    /// <summary>Kenarı yumuşatılmış dolu daire.</summary>
    public static Sprite Daire
    {
        get
        {
            if (daire != null) return daire;

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float m = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - m) * (x + 0.5f - m) + (y + 0.5f - m) * (y + 0.5f - m));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(m - 1f - d + 0.5f) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return daire = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }

    #endregion

    #region Paneller

    /// <summary>Bir çizgi film paneli: sert gölge, mürekkep kontur ve dolgu katmanları.</summary>
    public class Panel
    {
        public RectTransform kok;
        public Image golge, kontur, dolgu;

        /// <summary>İçerik dolgunun içine konur.</summary>
        public RectTransform Icerik => dolgu.rectTransform;
    }

    /// <summary>
    /// Verilen RectTransform'un içine (onu doldurarak) gölgeli, konturlu kağıt panel kurar.
    /// Panelin boyutunu/konumunu kok belirler; katmanlar ona yapışır.
    /// </summary>
    public static Panel PanelKur(RectTransform kok, Color dolgu, float kontur = Kontur, float golge = GolgeKayma, int yaricap = 14)
    {
        var p = new Panel { kok = kok };

        if (golge > 0f)
        {
            p.golge = Resim("Golge", kok, Yuvarlak(yaricap), Golge);
            Doldur(p.golge.rectTransform);
            p.golge.rectTransform.offsetMin = new Vector2(golge, -golge);
            p.golge.rectTransform.offsetMax = new Vector2(golge, -golge);
        }

        p.kontur = Resim("Kontur", kok, Yuvarlak(yaricap), Murekkep);
        Doldur(p.kontur.rectTransform);

        p.dolgu = Resim("Dolgu", kok, Yuvarlak(Mathf.Max(2, yaricap - Mathf.RoundToInt(kontur))), dolgu);
        Doldur(p.dolgu.rectTransform, kontur);
        return p;
    }

    /// <summary>Yeni bir çocuk nesne yaratıp içine panel kurar.</summary>
    public static Panel YeniPanel(string ad, Transform ebeveyn, Color dolgu, float kontur = Kontur, float golge = GolgeKayma, int yaricap = 14)
    {
        var kok = Bos(ad, ebeveyn);
        return PanelKur(kok, dolgu, kontur, golge, yaricap);
    }

    /// <summary>Panelin üstüne renkli başlık şeridi ve altına ince mürekkep çizgi koyar; şerit RectTransform'unu döndürür.</summary>
    public static RectTransform BaslikSeridi(Panel p, Color renk, float yukseklik)
    {
        var serit = Resim("Serit", p.Icerik, Yuvarlak(11), renk);
        var r = serit.rectTransform;
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(0f, -yukseklik);
        r.offsetMax = Vector2.zero;

        // Şeridin alt köşeleri düz olsun: alt yarıyı düz bir kutuyla kapat.
        var alt = Resim("SeritAlt", r, null, renk);
        alt.rectTransform.anchorMin = Vector2.zero;
        alt.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        alt.rectTransform.offsetMin = alt.rectTransform.offsetMax = Vector2.zero;

        var cizgi = Resim("SeritCizgi", p.Icerik, null, Murekkep);
        var c = cizgi.rectTransform;
        c.anchorMin = new Vector2(0f, 1f);
        c.anchorMax = Vector2.one;
        c.pivot = new Vector2(0.5f, 1f);
        c.offsetMin = new Vector2(0f, -yukseklik - 2.5f);
        c.offsetMax = new Vector2(0f, -yukseklik);
        return r;
    }

    /// <summary>Konturlu yuvarlak rozet (içine simge konur); simge Image'ını döndürür.</summary>
    public static Image Rozet(string ad, Transform ebeveyn, Color dolgu, Sprite simge, float cap, float kontur = 2.5f)
    {
        var dis = Resim(ad, ebeveyn, Daire, Murekkep);
        dis.rectTransform.sizeDelta = new Vector2(cap, cap);
        var ic = Resim("Ic", dis.rectTransform, Daire, dolgu);
        Doldur(ic.rectTransform, kontur);
        var s = Resim("Simge", dis.rectTransform, simge, Color.white);
        Doldur(s.rectTransform, cap * 0.14f);
        s.preserveAspect = true;
        return s;
    }

    #endregion

    #region Yazı

    /// <summary>Tema yazısı: büyük punto Underdog, küçük punto okunaklı varsayılan font; mürekkep renginde.</summary>
    public static TextMeshProUGUI Yazi(string ad, Transform ebeveyn, float punto, Color renk, TextAlignmentOptions hiza = TextAlignmentOptions.Left)
    {
        var go = new GameObject(ad, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(ebeveyn, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        YaziUygula(t, punto, renk);
        t.alignment = hiza;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        return t;
    }

    /// <summary>Var olan bir yazıyı temaya çevirir.</summary>
    public static void YaziUygula(TMP_Text t, float punto, Color renk)
    {
        if (punto >= 17f && CizgiFont != null) t.font = CizgiFont;
        t.fontSize = punto;
        t.fontStyle = FontStyles.Normal;
        t.color = renk;
        t.raycastTarget = false;
        KonturVer(t, 0f);
    }

    /// <summary>Koyu zemin üstündeki krem yazıya kalın mürekkep kontur (sayılar, ad yazıları).</summary>
    public static void KonturluYazi(TMP_Text t, float punto, Color renk, float kalinlik = 0.25f)
    {
        YaziUygula(t, punto, renk);
        KonturVer(t, kalinlik);
    }

    /// <summary>
    /// TMP konturu örnek materyal ister; kapalı (hiç uyanmamış) yazıda materyal yoktur ve hata verir.
    /// O durumda kontur yazı ilk açıldığında uygulanır.
    /// </summary>
    private static void KonturVer(TMP_Text t, float kalinlik)
    {
        if (t.isActiveAndEnabled && t.fontSharedMaterial != null)
        {
            if (kalinlik <= 0f && Mathf.Approximately(t.outlineWidth, 0f)) return;
            t.outlineWidth = kalinlik;
            t.outlineColor = (Color32)Murekkep;
            return;
        }

        if (kalinlik <= 0f) return;
        var later = t.GetComponent<GecikmeliKontur>();
        if (later == null) later = t.gameObject.AddComponent<GecikmeliKontur>();
        later.kalinlik = kalinlik;
    }

    /// <summary>Kapalıyken temaya giren yazının konturunu ilk açılışta verir.</summary>
    public class GecikmeliKontur : MonoBehaviour
    {
        public float kalinlik;

        private void Start()
        {
            var t = GetComponent<TMP_Text>();
            if (t != null && kalinlik > 0f)
            {
                t.outlineWidth = kalinlik;
                t.outlineColor = (Color32)Murekkep;
            }
            Destroy(this);
        }
    }

    #endregion

    #region Düzen Yardımcıları

    public static RectTransform Bos(string ad, Transform ebeveyn)
    {
        var go = new GameObject(ad, typeof(RectTransform));
        go.transform.SetParent(ebeveyn, false);
        return (RectTransform)go.transform;
    }

    public static Image Resim(string ad, Transform ebeveyn, Sprite sprite, Color renk)
    {
        var go = new GameObject(ad, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(ebeveyn, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = renk;
        img.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }
        return img;
    }

    public static void Doldur(RectTransform r, float iceri = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(iceri, iceri);
        r.offsetMax = new Vector2(-iceri, -iceri);
    }

    /// <summary>Tek çapa noktasıyla konum/boyut verir.</summary>
    public static void Yerlestir(RectTransform r, Vector2 capa, Vector2 pivot, Vector2 konum, Vector2 boyut)
    {
        r.anchorMin = r.anchorMax = capa;
        r.pivot = pivot;
        r.anchoredPosition = konum;
        r.sizeDelta = boyut;
    }

    #endregion
}
