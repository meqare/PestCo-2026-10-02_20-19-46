using UnityEngine;

/// <summary>
/// Single place for Lanetli Yuk balance values. Item prices and masses stay in ItemData.
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Lanetli/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Vacuum")]
    [Tooltip("Base pull force (N).")]
    [Min(0f)] public float pullForce = 60f;
    [Tooltip("Throw force reached after holding for the full charge time.")]
    [Min(0f)] public float maxThrowForce = 20f;
    [Tooltip("Radius around the pointer in which items are pulled.")]
    [Min(0.5f)] public float pullRange = 6f;

    [Header("Tower")]
    [Tooltip("Tower counts as toppled above this axis tilt (degrees).")]
    public float topplingAngleThreshold = 35f;

    [Header("Round (seconds)")]
    [Min(0f)] public float readyDuration = 3f;
    [Min(1f)] public float roundDuration = 60f;
    [Min(0f)] public float breakDuration = 3f;

    [Header("Cursed Vase")]
    [Tooltip("Seconds between delivery and breaking.")]
    [Min(0.1f)] public float vaseBreakDelay = 15f;
    [Tooltip("Impulse applied to nearby bodies when the vase breaks.")]
    [Min(0f)] public float vasePushForce = 2f;

    [Header("Anomaly (placeholder - not wired yet)")]
    [Min(1)] public int anomalyStartRound = 4;
    [Min(0f)] public float anomalyDuration = 2f;
    [Min(0f)] public float anomalyForce = 0f;
}
