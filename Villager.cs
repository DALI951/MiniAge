using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Villager — state-machine driven.
///
/// States:
///   Idle            → standing still, no orders
///   MovingToCommand → player ordered a move (any MoveTo call clears gather orders)
///   MovingToResource→ walking toward a resource node
///   AttackingAnimal → hitting the animal until dead
///   Gathering       → standing next to a node, extracting resources
///
/// Root cause fixes:
///   Bug 2 (desync): animation hooks only fire on state ENTER, never mid-state.
///   Bug 7 (stuck):  any MoveTo() from outside forces state → MovingToCommand,
///                   which fully clears the gather assignment.
/// </summary>
public class Villager : Unit
{
    // ── Inspector ────────────────────────────────────────────────────────
    [Header("Gathering")]
    [SerializeField] private float gatherRange    = 1.2f;
    [SerializeField] private float gatherInterval = 2f;
    [SerializeField] private int   gatherAmount   = 10;
    [SerializeField] private float orbitRadius    = 1.2f;

    // ── State machine ────────────────────────────────────────────────────
    private enum VState
    {
        Idle,
        MovingToCommand,    // explicit player move order — ignores gather logic
        MovingToResource,   // walking to a resource node
        AttackingAnimal,    // killing an animal before gathering
        Gathering,           // actively extracting from node
        MovingToBuild,  // ADD
        Building        // ADD
    }

    private VState      state          = VState.Idle;
    private ResourceNode targetNode    = null;
    private int          gatherSlot    = 0;
    private float        lastActionTime = -99f;  // shared cooldown for attack + gather
    private ConstructionSite targetSite = null;

    // ── Init ─────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        unitName        = "Villager";
        unitType        = "Villager";
        unitDescription = "Gathers resources. Right-click a tree, mine or animal.";
        maxHealth       = 60;
        baseSpeed       = 3f;
        attackDamage    = 15;
        attackRange     = 2f;
        attackCooldown  = 1f;
        base.Awake();
    }

    // ── Update — state machine tick ──────────────────────────────────────
    protected override void Update()
    {
        switch (state)
        {
            case VState.Idle:
                break;

            case VState.MovingToCommand:
                // Wait until NavMesh agent stops, then go Idle
                if (AgentStopped())
                    EnterState(VState.Idle);
                break;

            case VState.MovingToResource:
                TickMovingToResource();
                break;

            case VState.AttackingAnimal:
                TickAttackingAnimal();
                break;

            case VState.Gathering:
                TickGathering();
                break;
            case VState.MovingToBuild:
                TickMovingToBuild();
                break;

            case VState.Building:
                TickBuilding();
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Order to gather from a node.
    /// Called by SelectionManager when right-clicking a resource.
    /// </summary>
    public void GatherFrom(ResourceNode node)
    {
        if (node == null || node.IsEmpty) return;

        // Release previous slot
        ReleaseCurrentNode();

        // Ask node if there's room (may redirect to nearest)
        if (!node.RequestGather(this)) return;

        targetNode = node;
        gatherSlot = node.GathererCount - 1;

        // Go attack first if animal, else walk to gather spot
        if (node.RequiresKill && !node.IsKilled)
            EnterState(VState.MovingToResource); // will switch to AttackingAnimal on arrival
        else
            MoveToGatherSlot();
    }

    /// <summary>
    /// Override base MoveTo — any explicit move order cancels gathering.
    /// This is the root fix for Bug 7 (villager ignores player commands).
    /// </summary>
    public override void MoveTo(Vector3 dest)
    {
        ReleaseCurrentNode();
        targetSite = null;
        base.MoveTo(dest);
        EnterState(VState.MovingToCommand);
    }

    // ─────────────────────────────────────────────────────────────────────
    // STATE TICKS
    // ─────────────────────────────────────────────────────────────────────

    private void TickMovingToResource()
    {
        if (targetNode == null) { EnterState(VState.Idle); return; }

        float dist = Dist(targetNode);

        if (targetNode.RequiresKill && !targetNode.IsKilled)
        {
            // Close enough to start fighting
            if (dist <= attackRange)
                EnterState(VState.AttackingAnimal);
        }
        else
        {
            // Close enough to gather
            if (dist <= gatherRange)
                EnterState(VState.Gathering);
        }
    }

    private void TickAttackingAnimal()
    {
        if (targetNode == null || targetNode.IsKilled || targetNode.IsEmpty)
        {
            // Animal died — switch to gathering
            if (targetNode != null && !targetNode.IsEmpty)
            { MoveToGatherSlot(); return; }
            AutoFindNext();
            return;
        }

        float dist = Dist(targetNode);
        if (dist > attackRange)
        {
            // Drifted away — reapproach
            agent.SetDestination(targetNode.transform.position);
            return;
        }

        agent.ResetPath();
        transform.LookAt(targetNode.transform.position);

        if (Time.time - lastActionTime >= attackCooldown)
        {
            lastActionTime = Time.time;
            targetNode.TakeDamage(attackDamage);
            OnAttackAnimal(); // animation hook

            if (targetNode.IsKilled)
            {
                // Recalculate slot based on where animal actually stopped
                gatherSlot = targetNode.GathererCount;
                MoveToGatherSlot();
            }
        }
    }

    private void TickGathering()
    {
        if (targetNode == null || targetNode.IsEmpty) { AutoFindNext(); return; }
        // Confirm we are physically gathering (fixes count mismatch)
        targetNode.ConfirmGathering(this);
        float dist = Dist(targetNode);

        if (dist > gatherRange)
        {
            // Walked away somehow — re-approach without changing state
            agent.SetDestination(targetNode.transform.position);
            return;
        }

        agent.ResetPath();
        transform.LookAt(targetNode.transform.position);

        if (Time.time - lastActionTime >= gatherInterval)
        {
            lastActionTime = Time.time;

            int gathered = targetNode.Gather(gatherAmount);
            OnGatherTick(); // animation hook

            switch (targetNode.ResourceType)
            {
                case ResourceType.Wood: ResourceManager.Instance?.AddResources(0, gathered, 0); break;
                case ResourceType.Gold: ResourceManager.Instance?.AddResources(0, 0, gathered); break;
                case ResourceType.Food: ResourceManager.Instance?.AddResources(gathered, 0, 0); break;
            }

            if (targetNode.IsEmpty) AutoFindNext();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // STATE TRANSITIONS
    // ─────────────────────────────────────────────────────────────────────

    private void EnterState(VState next)
    {
        // Exit hooks
        switch (state)
        {
            case VState.Gathering:
                OnGatherStop(); // animation hook
                break;
        }

        state = next;

        // Enter hooks
        switch (state)
        {
            case VState.Idle:
                break;

            case VState.Gathering:
                OnGatherStart(); // animation hook
                break;

            case VState.AttackingAnimal:
                agent.ResetPath();
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private void MoveToGatherSlot()
    {
        if (targetNode == null) return;

        // Recalculate slot index based on current gatherer count
        gatherSlot = targetNode.GathererCount;

        int   totalSlots = Mathf.Max(targetNode.MaxGatherers, 1);
        float angle      = (360f / totalSlots) * gatherSlot;
        float radius     = orbitRadius;

        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
        Vector3 dest   = targetNode.transform.position + offset;

        // Snap to NavMesh
        if (UnityEngine.AI.NavMesh.SamplePosition(dest, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            dest = hit.position;

        agent.SetDestination(dest);
        EnterState(VState.MovingToResource);
    }

    private void AutoFindNext()
    {
        ResourceType type = targetNode != null
            ? targetNode.ResourceType : ResourceType.Wood;

        ReleaseCurrentNode();
        EnterState(VState.Idle);

        ResourceNode next = ResourceNode.FindNearest(transform.position, type);
        if (next != null) GatherFrom(next);
    }

    private void ReleaseCurrentNode()
    {
        if (targetNode != null)
        {
            targetNode.ReleaseGatherer(this);
            targetNode = null;
        }
    }

    private float Dist(ResourceNode node) =>
        node == null ? float.MaxValue
            : Vector3.Distance(transform.position, node.transform.position);

    private bool AgentStopped() =>
        agent != null && agent.isOnNavMesh &&
        !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;

    // ─────────────────────────────────────────────────────────────────────
    // ANIMATION HOOKS — uncomment Animator lines when you add animations
    // ─────────────────────────────────────────────────────────────────────

    protected virtual void OnGatherStart()
    {
        // GetComponent<Animator>()?.SetBool("IsGathering", true);
        Debug.Log("[Villager] Gathering started.");
    }

    protected virtual void OnGatherTick()
    {
        // GetComponent<Animator>()?.SetTrigger("GatherTick");
    }

    protected virtual void OnGatherStop()
    {
        // GetComponent<Animator>()?.SetBool("IsGathering", false);
        Debug.Log("[Villager] Gathering stopped.");
    }

    protected virtual void OnAttackAnimal()
    {
        // GetComponent<Animator>()?.SetTrigger("Attack");
    }

    // ─────────────────────────────────────────────────────────────────────
    // DEATH
    // ─────────────────────────────────────────────────────────────────────

    protected override void Die()
    {
        ReleaseCurrentNode();
        base.Die();
    }
    public void BuildAt(ConstructionSite site)
    {
        ReleaseCurrentNode();
        targetSite = site;
        site.AssignBuilder(this);
        agent.SetDestination(site.transform.position);
        EnterState(VState.MovingToBuild);
    }

    private void TickMovingToBuild()
    {
        if (targetSite == null) { EnterState(VState.Idle); return; }
        if (Vector3.Distance(transform.position, targetSite.transform.position) <= 2.5f)
            EnterState(VState.Building);
    }

    private void TickBuilding()
    {
        if (targetSite == null || targetSite.IsComplete)
        { targetSite = null; EnterState(VState.Idle); return; }

        agent.ResetPath();
        transform.LookAt(targetSite.transform.position);
        // ANIMATION HOOK: GetComponent<Animator>()?.SetBool("IsBuilding", true);
    }

    public void OnBuildingComplete()
    {
        targetSite = null;
        EnterState(VState.Idle);
        // ANIMATION HOOK: GetComponent<Animator>()?.SetBool("IsBuilding", false);
    }
}
