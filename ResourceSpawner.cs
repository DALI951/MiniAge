using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ResourceSpawner — places Trees, Mines, Animals at game start.
///
/// Fix Bug 4: each candidate position is tested against already-placed
/// nodes using a minimum separation radius before instantiating.
/// </summary>
public class ResourceSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private GameObject minePrefab;
    [SerializeField] private GameObject animalPrefab;

    [Header("Map")]
    [SerializeField] private float mapHalfSize   = 23f;
    [SerializeField] private float edgeMargin    = 3f;

    [Header("Global counts")]
    [SerializeField] private int totalTrees   = 25;
    [SerializeField] private int totalMines   = 12;
    [SerializeField] private int totalAnimals = 15;

    [Header("Guaranteed near each spawn point")]
    [SerializeField] private float nearSpawnRadius = 10f;
    [SerializeField] private int   treesPerSpawn   = 2;
    [SerializeField] private int   minesPerSpawn   = 1;
    [SerializeField] private int   animalsPerSpawn = 1;

    [Header("Spacing (fix overlap)")]
    [SerializeField] private float minNodeSeparation = 2.5f; // minimum distance between any two nodes
    [SerializeField] private float spawnClearance    = 4f;   // clear radius around each spawn point

    private SpawnAreaManager spawnArea;

    private void Start()
    {
        spawnArea = FindObjectOfType<SpawnAreaManager>();

        // Phase 1 — guaranteed clusters near spawn points
        if (spawnArea != null)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 sp = spawnArea.GetSpawnPosition(i);
                SpawnCluster(treePrefab,   treesPerSpawn,   sp, nearSpawnRadius, spawnClearance);
                SpawnCluster(minePrefab,   minesPerSpawn,   sp, nearSpawnRadius, spawnClearance);
                SpawnCluster(animalPrefab, animalsPerSpawn, sp, nearSpawnRadius, spawnClearance);
            }
        }

        // Phase 2 — scatter rest of map
        SpawnRandom(treePrefab,   totalTrees);
        SpawnRandom(minePrefab,   totalMines);
        SpawnRandom(animalPrefab, totalAnimals);
    }

    // ─────────────────────────────────────────────────────────────────────

    private void SpawnCluster(GameObject prefab, int count,
        Vector3 center, float maxRadius, float minRadius)
    {
        if (prefab == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3? pos = FindValidPos(center, minRadius, maxRadius);
            if (pos.HasValue)
                Instantiate(prefab, pos.Value, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
        }
    }

    private void SpawnRandom(GameObject prefab, int count)
    {
        if (prefab == null) return;
        float limit = mapHalfSize - edgeMargin;
        for (int i = 0; i < count; i++)
        {
            Vector3 center = new Vector3(
                Random.Range(-limit, limit), 0,
                Random.Range(-limit, limit));
            Vector3? pos = FindValidPos(center, 0, 3f);
            if (pos.HasValue)
                Instantiate(prefab, pos.Value, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
        }
    }

    /// <summary>
    /// Tries up to 15 times to find a position that:
    /// 1. Is on the NavMesh
    /// 2. Is within the map
    /// 3. Is at least minNodeSeparation away from every other resource node
    /// </summary>
    private Vector3? FindValidPos(Vector3 center, float minDist, float maxDist)
    {
        float limit = mapHalfSize - edgeMargin;

        for (int attempt = 0; attempt < 15; attempt++)
        {
            Vector2 rand   = Random.insideUnitCircle.normalized *
                             Random.Range(Mathf.Max(0.5f, minDist), maxDist);
            Vector3 cand   = center + new Vector3(rand.x, 0, rand.y);

            // Clamp to map
            cand.x = Mathf.Clamp(cand.x, -limit, limit);
            cand.z = Mathf.Clamp(cand.z, -limit, limit);

            // Snap to NavMesh
            if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;

            Vector3 snapped = hit.position;

            // Check separation from existing nodes (fixes overlap Bug 4)
            if (!ClearOfOtherNodes(snapped))
                continue;

            return snapped;
        }
        return null;
    }

    /// <summary>Returns true if pos is far enough from every existing ResourceNode.</summary>
    private bool ClearOfOtherNodes(Vector3 pos)
    {
        foreach (ResourceNode n in FindObjectsOfType<ResourceNode>())
        {
            if (Vector3.Distance(pos, n.transform.position) < minNodeSeparation)
                return false;
        }
        return true;
    }
}
