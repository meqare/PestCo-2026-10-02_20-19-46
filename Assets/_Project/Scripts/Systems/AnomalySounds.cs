using System;
using UnityEngine;

// Yer tutucu anomali sesleri: projede ses dosyası olmadığı için programla üretilir (düşük frekanslı gürültü, vb.).
// Gerçek sesler eklenince AnomalyData.triggerSound doldurulur, bu sınıf yedek olarak kalır.
public static class AnomalySounds
{
    private const int Rate = 22050;

    private static AudioClip breath, whisper, hiss, wind, lockClip, crackle, doorOpen, doorClose, doorLocked;

    public static AudioClip Breath => breath != null ? breath : (breath = Generate("AnomBreath", 2.6f, BreathGen));
    public static AudioClip Whisper => whisper != null ? whisper : (whisper = Generate("AnomWhisper", 5f, WhisperGen));
    public static AudioClip Hiss => hiss != null ? hiss : (hiss = Generate("AnomHiss", 1.6f, HissGen));
    public static AudioClip Wind => wind != null ? wind : (wind = Generate("AnomWind", 5f, WindGen));
    public static AudioClip Lock => lockClip != null ? lockClip : (lockClip = Generate("AnomLock", 0.5f, LockGen));
    public static AudioClip DoorOpen => doorOpen != null ? doorOpen : (doorOpen = Generate("DoorOpen", 0.55f, DoorOpenGen));
    public static AudioClip DoorClose => doorClose != null ? doorClose : (doorClose = Generate("DoorClose", 0.35f, DoorCloseGen));
    public static AudioClip DoorLocked => doorLocked != null ? doorLocked : (doorLocked = Generate("DoorLocked", 0.3f, DoorLockedGen));
    public static AudioClip Crackle => crackle != null ? crackle : (crackle = Generate("AnomCrackle", 0.3f, CrackleGen));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        breath = whisper = hiss = wind = lockClip = crackle = doorOpen = doorClose = doorLocked = null;
    }

    // Verilen konumda 3D tek seferlik ses çalar; bitince obje silinir.
    public static AudioSource PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 15f, bool spatial = true)
    {
        if (clip == null) return null;
        var go = new GameObject("AnomalySound");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = spatial ? 1f : 0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = maxDistance;
        src.Play();
        UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch)) + 0.25f);
        return src;
    }

    private static AudioClip Generate(string name, float seconds, Action<float[], int> fill)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        fill(data, n);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Clamp(float v) => Mathf.Clamp(v, -1f, 1f);

    // Düşük frekanslı nefes: alçak geçiren filtre uygulanmış gürültü, yavaş yükselip alçalan zarf.
    private static void BreathGen(float[] d, int n)
    {
        var rnd = new System.Random(11);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, len = n / (float)Rate;
            float noise = (float)(rnd.NextDouble() * 2 - 1);
            y += 0.035f * (noise - y);
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len)), 2f);
            d[i] = Clamp(y * env * 7f);
        }
    }

    // Fısıltı: bant geçiren gürültü, hece gibi hızlı genlik dalgalanması (anlaşılmaz mırıltı).
    private static void WhisperGen(float[] d, int n)
    {
        var rnd = new System.Random(23);
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, len = n / (float)Rate;
            float noise = (float)(rnd.NextDouble() * 2 - 1);
            lp1 += 0.30f * (noise - lp1);
            lp2 += 0.05f * (noise - lp2);
            float band = lp1 - lp2;
            float syll = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 4.7f * t + 2f * Mathf.Sin(2f * Mathf.PI * 0.6f * t)));
            float fade = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((len - t) / 0.6f);
            d[i] = Clamp(band * syll * fade * 4f);
        }
    }

    // Kalın, boğuk hışırtı: alçak uğultu + kısa pürüz patlamaları.
    private static void HissGen(float[] d, int n)
    {
        var rnd = new System.Random(5);
        float low = 0f, rasp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, len = n / (float)Rate;
            float noise = (float)(rnd.NextDouble() * 2 - 1);
            low += 0.02f * (noise - low);
            rasp += 0.35f * (noise - rasp);
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len));
            float burst = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 9f * t);
            d[i] = Clamp((low * 9f + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.25f + (rasp - low) * 0.5f * burst) * env);
        }
    }

    // Rüzgar/uğultu: çok alçak geçiren gürültü, yavaş salınan genlik.
    private static void WindGen(float[] d, int n)
    {
        var rnd = new System.Random(31);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, len = n / (float)Rate;
            float noise = (float)(rnd.NextDouble() * 2 - 1);
            y += 0.012f * (noise - y);
            float mod = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.35f * t);
            float fade = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((len - t) / 1f);
            d[i] = Clamp(y * 14f * mod * fade);
        }
    }

    // Kilitlenme: alçak "tok" vuruş + kısa metalik klik.
    private static void LockGen(float[] d, int n)
    {
        var rnd = new System.Random(3);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float thud = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 14f);
            float click = 0f;
            if (t > 0.12f)
            {
                float tc = t - 0.12f;
                click = (Mathf.Sin(2f * Mathf.PI * 1800f * tc) * 0.5f + (float)(rnd.NextDouble() * 2 - 1) * 0.5f) * Mathf.Exp(-tc * 70f) * 0.6f;
            }
            d[i] = Clamp(thud + click);
        }
    }

    // Kapı açılışı: yavaşça düşen gıcırtı (frekans süpürmesi) + hafif gürültü.
    private static void DoorOpenGen(float[] d, int n)
    {
        var rnd = new System.Random(41);
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, len = n / (float)Rate;
            float freq = Mathf.Lerp(210f, 130f, t / len) + Mathf.Sin(t * 38f) * 8f;
            phase += 2f * Mathf.PI * freq / Rate;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len));
            float noise = (float)(rnd.NextDouble() * 2 - 1) * 0.15f;
            d[i] = Clamp((Mathf.Sin(phase) * 0.35f + Mathf.Sin(phase * 2.01f) * 0.12f + noise) * env);
        }
    }

    // Kapı kapanışı: tok vuruş + kısa mandal tıkı.
    private static void DoorCloseGen(float[] d, int n)
    {
        var rnd = new System.Random(43);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float thud = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 16f) * 0.9f;
            float latch = t > 0.06f ? (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-(t - 0.06f) * 90f) * 0.4f : 0f;
            d[i] = Clamp(thud + latch);
        }
    }

    // Kilitli: iki kısa, kuru tıklama.
    private static void DoorLockedGen(float[] d, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float k1 = Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 40f);
            float t2 = t - 0.12f;
            float k2 = t2 > 0f ? Mathf.Sin(2f * Mathf.PI * 150f * t2) * Mathf.Exp(-t2 * 40f) : 0f;
            d[i] = Clamp((k1 + k2) * 0.8f);
        }
    }

    // Çok hafif çıtırtı: seyrek, yumuşatılmış tıklar.
    private static void CrackleGen(float[] d, int n)
    {
        var rnd = new System.Random(17);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float impulse = rnd.NextDouble() < 0.0018 ? (rnd.NextDouble() < 0.5 ? 1f : -1f) : 0f;
            y = y * 0.92f + impulse * 0.5f;
            float t = i / (float)Rate, len = n / (float)Rate;
            d[i] = Clamp(y * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len)));
        }
    }
}
