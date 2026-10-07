using UnityEngine;
using UnityEngine.Events;

// Oyuncunun canı ve geçici yavaşlama efekti (örümcek ağı). Ölünce sadece olay tetiklenir, oyuncu kapanmaz.
public class PlayerStatus : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private UnityEvent onDeath;

    public bool IsAlive => health > 0f;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public UnityEvent OnDeath => onDeath;
    // PlayerController hızını bununla çarpar: yavaşlama yokken 1.
    public float SpeedMultiplier => Time.time < slowUntil ? slowMultiplier : 1f;

    private float health;
    private float slowUntil;
    private float slowMultiplier = 1f;

    private void Awake()
    {
        health = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsAlive || amount <= 0f) return;

        health = Mathf.Max(0f, health - amount);
        if (health <= 0f)
        {
            Debug.LogWarning("Oyuncu öldü (PlayerStatus).", this);
            onDeath?.Invoke();
        }
    }

    // Hızı duration saniye boyunca multiplier ile çarpar (0.5 = %50). Tekrar değince süre yenilenir.
    public void ApplySlow(float multiplier, float duration)
    {
        slowMultiplier = Time.time < slowUntil ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
    }
}
