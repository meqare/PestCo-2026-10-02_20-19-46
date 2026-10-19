using System.Collections.Generic;
using UnityEngine;

// Vakumun üstündeki küçük cam kapsül: vakumla çekilen baygın CageableBug'lar burada taşınır,
// içlerinde küçük kopyaları döner. Kafese bakıp E'ye basınca BugCage hepsini alır.
public class VacuumCapsule : MonoBehaviour
{
    [SerializeField, Min(1)] private int capacity = 4;
    [Tooltip("Küçük böcek kopyalarının dizileceği nokta (kapsülün ortası).")]
    [SerializeField] private Transform slotRoot;
    [Tooltip("Küçük kopyanın en büyük boyutu (m).")]
    [SerializeField, Min(0.01f)] private float miniSize = 0.045f;
    [Tooltip("Kopyalar arası mesafe (m, slotRoot'un X ekseninde).")]
    [SerializeField, Min(0.005f)] private float slotSpacing = 0.045f;
    [SerializeField] private float spinSpeed = 60f;

    private readonly List<CageableBug> stored = new List<CageableBug>();
    private readonly List<Transform> minis = new List<Transform>();

    public int Count => stored.Count;
    public int Capacity => capacity;
    public bool IsFull => stored.Count >= capacity;

    private void Update()
    {
        for (int i = 0; i < minis.Count; i++)
            if (minis[i] != null)
                minis[i].localRotation = Quaternion.Euler(0f, Time.time * spinSpeed + i * 90f, 0f);
    }

    public bool TryStore(CageableBug bug)
    {
        if (bug == null || IsFull) return false;
        stored.Add(bug);
        minis.Add(MakeMini(bug));
        bug.OnStoredInCapsule();
        Layout();
        return true;
    }

    public int CountOf(BugData type)
    {
        int n = 0;
        foreach (CageableBug bug in stored)
            if (bug != null && bug.Data == type) n++;
        return n;
    }

    // Kapsüldeki ilk böceğin türü (boş bölme bunu sahiplenir).
    public BugData FirstType()
    {
        foreach (CageableBug bug in stored)
            if (bug != null && bug.Data != null) return bug.Data;
        return null;
    }

    // Verilen türden en fazla max böceği kapsülden çıkarır (kopyaları silinir).
    public List<CageableBug> Take(BugData type, int max)
    {
        var taken = new List<CageableBug>();
        for (int i = stored.Count - 1; i >= 0 && taken.Count < max; i--)
        {
            if (stored[i] != null && stored[i].Data != type) continue;
            if (stored[i] != null) taken.Add(stored[i]);
            stored.RemoveAt(i);
            if (minis[i] != null) Destroy(minis[i].gameObject);
            minis.RemoveAt(i);
        }
        Layout();
        return taken;
    }

    // Böceğin modelinden sadece görüntüsü kalan küçük bir kopya yapar.
    private Transform MakeMini(CageableBug bug)
    {
        Transform parent = slotRoot != null ? slotRoot : transform;
        var holder = new GameObject("Mini_" + bug.name).transform;
        holder.SetParent(parent, false);
        if (bug.Visual == null) return holder;

        GameObject copy = Instantiate(bug.Visual.gameObject, holder);
        copy.transform.localPosition = Vector3.zero;
        foreach (Component c in copy.GetComponentsInChildren<Component>(true))
            if (!(c is Transform) && !(c is MeshFilter) && !(c is Renderer)) Destroy(c);

        int layer = parent.gameObject.layer;
        foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;

        Renderer[] renderers = copy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return holder;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (biggest > 0.0001f) copy.transform.localScale *= miniSize / biggest;

        // Kopyanın ortası tutucunun merkezine gelsin (dönerken yerinde dönsün).
        b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        copy.transform.position += holder.position - b.center;
        return holder;
    }

    private void Layout()
    {
        float start = -(minis.Count - 1) * 0.5f * slotSpacing;
        for (int i = 0; i < minis.Count; i++)
            if (minis[i] != null)
                minis[i].localPosition = new Vector3(start + i * slotSpacing, 0f, 0f);
    }
}
