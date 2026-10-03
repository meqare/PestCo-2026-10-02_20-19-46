using UnityEngine;
using UnityEngine.Events;

// Basit can bileşeni. Hasar alır, 0'a inince OnDeath tetiklenir ve obje kapanır.
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [Tooltip("300 böcekte her hasarı loglamak yavaşlatır, testten sonra kapat.")]
    [SerializeField] private bool logDamage = true;
    [SerializeField] private UnityEvent onDeath;

    public bool IsAlive => currentHealth > 0f;
    public float CurrentHealth => currentHealth;
    public UnityEvent OnDeath => onDeath;

    private float currentHealth;

    // Obje her açıldığında canı doldurur (tekrar kullanılan objeler için de çalışır).
    private void OnEnable()
    {
        currentHealth = maxHealth;
    }

    // Canı azaltır, loglar ve can bittiyse ölümü başlatır.
    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsAlive || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (logDamage)
            Debug.Log($"{name}: hasar {amount}, kalan {currentHealth}", this);

        if (currentHealth <= 0f)
            Die();
    }

    // OnDeath olayını tetikler ve objeyi devre dışı bırakır.
    private void Die()
    {
        onDeath?.Invoke();
        gameObject.SetActive(false);
    }
}
