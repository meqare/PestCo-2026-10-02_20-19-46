using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

// FPS düşüşü teşhisi: belirli aralıkla ortalama kare süresini, Profiler sayaçlarını ve oyuncu konumunu Console'a yazar.
// F9 ile açılıp kapanır. Bölgesel düşüşü bulmak için ev içinde gezerken log'daki "pos" ile süreleri karşılaştır.
// Kendi kodunu ölçmek için: PerfProbe.Begin("Ad") ... PerfProbe.End() (Profiler penceresinde görünür).
public class PerfProbe : MonoBehaviour
{
    [SerializeField] private bool autoLog = false;
    [SerializeField, Min(0.5f)] private float logInterval = 2f;
    [Tooltip("Bu süreden (ms) uzun kareler tek tek uyarı olarak yazılır.")]
    [SerializeField, Min(1f)] private float spikeMs = 33f;

    private ProfilerRecorder mainThread, update, lateUpdate, fixedUpdate, cameraRender, gfxWait;
    private ProfilerRecorder batches, setPass, triangles, shadowCasters;
    private bool logging;
    private float accTime, nextLog, worst;
    private int frames;
    private Transform player;

    public static void Begin(string name) => UnityEngine.Profiling.Profiler.BeginSample(name);
    public static void End() => UnityEngine.Profiling.Profiler.EndSample();

    private void OnEnable()
    {
        logging = autoLog;
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        update = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate", 15);
        lateUpdate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", 15);
        fixedUpdate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FixedUpdate.ScriptRunBehaviourFixedUpdate", 15);
        cameraRender = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Camera.Render", 15);
        gfxWait = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", 15);
        batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
        nextLog = Time.unscaledTime + logInterval;
    }

    private void OnDisable()
    {
        mainThread.Dispose(); update.Dispose(); lateUpdate.Dispose(); fixedUpdate.Dispose(); cameraRender.Dispose();
        gfxWait.Dispose(); batches.Dispose(); setPass.Dispose(); triangles.Dispose(); shadowCasters.Dispose();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame)
        {
            logging = !logging;
            Debug.Log("[PERF] log " + (logging ? "açık" : "kapalı"));
        }
        if (!logging) return;

        float dt = Time.unscaledDeltaTime;
        accTime += dt;
        frames++;
        if (dt > worst) worst = dt;
        if (dt * 1000f > spikeMs && Time.frameCount > 60)
            Debug.LogWarning($"[PERF] SPIKE {dt * 1000f:0.0} ms (kare {Time.frameCount})");

        if (Time.unscaledTime < nextLog) return;
        nextLog = Time.unscaledTime + logInterval;

        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        var sb = new StringBuilder("[PERF] ");
        sb.Append($"fps={frames / accTime:0.0} avg={accTime / frames * 1000f:0.0}ms worst={worst * 1000f:0.0}ms | ");
        sb.Append($"main={Ms(mainThread)} update={Ms(update)} late={Ms(lateUpdate)} fixed={Ms(fixedUpdate)} camRender={Ms(cameraRender)} gfxWait={Ms(gfxWait)} | ");
        sb.Append($"batches={batches.LastValue} setPass={setPass.LastValue} tris={triangles.LastValue / 1000}k shadowCasters={shadowCasters.LastValue}");
        if (player != null) sb.Append($" | pos={player.position:0.0}");
        Debug.Log(sb.ToString());

        accTime = 0f; frames = 0; worst = 0f;
    }

    // Son 15 karenin ortalaması (ms).
    private static string Ms(ProfilerRecorder r)
    {
        if (!r.Valid || r.Count == 0) return "n/a";
        double sum = 0;
        int n = r.Count;
        for (int i = 0; i < n; i++) sum += r.GetSample(i).Value;
        return (sum / n / 1e6).ToString("0.0") + "ms";
    }
}
