using System.Collections.Generic;
using UnityEngine;

// Bir odanın ışık durumu: lambalar (pencere ışıkları dahil) + ışık huzmesi gibi yan objeler.
// Anahtar (LightSwitch) bunu açıp kapatır; RoomLightSystem oda sınırlarını okuyup shader'a karartma verir.
public class RoomLight : MonoBehaviour
{
    public static readonly List<RoomLight> All = new List<RoomLight>();

    // LightCuller kapalı ışıkları yeniden açmasın diye burada tutulur.
    private static readonly HashSet<Light> switchedOff = new HashSet<Light>();

    public static bool IsSwitchedOff(Light l) => switchedOff.Contains(l);

    [SerializeField] private string roomName = "Oda";
    [Tooltip("Odanın dünya sınırı (zemin boyutu, yükseklik dahil).")]
    [SerializeField] private Vector3 boundsMin;
    [SerializeField] private Vector3 boundsMax;
    [SerializeField] private Light[] lights;
    [Tooltip("Işık kapanınca gizlenecek objeler (ışık huzmesi vb.).")]
    [SerializeField] private GameObject[] extraObjects;
    [SerializeField] private bool startOn = true;

    public string RoomName => roomName;
    public bool IsOn { get; private set; }
    public Vector3 BoundsMin => boundsMin;
    public Vector3 BoundsMax => boundsMax;

    // 0 = aydınlık, 1 = tam karanlık. RoomLightSystem yumuşak geçişle günceller.
    public float Darkness { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        switchedOff.Clear();
    }

    private void Awake()
    {
        IsOn = startOn;
        Darkness = IsOn ? 0f : 1f;
        Apply();
    }

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);

    public bool Contains(Vector3 p)
    {
        return p.x >= boundsMin.x && p.x <= boundsMax.x
            && p.y >= boundsMin.y && p.y <= boundsMax.y
            && p.z >= boundsMin.z && p.z <= boundsMax.z;
    }

    public void Toggle() => SetOn(!IsOn);

    public void SetOn(bool on)
    {
        IsOn = on;
        Apply();
    }

    private void Apply()
    {
        if (lights != null)
        {
            foreach (var l in lights)
            {
                if (l == null) continue;
                l.enabled = IsOn;
                if (IsOn) switchedOff.Remove(l);
                else switchedOff.Add(l);
            }
        }
        if (extraObjects != null)
            foreach (var go in extraObjects)
                if (go != null) go.SetActive(IsOn);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOn ? Color.yellow : Color.gray;
        Vector3 c = (boundsMin + boundsMax) * 0.5f;
        Gizmos.DrawWireCube(c, boundsMax - boundsMin);
    }
}
