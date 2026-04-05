using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Unit base class — Feature 3: position clamped to MapBoundary every LateUpdate.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Unit : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] protected string unitName        = "Unit";
    [SerializeField] protected string unitType        = "Unknown";
    [SerializeField] protected string unitDescription = "";
    [SerializeField] protected int    maxHealth       = 100;
    [SerializeField] protected float  baseSpeed       = 3.5f;

    [Header("Combat")]
    [SerializeField] protected int   attackDamage   = 10;
    [SerializeField] protected float attackRange    = 2f;
    [SerializeField] protected float attackCooldown = 1.5f;

    [Header("Selection Visual (optional)")]
    [SerializeField] private GameObject selectionCircle;
    [SerializeField] private Renderer   unitRenderer;
    [SerializeField] private Material   outlineMaterial;

    // ── Runtime ────────────────────────────────────────────────────────
    protected int          currentHealth;
    protected NavMeshAgent agent;
    private   bool         isSelected     = false;
    private   Material[]   originalMats;
    private   float        lastClickTime  = -99f;
    private   float        lastAttackTime = -99f;
    private   const float  DOUBLE_CLICK   = 0.3f;
    protected Unit         attackTarget;
    public string huntingUnitName = ""; // remembers what type we're attacking

    protected virtual void Awake()
    {
        agent         = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;
        agent.speed   = baseSpeed;

        if (unitRenderer == null)
            unitRenderer = GetComponentInChildren<Renderer>();
        if (unitRenderer != null)
            originalMats = unitRenderer.sharedMaterials;
        if (selectionCircle != null)
            selectionCircle.SetActive(false);

        if (GetComponent<Collider>() == null &&
            GetComponentInChildren<Collider>() == null)
        {
            CapsuleCollider cc = gameObject.AddComponent<CapsuleCollider>();
            cc.height = 1f; cc.radius = 0.25f;
            cc.center = new Vector3(0, 0.5f, 0);
        }

        UnitSelectionManager.Instance?.Register(this);
    }

    protected virtual void Start() { }

    protected virtual void Update()
    {
        if (attackTarget != null) ChaseAndAttack();
    }

    // Feature 3: clamp position every frame so units never leave the map
    private void LateUpdate()
    {
        if (MapBoundary.Instance == null) return;
        Vector3 pos = transform.position;
        Vector3 clamped = MapBoundary.Instance.Clamp(pos);
        if (pos != clamped)
        {
            transform.position = clamped;
            if (agent != null && agent.isOnNavMesh)
                agent.Warp(clamped);
        }
    }

    // ── Direct click ───────────────────────────────────────────────────
    private float mouseDownTime = 0f;

    private void OnMouseDown()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject()) return;
        mouseDownTime = Time.unscaledTime;
    }

    private void OnMouseUp()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject()) return;

        // If held for more than 0.2s it was a drag — don't select
        if (Time.unscaledTime - mouseDownTime > 0.2f) return;

        // If drag-box just finished — don't select
        if (UnitSelectionBox.Instance != null && UnitSelectionBox.Instance.JustFinishedDrag)
            return;

        float now      = Time.unscaledTime;
        bool  isDouble = (now - lastClickTime) <= DOUBLE_CLICK;
        lastClickTime  = now;

        if (isDouble)
            SelectionManager.Instance?.SelectAllVisibleOfType(unitName);
        else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            SelectionManager.Instance?.ShiftClickUnit(this);
        else
            SelectionManager.Instance?.SelectSingleUnit(this);
    }
    
    // ── Combat ─────────────────────────────────────────────────────────
    private void ChaseAndAttack()
    {
        if (attackTarget == null) return;

        // Target died — find next of same type nearby
        if (attackTarget.gameObject == null)
        {
            FindNextTarget();
            return;
        }

        float dist = Vector3.Distance(transform.position, attackTarget.transform.position);
        if (dist <= attackRange)
        {
            agent.ResetPath();
            transform.LookAt(attackTarget.transform);
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                lastAttackTime = Time.time;
                PerformAttack(attackTarget);
            }
        }
        else agent.SetDestination(attackTarget.transform.position);
    }

    /// <summary>Find nearest enemy of same type within radius after target dies.</summary>
    public void FindNextTarget()
    {
        attackTarget = null;
        if (string.IsNullOrEmpty(huntingUnitName)) return;

        float    searchRadius = 20f;
        Collider[] nearby     = Physics.OverlapSphere(transform.position, searchRadius);
        Unit      closest     = null;
        float     closestDist = float.MaxValue;

        foreach (Collider c in nearby)
        {
            Unit u = c.GetComponentInParent<Unit>();
            if (u == null || u == this) continue;
            if (u.UnitName != huntingUnitName) continue;

            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d < closestDist) { closestDist = d; closest = u; }
        }

        attackTarget = closest;

        // Nothing left to hunt — clear hunt order
        if (attackTarget == null)
            huntingUnitName = "";
    }

    protected virtual void PerformAttack(Unit target)
    {
        // GetComponent<Animator>()?.SetTrigger("Attack");
        target.TakeDamage(attackDamage);
    }

    public void SetAttackTarget(Unit t) { attackTarget = t; }
    public void ClearAttackTarget()     { attackTarget = null; }

    // ── Selection ──────────────────────────────────────────────────────
    public virtual void Select(Color c)
    {
        isSelected = true;
        if (selectionCircle != null)
        {
            selectionCircle.SetActive(true);
            Renderer r = selectionCircle.GetComponent<Renderer>();
            if (r != null)
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }
        if (unitRenderer != null && outlineMaterial != null)
        {
            var mats = new Material[originalMats.Length + 1];
            originalMats.CopyTo(mats, 0);
            mats[mats.Length - 1] = outlineMaterial;
            unitRenderer.materials = mats;
        }
    }

    public virtual void Select() => Select(Color.white);

    public virtual void Deselect()
    {
        isSelected = false;
        if (selectionCircle != null) selectionCircle.SetActive(false);
        if (unitRenderer != null && originalMats != null)
            unitRenderer.materials = originalMats;
    }

    // ── Movement ───────────────────────────────────────────────────────
    public void SetMoveSpeed(float s) { if (agent != null) agent.speed = s; }
    public void RestoreSpeed()        { if (agent != null) agent.speed = baseSpeed; }

    // ── Health ─────────────────────────────────────────────────────────
    public virtual void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0) { Die(); return; }

        // Retaliate if idle
        if (attackTarget == null)
            attackTarget = FindAttacker();

        // Alert nearby allies of same type to help
        AlertNearbyAllies();
    }

    private Unit FindAttacker()
    {
        // Find closest unit that isn't the same type as us
        float    radius = 15f;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        Unit      best  = null;
        float     bestD = float.MaxValue;

        foreach (Collider c in hits)
        {
            Unit u = c.GetComponentInParent<Unit>();
            if (u == null || u == this) continue;
            if (u.UnitName == unitName) continue; // skip same type

            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d < bestD) { bestD = d; best = u; }
        }
        return best;
    }

    private void AlertNearbyAllies()
    {
        float    radius = 12f;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider c in hits)
        {
            Unit ally = c.GetComponentInParent<Unit>();
            if (ally == null || ally == this) continue;
            if (ally.UnitName != unitName) continue; // only same type helps
            if (ally.attackTarget != null) continue;  // already fighting

            // Find the closest enemy near us for the ally to attack
            Unit enemy = FindAttacker();
            if (enemy != null)
            {
                ally.attackTarget    = enemy;
                ally.huntingUnitName = enemy.UnitName;
            }
        }
    }

    protected virtual void Die()
    {
        // Notify nearby units that were targeting this unit to find a new target
        Collider[] nearby = Physics.OverlapSphere(transform.position, 20f);
        foreach (Collider c in nearby)
        {
            Unit u = c.GetComponentInParent<Unit>();
            if (u == null || u == this) continue;
            if (u.attackTarget == this)
                u.FindNextTarget();
        }

        UnitSelectionManager.Instance?.Unregister(this);
        if (isSelected) UnitInfoUI.Instance?.Hide();
        Destroy(gameObject);
    }
    public void SetHuntTarget(string unitName)
    {
        huntingUnitName = unitName;
    }
    // Add this field to track destination
private Vector3 currentDestination = Vector3.zero;
// Add this field to track destination
public virtual void MoveTo(Vector3 dest)
{
    // Store destination for flag tracking
    currentDestination = dest;
    
    // Clamp destination to map
    if (MapBoundary.Instance != null)
        dest = MapBoundary.Instance.Clamp(dest);

    ClearAttackTarget();
    if (agent != null && agent.isOnNavMesh)
        agent.SetDestination(dest);
}


// Add this coroutine to Unit.cs:
    private IEnumerator CheckArrival(Vector3 destination)
    {
        yield return new WaitForSeconds(0.5f);
        
        while (agent != null && agent.isOnNavMesh && agent.pathPending)
        {
            yield return null;
        }
        
        if (agent == null || !agent.isOnNavMesh) yield break;
        
        // Wait until unit arrives at destination
        while (agent.remainingDistance > agent.stoppingDistance)
        {
            yield return new WaitForSeconds(0.1f);
            if (agent == null || !agent.isOnNavMesh) yield break;
        }
        
        // Unit arrived - this could trigger flag removal if needed
        Debug.Log($"[Unit] {gameObject.name} arrived at destination");
    }


    // ── Properties ─────────────────────────────────────────────────────
    public string UnitName        => unitName;
    public string UnitType        => unitType;
    public string UnitDescription => unitDescription;
    public int    MaxHealth       => maxHealth;
    public int    CurrentHealth   => currentHealth;
    public float  BaseSpeed       => baseSpeed;
    public bool   IsSelected      => isSelected;
}
