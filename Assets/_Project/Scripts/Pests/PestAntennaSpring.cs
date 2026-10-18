using UnityEngine;

// Antenler için ikincil hareket: baş hareketini bir an geç takip eder, hedefi geçip (overshoot) geri oturur.
// Animator'ın yazdığı pozun ÜSTÜNE LateUpdate'te ek dönüş ekler; kapatınca anten sadece klibi oynar.
// Her zincir elemanı (AntL, AntL2 ...) ayrı yay; uçtaki eklem daha yumuşak/gecikmeli.
[DisallowMultipleComponent]
public class PestAntennaSpring : MonoBehaviour
{
    [Tooltip("Baş kemiği (hız buradan ölçülür). Boşsa 'Head' adlı kemik aranır.")]
    [SerializeField] private Transform head;
    [Tooltip("Zincirler: kökten uca sıralı kemikler. Boşsa AntL/AntL2 ve AntR/AntR2 aranır.")]
    [SerializeField] private Transform[] leftChain;
    [SerializeField] private Transform[] rightChain;

    [Header("Yay")]
    [Tooltip("Sertlik: yüksek = hızlı geri oturur.")]
    [SerializeField, Min(1f)] private float stiffness = 140f;
    [Tooltip("Sönüm: düşük = daha çok overshoot/sallanma. 5-9 arası çizgi film hissi verir.")]
    [SerializeField, Min(0f)] private float damping = 6f;
    [Tooltip("Zincirde her sonraki eklemin sertlik çarpanı (<1 = uç daha gevşek, daha geç).")]
    [SerializeField, Range(0.2f, 1f)] private float tipSoftness = 0.6f;

    [Header("Etki")]
    [Tooltip("Hızın 1 m/s'si kaç derece geriye savrulma yapar.")]
    [SerializeField] private float degreesPerMeterPerSecond = 70f;
    [SerializeField, Range(0f, 90f)] private float maxAngle = 40f;
    [Range(0f, 2f)] public float influence = 1f;

    private class Joint { public Transform t; public Vector3 angle, vel; public float k; }
    private Joint[] left, right;
    private Vector3 lastHeadPos;
    private bool hasLast;

    private void Awake()
    {
        if (head == null) head = FindDeep(transform, "Head");
        if (leftChain == null || leftChain.Length == 0) leftChain = Find(new[] { "AntL", "AntL2" });
        if (rightChain == null || rightChain.Length == 0) rightChain = Find(new[] { "AntR", "AntR2" });
        left = Build(leftChain); right = Build(rightChain);
    }

    private void OnEnable() { hasLast = false; }

    private Transform[] Find(string[] names)
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var n in names) { var t = FindDeep(transform, n); if (t != null) list.Add(t); }
        return list.ToArray();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    private Joint[] Build(Transform[] chain)
    {
        var js = new Joint[chain.Length];
        for (int i = 0; i < chain.Length; i++) js[i] = new Joint { t = chain[i], k = Mathf.Pow(tipSoftness, i) };
        return js;
    }

    private void LateUpdate()
    {
        if (head == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 pos = head.position;
        if (!hasLast) { lastHeadPos = pos; hasLast = true; }
        Vector3 worldVel = (pos - lastHeadPos) / dt;
        lastHeadPos = pos;

        // Baş uzayında hız → savrulma hedefi (hızın tersi yönünde yatma)
        Vector3 localVel = head.InverseTransformDirection(worldVel);
        // Modeli ölçekten bağımsız tutmak için (modelin kendi ölçeği küçük) dünya hızı kullanılır.
        Vector3 target = -localVel * degreesPerMeterPerSecond;
        Step(left, target, dt);
        Step(right, target, dt);
    }

    private void Step(Joint[] chain, Vector3 target, float dt)
    {
        // Büyük dt'de yay kararsızlaşmasın
        int sub = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
        float h = dt / sub;
        for (int i = 0; i < chain.Length; i++)
        {
            var j = chain[i];
            // Uç eklem, bir önceki eklemin açısını da takip eder (zincir gecikmesi)
            Vector3 tgt = i == 0 ? target : target * 0.5f + chain[i - 1].angle * 0.5f;
            for (int s = 0; s < sub; s++)
            {
                Vector3 acc = j.k * stiffness * (tgt - j.angle) - damping * j.vel;
                j.vel += acc * h;
                j.angle += j.vel * h;
            }
            Vector3 a = Vector3.ClampMagnitude(j.angle, maxAngle) * influence;
            if (j.t == null || a.sqrMagnitude < 1e-6f) continue;
            // a = baş uzayında "uç bu yöne yatsın" vektörü (Y yukarı olan anten için eksen = Y x a)
            Vector3 axis = Vector3.Cross(Vector3.up, a);
            if (axis.sqrMagnitude < 1e-8f) continue;
            j.t.localRotation = Quaternion.AngleAxis(a.magnitude, axis.normalized) * j.t.localRotation;
        }
    }
}
