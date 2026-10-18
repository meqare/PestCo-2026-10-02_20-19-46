using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Fizik vakum silahı: girdi + koni tespiti. Pullable layer'daki Rigidbody'leri AddForce ile çeker,
// yakına geleni tutma noktasında yumuşakça tutar. Böcek vakumundan (VacuumTool) bağımsızdır, aynı alette yan yana çalışır.
public class VacuumGun : MonoBehaviour
{
    [SerializeField] private VacuumSettings settings;
    [Tooltip("Boşsa Camera.main kullanılır.")]
    [SerializeField] private Camera cam;
    [Tooltip("Çizgilerin çıktığı silah ucu. Boşsa kamera.")]
    [SerializeField] private Transform muzzle;

    private Transform camT;
    private Collider[] hitBuffer;
    private PullableObject[] beamTargets;
    private int beamTargetCount;
    private readonly List<PullableObject> held = new List<PullableObject>(8);
    private LineRenderer[] beams;
    private Vector3[] beamPositions;
    private bool pulling, throwRequested, dropRequested;
    private int scanId;

    public int HeldCount => held.Count;

    private void Awake()
    {
        if (settings == null)
        {
            Debug.LogError("VacuumGun: VacuumSettings atanmamış.", this);
            enabled = false;
            return;
        }
        if (cam == null) cam = Camera.main;
        camT = cam != null ? cam.transform : transform;

        hitBuffer = new Collider[settings.queryBufferSize];
        beamTargets = new PullableObject[settings.queryBufferSize];
        BuildBeams();
    }

    private void OnDisable()
    {
        pulling = throwRequested = dropRequested = false;
        DropAll();
        beamTargetCount = 0;
        if (beams != null)
            for (int i = 0; i < beams.Length; i++)
                if (beams[i] != null) beams[i].enabled = false;
    }

    // Girdi: Update'te okunur, fizik FixedUpdate'te uygulanır.
    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        pulling = Button(mouse, settings.pullButton).isPressed;
        if (Button(mouse, settings.throwButton).wasPressedThisFrame) throwRequested = true;
        var kb = Keyboard.current;
        if (kb != null && kb[settings.dropKey].wasPressedThisFrame) dropRequested = true;
    }

    private void FixedUpdate()
    {
        PruneHeld();
        beamTargetCount = 0;

        if (dropRequested) { DropAll(); dropRequested = false; }
        if (throwRequested)
        {
            throwRequested = false;
            if (held.Count > 0)
            {
                PullableObject p = held[0];
                held.RemoveAt(0);
                p.Throw(camT.forward, settings);
            }
        }

        Vector3 origin = camT.position;
        if (pulling) ScanAndPull(origin);
        HoldAll(origin);
    }

    // Küre taraması (allocation yok) -> koni açısı -> engel kontrolü -> çekme.
    private void ScanAndPull(Vector3 origin)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, settings.maxDistance, hitBuffer,
            settings.pullableLayers, QueryTriggerInteraction.Ignore);
        scanId++;
        Vector3 fwd = camT.forward;
        float halfAngle = settings.coneAngle * 0.5f;
        bool canGrab = held.Count < settings.maxHeldObjects;

        for (int i = 0; i < count; i++)
        {
            Rigidbody body = hitBuffer[i].attachedRigidbody;
            if (body == null || !body.TryGetComponent(out PullableObject p)) continue;
            if (p.IsHeld || p.LastScanId == scanId) continue;
            p.LastScanId = scanId;
            p.Bind(settings);

            Vector3 target = body.worldCenterOfMass;
            Vector3 to = target - origin;
            float dist = to.magnitude;
            if (dist > settings.maxDistance || dist < 0.0001f) continue;
            if (Vector3.Angle(fwd, to) > halfAngle) continue;
            if (Physics.Linecast(origin, target, settings.obstacleLayers, QueryTriggerInteraction.Ignore)) continue;

            Vector3 dirToGun = -to / dist;
            Vector3 pullPoint = HoldPoint(origin, 0);
            Vector3 toHold = pullPoint - target;
            if (settings.horizontalOnly) toHold.y = 0f;
            float distToHold = toHold.magnitude;

            if (canGrab && distToHold <= settings.holdSnapDistance)
            {
                p.SetHeld(true);
                held.Add(p);
                canGrab = held.Count < settings.maxHeldObjects;
                continue;
            }
            if (!canGrab && distToHold <= settings.holdSnapDistance * 2f) continue; // dolu: yaklaşmasın

            // Çekim tutma noktasına doğru olur (oyuncunun içine değil).
            Vector3 towards = (pullPoint - target).normalized;
            p.ApplyPull(towards, dist / settings.maxDistance, settings);
            if (settings.debugDraw) Debug.DrawRay(target, towards * 1.5f, settings.debugPullColor);
            if (beamTargetCount < beamTargets.Length) beamTargets[beamTargetCount++] = p;
        }
    }

    // Tutulan nesneleri kendi tutma noktalarına yumuşakça oturtur.
    private void HoldAll(Vector3 origin)
    {
        for (int i = 0; i < held.Count; i++)
        {
            Vector3 point = HoldPoint(origin, i);
            held[i].ApplyHold(point, settings);
            if (settings.debugDraw) Debug.DrawLine(held[i].Body.position, point, settings.debugHeldColor);
            if (beamTargetCount < beamTargets.Length) beamTargets[beamTargetCount++] = held[i];
        }
    }

    // i. nesne için tutma noktası: kamera uzayında offset + yana dizilim.
    private Vector3 HoldPoint(Vector3 origin, int index)
    {
        int n = Mathf.Max(1, held.Count);
        float spread = (index - (n - 1) * 0.5f) * settings.holdSpacing;
        Vector3 local = settings.holdOffset + Vector3.right * spread;
        if (settings.horizontalOnly)
        {
            // Bakış açısından bağımsız: yatay ileri/sağ ile oyuncunun önünde yerde takip eder.
            Vector3 f = camT.forward; f.y = 0f;
            f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            return origin + r * local.x + f * local.z;
        }
        return origin + camT.right * local.x + camT.up * local.y + camT.forward * local.z;
    }

    private void PruneHeld()
    {
        for (int i = held.Count - 1; i >= 0; i--)
            if (held[i] == null || !held[i].IsHeld)
                held.RemoveAt(i);
    }

    private void DropAll()
    {
        for (int i = 0; i < held.Count; i++)
            if (held[i] != null) held[i].SetHeld(false);
        held.Clear();
    }

    // Kıvrımlı çizgi: silah ucundan nesneye, sinüs dalgasıyla yan yana sallanır.
    private void LateUpdate()
    {
        Vector3 start = muzzle != null ? muzzle.position : camT.position + camT.forward * 0.4f - camT.up * 0.2f;
        for (int b = 0; b < beams.Length; b++)
        {
            if (b >= beamTargetCount || beamTargets[b] == null)
            {
                if (beams[b].enabled) beams[b].enabled = false;
                continue;
            }
            beams[b].enabled = true;
            Vector3 end = beamTargets[b].Body.worldCenterOfMass;
            Vector3 axis = end - start;
            Vector3 side = Vector3.Cross(axis, camT.up);
            side = side.sqrMagnitude > 0.0001f ? side.normalized : camT.right;
            Vector3 up = Vector3.Cross(side, axis).normalized;

            int n = beamPositions.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float envelope = Mathf.Sin(t * Mathf.PI); // uçlarda sıfır
                float phase = t * settings.beamWaveFrequency * Mathf.PI * 2f - Time.time * settings.beamWaveSpeed;
                Vector3 offset = (side * Mathf.Sin(phase) + up * Mathf.Cos(phase)) * (settings.beamWaveAmplitude * envelope);
                beamPositions[i] = Vector3.Lerp(start, end, t) + offset;
            }
            beams[b].SetPositions(beamPositions);
        }
    }

    private void BuildBeams()
    {
        int count = settings.maxBeams;
        beams = new LineRenderer[count];
        beamPositions = new Vector3[settings.beamPoints];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("VacuumBeam_" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = settings.beamPoints;
            lr.widthMultiplier = settings.beamWidth;
            lr.colorGradient = settings.beamColor;
            lr.sharedMaterial = settings.beamMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 4;
            lr.enabled = false;
            beams[i] = lr;
        }
    }

    private static UnityEngine.InputSystem.Controls.ButtonControl Button(Mouse m, VacuumMouseButton b)
    {
        switch (b)
        {
            case VacuumMouseButton.Left: return m.leftButton;
            case VacuumMouseButton.Middle: return m.middleButton;
            default: return m.rightButton;
        }
    }

    // Scene görünümünde koni alanı (debugDraw açıkken).
    private void OnDrawGizmos()
    {
        if (settings == null || !settings.debugDraw) return;
        Transform t = cam != null ? cam.transform : (Camera.main != null ? Camera.main.transform : transform);
        Gizmos.color = settings.debugConeColor;
        float r = Mathf.Tan(settings.coneAngle * 0.5f * Mathf.Deg2Rad) * settings.maxDistance;
        Vector3 tip = t.position;
        Vector3 center = tip + t.forward * settings.maxDistance;
        Gizmos.DrawLine(tip, center);
        const int segments = 24;
        Vector3 prev = center + t.up * r;
        for (int i = 1; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 p = center + (t.up * Mathf.Cos(a) + t.right * Mathf.Sin(a)) * r;
            Gizmos.DrawLine(prev, p);
            if (i % 6 == 0) Gizmos.DrawLine(tip, p);
            prev = p;
        }
        Gizmos.DrawWireSphere(tip + t.right * settings.holdOffset.x + t.up * settings.holdOffset.y + t.forward * settings.holdOffset.z,
            settings.holdSnapDistance);
    }
}
