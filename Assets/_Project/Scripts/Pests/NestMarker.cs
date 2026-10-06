using UnityEngine;

// Yuvanın üstünde süzülüp dönen parlak işaret: oyuncu yuvayı uzaktan fark etsin.
// Yuvanın child'ı olmalı; yuva ölünce PestNest renderer'ları kapattığı için işaret de kaybolur.
public class NestMarker : MonoBehaviour
{
    [SerializeField, Min(0f)] private float bobHeight = 0.08f;
    [SerializeField, Min(0f)] private float bobSpeed = 2f;
    [Tooltip("Saniyede dönüş (derece).")]
    [SerializeField] private float spinSpeed = 90f;

    private Vector3 startLocalPos;

    // Başlangıç konumunu saklar (süzülme bunun etrafında olur).
    private void Awake()
    {
        startLocalPos = transform.localPosition;
    }

    // Her karede yukarı-aşağı süzülür ve kendi ekseninde döner.
    private void Update()
    {
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = startLocalPos + Vector3.up * offset;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }
}
