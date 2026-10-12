using UnityEngine;
using UnityEngine.InputSystem;

// Üçüncü şahıs orbit kamera: fare ile hedefin etrafında 360° döner, arkadan bakar, duvara girmez.
// Kamera nesnesine takılır (hedefin çocuğu OLMAMALI; kendi başına dolaşır).
public class PestCamera : MonoBehaviour
{
    [Header("Hedef")]
    [SerializeField] private Transform target;
    [Tooltip("Hedefin ayağından ne kadar yukarıya bakılsın.")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.07f, 0f);

    // Varsayılanlar ~10 cm'lik minik haşere içindir.
    [Header("Mesafe")]
    [SerializeField, Min(0.05f)] private float distance = 0.5f;
    [SerializeField, Min(0.02f)] private float minDistance = 0.12f;
    [Tooltip("Fare tekerleği ile yakınlaşma aralığı.")]
    [SerializeField] private Vector2 zoomRange = new Vector2(0.25f, 2f);
    [SerializeField, Min(0f)] private float zoomSpeed = 0.1f;

    [Header("Fare")]
    [SerializeField] private float sensitivity = 0.12f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Yumuşatma / Çarpışma")]
    [SerializeField, Min(0f)] private float followSmoothing = 18f;
    [SerializeField] private float collisionRadius = 0.04f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    private float yaw;
    private float pitch = 20f;
    private float currentDistance;
    private float wantedDistance;

    private void Start()
    {
        wantedDistance = distance;
        currentDistance = distance;
        if (target != null) yaw = target.eulerAngles.y;
        SetCursorLocked(true);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        else if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);

        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 d = mouse.delta.ReadValue() * sensitivity;
        yaw += d.x;
        pitch = Mathf.Clamp(pitch + (invertY ? d.y : -d.y), minPitch, maxPitch);

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            wantedDistance = Mathf.Clamp(wantedDistance - Mathf.Sign(scroll) * zoomSpeed, zoomRange.x, zoomRange.y);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;

        // Duvar/dolap kamerayı kapatıyorsa yaklaş
        float dist = wantedDistance;
        Vector3 back = rot * Vector3.back;
        if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, wantedDistance, collisionLayers, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(target))
            dist = Mathf.Max(minDistance, hit.distance);

        // Çarpışmada hemen yaklaş, serbest kalınca yumuşakça uzaklaş
        currentDistance = dist < currentDistance ? dist : Mathf.Lerp(currentDistance, dist, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));

        transform.SetPositionAndRotation(pivot + back * currentDistance, rot);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // Çalışma zamanında hedefi değiştirmek için.
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) yaw = target.eulerAngles.y;
    }
}
