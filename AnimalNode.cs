using UnityEngine;

public class AnimalNode : ResourceNode
{
    [Header("Wandering")]
    [SerializeField] private float wanderRadius   = 6f;
    [SerializeField] private float wanderInterval = 3f;
    [SerializeField] private float moveSpeed      = 1.5f;

    private Vector3 wanderTarget;
    private float   lastWanderTime;
    private bool    stopped;

    protected override void Awake()
    {
        base.Awake();
        wanderTarget = transform.position;
    }

    private new void Update()
    {
        // NOTE: visualRoot toggling is handled by ResourceCullingManager.
        // Do NOT touch visualRoot here — it causes flicker and fighting.

        if (stopped || IsEmpty) return;

        if (Time.time - lastWanderTime > wanderInterval)
        {
            lastWanderTime = Time.time;
            Vector2 rand = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = transform.position + new Vector3(rand.x, 0, rand.y);

            if (MapBoundary.Instance != null)
                candidate = MapBoundary.Instance.Clamp(candidate);

            wanderTarget = candidate;
        }

        transform.position = Vector3.MoveTowards(
            transform.position, wanderTarget, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, wanderTarget) > 0.05f)
            transform.LookAt(new Vector3(wanderTarget.x, transform.position.y, wanderTarget.z));
    }

    public void StopMoving()
    {
        stopped = true;
        wanderTarget = transform.position;
    }

    protected override void OnKilled()
    {
        base.OnKilled();
        StopMoving();
    }
}