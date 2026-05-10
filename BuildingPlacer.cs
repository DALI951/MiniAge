using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;

public class BuildingPlacer : MonoBehaviour
{
    public static BuildingPlacer Instance { get; private set; }

    [Header("Layers")]
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private float buildingYOffset = 1f;
    
    private GameObject    ghostObject;
    private GameObject    buildingPrefab;
    private GameObject    sitePrefab;
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

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
        {
            // FIX: Snap ghost to NavMesh height
            Vector3 ghostPos = hit.point;
            if (NavMesh.SamplePosition(ghostPos, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
                ghostPos = new Vector3(ghostPos.x, navHit.position.y, ghostPos.z);

            if (ghostObject != null)
                ghostObject.transform.position = ghostPos;

            if (Input.GetMouseButtonDown(0) &&
                !EventSystem.current.IsPointerOverGameObject())
            {
                PlaceBuilding(hit.point);
            }
        }
    }

    public void StartPlacing(GameObject buildPrefab, GameObject constructionSitePrefab,
        float time, Material ghostMaterial)
    {
        CancelPlacement();

        buildingPrefab = buildPrefab;
        sitePrefab     = constructionSitePrefab;
        buildTime      = time;
        isPlacing      = true;

        ghostObject = Instantiate(buildPrefab);
        ghostObject.name = "GhostPreview";

        foreach (Renderer r in ghostObject.GetComponentsInChildren<Renderer>())
        {
            if (ghostMaterial != null)
                r.material = ghostMaterial;
        }

        foreach (MonoBehaviour mb in ghostObject.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;
        foreach (Collider c in ghostObject.GetComponentsInChildren<Collider>())
            c.enabled = false;
        foreach (UnityEngine.AI.NavMeshObstacle o in
            ghostObject.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
            o.enabled = false;
    }

    private void PlaceBuilding(Vector3 pos)
    {
        if (sitePrefab == null) return;

        // FIX: Snap to NavMesh for correct Y height
        if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 10f, NavMesh.AllAreas))
            pos = navHit.position;
        else
            pos.y = 0f;

        if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out RaycastHit groundHit, 20f))
            pos.y = groundHit.point.y;

        pos.y += buildingYOffset;
        GameObject siteGO = Instantiate(sitePrefab, pos, Quaternion.identity);
        ConstructionSite site = siteGO.GetComponent<ConstructionSite>();
        site?.Initialize(buildingPrefab, buildTime);

        foreach (Unit u in SelectionManager.Instance?.SelectedUnits ?? 
            new System.Collections.Generic.List<Unit>())
        {
            if (u is Villager v)
                v.BuildAt(site);
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