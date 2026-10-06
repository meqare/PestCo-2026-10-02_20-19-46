using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Merkezî "his" yöneticisi: hitstop, ekran sarsıntısı, ses ve partikül.
// Sahneye bir kez konur. Diğer script'ler Juice.PestDeath(...) gibi tek satırla çağırır.
public class JuiceManager : MonoBehaviour
{
    public static JuiceManager Instance { get; private set; }

    // Tek bir olayın tüm his ayarları (Inspector'da düzenlenir).
    [System.Serializable]
    public class JuicePreset
    {
        [Header("Hitstop (0 = kapalı)")]
        [SerializeField, Min(0f)] private float hitstopDuration;
        [SerializeField, Range(0f, 1f)] private float hitstopTimeScale = 1f;

        [Header("Sarsıntı (0 = kapalı)")]
        [SerializeField, Min(0f)] private float shakeIntensity;
        [SerializeField, Min(0f)] private float shakeDuration;

        [Header("Ses ve Partikül (boş bırakılabilir)")]
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField] private ParticleSystem particlePrefab;

        public float HitstopDuration => hitstopDuration;
        public float HitstopTimeScale => hitstopTimeScale;
        public float ShakeIntensity => shakeIntensity;
        public float ShakeDuration => shakeDuration;
        public AudioClip[] Clips => clips;
        public float Volume => volume;
        public ParticleSystem ParticlePrefab => particlePrefab;

        // Varsayılan değerlerle preset oluşturur.
        public JuicePreset(float hitstopDuration, float hitstopTimeScale, float shakeIntensity, float shakeDuration)
        {
            this.hitstopDuration = hitstopDuration;
            this.hitstopTimeScale = hitstopTimeScale;
            this.shakeIntensity = shakeIntensity;
            this.shakeDuration = shakeDuration;
        }
    }

    [Header("Hazır Ayarlar")]
    [SerializeField] private JuicePreset hit = new JuicePreset(0f, 1f, 0.005f, 0.06f);
    [SerializeField] private JuicePreset pestDeath = new JuicePreset(0.04f, 0.08f, 0.012f, 0.08f);
    [SerializeField] private JuicePreset nestDeath = new JuicePreset(0.12f, 0.02f, 0.09f, 0.35f);
    [SerializeField] private JuicePreset playerHurt = new JuicePreset(0f, 1f, 0.14f, 0.4f);

    [Header("Hitstop")]
    [Tooltip("Bir hitstop bittikten sonra bu kadar (gerçek) saniye yenisi gelmez, daha güçlüsü hariç. " +
             "Sürekli sprey sıkarken oyunun takılıp durmasını önler.")]
    [SerializeField, Min(0f)] private float hitstopCooldown = 0.25f;

    [Header("Sarsıntı")]
    [Tooltip("Player > CameraHolder > ShakeHolder > Main Camera hiyerarşisindeki ShakeHolder.")]
    [SerializeField] private Transform shakeHolder;
    [Tooltip("Titreşim hızı. Yüksek = daha sert/hızlı titreme.")]
    [SerializeField, Min(0f)] private float shakeFrequency = 25f;
    [Tooltip("Sarsıntının zamanla sönmesi: 0 = başlangıç, 1 = bitiş.")]
    [SerializeField] private AnimationCurve shakeFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Ses")]
    [SerializeField, Min(1)] private int audioPoolSize = 12;
    [SerializeField, Min(1)] private int maxConcurrentSounds = 6;
    [SerializeField, Min(0f)] private float soundMinDistance = 1f;
    [SerializeField, Min(0f)] private float soundMaxDistance = 25f;

    [Header("Partikül")]
    [SerializeField, Min(1)] private int particlePoolSize = 20;

    // --- Hitstop durumu ---
    private float defaultFixedDeltaTime;
    private Coroutine hitstopRoutine;
    private float activeHitstopScale = 1f; // 1 = şu an hitstop yok
    private float lastHitstopEndTime = -999f;
    private float lastHitstopScale = 1f;

    // --- Sarsıntı durumu ---
    private float shakeIntensity;
    private float shakeDuration;
    private float shakeElapsed;
    private bool shaking;
    private float seedX;
    private float seedY;

    // --- Ses havuzu ---
    private AudioSource[] audioSources;
    private float[] audioStartTimes;

    // --- Partikül havuzu (prefab başına ayrı liste) ---
    private readonly Dictionary<ParticleSystem, List<ParticleSystem>> particlePools = new Dictionary<ParticleSystem, List<ParticleSystem>>();
    private readonly Dictionary<ParticleSystem, int> particleNextIndex = new Dictionary<ParticleSystem, int>();
    private Transform particleParent;

    // Singleton'ı kurar ve ses havuzunu baştan doldurur.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Sahnede birden fazla JuiceManager var, fazlası siliniyor.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        defaultFixedDeltaTime = Time.fixedDeltaTime;
        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(100f, 200f);

        CreateAudioPool();

        particleParent = new GameObject("ParticlePool").transform;
        particleParent.SetParent(transform, false);
    }

    // Obje kapanırsa zamanı ve kamerayı normale döndürür (oyun yavaş kalmasın).
    private void OnDisable()
    {
        if (Instance != this) return;
        StopHitstopNow();
        StopShakeNow();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ================= HAZIR AYARLAR =================

    // Hafif vuruş: küçük sarsıntı + partikül + ses.
    public void Hit(Vector3 pos, Vector3 dir) => PlayPreset(hit, pos, dir);

    // Böcek ölümü: kısa donma + çok hafif sarsıntı.
    public void PestDeath(Vector3 pos, Vector3 dir) => PlayPreset(pestDeath, pos, dir);

    // Yuva ölümü: daha uzun donma + belirgin sarsıntı.
    public void NestDeath(Vector3 pos) => PlayPreset(nestDeath, pos, Vector3.up);

    // Oyuncu hasar aldı: sadece sarsıntı + ses (kameranın yanında çalar).
    public void PlayerHurt()
    {
        Vector3 pos = shakeHolder != null ? shakeHolder.position : transform.position;
        PlayPreset(playerHurt, pos, Vector3.up);
    }

    // Bir preset'in tüm parçalarını (hitstop, sarsıntı, ses, partikül) çalıştırır.
    private void PlayPreset(JuicePreset preset, Vector3 pos, Vector3 dir)
    {
        if (preset == null) return;
        if (preset.HitstopDuration > 0f) Hitstop(preset.HitstopDuration, preset.HitstopTimeScale);
        if (preset.ShakeIntensity > 0f) Shake(preset.ShakeIntensity, preset.ShakeDuration);
        PlaySound(preset.Clips, pos, preset.Volume);
        if (preset.ParticlePrefab != null) Burst(preset.ParticlePrefab, pos, dir);
    }

    // ================= 1) HITSTOP =================

    // Zamanı kısa süre yavaşlatır. Zaten hitstop varken sadece daha güçlüsü (daha düşük timeScale) devralır.
    public void Hitstop(float duration, float timeScale)
    {
        if (duration <= 0f) return;
        timeScale = Mathf.Clamp(timeScale, 0f, 1f);

        // Mevcut hitstop daha güçlü ya da eşitse yenisini yok say (300 böcek = tek donma).
        if (hitstopRoutine != null && timeScale >= activeHitstopScale) return;

        // Az önce bitti: bekleme süresi dolmadıysa sadece daha güçlüsü geçer (sürekli takılma olmasın).
        if (hitstopRoutine == null && Time.unscaledTime < lastHitstopEndTime + hitstopCooldown
            && timeScale >= lastHitstopScale) return;

        if (hitstopRoutine != null) StopCoroutine(hitstopRoutine);
        hitstopRoutine = StartCoroutine(HitstopRoutine(duration, timeScale));
    }

    // Gerçek zamanla bekler (timeScale'den etkilenmez), sonra zamanı normale döndürür.
    private IEnumerator HitstopRoutine(float duration, float timeScale)
    {
        activeHitstopScale = timeScale;
        Time.timeScale = timeScale;
        // Fizik adımı da aynı oranda küçülür; yavaş çekimde fizik takılmaz.
        Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(timeScale, 0.01f);

        yield return new WaitForSecondsRealtime(duration);

        lastHitstopEndTime = Time.unscaledTime;
        lastHitstopScale = timeScale;
        hitstopRoutine = null;
        StopHitstopNow();
    }

    // Zamanı ve fizik adımını hemen normale döndürür.
    private void StopHitstopNow()
    {
        if (hitstopRoutine != null)
        {
            StopCoroutine(hitstopRoutine);
            hitstopRoutine = null;
        }
        activeHitstopScale = 1f;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    // ================= 2) EKRAN SARSINTISI =================

    // ShakeHolder'ı sarsar. Üst üste gelirse o an daha güçlü olan kazanır.
    public void Shake(float intensity, float duration)
    {
        if (intensity <= 0f || duration <= 0f) return;

        // Yeni sarsıntı, mevcudun ŞU ANKİ (sönmüş) gücünden zayıfsa yok say.
        if (shaking && intensity < CurrentShakeStrength()) return;

        shakeIntensity = intensity;
        shakeDuration = duration;
        shakeElapsed = 0f;
        shaking = true;
    }

    // Sarsıntının şu anki gücü (eğriye göre sönmüş hali).
    private float CurrentShakeStrength()
    {
        float t = Mathf.Clamp01(shakeElapsed / shakeDuration);
        return shakeIntensity * shakeFalloff.Evaluate(t);
    }

    // Kamera hareketinden sonra çalışır. Gerçek zaman kullanır ki hitstop sırasında sarsıntı donmasın.
    private void LateUpdate()
    {
        if (!shaking || shakeHolder == null) return;

        shakeElapsed += Time.unscaledDeltaTime;
        if (shakeElapsed >= shakeDuration)
        {
            StopShakeNow();
            return;
        }

        float strength = CurrentShakeStrength();
        float time = Time.unscaledTime * shakeFrequency;
        // PerlinNoise 0..1 verir; -1..1 aralığına çeviriyoruz.
        float x = (Mathf.PerlinNoise(time, seedX) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(time, seedY) - 0.5f) * 2f;
        shakeHolder.localPosition = new Vector3(x, y, 0f) * strength;
    }

    // Sarsıntıyı bitirir ve ShakeHolder'ı tam sıfıra döndürür (kayma kalmaz).
    private void StopShakeNow()
    {
        shaking = false;
        if (shakeHolder != null) shakeHolder.localPosition = Vector3.zero;
    }

    // ================= 3) SES =================

    // Açılışta AudioSource havuzunu bir kez oluşturur.
    private void CreateAudioPool()
    {
        var parent = new GameObject("AudioPool").transform;
        parent.SetParent(transform, false);

        audioSources = new AudioSource[audioPoolSize];
        audioStartTimes = new float[audioPoolSize];
        for (int i = 0; i < audioPoolSize; i++)
        {
            var go = new GameObject("Audio_" + i);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f; // tam 3D
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = soundMinDistance;
            source.maxDistance = soundMaxDistance;
            audioSources[i] = source;
        }
    }

    // Diziden rastgele klip seçip havuzdan bir kaynakla çalar. Çok ses varsa en eskisini keser.
    public void PlaySound(AudioClip[] clips, Vector3 pos, float volume = 1f)
    {
        if (clips == null || clips.Length == 0 || audioSources == null) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        AudioSource source = GetAudioSource();
        source.transform.position = pos;
        source.clip = clip;
        source.volume = volume;
        source.pitch = Random.Range(0.9f, 1.1f);
        source.Play();
        audioStartTimes[System.Array.IndexOf(audioSources, source)] = Time.unscaledTime;
    }

    // Boş kaynak bulur. Aynı anda çalan ses sınırı doluysa en eski çalanı durdurup onu verir.
    private AudioSource GetAudioSource()
    {
        int playingCount = 0;
        int oldestIndex = -1;
        int freeIndex = -1;

        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i].isPlaying)
            {
                playingCount++;
                if (oldestIndex < 0 || audioStartTimes[i] < audioStartTimes[oldestIndex])
                    oldestIndex = i;
            }
            else if (freeIndex < 0)
            {
                freeIndex = i;
            }
        }

        // Sınır aşıldıysa ya da boş kaynak yoksa en eskisini kes.
        if (playingCount >= maxConcurrentSounds || freeIndex < 0)
        {
            audioSources[oldestIndex].Stop();
            return audioSources[oldestIndex];
        }
        return audioSources[freeIndex];
    }

    // ================= 4) PARTİKÜL =================

    // Havuzdan bir partikül alıp verilen yöne bakacak şekilde patlatır. Destroy yok.
    public void Burst(ParticleSystem prefab, Vector3 pos, Vector3 dir)
    {
        if (prefab == null) return;

        ParticleSystem ps = GetParticle(prefab);
        Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : prefab.transform.rotation;
        ps.transform.SetPositionAndRotation(pos, rot);
        ps.gameObject.SetActive(true);
        ps.Clear(true);
        ps.Play(true);
    }

    // Bu prefab'ın havuzundan bitmiş bir partikül verir. Havuz dolana kadar yenisini oluşturur,
    // doluysa sırayla en eskisini yeniden kullanır.
    private ParticleSystem GetParticle(ParticleSystem prefab)
    {
        if (!particlePools.TryGetValue(prefab, out List<ParticleSystem> pool))
        {
            pool = new List<ParticleSystem>(particlePoolSize);
            particlePools[prefab] = pool;
            particleNextIndex[prefab] = 0;
        }

        // Önce oynatması bitmiş olanı ara.
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].IsAlive(true)) return pool[i];
        }

        // Havuz dolmadıysa yeni kopya ekle (her prefab için en fazla particlePoolSize kez).
        if (pool.Count < particlePoolSize)
        {
            ParticleSystem created = Instantiate(prefab, particleParent);
            var main = created.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None; // kendini yok etmesin/kapatmasın
            pool.Add(created);
            return created;
        }

        // Hepsi meşgul: sıradakini (en eskisini) yeniden kullan.
        int index = particleNextIndex[prefab];
        particleNextIndex[prefab] = (index + 1) % pool.Count;
        return pool[index];
    }
}

// Kısayollar: her yerden tek satırla çağır. Örn: Juice.PestDeath(transform.position, hitDir);
// Sahnede JuiceManager yoksa hiçbir şey yapmaz (hata vermez).
public static class Juice
{
    // Hafif vuruş hissi.
    public static void Hit(Vector3 pos, Vector3 dir) { if (JuiceManager.Instance) JuiceManager.Instance.Hit(pos, dir); }

    // Böcek öldü.
    public static void PestDeath(Vector3 pos, Vector3 dir) { if (JuiceManager.Instance) JuiceManager.Instance.PestDeath(pos, dir); }

    // Yuva yok edildi.
    public static void NestDeath(Vector3 pos) { if (JuiceManager.Instance) JuiceManager.Instance.NestDeath(pos); }

    // Oyuncu hasar aldı.
    public static void PlayerHurt() { if (JuiceManager.Instance) JuiceManager.Instance.PlayerHurt(); }

    // Zamanı kısa süre yavaşlat.
    public static void Hitstop(float duration, float timeScale) { if (JuiceManager.Instance) JuiceManager.Instance.Hitstop(duration, timeScale); }

    // Ekranı sars.
    public static void Shake(float intensity, float duration) { if (JuiceManager.Instance) JuiceManager.Instance.Shake(intensity, duration); }

    // Rastgele klip çal (3D).
    public static void PlaySound(AudioClip[] clips, Vector3 pos, float volume = 1f) { if (JuiceManager.Instance) JuiceManager.Instance.PlaySound(clips, pos, volume); }

    // Partikül patlat.
    public static void Burst(ParticleSystem prefab, Vector3 pos, Vector3 dir) { if (JuiceManager.Instance) JuiceManager.Instance.Burst(prefab, pos, dir); }
}
