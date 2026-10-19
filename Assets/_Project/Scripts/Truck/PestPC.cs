using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PestCo.Truck
{
    /// <summary>
    /// Kamyondaki bilgisayar: E ile açılınca tam ekran "PestOS" masaüstü gelir.
    /// Görünüm oyunun çizgi film korku stiline uyar: krem kağıt pencereler, kahve mürekkep konturlar, sert gölgeler,
    /// elle çizilmiş ikonlar (PestOSCizim), "Hiçlik" gecesi duvar kağıdı, CRT tarama çizgisi + vinyet, Underdog fontu.
    /// Uygulamalar: Görevler, Market, Fanus Takip, PestBank, Notlar (düzenlenebilir), Ayarlar.
    /// Pencereler sürüklenir, çift tıkla büyür, küçültülür; görev çubuğu, aramalı Başlat menüsü ve bildirimler var.
    /// Açıkken oyun durur ve imleç serbest kalır. Esc ile kapanır.
    /// </summary>
    public class PestPC : MonoBehaviour
    {
        // Açıkken PlayerController imleci geri kilitlemesin, oyun tuşları (B, E, 1-9...) çalışmasın diye.
        public static bool IsOpen { get; private set; }

        [Tooltip("İşe Başla'nın yükleyeceği sahne (İş panosuyla aynı).")]
        [SerializeField] private string isSahnesi = "SampleScene";

        // ------------------------------------------------------------------ PALET (Cesur Korkak Köpek: krem kağıt, kahve mürekkep, hardal, soluk mor)

        private static readonly Color Murekkep = PestOSCizim.Murekkep;
        private static readonly Color Kagit = new Color(0.96f, 0.91f, 0.78f), Kart = new Color(0.91f, 0.84f, 0.68f), KartKoyu = new Color(0.85f, 0.77f, 0.6f);
        private static readonly Color KartSecili = new Color(0.99f, 0.82f, 0.42f), Gece = new Color(0.14f, 0.09f, 0.15f), Krem = new Color(0.98f, 0.94f, 0.84f);
        private static readonly Color Cizgi = new Color(0.17f, 0.1f, 0.08f, 0.2f), Hayalet = new Color(1f, 1f, 1f, 0f), HayaletKoyu = new Color(0.17f, 0.1f, 0.08f, 0f);
        private static readonly Color Hardal = new Color(0.93f, 0.68f, 0.22f), Mor = new Color(0.45f, 0.32f, 0.55f);
        private static readonly Color Soluk = new Color(0.47f, 0.38f, 0.31f), Kirmizi = new Color(0.82f, 0.3f, 0.24f);
        private static readonly Color Yesil = new Color(0.3f, 0.56f, 0.18f), YesilAcik = new Color(0.58f, 0.8f, 0.4f);
        private static readonly Color Turuncu = new Color(0.95f, 0.55f, 0.24f), Mavi = new Color(0.58f, 0.68f, 0.92f), Turkuaz = new Color(0.45f, 0.75f, 0.7f);
        private const string SolukHex = "#78614F", HardalHex = "#B5761A", YesilHex = "#3F7F25", KirmiziHex = "#B8382E", MorHex = "#6A4C80", AcikYesilHex = "#A6DC74";

        private static readonly Vector2[] Olcekler = { new Vector2(2200, 1238), new Vector2(1920, 1080), new Vector2(1640, 922) };
        private static readonly string[] OlcekAdlari = { "Küçük", "Normal", "Büyük" };
        private const string TemaAnahtar = "PestOS.Manzara", // eski tema listesi değişti; yeni anahtar herkesi "Hiçlik Gecesi"nden başlatır
 OlcekAnahtar = "PestOS.Olcek", NotAnahtar = "PestOS.Notlar";
        private const float CubukYukseklik = 60f, BaslikYukseklik = 46f, Kontur = 3.5f;

        private static TMP_FontAsset cizgiFont;

        // ------------------------------------------------------------------ DURUM

        private Canvas canvas;
        private CanvasScaler olceklendirici;
        private RectTransform masaustu, pencereKoku, gorevCubugu, baslatMenusu, baslatIzgara, acilis, bildirimKoku;
        private RectTransform gorevDugmeleri, baslatDugmesi, acilisDolgu, acilisMaskot, acilisBar;
        private Image duvarKagidi;
        private TMP_Text saatYazi, paraYazi, widgetSaat, widgetTarih, widgetBilgi, acilisLogo;
        private TMP_InputField arama;
        private Sprite yuvarlak, daire;
        private Texture2D grenDokusu;
        private readonly Dictionary<string, Uygulama> uygulamalar = new Dictionary<string, Uygulama>();
        private readonly List<Uygulama> sira = new List<Uygulama>();
        private Uygulama aktif;
        private float yenileZaman;
        private int temaIndeks, olcekIndeks, acilanSayisi;
        private bool ilkAcilisYapildi, sonKareYaziOdak;
        private BugShelf rafOnbellek;

        // Görevler
        private List<ContractData> ilanlar = new List<ContractData>();
        private int seciliIlan = -1;
        // Market / Fanus / Notlar
        private int marketFiltre, notSekme = 1;
        private string fanusMesaj = "";

        // Banka hareketleri: oturum boyunca (sahne değişse de) tutulur
        private struct Hareket { public DateTime zaman; public string ad; public float miktar; }
        private static readonly List<Hareket> hareketler = new List<Hareket>();
        private static float sonPara = float.NaN;

        private class Uygulama
        {
            public string id, ad, baslik;
            public Color renk;
            public Vector2 boyut;
            public RectTransform pencere, icerik, gorevCizgi;
            public TMP_Text baslikYazi;
            public Image baslikZemin, baslikDolgu;
            public Button gorevDugmesi;
            public bool buyuk;
            public Vector2 eskiPoz;
            public Action<RectTransform> kur;   // içerik baştan kurulur
            public Action yenile;              // canlı değerler (her 0.25 sn)
        }

        // ------------------------------------------------------------------ AÇ / KAPAT

        public void Open()
        {
            if (IsOpen) return;
            if (canvas == null) Kur();
            canvas.gameObject.SetActive(true);
            IsOpen = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            baslatMenusu.gameObject.SetActive(false);
            DisHareketleriYakala();
            // Kapalıyken değişen veriler (para, fanus, iş) için açık pencereleri tazele
            foreach (var u in sira)
                if (u.pencere != null && u.pencere.gameObject.activeSelf) YenidenKur(u);
            StartCoroutine(Acilis(!ilkAcilisYapildi));
            ilkAcilisYapildi = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            PlayerPrefs.Save();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            canvas.gameObject.SetActive(false);
            IsOpen = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // PlayerController aynı karede Esc'yi görüp imleci serbest bırakabilir: bir kare sonra yeniden kilitle
            if (isActiveAndEnabled) StartCoroutine(SonrakiKaredeKilitle());
        }

        private static IEnumerator SonrakiKaredeKilitle()
        {
            yield return null;
            if (IsOpen) yield break;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private void Update()
        {
            if (!IsOpen) return;
            var k = Keyboard.current;
            bool yaziOdak = YaziOdakta();
            if (k != null && k.escapeKey.wasPressedThisFrame)
            {
                // Sıra: Başlat menüsü → yazı alanı → bilgisayarı kapat
                if (baslatMenusu.gameObject.activeSelf) BaslatGoster(false);
                else if (yaziOdak || sonKareYaziOdak) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
                else Close();
                sonKareYaziOdak = false;
                return;
            }
            sonKareYaziOdak = yaziOdak;
            TiklamaOdagi();

            if (Time.unscaledTime < yenileZaman) return;
            yenileZaman = Time.unscaledTime + 0.25f;
            var simdi = DateTime.Now;
            saatYazi.text = simdi.ToString("HH:mm") + "\n<size=68%><alpha=#99>" + simdi.ToString("dd.MM.yyyy") + "</size>";
            paraYazi.text = $"<size=68%><alpha=#99>BAKİYE</size><alpha=#FF>  <color={AcikYesilHex}>${Para():N0}</color>";
            WidgetYenile(simdi);
            foreach (var u in sira)
                if (u.pencere != null && u.pencere.gameObject.activeSelf) u.yenile?.Invoke();
        }

        private static bool YaziOdakta()
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
            var g = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
            return g != null && g.isFocused;
        }

        // Fareye basılan pencereyi öne getirir; Başlat menüsü dışına tıklanınca menüyü kapatır.
        private void TiklamaOdagi()
        {
            var m = Mouse.current;
            if (m == null || !m.leftButton.wasPressedThisFrame) return;
            Vector2 mp = m.position.ReadValue();
            if (baslatMenusu.gameObject.activeSelf)
            {
                if (Icinde(baslatMenusu, mp)) return;
                if (!Icinde(baslatDugmesi, mp)) BaslatGoster(false);
            }
            if (Icinde(gorevCubugu, mp)) return;
            for (int i = pencereKoku.childCount - 1; i >= 0; i--)
            {
                var c = pencereKoku.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeSelf || !Icinde(c, mp)) continue;
                foreach (var u in sira)
                    if (u.pencere == c) { if (aktif != u) Odakla(u); return; }
                return;
            }
        }

        private static bool Icinde(RectTransform r, Vector2 ekranNoktasi) =>
            r != null && RectTransformUtility.RectangleContainsScreenPoint(r, ekranNoktasi, null);

        private IEnumerator Acilis(bool tam)
        {
            // Oyun durduğu için gerçek zaman. İlk açılışta zıplayan böcekli yükleme ekranı, sonra sadece kısa geçiş.
            acilis.gameObject.SetActive(true);
            var grup = acilis.GetComponent<CanvasGroup>();
            grup.alpha = 1f;
            acilisBar.gameObject.SetActive(tam);
            acilisLogo.gameObject.SetActive(tam);
            acilisMaskot.gameObject.SetActive(tam);
            if (tam)
            {
                for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
                {
                    acilisDolgu.anchorMax = new Vector2(Mathf.SmoothStep(0f, 1f, t / 1.1f), 1f);
                    float zipla = Mathf.Abs(Mathf.Sin(t * 9f));
                    acilisMaskot.anchoredPosition = new Vector2(0f, 120f + zipla * 26f);
                    acilisMaskot.localScale = new Vector3(1f + (1f - zipla) * 0.08f, 1f - (1f - zipla) * 0.08f, 1f); // ezil-uza
                    yield return null;
                }
            }
            float sure = tam ? 0.3f : 0.18f;
            for (float t = 0f; t < sure; t += Time.unscaledDeltaTime)
            {
                grup.alpha = 1f - t / sure;
                yield return null;
            }
            acilis.gameObject.SetActive(false);
            yenileZaman = 0f;
        }

        // ------------------------------------------------------------------ KURULUM

        private static void FontYukle()
        {
            if (cizgiFont != null) return;
            var f = Resources.Load<Font>("PestOS/Underdog-Regular");
            if (f == null) return;
            cizgiFont = TMP_FontAsset.CreateFontAsset(f);
            if (cizgiFont != null && TMP_Settings.defaultFontAsset != null)
                cizgiFont.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
        }

        private void Kur()
        {
            FontYukle();
            yuvarlak = YuvarlakSprite();
            daire = DaireSprite();
            grenDokusu = PestOSCizim.GrenDokusu();
            temaIndeks = Mathf.Clamp(PlayerPrefs.GetInt(TemaAnahtar, 0), 0, PestOSCizim.Manzaralar.Length - 1);
            olcekIndeks = Mathf.Clamp(PlayerPrefs.GetInt(OlcekAnahtar, 1), 0, Olcekler.Length - 1);

            // Sahnede EventSystem yoksa arayüz tıklanamaz: yedek olarak kur
            if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem (PestOS)", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var go = new GameObject("PestPC_Ekran", typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            olceklendirici = go.AddComponent<CanvasScaler>();
            olceklendirici.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olceklendirici.matchWidthOrHeight = 0.5f;
            OlcekUygula();
            go.AddComponent<GraphicRaycaster>();
            var kok = (RectTransform)go.transform;

            // Masaüstü: "Hiçlik" gecesi duvar kağıdı (ev, yel değirmeni, ay, çalılıkta parlayan gözler)
            masaustu = Panel("Masaustu", kok, Color.white, 0f);
            Doldur(masaustu);
            duvarKagidi = masaustu.GetComponent<Image>();
            TemaUygula();

            UygulamalariTanimla();
            var ikonlar = Rt("Ikonlar", masaustu);
            Konum(ikonlar, new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(120f, 900f), new Vector2(0f, 1f));
            var dikey = ikonlar.gameObject.AddComponent<VerticalLayoutGroup>();
            dikey.spacing = 6f; dikey.childControlHeight = false; dikey.childControlWidth = false; dikey.childForceExpandHeight = false;
            foreach (var u in sira) Ikon(ikonlar, u);

            MasaustuWidget();

            pencereKoku = Rt("Pencereler", masaustu);
            Doldur(pencereKoku);
            pencereKoku.offsetMin = new Vector2(0f, CubukYukseklik);

            bildirimKoku = Rt("Bildirimler", kok);
            Konum(bildirimKoku, new Vector2(1f, 0f), new Vector2(-18f, CubukYukseklik + 16f), new Vector2(390f, 540f), new Vector2(1f, 0f));
            var bdikey = bildirimKoku.gameObject.AddComponent<VerticalLayoutGroup>();
            bdikey.childAlignment = TextAnchor.LowerRight; bdikey.spacing = 12f;
            bdikey.childControlWidth = true; bdikey.childControlHeight = true; bdikey.childForceExpandHeight = false; bdikey.childForceExpandWidth = true;

            GorevCubugu(kok);
            BaslatMenusu(kok);
            EkranKatmani(kok);
            AcilisEkrani(kok);
        }

        private void UygulamalariTanimla()
        {
            Ekle("gorev", "Görevler", "PestJobs — İş İlanları", Turuncu, new Vector2(1080, 700), GorevlerKur, GorevlerYenile);
            Ekle("market", "Market", "PestMarket — Ekipman", YesilAcik, new Vector2(920, 680), MarketKur, MarketYenile);
            Ekle("fanus", "Fanus Takip", "Fanus Takip — Numune Bölmeleri", Turkuaz, new Vector2(880, 730), FanusKur, null);
            Ekle("banka", "PestBank", "PestBank — Hesap Özeti", Mavi, new Vector2(800, 740), BankaKur, BankaYenile);
            Ekle("not", "Notlar", "Not Defteri", Hardal, new Vector2(800, 600), NotlarKur, null);
            Ekle("ayar", "Ayarlar", "Sistem Ayarları", new Color(0.74f, 0.64f, 0.82f), new Vector2(720, 640), AyarlarKur, null);
        }

        private void Ekle(string id, string ad, string baslik, Color renk, Vector2 boyut, Action<RectTransform> kur, Action yenile)
        {
            var u = new Uygulama { id = id, ad = ad, baslik = baslik, renk = renk, boyut = boyut, kur = kur, yenile = yenile };
            uygulamalar[id] = u;
            sira.Add(u);
        }

        // ------------------------------------------------------------------ TEMA / ÖLÇEK

        // Duvar kağıdı arka planda çizilir (açılış animasyonu sırasında); hazır olana kadar gökyüzü renginde düz zemin
        private void TemaUygula()
        {
            int istenen = temaIndeks;
            if (duvarKagidi.sprite == null) duvarKagidi.color = PestOSCizim.Manzaralar[istenen].ust;
            StartCoroutine(PestOSCizim.DuvarHazirla(istenen, s =>
            {
                if (duvarKagidi == null || temaIndeks != istenen) return;
                duvarKagidi.sprite = s;
                duvarKagidi.color = Color.white;
            }));
        }

        private void OlcekUygula() => olceklendirici.referenceResolution = Olcekler[olcekIndeks];

        // CRT monitör hissi: ince tarama çizgileri + tüp ekran vinyeti (tıklamayı engellemez)
        private void EkranKatmani(RectTransform kok)
        {
            var tarama = new GameObject("Tarama", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            tarama.transform.SetParent(kok, false);
            Doldur(tarama.rectTransform);
            tarama.texture = PestOSCizim.TaramaDokusu();
            tarama.uvRect = new Rect(0f, 0f, 1f, 1080f / 3f);
            tarama.color = new Color(0.08f, 0.03f, 0.08f, 0.09f);
            tarama.raycastTarget = false;
            var vin = Resim("Vinyet", kok, PestOSCizim.Vinyet());
            vin.color = new Color(0.07f, 0.02f, 0.08f, 0.6f);
            Doldur(vin.rectTransform);
        }

        // ------------------------------------------------------------------ MASAÜSTÜ PARÇALARI

        private void Ikon(RectTransform p, Uygulama u)
        {
            var b = Dugme("Ikon_" + u.id, p, "", Hayalet, Murekkep, () => PencereAc(u), 1.4f);
            var kutu = (RectTransform)b.transform;
            kutu.sizeDelta = new Vector2(116f, 124f);
            var simge = Resim("Simge", kutu, PestOSCizim.Ikon(u.id));
            Konum(simge, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(84f, 84f), new Vector2(0.5f, 1f));
            var etiket = Kutu("Etiket", kutu, Kagit, 2.2f, 2.4f);
            etiket.GetComponent<Image>().raycastTarget = false;
            Konum(etiket, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(108f, 30f), new Vector2(0.5f, 0f));
            etiket.localRotation = Quaternion.Euler(0f, 0f, (u.id.GetHashCode() % 5) * 0.8f);
            var ad = Metin("Ad", etiket, u.ad, 17, Murekkep, TextAlignmentOptions.Center);
            Doldur(ad.rectTransform, 4f, 0f);
            Kirp(ad);
        }

        private void MasaustuWidget()
        {
            // Duvara iğnelenmiş yapışkan not
            var w = Kutu("Widget", masaustu, new Color(0.99f, 0.88f, 0.5f), Kontur, 3f);
            Konum(w, new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(380f, 256f), new Vector2(1f, 1f));
            w.localRotation = Quaternion.Euler(0f, 0f, -2.2f);
            SertGolge(w, 9f);
            var igne = Resim("Igne", w, daire);
            igne.color = Murekkep;
            Konum(igne, new Vector2(0.5f, 1f), new Vector2(0f, 8f), new Vector2(26f, 26f), new Vector2(0.5f, 0.5f));
            var ignIc = Resim("Ic", igne.transform, daire);
            ignIc.color = Kirmizi;
            Doldur(ignIc.rectTransform, 4f, 4f);
            widgetSaat = Metin("Saat", w, "00:00", 88, Murekkep, TextAlignmentOptions.TopLeft);
            Konum(widgetSaat.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(340f, 100f), new Vector2(0f, 1f));
            widgetTarih = Metin("Tarih", w, "", 20, Soluk, TextAlignmentOptions.TopLeft);
            Konum(widgetTarih.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -110f), new Vector2(340f, 28f), new Vector2(0f, 1f));
            var c = Panel("Cizgi", w, Cizgi, 0f);
            Kenar(c, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -150f), new Vector2(-24f, -148f));
            widgetBilgi = Metin("Bilgi", w, "", 18, Murekkep, TextAlignmentOptions.TopLeft, false);
            widgetBilgi.lineSpacing = 16f;
            Kenar(widgetBilgi.rectTransform, Vector2.zero, Vector2.one, new Vector2(26f, 12f), new Vector2(-24f, -160f));
        }

        private static readonly string[] Aylar = { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
        private static readonly string[] Gunler = { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };

        private void WidgetYenile(DateTime simdi)
        {
            widgetSaat.text = simdi.ToString("HH:mm") + $"<size=40%><color={SolukHex}>:" + simdi.ToString("ss") + "</color></size>";
            widgetTarih.text = $"{simdi.Day} {Aylar[simdi.Month - 1]} {simdi.Year}, {Gunler[(int)simdi.DayOfWeek]}";
            var jm = JobManager.Instance;
            string isDurum = jm != null ? $"<color={HardalHex}><b>Sürüyor</b></color> · temizlik %{jm.CleanPercent * 100f:0}" : $"<color={SolukHex}>Aktif iş yok</color>";
            int numune = 0;
            var raf = Raf();
            if (raf != null) foreach (var b in raf.GetComponentsInChildren<BugCage>()) numune += b.Count;
            widgetBilgi.text =
                $"<color={SolukHex}>Bakiye</color><pos=34%><b><color={YesilHex}>${Para():N0}</color></b>\n" +
                $"<color={SolukHex}>İş</color><pos=34%>{isDurum}\n" +
                $"<color={SolukHex}>Fanus</color><pos=34%>{numune} numune";
        }

        private void GorevCubugu(RectTransform kok)
        {
            gorevCubugu = Panel("GorevCubugu", kok, Gece, 0f);
            gorevCubugu.anchorMin = new Vector2(0f, 0f); gorevCubugu.anchorMax = new Vector2(1f, 0f);
            gorevCubugu.pivot = new Vector2(0.5f, 0f);
            gorevCubugu.sizeDelta = new Vector2(0f, CubukYukseklik);
            var ust = Panel("UstCizgi", gorevCubugu, Murekkep, 0f);
            Kenar(ust, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -4f), Vector2.zero);
            var gren = Gren(gorevCubugu, new Vector2(1920f, CubukYukseklik), 0f, new Color(1f, 0.85f, 0.7f, 0.05f));
            gren.rectTransform.offsetMax = new Vector2(0f, -4f);

            var baslat = Dugme("Baslat", gorevCubugu, "", Hardal, Murekkep, () => BaslatGoster(!baslatMenusu.gameObject.activeSelf), 1.6f);
            baslatDugmesi = (RectTransform)baslat.transform;
            Konum(baslatDugmesi, new Vector2(0f, 0.5f), new Vector2(10f, -2f), new Vector2(150f, 44f), new Vector2(0f, 0.5f));
            var maskot = Resim("Maskot", baslatDugmesi, PestOSCizim.Bocek(PestOSCizim.BocekYesil));
            Konum(maskot, new Vector2(0f, 0.5f), new Vector2(4f, 3f), new Vector2(48f, 48f), new Vector2(0f, 0.5f));
            maskot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var lt = Metin("Yazi", baslatDugmesi, "PestOS", 24, Murekkep, TextAlignmentOptions.Left);
            Kenar(lt.rectTransform, Vector2.zero, Vector2.one, new Vector2(54f, 0f), Vector2.zero);

            var ayrac = Panel("Ayrac", gorevCubugu, new Color(1f, 0.9f, 0.8f, 0.14f), 0f);
            Konum(ayrac, new Vector2(0f, 0.5f), new Vector2(172f, -2f), new Vector2(2f, 32f), new Vector2(0f, 0.5f));

            gorevDugmeleri = Rt("AcikPencereler", gorevCubugu);
            Kenar(gorevDugmeleri, Vector2.zero, Vector2.one, new Vector2(186f, 8f), new Vector2(-390f, -12f));
            var yatay = gorevDugmeleri.gameObject.AddComponent<HorizontalLayoutGroup>();
            yatay.spacing = 8f; yatay.childControlWidth = false; yatay.childForceExpandWidth = false; yatay.childControlHeight = true;

            var ayrac2 = Panel("Ayrac2", gorevCubugu, new Color(1f, 0.9f, 0.8f, 0.14f), 0f);
            Konum(ayrac2, new Vector2(1f, 0.5f), new Vector2(-146f, -2f), new Vector2(2f, 32f), new Vector2(1f, 0.5f));
            paraYazi = Metin("Para", gorevCubugu, "", 22, Krem, TextAlignmentOptions.Right);
            Konum(paraYazi.rectTransform, new Vector2(1f, 0.5f), new Vector2(-162f, -2f), new Vector2(230f, 44f), new Vector2(1f, 0.5f));
            saatYazi = Metin("Saat", gorevCubugu, "00:00", 20, Krem, TextAlignmentOptions.Center);
            Konum(saatYazi.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, -2f), new Vector2(124f, 52f), new Vector2(1f, 0.5f));
        }

        private void BaslatMenusu(RectTransform kok)
        {
            baslatMenusu = Rt("BaslatMenusu", kok);
            Konum(baslatMenusu, Vector2.zero, new Vector2(10f, CubukYukseklik + 12f), new Vector2(480f, 590f), Vector2.zero);
            SertGolge(baslatMenusu, 10f);
            var govde = Kutu("Govde", baslatMenusu, Kagit, Kontur, 1.4f);
            Doldur(govde);
            Gren(govde, new Vector2(480f, 590f));

            // Mor başlık şeridi + maskot
            var serit = BaslikSeridi(govde, Mor, 92f, out _, out _);
            var maskot = Resim("Maskot", serit, PestOSCizim.Bocek(PestOSCizim.BocekYesil));
            Konum(maskot, new Vector2(0f, 0.5f), new Vector2(10f, -4f), new Vector2(92f, 92f), new Vector2(0f, 0.5f));
            maskot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            var ad = Metin("Ad", serit, $"PestOS\n<size=48%><alpha=#BB>Saha Ekibi · kamyon-terminal-01</size>", 38, Krem, TextAlignmentOptions.Left);
            Kenar(ad.rectTransform, Vector2.zero, Vector2.one, new Vector2(108f, 4f), new Vector2(-16f, -4f));

            arama = Giris("Arama", govde, "Uygulama ara...", 19, false, Color.white, Murekkep);
            Kenar((RectTransform)arama.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(22f, -160f), new Vector2(-22f, -112f));
            arama.onValueChanged.AddListener(BaslatListesi);
            arama.onSubmit.AddListener(s =>
            {
                var bulunan = Eslesenler(s);
                if (bulunan.Count == 0) return;
                BaslatGoster(false);
                PencereAc(bulunan[0]);
            });

            var et = Metin("Etiket", govde, "<b>UYGULAMALAR</b>", 13, Soluk, TextAlignmentOptions.Left);
            et.characterSpacing = 6f;
            Kenar(et.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(26f, -196f), new Vector2(-24f, -170f));

            baslatIzgara = Rt("Izgara", govde);
            Kenar(baslatIzgara, Vector2.zero, Vector2.one, new Vector2(20f, 86f), new Vector2(-20f, -202f));
            var izgara = baslatIzgara.gameObject.AddComponent<GridLayoutGroup>();
            izgara.cellSize = new Vector2(140f, 128f); izgara.spacing = new Vector2(6f, 6f);
            izgara.constraint = GridLayoutGroup.Constraint.FixedColumnCount; izgara.constraintCount = 3;

            var alt = Panel("Alt", govde, KartKoyu, 0f);
            Kenar(alt, Vector2.zero, new Vector2(1f, 0f), new Vector2(Kontur, Kontur), new Vector2(-Kontur, 74f));
            var altCizgi = Panel("Cizgi", alt, Murekkep, 0f);
            Kenar(altCizgi, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -2.5f), Vector2.zero);
            var ipucu = Metin("Ipucu", alt, $"<color={SolukHex}>Yaz + Enter: uygulamayı aç</color>", 15, Murekkep, TextAlignmentOptions.Left, false);
            Kenar(ipucu.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 0f), new Vector2(-160f, 0f));
            var kapat = Dugme("Kapat", alt, "Kapat", Kirmizi, Krem, Close, 1.6f, 20f);
            Konum(kapat, new Vector2(1f, 0.5f), new Vector2(-16f, -1f), new Vector2(126f, 44f), new Vector2(1f, 0.5f));

            BaslatListesi("");
            baslatMenusu.gameObject.SetActive(false);
        }

        private void BaslatGoster(bool goster)
        {
            baslatMenusu.gameObject.SetActive(goster);
            if (!goster)
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == arama.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                return;
            }
            arama.text = "";
            arama.Select();
            arama.ActivateInputField();
        }

        private void BaslatListesi(string filtre)
        {
            Temizle(baslatIzgara);
            var liste = Eslesenler(filtre);
            foreach (var u in liste)
            {
                var uu = u;
                var b = Dugme("Menu_" + u.id, baslatIzgara, "", HayaletKoyu, Murekkep, () => { BaslatGoster(false); PencereAc(uu); }, 1.4f);
                var s = Resim("Simge", b.transform, PestOSCizim.Ikon(u.id));
                Konum(s, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(78f, 78f), new Vector2(0.5f, 1f));
                var ad = Metin("Ad", b.transform, u.ad, 18, Murekkep, TextAlignmentOptions.Center);
                Kenar(ad.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(4f, 6f), new Vector2(-4f, 36f));
            }
            if (liste.Count == 0)
            {
                var t = Metin("Yok", baslatIzgara, $"<color={SolukHex}>\"{filtre}\" diye bir uygulama yok.</color>", 17, Murekkep, TextAlignmentOptions.Center);
                t.rectTransform.sizeDelta = new Vector2(420f, 60f);
            }
        }

        private List<Uygulama> Eslesenler(string filtre)
        {
            var sonuc = new List<Uygulama>();
            string f = Kucuk(filtre).Trim();
            foreach (var u in sira)
                if (f.Length == 0 || Kucuk(u.ad).Contains(f) || Kucuk(u.baslik).Contains(f)) sonuc.Add(u);
            return sonuc;
        }

        // Türkçe İ/I harflerini bozmadan küçült
        private static string Kucuk(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace('İ', 'i').Replace('I', 'ı').ToLowerInvariant();

        private void AcilisEkrani(RectTransform kok)
        {
            acilis = Panel("Acilis", kok, Gece, 0f);
            Doldur(acilis);
            acilis.gameObject.AddComponent<CanvasGroup>();
            Gren(acilis, new Vector2(1920f, 1080f), 0f, new Color(1f, 0.85f, 0.7f, 0.06f));
            var maskot = Resim("Maskot", acilis, PestOSCizim.Bocek(PestOSCizim.BocekYesil));
            Konum(maskot, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(210f, 210f), new Vector2(0.5f, 0f));
            acilisMaskot = maskot.rectTransform;
            acilisLogo = Metin("Logo", acilis, $"PestOS\n<size=26%><color=#F6EDD2><alpha=#BB>Haşere mi? Biz hallederiz.</color></size>", 100, Hardal, TextAlignmentOptions.Center);
            Konum(acilisLogo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 200f), new Vector2(0.5f, 0.5f));
            acilisBar = Kutu("Bar", acilis, new Color(0.24f, 0.16f, 0.24f), Kontur, 2.2f, Krem);
            Konum(acilisBar, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(360f, 28f), new Vector2(0.5f, 0.5f));
            var alan = Rt("Alan", acilisBar);
            Doldur(alan, Kontur + 2f, Kontur + 2f);
            acilisDolgu = Panel("Dolgu", alan, Hardal, 3f);
            acilisDolgu.anchorMin = Vector2.zero; acilisDolgu.anchorMax = new Vector2(0f, 1f);
            acilisDolgu.offsetMin = acilisDolgu.offsetMax = Vector2.zero;
            acilis.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ PENCERE YÖNETİMİ

        private void PencereAc(Uygulama u)
        {
            if (u.pencere == null) PencereOlustur(u);
            if (!u.pencere.gameObject.activeSelf)
            {
                u.pencere.gameObject.SetActive(true);
                StartCoroutine(Belir(u.pencere));
            }
            Odakla(u);
            YenidenKur(u);
        }

        private void PencereOlustur(Uygulama u)
        {
            var r = Rt("Pencere_" + u.id, pencereKoku);
            r.gameObject.AddComponent<CanvasGroup>();
            r.sizeDelta = u.boyut;
            int n = acilanSayisi++ % 6;
            r.anchoredPosition = new Vector2(-60f + n * 36f, 20f - n * 30f);
            u.pencere = r;
            u.buyuk = false;

            SertGolge(r, 11f);
            var govde = Kutu("Govde", r, Kagit, Kontur, 1.4f);
            Doldur(govde);
            Gren(govde, u.boyut);

            // Başlık: uygulama renginde şerit, altta mürekkep çizgi
            var bas = BaslikSeridi(govde, u.renk, BaslikYukseklik, out u.baslikZemin, out u.baslikDolgu);
            bas.gameObject.AddComponent<PencereSurukle>().Hazirla(r, pencereKoku, canvas,
                () => Odakla(u), () => BuyutKucult(u), () => { if (u.buyuk) BuyutKucult(u); });
            var simge = Resim("Simge", bas, PestOSCizim.Ikon(u.id));
            Konum(simge, new Vector2(0f, 0.5f), new Vector2(8f, 1f), new Vector2(40f, 40f), new Vector2(0f, 0.5f));
            u.baslikYazi = Metin("Yazi", bas, u.baslik, 22, Murekkep, TextAlignmentOptions.Left);
            Kenar(u.baslikYazi.rectTransform, Vector2.zero, Vector2.one, new Vector2(54f, 0f), new Vector2(-124f, 0f));
            Kirp(u.baslikYazi);

            // Şeker gibi yuvarlak düğmeler: küçült (yeşil), büyüt (hardal), kapat (kırmızı)
            var kapat = YuvarlakDugme("Kapat", bas, Kirmizi, () => PencereKapat(u), -12f);
            Cizik(kapat, 11f, 45f); Cizik(kapat, 11f, -45f);
            var buyut = YuvarlakDugme("Buyut", bas, Hardal, () => BuyutKucult(u), -48f);
            KareCiz(buyut, 9f);
            var kucult = YuvarlakDugme("Kucult", bas, YesilAcik, () => Kucult(u), -84f);
            Cizik(kucult, 11f, 0f);

            u.icerik = Rt("Icerik", r);
            Kenar(u.icerik, Vector2.zero, Vector2.one, new Vector2(20f, 18f), new Vector2(-20f, -(Kontur + BaslikYukseklik + 3f + 14f)));

            // Görev çubuğu düğmesi: kağıt etiket
            var gd = Dugme("Gorev_" + u.id, gorevDugmeleri, "", Kagit, Murekkep, () => GorevDugmesi(u), 1.6f, 18f, 2.5f);
            ((RectTransform)gd.transform).sizeDelta = new Vector2(186f, 40f);
            var gs = Resim("Simge", gd.transform, PestOSCizim.Ikon(u.id));
            Konum(gs, new Vector2(0f, 0.5f), new Vector2(4f, 1f), new Vector2(36f, 36f), new Vector2(0f, 0.5f));
            var gy = Metin("Yazi", gd.transform, u.ad, 18, Murekkep, TextAlignmentOptions.Left);
            Kenar(gy.rectTransform, Vector2.zero, Vector2.one, new Vector2(44f, 0f), new Vector2(-8f, 0f));
            Kirp(gy);
            u.gorevCizgi = Panel("Gosterge", gd.transform, Murekkep, 4f);
            u.gorevCizgi.GetComponent<Image>().raycastTarget = false;
            Konum(u.gorevCizgi, new Vector2(0.5f, 0f), new Vector2(0f, -7f), new Vector2(30f, 4f), new Vector2(0.5f, 0f));
            u.gorevDugmesi = gd;
        }

        // Kağıdın üstüne renkli başlık şeridi (üst köşeleri yuvarlak), altına mürekkep çizgi
        private RectTransform BaslikSeridi(RectTransform govde, Color renk, float yukseklik, out Image zemin, out Image dolgu)
        {
            var bas = Panel("Baslik", govde, renk, 1.4f);
            Kenar(bas, new Vector2(0f, 1f), Vector2.one, new Vector2(Kontur, -Kontur - yukseklik), new Vector2(-Kontur, -Kontur));
            var alt = Panel("BaslikAlt", bas, renk, 0f);
            alt.GetComponent<Image>().raycastTarget = false;
            Kenar(alt, Vector2.zero, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            alt.SetAsFirstSibling();
            var cizgi = Panel("BaslikCizgi", govde, Murekkep, 0f);
            cizgi.GetComponent<Image>().raycastTarget = false;
            Kenar(cizgi, new Vector2(0f, 1f), Vector2.one, new Vector2(Kontur, -Kontur - yukseklik - 3f), new Vector2(-Kontur, -Kontur - yukseklik));
            zemin = bas.GetComponent<Image>();
            dolgu = alt.GetComponent<Image>();
            return bas;
        }

        private RectTransform YuvarlakDugme(string ad, RectTransform p, Color renk, Action tik, float x)
        {
            var dis = Resim(ad, p, daire);
            dis.color = Murekkep;
            dis.raycastTarget = true;
            Konum(dis, new Vector2(1f, 0.5f), new Vector2(x, 1f), new Vector2(28f, 28f), new Vector2(1f, 0.5f));
            var ic = Resim("Ic", dis.transform, daire);
            Doldur(ic.rectTransform, 2.6f, 2.6f);
            var b = dis.gameObject.AddComponent<Button>();
            b.targetGraphic = ic;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            Renklendir(b, renk);
            b.onClick.AddListener(() => tik());
            return dis.rectTransform;
        }

        private void GorevDugmesi(Uygulama u)
        {
            if (u.pencere == null) return;
            if (!u.pencere.gameObject.activeSelf)
            {
                u.pencere.gameObject.SetActive(true);
                StartCoroutine(Belir(u.pencere));
                Odakla(u);
            }
            else if (aktif == u) Kucult(u);
            else Odakla(u);
        }

        private void Odakla(Uygulama u)
        {
            aktif = u;
            if (u != null && u.pencere != null) u.pencere.SetAsLastSibling();
            foreach (var x in sira) OdakGorunumu(x);
        }

        private void OdakGorunumu(Uygulama x)
        {
            if (x.pencere == null) return;
            bool acik = x.pencere.gameObject.activeSelf;
            bool odak = x == aktif && acik;
            x.baslikZemin.color = x.baslikDolgu.color = odak ? x.renk : KartKoyu;
            x.baslikYazi.color = odak ? Murekkep : Soluk;
            Renklendir(x.gorevDugmesi, odak ? KartSecili : acik ? Kagit : KartKoyu);
            x.gorevCizgi.GetComponent<Image>().color = odak ? Hardal : acik ? Krem : new Color(Krem.r, Krem.g, Krem.b, 0.3f);
            x.gorevCizgi.sizeDelta = new Vector2(odak ? 40f : 14f, 4f);
        }

        private void Kucult(Uygulama u)
        {
            if (u.pencere == null) return;
            u.pencere.gameObject.SetActive(false);
            if (aktif == u) aktif = EnUstteki();
            foreach (var x in sira) OdakGorunumu(x);
        }

        private void PencereKapat(Uygulama u)
        {
            if (u.gorevDugmesi != null) Destroy(u.gorevDugmesi.gameObject);
            if (u.pencere != null) Destroy(u.pencere.gameObject);
            u.pencere = null; u.icerik = null; u.gorevDugmesi = null; u.gorevCizgi = null;
            if (aktif == u) aktif = EnUstteki();
            foreach (var x in sira) OdakGorunumu(x);
        }

        private Uygulama EnUstteki()
        {
            Uygulama en = null;
            int enSira = -1;
            foreach (var x in sira)
            {
                if (x.pencere == null || !x.pencere.gameObject.activeSelf) continue;
                int s = x.pencere.GetSiblingIndex();
                if (s > enSira) { enSira = s; en = x; }
            }
            return en;
        }

        private void BuyutKucult(Uygulama u)
        {
            if (u.pencere == null) return;
            if (!u.buyuk)
            {
                u.eskiPoz = u.pencere.anchoredPosition;
                u.pencere.anchoredPosition = Vector2.zero;
                u.pencere.sizeDelta = pencereKoku.rect.size - new Vector2(16f, 16f); // sert gölge görünsün
            }
            else
            {
                u.pencere.sizeDelta = u.boyut;
                u.pencere.anchoredPosition = u.eskiPoz;
            }
            u.buyuk = !u.buyuk;
            var gren = u.pencere.Find("Govde/Gren")?.GetComponent<RawImage>();
            if (gren != null) gren.uvRect = new Rect(0f, 0f, u.pencere.sizeDelta.x / 128f, u.pencere.sizeDelta.y / 128f);
            Odakla(u);
            YenidenKur(u); // ızgara/sütunlar yeni boyuta göre yeniden dizilsin
        }

        // Çizgi film "pop" açılışı: hafif büyüyüp yerine oturur
        private static IEnumerator Belir(RectTransform r)
        {
            var cg = r.GetComponent<CanvasGroup>();
            const float sure = 0.2f;
            for (float t = 0f; t < sure; t += Time.unscaledDeltaTime)
            {
                if (r == null) yield break;
                float k = t / sure;
                cg.alpha = Mathf.Clamp01(k * 2.5f);
                float s = k < 0.6f ? Mathf.Lerp(0.9f, 1.03f, k / 0.6f) : Mathf.Lerp(1.03f, 1f, (k - 0.6f) / 0.4f);
                r.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (r == null) yield break;
            cg.alpha = 1f;
            r.localScale = Vector3.one;
        }

        private void YenidenKur(Uygulama u)
        {
            if (u.icerik == null) return;
            Temizle(u.icerik);
            u.kur?.Invoke(u.icerik);
            u.yenile?.Invoke();
        }

        private void YenidenKur(string id) { if (uygulamalar.TryGetValue(id, out var u)) YenidenKur(u); }

        // ------------------------------------------------------------------ BİLDİRİM

        private void Bildirim(string baslik, string metin, Color renk, Sprite simge = null)
        {
            var b = Kutu("Bildirim", bildirimKoku, Kagit, Kontur, 1.4f);
            b.GetComponent<Image>().raycastTarget = false;
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 84f; le.minHeight = 84f;
            var cg = b.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.alpha = 0f;
            var serit = Panel("Serit", b, renk, 0f);
            serit.GetComponent<Image>().raycastTarget = false;
            Kenar(serit, Vector2.zero, new Vector2(0f, 1f), new Vector2(Kontur, Kontur), new Vector2(Kontur + 64f, -Kontur));
            var sc = Panel("SeritCizgi", b, Murekkep, 0f);
            Kenar(sc, Vector2.zero, new Vector2(0f, 1f), new Vector2(Kontur + 64f, Kontur), new Vector2(Kontur + 66.5f, -Kontur));
            var ik = Resim("Simge", b, simge != null ? simge : PestOSCizim.Bocek(PestOSCizim.BocekYesil));
            Konum(ik, new Vector2(0f, 0.5f), new Vector2(Kontur + 4f, 0f), new Vector2(58f, 58f), new Vector2(0f, 0.5f));
            var t = Metin("Yazi", b, $"{baslik}\n<size=80%><color={SolukHex}>{metin}</color></size>", 20, Murekkep, TextAlignmentOptions.Left);
            Kenar(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(84f, 8f), new Vector2(-14f, -8f));
            Kirp(t);
            StartCoroutine(BildirimOmru(b, cg));
        }

        private static IEnumerator BildirimOmru(RectTransform r, CanvasGroup cg)
        {
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
            {
                if (r == null) yield break;
                float k = t / 0.2f;
                cg.alpha = k;
                r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-4f, -1f, k));
                yield return null;
            }
            if (r == null) yield break;
            cg.alpha = 1f;
            yield return new WaitForSecondsRealtime(3.4f);
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { if (r == null) yield break; cg.alpha = 1f - t / 0.35f; yield return null; }
            if (r != null) Destroy(r.gameObject);
        }

        // ------------------------------------------------------------------ UYGULAMA: GÖREVLER

        private TMP_Text aktifIsYazi, aktifIsYuzde;
        private RectTransform aktifIsDolgu, gorevDetay;
        private readonly List<Button> ilanKartlari = new List<Button>();

        private void GorevlerKur(RectTransform p)
        {
            ilanKartlari.Clear();
            gorevDetay = null;

            // Aktif iş kartı
            var aktifKart = Kutu("AktifIs", p, Kart, 3f, 1.4f);
            Ust(aktifKart, 0f, 112f);
            var pano = Resim("Pano", aktifKart, PestOSCizim.Ikon("gorev"));
            Konum(pano, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(88f, 88f), new Vector2(0f, 0.5f));
            pano.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 5f);
            aktifIsYazi = Metin("Yazi", aktifKart, "", 17, Murekkep, TextAlignmentOptions.TopLeft, false);
            Kenar(aktifIsYazi.rectTransform, Vector2.zero, Vector2.one, new Vector2(108f, 12f), new Vector2(-300f, -14f));
            var et = Metin("Etiket", aktifKart, "<b>TEMİZLİK</b>", 13, Soluk, TextAlignmentOptions.TopRight);
            et.characterSpacing = 6f;
            Konum(et.rectTransform, Vector2.one, new Vector2(-22f, -14f), new Vector2(260f, 20f), Vector2.one);
            aktifIsYuzde = Metin("Yuzde", aktifKart, "", 38, Murekkep, TextAlignmentOptions.TopRight);
            Konum(aktifIsYuzde.rectTransform, Vector2.one, new Vector2(-22f, -30f), new Vector2(260f, 48f), Vector2.one);
            var bar = Kutu("Bar", aktifKart, KartKoyu, 2.5f, 3f);
            Konum(bar, new Vector2(1f, 0f), new Vector2(-22f, 16f), new Vector2(260f, 18f), new Vector2(1f, 0f));
            var barAlan = Rt("Alan", bar);
            Doldur(barAlan, 4f, 4f);
            aktifIsDolgu = Panel("Dolgu", barAlan, Hardal, 4f);
            aktifIsDolgu.anchorMin = Vector2.zero; aktifIsDolgu.anchorMax = new Vector2(0f, 1f);
            aktifIsDolgu.offsetMin = aktifIsDolgu.offsetMax = Vector2.zero;

            // Sol: ilan listesi
            var sol = Rt("Sol", p);
            Kenar(sol, Vector2.zero, new Vector2(0.56f, 1f), Vector2.zero, new Vector2(-8f, -128f));
            var solBas = Metin("Baslik", sol, "YENİ İLANLAR", 20, Murekkep, TextAlignmentOptions.Left);
            Konum(solBas.rectTransform, new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(260f, 36f), new Vector2(0f, 1f));

            // Sağ: ayrıntı paneli
            var detayKutu = Kutu("Detay", p, KartKoyu, 3f, 1.4f);
            Kenar(detayKutu, new Vector2(0.56f, 0f), Vector2.one, new Vector2(8f, 0f), new Vector2(0f, -128f));
            gorevDetay = Rt("Icerik", detayKutu);
            Doldur(gorevDetay);

            var cg = ContractGenerator.Instance;
            if (cg == null)
            {
                Bos(sol, "İlan sunucusuna ulaşılamıyor", "Sahnede ContractGenerator yok.");
                GorevDetayKur();
                return;
            }
            if (ilanlar.Count == 0) ilanlar = cg.GenerateContracts(4);

            var yenile = Dugme("Yenile", sol, "Yeni ilanlar", Kagit, Murekkep, () =>
            {
                ilanlar = cg.GenerateContracts(4);
                seciliIlan = -1;
                YenidenKur("gorev");
                Bildirim("İlanlar yenilendi", $"{ilanlar.Count} yeni iş ilanı geldi.", Turuncu, PestOSCizim.Ikon("gorev"));
            }, 1.6f, 17f);
            Konum(yenile, Vector2.one, new Vector2(0f, 1f), new Vector2(144f, 36f), Vector2.one);

            var kok = Kaydirma(sol, out var liste, 10f);
            Kenar(kok, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -46f));
            for (int i = 0; i < ilanlar.Count; i++)
            {
                int idx = i;
                var c = ilanlar[i];
                var kart = Dugme("Ilan_" + i, liste, "", i == seciliIlan ? KartSecili : Kart, Murekkep, () => IlanSec(idx), 1.4f, 18f, 3f);
                Yukseklik(kart, 100f);
                ilanKartlari.Add(kart);
                var tur = Resim("Tur", kart.transform, PestOSCizim.Tur(c.pestSpecies));
                Konum(tur, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(78f, 78f), new Vector2(0f, 0.5f));
                tur.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? -6f : 6f));
                var ad = Metin("Ad", kart.transform, c.title, 22, Murekkep, TextAlignmentOptions.TopLeft);
                Kenar(ad.rectTransform, Vector2.zero, Vector2.one, new Vector2(94f, 8f), new Vector2(-128f, -12f));
                Kirp(ad);
                var bilgi = Metin("Bilgi", kart.transform,
                    $"<color={SolukHex}>{c.district} · {c.address}</color>\n{TurAdi(c.pestSpecies)}{ModKisa(c.modifier)}    {Zorluk(c.difficulty)}", 15, Murekkep, TextAlignmentOptions.BottomLeft, false);
                Kenar(bilgi.rectTransform, Vector2.zero, Vector2.one, new Vector2(94f, 12f), new Vector2(-128f, -40f));
                Kirp(bilgi);
                var odul = Metin("Odul", kart.transform, $"${c.baseReward:N0}\n<size=45%><color={SolukHex}>ÖDÜL</color></size>", 32, Yesil, TextAlignmentOptions.Right);
                Kenar(odul.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-128f, 0f), new Vector2(-18f, 0f));
            }
            GorevDetayKur();
        }

        private void IlanSec(int idx)
        {
            var cg = ContractGenerator.Instance;
            if (cg == null || idx < 0 || idx >= ilanlar.Count) return;
            seciliIlan = idx;
            cg.SelectContract(ilanlar[idx]);
            for (int i = 0; i < ilanKartlari.Count; i++)
                if (ilanKartlari[i] != null) Renklendir(ilanKartlari[i], i == idx ? KartSecili : Kart);
            GorevDetayKur();
        }

        private void GorevDetayKur()
        {
            if (gorevDetay == null) return;
            Temizle(gorevDetay);
            if (ContractGenerator.Instance == null || seciliIlan < 0 || seciliIlan >= ilanlar.Count)
            {
                var m = Resim("Maskot", gorevDetay, PestOSCizim.Bocek(PestOSCizim.BocekYesil));
                Konum(m, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(150f, 150f), new Vector2(0.5f, 0.5f));
                var t = Metin("Ipucu", gorevDetay, $"İlan seçilmedi\n<size=72%><color={SolukHex}>Ayrıntılar için soldan bir ilana tıkla.</color></size>", 26, Murekkep, TextAlignmentOptions.Top);
                Kenar(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(24f, -110f), new Vector2(-24f, -20f));
                return;
            }
            var c = ilanlar[seciliIlan];
            var resim = Resim("Tur", gorevDetay, PestOSCizim.Tur(c.pestSpecies));
            Konum(resim, Vector2.one, new Vector2(-14f, -10f), new Vector2(118f, 118f), Vector2.one);
            resim.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var bas = Metin("Baslik", gorevDetay, $"{c.title}\n<size=60%><color={SolukHex}>{c.district}</color></size>", 34, Murekkep, TextAlignmentOptions.TopLeft);
            Kenar(bas.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -104f), new Vector2(-140f, -18f));
            Kirp(bas);
            var ayrac = Panel("Ayrac", gorevDetay, Murekkep, 0f);
            Kenar(ayrac, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -126f), new Vector2(-24f, -123.5f));

            string[,] satirlar =
            {
                { "Adres", c.address },
                { "Haşere", TurAdi(c.pestSpecies) },
                { "Zorluk", Zorluk(c.difficulty) + $"  <color={SolukHex}>{c.difficulty}/5</color>" },
                { "Süre sınırı", Sure(c.requiredTime) },
                { "Koşul", ModUzun(c.modifier) },
            };
            for (int i = 0; i < satirlar.GetLength(0); i++)
            {
                float y = -138f - i * 34f;
                var k = Metin("Anahtar", gorevDetay, satirlar[i, 0], 15, Soluk, TextAlignmentOptions.Left, false);
                Kenar(k.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, y - 30f), new Vector2(150f, y));
                var v = Metin("Deger", gorevDetay, satirlar[i, 1], 17, Murekkep, TextAlignmentOptions.Left, false);
                Kenar(v.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(150f, y - 30f), new Vector2(-24f, y));
                Kirp(v);
            }

            var odul = Kutu("Odul", gorevDetay, new Color(0.8f, 0.9f, 0.64f), 3f, 1.4f);
            Kenar(odul, Vector2.zero, new Vector2(1f, 0f), new Vector2(22f, 90f), new Vector2(-22f, 168f));
            var para = Resim("Para", odul, PestOSCizim.Ikon("banka"));
            Konum(para, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(70f, 70f), new Vector2(1f, 0.5f));
            var ot = Metin("Yazi", odul, $"<size=50%><color={SolukHex}>KONTRAT ÖDÜLÜ</color></size>\n${c.baseReward:N0}", 36, Yesil, TextAlignmentOptions.Left);
            Kenar(ot.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 2f), new Vector2(-90f, -2f));

            var basla = Dugme("Basla", gorevDetay, "İŞE BAŞLA!", Turuncu, Murekkep, IseBasla, 1.4f, 26f, Kontur);
            Kenar((RectTransform)basla.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(22f, 18f), new Vector2(-22f, 76f));
        }

        private void GorevlerYenile()
        {
            if (aktifIsYazi == null) return;
            var sb = new System.Text.StringBuilder();
            var jm = JobManager.Instance;
            float yuzde = 0f;
            if (jm != null)
            {
                yuzde = jm.CleanPercent;
                float sure = jm.ElapsedTime;
                sb.Append($"<b><color={HardalHex}>AKTİF İŞ</color></b>   <color={SolukHex}>Süre</color> {(int)(sure / 60f):00}:{(int)(sure % 60f):00}  ·  <color={SolukHex}>Kalan yuva</color> <b>{PestNest.ActiveNestCount}</b>  ·  <color={SolukHex}>Böcek</color> <b>{jm.PestsKilled}/{jm.TotalPestTarget}</b>\n");
            }
            else sb.Append($"<b><color={HardalHex}>AKTİF İŞ</color></b>   <color={SolukHex}>Şu an aktif bir iş yok. Bir ilan seçip işe başla.</color>\n");

            var om = OrderManager.Instance;
            if (om != null && om.ActiveOrder != null)
            {
                var o = om.ActiveOrder;
                string durum = om.IsCompleted ? $"<color={YesilHex}>TESLİM EDİLDİ</color>" : $"<color={KirmiziHex}>BEKLİYOR</color>";
                sb.Append($"<size=92%><b>Sipariş:</b> {o.title}  ·  ${o.reward}  ·  <b>{durum}</b></size>\n<size=82%><color={SolukHex}>{o.description}</color></size>");
            }
            else
            {
                var cg = ContractGenerator.Instance;
                if (cg != null && cg.CurrentContract != null)
                    sb.Append($"<size=90%><color={SolukHex}>Seçili kontrat:</color> <b>{cg.CurrentContract.title}</b> <color={SolukHex}>({cg.CurrentContract.district})</color></size>");
            }
            aktifIsYazi.text = sb.ToString();
            aktifIsYuzde.text = jm != null ? $"%{yuzde * 100f:0}" : $"<color={SolukHex}>—</color>";
            aktifIsDolgu.anchorMax = new Vector2(jm != null ? Mathf.Clamp01(yuzde) : 0f, 1f);
        }

        private void IseBasla()
        {
            if (seciliIlan < 0) return;
            var sl = SceneLoader.Instance;
            if (sl == null) { Debug.LogError("PestPC: Sahnede SceneLoader yok!", this); Bildirim("Başlatılamadı", "Sahne yükleyici bulunamadı.", Kirmizi); return; }
            Close();
            sl.LoadJobScene(isSahnesi);
        }

        private static string TurAdi(string tur) => tur switch
        {
            "Cockroach" => "Hamam böceği",
            "Mouse" => "Fare",
            "Unknown" => $"<color={MorHex}><b>Bilinmeyen tür</b></color>",
            _ => tur
        };

        private static string ModKisa(string m) => m switch
        {
            "NightShift" => $"  <color={MorHex}>· Gece</color>",
            "OwnerHome" => $"  <color={KirmiziHex}>· Ev sahibi evde</color>",
            _ => ""
        };

        private static string ModUzun(string m) => m switch
        {
            "NightShift" => $"<color={MorHex}>Gece vardiyası</color> — ışıklar kapalı",
            "OwnerHome" => $"<color={KirmiziHex}>Ev sahibi evde</color> — sessiz çalış",
            _ => $"<color={SolukHex}>Standart iş</color>"
        };

        private static string Zorluk(int z)
        {
            z = Mathf.Clamp(z, 0, 5);
            return $"<color={KirmiziHex}>{new string('●', z)}</color><color=#C9B99A>{new string('●', 5 - z)}</color>";
        }

        private static string Sure(float sn)
        {
            int dk = Mathf.Max(0, Mathf.RoundToInt(sn / 60f));
            return dk > 0 ? $"{dk} dakika" : "Süresiz";
        }

        // ------------------------------------------------------------------ UYGULAMA: MARKET

        private TMP_Text marketBakiye;
        private static readonly Color[] AracRenkleri = { new Color(0.98f, 0.8f, 0.5f), new Color(0.7f, 0.88f, 0.84f), new Color(0.78f, 0.82f, 0.96f), new Color(0.96f, 0.72f, 0.66f), new Color(0.8f, 0.9f, 0.64f) };

        private void MarketKur(RectTransform p)
        {
            marketBakiye = null;
            var tm = ToolManager.Instance;
            if (tm == null || tm.AllTools == null)
            {
                Bos(p, "Mağaza çevrimdışı", "Sahnede ToolManager bulunamadı.");
                return;
            }
            var ust = Metin("Bilgi", p, $"Saha Ekipmanı\n<size=62%><color={SolukHex}>Satın aldığın ekipman anında kamyona teslim edilir.</color></size>", 28, Murekkep, TextAlignmentOptions.TopLeft);
            Kenar(ust.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -64f), new Vector2(-270f, 0f));
            var bakiye = Kutu("Bakiye", p, Kagit, 3f, 1.6f);
            Konum(bakiye, Vector2.one, new Vector2(0f, -2f), new Vector2(250f, 56f), Vector2.one);
            var para = Resim("Para", bakiye, PestOSCizim.Ikon("banka"));
            Konum(para, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(52f, 52f), new Vector2(0f, 0.5f));
            marketBakiye = Metin("Yazi", bakiye, "", 26, Yesil, TextAlignmentOptions.Right);
            Kenar(marketBakiye.rectTransform, Vector2.zero, Vector2.one, new Vector2(60f, 0f), new Vector2(-16f, 0f));

            var araclar = new List<ToolData>();
            int sahipSay = 0;
            foreach (var t in tm.AllTools)
            {
                if (t == null) continue;
                araclar.Add(t);
                if (tm.IsToolOwned(t.toolId)) sahipSay++;
            }
            string[] sekmeler = { $"Tümü ({araclar.Count})", $"Mağaza ({araclar.Count - sahipSay})", $"Sahip olunan ({sahipSay})" };
            for (int k = 0; k < sekmeler.Length; k++)
            {
                int kk = k;
                bool secili = k == marketFiltre;
                var s = Dugme("Sekme" + k, p, sekmeler[k], secili ? KartSecili : Kart, secili ? Murekkep : Soluk, () => { marketFiltre = kk; YenidenKur("market"); }, 1.6f, 17f, 2.5f);
                Konum(s, new Vector2(0f, 1f), new Vector2(k * 186f, -76f), new Vector2(176f, 38f), new Vector2(0f, 1f));
            }

            float genislik = p.rect.width > 10f ? p.rect.width : 880f;
            int sutun = Mathf.Max(1, Mathf.FloorToInt((genislik + 16f) / (266f + 16f)));
            float hucre = (genislik - 18f - (sutun - 1) * 16f) / sutun;
            var kok = Kaydirma(p, out var icerik, 16f, new Vector2(hucre, 250f), sutun);
            Kenar(kok, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -130f));

            float bakiyeDeger = Para();
            int gosterilen = 0;
            for (int i = 0; i < araclar.Count; i++)
            {
                var t = araclar[i];
                bool sahip = tm.IsToolOwned(t.toolId);
                if ((marketFiltre == 1 && sahip) || (marketFiltre == 2 && !sahip)) continue;
                gosterilen++;
                var kart = Kutu("Urun_" + t.toolId, icerik, Kart, 3f, 1.4f);

                // Yuvarlak "çıkartma" içinde aletin resmi
                var cikartma = Resim("Cikartma", kart, daire);
                cikartma.color = Murekkep;
                Konum(cikartma, new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(78f, 78f), new Vector2(0f, 1f));
                var ci = Resim("Ic", cikartma.transform, daire);
                ci.color = AracRenkleri[i % AracRenkleri.Length];
                Doldur(ci.rectTransform, 3f, 3f);
                var resim = Resim("Ikon", cikartma.transform, t.icon != null ? t.icon : PestOSCizim.Ikon("market"));
                resim.preserveAspect = true;
                Doldur(resim.rectTransform, 9f, 9f);

                var ad = Metin("Ad", kart, $"{t.toolName}\n<size=62%><color={SolukHex}>Seviye {t.unlockLevel}+</color></size>", 23, Murekkep, TextAlignmentOptions.Left);
                Kenar(ad.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(100f, -86f), new Vector2(-12f, -14f));
                Kirp(ad);
                var aciklama = Metin("Aciklama", kart, string.IsNullOrEmpty(t.description) ? "PestCo onaylı saha ekipmanı." : t.description, 15, Soluk, TextAlignmentOptions.TopLeft, false);
                Kenar(aciklama.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 68f), new Vector2(-16f, -100f));
                Kirp(aciklama);

                var fiyat = Metin("Fiyat", kart, t.price <= 0 ? "Ücretsiz" : $"${t.price:N0}", 28, Murekkep, TextAlignmentOptions.Left);
                Kenar(fiyat.rectTransform, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(16f, 16f), new Vector2(0f, 58f));
                if (sahip)
                {
                    var rozet = Kutu("Sahip", kart, new Color(0.8f, 0.92f, 0.66f), 2.5f, 1.6f);
                    Konum(rozet, new Vector2(1f, 0f), new Vector2(-14f, 16f), new Vector2(124f, 40f), new Vector2(1f, 0f));
                    rozet.localRotation = Quaternion.Euler(0f, 0f, -4f);
                    var rt = Metin("Yazi", rozet, "SAHİPSİN", 18, Yesil, TextAlignmentOptions.Center);
                    Doldur(rt.rectTransform);
                }
                else
                {
                    var tool = t;
                    bool yeter = bakiyeDeger >= t.price;
                    var al = Dugme("Al", kart, yeter ? "Satın Al" : "Yetersiz", yeter ? YesilAcik : KartKoyu, yeter ? Murekkep : Soluk, () => SatinAl(tool), 1.6f, 19f, 3f);
                    al.interactable = yeter;
                    Konum(al, new Vector2(1f, 0f), new Vector2(-14f, 16f), new Vector2(124f, 42f), new Vector2(1f, 0f));
                }
            }
            if (gosterilen == 0)
            {
                var bos = Metin("Bos", kok, $"<color={SolukHex}>Bu sekmede ürün yok.</color>", 20, Murekkep, TextAlignmentOptions.Center);
                Doldur(bos.rectTransform);
            }
        }

        private void SatinAl(ToolData tool)
        {
            var tm = ToolManager.Instance;
            if (tm == null) return;
            DisHareketleriYakala();
            float once = Para();
            bool ok = tm.PurchaseTool(tool.toolId);
            if (ok)
            {
                HareketEkle($"{tool.toolName} satın alındı", Para() - once);
                Bildirim("Satın alma tamam!", $"{tool.toolName} kamyona teslim edildi.", YesilAcik, tool.icon != null ? tool.icon : PestOSCizim.Ikon("market"));
            }
            else Bildirim("İşlem reddedildi", $"{tool.toolName} için bakiye yetersiz.", Kirmizi, PestOSCizim.Bocek(Kirmizi, true));
            YenidenKur("market");
            YenidenKur("banka");
        }

        private void MarketYenile()
        {
            if (marketBakiye != null) marketBakiye.text = $"${Para():N0}";
        }

        // ------------------------------------------------------------------ UYGULAMA: FANUS

        private void FanusKur(RectTransform p)
        {
            var raf = Raf();
            if (raf == null)
            {
                Bos(p, "Fanus rafına bağlanılamadı", "Kamyonda BugShelf bulunamadı.");
                return;
            }
            var bolmeler = raf.GetComponentsInChildren<BugCage>();
            int dolu = 0, numune = 0;
            float toplam = 0f;
            foreach (var b in bolmeler)
            {
                if (b.Count > 0) dolu++;
                numune += b.Count;
                toplam += b.TotalValue();
            }
            Kutucuk(p, 0, 3, "DOLU BÖLME", $"{dolu}<size=55%><color={SolukHex}> / {bolmeler.Length}</color></size>", PestOSCizim.Ikon("fanus"));
            Kutucuk(p, 1, 3, "NUMUNE", numune.ToString(), PestOSCizim.Bocek(PestOSCizim.BocekYesil));
            Kutucuk(p, 2, 3, "TOPLAM DEĞER", $"<color={YesilHex}>${toplam:N0}</color>", PestOSCizim.Ikon("banka"));

            // Tablo başlığı
            var baslik = Rt("TabloBaslik", p);
            Ust(baslik, 112f, 28f);
            SutunYazi(baslik, "BÖLME", 0f, 0.15f, TextAlignmentOptions.Left, 13, Soluk);
            SutunYazi(baslik, "TÜR", 0.15f, 0.5f, TextAlignmentOptions.Left, 13, Soluk);
            SutunYazi(baslik, "DOLULUK", 0.5f, 0.8f, TextAlignmentOptions.Left, 13, Soluk);
            SutunYazi(baslik, "DEĞER", 0.8f, 1f, TextAlignmentOptions.Right, 13, Soluk);

            var tablo = Kutu("Tablo", p, KartKoyu, 3f, 1.4f);
            Kenar(tablo, Vector2.zero, Vector2.one, new Vector2(0f, 76f), new Vector2(0f, -142f));
            var kok = Kaydirma(tablo, out var liste, 3f);
            Kenar(kok, Vector2.zero, Vector2.one, new Vector2(8f, 6f), new Vector2(-6f, -8f));
            int satir = 0;
            foreach (var b in bolmeler)
            {
                float deger = b.TotalValue();
                var s = Panel("Bolme", liste, satir % 2 == 0 ? Kagit : Kart, 2.2f);
                Yukseklik(s, 50f);
                string ad = b.name.Replace("Fanus_", "").Replace("Bolme_", "").Replace("_", "-");
                SutunYazi(s, ad, 0f, 0.15f, TextAlignmentOptions.Left, 20, Murekkep);

                var turAlan = Rt("TurAlan", s);
                Kenar(turAlan, new Vector2(0.15f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
                if (b.BugType != null)
                {
                    var bocek = Resim("Bocek", turAlan, PestOSCizim.Bocek(b.BugType.cuteColor));
                    Konum(bocek, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(44f, 44f), new Vector2(0f, 0.5f));
                }
                var tt = Metin("Tur", turAlan, b.BugType != null ? b.BugType.bugName : $"<color={SolukHex}>Boş bölme</color>", 17, Murekkep, TextAlignmentOptions.Left, false);
                Kenar(tt.rectTransform, Vector2.zero, Vector2.one, new Vector2(b.BugType != null ? 54f : 14f, 0f), new Vector2(-8f, 0f));
                Kirp(tt);

                var dolAlan = Rt("DolulukAlan", s);
                Kenar(dolAlan, new Vector2(0.5f, 0f), new Vector2(0.8f, 1f), Vector2.zero, Vector2.zero);
                float oran = b.Capacity > 0 ? Mathf.Clamp01(b.Count / (float)b.Capacity) : 0f;
                var bar = Kutu("Bar", dolAlan, Kart, 2.2f, 3f);
                Kenar(bar, new Vector2(0f, 0.5f), new Vector2(0.6f, 0.5f), new Vector2(0f, -8f), new Vector2(0f, 8f));
                var barAlan = Rt("Alan", bar);
                Doldur(barAlan, 3.5f, 3.5f);
                var dolgu = Panel("Dolgu", barAlan, oran >= 1f ? Kirmizi : Turkuaz, 4f);
                dolgu.anchorMin = Vector2.zero; dolgu.anchorMax = new Vector2(oran, 1f);
                dolgu.offsetMin = dolgu.offsetMax = Vector2.zero;
                dolgu.gameObject.SetActive(oran > 0f);
                var dt = Metin("Sayi", dolAlan, b.Count > 0 ? $"{b.Count}/{b.Capacity}" : $"<color={SolukHex}>0/{b.Capacity}</color>", 16, Murekkep, TextAlignmentOptions.Left, false);
                Kenar(dt.rectTransform, new Vector2(0.64f, 0f), Vector2.one, Vector2.zero, Vector2.zero);

                SutunYazi(s, deger > 0 ? $"${deger:N0}" : $"<color={SolukHex}>—</color>", 0.8f, 1f, TextAlignmentOptions.Right, 20, Yesil);
                satir++;
            }
            if (bolmeler.Length == 0)
            {
                var bos = Metin("Bos", kok, $"<color={SolukHex}>Rafta bölme yok.</color>", 20, Murekkep, TextAlignmentOptions.Center);
                Doldur(bos.rectTransform);
            }

            var mesaj = Metin("Mesaj", p, string.IsNullOrEmpty(fanusMesaj) ? $"<color={SolukHex}>Satış tüm bölmeleri boşaltır, tutar hesabına yatar.</color>" : fanusMesaj, 16, Murekkep, TextAlignmentOptions.Left, false);
            Kenar(mesaj.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(-270f, 60f));
            bool satilabilir = toplam > 0f;
            var sat = Dugme("Sat", p, satilabilir ? $"HEPSİNİ SAT  ${toplam:N0}" : "Satılacak numune yok", satilabilir ? Hardal : KartKoyu, satilabilir ? Murekkep : Soluk, () => FanusSat(raf), 1.4f, 20f, Kontur);
            sat.interactable = satilabilir;
            Konum(sat, new Vector2(1f, 0f), new Vector2(-2f, 2f), new Vector2(256f, 56f), new Vector2(1f, 0f));
        }

        private void FanusSat(BugShelf raf)
        {
            if (raf == null) return;
            DisHareketleriYakala();
            float once = Para();
            raf.SellAll();
            float kazanc = Para() - once;
            HareketEkle("Numune satışı (fanus)", kazanc);
            fanusMesaj = $"<color={YesilHex}><b>Numuneler satıldı: +${kazanc:N0}</b></color>";
            Bildirim("Numuneler satıldı!", $"+${kazanc:N0} hesabına geçti.", Turkuaz, PestOSCizim.Ikon("fanus"));
            YenidenKur("fanus");
            YenidenKur("banka");
        }

        private BugShelf Raf()
        {
            if (rafOnbellek == null) rafOnbellek = FindFirstObjectByType<BugShelf>();
            return rafOnbellek;
        }

        // ------------------------------------------------------------------ UYGULAMA: BANKA

        private TMP_Text bankaBakiye;
        private readonly List<TMP_Text> bankaDegerler = new List<TMP_Text>();

        private void BankaKur(RectTransform p)
        {
            bankaBakiye = null;
            bankaDegerler.Clear();
            if (GameManager.Instance == null)
            {
                Bos(p, "Hesaba ulaşılamıyor", "Sahnede GameManager yok.");
                return;
            }
            // Hardal "hesap cüzdanı" kartı
            var kart = Kutu("Kart", p, new Color(0.98f, 0.8f, 0.42f), Kontur, 1.2f);
            Ust(kart, 0f, 156f);
            var paralar = Resim("Paralar", kart, PestOSCizim.Ikon("banka"));
            Konum(paralar, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(136f, 136f), new Vector2(1f, 0.5f));
            paralar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var ke = Metin("Etiket", kart, "<b>PESTBANK · VADESİZ HESAP</b>", 13, new Color(0.35f, 0.24f, 0.12f), TextAlignmentOptions.TopLeft, false);
            ke.characterSpacing = 5f;
            Konum(ke.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -18f), new Vector2(500f, 22f), new Vector2(0f, 1f));
            bankaBakiye = Metin("Bakiye", kart, "", 62, Murekkep, TextAlignmentOptions.Left);
            Kenar(bankaBakiye.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 36f), new Vector2(-170f, -40f));
            var kn = Metin("No", kart, "**** **** 2481   ·   Saha Ekibi", 16, new Color(0.35f, 0.24f, 0.12f), TextAlignmentOptions.BottomLeft, false);
            Kenar(kn.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(26f, 16f), new Vector2(-170f, 42f));

            var izgara = Rt("Istatistik", p);
            Kenar(izgara, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -172f - 2f * 80f - 12f), new Vector2(0f, -172f));
            var g = izgara.gameObject.AddComponent<GridLayoutGroup>();
            float gen = p.rect.width > 10f ? p.rect.width : 760f;
            g.cellSize = new Vector2((gen - 2f * 12f) / 3f, 80f);
            g.spacing = new Vector2(12f, 12f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 3;
            string[] etiketler = { "TAMAMLANAN İŞ", "ÖLDÜRÜLEN BÖCEK", "İTİBAR", "BULUNAN ANOMALİ", "HATALI TEŞHİS", "SAHİP OLUNAN ALET" };
            foreach (var e in etiketler)
            {
                var kutu = Kutu("Kutu", izgara, Kart, 3f, 1.4f);
                var et = Metin("Etiket", kutu, "<b>" + e + "</b>", 12, Soluk, TextAlignmentOptions.TopLeft, false);
                et.characterSpacing = 4f;
                Kenar(et.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 10f), new Vector2(-12f, -14f));
                var d = Metin("Deger", kutu, "0", 34, Murekkep, TextAlignmentOptions.BottomLeft);
                Kenar(d.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 6f), new Vector2(-12f, -26f));
                bankaDegerler.Add(d);
            }

            var hb = Metin("HareketBaslik", p, "Son Hareketler", 22, Murekkep, TextAlignmentOptions.Left);
            Ust(hb.rectTransform, 352f, 32f);
            var tablo = Kutu("Tablo", p, KartKoyu, 3f, 1.4f);
            Kenar(tablo, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -390f));
            var kok = Kaydirma(tablo, out var liste, 4f);
            Kenar(kok, Vector2.zero, Vector2.one, new Vector2(8f, 6f), new Vector2(-6f, -8f));
            if (hareketler.Count == 0)
            {
                var bos = Metin("Bos", kok, $"<color={SolukHex}>Henüz hesap hareketi yok.\n<size=80%>Market alışverişleri ve fanus satışları burada görünür.</size></color>", 18, Murekkep, TextAlignmentOptions.Center);
                Doldur(bos.rectTransform);
            }
            int sira2 = 0;
            foreach (var h in hareketler)
            {
                var s = Panel("Hareket", liste, sira2++ % 2 == 0 ? Kagit : Kart, 2.2f);
                Yukseklik(s, 52f);
                bool gelir = h.miktar >= 0f;
                var ok = Resim("Isaret", s, daire);
                ok.color = Murekkep;
                Konum(ok, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(32f, 32f), new Vector2(0f, 0.5f));
                var oi = Resim("Ic", ok.transform, daire);
                oi.color = gelir ? YesilAcik : new Color(0.96f, 0.62f, 0.55f);
                Doldur(oi.rectTransform, 2.5f, 2.5f);
                var oy = Metin("Yon", ok.transform, gelir ? "+" : "-", 22, Murekkep, TextAlignmentOptions.Center);
                Doldur(oy.rectTransform);
                var ad = Metin("Ad", s, $"{h.ad}\n<size=78%><color={SolukHex}>{h.zaman:dd.MM · HH:mm}</color></size>", 16, Murekkep, TextAlignmentOptions.Left, false);
                Kenar(ad.rectTransform, Vector2.zero, Vector2.one, new Vector2(56f, 0f), new Vector2(-150f, 0f));
                Kirp(ad);
                var m = Metin("Miktar", s, $"{(gelir ? "+" : "-")}${Mathf.Abs(h.miktar):N0}", 22, gelir ? Yesil : Kirmizi, TextAlignmentOptions.Right);
                Kenar(m.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-150f, 0f), new Vector2(-16f, 0f));
            }
        }

        private void BankaYenile()
        {
            var gm = GameManager.Instance;
            if (bankaBakiye == null || gm == null) return;
            var d = gm.Data;
            bankaBakiye.text = $"${d.money:N0}";
            int sahip = 0;
            var tm = ToolManager.Instance;
            if (tm != null && tm.AllTools != null)
                foreach (var t in tm.AllTools) if (t != null && tm.IsToolOwned(t.toolId)) sahip++;
            string[] degerler =
            {
                d.completedJobs.ToString(), d.totalBugsKilled.ToString("N0"), d.reputation.ToString(),
                d.anomaliesFound.ToString(), d.wrongDiagnoses > 0 ? $"<color={KirmiziHex}>{d.wrongDiagnoses}</color>" : "0", sahip.ToString()
            };
            for (int i = 0; i < bankaDegerler.Count && i < degerler.Length; i++)
                if (bankaDegerler[i] != null) bankaDegerler[i].text = degerler[i];
        }

        private void HareketEkle(string ad, float miktar)
        {
            if (Mathf.Abs(miktar) >= 0.5f)
            {
                hareketler.Insert(0, new Hareket { zaman = DateTime.Now, ad = ad, miktar = miktar });
                if (hareketler.Count > 60) hareketler.RemoveAt(hareketler.Count - 1);
            }
            sonPara = Para();
        }

        // PC kapalıyken (iş ödülü, yemek satışı...) değişen parayı tek hareket olarak kaydet
        private void DisHareketleriYakala()
        {
            if (GameManager.Instance == null) return;
            float p = Para();
            if (!float.IsNaN(sonPara) && Mathf.Abs(p - sonPara) >= 0.5f)
                hareketler.Insert(0, new Hareket { zaman = DateTime.Now, ad = p > sonPara ? "Saha geliri" : "Saha gideri", miktar = p - sonPara });
            sonPara = p;
        }

        // ------------------------------------------------------------------ UYGULAMA: NOTLAR

        private const string VardiyaNotlari =
            "VARDİYA NOTLARI\n" +
            "- B-07 bölmesindeki numune yine kapağı yokladı.\n  Kilit sağlam ama sesini gece duyuyorum.\n\n" +
            "- Fanuslar 24.6 derecede kalmalı. Isı düşünce\n  hareketleniyorlar.\n\n" +
            "- Arka kapıyı İÇERİDEN kilitle. Dışarıdan\n  biri kepengi yoklamış, sabah lameller eğikti.\n\n" +
            "- Terminal kendi kendine açılırsa ekrana\n  BAKMA. Sadece kapat.\n\n" +
            "                                        — M.";

        private void NotlarKur(RectTransform p)
        {
            var yan = Kutu("Dosyalar", p, KartKoyu, 3f, 1.4f);
            Kenar(yan, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(214f, 0f));
            var yb = Metin("Baslik", yan, "<b>DOSYALAR</b>", 12, Soluk, TextAlignmentOptions.Left, false);
            yb.characterSpacing = 6f;
            Kenar(yb.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -40f), new Vector2(-12f, -12f));
            string[] dosyalar = { "vardiya_notlari.txt", "notlarim.txt" };
            string[] altlar = { "salt okunur · M.", "senin notların" };
            for (int i = 0; i < dosyalar.Length; i++)
            {
                int ii = i;
                bool secili = i == notSekme;
                var d = Dugme("Dosya" + i, yan, "", secili ? KartSecili : HayaletKoyu, Murekkep, () => { notSekme = ii; YenidenKur("not"); }, 1.6f, 18f, 2.5f);
                Kenar((RectTransform)d.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -50f - (i + 1) * 62f), new Vector2(-10f, -50f - i * 62f - 4f));
                var ds = Resim("Simge", d.transform, PestOSCizim.Ikon("not"));
                Konum(ds, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(40f, 40f), new Vector2(0f, 0.5f));
                var dt = Metin("Yazi", d.transform, $"{dosyalar[i]}\n<size=80%><color={SolukHex}>{altlar[i]}</color></size>", 15, secili ? Murekkep : Soluk, TextAlignmentOptions.Left, false);
                Kenar(dt.rectTransform, Vector2.zero, Vector2.one, new Vector2(48f, 0f), new Vector2(-6f, 0f));
                Kirp(dt);
            }

            var alan = Rt("Alan", p);
            Kenar(alan, Vector2.zero, Vector2.one, new Vector2(228f, 0f), Vector2.zero);
            var kagitRenk = new Color(0.99f, 0.97f, 0.89f);
            if (notSekme == 0)
            {
                var kagit = Kutu("Kagit", alan, kagitRenk, 3f, 1.6f);
                Doldur(kagit);
                KenarCizgisi(kagit);
                var t = Metin("Yazi", kagit, VardiyaNotlari, 21, Murekkep, TextAlignmentOptions.TopLeft);
                t.lineSpacing = 6f;
                Kenar(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(66f, 16f), new Vector2(-18f, -18f));
                return;
            }

            var not = Giris("Not", alan, "Buraya yaz... (otomatik kaydedilir)", 21, true, kagitRenk, Murekkep, 66f);
            Kenar((RectTransform)not.transform, Vector2.zero, Vector2.one, new Vector2(0f, 32f), Vector2.zero);
            KenarCizgisi((RectTransform)not.transform);
            not.text = PlayerPrefs.GetString(NotAnahtar, "");
            var durum = Metin("Durum", alan, "", 14, Soluk, TextAlignmentOptions.Left, false);
            Kenar(durum.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(6f, 0f), new Vector2(0f, 26f));
            Action<string> yaz = s => durum.text = $"Otomatik kaydedilir   ·   {s.Length} karakter";
            yaz(not.text);
            not.onValueChanged.AddListener(s => { PlayerPrefs.SetString(NotAnahtar, s); yaz(s); });
        }

        // Defter kağıdının soldaki kırmızı kenar çizgisi
        private void KenarCizgisi(RectTransform kagit)
        {
            var c = Panel("KenarCizgisi", kagit, new Color(0.86f, 0.35f, 0.35f, 0.7f), 0f);
            c.GetComponent<Image>().raycastTarget = false;
            Kenar(c, Vector2.zero, new Vector2(0f, 1f), new Vector2(50f, 3f), new Vector2(52f, -3f));
            c.SetSiblingIndex(1); // kağıt dolgusunun hemen üstünde, yazının altında
        }

        // ------------------------------------------------------------------ UYGULAMA: AYARLAR

        private void AyarlarKur(RectTransform p)
        {
            var b1 = Metin("DuvarBaslik", p, "Duvar Kağıdı", 22, Murekkep, TextAlignmentOptions.Left);
            Ust(b1.rectTransform, 0f, 30f);
            var manzaralar = PestOSCizim.Manzaralar;
            for (int i = 0; i < manzaralar.Length; i++)
            {
                int ii = i;
                bool secili = i == temaIndeks;
                var b = Dugme("Tema" + i, p, "", Color.white, Murekkep, () =>
                {
                    temaIndeks = ii;
                    PlayerPrefs.SetInt(TemaAnahtar, ii);
                    TemaUygula();
                    YenidenKur("ayar");
                }, 1.4f, 18f, secili ? 5f : 3f);
                b.GetComponent<Image>().color = secili ? Hardal : Murekkep;
                Konum(b, new Vector2(0f, 1f), new Vector2(i * 168f, -40f), new Vector2(156f, 92f), new Vector2(0f, 1f));
                b.transform.localRotation = Quaternion.Euler(0f, 0f, secili ? -2f : 0f);
                var onizleme = Resim("Onizleme", b.transform, PestOSCizim.DuvarOnizleme(i));
                Doldur(onizleme.rectTransform, secili ? 5f : 3f, secili ? 5f : 3f);
                var ad = Metin("Ad", p, manzaralar[i].ad, 17, secili ? Murekkep : Soluk, TextAlignmentOptions.Center);
                Konum(ad.rectTransform, new Vector2(0f, 1f), new Vector2(i * 168f, -138f), new Vector2(156f, 24f), new Vector2(0f, 1f));
            }

            var b2 = Metin("OlcekBaslik", p, "Arayüz Boyutu", 22, Murekkep, TextAlignmentOptions.Left);
            Ust(b2.rectTransform, 182f, 30f);
            for (int i = 0; i < OlcekAdlari.Length; i++)
            {
                int ii = i;
                bool secili = i == olcekIndeks;
                var b = Dugme("Olcek" + i, p, OlcekAdlari[i], secili ? KartSecili : Kart, secili ? Murekkep : Soluk, () =>
                {
                    olcekIndeks = ii;
                    PlayerPrefs.SetInt(OlcekAnahtar, ii);
                    OlcekUygula();
                    foreach (var u in sira) if (u.buyuk && u.pencere != null) { u.buyuk = false; BuyutKucult(u); }
                    YenidenKur("ayar");
                }, 1.6f, 19f, 2.5f);
                Konum(b, new Vector2(0f, 1f), new Vector2(i * 160f, -218f), new Vector2(150f, 42f), new Vector2(0f, 1f));
            }

            var kart = Kutu("Hakkinda", p, Kart, 3f, 1.4f);
            Kenar(kart, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -282f));
            var maskot = Resim("Maskot", kart, PestOSCizim.Bocek(PestOSCizim.BocekYesil));
            Konum(maskot, new Vector2(1f, 1f), new Vector2(-10f, -6f), new Vector2(120f, 120f), new Vector2(1f, 1f));
            maskot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            var bas = Metin("Baslik", kart, $"PestOS 3.2\n<size=58%><color={SolukHex}>PestCo Mobil Biyolojik Araştırma Birimi</color></size>", 30, Murekkep, TextAlignmentOptions.TopLeft);
            Kenar(bas.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -80f), new Vector2(-140f, -14f));
            var t = Metin("Yazi", kart,
                "<b>Kısayollar</b>\n" +
                $"<color={SolukHex}>Başlığı sürükle</color><pos=46%>Pencereyi taşı\n" +
                $"<color={SolukHex}>Başlığa çift tıkla</color><pos=46%>Büyüt / eski boyut\n" +
                $"<color={SolukHex}>Görev çubuğu düğmesi</color><pos=46%>Öne getir / küçült\n" +
                $"<color={SolukHex}>Başlat'ta yaz + Enter</color><pos=46%>Uygulama aç\n" +
                $"<color={SolukHex}>Esc</color><pos=46%>Menüyü / bilgisayarı kapat",
                16, Murekkep, TextAlignmentOptions.TopLeft, false);
            t.lineSpacing = 8f;
            Kenar(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 14f), new Vector2(-20f, -92f));
        }

        // ------------------------------------------------------------------ YARDIMCILAR

        private static float Para() => GameManager.Instance != null ? GameManager.Instance.Data.money : 0f;

        // Boş/hata durumu: üzgün böcek + açıklama
        private void Bos(RectTransform p, string baslik, string alt)
        {
            var m = Resim("Maskot", p, PestOSCizim.Bocek(new Color(0.62f, 0.7f, 0.55f), true));
            Konum(m, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(140f, 140f), new Vector2(0.5f, 0.5f));
            var t = Metin("Bos", p, $"<color={KirmiziHex}>{baslik}</color>\n<size=68%><color={SolukHex}>{alt}</color></size>", 26, Murekkep, TextAlignmentOptions.Top);
            Kenar(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(10f, -90f), new Vector2(-10f, -14f));
        }

        // Üstte, n sütundan i'incisi: küçük çizim + etiket + büyük değer
        private void Kutucuk(RectTransform p, int i, int n, string etiket, string deger, Sprite simge)
        {
            var k = Kutu("Kutu_" + i, p, Kart, 3f, 1.4f);
            Kenar(k, new Vector2(i / (float)n, 1f), new Vector2((i + 1) / (float)n, 1f),
                new Vector2(i == 0 ? 0f : 7f, -96f), new Vector2(i == n - 1 ? 0f : -7f, 0f));
            var s = Resim("Simge", k, simge);
            Konum(s, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(70f, 70f), new Vector2(1f, 0.5f));
            s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);
            var e = Metin("Etiket", k, "<b>" + etiket + "</b>", 12, Soluk, TextAlignmentOptions.TopLeft, false);
            e.characterSpacing = 4f;
            Kenar(e.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 10f), new Vector2(-80f, -14f));
            var d = Metin("Deger", k, deger, 38, Murekkep, TextAlignmentOptions.BottomLeft);
            Kenar(d.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 6f), new Vector2(-80f, -30f));
        }

        private static void SutunYazi(RectTransform p, string yazi, float x0, float x1, TextAlignmentOptions hiza, float punto, Color renk)
        {
            var t = Metin("Sutun", p, punto <= 13f ? "<b>" + yazi + "</b>" : yazi, punto, renk, hiza);
            if (punto <= 13f) t.characterSpacing = 4f;
            Kenar(t.rectTransform, new Vector2(x0, 0f), new Vector2(x1, 1f), new Vector2(14f, 0f), new Vector2(-14f, 0f));
            Kirp(t);
        }

        private RectTransform Panel(string ad, Transform p, Color renk, float kose)
        {
            var go = new GameObject(ad, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(p, false);
            var img = go.GetComponent<Image>();
            img.color = renk;
            if (kose > 0f) { img.sprite = yuvarlak; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = kose; }
            return (RectTransform)go.transform;
        }

        // Mürekkep konturlu kutu: dışı mürekkep, içi dolgu. Eklenen çocuklar dolgunun üstüne çizilir.
        private RectTransform Kutu(string ad, Transform p, Color dolgu, float kalinlik = Kontur, float kose = 1.4f, Color? murekkep = null)
        {
            var dis = Panel(ad, p, murekkep ?? Murekkep, kose);
            var ic = Panel("Ic", dis, dolgu, kose);
            ic.GetComponent<Image>().raycastTarget = false;
            Doldur(ic, kalinlik, kalinlik);
            return dis;
        }

        // Çizgi film sert gölgesi: aynı şekil, sağ-alta kaydırılmış koyu mor
        private void SertGolge(RectTransform p, float kayma)
        {
            var g = Panel("Golge", p, new Color(0.05f, 0.02f, 0.05f, 0.6f), 1.4f);
            g.GetComponent<Image>().raycastTarget = false;
            Kenar(g, Vector2.zero, Vector2.one, new Vector2(kayma, -kayma), new Vector2(kayma, -kayma));
            g.SetAsFirstSibling();
        }

        // Kağıt greni (tekrarlı doku)
        private RawImage Gren(RectTransform p, Vector2 boyut, float kenar = Kontur, Color? renk = null)
        {
            var g = new GameObject("Gren", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            g.transform.SetParent(p, false);
            Doldur(g.rectTransform, kenar, kenar);
            g.texture = grenDokusu;
            g.uvRect = new Rect(0f, 0f, boyut.x / 128f, boyut.y / 128f);
            g.color = renk ?? new Color(0.42f, 0.26f, 0.14f, 0.16f);
            g.raycastTarget = false;
            return g;
        }

        private static Image Resim(string ad, Transform p, Sprite s)
        {
            var go = new GameObject(ad, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(p, false);
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.raycastTarget = false;
            return img;
        }

        private static RectTransform Rt(string ad, Transform p)
        {
            var r = new GameObject(ad, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(p, false);
            return r;
        }

        // cizgi: null → büyük yazılar (17+) çizgi film fontuyla, küçük gövde metni okunaklı fontla
        private static TMP_Text Metin(string ad, Transform p, string yazi, float punto, Color renk, TextAlignmentOptions hiza, bool? cizgi = null)
        {
            var go = new GameObject(ad, typeof(RectTransform));
            go.transform.SetParent(p, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if ((cizgi ?? punto >= 17f) && cizgiFont != null) t.font = cizgiFont;
            t.text = yazi;
            t.fontSize = punto;
            t.color = renk;
            t.alignment = hiza;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        private static void Kirp(TMP_Text t) => t.overflowMode = TextOverflowModes.Ellipsis;

        // kontur > 0 ve zemin görünürse mürekkep konturlu "kağıt düğme"; saydam zeminde hayalet düğme
        private Button Dugme(string ad, Transform p, string yazi, Color zemin, Color yaziRenk, Action tik, float kose = 1.6f, float punto = 18f, float kontur = 3f)
        {
            RectTransform r;
            Image hedef;
            if (kontur > 0f && zemin.a > 0.2f)
            {
                r = Panel(ad, p, Murekkep, kose);
                var ic = Panel("Ic", r, Color.white, kose);
                Doldur(ic, kontur, kontur);
                hedef = ic.GetComponent<Image>();
                hedef.raycastTarget = false;
            }
            else
            {
                r = Panel(ad, p, Color.white, kose);
                hedef = r.GetComponent<Image>();
            }
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = hedef;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            Renklendir(b, zemin);
            b.onClick.AddListener(() => tik?.Invoke());
            if (!string.IsNullOrEmpty(yazi))
            {
                var t = Metin("Yazi", r, yazi, punto, yaziRenk, TextAlignmentOptions.Center);
                Doldur(t.rectTransform, 8f, 2f);
                Kirp(t);
            }
            return b;
        }

        // Görsel beyaz kalır, renkler ColorBlock ile verilir: hover/basılı durumları her zeminde görünür.
        private static void Renklendir(Button b, Color zemin, Color? hover = null)
        {
            if (b == null) return;
            bool saydam = zemin.a < 0.2f;
            var c = b.colors;
            c.normalColor = zemin;
            c.selectedColor = zemin;
            c.highlightedColor = hover ?? (saydam ? new Color(zemin.r, zemin.g, zemin.b, zemin.a + 0.1f) : Acik(zemin, 0.28f));
            c.pressedColor = saydam ? new Color(zemin.r, zemin.g, zemin.b, zemin.a + 0.18f) : new Color(zemin.r * 0.85f, zemin.g * 0.85f, zemin.b * 0.85f, zemin.a);
            c.disabledColor = saydam ? zemin : Color.Lerp(zemin, new Color(0.8f, 0.74f, 0.64f, 1f), 0.6f);
            c.colorMultiplier = 1f;
            c.fadeDuration = 0.08f;
            b.colors = c;
        }

        private static Color Acik(Color c, float m) =>
            new Color(Mathf.Lerp(c.r, 1f, m), Mathf.Lerp(c.g, 1f, m), Mathf.Lerp(c.b, 1f, m), c.a);

        private TMP_InputField Giris(string ad, Transform p, string yerTutucu, float punto, bool cokSatir, Color zemin, Color yaziRenk, float solBosluk = 14f)
        {
            var r = Panel(ad, p, Murekkep, 1.6f);
            r.gameObject.SetActive(false); // bileşenler bağlandıktan sonra açılsın (imleç doğru kurulsun)
            var ic = Panel("Ic", r, Color.white, 1.6f);
            ic.GetComponent<Image>().raycastTarget = false;
            Doldur(ic, 3f, 3f);
            var alan = Rt("Alan", r);
            alan.gameObject.AddComponent<RectMask2D>();
            Doldur(alan, 14f, cokSatir ? 14f : 6f);
            alan.offsetMin = new Vector2(solBosluk, alan.offsetMin.y);
            var hiza = cokSatir ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
            var yer = Metin("YerTutucu", alan, yerTutucu, punto, new Color(yaziRenk.r, yaziRenk.g, yaziRenk.b, 0.4f), hiza);
            yer.fontStyle = FontStyles.Italic;
            Doldur(yer.rectTransform);
            var yazi = Metin("Yazi", alan, "", punto, yaziRenk, hiza);
            yazi.richText = false;
            if (!cokSatir) yazi.textWrappingMode = TextWrappingModes.NoWrap;
            Doldur(yazi.rectTransform);
            var g = r.gameObject.AddComponent<TMP_InputField>();
            g.textViewport = alan;
            g.textComponent = yazi;
            g.placeholder = yer;
            g.lineType = cokSatir ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            g.richText = false;
            g.customCaretColor = true;
            g.caretColor = Murekkep;
            g.caretWidth = 2;
            g.selectionColor = new Color(0.93f, 0.68f, 0.22f, 0.45f);
            g.navigation = new Navigation { mode = Navigation.Mode.None };
            g.targetGraphic = ic.GetComponent<Image>();
            var c = g.colors;
            c.normalColor = zemin; c.highlightedColor = Acik(zemin, 0.2f); c.selectedColor = zemin; c.pressedColor = zemin;
            g.colors = c;
            r.gameObject.SetActive(true);
            return g;
        }

        // Kaydırılabilir liste/ızgara: kökü döndürür, içerik 'icerik'e eklenir.
        private RectTransform Kaydirma(Transform p, out RectTransform icerik, float bosluk, Vector2? hucre = null, int sutun = 1)
        {
            var kok = Panel("Kaydirma", p, new Color(0f, 0f, 0f, 0f), 0f);
            var sr = kok.gameObject.AddComponent<ScrollRect>();
            var gorunum = Rt("Gorunum", kok);
            gorunum.gameObject.AddComponent<RectMask2D>();
            Doldur(gorunum);
            icerik = Rt("Icerik", gorunum);
            icerik.anchorMin = new Vector2(0f, 1f); icerik.anchorMax = Vector2.one;
            icerik.pivot = new Vector2(0.5f, 1f);
            icerik.offsetMin = icerik.offsetMax = Vector2.zero;
            if (hucre.HasValue)
            {
                var g = icerik.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = hucre.Value; g.spacing = new Vector2(bosluk, bosluk);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = Mathf.Max(1, sutun);
                g.padding = new RectOffset(0, 0, 0, 8);
            }
            else
            {
                var v = icerik.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = bosluk; v.padding = new RectOffset(0, 0, 0, 6);
                v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            }
            icerik.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cubuk = Panel("KaydirmaCubugu", kok, new Color(0.17f, 0.1f, 0.08f, 0.1f), 3f);
            cubuk.anchorMin = new Vector2(1f, 0f); cubuk.anchorMax = Vector2.one; cubuk.pivot = new Vector2(1f, 0.5f);
            cubuk.offsetMin = new Vector2(-8f, 0f); cubuk.offsetMax = Vector2.zero;
            var tutamak = Panel("Tutamak", cubuk, Color.white, 3f);
            tutamak.offsetMin = tutamak.offsetMax = Vector2.zero; // Scrollbar yalnız çapaları ayarlar; boyut 0 kalmalı
            var sb = cubuk.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = tutamak;
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.targetGraphic = tutamak.GetComponent<Image>();
            sb.navigation = new Navigation { mode = Navigation.Mode.None };
            var rk = sb.colors;
            rk.normalColor = new Color(0.17f, 0.1f, 0.08f, 0.55f); rk.highlightedColor = new Color(0.17f, 0.1f, 0.08f, 0.75f);
            rk.pressedColor = Murekkep; rk.selectedColor = rk.normalColor;
            sb.colors = rk;

            sr.viewport = gorunum; sr.content = icerik;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            sr.verticalScrollbarSpacing = 8f;
            return kok;
        }

        private static void Yukseklik(Component c, float h)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
        }

        // Başlık düğmeleri için mürekkep çizgi (açı 0: yatay, ±45: çarpı)
        private void Cizik(Transform p, float uzunluk, float aci)
        {
            var c = Panel("Cizgi", p, Murekkep, 0f);
            c.GetComponent<Image>().raycastTarget = false;
            Konum(c, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(uzunluk, 2.6f), new Vector2(0.5f, 0.5f));
            c.localRotation = Quaternion.Euler(0f, 0f, aci);
        }

        private void KareCiz(Transform p, float boy)
        {
            float y = boy * 0.5f;
            Vector2[] pozlar = { new Vector2(0f, y), new Vector2(0f, -y), new Vector2(-y, 0f), new Vector2(y, 0f) };
            for (int i = 0; i < 4; i++)
            {
                var c = Panel("Kenar", p, Murekkep, 0f);
                c.GetComponent<Image>().raycastTarget = false;
                Konum(c, new Vector2(0.5f, 0.5f), pozlar[i], i < 2 ? new Vector2(boy + 2.4f, 2.4f) : new Vector2(2.4f, boy), new Vector2(0.5f, 0.5f));
            }
        }

        private static void Temizle(RectTransform r)
        {
            for (int i = r.childCount - 1; i >= 0; i--)
            {
                var c = r.GetChild(i).gameObject;
                c.SetActive(false); // yerleşim grupları aynı karede yok saysın
                Destroy(c);
            }
        }

        private static void Konum(Component c, Vector2 capa, Vector2 poz, Vector2 boyut, Vector2 pivot)
        {
            var r = (RectTransform)c.transform;
            r.anchorMin = r.anchorMax = capa;
            r.pivot = pivot;
            r.anchoredPosition = poz;
            r.sizeDelta = boyut;
        }

        private static void Kenar(RectTransform r, Vector2 capaMin, Vector2 capaMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            r.anchorMin = capaMin; r.anchorMax = capaMax;
            r.offsetMin = offsetMin; r.offsetMax = offsetMax;
        }

        // İçerik alanının üstünden 'y' piksel aşağıda, tam genişlikte, 'h' yüksekliğinde
        private static void Ust(RectTransform r, float y, float h)
        {
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -y);
            r.sizeDelta = new Vector2(0f, h);
        }

        private static void Doldur(RectTransform r, float yatay = 0f, float dikey = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(yatay, dikey); r.offsetMax = new Vector2(-yatay, -dikey);
        }

        // ------------------------------------------------------------------ SPRITE ÜRETİMİ

        private static Sprite YuvarlakSprite()
        {
            const int n = 64, r = 14;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static Sprite DaireSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float m = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - m) * (x + 0.5f - m) + (y + 0.5f - m) * (y + 0.5f - m));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(m - d) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }

    /// <summary>Pencereyi başlık çubuğundan sürükler (ekran içinde tutar); basınca öne getirir, çift tıkla büyütür.</summary>
    public class PencereSurukle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        private RectTransform pencere, alan;
        private Canvas canvas;
        private Action basildi, ciftTik, suruklemeBasladi;

        public void Hazirla(RectTransform p, RectTransform a, Canvas c, Action bas, Action cift, Action basla)
        {
            pencere = p; alan = a; canvas = c;
            basildi = bas; ciftTik = cift; suruklemeBasladi = basla;
        }

        public void OnPointerDown(PointerEventData e) => basildi?.Invoke();

        public void OnPointerClick(PointerEventData e)
        {
            if (e.clickCount == 2) ciftTik?.Invoke();
        }

        public void OnBeginDrag(PointerEventData e) => suruklemeBasladi?.Invoke();

        public void OnDrag(PointerEventData e)
        {
            var poz = pencere.anchoredPosition + e.delta / canvas.scaleFactor;
            Vector2 yarim = alan.rect.size * 0.5f, boy = pencere.rect.size;
            // En az 120 px yatayda, başlık çubuğu dikeyde ekranda kalsın
            poz.x = Mathf.Clamp(poz.x, -yarim.x - boy.x * 0.5f + 120f, yarim.x + boy.x * 0.5f - 120f);
            poz.y = Mathf.Clamp(poz.y, -yarim.y - boy.y * 0.5f + 50f, yarim.y - boy.y * 0.5f);
            pencere.anchoredPosition = poz;
        }
    }
}
