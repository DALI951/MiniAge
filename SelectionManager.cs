using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// SelectionManager — right-click move/gather, ground deselect, shift-click, double-click.
///
/// Feature 1: Group movement by unit type.
///   When multiple unit types are selected and right-clicked:
///   - All Infantry form one cluster
///   - All Cavalry form another cluster offset to the side
///   - All Villagers form another cluster
///   Each type moves at its own fastest speed internally but the
///   formation keeps them separated cleanly.
/// </summary>
public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }
    private List<Vector3> waypoints = new List<Vector3>();

    [Header("Layers")]
    [SerializeField] private LayerMask resourceLayer;
    [SerializeField] private LayerMask groundLayer;

    [Header("Group Move")]
    [SerializeField] private float groupSpacing    = 2.2f;   // within a type cluster
    [SerializeField] private float typeGroupOffset = 6f;     // between type clusters

    private List<Unit> selectedUnits    = new List<Unit>();
    private Building   selectedBuilding = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;
        HandleGroundDeselect();
        HandleRightClick();
    }

    // ── Ground click — deselect + close panels ────────────────────────────
    private void HandleGroundDeselect()
    {
        if (!Input.GetMouseButtonUp(0)) return;

        // Don't deselect if a drag-select just finished
        if (UnitSelectionBox.Instance != null && UnitSelectionBox.Instance.JustFinishedDrag)
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        bool hitUnit     = hit.collider.GetComponentInParent<Unit>()         != null;
        bool hitBuilding = hit.collider.GetComponentInParent<Building>()     != null;
        bool hitResource = hit.collider.GetComponentInParent<ResourceNode>() != null;

        if (!hitUnit && !hitBuilding && !hitResource)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) ||
                        Input.GetKey(KeyCode.RightShift);
            if (!shift) { DeselectAll(); CloseAllPanels(); }
        }
    }

    // ── Right-click: gather or move ───────────────────────────────────────
    private void HandleRightClick()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        bool shift = Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Hit an enemy unit? → attack it
        if (selectedUnits.Count > 0 &&
            Physics.Raycast(ray, out RaycastHit unitHit, Mathf.Infinity))
        {
            Unit target = unitHit.collider.GetComponentInParent<Unit>();
            if (target != null && !selectedUnits.Contains(target))
            {
                foreach (Unit u in selectedUnits)
                {
                    if (u is Villager) continue; // villagers don't attack on command
                    u.SetAttackTarget(target);
                }
                // Clear movement flags when attacking
                MoveFlag.Instance?.ClearAllFlags();
                return;
            }
        }

        // Resource node
        if (selectedUnits.Count > 0 &&
            Physics.Raycast(ray, out RaycastHit resHit, Mathf.Infinity, resourceLayer))
        {
            ResourceNode node = resHit.collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                // Clear movement flags when gathering
                MoveFlag.Instance?.ClearAllFlags();
                foreach (Unit u in selectedUnits)
                {
                    if (u is Villager v) v.GatherFrom(node);
                    else                 u.MoveTo(node.transform.position);
                }
                return;
            }
        }

        // Ground — move in type-based formation
        if (selectedUnits.Count == 0) return;
        if (!Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, groundLayer)) return;
        
        if (shift)
        {
            // Add to waypoint queue - show persistent flag
            waypoints.Add(groundHit.point);
            MoveFlag.Instance?.ShowFlag(groundHit.point, addToPath: true);
            // If only one waypoint so far, start moving
            if (waypoints.Count == 1)
                StartCoroutine(ProcessWaypoints());
        }
        else   
        {
            // Single destination - show flag and move
            waypoints.Clear();
            MoveFlag.Instance?.ClearAllFlags();
            MoveFlag.Instance?.ShowFlag(groundHit.point, addToPath: false);
            MoveByType(groundHit.point);
        }
    }




    private System.Collections.IEnumerator ProcessWaypoints()
    {
        while (waypoints.Count > 0)
        {
            Vector3 next = waypoints[0];
            MoveByType(next);
            
            // Track units for this waypoint
            MoveFlag.Instance?.StartTrackingUnits(next, selectedUnits);

            // Wait until units arrive
            yield return new WaitForSeconds(0.5f);
            while (true)
            {
                bool arrived = true;
                foreach (Unit u in selectedUnits)
                {
                    if (u == null) continue;
                    var ag = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (ag != null && ag.remainingDistance > 1.5f)
                    { arrived = false; break; }
                }
                if (arrived) break;
                yield return new WaitForSeconds(0.3f);
            }

            waypoints.RemoveAt(0);
            MoveFlag.Instance?.ClearFlags();

            // Show remaining waypoint flags
            foreach (Vector3 wp in waypoints)
                MoveFlag.Instance?.ShowFlag(wp, addToPath: true);
        }
    }


    // ── Group move sorted by unit type (Feature 1) ────────────────────────
    private void MoveByType(Vector3 center)
    {
        // Group units by their UnitName (Infantry, Cavalry, Villager, etc.)
        var groups = new Dictionary<string, List<Unit>>();
        foreach (Unit u in selectedUnits)
        {
            if (u == null) continue;
            if (!groups.ContainsKey(u.UnitName))
                groups[u.UnitName] = new List<Unit>();
            groups[u.UnitName].Add(u);
        }

        // Lay groups side by side, each in its own sub-formation
        int groupIndex = 0;
        int groupCount = groups.Count;
        float totalWidth = (groupCount - 1) * typeGroupOffset;

        foreach (var kvp in groups)
        {
            List<Unit> group = kvp.Value;

            // Offset this type's centre left/right from the click point
            float xShift = -totalWidth / 2f + groupIndex * typeGroupOffset;
            Vector3 groupCenter = center + new Vector3(xShift, 0, 0);

            // Within the group, move at the group's slowest speed
            float slowest = float.MaxValue;
            foreach (Unit u in group)
                if (u != null && u.BaseSpeed < slowest) slowest = u.BaseSpeed;

            int cols = Mathf.CeilToInt(Mathf.Sqrt(group.Count));
            for (int i = 0; i < group.Count; i++)
            {
                Unit u = group[i];
                if (u == null) continue;
                int   col  = i % cols;
                int   row  = i / cols;
                float xOff = (col - (cols - 1) / 2f) * groupSpacing;
                float zOff = -row * groupSpacing;
                u.SetMoveSpeed(slowest);
                u.MoveTo(groupCenter + new Vector3(xOff, 0, zOff));
            }

            groupIndex++;
        }

        StartCoroutine(RestoreSpeedsWhenArrived());
        bool shift = Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);

        MoveFlag.Instance?.ShowFlag(center, addToPath: shift);

        if (!shift)
        {
            MoveFlag.Instance?.StartTrackingUnits(center, selectedUnits);
        }
    }

    private IEnumerator RestoreSpeedsWhenArrived()
    {
        yield return new WaitForSeconds(0.3f);
        while (true)
        {
            bool done = true;
            foreach (Unit u in selectedUnits)
            {
                if (u == null) continue;
                var ag = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (ag != null && ag.isOnNavMesh && ag.remainingDistance > 0.5f)
                { done = false; break; }
            }
            if (done) break;
            yield return null;
        }
        foreach (Unit u in selectedUnits)
            if (u != null) u.RestoreSpeed();
    }

    // ── PUBLIC — called by Unit.OnMouseDown ───────────────────────────────

    public void SelectSingleUnit(Unit unit)
    {
        DeselectAll();
        selectedUnits.Add(unit);
        unit.Select(PlayerColorManager.LocalPlayerColor);
        UnitInfoUI.Instance?.ShowUnit(unit);
        GameUI.Instance?.HideBuildingUI();
        ResourceInfoUI.Instance?.Hide();
    }

    public void ShiftClickUnit(Unit unit)
    {
        if (selectedUnits.Contains(unit))
        { unit.Deselect(); selectedUnits.Remove(unit); }
        else
        { selectedUnits.Add(unit); unit.Select(PlayerColorManager.LocalPlayerColor); }
        RefreshInfoPanelPublic();
    }

    public void SelectAllVisibleOfType(string name)
    {
        DeselectAll();
        foreach (Unit u in FindObjectsOfType<Unit>())
        {
            if (u.UnitName != name) continue;
            Vector3 sp = Camera.main.WorldToScreenPoint(u.transform.position);
            if (sp.z < 0 || sp.x < 0 || sp.x > Screen.width ||
                sp.y < 0 || sp.y > Screen.height) continue;
            selectedUnits.Add(u);
            u.Select(PlayerColorManager.LocalPlayerColor);
        }
        RefreshInfoPanelPublic();
    }

    public void SelectBuilding(Building building)
    {
        DeselectAll();
        selectedBuilding = building;
        selectedBuilding.Select();
        UnitInfoUI.Instance?.Hide();
        ResourceInfoUI.Instance?.Hide();
    }

    // ── PUBLIC — called by UnitSelectionBox ───────────────────────────────

    public void AddUnitToSelection(Unit unit)
    {
        if (unit == null || selectedUnits.Contains(unit)) return;
        selectedUnits.Add(unit);
        unit.Select(PlayerColorManager.LocalPlayerColor);
    }

    public void RefreshInfoPanelPublic() => RefreshInfoPanel();

    // ── Deselect ─────────────────────────────────────────────────────────

    public void DeselectAll()
    {
        foreach (Unit u in selectedUnits) if (u != null) u.Deselect();
        selectedUnits.Clear();
        if (selectedBuilding != null)
        {
            selectedBuilding.Deselect();
            selectedBuilding = null;
            GameUI.Instance?.HideBuildingUI();
        }
        UnitInfoUI.Instance?.Hide();
    }

    private void CloseAllPanels()
    {
        GameUI.Instance?.HideBuildingUI();
        UnitInfoUI.Instance?.Hide();
        ResourceInfoUI.Instance?.Hide();
    }

    private void RefreshInfoPanel()
    {
        if      (selectedUnits.Count == 1) UnitInfoUI.Instance?.ShowUnit(selectedUnits[0]);
        else if (selectedUnits.Count > 1)  UnitInfoUI.Instance?.ShowMultiple(selectedUnits);
        else                               UnitInfoUI.Instance?.Hide();
    }

    public List<Unit> SelectedUnits    => selectedUnits;
    public Building   SelectedBuilding => selectedBuilding;
}
