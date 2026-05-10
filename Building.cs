using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Building : MonoBehaviour
{
    [Header("Ownership")]
    [SerializeField] private int ownerPlayerId = 0;

    [Header("Spawn Settings")]
    [SerializeField] protected Transform        spawnPoint;
    [SerializeField] protected List<GameObject> spawnablePrefabs = new List<GameObject>();
    [SerializeField] private   float            spawnSpacing     = 1.5f;

    [Header("Building Info")]
    [SerializeField] protected string buildingName = "Building";

    [Header("Health")]
    [SerializeField] public int maxBuildingHealth = 500;   
    [SerializeField] public int currentBuildingHealth;

    [Header("Training Costs & Times")]
    [Tooltip("Training time in seconds per unit index. Matches spawnablePrefabs order.")]
    [SerializeField] private List<float> unitTrainingTimes = new List<float>();
    [SerializeField] private List<int>   unitCostFood      = new List<int>();
    [SerializeField] private List<int>   unitCostWood      = new List<int>();
    [SerializeField] private List<int>   unitCostGold      = new List<int>();
    [SerializeField] private int         populationPerUnit = 1;

    // ── Batch training system ────────────────────────────────────────────
    // A batch = one queue slot containing N units of the same type.
    // The timer runs once per batch; on completion all N units spawn at once.
    public struct TrainingBatch
    {
        public int unitIndex;
        public int count;
        public TrainingBatch(int idx, int cnt) { unitIndex = idx; count = cnt; }
    }

    private List<TrainingBatch> trainingQueue   = new List<TrainingBatch>(); // max 5 batches
    private bool                isTraining      = false;
    private float               trainingTimer   = 0f;
    private float               currentUnitTime = 0f;
    private TrainingBatch       currentBatch;
    private int                 pendingPopulation = 0; // pop reserved for queued (not yet spawned) units
    private int                 batchSpawnedSoFar = 0;
    private Renderer[]          cachedRenderers;

    public static List<Building> AllBuildings { get; private set; } = new List<Building>();

    // ────────────────────────────────────────────────────────────────────

    protected virtual void Start()
    {
        currentBuildingHealth = maxBuildingHealth;

        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.size   = new Vector3(2.5f, 2.5f, 2.5f);
            bc.center = new Vector3(0f, 1.25f, 0f);
        }

        if (spawnPoint == null)
            Debug.LogWarning($"[{buildingName}] No spawnPoint assigned!");

        if (!AllBuildings.Contains(this))
            AllBuildings.Add(this);

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        MinimapSystem.Instance?.TrackBuilding(this);
        GameUI.Instance?.RegisterBuilding(this);
    }

    protected virtual void Update()
    {
        if (ownerPlayerId != PlayerColorManager.LocalPlayerIndex && FogOfWar.Instance != null)
        {
            bool explored = FogOfWar.Instance.IsExplored(transform.position);
            if (cachedRenderers != null)
                foreach (Renderer r in cachedRenderers)
                    if (r != null) r.enabled = explored;
        }

        if (!isTraining) return;
        trainingTimer -= Time.deltaTime;
        if (trainingTimer <= 0f)
            TryCompleteBatch();
    }

    private void OnDestroy()
    {
        AllBuildings.Remove(this);
        trainingQueue.Clear();
        isTraining        = false;
        pendingPopulation = 0;
    }

    // ── Click Detection ──────────────────────────────────────────────────
    private void OnMouseDown()
    {
        if (IsClickOnInteractiveUI()) return;
        bool isEnemy = ownerPlayerId != PlayerColorManager.LocalPlayerIndex;
        if (!isEnemy)
            SelectionManager.Instance?.SelectBuilding(this);
        BuildingInfoUI.Instance?.ShowBuilding(this);
    }

    private bool IsClickOnInteractiveUI()
    {
        if (EventSystem.current == null) return false;
        var pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (var result in results)
        {
            string n = result.gameObject.name.ToLower();
            if (n.Contains("viewport")) continue;
            if (n.Contains("fog")) continue;
            if (n.Contains("rawimage")) continue;
            if (n.Contains("image") && !n.Contains("button")) continue;
            if (result.gameObject.GetComponent<Button>() != null) return true;
            if (result.gameObject.GetComponent<TMPro.TMP_InputField>() != null) return true;
            if (result.gameObject.GetComponent<UnityEngine.UI.Slider>() != null) return true;
            if (result.gameObject.GetComponent<UnityEngine.UI.Dropdown>() != null) return true;
            if (result.gameObject.GetComponent<UnityEngine.UI.Scrollbar>() != null) return true;
            if (n.Contains("button")) return true;
            if (n.Contains("close")) return true;
        }
        return false;
    }

    public void SetSpawnPoint(Vector3 worldPos)
    {
        if (spawnPoint != null) spawnPoint.position = worldPos;
    }

    public virtual void Select()
    {
        Debug.Log($"[{buildingName}] Selected.");
        GameUI.Instance?.ShowBuildingUI(this);
    }

    public virtual void Deselect()
    {
    }

    // ── Queue a unit for training ────────────────────────────────────────
    public void SpawnUnit(int index)
    {
        if (index < 0 || index >= spawnablePrefabs.Count)
        { Debug.LogError($"[{buildingName}] Bad index {index}"); return; }
        if (spawnablePrefabs[index] == null)
        { Debug.LogError($"[{buildingName}] Null prefab at {index}"); return; }

        // ── Resource check ──────────────────────────────────────────────
        int food = GetCost(unitCostFood, index);
        int wood = GetCost(unitCostWood, index);
        int gold = GetCost(unitCostGold, index);

        bool isEnemyOwned = ownerPlayerId != PlayerColorManager.LocalPlayerIndex;
        if (!isEnemyOwned)
        {
            if (ResourceManager.Instance != null && !ResourceManager.Instance.TrySpend(food, wood, gold))
            { Debug.Log($"[{buildingName}] Not enough resources."); return; }
        }
        else
        {
            if (EnemyAI.Instance != null && !EnemyAI.Instance.TrySpend(food, wood, gold)) return;
        }

        // ── Population check (includes already-queued pending units) ────
        int currentPop = !isEnemyOwned
            ? (ResourceManager.Instance != null ? ResourceManager.Instance.CurrentPopulation : 0)
            : (EnemyAI.Instance        != null ? EnemyAI.Instance.CurrentPopulation         : 0);
        int maxPop = !isEnemyOwned
            ? (ResourceManager.Instance != null ? ResourceManager.Instance.MaxPopulation     : 20)
            : (EnemyAI.Instance        != null ? EnemyAI.Instance.MaxPopulation              : 20);
        if (currentPop + pendingPopulation + populationPerUnit > maxPop)
        {
            if (!isEnemyOwned) ResourceManager.Instance?.AddResources(food, wood, gold);
            else               EnemyAI.Instance?.AddEnemyResources(food, wood, gold);
            Debug.Log($"[{buildingName}] Population limit reached.");
            return;
        }

        // ── Add to batch queue ──────────────────────────────────────────
        // If the LAST batch has the same unit type, just increment its count.
        // Otherwise add a new batch (max 5 batches total).
        bool lastBatchFull = trainingQueue.Count > 0
            && trainingQueue[trainingQueue.Count - 1].count >= 5;
        if (trainingQueue.Count >= 5 && lastBatchFull)
        {
            if (!isEnemyOwned) ResourceManager.Instance?.AddResources(food, wood, gold);
            else               EnemyAI.Instance?.AddEnemyResources(food, wood, gold);
            Debug.Log($"[{buildingName}] Queue full (max 5 batches of 5).");
            return;
        }

        bool canMerge = trainingQueue.Count > 0
            && trainingQueue[trainingQueue.Count - 1].unitIndex == index
            && trainingQueue[trainingQueue.Count - 1].count < 5;
        if (canMerge)
        {
            var last = trainingQueue[trainingQueue.Count - 1];
            trainingQueue[trainingQueue.Count - 1] = new TrainingBatch(last.unitIndex, last.count + 1);
        }
        else
        {
            trainingQueue.Add(new TrainingBatch(index, 1));
        }

        pendingPopulation += populationPerUnit;
        Debug.Log($"[{buildingName}] Queued. Batches in queue: {trainingQueue.Count}, pending pop: {pendingPopulation}");

        if (!isTraining)
            StartNextBatch();
    }

    // ── Training loop ────────────────────────────────────────────────────
    private void StartNextBatch()
    {
        if (trainingQueue.Count == 0) { isTraining = false; return; }

        currentBatch    = trainingQueue[0];
        trainingQueue.RemoveAt(0);
        currentUnitTime = GetTrainingTime(currentBatch.unitIndex);
        trainingTimer   = currentUnitTime;
        isTraining      = true;
        batchSpawnedSoFar = 0;
        Debug.Log($"[{buildingName}] Training: {currentBatch.count}x unit[{currentBatch.unitIndex}] over {currentUnitTime:F1}s");
    }

    private void TryCompleteBatch()
    {
        if (currentBatch.unitIndex < 0 || currentBatch.unitIndex >= spawnablePrefabs.Count)
        { StartNextBatch(); return; }

        GameObject prefab = spawnablePrefabs[currentBatch.unitIndex];
        if (prefab == null) { StartNextBatch(); return; }

        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        int spawned = 0;

        for (int i = batchSpawnedSoFar; i < currentBatch.count; i++)
        {
            Vector3 pos = FindFreeSpawnPosition(batchSpawnedSoFar + spawned);
            if (pos == Vector3.zero)
            {
                if (spawned > 0)
                {
                    int partialPop = spawned * populationPerUnit;
                    if (ownerPlayerId == PlayerColorManager.LocalPlayerIndex)
                        ResourceManager.Instance?.AddPopulation(partialPop);
                    else
                        EnemyAI.Instance?.AddPopulation(partialPop);
                    pendingPopulation  = Mathf.Max(0, pendingPopulation - partialPop);
                    batchSpawnedSoFar += spawned;
                }
                trainingTimer = 0.3f;
                return;
            }
            GameObject go = Instantiate(prefab, pos, spawnRot);
            if (go.TryGetComponent(out Unit unit))
                unit.SetOwner(ownerPlayerId);
            spawned++;
        }

        int popCost = spawned * populationPerUnit;
        if (ownerPlayerId == PlayerColorManager.LocalPlayerIndex)
            ResourceManager.Instance?.AddPopulation(popCost);
        else
            EnemyAI.Instance?.AddPopulation(popCost);
        pendingPopulation = Mathf.Max(0, pendingPopulation - popCost);
        batchSpawnedSoFar = 0;

        Debug.Log($"[{buildingName}] Batch complete — spawned {spawned}x {prefab.name}");
        StartNextBatch();
    }

    // ── Spawn position helpers ───────────────────────────────────────────
    private Vector3 FindFreeSpawnPosition(int spawnIndex)
    {
        Vector3 basePos = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.forward * 3f;

        // Ring search: centre, then ring 1 (8 pts), ring 2 (16 pts), ring 3 (24 pts)
        for (int ring = 0; ring <= 3; ring++)
        {
            int steps = ring == 0 ? 1 : ring * 8;
            for (int i = 0; i < steps; i++)
            {
                Vector3 candidate;
                if (ring == 0)
                {
                    candidate = basePos;
                }
                else
                {
                    float angle = (i + spawnIndex) * (360f / steps) * Mathf.Deg2Rad;
                    candidate = basePos + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle))
                                         * (spawnSpacing * ring);
                }

                if (IsPosClear(candidate, out Vector3 snapped))
                    return snapped;
            }
        }
        return Vector3.zero; // no free spot — caller retries
    }

    private bool IsPosClear(Vector3 pos, out Vector3 snapped)
    {
        snapped = pos;
        if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            return false;
        snapped = hit.position;
        Collider[] cols = Physics.OverlapSphere(snapped, 0.45f);
        foreach (Collider c in cols)
            if (c.GetComponentInParent<Unit>() != null) return false;
        return true;
    }

    // ── Cost / time helpers ──────────────────────────────────────────────
    private float GetTrainingTime(int index)
    {
        if (unitTrainingTimes != null && index < unitTrainingTimes.Count && unitTrainingTimes[index] > 0f)
            return unitTrainingTimes[index];
        return 5f; // default
    }

    private int GetCost(List<int> costList, int index)
    {
        if (costList != null && index < costList.Count) return costList[index];
        return 0;
    }

    // ── UI-facing properties ─────────────────────────────────────────────
    public bool  IsTraining      => isTraining;
    public float TrainingProgress => (isTraining && currentUnitTime > 0f)
                                        ? 1f - (trainingTimer / currentUnitTime) : 0f;
    public int   QueueBatchCount => trainingQueue.Count;

    /// <summary>"Infantry x3" or "Cavalry" for the unit currently training.</summary>
    public string CurrentTrainingLabel
    {
        get
        {
            if (!isTraining) return "";
            int idx = currentBatch.unitIndex;
            if (idx < 0 || idx >= spawnablePrefabs.Count || spawnablePrefabs[idx] == null) return "";
            string n = spawnablePrefabs[idx].name;
            return currentBatch.count > 1 ? $"{n} x{currentBatch.count}" : n;
        }
    }

    /// <summary>Labels for every queued batch, e.g. ["Infantry x2", "Cavalry"].</summary>
    public string[] GetQueueLabels()
    {
        string[] labels = new string[trainingQueue.Count];
        for (int i = 0; i < trainingQueue.Count; i++)
        {
            int idx = trainingQueue[i].unitIndex;
            string n = (idx >= 0 && idx < spawnablePrefabs.Count && spawnablePrefabs[idx] != null)
                ? spawnablePrefabs[idx].name : "?";
            labels[i] = trainingQueue[i].count > 1 ? $"{n} x{trainingQueue[i].count}" : n;
        }
        return labels;
    }
    // ── Combat damage (enemy units attacking buildings) ──────────────────
    public void TakeDamage(int amount)
    {
        currentBuildingHealth = Mathf.Max(0, currentBuildingHealth - amount);
        if (currentBuildingHealth <= 0) OnBuildingDestroyed();
    }

    private void OnBuildingDestroyed()
    {
        EnemyAI.Instance?.OnBuildingDestroyed(this);
        AllBuildings.Remove(this);
        Destroy(gameObject);
    }
    // ── Standard properties ──────────────────────────────────────────────
    public string            BuildingName     => buildingName;
    public List<GameObject>  SpawnablePrefabs => spawnablePrefabs;
    public int               OwnerPlayerId    => ownerPlayerId;
    public int               CurrentBuildingHealth => currentBuildingHealth;
    public int               MaxBuildingHealth     => maxBuildingHealth;
    public virtual int       TrainingPriority      => 0;

    public void SetOwner(int playerId) { ownerPlayerId = playerId; }
}