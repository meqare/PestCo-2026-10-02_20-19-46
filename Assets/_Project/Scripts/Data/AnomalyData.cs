using UnityEngine;

public enum AnomalyType
{
    WallBreathing,
    GiantRoach,
    BasementShadow,
    HumanVoice,
    DoorClose,
    LightTrick
}

// Tek bir anomalinin ayarları (AnomalyManager listesinde durur).
[CreateAssetMenu(fileName = "Anomaly_New", menuName = "PEST CO/Anomaly Data")]
public class AnomalyData : ScriptableObject
{
    public string anomalyName = "Anomaly";
    public AnomalyType type;

    [Tooltip("İş başladıktan kaç saniye sonra tetiklenir. 0 = AnomalyManager'ın min/max aralığından rastgele " +
             "(olay tabanlı anomalilerde bu süre beklenmez, olay gelince tetiklenir).")]
    [Min(0f)] public float triggerDelay;
    [Tooltip("0'dan büyükse gecikme triggerDelay ile bu değer arasında rastgele seçilir.")]
    [Min(0f)] public float triggerDelayMax;

    [Tooltip("Seçim ağırlığı (0-1). Hepsi 0.12 ise eşit şansla seçilir.")]
    [Range(0f, 1f)] public float rarity = 0.12f;

    [Tooltip("Opsiyonel: boşsa programla üretilen yer tutucu ses çalınır (gerçek sesler sonra eklenecek).")]
    public AudioClip triggerSound;
}
