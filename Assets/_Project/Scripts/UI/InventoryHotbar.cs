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
    [SerializeField] private Color _barColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color _slotBorderColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color _slotInnerColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
    [SerializeField] private Color _selectColor = Color.white;
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
        public Image icon;
        public TMP_Text count;
        public TMP_Text countShadow;
        public bool isTool;
        public string toolName;
        public bool hasItem;
        public ItemKey key;
        public int amount;
        public float popTimer;
    }

    private const float PopDuration = 0.25f;

    private readonly List<Slot> _slots = new List<Slot>();
    private readonly List<ItemKey> _currentKeys = new List<ItemKey>();
    private readonly List<int> _currentCounts = new List<int>();
    private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

    private RectTransform _selectFrame;
    private TMP_Text _nameText;
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
            slot.icon.sprite = data != null && data.icon != null ? data.icon : PixelIcons.Tool(data != null ? data.toolId : null);
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
        slot.icon.enabled = false;
        slot.icon.rectTransform.localScale = Vector3.one;
        SetCountText(slot, 0);
    }

    /// <summary>Adet yazısı yalnızca 1'den fazlaysa görünür (Minecraft'taki gibi).</summary>
    private static void SetCountText(Slot slot, int amount)
    {
        string text = amount > 1 ? amount.ToString() : string.Empty;
        slot.count.text = text;
        slot.countShadow.text = text;
    }

    #endregion

    #region Seçim ve İsim

    /// <summary>1-9 tuşları aynı numaralı slotu seçer (Minecraft'taki gibi).</summary>
    private void HandleNumberKeys()
    {
        Keyboard keyboard = Keyboard.current;
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
            _nameText.text = slot.toolName;
            _nameTimer = _nameShowTime;
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
        _nameText.text = GetDisplayName(key, amount);
        _nameTimer = _nameShowTime;
    }

    /// <summary>Son yarım saniyede isim yazısını yavaşça söndürür.</summary>
    private void FadeName()
    {
        if (_nameTimer > 0f) _nameTimer -= Time.unscaledDeltaTime;
        _nameText.alpha = Mathf.Clamp01(_nameTimer / 0.5f);
    }

    /// <summary>Yeni gelen eşyanın simgesi kısa bir an büyüyüp yerine oturur.</summary>
    private void AnimatePop()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
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

    /// <summary>Alt-ortada koyu çubuğu, slotları, seçim çerçevesini ve yazıları oluşturur.</summary>
    private void BuildVisuals()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();

        float width = _slotCount * _slotSize + (_slotCount - 1) * _slotSpacing;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(width + 8f, _slotSize + 8f);
        rect.anchoredPosition = new Vector2(0f, _bottomMargin);

        Image bar = CreateImage("Bar", rect, _barColor);
        Stretch(bar.rectTransform);
        bar.raycastTarget = false;

        float startX = -width * 0.5f + _slotSize * 0.5f;
        for (int i = 0; i < _slotCount; i++)
        {
            Vector2 position = new Vector2(startX + i * (_slotSize + _slotSpacing), 0f);
            _slots.Add(CreateSlot(rect, i, position));
        }

        // Seçim çerçevesi slotlardan biraz büyük, beyaz ve içi boş.
        _selectFrame = CreateFrame("Selected", rect, _slotSize + 8f, 4f, _selectColor);
        _selectFrame.anchoredPosition = _slots[0].root.anchoredPosition;

        _nameText = CreateText("ItemName", rect, 22f, TextAlignmentOptions.Bottom);
        _nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        _nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _nameText.rectTransform.pivot = new Vector2(0.5f, 0f);
        _nameText.rectTransform.sizeDelta = new Vector2(200f, 32f);
        _nameText.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        _nameText.alpha = 0f;

        _overflowText = CreateText("Overflow", rect, 22f, TextAlignmentOptions.Left);
        _overflowText.rectTransform.anchorMin = _overflowText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        _overflowText.rectTransform.pivot = new Vector2(0f, 0.5f);
        _overflowText.rectTransform.sizeDelta = new Vector2(60f, 32f);
        _overflowText.rectTransform.anchoredPosition = new Vector2(8f, 0f);
        _overflowText.text = string.Empty;
    }

    private Slot CreateSlot(RectTransform parent, int index, Vector2 position)
    {
        Image border = CreateImage("Slot" + (index + 1), parent, _slotBorderColor);
        RectTransform root = border.rectTransform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(_slotSize, _slotSize);
        root.anchoredPosition = position;

        Image inner = CreateImage("Inner", root, _slotInnerColor);
        Stretch(inner.rectTransform, 3f);

        Image icon = CreateImage("Icon", root, Color.white);
        Stretch(icon.rectTransform, 8f);
        icon.preserveAspect = true;
        icon.enabled = false;

        TMP_Text shadow = CreateText("CountShadow", root, 20f, TextAlignmentOptions.BottomRight);
        Stretch(shadow.rectTransform, 4f);
        shadow.rectTransform.anchoredPosition = new Vector2(2f, -2f);
        shadow.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        TMP_Text count = CreateText("Count", root, 20f, TextAlignmentOptions.BottomRight);
        Stretch(count.rectTransform, 4f);

        return new Slot { root = root, icon = icon, count = count, countShadow = shadow };
    }

    /// <summary>İçi boş kare çerçeve (dört kenar).</summary>
    private static RectTransform CreateFrame(string name, RectTransform parent, float size, float thickness, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);

        Edge(rect, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thickness));
        Edge(rect, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness));
        Edge(rect, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, 0f));
        Edge(rect, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, 0f));
        return rect;
    }

    private static void Edge(RectTransform parent, Color color, Vector2 min, Vector2 max, Vector2 size)
    {
        Image edge = CreateImage("Edge", parent, color);
        RectTransform rect = edge.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(Mathf.Approximately(min.x, max.x) ? min.x : 0.5f, Mathf.Approximately(min.y, max.y) ? min.y : 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
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

    private static TMP_Text CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.color = Color.white;
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
            return custom != null ? custom : PixelIcons.Food(key.food);
        }

        if (key.bug.icon != null) return key.bug.icon;

        Color tint = key.state == BugState.Horror ? key.bug.horrorColor : key.bug.cuteColor;
        return PixelIcons.Bug(tint);
    }

    /// <summary>16x16 piksel simgeler (Minecraft havası için nokta filtreli).</summary>
    private static class PixelIcons
    {
        private static readonly string[] BugRows =
        {
            "................",
            "....K......K....",
            ".....K....K.....",
            "......KKKK......",
            ".....KWKKWK.....",
            ".....KDDDDK.....",
            "..K.KBBBBBBK.K..",
            "...KBBBDDBBBK...",
            "..K.KBBDDBBK.K..",
            "...KBBBDDBBBK...",
            "..K.KBBDDBBK.K..",
            "....KBBDDBBK....",
            ".....KBDDBK.....",
            "......KKKK......",
            "................",
            "................",
        };

        private static readonly string[] SodaRows =
        {
            "................",
            "....KSSSSSSK....",
            "....KKKKKKKK....",
            "....KGGGGGGK....",
            "....KGLGGGGK....",
            "....KGLYYGGK....",
            "....KGYKKYGK....",
            "....KGYKKYGK....",
            "....KGLYYGGK....",
            "....KGLGGGGK....",
            "....KGLGGGGK....",
            "....KGGGGGGK....",
            "....KKKKKKKK....",
            "....KSSSSSSK....",
            "....KKKKKKKK....",
            "................",
        };

        private static readonly string[] CandyRows =
        {
            "................",
            "................",
            "................",
            "..KK........KK..",
            "..KPK.KKKK.KPK..",
            "..KPPKRRRRKPPK..",
            "..KPPRRWRRRPPK..",
            "..KPPRWRRRRPPK..",
            "..KPPRRRRRRPPK..",
            "..KPPKRRRRKPPK..",
            "..KPK.KKKK.KPK..",
            "..KK........KK..",
            "................",
            "................",
            "................",
            "................",
        };

        private static readonly string[] CookieRows =
        {
            "................",
            "................",
            ".....KKKKKK.....",
            "....KCCCCCCK....",
            "...KCCPCCCCCK...",
            "..KCCCCCCPCCCK..",
            "..KCPCCCCCCCCK..",
            "..KCCCCCPCCCCK..",
            "..KCCCCCCCCPCK..",
            "..KCCPCCCCCCCK..",
            "..KCCCCCCPCCCK..",
            "...KCCCCCCCCK...",
            "....KCCPCCCK....",
            ".....KKKKKK.....",
            "................",
            "................",
        };

        private static readonly string[] SprayRows =
        {
            "................",
            ".......KKK......",
            "......KRRK.M....",
            "......KKKK..M...",
            ".....KSSSSK.....",
            "....KBBBBBBK....",
            "....KBWBBBBK....",
            "....KBWBBBBK....",
            "....KYYYYYYK....",
            "....KYKKYYYK....",
            "....KYYYYYYK....",
            "....KBWBBBBK....",
            "....KBBBBBBK....",
            "....KBBBBBBK....",
            "....KKKKKKKK....",
            "................",
        };

        private static readonly string[] VacuumRows =
        {
            "................",
            "................",
            "................",
            "..........KKKK..",
            ".........KOOOOK.",
            "KKKKKKKKKKOOOOK.",
            "KSSSSSSSKOOWOOK.",
            "KKKKKKKKKOOOOOK.",
            ".........KOOOOK.",
            "........KKKKKKK.",
            "........KGK.....",
            "........KGK.....",
            "........KKK.....",
            "................",
            "................",
            "................",
        };

        private static readonly string[] TrapRows =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "....KKKKKKKK....",
            "...KSSSSSSSSK...",
            "...KSKKKKKKSK...",
            "...KSK....KSK...",
            "..KKKKKKKKKKKK..",
            "..KCCCCCCCCCCK..",
            "..KCCCYYCCCCCK..",
            "..KCCCCCCCCCCK..",
            "..KKKKKKKKKKKK..",
            "................",
            "................",
        };

        private static readonly string[] RacketRows =
        {
            "................",
            "....KKKKKKK.....",
            "...KSYSYSYSK....",
            "..KSYSYSYSYSK...",
            "..KYSYSYSYSYK...",
            "..KSYSYSYSYSK...",
            "..KYSYSYSYSYK...",
            "...KSYSYSYSK....",
            "....KKKSKKK.....",
            "......KSK.......",
            "......KBK.......",
            "......KBK.......",
            "......KBK.......",
            "......KBK.......",
            "......KKK.......",
            "................",
        };

        /// <summary>ToolData.toolId'ye göre silah simgesi (spray, vacuum, trap, racket).</summary>
        public static Sprite Tool(string toolId)
        {
            string cacheKey = "tool_" + (toolId ?? "none");
            if (IconCache.TryGetValue(cacheKey, out Sprite cached) && cached != null) return cached;

            var palette = new Dictionary<char, Color>
            {
                { 'K', new Color(0.08f, 0.06f, 0.06f) },
                { 'W', Color.white },
                { 'S', new Color(0.78f, 0.8f, 0.82f) },
                { 'R', new Color(0.95f, 0.25f, 0.2f) },
                { 'M', new Color(0.6f, 0.9f, 1f) },
                { 'B', new Color(0.25f, 0.55f, 0.95f) },
                { 'Y', new Color(1f, 0.9f, 0.2f) },
                { 'O', new Color(1f, 0.55f, 0.15f) },
                { 'G', new Color(0.35f, 0.35f, 0.38f) },
                { 'C', new Color(0.72f, 0.48f, 0.25f) },
            };

            string[] rows;
            switch (toolId)
            {
                case "vacuum": rows = VacuumRows; break;
                case "trap": rows = TrapRows; break;
                case "racket": rows = RacketRows; break;
                default: rows = SprayRows; break;
            }
            return IconCache[cacheKey] = Draw(cacheKey, rows, palette);
        }

        public static Sprite Bug(Color tint)
        {
            string cacheKey = "bug_" + ColorUtility.ToHtmlStringRGBA(tint);
            if (IconCache.TryGetValue(cacheKey, out Sprite cached) && cached != null) return cached;

            var palette = new Dictionary<char, Color>
            {
                { 'K', new Color(0.08f, 0.06f, 0.06f) },
                { 'W', Color.white },
                { 'B', tint },
                { 'D', Color.Lerp(tint, Color.black, 0.4f) },
            };
            return IconCache[cacheKey] = Draw(cacheKey, BugRows, palette);
        }

        public static Sprite Food(FoodType food)
        {
            string cacheKey = "food_" + food;
            if (IconCache.TryGetValue(cacheKey, out Sprite cached) && cached != null) return cached;

            var palette = new Dictionary<char, Color>
            {
                { 'K', new Color(0.08f, 0.06f, 0.06f) },
                { 'W', Color.white },
                { 'S', new Color(0.78f, 0.8f, 0.82f) },
                { 'G', new Color(0.3f, 0.85f, 0.2f) },
                { 'L', new Color(0.7f, 1f, 0.5f) },
                { 'Y', new Color(1f, 0.9f, 0.2f) },
                { 'R', new Color(0.95f, 0.25f, 0.2f) },
                { 'P', new Color(1f, 0.6f, 0.75f) },
                { 'C', new Color(0.72f, 0.48f, 0.25f) },
            };

            string[] rows;
            switch (food)
            {
                case FoodType.NuclearSoda: rows = SodaRows; break;
                case FoodType.ExplosiveCandy: rows = CandyRows; break;
                default:
                    rows = CookieRows;
                    palette['P'] = new Color(0.55f, 0.2f, 0.75f); // lanetli mor parçacıklar
                    break;
            }
            return IconCache[cacheKey] = Draw(cacheKey, rows, palette);
        }

        /// <summary>Satır dizisini (üstten alta) noktalı dokuya çevirir; '.' saydamdır.</summary>
        private static Sprite Draw(string name, string[] rows, Dictionary<char, Color> palette)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                string row = y < rows.Length ? rows[y] : string.Empty;
                for (int x = 0; x < size; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    // Doku satırları alttan başlar; çizim üstten olduğu için ters çevrilir.
                    pixels[(size - 1 - y) * size + x] = palette.TryGetValue(c, out Color color) ? color : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }

    #endregion
}
