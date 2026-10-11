using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Lives on the "ToolSocket" object (child of the camera). Tool models are listed in `tools`.
public class ToolHolder : MonoBehaviour
{
    [Header("Tools")]
    [SerializeField] private List<GameObject> tools = new List<GameObject>();
    [SerializeField] private int startIndex = 0;

    [Header("References")]
    [Tooltip("Used to read movement speed for bob. Auto-found in parents if empty.")]
    [SerializeField] private CharacterController characterController;
    [Tooltip("Duvar yakınlığında silahı geri çeker. Boş bırakılırsa aynı objede aranır.")]
    [SerializeField] private WeaponObstruction wallClearance;

    [Header("Sway")]
    [SerializeField] private float swayPositionAmount = 0.0015f;
    [SerializeField] private float swayRotationAmount = 0.08f;
    [SerializeField] private float swayMaxPosition = 0.04f;
    [SerializeField] private float swayMaxRotation = 4f;
    [SerializeField] private float swaySmooth = 8f;

    [Header("Bob")]
    [SerializeField] private float walkSpeedReference = 4f;
    [SerializeField] private float runSpeedReference = 7f;
    [SerializeField] private Vector2 walkBobAmount = new Vector2(0.01f, 0.008f);
    [SerializeField] private Vector2 runBobAmount = new Vector2(0.025f, 0.018f);
    [SerializeField] private float walkBobFrequency = 1.6f;
    [SerializeField] private float runBobFrequency = 2.4f;
    [SerializeField] private float bobSmooth = 10f;

    [Header("Switch Animation")]
    [SerializeField] private float switchDuration = 0.25f;
    [SerializeField] private float switchDropDistance = 0.25f;

    public int CurrentIndex { get; private set; } = -1;
    public GameObject CurrentTool => CurrentIndex >= 0 && CurrentIndex < tools.Count ? tools[CurrentIndex] : null;

    private Vector3 basePosition;
    private Quaternion baseRotation;

    private Vector3 swayPosition;
    private Quaternion swayRotation = Quaternion.identity;
    private Vector3 bobPosition;
    private float bobTimer;

    private float switchTimer = -1f;
    private int pendingIndex = -1;

    private void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();
        if (wallClearance == null)
            wallClearance = GetComponent<WeaponObstruction>();

        for (int i = 0; i < tools.Count; i++)
            if (tools[i] != null) tools[i].SetActive(false);
        ShowTool(Mathf.Clamp(startIndex, 0, tools.Count - 1));
    }

    private void Update()
    {
        HandleSwitchInput();
        UpdateSway();
        UpdateBob();
        float switchOffset = UpdateSwitchAnimation();
        Vector3 wallOffset = GetWallOffsetLocal();

        transform.localPosition = basePosition + swayPosition + bobPosition + wallOffset + Vector3.down * switchOffset;
        transform.localRotation = baseRotation * swayRotation;
    }

    // WeaponObstruction'ın geri/aşağı çekilmesi zaten socket'in parent'ı (kamera) uzayındadır.
    private Vector3 GetWallOffsetLocal()
    {
        return wallClearance != null ? wallClearance.LocalOffset : Vector3.zero;
    }

    private void HandleSwitchInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame) RequestSwitch(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) RequestSwitch(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) RequestSwitch(2);
        else if (keyboard.digit4Key.wasPressedThisFrame) RequestSwitch(3);
    }

    public void RequestSwitch(int index)
    {
        if (index < 0 || index >= tools.Count || tools[index] == null) return;
        if (index == CurrentIndex && switchTimer < 0f) return;

        pendingIndex = index;
        if (switchTimer < 0f)
            switchTimer = 0f;
        else if (switchTimer >= switchDuration * 0.5f)
            switchTimer = switchDuration - switchTimer; // already rising: mirror back to the same height, going down

    }

    // Returns how far down the socket should be pushed this frame.
    private float UpdateSwitchAnimation()
    {
        if (switchTimer < 0f) return 0f;

        float half = switchDuration * 0.5f;
        bool wasFirstHalf = switchTimer < half;
        switchTimer += Time.deltaTime;

        // Swap the model at the lowest point, out of view.
        if (wasFirstHalf && switchTimer >= half && pendingIndex >= 0)
        {
            ShowTool(pendingIndex);
            pendingIndex = -1;
        }

        if (switchTimer >= switchDuration)
        {
            switchTimer = -1f;
            return 0f;
        }

        float t = switchTimer / switchDuration;          // 0..1
        float curve = Mathf.Sin(t * Mathf.PI);           // 0 -> 1 -> 0
        return curve * switchDropDistance;
    }

    private void ShowTool(int index)
    {
        if (index < 0 || index >= tools.Count) return;
        if (CurrentTool != null) CurrentTool.SetActive(false);
        CurrentIndex = index;
        if (CurrentTool != null) CurrentTool.SetActive(true);
    }

    private void UpdateSway()
    {
        Vector2 delta = Vector2.zero;
        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            delta = mouse.delta.ReadValue();

        // Opposite direction to the mouse movement.
        Vector3 targetPos = new Vector3(-delta.x, -delta.y, 0f) * swayPositionAmount;
        targetPos = Vector3.ClampMagnitude(targetPos, swayMaxPosition);

        float pitch = Mathf.Clamp(delta.y * swayRotationAmount, -swayMaxRotation, swayMaxRotation);
        float yaw = Mathf.Clamp(-delta.x * swayRotationAmount, -swayMaxRotation, swayMaxRotation);
        Quaternion targetRot = Quaternion.Euler(pitch, yaw, yaw * 0.5f);

        float k = 1f - Mathf.Exp(-swaySmooth * Time.deltaTime);
        swayPosition = Vector3.Lerp(swayPosition, targetPos, k);
        swayRotation = Quaternion.Slerp(swayRotation, targetRot, k);
    }

    private void UpdateBob()
    {
        float speed = 0f;
        if (characterController != null && characterController.isGrounded)
        {
            Vector3 v = characterController.velocity;
            speed = new Vector2(v.x, v.z).magnitude;
        }

        Vector3 target = Vector3.zero;
        if (speed > 0.1f)
        {
            // 0 = walking, 1 = running.
            float run = Mathf.InverseLerp(walkSpeedReference, runSpeedReference, speed);
            Vector2 amount = Vector2.Lerp(walkBobAmount, runBobAmount, run);
            float frequency = Mathf.Lerp(walkBobFrequency, runBobFrequency, run);

            bobTimer += Time.deltaTime * frequency * Mathf.PI * 2f;
            // Figure-eight: horizontal at base frequency, vertical at double.
            target.x = Mathf.Sin(bobTimer) * amount.x;
            target.y = Mathf.Sin(bobTimer * 2f) * amount.y;
        }
        else
        {
            bobTimer = 0f;
        }

        bobPosition = Vector3.Lerp(bobPosition, target, 1f - Mathf.Exp(-bobSmooth * Time.deltaTime));
    }
}
