using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Head Bob")]
    [SerializeField, Min(0f)] private float headBobAmount = 0.03f;
    [SerializeField] private float headBobFrequency = 1.8f;

    private CharacterController controller;
    private PlayerStatus status;
    private float verticalVelocity;
    private float pitch;
    private Vector3 cameraBaseLocalPos;
    private float bobTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        status = GetComponent<PlayerStatus>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform != null)
            cameraBaseLocalPos = cameraTransform.localPosition;
    }

    private void Start()
    {
        LockCursor(true);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        HandleCursor(keyboard, mouse);

        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
        if (cursorLocked)
            HandleLook(mouse);

        HandleMovement(keyboard, cursorLocked, out float horizontalSpeed);
        HandleHeadBob(horizontalSpeed);
    }

    private void HandleCursor(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard.escapeKey.wasPressedThisFrame)
            LockCursor(false);
        else if (!JobBoardUI.IsOpen && Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
            LockCursor(true);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void HandleLook(Mouse mouse)
    {
        // Mouse delta is already per-frame pixels, so no Time.deltaTime here.
        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;

        transform.Rotate(Vector3.up, delta.x);

        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement(Keyboard keyboard, bool acceptInput, out float horizontalSpeed)
    {
        Vector2 input = Vector2.zero;
        if (acceptInput)
        {
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);
        }

        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f; // keep the controller pressed onto the ground

        float speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;
        if (status != null)
            speed *= status.SpeedMultiplier; // örümcek ağı yavaşlatması
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

        if (acceptInput && grounded && keyboard.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);

        horizontalSpeed = grounded ? input.magnitude * speed : 0f;
    }

    private void HandleHeadBob(float horizontalSpeed)
    {
        if (cameraTransform == null) return;

        Vector3 target = cameraBaseLocalPos;
        if (headBobAmount > 0f && horizontalSpeed > 0.1f)
        {
            // Bob faster when running, scaled relative to walk speed.
            bobTimer += Time.deltaTime * headBobFrequency * (horizontalSpeed / walkSpeed) * Mathf.PI * 2f;
            target.y += Mathf.Sin(bobTimer) * headBobAmount;
        }
        else
        {
            bobTimer = 0f;
        }

        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, target, Time.deltaTime * 10f);
    }
}
