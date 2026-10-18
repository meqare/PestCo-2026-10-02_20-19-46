using UnityEngine;

// Çizgi film fiziği ayarları. CartoonPhysics buradan okur; kodda sabit değer yok.
[CreateAssetMenu(fileName = "CartoonPhysicsSettings", menuName = "PEST CO/Cartoon Physics Settings")]
public class CartoonPhysicsSettings : ScriptableObject
{
    [Header("Jöle (görsel yay)")]
    [Tooltip("Ezilme/uzama yayının sertliği. Yüksek = hızlı toparlanır.")]
    [Min(1f)] public float jellyStiffness = 220f;
    [Tooltip("Düşük = uzun süre sallanır (jöle), yüksek = çabuk durur.")]
    [Min(0f)] public float jellyDamping = 11f;
    [Tooltip("Her m/s hız için uzama miktarı.")]
    [Min(0f)] public float stretchPerSpeed = 0.02f;
    [Range(0.1f, 2f)] public float maxStretch = 0.3f;
    [Range(0.1f, 0.9f)] public float maxSquash = 0.25f;
    [Tooltip("Çarpma hızı başına ezilme darbesi.")]
    [Min(0f)] public float impactSquashPerSpeed = 8f;

    [Header("Boing (enerji kazanan sekme)")]
    public bool boingEnabled = true;
    [Tooltip("Bu çarpma hızının (m/s) altında seğirmez.")]
    [Min(0f)] public float boingMinSpeed = 2.5f;
    [Tooltip("Çarpmadan sonraki hız = çarpma hızı x bu. Gerçekçi: 0.2-0.4 (enerji kaybeder). 1'in üstü = enerji KAZANIR (absürt).")]
    [Range(0.05f, 2f)] public float boingGain = 0.3f;
    [Tooltip("Arka arkaya en fazla bu kadar enerji kazanan sekme; sonra normal fiziğe döner.")]
    [Min(0)] public int maxBoings = 2;
    [Min(1f)] public float maxBounceSpeed = 5f;
    [Min(0f)] public float boingCooldown = 0.08f;

    [Header("Coyote asılı kalma (zirvede donma)")]
    [Tooltip("Kapalı = gerçekçi. Açık = zirvede donup örs gibi çakılma (absürt).")]
    public bool hangEnabled = false;
    [Tooltip("Zirvede havada donup titrediği süre.")]
    [Min(0.05f)] public float hangTime = 0.5f;
    [Tooltip("Dikey hız bunun altına düşünce zirve sayılır.")]
    [Min(0f)] public float hangApexSpeed = 0.6f;
    [Tooltip("Fırlatıldıktan/seğirdikten en az bu kadar sonra donabilir.")]
    [Min(0f)] public float hangMinAirTime = 0.15f;
    [Tooltip("Asılı kalma bitince başlangıç iniş hızı (m/s).")]
    [Min(0f)] public float slamInitialSpeed = 6f;
    [Tooltip("Çakılırken yerçekimi çarpanı (örs gibi).")]
    [Min(1f)] public float slamGravityMultiplier = 1f;
    [Tooltip("Çakılma çarpmasında ekran sarsıntısı.")]
    [Min(0f)] public float slamShake = 0.05f;
    [Tooltip("Çakılıp ezildikten sonra yukarı seğirme hızı.")]
    [Min(0f)] public float splatBounceSpeed = 2.5f;

    [Header("Gerilmeli fırlatma (basılı tut)")]
    public bool chargedThrow = true;
    [Tooltip("Tam gerilmeye kaç saniyede ulaşılır.")]
    [Min(0.1f)] public float chargeTime = 0.9f;
    [Tooltip("Tam gerilmede eşya oyuncuya doğru bu kadar geriye çekilir (tutma noktası yakınlaşır).")]
    [Min(0f)] public float chargeBackDistance = 0.9f;
    [Tooltip("Tam gerilmede fırlatma gücü çarpanı (1 = geril-me yok).")]
    [Min(1f)] public float maxChargeMultiplier = 1.6f;
    [Range(0f, 0.8f)] public float chargeSquash = 0.15f;
    [Min(0f)] public float chargeQuiver = 0.02f;
}
