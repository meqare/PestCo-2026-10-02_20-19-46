using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Light))]
public class Flashlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private AudioSource clickSound;

    [Header("Sway")]
    [Tooltip("Approximate lag in seconds behind the camera. 0 = no sway.")]
    [SerializeField, Min(0f)] private float swayDelay = 0.08f;

    [Header("Battery")]
    [SerializeField] private bool infiniteBattery = true;
    [SerializeField, Range(0f, 100f)] private float battery = 100f;
    [SerializeField, Min(0f)] private float drainPerSecond = 1f;
    [SerializeField, Range(0f, 100f)] private float flickerThreshold = 20f;

    [Header("UV Mode")]
    [SerializeField] private Color uvColor = new Color(0.55f, 0.1f, 1f);
    [SerializeField, Range(0.1f, 1f)] private float uvRangeMultiplier = 0.5f;

    public bool IsOn { get; private set; }
    public bool IsUVOn { get; private set; }
    public float Battery => battery;

    private Light spotLight;
    private Color normalColor;
    private float normalRange;
    private float normalIntensity;
    private Quaternion swayRotation;

    private void Awake()
    {
        spotLight = GetComponent<Light>();
        if (cameraTransform == null)
            cameraTransform = transform.parent;

        normalColor = spotLight.color;
        normalRange = spotLight.range;
        normalIntensity = spotLight.intensity;

        IsOn = spotLight.enabled;
        swayRotation = cameraTransform != null ? cameraTransform.rotation : transform.rotation;
        ApplyMode();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.fKey.wasPressedThisFrame)
                SetOn(!IsOn);
            if (keyboard.gKey.wasPressedThisFrame)
            {
                IsUVOn = !IsUVOn;
                ApplyMode();
            }
        }

        UpdateBattery();
    }

    private void LateUpdate()
    {
        // Runs after the camera has been rotated, so we lag behind this frame's look.
        if (cameraTransform == null) return;

        Quaternion target = cameraTransform.rotation;
        if (swayDelay <= 0f)
            swayRotation = target;
        else
            swayRotation = Quaternion.Slerp(swayRotation, target, 1f - Mathf.Exp(-Time.deltaTime / swayDelay));

        transform.rotation = swayRotation;
    }

    public void SetOn(bool on)
    {
        if (on && !infiniteBattery && battery <= 0f) return;

        IsOn = on;
        spotLight.enabled = on;
        if (on && clickSound != null)
            clickSound.Play();
    }

    private void UpdateBattery()
    {
        if (!IsOn) return;

        if (!infiniteBattery)
        {
            battery = Mathf.Max(0f, battery - drainPerSecond * Time.deltaTime);
            if (battery <= 0f)
            {
                IsOn = false;
                spotLight.enabled = false;
                return;
            }
        }

        float intensity = normalIntensity;
        if (!infiniteBattery && battery < flickerThreshold)
        {
            // Subtle flicker, stronger as the battery gets closer to empty.
            float strength = 1f - battery / flickerThreshold;
            float noise = Mathf.PerlinNoise(Time.time * 12f, 0f);
            intensity *= 1f - noise * Mathf.Lerp(0.15f, 0.6f, strength);
        }
        spotLight.intensity = intensity;
    }

    private void ApplyMode()
    {
        spotLight.color = IsUVOn ? uvColor : normalColor;
        spotLight.range = IsUVOn ? normalRange * uvRangeMultiplier : normalRange;
    }
}
