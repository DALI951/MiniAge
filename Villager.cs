using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Villager v2 — Slot-based gathering, no clipping, smart overflow.
///
/// KEY FIXES:
/// 1. Uses ResourceNode.ReserveSlot() for explicit slot assignment.
/// 2. orbitRadius = 2.5f for comfortable 8-villager circle.
/// 3. Slot index determines exact angle — no random fighting.
/// 4. On overflow: auto-redirects to same-type or any nearby resource.
/// 5. NavMeshAgent obstacle avoidance enabled.
/// </summary>
public class Villager : Unit
{
    [Header("Gathering")]
    [SerializeField] private float gatherRange    = 1.2f;
    [SerializeField] private float gatherInterval = 2f;
    [SerializeField] private int   gatherAmount   = 10;
    [SerializeField] private float orbitRadius    = 2.5f;  // INCREASED from 1.2f

    private enum VState
    {
        Idle,
        MovingToCommand,
        MovingToResource,
        AttackingAnimal,
        Gathering,
        MovingToBuild,
        Building
    }

    private VState      state          = VState.Idle;
    private ResourceNode targetNode    = null;
    private int          gatherSlot     = -1;  // -1 = no slot assigned
    private float        lastActionTime = -99f;
    private ConstructionSite targetSite = null;

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

        // Enable NavMesh obstacle avoidance so villagers don't walk through each other
        if (agent != null)
        {
            agent.avoidancePriority = Random.Range(1, 100);
            agent.radius = 0.4f;
        }
    }

    protected override void Update()
    {
        base.Update();

        if (targetNode != null && targetNode.gameObject == null)
        {
            targetNode = null;
            gatherSlot = -1;
            EnterState(VState.Idle);
        }
        if (targetSite != null && targetSite.gameObject == null)
        {
            targetSite = null;
            EnterState(VState.Idle);
        }

        switch (state)
        {
            case VState.Idle: break;
            case VState.MovingToCommand:
                if (AgentStopped()) EnterState(VState.Idle);
                break;
            case VState.MovingToResource: TickMovingToResource(); break;
            case VState.AttackingAnimal:    TickAttackingAnimal(); break;
            case VState.Gathering:          TickGathering(); break;
            case VState.MovingToBuild:      TickMovingToBuild(); break;
            case VState.Building:           TickBuilding(); break;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────

    public void GatherFrom(ResourceNode node)
    {
        if (node == null || node.IsEmpty) return;

        ClearWaypoints();
        ReleaseCurrentNode();

        // CRITICAL: Try to reserve a slot
        int slot = node.ReserveSlot(this);

        if (slot < 0)
        {
            // Node is full — overflow handling is done inside ReserveSlot
            // It will redirect us to another node automatically
            // If we get here, no redirect was possible, so we queue
            gatherSlot = -1;
            MoveTo(node.transform.position); // Move close and wait (calls ReleaseCurrentNode internally)
            targetNode = node;               // re-assign AFTER MoveTo clears it
            EnterState(VState.MovingToResource);
            return;
        }

        targetNode = node;
        gatherSlot = slot;

        if (node.RequiresKill && !node.IsKilled)
            EnterState(VState.MovingToResource);
        else
            MoveToGatherSlot();
    }

    public override void MoveTo(Vector3 dest)
    {
        ReleaseCurrentNode();
        targetSite = null;
        ClearWaypoints();
        base.MoveTo(dest);
        EnterState(VState.MovingToCommand);
    }

    /// <summary>Called by ResourceNode when a slot opens up and we're promoted from overflow.</summary>
    public void OnSlotPromoted(ResourceNode node, int slot)
    {
        if (targetNode == node && gatherSlot < 0)
        {
            gatherSlot = slot;
            Debug.Log($"[{name}] Promoted to slot {slot} at {node.name}");
            if (!node.RequiresKill || node.IsKilled)
                MoveToGatherSlot();
        }
    }

    // ── State Ticks ────────────────────────────────────────────────────

    private void TickMovingToResource()
    {
        if (targetNode == null || targetNode.IsEmpty)
        {
            EnterState(VState.Idle);
            AutoFindNext();
            return;
        }

        // If we're queued (no slot yet), just wait near the node
        if (gatherSlot < 0)
        {
            float dist = Dist(targetNode);
            if (dist <= gatherRange + orbitRadius)
            {
                agent.ResetPath();
                // Check if we got promoted
                if (targetNode != null)
                {
                    int newSlot = targetNode.ReserveSlot(this);
                    if (newSlot >= 0)
                    {
                        gatherSlot = newSlot;
                        if (!targetNode.RequiresKill || targetNode.IsKilled)
                            MoveToGatherSlot();
                    }
                }
            }
            return;
        }

        float d = Dist(targetNode);

        if (targetNode.RequiresKill && !targetNode.IsKilled)
        {
            if (d <= attackRange) EnterState(VState.AttackingAnimal);
        }
        else
        {
            if (d <= gatherRange + 0.5f) EnterState(VState.Gathering);
        }
    }

    private void TickAttackingAnimal()
    {
        if (targetNode == null || targetNode.IsEmpty || targetNode.IsKilled)
        {
            if (targetNode != null && !targetNode.IsEmpty)
            { MoveToGatherSlot(); return; }
            AutoFindNext();
            return;
        }

        float dist = Dist(targetNode);
        if (dist > attackRange)
        {
            agent.SetDestination(targetNode.transform.position);
            return;
        }

        agent.ResetPath();
        transform.LookAt(targetNode.transform.position);

        if (Time.time - lastActionTime >= attackCooldown)
        {
            lastActionTime = Time.time;
            targetNode.TakeDamage(attackDamage);

            if (targetNode.IsKilled)
            {
                // Recalculate slot based on current gatherer count
                if (targetNode != null)
                {
                    targetNode.ReleaseSlot(this); // release old
                    int newSlot = targetNode.ReserveSlot(this); // get new position
                    gatherSlot = Mathf.Max(0, newSlot);
                }
                MoveToGatherSlot();
            }
        }
    }

    private void TickGathering()
    {
        if (targetNode == null || targetNode.IsEmpty)
        {
            AutoFindNext();
            return;
        }

        // Ensure we still have a valid slot
        if (gatherSlot < 0)
        {
            int newSlot = targetNode.ReserveSlot(this);
            if (newSlot < 0)
            {
                // Lost our slot — get redirected or wait
                MoveTo(targetNode.transform.position);
                return;
            }
            gatherSlot = newSlot;
        }

        float dist = Dist(targetNode);
        if (dist > gatherRange + 1.0f)
        {
            // Drifted too far — return to slot
            MoveToGatherSlot();
            return;
        }

        agent.ResetPath();
        transform.LookAt(targetNode.transform.position);

        if (Time.time - lastActionTime >= gatherInterval)
        {
            lastActionTime = Time.time;
            int gathered = targetNode.Gather(gatherAmount);

            bool isLocalPlayer = OwnerPlayerId == PlayerColorManager.LocalPlayerIndex;
            switch (targetNode.ResourceType)
            {
                case ResourceType.Wood:
                    if (isLocalPlayer) ResourceManager.Instance?.AddResources(0, gathered, 0);
                    else               EnemyAI.Instance?.AddEnemyResources(0, gathered, 0);
                    break;
                case ResourceType.Gold:
                    if (isLocalPlayer) ResourceManager.Instance?.AddResources(0, 0, gathered);
                    else               EnemyAI.Instance?.AddEnemyResources(0, 0, gathered);
                    break;
                case ResourceType.Food:
                    if (isLocalPlayer) ResourceManager.Instance?.AddResources(gathered, 0, 0);
                    else               EnemyAI.Instance?.AddEnemyResources(gathered, 0, 0);
                    break;
            }

            if (targetNode.IsEmpty) AutoFindNext();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private void MoveToGatherSlot()
    {
    if (targetNode == null) return;
    if (gatherSlot < 0) return;
        // FIXED: Use explicit slot index for deterministic positioning
        int   totalSlots = Mathf.Max(targetNode.MaxGatherers, 1);
        float angleStep  = 360f / totalSlots;
        float angle      = angleStep * gatherSlot;
        
        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * orbitRadius;
        Vector3 dest   = targetNode.transform.position + offset;

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            dest = hit.position;

        agent.SetDestination(dest);
        EnterState(VState.MovingToResource);
    }

    private void AutoFindNext()
    {
        ResourceType type = targetNode != null ? targetNode.ResourceType : ResourceType.Wood;
        ReleaseCurrentNode();
        EnterState(VState.Idle);

        // Try same type first
        ResourceNode next = ResourceNode.FindNearest(transform.position, type, null, 50f);
        if (next != null)
        {
            GatherFrom(next);
            return;
        }

        // Fallback: any resource type
        ResourceNode any = ResourceNode.FindNearestAny(transform.position, 80f, null);
        if (any != null)
        {
            GatherFrom(any);
            return;
        }

        Debug.Log($"[{name}] No resources available nearby — going idle");
    }

    private void ReleaseCurrentNode()
    {
        if (targetNode != null)
        {
            targetNode.ReleaseSlot(this);
            targetNode = null;
        }
        gatherSlot = -1;
    }

    private float Dist(ResourceNode node) =>
        node == null ? float.MaxValue : Vector3.Distance(transform.position, node.transform.position);

    private bool AgentStopped() =>
        agent != null && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;

    private void EnterState(VState next)
    {
        state = next;
        switch (state)
        {
            case VState.Idle:
                if (agent != null && agent.isOnNavMesh) agent.ResetPath();
                break;
            case VState.AttackingAnimal:
                if (agent != null && agent.isOnNavMesh) agent.ResetPath();
                break;
        }
    }

    // ── Building ───────────────────────────────────────────────────────

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
        if (targetSite == null || targetSite.gameObject == null)
        { targetSite = null; EnterState(VState.Idle); return; }
        if (Vector3.Distance(transform.position, targetSite.transform.position) <= 2.5f)
            EnterState(VState.Building);
    }

    private void TickBuilding()
    {
        if (targetSite == null || targetSite.gameObject == null || targetSite.IsComplete)
        { targetSite = null; EnterState(VState.Idle); return; }
        agent.ResetPath();
        transform.LookAt(targetSite.transform.position);
    }

    public void OnBuildingComplete()
    {
        targetSite = null;
        EnterState(VState.Idle);
    }

    // ── Death ──────────────────────────────────────────────────────────

    protected override void Die()
    {
        ReleaseCurrentNode();
        targetSite = null;
        base.Die();
    }
    public bool IsGathering => state == VState.Gathering
                            || state == VState.MovingToResource
                            || state == VState.AttackingAnimal;
}