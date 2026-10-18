using System.Collections;
using UnityEngine;

// Kamyondaki kafesin içinde beslenen böceğin görseli: her beslemede şişer.
// Yeterince şişince kaçış sayacı başlar; süre dolarsa kafesi kırar ve gerçek bir düşmana dönüşür.
public class FedBugVisual : MonoBehaviour
{
    [Tooltip("Şişme eşiğine ulaşınca kafesi kırmasına kalan süre (sn).")]
    public float escapeTimer = 10f;
    [Tooltip("Başlangıç boyunun kaç katına ulaşınca geri sayım başlar (2.5 = 1 → 1.5 → 2 → 2.5).")]
    public float dangerScale = 2.5f;

    public BugData Data { get; private set; }

    private TruckZone owner;
    private Renderer rend;
    private Vector3 groundPoint;
    private Vector3 normalScale;
    private Vector3 targetScale;
    private float remaining;
    private bool counting;
    private bool escaped;

    public void Init(BugData data, TruckZone zone, Vector3 ground)
    {
        Data = data;
        owner = zone;
        groundPoint = ground;
        normalScale = transform.localScale;
        targetScale = normalScale;
        rend = GetComponentInChildren<Renderer>();
        SnapToGround();
    }

    // Böcek zeminde durur; büyüdükçe yukarı doğru şişer (modelin alt noktası zemine oturur).
    private void SnapToGround()
    {
        transform.position = new Vector3(groundPoint.x, transform.position.y, groundPoint.z);
        if (rend != null)
            transform.position += Vector3.up * (groundPoint.y - rend.bounds.min.y);
    }

    // Her beslemede hedef boyut başlangıç boyunun yarısı kadar artar ve döner (ölçeği TruckZone yumuşakça uygular).
    // Eşik aşılınca kaçış geri sayımı başlar (bir kez). Yeni hedef ölçeği döner.
    public Vector3 Grow()
    {
        if (escaped) return targetScale;

        targetScale += normalScale * 0.5f;

        if (!counting && targetScale.x >= normalScale.x * dangerScale - 0.0001f)
        {
            counting = true;
            remaining = escapeTimer;
            Debug.LogWarning("[DANGER] The mutated bug is breaking the cage!");
            if (RadioManager.Instance != null)
                RadioManager.Instance.PlayRadioSubtitle("Cemil! The sensors are going crazy in the back tank! Hit the sale button NOW!");
        }

        return targetScale;
    }

    // Ölçek yumuşakça değişirken model alt noktasıyla zeminde kalsın.
    private void LateUpdate()
    {
        if (!escaped) SnapToGround();
    }

    private void Update()
    {
        if (!counting || escaped) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            escaped = true;
            owner.HandleBugEscaped(this);
        }
    }

    // Boyutu sıfırlar, gerçek bir BugController olarak çıldırtır; oyuncu tekrar yakalayabilir.
    public void BecomeEnemy()
    {
        targetScale = normalScale;
        transform.localScale = normalScale;
        SnapToGround();

        int bugLayer = LayerMask.NameToLayer("Bug");
        if (bugLayer >= 0) gameObject.layer = bugLayer;

        // Vakumun bulabilmesi için trigger collider (modelde yoksa eklenir).
        Collider col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;

        StartCoroutine(EnemyRoutine());
    }

    private IEnumerator EnemyRoutine()
    {
        var bug = gameObject.AddComponent<BugController>();
        bug.bugData = Data;
        bug.wanderRadius = 3f;

        // BugController.Start bir sonraki karede çalışır; horror ondan sonra verilmeli.
        yield return null;
        bug.TriggerHorrorMode();
        Destroy(this);
    }
}
