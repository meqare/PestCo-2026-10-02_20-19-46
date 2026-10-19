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
    [SerializeField] private Color dotColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color dotColorHighlighted = new Color(1f, 0.85f, 0.2f, 1f);

    public IInteractable CurrentTarget { get; private set; }
    public string CurrentPrompt => CurrentTarget != null ? CurrentTarget.GetPrompt() : string.Empty;

    private float nextCheckTime;
    private GrabController grab;

    // Nişan noktası ve etkileşim yazısı Canvas üzerinde bir kez kurulur (FPS: eski OnGUI her karede
    // metni yeniden ölçüp editörde ~10 ms takılma yapıyordu). Sadece hedef/metin değişince güncellenir.
    private RectTransform crosshair;
    private Image crosshairImage;
    private TMP_Text promptText;
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
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;

        var dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
        crosshair = (RectTransform)dot.transform;
        crosshair.SetParent(rootRect, false);
        crosshairImage = dot.GetComponent<Image>();
        crosshairImage.raycastTarget = false;

        var label = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)label.transform;
        labelRect.SetParent(rootRect, false);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.sizeDelta = new Vector2(400f, 30f);
        labelRect.anchoredPosition = new Vector2(0f, -20f);
        promptText = label.GetComponent<TextMeshProUGUI>();
        promptText.fontSize = 18f;
        promptText.alignment = TextAlignmentOptions.Top;
        promptText.color = Color.white;
        promptText.raycastTarget = false;
        promptText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // Nişan boyut/rengini ve yazıyı yalnızca değiştiklerinde yazar.
    private void RefreshHud(bool force)
    {
        if (crosshairImage == null || promptText == null) return;

        bool highlighted = CurrentTarget != null;
        if (force || highlighted != shownHighlighted)
        {
            shownHighlighted = highlighted;
            float size = highlighted ? dotSizeHighlighted : dotSize;
            crosshair.sizeDelta = new Vector2(size, size);
            crosshairImage.color = highlighted ? dotColorHighlighted : dotColor;
        }

        string prompt = CurrentPrompt;
        if (force || prompt != shownPrompt)
        {
            shownPrompt = prompt;
            promptText.text = prompt;
        }
    }
}
