using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Building v11.
/// OnMouseDown = primary click handler (no layer config needed).
/// Auto-adds BoxCollider at Start if none found.
/// </summary>
public class Building : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] protected Transform        spawnPoint;
    [SerializeField] protected List<GameObject> spawnablePrefabs = new List<GameObject>();
    [SerializeField] private   float            spawnSpacing     = 1.5f;
    [SerializeField] private   int              unitsPerRow      = 5;

    [Header("Building Info")]
    [SerializeField] protected string buildingName = "Building";
    [Header("Health")]
    [SerializeField] private int maxBuildingHealth = 500;
    private int currentBuildingHealth;

    private bool isSelected   = false;
    private int  spawnCounter = 0;
    private float lastSpawnTime = -99f;

    protected virtual void Start()
    {
        currentBuildingHealth = maxBuildingHealth;
        // Auto-add collider so OnMouseDown always fires
        if (GetComponent<Collider>() == null &&
            GetComponentInChildren<Collider>() == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.size   = new Vector3(2f, 2f, 2f);
            bc.center = new Vector3(0f, 1f, 0f);
        }

        if (spawnPoint == null)
            Debug.LogWarning($"[{buildingName}] No spawnPoint assigned! Units will spawn at building origin.");
    }
    public void SetSpawnPoint(Vector3 worldPos)
    {
        if (spawnPoint != null)
            spawnPoint.position = worldPos;
    }

    // ── Direct click — works without any layer setup ──────────────────────
    private void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        SelectionManager.Instance?.SelectBuilding(this);
        BuildingInfoUI.Instance?.ShowBuilding(this);
    }

    // ─── Selection ───────────────────────────────────────────────────────
    public virtual void Select()
    {
        isSelected = true;
        Debug.Log($"[{buildingName}] Selected.");
        GameUI.Instance?.ShowBuildingUI(this);
    }

    public virtual void Deselect()
    {
        isSelected = false;
    }

    // ─── Spawning ────────────────────────────────────────────────────────
    public void SpawnUnit(int index)
    {
        if (index < 0 || index >= spawnablePrefabs.Count)
        { Debug.LogError($"[{buildingName}] Bad index {index}"); return; }

        GameObject prefab = spawnablePrefabs[index];
        if (prefab == null)
        { Debug.LogError($"[{buildingName}] Null prefab at {index}"); return; }

        Vector3    basePos  = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.forward * 3f;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        int   col   = spawnCounter % unitsPerRow;
        int   row   = spawnCounter / unitsPerRow;
        float xOff  = (col - (unitsPerRow - 1) / 2f) * spawnSpacing;
        float zOff  = -row * spawnSpacing;

        Vector3 right   = spawnPoint != null ? spawnPoint.right   : transform.right;
        Vector3 forward = spawnPoint != null ? spawnPoint.forward : transform.forward;
        Vector3 pos     = basePos + right * xOff + forward * zOff;

        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            pos = hit.position;

        // Reset counter if enough time passed since last spawn
        if (Time.time - lastSpawnTime > 2f)
            spawnCounter = 0;

        Instantiate(prefab, pos, spawnRot);
        spawnCounter++;
        lastSpawnTime = Time.time;
        Debug.Log($"[{buildingName}] Spawned {prefab.name}");
    }
    // ─── Properties ──────────────────────────────────────────────────────
    public string            BuildingName     => buildingName;
    public bool              IsSelected       => isSelected;
    public List<GameObject>  SpawnablePrefabs => spawnablePrefabs;
}
