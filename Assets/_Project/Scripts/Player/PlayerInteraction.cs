using UnityEngine;
using UnityEngine.InputSystem;

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
    private GUIStyle promptStyle;

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
    }

    private void Update()
    {
        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            CurrentTarget = FindTarget();
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            // Fresh check on press so a stale target from the last interval is never used.
            CurrentTarget = FindTarget();
            CurrentTarget?.Interact(this);
        }
    }

    private IInteractable FindTarget()
    {
        if (cameraTransform == null) return null;

        var ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableMask, QueryTriggerInteraction.Collide))
            return hit.collider.GetComponentInParent<IInteractable>();
        return null;
    }

    private void OnGUI()
    {
        bool highlighted = CurrentTarget != null;
        float size = highlighted ? dotSizeHighlighted : dotSize;
        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        var previousColor = GUI.color;
        GUI.color = highlighted ? dotColorHighlighted : dotColor;
        GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), Texture2D.whiteTexture);
        GUI.color = previousColor;

        if (!highlighted) return;

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 18
            };
            promptStyle.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(center.x - 200f, center.y + 20f, 400f, 30f), CurrentPrompt, promptStyle);
    }
}
