using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles ghost preview and placement of buildings.
/// Activated when a build button is clicked.
/// Attach to GameManager.
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    public static BuildingPlacer Instance { get; private set; }

    [Header("Layers")]
    [SerializeField] private LayerMask groundLayer;

    // Runtime
    private GameObject    ghostObject;      // transparent preview
    private GameObject    buildingPrefab;   // real building to place
    private GameObject    sitePrefab;       // construction site prefab
    private float         buildTime;
    private bool          isPlacing = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!isPlacing) return;

        // Cancel with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        { CancelPlacement(); return; }

        // Move ghost to mouse position
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
        {
            if (ghostObject != null)
                ghostObject.transform.position = hit.point;

            // Place on left click
            if (Input.GetMouseButtonDown(0) &&
                !EventSystem.current.IsPointerOverGameObject())
            {
                PlaceBuilding(hit.point);
            }
        }
    }

    /// <summary>Start placement mode for a building type.</summary>
    public void StartPlacing(GameObject buildPrefab, GameObject constructionSitePrefab,
        float time, Material ghostMaterial)
    {
        CancelPlacement();

        buildingPrefab = buildPrefab;
        sitePrefab     = constructionSitePrefab;
        buildTime      = time;
        isPlacing      = true;

        // Create ghost preview
        ghostObject = Instantiate(buildPrefab);
        ghostObject.name = "GhostPreview";

        // Make ghost transparent
        foreach (Renderer r in ghostObject.GetComponentsInChildren<Renderer>())
        {
            if (ghostMaterial != null)
                r.material = ghostMaterial;
        }

        // Disable all scripts and colliders on ghost
        foreach (MonoBehaviour mb in ghostObject.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;
        foreach (Collider c in ghostObject.GetComponentsInChildren<Collider>())
            c.enabled = false;

        // Disable navmesh obstacle if any
        foreach (UnityEngine.AI.NavMeshObstacle o in
            ghostObject.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
            o.enabled = false;
    }

    private void PlaceBuilding(Vector3 pos)
    {
        if (sitePrefab == null) return;

        // Snap to NavMesh
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit navHit,
            3f, UnityEngine.AI.NavMesh.AllAreas))
            pos = navHit.position;

        // Create construction site
        GameObject siteGO = Instantiate(sitePrefab, pos, Quaternion.identity);
        ConstructionSite site = siteGO.GetComponent<ConstructionSite>();
        site?.Initialize(buildingPrefab, buildTime);

        // Tell selected villagers to build it
        foreach (Unit u in SelectionManager.Instance?.SelectedUnits ?? 
            new System.Collections.Generic.List<Unit>())
        {
            if (u is Villager v)
            {
                v.BuildAt(site);
            }
        }

        CancelPlacement();
    }

    public void CancelPlacement()
    {
        isPlacing = false;
        if (ghostObject != null) Destroy(ghostObject);
        ghostObject = null;
    }

    public bool IsPlacing => isPlacing;
}