using UnityEngine;

[CreateAssetMenu(fileName = "NewBugData", menuName = "PEST CO/Bug Data")]
public class BugData : ScriptableObject
{
    public string bugName;
    public BugState startState = BugState.Cute;
    public float baseValue;
    public float dangerMultiplier;

    public Color cuteColor = Color.white;
    public Color horrorColor = Color.magenta;

    [Tooltip("Envanter çubuğundaki simge (boşsa renge göre piksel böcek simgesi çizilir).")]
    public Sprite icon;
}
