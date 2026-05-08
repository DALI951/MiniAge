using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyAI — controls the enemy faction.
/// Attach to any persistent GameObject (e.g. GameManager).
/// Requires a second BuildingSpawner in the scene with playerIndex = enemyPlayerId.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance { get; private set; }

    [Header("Enemy Identity")]
    [SerializeField] public int enemyPlayerId = 8;

    [Header("Starting Resources")]
    [SerializeField] private int startFood = 400;
    [SerializeField] private int startWood = 300;
    [SerializeField] private int startGold = 200;

    [Header("Population")]
    [SerializeField] private int maxPopulation = 20;
    private int currentPopulation = 0;

    [Header("AI Timers (seconds)")]
    [SerializeField] private float trainCheckInterval  = 20f;  // try to queue a unit every N s
    [SerializeField] private float attackInterval      = 60f;  // send attack wave every N s
    [SerializeField] private float patrolInterval      = 15f;  // give patrol orders every N s
    [SerializeField] private float gatherCheckInterval = 10f;  // redirect idle villagers every N s

    private int   food, wood, gold;
    private float trainTimer, attackTimer, patrolTimer, gatherTimer;

    // ── Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        food = startFood; wood = startWood; gold = startGold;
        // Give buildings and resources 3 s to finish spawning before issuing first orders
        Invoke(nameof(InitialGatherOrders), 3f);
    }

    private void Update()
    {
        trainTimer  += Time.deltaTime;
        attackTimer += Time.deltaTime;
        patrolTimer += Time.deltaTime;
        gatherTimer += Time.deltaTime;

        if (trainTimer  >= trainCheckInterval)  { trainTimer  = 0f; TryTrainUnits(); }
        if (attackTimer >= attackInterval)       { attackTimer = 0f; SendAttackWave(); }
        if (patrolTimer >= patrolInterval)       { patrolTimer = 0f; IssuePatrolOrders(); }
        if (gatherTimer >= gatherCheckInterval)  { gatherTimer = 0f; CheckGatherers(); }
    }

    // ── Resource API (called by Building and Villager) ────────────────────

    /// <summary>Add gathered/refunded resources to the enemy pool.</summary>
    public void AddEnemyResources(int f, int w, int g)
    {
        food = Mathf.Max(0, food + f);
        wood = Mathf.Max(0, wood + w);
        gold = Mathf.Max(0, gold + g);
    }

    /// <summary>Returns true and deducts if the enemy can afford the cost.</summary>
    public bool TrySpend(int costFood, int costWood, int costGold)
    {
        if (food < costFood || wood < costWood || gold < costGold) return false;
        food -= costFood; wood -= costWood; gold -= costGold;
        return true;
    }

    public bool CanAddPopulation(int amount = 1) => currentPopulation + amount <= maxPopulation;
    public void AddPopulation(int amount = 1)    => currentPopulation = Mathf.Clamp(currentPopulation + amount, 0, maxPopulation);
    public void RemovePopulation(int amount = 1) => currentPopulation = Mathf.Max(0, currentPopulation - amount);
    public int  CurrentPopulation => currentPopulation;
    public int  MaxPopulation     => maxPopulation;

    // ── Training ─────────────────────────────────────────────────────────

    private void TryTrainUnits()
    {
        foreach (Building b in Building.AllBuildings)
        {
            if (b == null || b.OwnerPlayerId != enemyPlayerId) continue;
            if (b.QueueBatchCount >= 3) continue;   // don't over-queue
            b.SpawnUnit(0);                          // unit index 0 of whatever building has
        }
    }

    // ── Gathering ─────────────────────────────────────────────────────────

    private void InitialGatherOrders() => CheckGatherers();

    private void CheckGatherers()
    {
        if (UnitSelectionManager.Instance == null) return;
        foreach (Unit u in UnitSelectionManager.Instance.allUnitsList)
        {
            if (u == null || !(u is Villager v)) continue;
            if (u.OwnerPlayerId != enemyPlayerId) continue;
            if (v.IsGathering) continue;   // already working
            ResourceNode node = ResourceNode.FindNearestAny(u.transform.position, 120f);
            if (node != null) v.GatherFrom(node);
        }
    }

    // ── Attack Wave ───────────────────────────────────────────────────────

    private void SendAttackWave()
    {
        if (UnitSelectionManager.Instance == null) return;

        List<Unit> attackers = new List<Unit>();
        foreach (Unit u in UnitSelectionManager.Instance.allUnitsList)
        {
            if (u == null || u is Villager) continue;
            if (u.OwnerPlayerId != enemyPlayerId) continue;
            attackers.Add(u);
        }
        if (attackers.Count == 0) return;

        Building target = FindNearestPlayerBuilding(AveragePos(attackers));
        if (target == null) return;

        foreach (Unit u in attackers)
            u.SetBuildingTarget(target);
    }

    // ── Patrol ────────────────────────────────────────────────────────────

    private void IssuePatrolOrders()
    {
        if (UnitSelectionManager.Instance == null) return;
        Vector3 basePos = GetEnemyBasePosition();

        foreach (Unit u in UnitSelectionManager.Instance.allUnitsList)
        {
            if (u == null || u is Villager) continue;
            if (u.OwnerPlayerId != enemyPlayerId) continue;
            if (u.HasActiveTarget) continue;   // already has orders

            Vector2 rand   = Random.insideUnitCircle * 12f;
            Vector3 patrol = basePos + new Vector3(rand.x, 0f, rand.y);
            if (MapBoundary.Instance != null) patrol = MapBoundary.Instance.Clamp(patrol);
            u.SetFirstWaypoint(patrol);
        }
    }

    // ── Win / Lose ────────────────────────────────────────────────────────

    /// <summary>Called by Building.OnBuildingDestroyed() before the GO is removed.</summary>
    public void OnBuildingDestroyed(Building building)
    {
        StartCoroutine(CheckWinLose());
    }

    private IEnumerator CheckWinLose()
    {
        yield return null;   // let AllBuildings finish updating

        bool enemyHasBuildings  = false;
        bool playerHasBuildings = false;
        int  localId            = PlayerColorManager.LocalPlayerIndex;

        foreach (Building b in Building.AllBuildings)
        {
            if (b == null) continue;
            if (b.OwnerPlayerId == enemyPlayerId) enemyHasBuildings  = true;
            if (b.OwnerPlayerId == localId)        playerHasBuildings = true;
        }

        if (!enemyHasBuildings)  WinLoseUI.Instance?.ShowWin();
        if (!playerHasBuildings) WinLoseUI.Instance?.ShowLose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Building FindNearestPlayerBuilding(Vector3 from)
    {
        Building nearest = null;
        float    best    = float.MaxValue;
        int      localId = PlayerColorManager.LocalPlayerIndex;

        foreach (Building b in Building.AllBuildings)
        {
            if (b == null || b.OwnerPlayerId != localId) continue;
            float d = Vector3.Distance(from, b.transform.position);
            if (d < best) { best = d; nearest = b; }
        }
        return nearest;
    }

    private Vector3 AveragePos(List<Unit> units)
    {
        Vector3 sum = Vector3.zero; int cnt = 0;
        foreach (Unit u in units) { if (u != null) { sum += u.transform.position; cnt++; } }
        return cnt > 0 ? sum / cnt : Vector3.zero;
    }

    private Vector3 GetEnemyBasePosition()
    {
        foreach (Building b in Building.AllBuildings)
            if (b != null && b.OwnerPlayerId == enemyPlayerId) return b.transform.position;
        return Vector3.zero;
    }
}