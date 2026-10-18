using UnityEngine;
using UnityEngine.InputSystem;

// Repo tarzı fizik tutma sisteminin tüm ayarları. GrabController ve GrabbableObject buradan okur.
[CreateAssetMenu(fileName = "GrabSettings", menuName = "PEST CO/Grab Settings")]
public class GrabSettings : ScriptableObject
{
    [Header("Girdi")]
    [Tooltip("Tutarken bırakma tuşu (tutma E/etkileşim ile başlar).")]
    public Key releaseKey = Key.E;
    [Tooltip("Tutulan eşyayı fırlatır.")]
    public Key throwKey = Key.Q;

    [Header("Tutma Noktası")]
    [Tooltip("Yakalama menzili PlayerInteraction.interactDistance'tır. Tutma noktasının kameraya uzaklığı:")]
    [Min(0.3f)] public float holdDistance = 1.8f;
    [Tooltip("Tutma noktasının kamera hizasına göre dikey kayması (negatif = biraz aşağıda).")]
    public float holdHeightOffset = -0.25f;
    [Tooltip("Tutma noktası kameranın dönüşünü bu hızla takip eder. Düşük = eşya daha gecikmeli döner.")]
    [Min(0.1f)] public float holdPointFollowSharpness = 12f;
    [Tooltip("Tutma noktası duvara bu yarıçapla küre taraması yapar; duvarın arkasına geçmez.")]
    [Min(0f)] public float holdPointProbeRadius = 0.2f;

    [Header("Yay - Sönümleme (pozisyon)")]
    [Tooltip("referenceMass kütlesindeki eşya için yay sabiti (N/m).")]
    [Min(0f)] public float spring = 220f;
    [Tooltip("1 = kritik sönüm (salınım yok). <1 hafif yaylanma, >1 ağır/yapışkan.")]
    [Min(0f)] public float dampingRatio = 0.9f;
    [Tooltip("Bu kütle (kg) için ayarlar birebir uygulanır.")]
    [Min(0.01f)] public float referenceMass = 1f;
    [Tooltip("Yay sabiti kütle^üs ile ölçeklenir. 1 = kütleden bağımsız ivme, 0 = sabit kuvvet. Düşük = ağır eşya daha gevşek takip eder.")]
    [Range(0f, 1f)] public float massSpringExponent = 0.55f;
    [Tooltip("Uygulanabilecek en büyük kuvvet = referenceMass için bu ivme x kütle^üs. Ağır eşyayı sınırlar.")]
    [Min(0f)] public float maxAcceleration = 60f;

    [Header("Hız ve Mesafe Sınırları")]
    [Tooltip("referenceMass eşyanın tutulurken ulaşabileceği en yüksek hız (m/s).")]
    [Min(0.1f)] public float maxSpeed = 9f;
    [Tooltip("Ağır eşyanın maxSpeed'i bu oranla düşer: maxSpeed * (refMass/kütle)^bu.")]
    [Range(0f, 1f)] public float massSpeedExponent = 0.35f;
    [Tooltip("Eşya tutma noktasından bu kadar uzaklaşırsa (duvara takıldı vs.) bırakılır.")]
    [Min(0.5f)] public float maxDistance = 2.5f;
    [Tooltip("Uzaktan çekilen eşya bu süre içinde maxDistance içine giremezse (bir şeye takıldı) bırakılır.")]
    [Min(0.1f)] public float pullTimeout = 1.5f;

    [Header("Rotasyon")]
    [Tooltip("Hedef rotasyona dönme yay sabiti (1/s²).")]
    [Min(0f)] public float rotationSpring = 60f;
    [Tooltip("Açısal sönüm (1/s). Titrememesi için yüksek tut.")]
    [Min(0f)] public float rotationDamping = 14f;
    [Tooltip("Tutulurken Rigidbody angularDamping değeri.")]
    [Min(0f)] public float heldAngularDamping = 5f;
    [Tooltip("Tutulurken en yüksek açısal hız (rad/s).")]
    [Min(0.1f)] public float maxAngularSpeed = 10f;

    [Header("Fırlatma")]
    [Tooltip("referenceMass eşyaya verilen impulse (N·s). Ağır eşya daha yavaş uçar.")]
    [Min(0f)] public float throwImpulse = 9f;
    [Tooltip("Fırlatmaya eşyanın o anki hızının ne kadarı eklensin.")]
    [Range(0f, 1f)] public float keepVelocityOnThrow = 0.5f;

    [Header("Oyuncu Yavaşlaması")]
    [Tooltip("Bu kütlenin altındaki toplam taşınan kütle oyuncuyu yavaşlatmaz.")]
    [Min(0f)] public float freeCarryMass = 1f;
    [Tooltip("Toplam kütle bu değere ulaşınca oyuncu hızı minSpeedMultiplier olur.")]
    [Min(0.01f)] public float heavyCarryMass = 25f;
    [Range(0.05f, 1f)] public float minSpeedMultiplier = 0.4f;
    [Tooltip("X: (kütle - free) / (heavy - free), Y: 0 = hız 1, 1 = minSpeedMultiplier.")]
    public AnimationCurve speedPenaltyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Oyuncuyla Çarpışma")]
    [Tooltip("Tutulmayan eşyaya yürüyünce uygulanan itme (CharacterController normalde itmez).")]
    [Min(0f)] public float playerPushStrength = 1.5f;
    [Tooltip("Bıraktıktan sonra oyuncu çarpışmasının geri açılma gecikmesi (içinde sıkışıp fırlamasın).")]
    [Min(0f)] public float restoreCollisionDelay = 0.4f;

    [Header("Kaybolmama")]
    [Tooltip("Eşya bu yüksekliğin altına düşerse spawn noktasına döner.")]
    public float killY = -20f;

    // Kütleye göre yay sabiti: spring * (m/ref)^üs (referenceMass'ta spring'e eşit).
    public float SpringFor(float mass) => spring * Mathf.Pow(mass / referenceMass, massSpringExponent);

    // Kritik sönüm 2*sqrt(k*m); dampingRatio ile ölçeklenir.
    public float DamperFor(float mass, float k) => 2f * Mathf.Sqrt(k * mass) * dampingRatio;

    public float MaxForceFor(float mass) => maxAcceleration * referenceMass * Mathf.Pow(mass / referenceMass, massSpringExponent);

    public float MaxSpeedFor(float mass) => maxSpeed * Mathf.Pow(referenceMass / Mathf.Max(mass, 0.01f), massSpeedExponent);

    public float SpeedMultiplierFor(float totalMass)
    {
        if (totalMass <= freeCarryMass) return 1f;
        float t = Mathf.Clamp01((totalMass - freeCarryMass) / Mathf.Max(0.01f, heavyCarryMass - freeCarryMass));
        return Mathf.Lerp(1f, minSpeedMultiplier, Mathf.Clamp01(speedPenaltyCurve.Evaluate(t)));
    }
}
