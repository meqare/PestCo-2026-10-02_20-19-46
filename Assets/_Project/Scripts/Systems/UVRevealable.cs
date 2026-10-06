using UnityEngine;

// UV fenerin ışığı değince parlayan obje (ör. yuva). Normal ışıkta sönük durur.
// Işık çekilince parlama yavaşça söner; işaret objeleri de sadece parlarken görünür.
// Aynı objede IDamageable varsa ve ölmüşse hiçbir şey göstermez.
public class UVRevealable : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Görünüm")]
    [Tooltip("UV değince parlayacak renderer'lar. Materyalde Emission açık olmalı (rengi siyah olabilir).")]
    [SerializeField] private Renderer[] glowRenderers;
    [SerializeField, ColorUsage(false, true)] private Color glowColor = new Color(0.7f, 1.4f, 0.35f);
    [Tooltip("Sadece UV ile görülünce açılacak objeler (ör. üstteki elmas işaret).")]
    [SerializeField] private GameObject[] showWhenRevealed;

    [Header("Zamanlama")]
    [Tooltip("Parlamanın tam açılma süresi (saniye).")]
    [SerializeField, Min(0.01f)] private float fadeInTime = 0.15f;
    [Tooltip("Işık çekilince sönme süresi (saniye).")]
    [SerializeField, Min(0.01f)] private float fadeOutTime = 1.5f;

    [Header("Görüş")]
    [Tooltip("Fener ile obje arasındaki bu layer'lar ışığı keser (duvar vb.).")]
    [SerializeField] private LayerMask obstacleLayers = 1; // Default

    private Flashlight flashlight;
    private Light flashlightLight;
    private Transform lightTransform;
    private IDamageable damageable;
    private Collider ownCollider;
    private MaterialPropertyBlock block;
    private float reveal;          // 0 = sönük, 1 = tam parlak
    private bool markersShown = true;

    // Feneri bir kere bulur, başlangıçta parlamayı ve işaretleri kapatır.
    private void Awake()
    {
        flashlight = FindFirstObjectByType<Flashlight>(FindObjectsInactive.Include);
        if (flashlight != null)
        {
            flashlightLight = flashlight.GetComponent<Light>();
            lightTransform = flashlight.transform;
        }
        damageable = GetComponent<IDamageable>();
        ownCollider = GetComponentInChildren<Collider>();
        block = new MaterialPropertyBlock();

        reveal = 0f;
        ApplyGlow();
        SetMarkers(false);
    }

    // Her karede: UV değiyorsa parlamayı artırır, değmiyorsa yavaşça söndürür.
    private void Update()
    {
        bool alive = damageable == null || damageable.IsAlive;
        if (!alive)
        {
            if (reveal > 0f || markersShown)
            {
                reveal = 0f;
                ApplyGlow();
                SetMarkers(false);
            }
            return;
        }

        float target = IsLitByUV() ? 1f : 0f;
        float speed = target > reveal ? 1f / fadeInTime : 1f / fadeOutTime;
        float newReveal = Mathf.MoveTowards(reveal, target, speed * Time.deltaTime);

        if (!Mathf.Approximately(newReveal, reveal))
        {
            reveal = newReveal;
            ApplyGlow();
        }
        SetMarkers(reveal > 0.05f);
    }

    // Fener açık + UV modunda + obje ışık konisinin içinde + arada duvar yoksa true.
    private bool IsLitByUV()
    {
        if (flashlight == null || !flashlight.isActiveAndEnabled || !flashlight.IsOn || !flashlight.IsUVOn)
            return false;

        Vector3 target = ownCollider != null ? ownCollider.bounds.center : transform.position;
        Vector3 toTarget = target - lightTransform.position;
        float range = flashlightLight.range;
        if (toTarget.sqrMagnitude > range * range) return false;
        if (Vector3.Angle(lightTransform.forward, toTarget) > flashlightLight.spotAngle * 0.5f) return false;

        return !Physics.Linecast(lightTransform.position, target, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    // Parlama miktarını renderer'lara property block ile uygular (materyal kopyalanmaz).
    private void ApplyGlow()
    {
        if (glowRenderers == null) return;
        for (int i = 0; i < glowRenderers.Length; i++)
        {
            Renderer r = glowRenderers[i];
            if (r == null) continue;
            if (reveal <= 0f)
            {
                r.SetPropertyBlock(null);
                continue;
            }
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, glowColor * reveal);
            r.SetPropertyBlock(block);
        }
    }

    // İşaret objelerini açar/kapatır (değişmediyse dokunmaz).
    private void SetMarkers(bool show)
    {
        if (show == markersShown) return;
        markersShown = show;
        if (showWhenRevealed == null) return;
        for (int i = 0; i < showWhenRevealed.Length; i++)
            if (showWhenRevealed[i] != null)
                showWhenRevealed[i].SetActive(show);
    }
}
