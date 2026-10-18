using UnityEngine;

// Odaların karartmasını yönetir: kapalı odayı shader'a bildirir (PestCoToon, _PC_Room* global'leri)
// ve oyuncuya bağlı dolgu ışıklarını karanlık odada kısar (el feneri her zaman çalışır).
[DefaultExecutionOrder(150)]
public class RoomLightSystem : MonoBehaviour
{
    private const int MaxRooms = 10; // shader'daki dizi boyutu

    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("Kapalı odada yüzeylerin çarpıldığı renk (RGB = ne kadar koyu, nötr gri-mavi). Alfa = renk solması (1 = tamamen gri).")]
    [SerializeField] private Color darkTint = new Color(0.085f, 0.09f, 0.1f, 0.85f);
    [Tooltip("Açma/kapama geçiş süresi (sn).")]
    [SerializeField, Min(0.01f)] private float fadeTime = 0.25f;
    [Tooltip("Karanlık odada oyuncu dolgu ışığının kalan oranı.")]
    [SerializeField, Range(0f, 1f)] private float playerFillInDark = 0.05f;
    [Tooltip("Bu isimleri içeren oyuncu ışıklarına dokunulmaz (el feneri vb.).")]
    [SerializeField] private string[] keepNames = { "Flashlight", "NearLight" };

    private readonly Vector4[] mins = new Vector4[MaxRooms];
    private readonly Vector4[] maxs = new Vector4[MaxRooms];
    private readonly float[] darks = new float[MaxRooms];

    private Light[] fillLights = new Light[0];
    private float[] fillBase = new float[0];

    private static readonly int RoomMinId = Shader.PropertyToID("_PC_RoomMin");
    private static readonly int RoomMaxId = Shader.PropertyToID("_PC_RoomMax");
    private static readonly int RoomDarkId = Shader.PropertyToID("_PC_RoomDark");
    private static readonly int RoomCountId = Shader.PropertyToID("_PC_RoomCount");
    private static readonly int DarkTintId = Shader.PropertyToID("_PC_DarkTint");

    private void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;

        var list = new System.Collections.Generic.List<Light>();
        foreach (var l in player.GetComponentsInChildren<Light>(true))
        {
            if (IsKept(l)) continue;
            list.Add(l);
        }
        fillLights = list.ToArray();
        fillBase = new float[fillLights.Length];
        for (int i = 0; i < fillLights.Length; i++) fillBase[i] = fillLights[i].intensity;
    }

    private bool IsKept(Light l)
    {
        foreach (var n in keepNames) if (l.name.Contains(n)) return true;
        return false;
    }

    private void LateUpdate()
    {
        float step = Time.unscaledDeltaTime / fadeTime;
        int count = Mathf.Min(RoomLight.All.Count, MaxRooms);
        for (int i = 0; i < count; i++)
        {
            var r = RoomLight.All[i];
            r.Darkness = Mathf.MoveTowards(r.Darkness, r.IsOn ? 0f : 1f, step);
            mins[i] = r.BoundsMin;
            maxs[i] = r.BoundsMax;
            darks[i] = r.Darkness;
        }

        Shader.SetGlobalVectorArray(RoomMinId, mins);
        Shader.SetGlobalVectorArray(RoomMaxId, maxs);
        Shader.SetGlobalFloatArray(RoomDarkId, darks);
        Shader.SetGlobalFloat(RoomCountId, count);
        Shader.SetGlobalColor(DarkTintId, darkTint);

        DimPlayerFill();
    }

    // Kamera karanlık bir odadaysa oyuncuya bağlı dolgu ışıklarını kısar.
    private void DimPlayerFill()
    {
        if (fillLights.Length == 0) return;
        if (viewCamera == null) viewCamera = Camera.main;

        float dark = 0f;
        if (viewCamera != null)
        {
            Vector3 p = viewCamera.transform.position;
            foreach (var r in RoomLight.All)
                if (r.Contains(p) && r.Darkness > dark) dark = r.Darkness;
        }

        float mul = Mathf.Lerp(1f, playerFillInDark, dark);
        for (int i = 0; i < fillLights.Length; i++)
            if (fillLights[i] != null) fillLights[i].intensity = fillBase[i] * mul;
    }

    // Çıkarken dolgu ışıklarını eski değerine döndür ve karartmayı sıfırla.
    private void OnDisable()
    {
        for (int i = 0; i < fillLights.Length; i++)
            if (fillLights[i] != null) fillLights[i].intensity = fillBase[i];
        Shader.SetGlobalFloat(RoomCountId, 0f);
    }
}
