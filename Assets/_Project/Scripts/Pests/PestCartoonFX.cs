using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Oynanabilir haşereye çılgın çizgi film hali verir: dengesiz idle titremesi, zıplama/iniş geri tepmesi (overshoot),
// büyüyen gözler + dönen göz bebekleri + keskin kaşlar + sırıtış/dil/diş, çizgi film partikülleri, "tık" sesleri,
// öldürme zafer pozu ve hareketle kalınlaşan kontur.
// Sadece "Rig" altındaki görseli oynatır; PestMovement'a dokunmaz. Kapatınca (bileşen ya da F1) normal haşere görünümü geri gelir.
[DisallowMultipleComponent]
public class PestCartoonFX : MonoBehaviour
{
    [Header("Açma / Kapama")]
    [SerializeField] private bool crazyMode = true;
    [Tooltip("Çılgın / normal görünümü çalışma anında değiştirir.")]
    [SerializeField] private Key compareKey = Key.F1;
    [Tooltip("G tuşu ile zafer pozunu dene.")]
    [SerializeField] private bool testVictoryKey = true;

    [Header("Referanslar")]
    [SerializeField] private PestMovement movement;
    [Tooltip("Tüm görseli (gövde + yüz) taşıyan nesne. Efektler bunu oynatır.")]
    [SerializeField] private Transform rig;
    [SerializeField] private Transform face;
    [SerializeField] private Transform eyeL, eyeR, pupilL, pupilR;
    [SerializeField] private Transform browL, browR;
    [SerializeField] private Transform mouth, teeth, tongue;
    [SerializeField] private Renderer bodyRenderer;

    [Header("Partiküller (isteğe bağlı)")]
    [SerializeField] private ParticleSystem smoke;
    [SerializeField] private ParticleSystem stars;
    [SerializeField] private ParticleSystem speedLines;
    [SerializeField] private ParticleSystem spiral;
    [SerializeField] private ParticleSystem icons;

    [Header("Ses")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tik;
    [SerializeField] private AudioClip pop;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.6f;

    [Header("Duruş / Titreme")]
    [SerializeField] private float swayDegrees = 4f;
    [SerializeField] private float shakeDegrees = 3.5f;
    [Tooltip("Rastgele 'patlama' sarsıntısı aralığı (sn).")]
    [SerializeField] private Vector2 burstInterval = new Vector2(3f, 6f);

    [Header("Geri tepme / Zıplama")]
    [SerializeField] private float jumpStretch = 0.35f;
    [SerializeField] private float landSquash = 0.45f;
    [SerializeField] private float springFrequency = 9f;
    [Tooltip("Düşük = daha fazla overshoot (boing).")]
    [SerializeField, Range(0.05f, 1f)] private float springDamping = 0.22f;

    [Header("Kontur")]
    [SerializeField] private float baseOutlinePixels = 3.5f;
    [SerializeField] private float maxOutlinePixels = 9f;

    private struct Spring
    {
        public float x, v;
        public void Step(float target, float freq, float damp, float dt)
        {
            float a = freq * freq * (target - x) - 2f * damp * freq * v;
            v += a * dt;
            x += v * dt;
        }
    }

    private Spring squash;   // + = dikey uzama, - = ezilme
    private Spring tilt;     // öne/arkaya geri tepme (derece)
    private float burst;     // 0-1, patlama sarsıntısı
    private float nextBurst;
    private float tongueTimer;
    private float nextTongue = 2f;
    private float teethAmount;
    private float seed;
    private float pupilAngle;
    private float victoryTimer = -1f;
    private int lastKilled = -1;
    private bool wasCrazy;

    private Vector3 rigBasePos, faceBasePos;
    private Vector3 eyeLBase, eyeRBase, tongueBase, mouthBase, teethBase;
    private Vector3 browLBasePos, browRBasePos, pupilLBasePos, pupilRBasePos;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private JobManager jobManager;

    private static readonly int OutlineId = Shader.PropertyToID("_OutlinePixels");
    private static readonly int GradY0Id = Shader.PropertyToID("_GradY0");
    private static readonly int GradHId = Shader.PropertyToID("_GradH");

    private void Awake()
    {
        if (movement == null) movement = GetComponent<PestMovement>();
        seed = Random.value * 100f;
        block = new MaterialPropertyBlock();
        if (rig != null) { rigBasePos = rig.localPosition; renderers = rig.GetComponentsInChildren<Renderer>(true); }
        if (face != null) faceBasePos = face.localPosition;
        Cache(eyeL, ref eyeLBase); Cache(eyeR, ref eyeRBase);
        Cache(tongue, ref tongueBase); Cache(mouth, ref mouthBase); Cache(teeth, ref teethBase);
        if (browL != null) browLBasePos = browL.localPosition;
        if (browR != null) browRBasePos = browR.localPosition;
        if (pupilL != null) pupilLBasePos = pupilL.localPosition;
        if (pupilR != null) pupilRBasePos = pupilR.localPosition;
        ScheduleBurst();
    }

    private static void Cache(Transform t, ref Vector3 scale) { if (t != null) scale = t.localScale; }

    private void OnEnable()
    {
        if (movement != null) { movement.Jumped += OnJumped; movement.Landed += OnLanded; }
        wasCrazy = false;
    }

    private void Start()
    {
        if (!crazyMode) ResetToNormal();
        jobManager = JobManager.Instance;
        if (jobManager != null) jobManager.OnPestCountChanged += OnPestCountChanged;
    }

    private void OnDisable()
    {
        if (movement != null) { movement.Jumped -= OnJumped; movement.Landed -= OnLanded; }
        ResetToNormal();
    }

    private void OnDestroy()
    {
        if (jobManager != null) jobManager.OnPestCountChanged -= OnPestCountChanged;
    }

    // Öldürülen sayı artınca zafer pozu (ilk gelen değer sadece başlangıç sayılır).
    private void OnPestCountChanged(int killed, int total)
    {
        if (lastKilled >= 0 && killed > lastKilled) PlayVictory();
        lastKilled = killed;
    }

    public void SetCrazy(bool value) { crazyMode = value; }

    // Başka scriptlerden de çağrılabilir (ör. haşere yakalanınca).
    public void PlayVictory()
    {
        if (!crazyMode || !isActiveAndEnabled) return;
        victoryTimer = 0f;
        tongueTimer = 1.2f;
    }

    private void OnJumped()
    {
        if (!crazyMode) return;
        squash.v += jumpStretch * 14f;
        tilt.v += -60f;
        Emit(stars, 3);
        Tik(1.15f);
    }

    private void OnLanded(float fallSpeed)
    {
        if (!crazyMode || fallSpeed < 1.2f) return;
        float k = Mathf.Clamp01(fallSpeed / 6f);
        squash.v += -landSquash * 18f * k;
        tilt.v += 80f * k;
        Emit(stars, 2 + (int)(4 * k));
        if (k > 0.5f) Emit(spiral, 1);
        Tik(0.85f);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (compareKey != Key.None && kb[compareKey].wasPressedThisFrame) crazyMode = !crazyMode;
            if (testVictoryKey && kb.gKey.wasPressedThisFrame) PlayVictory();
        }

        if (!crazyMode)
        {
            if (wasCrazy) ResetToNormal();
            wasCrazy = false;
            return;
        }
        if (!wasCrazy) { SetFxVisible(true); wasCrazy = true; }
        if (rig == null) return;

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        float t = Time.time + seed;
        bool sprint = movement != null && movement.IsSprinting;
        float speed01 = movement != null ? Mathf.Clamp01(movement.PlanarSpeed / 1.8f) : 0f;
        bool victory = victoryTimer >= 0f;
        bool frozen = victory && victoryTimer > 0.35f;

        // --- Rastgele "patlama" sarsıntısı
        nextBurst -= dt;
        if (nextBurst <= 0f && !victory)
        {
            burst = 1f;
            squash.v += 5f;
            tilt.v += Random.Range(-40f, 40f);
            Emit(stars, 3);
            Tik(1.3f);
            ScheduleBurst();
        }
        burst = Mathf.MoveTowards(burst, 0f, dt * 2.2f);

        // --- Yaylar (overshoot'lu)
        squash.Step(0f, springFrequency, springDamping, dt);
        tilt.Step(0f, springFrequency * 0.9f, springDamping, dt);

        // --- Dengesiz idle: yavaş sallanma + patlamaya yaklaşırken artan titreme
        float tension = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 0.9f), 3f);
        float calm = frozen ? 0f : Mathf.Lerp(1f, 0.45f, speed01);
        float shakeAmp = (0.5f + shakeDegrees * tension + 4f * burst) * calm;
        float roll = (Mathf.Sin(t * 1.8f) * swayDegrees + Mathf.Sin(t * 3.1f) * swayDegrees * 0.5f) * calm;
        float pitch = Mathf.Sin(t * 2.2f) * swayDegrees * 0.75f * calm;
        Vector3 shake = new Vector3(Noise(t * 38f, 1f), Noise(t * 41f, 2f), Noise(t * 36f, 3f)) * shakeAmp;

        // --- Zafer pozu: kısa zıplama sonra ani donma
        Vector3 hop = Vector3.zero;
        float poseRoll = 0f, posePitch = 0f;
        if (victory)
        {
            victoryTimer += dt;
            if (victoryTimer < 0.35f)
            {
                float u = victoryTimer / 0.35f;
                hop.y = Mathf.Sin(u * Mathf.PI) * 0.045f;
                squash.x = Mathf.Max(squash.x, Mathf.Sin(u * Mathf.PI) * 0.35f);
            }
            else
            {
                poseRoll = 14f; posePitch = -10f;
                squash.x = Mathf.Lerp(squash.x, 0.12f, dt * 12f); // dondurulmuş hafif şişkin poz
                squash.v = 0f;
            }
            if (victoryTimer < dt * 1.5f)
            {
                Emit(stars, 8); Emit(icons, 3); Emit(spiral, 1);
                Pop();
            }
            if (victoryTimer > 1.0f) victoryTimer = -1f;
        }

        rig.localPosition = rigBasePos + hop;
        rig.localRotation = Quaternion.Euler(pitch + tilt.x + posePitch + shake.x, shake.y, roll + poseRoll + shake.z);
        float s = squash.x;
        rig.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);

        AnimateFace(t, dt, sprint, victory, speed01);
        AnimateParticles(sprint, speed01);
        UpdateOutline(speed01);
    }

    private static float Noise(float x, float y) { return (Mathf.PerlinNoise(x, y) - 0.5f) * 2f; }

    // Gözler büyür, göz bebekleri küçülür/döner, kaşlar keskinleşir, ağız sırıtır, ara sıra dil/diş görünür.
    private void AnimateFace(float t, float dt, bool sprint, bool victory, float speed01)
    {
        float excite = Mathf.Clamp01((sprint ? 0.8f : 0f) + (victory ? 1f : 0f) + burst * 0.8f + tension01(t) * 0.3f);

        float eyeScale = 1f + 0.35f * excite;
        if (eyeL != null) eyeL.localScale = eyeLBase * eyeScale;
        if (eyeR != null) eyeR.localScale = eyeRBase * eyeScale;

        // Göz bebekleri: heyecanda küçülür; sprint/zaferde döner, normalde titrek bakar.
        bool dizzy = sprint || victory;
        pupilAngle += dt * (dizzy ? 14f : 0f);
        float pupilScale = Mathf.Lerp(0.42f, 0.24f, excite);
        Vector2 wander = new Vector2(Noise(t * 1.3f, 5f), Noise(t * 1.1f, 9f)) * 0.12f;
        Vector2 orbit = dizzy ? new Vector2(Mathf.Cos(pupilAngle), Mathf.Sin(pupilAngle)) * 0.2f : wander;
        SetPupil(pupilL, pupilLBasePos, orbit, pupilScale, 0f);
        SetPupil(pupilR, pupilRBasePos, orbit, pupilScale, 1.7f);

        // Keskin kaşlar: heyecanla daha dik ve yukarıda, ara sıra seğirir.
        float twitch = Mathf.Sin(t * 17f) * 3f * tension01(t);
        float ang = 24f + 14f * excite + twitch;
        if (browL != null) { browL.localRotation = Quaternion.Euler(0, 0, -ang); browL.localPosition = browLBasePos + Vector3.up * 0.004f * excite; }
        if (browR != null) { browR.localRotation = Quaternion.Euler(0, 0, ang); browR.localPosition = browRBasePos + Vector3.up * 0.004f * excite; }

        // Sırıtış: ağız geniş, sprint/zaferde daha geniş ve dişler görünür.
        float grin = 1f + 0.25f * Mathf.Sin(t * 2.4f) * 0.5f + 0.5f * excite;
        if (mouth != null) mouth.localScale = new Vector3(mouthBase.x * grin, mouthBase.y * (1f + 0.6f * excite), mouthBase.z);
        if (teeth != null)
        {
            teethAmount = Mathf.MoveTowards(teethAmount, (dizzy || burst > 0.2f) ? 1f : 0f, dt * 12f);
            teeth.localScale = new Vector3(teethBase.x * grin, teethBase.y * Mathf.Max(teethAmount, 0.001f), teethBase.z);
        }

        // Dil: ara sıra dışarı çıkar, zaferde kesin çıkar.
        tongueTimer -= dt;
        nextTongue -= dt;
        if (nextTongue <= 0f && !victory)
        {
            tongueTimer = 0.55f;
            nextTongue = Random.Range(2.5f, 5f);
        }
        if (tongue != null)
        {
            float target = tongueTimer > 0f ? 1f : 0f;
            float cur = tongue.localScale.y / Mathf.Max(tongueBase.y, 1e-5f);
            cur = Mathf.MoveTowards(cur, target, dt * 9f);
            float wag = tongueTimer > 0f ? Mathf.Sin(t * 20f) * 10f : 0f;
            tongue.localScale = new Vector3(tongueBase.x, tongueBase.y * cur, tongueBase.z);
            tongue.localRotation = Quaternion.Euler(0, 0, wag);
        }

        // Kafa titremesi (yüz grubu) + ani küçük sarsıntılar
        if (face != null)
        {
            float jit = 0.0012f * (0.4f + tension01(t) + burst * 2f);
            face.localPosition = faceBasePos + new Vector3(Noise(t * 45f, 11f), Noise(t * 43f, 12f), 0f) * jit;
        }
    }

    private float tension01(float t) { return Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 0.9f), 3f); }

    private static void SetPupil(Transform p, Vector3 basePos, Vector2 offset, float scale, float phase)
    {
        if (p == null) return;
        p.localPosition = new Vector3(offset.x, offset.y, basePos.z);
        p.localScale = Vector3.one * scale;
    }

    // Sprint: duman puf'ları + hız çizgileri. Dairesel/yıldız şekiller olaylarla (Emit) çıkar.
    private void AnimateParticles(bool sprint, float speed01)
    {
        SetRate(smoke, sprint ? 22f : 0f);
        SetRate(speedLines, sprint ? 14f : 0f);
    }

    private static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var e = ps.emission; e.rateOverTime = rate;
        if (rate > 0f && !ps.isPlaying) ps.Play();
    }

    private static void Emit(ParticleSystem ps, int count) { if (ps != null) ps.Emit(count); }

    // Kontur kalınlığı hareketle değişir; gövdeye yeşil gradyanın zemin referansı yazılır.
    private void UpdateOutline(float speed01)
    {
        if (renderers == null) return;
        float punch = Mathf.Abs(squash.x) * 8f + burst * 3f;
        float px = Mathf.Clamp(baseOutlinePixels + speed01 * 2.5f + punch, baseOutlinePixels, maxOutlinePixels);
        foreach (var r in renderers)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(OutlineId, px);
            if (r == bodyRenderer)
            {
                block.SetFloat(GradY0Id, transform.position.y);
                block.SetFloat(GradHId, 0.1f);
            }
            r.SetPropertyBlock(block);
        }
    }

    private void Tik(float pitch)
    {
        if (audioSource == null || tik == null) return;
        audioSource.pitch = pitch * Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(tik, sfxVolume);
    }

    private void Pop()
    {
        if (audioSource == null) return;
        audioSource.pitch = Random.Range(0.95f, 1.1f);
        audioSource.PlayOneShot(pop != null ? pop : tik, sfxVolume);
    }

    private void ScheduleBurst() { nextBurst = Random.Range(burstInterval.x, burstInterval.y); }

    // Çılgın efektlerin yüz parçalarını gösterir/gizler (normal haşerede sadece gövde + gözler kalır).
    private void SetFxVisible(bool on)
    {
        foreach (var t in new[] { browL, browR, mouth, teeth, tongue })
            if (t != null) t.gameObject.SetActive(on);
        foreach (var ps in new[] { smoke, stars, speedLines, spiral, icons })
            if (ps != null) { if (on) ps.gameObject.SetActive(true); else { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } }
    }

    // Normal haşere görünümü: tüm efektleri sıfırla.
    private void ResetToNormal()
    {
        squash = default; tilt = default; burst = 0f; victoryTimer = -1f;
        if (rig != null)
        {
            rig.localPosition = rigBasePos; rig.localRotation = Quaternion.identity; rig.localScale = Vector3.one;
        }
        if (face != null) face.localPosition = faceBasePos;
        if (eyeL != null && eyeLBase != Vector3.zero) eyeL.localScale = eyeLBase;
        if (eyeR != null && eyeRBase != Vector3.zero) eyeR.localScale = eyeRBase;
        if (pupilL != null) { pupilL.localPosition = pupilLBasePos; pupilL.localScale = Vector3.one * 0.42f; }
        if (pupilR != null) { pupilR.localPosition = pupilRBasePos; pupilR.localScale = Vector3.one * 0.42f; }
        if (renderers != null)
            foreach (var r in renderers) if (r != null && !(r is ParticleSystemRenderer)) r.SetPropertyBlock(null);
        SetFxVisible(false);
    }
}
