using System;
using UnityEngine;

// Eşyanın dayanıklılığı ve değeri. Sert çarpınca hasar alır, değeri düşer; kaybedilen değer iş kazancından
// ceza olarak kesilir (JobManager.AddDamage). Hasar çarpma hızına bağlıdır: yavaş çarpma zarar vermez.
[RequireComponent(typeof(Rigidbody))]
public class PropDurability : MonoBehaviour
{
    [Header("Değer")]
    [Tooltip("Hasarsız eşyanın değeri ($).")]
    [SerializeField, Min(0)] private int baseValue = 50;
    [Tooltip("Kaybedilen değer iş kazancından da düşülsün mü?")]
    [SerializeField] private bool chargePenaltyToJob = true;

    [Header("Hasar")]
    [Tooltip("Bu çarpma hızının (m/s) altında hasar yok. Yerden 1 m düşmek ~4.4 m/s.")]
    [SerializeField, Min(0f)] private float minImpactSpeed = 4.5f;
    [Tooltip("Bu çarpma hızında tek vuruşta tamamen bozulur.")]
    [SerializeField, Min(0.1f)] private float breakImpactSpeed = 16f;
    [Tooltip("Hasar eğrisi: 1 = doğrusal, >1 hafif çarpmada az, sert çarpmada çok hasar.")]
    [SerializeField, Range(0.5f, 3f)] private float damageExponent = 1.4f;
    [Tooltip("Arka arkaya hasar arası en az bu kadar saniye (aynı çarpmanın çoklu temasları tek sayılsın).")]
    [SerializeField, Min(0f)] private float hitCooldown = 0.12f;

    [Header("Görünüm")]
    [Tooltip("Tam bozukken renk çarpanı (1 = değişmez). Hasarlandıkça kararır.")]
    [SerializeField, Range(0.1f, 1f)] private float brokenTint = 0.45f;

    public int BaseValue => baseValue;
    public float Integrity { get; private set; } = 1f;
    public int Value => Mathf.RoundToInt(baseValue * Integrity);
    public bool IsBroken => Integrity <= 0f;
    // (kaybedilen $, kalan değer $)
    public event Action<int, int> OnDamaged;

    private Renderer[] renderers;
    private Color[] baseColors;
    private MaterialPropertyBlock block;
    private int colorId;
    private float lastHitTime = -99f;
    private float penaltyCarry; // yuvarlama kaybı olmasın: kesirli $ biriktirilir

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = renderers[i].sharedMaterial;
            baseColors[i] = Color.white;
            if (mat == null) continue;
            if (mat.HasProperty(BaseColorId)) baseColors[i] = mat.GetColor(BaseColorId);
            else if (mat.HasProperty(ColorId)) baseColors[i] = mat.GetColor(ColorId);
        }
        colorId = BaseColorId;
    }

    private void OnCollisionEnter(Collision col)
    {
        if (IsBroken || Time.time < lastHitTime + hitCooldown) return;
        // Oyuncunun kapsülü (CharacterController) hasar vermez; iten oyuncu eşyayı kırmasın.
        if (col.collider.GetComponentInParent<CharacterController>() != null) return;

        float speed = col.relativeVelocity.magnitude;
        if (speed <= minImpactSpeed) return;

        float t = Mathf.Clamp01((speed - minImpactSpeed) / (breakImpactSpeed - minImpactSpeed));
        float damage = Mathf.Pow(t, damageExponent);
        if (damage <= 0.005f) return;

        lastHitTime = Time.time;
        ApplyDamage(damage, col.contactCount > 0 ? col.GetContact(0).point : transform.position,
            col.contactCount > 0 ? col.GetContact(0).normal : Vector3.up, speed);
    }

    private void ApplyDamage(float fraction, Vector3 point, Vector3 normal, float speed)
    {
        int before = Value;
        Integrity = Mathf.Max(0f, Integrity - fraction);
        int lost = before - Value;
        RefreshTint();

        Juice.Hit(point, normal);
        Juice.Shake(Mathf.Lerp(0.01f, 0.05f, fraction), 0.15f);

        if (lost > 0)
        {
            penaltyCarry += lost;
            if (chargePenaltyToJob && JobManager.Instance != null)
            {
                int whole = Mathf.FloorToInt(penaltyCarry);
                if (whole > 0) { JobManager.Instance.AddDamage(whole); penaltyCarry -= whole; }
            }
            Debug.Log($"{name} hasar aldı: {speed:0.0} m/s, -{lost}$ (kalan {Value}/{baseValue}$)", this);
        }
        OnDamaged?.Invoke(lost, Value);
        if (IsBroken) Debug.Log($"{name} bozuldu!", this);
    }

    private void RefreshTint()
    {
        float k = Mathf.Lerp(brokenTint, 1f, Integrity);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = baseColors[i];
            c = new Color(c.r * k, c.g * k, c.b * k, c.a);
            renderers[i].GetPropertyBlock(block);
            block.SetColor(colorId, c);
            renderers[i].SetPropertyBlock(block);
        }
    }

    // Test/onarım için.
    public void Repair()
    {
        Integrity = 1f;
        penaltyCarry = 0f;
        RefreshTint();
    }
}
