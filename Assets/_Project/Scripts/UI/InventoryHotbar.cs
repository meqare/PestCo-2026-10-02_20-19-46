using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Minecraft tarzı envanter çubuğu: ilk slotlarda silahlar, sonra alınan böcekler ve yemekler yan yana durur.
/// Yeni eşya ilk boş slota girer ve yerinde kalır; aynı eşya üst üste yığılır (sağ altta adet).
/// 1-9 tuşları ve fare tekerleği slot seçer; silah slotu seçilince o silah ele alınır.
/// Seçili/yeni alınan eşyanın adı çubuğun üstünde belirip söner.
/// Görseller çalışırken kodla kurulur; bu bileşeni Canvas altındaki boş bir nesneye eklemek yeterlidir.
/// </summary>
public class InventoryHotbar : MonoBehaviour
{
    [Header("Düzen")]
    [SerializeField, Range(1, 12)] private int _slotCount = 9;
    [SerializeField] private float _slotSize = 60f;
    [SerializeField] private float _slotSpacing = 4f;
    [SerializeField] private float _bottomMargin = 12f;

    [Header("Renkler")]
    [Tooltip("Slotların altındaki koyu tepsi.")]
    [SerializeField] private Color _tray = new Color(0.14f, 0.09f, 0.15f, 0.72f);
    [SerializeField] private Color _slotEmptyColor = new Color(0.85f, 0.77f, 0.6f, 0.9f);
    [SerializeField] private Color _selectFillColor = new Color(0.99f, 0.82f, 0.42f);
    [SerializeField] private Color _selectRingColor = new Color(0.93f, 0.68f, 0.22f);
    [SerializeField] private Color _horrorTint = new Color(0.8f, 0.5f, 1f);

    [Header("İsim Yazısı")]
    [Tooltip("Eşya adı çubuğun üstünde kaç saniye görünür.")]
    [SerializeField] private float _nameShowTime = 2f;

    [Header("Silahlar")]
    [Tooltip("Silahları tutan ToolHolder (boşsa sahnede aranır). Silahlar çubuğun ilk slotlarına dizilir.")]
    [SerializeField] private ToolHolder _toolHolder;
    [Tooltip("ToolHolder'daki silahlarla aynı sırada ad ve simge bilgisi (simge boşsa piksel simge çizilir).")]
    [SerializeField] private ToolData[] _toolData;

    [Header("Yemek Simgeleri (boşsa piksel simge çizilir)")]
    [SerializeField] private Sprite _nuclearSodaIcon;
    [SerializeField] private Sprite _explosiveCandyIcon;
    [SerializeField] private Sprite _cursedCookieIcon;

    /// <summary>Seçili slotun indeksi (0 tabanlı).</summary>
    public int SelectedIndex { get; private set; }

    /// <summary>Envanterdeki bir eşya kalemi: böcek (tür+durum+çarpan) ya da yemek türü.</summary>
    private struct ItemKey
    {
        public BugData bug;
        public BugState state;
        public float multiplier;
        public bool isFood;
        public FoodType food;

        public bool SameAs(ItemKey other)
        {
            if (isFood != other.isFood) return false;
            if (isFood) return food == other.food;
            return bug == other.bug && state == other.state && Mathf.Approximately(multiplier, other.multiplier);
        }
    }

    private class Slot
    {
        public RectTransform root;
        public Image fill;
        public Image icon;
        public TMP_Text count;
        public bool isTool;
        public string toolName;
        public bool hasItem;
        public ItemKey key;
        public int amount;
        public float popTimer;
    }

    private const float PopDuration = 0.25f;
    private const float SelectedLift = 8f;

    private readonly List<Slot> _slots = new List<Slot>();
    private readonly List<ItemKey> _currentKeys = new List<ItemKey>();
    private readonly List<int> _currentCounts = new List<int>();

    private RectTransform _selectFrame;
    private TMP_Text _nameText;
    private PestHudTema.Panel _namePlate;
    private CanvasGroup _nameGroup;
    private TMP_Text _overflowText;
    private float _nameTimer;
    private int _lastToolIndex = -1;

    private BugInventory _bugInventory;
    private FoodInventory _foodInventory;

    #region Unity Yaşam Döngüsü

    /// <summary>Çubuğu ve slotları kurar.</summary>
    private void Awake()
    {
        BuildVisuals();
    }

    /// <summary>Envanterlere abone olur ve ilk görünümü çizer (Instance'lar Awake'te hazır olur).</summary>
    private void Start()
    {
        _bugInventory = BugInventory.Instance;
        _foodInventory = FoodInventory.Instance;

        if (_bugInventory != null) _bugInventory.Changed += Refresh;
        if (_foodInventory != null) _foodInventory.Changed += Refresh;

        SetupToolSlots();
        Refresh();
        _nameTimer = 0f;
    }

    /// <summary>Aboneliklerden çıkar; rakam tuşlarını tekrar ToolHolder'a bırakır.</summary>
    private void OnDestroy()
    {
        if (_bugInventory != null) _bugInventory.Changed -= Refresh;
        if (_foodInventory != null) _foodInventory.Changed -= Refresh;
        if (_toolHolder != null) _toolHolder.HandleNumberKeys = true;
    }

    /// <summary>Tuş/tekerlekle seçim, silahla eşitleme, eşya "pop" animasyonu ve isim yazısının sönmesi.</summary>
    private void Update()
    {
        HandleNumberKeys();
        HandleScroll();
        SyncWithToolHolder();
        AnimatePop();
        FadeName();
    }

    #endregion

    #region Silahlar

    /// <summary>
    /// ToolHolder'daki silahları ilk slotlara yerleştirir ve rakam tuşlarını devralır
    /// (ToolHolder artık kendisi dinlemez; seçim buradan iletilir).
    /// </summary>
    private void SetupToolSlots()
    {
        if (_toolHolder == null) _toolHolder = FindFirstObjectByType<ToolHolder>(FindObjectsInactive.Include);
        if (_toolHolder == null) return;

        _toolHolder.HandleNumberKeys = false;

        int count = Mathf.Min(_toolHolder.ToolCount, _slots.Count);
        for (int i = 0; i < count; i++)
        {
            Slot slot = _slots[i];
            ToolData data = _toolData != null && i < _toolData.Length ? _toolData[i] : null;
            GameObject tool = _toolHolder.GetTool(i);

            slot.isTool = true;
            slot.toolName = data != null && !string.IsNullOrEmpty(data.toolName) ? data.toolName
                : tool != null ? tool.name : $"Silah {i + 1}";
            slot.icon.sprite = data != null && data.icon != null ? data.icon : CartoonIcons.Tool(data != null ? data.toolId : null);
            slot.icon.enabled = true;
        }

        _lastToolIndex = _toolHolder.CurrentIndex;
        if (_lastToolIndex >= 0) Select(_lastToolIndex);
    }

    /// <summary>Silah başka bir yerden değiştirilirse seçim çerçevesi de ona geçer.</summary>
    private void SyncWithToolHolder()
    {
        if (_toolHolder == null || _toolHolder.CurrentIndex == _lastToolIndex) return;

        _lastToolIndex = _toolHolder.CurrentIndex;
        if (_lastToolIndex < 0 || _lastToolIndex >= _slots.Count) return;

        // Oyuncu kendi seçtiyse (çerçeve zaten bir silah slotunda) dokunma; değilse silahı göster.
        if (!_slots[SelectedIndex].isTool) MoveFrame(_lastToolIndex);
    }

    #endregion

    #region Envanter

    /// <summary>
    /// Envanterleri okur: biten eşyaların slotunu boşaltır, yenileri ilk boş slota koyar, adetleri günceller.
    /// </summary>
    private void Refresh()
    {
        CollectCurrentItems();

        // Artık envanterde olmayan eşyaların slotlarını boşalt (diğerleri yerinde kalır).
        for (int s = 0; s < _slots.Count; s++)
        {
            Slot slot = _slots[s];
            if (slot.hasItem && IndexOfCurrent(slot.key) < 0) ClearSlot(slot);
        }

        int overflow = 0;
        for (int i = 0; i < _currentKeys.Count; i++)
        {
            ItemKey key = _currentKeys[i];
            int amount = _currentCounts[i];

            Slot slot = FindSlot(key);
            if (slot == null)
            {
                slot = FindEmptySlot();
                if (slot == null) { overflow++; continue; }

                slot.hasItem = true;
                slot.key = key;
                slot.amount = 0;
                slot.icon.sprite = GetIcon(key);
                slot.icon.color = GetIconTint(key);
                slot.icon.enabled = true;
            }

            if (amount > slot.amount)
            {
                // Yeni alınan eşya: zıplasın ve adı görünsün (Minecraft'taki gibi).
                slot.popTimer = PopDuration;
                ShowName(key, amount);
            }

            slot.amount = amount;
            SetCountText(slot, amount);
        }

        _overflowText.text = overflow > 0 ? $"+{overflow}" : string.Empty;
    }

    /// <summary>İki envanterdeki tüm kalemleri ve adetlerini geçici listelere toplar.</summary>
    private void CollectCurrentItems()
    {
        _currentKeys.Clear();
        _currentCounts.Clear();

        if (_bugInventory != null)
        {
            List<CapturedBugInfo> bugs = _bugInventory.GetCapturedBugs();
            for (int i = 0; i < bugs.Count; i++)
            {
                CapturedBugInfo info = bugs[i];
                if (info.data == null || info.count <= 0) continue;

                AddCurrent(new ItemKey { bug = info.data, state = info.stateAtCapture, multiplier = info.valueMultiplier }, info.count);
            }
        }

        if (_foodInventory != null)
        {
            foreach (FoodType food in System.Enum.GetValues(typeof(FoodType)))
            {
                int count = _foodInventory.GetCount(food);
                if (count > 0) AddCurrent(new ItemKey { isFood = true, food = food }, count);
            }
        }
    }

    /// <summary>Aynı kalem listede varsa adedini artırır, yoksa ekler.</summary>
    private void AddCurrent(ItemKey key, int count)
    {
        int index = IndexOfCurrent(key);
        if (index >= 0) { _currentCounts[index] += count; return; }

        _currentKeys.Add(key);
        _currentCounts.Add(count);
    }

    private int IndexOfCurrent(ItemKey key)
    {
        for (int i = 0; i < _currentKeys.Count; i++)
            if (_currentKeys[i].SameAs(key)) return i;
        return -1;
    }

    private Slot FindSlot(ItemKey key)
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].hasItem && _slots[i].key.SameAs(key)) return _slots[i];
        return null;
    }

    private Slot FindEmptySlot()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (!_slots[i].isTool && !_slots[i].hasItem) return _slots[i];
        return null;
    }

    private void ClearSlot(Slot slot)
    {
        slot.hasItem = false;
        slot.amount = 0;
        slot.icon.sprite = null;
        slot.icon.color = Color.white;
        slot.icon.enabled = false;
        slot.icon.rectTransform.localScale = Vector3.one;
        SetCountText(slot, 0);
    }

    /// <summary>Adet yazısı yalnızca 1'den fazlaysa görünür (Minecraft'taki gibi).</summary>
    private static void SetCountText(Slot slot, int amount)
    {
        string text = amount > 1 ? amount.ToString() : string.Empty;
        slot.count.text = text;
    }

    #endregion

    #region Seçim ve İsim

    /// <summary>1-9 tuşları aynı numaralı slotu seçer (Minecraft'taki gibi).</summary>
    private void HandleNumberKeys()
    {
        Keyboard keyboard = PestCo.Truck.PestPC.IsOpen ? null : Keyboard.current; // PC açıkken tuşlar oyuna gitmesin
        if (keyboard == null) return;

        for (int i = 0; i < _slots.Count && i < 9; i++)
        {
            if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
            {
                Select(i);
                return;
            }
        }
    }

    /// <summary>Fare tekerleği seçili slotu kaydırır (yalnızca oyunda, imleç kilitliyken).</summary>
    private void HandleScroll()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;
        // Vakumla bir nesne tutulurken tekerlek mesafe ayarı içindir; slot değiştirmez.
        if (PhysicsVacuumController.IsAnyHolding) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        int step = scroll > 0f ? -1 : 1;
        Select((SelectedIndex + step + _slots.Count) % _slots.Count);
    }

    /// <summary>
    /// Verilen slotu seçer, çerçeveyi taşır ve adını gösterir. Silah slotuysa o silahı ele alır.
    /// </summary>
    public void Select(int index)
    {
        if (_slots.Count == 0) return;

        MoveFrame(index);

        Slot slot = _slots[SelectedIndex];
        if (slot.isTool)
        {
            if (_toolHolder != null) _toolHolder.RequestSwitch(SelectedIndex);
            SetName(slot.toolName);
        }
        else if (slot.hasItem) ShowName(slot.key, slot.amount);
        else _nameTimer = 0f;
    }

    private void MoveFrame(int index)
    {
        SelectedIndex = Mathf.Clamp(index, 0, _slots.Count - 1);
        _selectFrame.anchoredPosition = _slots[SelectedIndex].root.anchoredPosition;
    }

    private void ShowName(ItemKey key, int amount)
    {
        SetName(GetDisplayName(key, amount));
    }

    /// <summary>Etiketi yazıya göre genişletip gösterir.</summary>
    private void SetName(string text)
    {
        _nameText.text = text;
        float width = _nameText.GetPreferredValues(text).x;
        _namePlate.kok.sizeDelta = new Vector2(width + 36f, 40f);
        _nameTimer = _nameShowTime;
    }

    /// <summary>Son yarım saniyede isim etiketini yavaşça söndürür.</summary>
    private void FadeName()
    {
        if (_nameTimer > 0f) _nameTimer -= Time.unscaledDeltaTime;
        _nameGroup.alpha = Mathf.Clamp01(_nameTimer / 0.5f);
    }

    /// <summary>
    /// Seçili slot hafifçe büyüyüp yukarı kalkar ve hardala boyanır (halka da onunla);
    /// yeni gelen eşyanın simgesi kısa bir an zıplar.
    /// </summary>
    private void AnimatePop()
    {
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            bool selected = i == SelectedIndex;

            float s = Mathf.Lerp(slot.root.localScale.x, selected ? 1.08f : 1f, k);
            slot.root.localScale = new Vector3(s, s, 1f);

            Vector2 pos = slot.root.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, selected ? SelectedLift : 0f, k);
            slot.root.anchoredPosition = pos;

            slot.fill.color = Color.Lerp(slot.fill.color, SlotFill(i), k);

            if (selected)
            {
                _selectFrame.localScale = slot.root.localScale;
                _selectFrame.anchoredPosition = pos;
            }

            if (slot.popTimer <= 0f) continue;

            slot.popTimer -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(slot.popTimer / PopDuration);
            float scale = 1f + 0.35f * Mathf.Sin(t * Mathf.PI);
            slot.icon.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private string GetDisplayName(ItemKey key, int amount)
    {
        if (key.isFood) return $"{FoodName(key.food)}  x{amount}";

        string name = string.IsNullOrEmpty(key.bug.bugName) ? key.bug.name : key.bug.bugName;
        string state = key.state == BugState.Horror
            ? $" <color=#{ColorUtility.ToHtmlStringRGB(_horrorTint)}>[Horror]</color>"
            : " [Cute]";
        string fed = key.multiplier > 1f ? $"  (x{key.multiplier:0.#} değer)" : string.Empty;
        return $"{name}{state}  x{amount}{fed}";
    }

    private static string FoodName(FoodType food)
    {
        switch (food)
        {
            case FoodType.NuclearSoda: return "Nükleer Soda";
            case FoodType.ExplosiveCandy: return "Patlayan Şeker";
            case FoodType.CursedCookie: return "Lanetli Kurabiye";
            default: return food.ToString();
        }
    }

    #endregion

    #region Görsel Kurulum

    /// <summary>
    /// Alt-ortada çizgi film envanter çubuğu: koyu tepsi üstünde krem kağıt slotlar (mürekkep kontur + sert gölge),
    /// köşede tuş numarası, seçili slotta hardal dolgu ve kalkma; üstte kağıt etiket halinde eşya adı.
    /// </summary>
    private void BuildVisuals()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();

        float width = _slotCount * _slotSize + (_slotCount - 1) * _slotSpacing;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(width + 8f, _slotSize + 8f);
        rect.anchoredPosition = new Vector2(0f, _bottomMargin);

        // Koyu tepsi: parlak sahnelerde de slotlar seçilsin.
        PestHudTema.Panel tray = PestHudTema.YeniPanel("Bar", rect, _tray, 3f, PestHudTema.GolgeKayma, 18);
        PestHudTema.Doldur(tray.kok, -8f);
        tray.kok.offsetMax = new Vector2(8f, 4f);

        float startX = -width * 0.5f + _slotSize * 0.5f;
        for (int i = 0; i < _slotCount; i++)
        {
            Vector2 position = new Vector2(startX + i * (_slotSize + _slotSpacing), 0f);
            _slots.Add(CreateSlot(rect, i, position));
        }

        // Seçim halkası: hardal çerçeve, dışında ince mürekkep.
        Image ink = CreateImage("Selected", rect, PestHudTema.Murekkep);
        SetRounded(ink, CartoonIcons.RoundedFrame(18, 8f));
        _selectFrame = ink.rectTransform;
        _selectFrame.anchorMin = _selectFrame.anchorMax = new Vector2(0.5f, 0.5f);
        _selectFrame.sizeDelta = new Vector2(_slotSize + 14f, _slotSize + 14f);
        Image ring = CreateImage("Ring", _selectFrame, _selectRingColor);
        SetRounded(ring, CartoonIcons.RoundedFrame(16, 4.5f));
        Stretch(ring.rectTransform, 2f);
        _selectFrame.anchoredPosition = _slots[0].root.anchoredPosition;

        // Eşya adı: çubuğun üstünde kağıt etiket (genişliği yazıya göre ayarlanır).
        _namePlate = PestHudTema.YeniPanel("ItemName", rect, PestHudTema.Kagit, 3f, 4f, 12);
        RectTransform plate = _namePlate.kok;
        plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 1f);
        plate.pivot = new Vector2(0.5f, 0f);
        plate.anchoredPosition = new Vector2(0f, 22f);
        plate.sizeDelta = new Vector2(200f, 40f);
        _nameGroup = plate.gameObject.AddComponent<CanvasGroup>();
        _nameGroup.alpha = 0f;
        _nameGroup.blocksRaycasts = false;

        _nameText = PestHudTema.Yazi("Text", _namePlate.Icerik, 22f, PestHudTema.Murekkep, TextAlignmentOptions.Center);
        Stretch(_nameText.rectTransform);

        _overflowText = CreateText("Overflow", rect, 24f, TextAlignmentOptions.Left);
        _overflowText.rectTransform.anchorMin = _overflowText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        _overflowText.rectTransform.pivot = new Vector2(0f, 0.5f);
        _overflowText.rectTransform.sizeDelta = new Vector2(60f, 32f);
        _overflowText.rectTransform.anchoredPosition = new Vector2(16f, 0f);
        _overflowText.text = string.Empty;
    }

    private Slot CreateSlot(RectTransform parent, int index, Vector2 position)
    {
        RectTransform root = PestHudTema.Bos("Slot" + (index + 1), parent);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(_slotSize, _slotSize);
        root.anchoredPosition = position;

        PestHudTema.Panel panel = PestHudTema.PanelKur(root, _slotEmptyColor, 3f, 4f, 14);

        // Üst yarıda hafif parlama: kağıt şişkin dursun.
        Image shine = CreateImage("Shine", panel.Icerik, new Color(1f, 1f, 1f, 0.22f));
        SetRounded(shine, CartoonIcons.RoundedPanel(10));
        shine.rectTransform.anchorMin = new Vector2(0f, 0.55f);
        shine.rectTransform.anchorMax = Vector2.one;
        shine.rectTransform.offsetMin = new Vector2(4f, 0f);
        shine.rectTransform.offsetMax = new Vector2(-4f, -4f);

        Image icon = CreateImage("Icon", root, Color.white);
        Stretch(icon.rectTransform, 9f);
        icon.rectTransform.offsetMin = new Vector2(12f, 8f);
        icon.preserveAspect = true;
        icon.enabled = false;

        // Tuş numarası sol üstte (1-9).
        if (index < 9)
        {
            TextMeshProUGUI key = PestHudTema.Yazi("Key", root, 14f, PestHudTema.Soluk, TextAlignmentOptions.TopLeft);
            key.fontStyle = FontStyles.Bold;
            key.text = (index + 1).ToString();
            Stretch(key.rectTransform);
            key.rectTransform.offsetMin = new Vector2(8f, 0f);
            key.rectTransform.offsetMax = new Vector2(0f, -5f);
        }

        TMP_Text count = CreateText("Count", root, 22f, TextAlignmentOptions.BottomRight);
        Stretch(count.rectTransform, 5f);

        return new Slot { root = root, fill = panel.dolgu, icon = icon, count = count };
    }

    /// <summary>Slotun dolgu rengi: seçili hardal, dolu kağıt, boş soluk kart.</summary>
    private Color SlotFill(int index)
    {
        if (index == SelectedIndex) return _selectFillColor;
        Slot slot = _slots[index];
        return slot.isTool || slot.hasItem ? PestHudTema.Kagit : _slotEmptyColor;
    }

    private static void SetRounded(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Slot üstündeki sayı yazısı: krem, kalın mürekkep konturlu (çizgi film rakamı).</summary>
    private static TMP_Text CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        var text = PestHudTema.Yazi(name, parent, fontSize, PestHudTema.Krem, alignment);
        PestHudTema.KonturluYazi(text, fontSize, PestHudTema.Krem, 0.3f);
        return text;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    #endregion

    #region Simgeler

    private Sprite GetIcon(ItemKey key)
    {
        if (key.isFood)
        {
            Sprite custom = key.food == FoodType.NuclearSoda ? _nuclearSodaIcon
                : key.food == FoodType.ExplosiveCandy ? _explosiveCandyIcon
                : _cursedCookieIcon;
            return custom != null ? custom : CartoonIcons.Food(key.food);
        }

        if (key.bug.icon != null) return key.bug.icon;

        Color tint = key.state == BugState.Horror ? key.bug.horrorColor : key.bug.cuteColor;
        return CartoonIcons.Bug(tint);
    }

    /// <summary>Modelden üretilmiş böcek simgesi Horror yakalandıysa mor tona boyanır.</summary>
    private Color GetIconTint(ItemKey key)
    {
        if (key.isFood || key.bug.icon == null || key.state != BugState.Horror) return Color.white;
        return Color.Lerp(Color.white, _horrorTint, 0.65f);
    }
    #endregion
}
