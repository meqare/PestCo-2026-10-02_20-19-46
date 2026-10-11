using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// İş panosu: 3 kontrat kartı gösterir, oyuncu birini seçip "BAŞLA" ile iş sahnesine geçer.
public class JobBoardUI : MonoBehaviour
{
    [Serializable]
    public class CardView
    {
        public GameObject root;
        public TMP_Text title;
        public TMP_Text district;
        public TMP_Text reward;
        public TMP_Text difficulty;
        public Button selectButton;
        public Outline highlight; // seçilince yeşil çerçeve
    }

    // '★' varsayılan TMP fontunda yok (kutu çıkar); ● var. Yıldız fontu eklenirse buradan değiştirilir.
    private const char DifficultyGlyph = '●';

    public static JobBoardUI Instance { get; private set; }

    // Panel açıkken true; PlayerController imleci geri kilitlemesin diye.
    public static bool IsOpen { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private CardView[] cards;
    [SerializeField] private Button startButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private string jobSceneName = "SampleScene";

    private List<ContractData> offered = new List<ContractData>();
    private int selectedIndex = -1;

    // Singleton'ı ve buton olaylarını kurar; panel kapalı başlar.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            cards[i].selectButton.onClick.AddListener(() => Select(index));
        }
        startButton.onClick.AddListener(StartJob);
        closeButton.onClick.AddListener(Close);

        if (panel != null) panel.SetActive(false);
    }

    // Panel açıkken Esc ile kapatır (zaman durduğu için Update çalışmaya devam eder).
    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private void OnDestroy()
    {
        if (Instance == this) IsOpen = false;
    }

    // Panoyu açar, oyunu durdurur ve yeni kontratları doldurur.
    public void Open()
    {
        if (ContractGenerator.Instance == null)
        {
            Debug.LogError("JobBoardUI: Sahnede ContractGenerator yok!", this);
            return;
        }

        panel.SetActive(true);
        IsOpen = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        offered = ContractGenerator.Instance.GenerateContracts(cards.Length);
        selectedIndex = -1;
        for (int i = 0; i < cards.Length; i++)
            FillCard(cards[i], offered[i]);
        RefreshSelection();
    }

    // Panoyu kapatır ve oyunu devam ettirir.
    public void Close()
    {
        panel.SetActive(false);
        IsOpen = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void FillCard(CardView card, ContractData c)
    {
        card.title.text = c.title;
        card.district.text = c.district;
        card.reward.text = c.baseReward + "$";
        card.difficulty.text = new string(DifficultyGlyph, c.difficulty);
    }

    // Kartı seçer, çerçeveyi vurgular.
    private void Select(int index)
    {
        selectedIndex = index;
        ContractGenerator.Instance.SelectContract(offered[index]);
        Debug.Log("[JOB] Seçildi: " + offered[index].title);
        RefreshSelection();
    }

    // Seçili kartın çerçevesini açar, BAŞLA'yı yalnızca seçim varsa aktif eder.
    private void RefreshSelection()
    {
        for (int i = 0; i < cards.Length; i++)
            cards[i].highlight.enabled = i == selectedIndex;
        startButton.interactable = selectedIndex >= 0;
    }

    private void StartJob()
    {
        if (selectedIndex < 0) return;
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("JobBoardUI: Sahnede SceneLoader yok!", this);
            return;
        }
        SceneLoader.Instance.LoadJobScene(jobSceneName);
    }
}
