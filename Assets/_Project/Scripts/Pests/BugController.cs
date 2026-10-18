using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class BugController : MonoBehaviour
{
    [Header("Veri")]
    public BugData bugData;

    [Header("Hızlar")]
    public float cuteSpeed = 2f;
    public float horrorSpeed = 5f;

    [Header("Hareket")]
    public float wanderRadius = 5f;
    public float targetInterval = 3f;

    public BugState currentState;

    private MeshRenderer meshRenderer;
    private Vector3 spawnCenter;
    private Vector3 targetPosition;
    private float currentSpeed;
    private float timer;

    private void Start()
    {
        KinematicBody.Ensure(gameObject); // sürekli hareket eden collider (FPS)
        meshRenderer = GetComponent<MeshRenderer>();
        spawnCenter = transform.position;

        currentState = bugData.startState;
        currentSpeed = cuteSpeed;
        meshRenderer.material.color = bugData.cuteColor;

        PickNewTarget();
    }

    private void Update()
    {
        MoveTowardsTarget();
    }

    private void MoveTowardsTarget()
    {
        timer += Time.deltaTime;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            currentSpeed * Time.deltaTime);

        bool reachedTarget = Vector3.Distance(transform.position, targetPosition) < 0.1f;
        bool timeExpired = timer >= targetInterval;

        if (reachedTarget || timeExpired)
        {
            PickNewTarget();
        }
    }

    private void PickNewTarget()
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        targetPosition = new Vector3(
            spawnCenter.x + offset.x,
            spawnCenter.y,
            spawnCenter.z + offset.y);

        timer = 0f;
    }

    // Kapanırken (yakalanma vb.) Horror'daysa ev efektine haber verir.
    private void OnDisable()
    {
        if (currentState == BugState.Horror && AnomalicVFXManager.Instance != null)
            AnomalicVFXManager.Instance.NotifyHorrorBugRemoved();
    }

    public void TriggerHorrorMode()
    {
        bool alreadyHorror = currentState == BugState.Horror;
        currentState = BugState.Horror;
        if (!alreadyHorror && AnomalicVFXManager.Instance != null)
            AnomalicVFXManager.Instance.NotifyHorrorBugAdded();
        currentSpeed = horrorSpeed;
        meshRenderer.material.color = bugData.horrorColor;

        Debug.LogWarning($"{bugData.bugName} çıldırdı! Horror moda geçti.");
    }
}
