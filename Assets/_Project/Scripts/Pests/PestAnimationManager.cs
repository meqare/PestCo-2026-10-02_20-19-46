using UnityEngine;

// PestMovement'ın durumunu okuyup hamam böceği modelinin Animator parametrelerine yazar:
//   IsWalking (bool)  : W/A/S/D ile yürüme
//   IsRunning (bool)  : Shift ile koşma
//   IsGrounded (bool) : yerde mi (false = zıplama/düşme)
//   IsClimbing (bool) : duvarda tırmanıyor
//   Speed (float)     : yatay hız (m/s)
//   AnimSpeed (float) : yürüme/koşma klip hızı çarpanı (hızla orantılı)
//   ClimbMove (float) : duvarda hareket girdisi 0-1 (ayaklar sadece hareket ederken oynar)
//   Jump (trigger)    : zıplama anında
// Animator'da olmayan parametreler atlanır (uyarı vermez). Hareket mantığına dokunmaz.
[RequireComponent(typeof(PestMovement))]
[DisallowMultipleComponent]
public class PestAnimationManager : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Bu hızın altı 'duruyor' sayılır (m/s).")]
    [SerializeField, Min(0f)] private float walkThreshold = 0.12f;
    [Tooltip("Yürüyüş klibi 1x hızda oynadığı yatay hız (m/s).")]
    [SerializeField, Min(0.01f)] private float referenceWalkSpeed = 0.6f;
    [Tooltip("CharacterController.isGrounded titremesine karşı: yerden bu kadar süre ayrık kalmadan 'havada' sayılmaz.")]
    [SerializeField, Min(0f)] private float groundedGrace = 0.08f;

    private static readonly int WalkingId = Animator.StringToHash("IsWalking");
    private static readonly int RunningId = Animator.StringToHash("IsRunning");
    private static readonly int GroundedId = Animator.StringToHash("IsGrounded");
    private static readonly int ClimbingId = Animator.StringToHash("IsClimbing");
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int AnimSpeedId = Animator.StringToHash("AnimSpeed");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int ClimbMoveId = Animator.StringToHash("ClimbMove");

    private PestMovement movement;
    private float lastGroundedTime;
    private bool hasWalking, hasRunning, hasGrounded, hasClimbing, hasSpeed, hasAnimSpeed, hasJump, hasClimbMove;

    // Dışarıdan okunabilir (HUD/debug).
    public bool IsWalking { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGrounded { get; private set; } = true;

    private void Awake()
    {
        movement = GetComponent<PestMovement>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        CacheParameters();
    }

    private void OnEnable()
    {
        if (movement != null) movement.Jumped += OnJumped;
        lastGroundedTime = Time.time;
    }

    private void OnDisable()
    {
        if (movement != null) movement.Jumped -= OnJumped;
    }

    private void CacheParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == WalkingId) hasWalking = true;
            else if (p.nameHash == RunningId) hasRunning = true;
            else if (p.nameHash == GroundedId) hasGrounded = true;
            else if (p.nameHash == ClimbingId) hasClimbing = true;
            else if (p.nameHash == SpeedId) hasSpeed = true;
            else if (p.nameHash == AnimSpeedId) hasAnimSpeed = true;
            else if (p.nameHash == JumpId) hasJump = true;
            else if (p.nameHash == ClimbMoveId) hasClimbMove = true;
        }
    }

    private void OnJumped()
    {
        if (animator != null && hasJump) animator.SetTrigger(JumpId);
    }

    private void Update()
    {
        if (animator == null || movement == null) return;

        float speed = movement.PlanarSpeed;
        bool moving = speed > walkThreshold;

        if (movement.IsGrounded || movement.IsClimbing) lastGroundedTime = Time.time;
        IsGrounded = Time.time - lastGroundedTime <= groundedGrace;

        IsRunning = moving && movement.IsSprinting && IsGrounded;
        IsWalking = moving && !IsRunning && (IsGrounded || movement.IsClimbing);

        if (hasWalking) animator.SetBool(WalkingId, IsWalking);
        if (hasRunning) animator.SetBool(RunningId, IsRunning);
        if (hasGrounded) animator.SetBool(GroundedId, IsGrounded);
        if (hasClimbing) animator.SetBool(ClimbingId, movement.IsClimbing);
        if (hasClimbMove) animator.SetFloat(ClimbMoveId, movement.ClimbMoveAmount);
        if (hasSpeed) animator.SetFloat(SpeedId, speed);
        if (hasAnimSpeed) animator.SetFloat(AnimSpeedId, moving ? Mathf.Clamp(speed / referenceWalkSpeed, 0.6f, 3f) : 1f);
    }
}
