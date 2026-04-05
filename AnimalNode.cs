using UnityEngine;

/// <summary>
/// AnimalNode — wanders the map until killed by a Villager, then becomes gatherable.
///
/// Fix Bug 6: wanderTarget is always clamped by MapBoundary before moving.
/// </summary>
public class AnimalNode : ResourceNode
{
    [Header("Wandering")]
    [SerializeField] private float wanderRadius   = 6f;
    [SerializeField] private float wanderInterval = 3f;
    [SerializeField] private float moveSpeed      = 1.5f;

    private Vector3 wanderTarget;
    private float   lastWanderTime = 0f;
    private bool    stopped        = false;

    protected override void Awake()
    {
        base.Awake();
        wanderTarget = transform.position;
    }

    private void Update()
    {
        if (stopped || IsEmpty) return;

        // Pick new wander target periodically
        if (Time.time - lastWanderTime > wanderInterval)
        {
            lastWanderTime = Time.time;

            Vector2 rand     = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = transform.position + new Vector3(rand.x, 0, rand.y);

            // Always clamp to map — fixes Bug 6
            if (MapBoundary.Instance != null)
                candidate = MapBoundary.Instance.Clamp(candidate);

            wanderTarget = candidate;
        }

        // Move toward target
        transform.position = Vector3.MoveTowards(
            transform.position, wanderTarget, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, wanderTarget) > 0.05f)
            transform.LookAt(new Vector3(wanderTarget.x, transform.position.y, wanderTarget.z));
    }

    public void StopMoving()
    {
        stopped      = true;
        wanderTarget = transform.position;
    }

    protected override void OnKilled()
    {
        base.OnKilled();
        StopMoving();
    }
}
