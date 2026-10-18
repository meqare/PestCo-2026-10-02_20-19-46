using System.Collections;
using UnityEngine;

/// <summary>
/// Menteşeli dolap kapağı. Bu nesne menteşe noktasında durur; kapağın parçaları (kapak, panel, tutamak) onun çocuğudur.
/// Oyuncu E ile açar/kapatır; haşereler <see cref="Open"/> ve <see cref="Close"/> ile açıp içine saklanabilir.
/// </summary>
public class CabinetDoor : MonoBehaviour, IInteractable, IOpenableFurniture
{
    [Header("Hareket")]
    [Tooltip("Açıkken menteşe etrafındaki dönüş (derece, işaretli: kapak odaya doğru açılacak şekilde ayarlanır).")]
    [SerializeField] private float _openAngle = 100f;
    [SerializeField, Min(0.05f)] private float _duration = 0.4f;

    [Header("Etkileşim")]
    [SerializeField] private string _displayName = "Dolabı";
    [Tooltip("Bu kapağın arkasında haşere saklanabiliyorsa, saklanma noktası (oyuncu açınca haşere fırlar).")]
    [SerializeField] private CabinetHideSpot _hideSpot;

    [Header("Ses")]
    [SerializeField, Range(0f, 1f)] private float _soundVolume = 0.45f;
    [SerializeField, Min(0.1f)] private float _maxDistance = 8f;

    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private Coroutine _animation;

    /// <inheritdoc/>
    public bool IsOpen { get; private set; }

    /// <inheritdoc/>
    public float OpenDuration => _duration;

    /// <summary>Kapalı ve açık duruş dönüşlerini saklar (menteşe ekseni: ebeveynin dikey ekseni).</summary>
    private void Awake()
    {
        KinematicBody.Ensure(gameObject); // dönen kapak collider'ı statik sayılmasın (FPS)
        _closedRotation = transform.localRotation;
        _openRotation = Quaternion.AngleAxis(_openAngle, Vector3.up) * _closedRotation;
    }

    /// <summary>Menteşe açısını ve saklanma noktasını kurulum aracının ayarlayabilmesi için verir.</summary>
    public void Configure(float openAngle, string displayName, CabinetHideSpot hideSpot)
    {
        _openAngle = openAngle;
        _displayName = displayName;
        _hideSpot = hideSpot;
    }

    /// <summary>Oyuncuya gösterilen etkileşim yazısı.</summary>
    public string GetPrompt() => IsOpen ? $"[E] {_displayName} Kapat" : $"[E] {_displayName} Aç";

    /// <summary>Oyuncu E'ye basınca açar/kapatır; içinde saklanan haşere varsa onu ortaya çıkarıp kaçırır.</summary>
    public void Interact(PlayerInteraction player)
    {
        bool willOpen = !IsOpen;
        SetOpen(willOpen);

        if (willOpen && _hideSpot != null) _hideSpot.ExposeOccupant();
    }

    /// <inheritdoc/>
    public void Open() => SetOpen(true);

    /// <inheritdoc/>
    public void Close() => SetOpen(false);

    /// <summary>Durumu değiştirir, sesi çalar ve animasyonu başlatır.</summary>
    private void SetOpen(bool open)
    {
        if (IsOpen == open) return;
        IsOpen = open;

        AnomalySounds.PlayAt(open ? AnomalySounds.DoorOpen : AnomalySounds.DoorClose,
            transform.position, _soundVolume, open ? 1.3f : 1.15f, _maxDistance);

        if (_animation != null) StopCoroutine(_animation);
        _animation = StartCoroutine(Swing(open ? _openRotation : _closedRotation));
    }

    /// <summary>Kapağı mevcut açısından hedefe yumuşakça (SmoothStep) döndürür.</summary>
    private IEnumerator Swing(Quaternion target)
    {
        Quaternion start = transform.localRotation;
        for (float t = 0f; t < _duration; t += Time.deltaTime)
        {
            transform.localRotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, t / _duration));
            yield return null;
        }
        transform.localRotation = target;
        _animation = null;
    }
}
