using UnityEngine;

public enum PropertyKind { Weight, Heat, Sound }

// Mutfak nesnesinin ozellikleri: Weight (kutle), Heat (sicaklik), Sound (ses degeri).
// Bagimsiz bir bilesen: vakum/karakter kodu bunu bilmek zorunda degil.
// Weight Rigidbody.mass ile senkron tutulur; Heat ve Sound simdilik sadece saklanir.
[DisallowMultipleComponent]
public class KitchenObjectProperties : MonoBehaviour
{
    [Header("Weight (kg, Rigidbody.mass ile eslenir)")]
    [SerializeField] private float weightMin = 0.05f;
    [SerializeField] private float weightMax = 50f;
    [SerializeField] private float weight = 1f;

    [Header("Heat (derece)")]
    [SerializeField] private float heatMin = 0f;
    [SerializeField] private float heatMax = 200f;
    [SerializeField] private float heat = 20f;

    [Header("Sound (0-100 ses degeri)")]
    [SerializeField] private float soundMin = 0f;
    [SerializeField] private float soundMax = 100f;
    [SerializeField] private float sound = 0f;

    private Rigidbody body;

    public float Weight => weight;
    public float Heat => heat;
    public float Sound => sound;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
            Debug.LogWarning($"KitchenObjectProperties ({name}): Rigidbody yok, Weight yalnizca saklanir.", this);
        ApplyWeight();
    }

    // Bileseni ilk eklediginde (veya Reset'te) mevcut Rigidbody kutlesini baslangic agirligi yapar;
    // boylece nesnenin vakumla davranisi degismez.
    private void Reset()
    {
        var rbOnAdd = GetComponent<Rigidbody>();
        if (rbOnAdd != null) weight = Clamp(rbOnAdd.mass, weightMin, weightMax);
    }

    // Inspector'da degerler degisince sinirlari uygular ve kutleyi gunceller.
    private void OnValidate()
    {
        SanitizeRanges();
        weight = Clamp(weight, weightMin, weightMax);
        heat = Clamp(heat, heatMin, heatMax);
        sound = Clamp(sound, soundMin, soundMax);
        if (body == null) body = GetComponent<Rigidbody>();
        ApplyWeight();
    }

    public void SetWeight(float value)
    {
        weight = Clamp(value, weightMin, weightMax);
        ApplyWeight();
    }

    public void SetHeat(float value)
    {
        heat = Clamp(value, heatMin, heatMax);
    }

    public void SetSound(float value)
    {
        sound = Clamp(value, soundMin, soundMax);
    }

    public float GetValue(PropertyKind kind)
    {
        switch (kind)
        {
            case PropertyKind.Weight: return weight;
            case PropertyKind.Heat: return heat;
            default: return sound;
        }
    }

    public float GetMin(PropertyKind kind) => kind == PropertyKind.Weight ? weightMin : kind == PropertyKind.Heat ? heatMin : soundMin;
    public float GetMax(PropertyKind kind) => kind == PropertyKind.Weight ? weightMax : kind == PropertyKind.Heat ? heatMax : soundMax;

    // Kaynaktan en fazla 'amount' kadar alır; alt sınırın altına inmez. Gerçekte alınan miktarı döndürür.
    public float Take(PropertyKind kind, float amount)
    {
        if (amount <= 0f) return 0f;
        float current = GetValue(kind);
        float taken = Mathf.Min(amount, Mathf.Max(0f, current - GetMin(kind)));
        if (taken <= 0f) return 0f;
        SetValue(kind, current - taken);
        return taken;
    }

    // Hedefe en fazla 'amount' kadar ekler; üst sınırı aşmaz. Gerçekte eklenen miktarı döndürür.
    public float Give(PropertyKind kind, float amount)
    {
        if (amount <= 0f) return 0f;
        float current = GetValue(kind);
        float given = Mathf.Min(amount, Mathf.Max(0f, GetMax(kind) - current));
        if (given <= 0f) return 0f;
        SetValue(kind, current + given);
        return given;
    }

    private void SetValue(PropertyKind kind, float value)
    {
        switch (kind)
        {
            case PropertyKind.Weight: SetWeight(value); break;
            case PropertyKind.Heat: SetHeat(value); break;
            default: SetSound(value); break;
        }
    }

    private void ApplyWeight()
    {
        if (body != null) body.mass = weight;
    }

    // Min > max girilirse degerleri takas eder; negatif kutle engellenir.
    private void SanitizeRanges()
    {
        weightMin = Mathf.Max(0.01f, weightMin);
        if (weightMax < weightMin) weightMax = weightMin;
        if (heatMax < heatMin) heatMax = heatMin;
        if (soundMax < soundMin) soundMax = soundMin;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (float.IsNaN(value)) return min;
        return Mathf.Clamp(value, min, max);
    }
}
