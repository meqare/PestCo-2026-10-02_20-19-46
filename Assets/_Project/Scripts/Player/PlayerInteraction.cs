using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Raycast")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private float checkInterval = 0.1f;

    [Header("Crosshair")]
    [SerializeField] private float dotSize = 4f;
    [SerializeField] private float dotSizeHighlighted = 10f;
    [SerializeField] private Color dotColor = new Color(0.98f, 0.94f, 0.84f, 1f);
    [SerializeField] private Color dotColorHighlighted = new Color(0.93f, 0.68f, 0.22f, 1f);

    public IInteractable CurrentTarget { get; private set; }
    public string CurrentPrompt => CurrentTarget != null ? CurrentTarget.GetPrompt() : string.Empty;

    private float nextCheckTime;
    private GrabController grab;

    // Nişan noktası ve etkileşim yazısı Canvas üzerinde bir kez kurulur (FPS: eski OnGUI her karede
    // metni yeniden ölçüp editörde ~10 ms takılma yapıyordu). Sadece hedef/metin değişince güncellenir.
    private GameObject hudRoot;
    private RectTransform crosshair;
    private Image crosshairImage;
    private TMP_Text promptText;
    private PestHudTema.Panel promptPlate;
    private bool shownHighlighted;
    private string shownPrompt;

    private void Reset()
    {
        interactableMask = LayerMask.GetMask("Interactable");
    }

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (interactableMask == 0)
            interactableMask = LayerMask.GetMask("Interactable");
        grab = GetComponent<GrabController>();
    }

    private void Start()
    {
        BuildHud();
        RefreshHud(true);
    }

    private void Update()
    {
        // Fanus inceleme paneli açıkken hiçbir etkileşim olmaz; nişan ve yazı gizlenir.
        if (CageInspectPanel.IsOpen)
        {
            if (CurrentTarget != null)
            {
                CurrentTarget = null;
                RefreshHud(false);
            }
            if (hudRoot != null && hudRoot.activeSelf) hudRoot.SetActive(false);
            return;
        }
        if (hudRoot != null && !hudRoot.activeSelf) hudRoot.SetActive(true);

        bool changed = false;
        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            CurrentTarget = FindTarget();
            changed = true;
        }

        var keyboard = Keyboard.current;
        // Eşya tutulurken E bırakma tuşudur (GrabController); başka etkileşim tetiklenmesin.
        if (keyboard != null && !PestCo.Truck.PestPC.IsOpen && keyboard.eKey.wasPressedThisFrame && (grab == null || !grab.ConsumesInteract))
        {
            // Fresh check on press so a stale target from the last interval is never used.
            CurrentTarget = FindTarget();
            CurrentTarget?.Interact(this);
            changed = true;
        }

        if (changed) RefreshHud(false);
    }

    private IInteractable FindTarget()
    {
        if (cameraTransform == null) return null;

        var ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableMask, QueryTriggerInteraction.Collide))
            return hit.collider.GetComponentInParent<IInteractable>();
        return null;
    }

    // Ekranın ortasına nişan noktası ve altına etkileşim yazısı kurar (sahnedeki Canvas'a).
    private void BuildHud()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var root = new GameObject("InteractionHud", typeof(RectTransform));
        hudRoot = root;
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;

        // Nişan: mürekkep konturlu yuvarlak nokta (çizgi film stili).
        var ink = PestHudTema.Resim("Crosshair", rootRect, PestHudTema.Daire, PestHudTema.Murekkep);
        crosshair = ink.rectTransform;
        crosshairImage = PestHudTema.Resim("Dot", crosshair, PestHudTema.Daire, dotColor);
        PestHudTema.Doldur(crosshairImage.rectTransform, 2f);

        // Etkileşim yazısı: nişanın altında kağıt etiket, yazıya göre genişler.
        promptPlate = PestHudTema.YeniPanel("Prompt", rootRect, PestHudTema.Kagit, 3f, 4f, 12);
        PestHudTema.Yerlestir(promptPlate.kok, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(200f, 38f));
        promptText = PestHudTema.Yazi("Text", promptPlate.Icerik, 20f, PestHudTema.Murekkep, TextAlignmentOptions.Center);
        PestHudTema.Doldur(promptText.rectTransform);
        promptPlate.kok.gameObject.SetActive(false);
    }

    // Nişan boyut/rengini ve yazıyı yalnızca değiştiklerinde yazar.
    private void RefreshHud(bool force)
    {
        if (crosshairImage == null || promptText == null) return;

        bool highlighted = CurrentTarget != null;
        if (force || highlighted != shownHighlighted)
        {
            shownHighlighted = highlighted;
            float size = (highlighted ? dotSizeHighlighted : dotSize) + 4f;
            crosshair.sizeDelta = new Vector2(size, size);
            crosshairImage.color = highlighted ? dotColorHighlighted : dotColor;
        }

        string prompt = CurrentPrompt;
        if (force || prompt != shownPrompt)
        {
            shownPrompt = prompt;
            promptText.text = prompt;
            bool show = !string.IsNullOrEmpty(prompt);
            promptPlate.kok.gameObject.SetActive(show);
            if (show) promptPlate.kok.sizeDelta = new Vector2(promptText.GetPreferredValues(prompt).x + 32f, 38f);
        }
    }
}
