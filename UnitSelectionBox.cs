using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UnitSelectionBox — drag-to-select rectangle.
///
/// Fix Bug 3: startPosition is now recorded at the very first line of
/// GetMouseButtonDown, before any EventSystem or UI checks.
/// This prevents the box from starting at (0,0) when the click origin
/// was briefly on a UI element.
///
/// Attach to GameManager.
/// Assign boxVisual (UI Image, pivot+anchor = 0.5,0.5) and uiCanvas.
/// </summary>
public class UnitSelectionBox : MonoBehaviour
{
    Camera myCam;

    [SerializeField] RectTransform boxVisual;
    [SerializeField] Canvas        uiCanvas;
    [SerializeField] float         dragThreshold = 8f;

    Rect    selectionBox;
    Vector2 startPosition;      // screen-space start of drag
    Vector2 endPosition;
    bool    isDragging       = false;
    bool    startOnUI        = false; // was the click origin on a UI element?
    public static UnitSelectionBox Instance { get; private set; }
    public bool JustFinishedDrag { get; private set; }
    private void Start()
    {
        Instance = this;
        myCam = Camera.main;
        if (boxVisual != null) boxVisual.gameObject.SetActive(false);
    }
    private System.Collections.IEnumerator ResetDragFlag()
    {
        yield return null;
        JustFinishedDrag = false;
    }
    private bool wasMouseDown = false;

    private void Update()
    {
        bool isDown = Input.GetMouseButton(0);
        bool justReleased = wasMouseDown && !isDown;
        wasMouseDown = isDown;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject() && !isDragging) 
        {
            if (justReleased && isDragging)
            {
                endPosition = Input.mousePosition;
                JustFinishedDrag = true;
                SelectUnits();
                CancelDrag();
            }
            return;
        }

        if (isDown && !wasMouseDown == false && !isDragging && startPosition == Vector2.zero)
        {
            startPosition = Input.mousePosition;
            endPosition   = startPosition;
            selectionBox  = new Rect();
            startOnUI     = EventSystem.current != null &&
                            EventSystem.current.IsPointerOverGameObject();
        }

        if (isDown && !startOnUI)
        {
            endPosition = Input.mousePosition;

            if (!isDragging &&
                Vector2.Distance(startPosition, endPosition) > dragThreshold)
            {
                isDragging = true;
                if (boxVisual != null) boxVisual.gameObject.SetActive(true);
            }

            if (isDragging)
            {
                DrawVisual();
                selectionBox.xMin = Mathf.Min(startPosition.x, endPosition.x);
                selectionBox.xMax = Mathf.Max(startPosition.x, endPosition.x);
                selectionBox.yMin = Mathf.Min(startPosition.y, endPosition.y);
                selectionBox.yMax = Mathf.Max(startPosition.y, endPosition.y);
            }
        }

        if (justReleased)
        {
            endPosition = Input.mousePosition;
            if (isDragging && !startOnUI)
            {
                JustFinishedDrag = true;
                SelectUnits();
            }
            CancelDrag();
        }
    }

    // ── Draw the UI rectangle ─────────────────────────────────────────────
    void DrawVisual()
    {
        if (boxVisual == null) return;

        Vector2 s = startPosition;
        Vector2 e = endPosition;

        // Always convert screen positions to canvas local space
        RectTransform canvasRect = uiCanvas != null
            ? uiCanvas.GetComponent<RectTransform>()
            : null;

        if (canvasRect != null)
        {
            Camera uiCam = uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : uiCanvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, startPosition, uiCam, out s);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, endPosition, uiCam, out e);
        }

        boxVisual.anchoredPosition = (s + e) / 2f;
        boxVisual.sizeDelta = new Vector2(
            Mathf.Abs(s.x - e.x),
            Mathf.Abs(s.y - e.y));
    }

    // ── Build screen-space Rect ───────────────────────────────────────────
    void DrawSelection()
    {
        selectionBox.xMin = Mathf.Min(startPosition.x, endPosition.x);
        selectionBox.xMax = Mathf.Max(startPosition.x, endPosition.x);
        selectionBox.yMin = Mathf.Min(startPosition.y, endPosition.y);
        selectionBox.yMax = Mathf.Max(startPosition.y, endPosition.y);
    }

    // ── Select units inside the rect ──────────────────────────────────────
    void SelectUnits()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);

        if (!shift) SelectionManager.Instance?.DeselectAll();

        Rect currentRect = new Rect(
            Mathf.Min(startPosition.x, endPosition.x),
            Mathf.Min(startPosition.y, endPosition.y),
            Mathf.Abs(endPosition.x - startPosition.x),
            Mathf.Abs(endPosition.y - startPosition.y));

        Debug.Log($"[SelectionBox] Rect: {currentRect}");

        var list = UnitSelectionManager.Instance?.allUnitsList;

        if (list == null)
        { Debug.LogError("[SelectionBox] UnitSelectionManager list is NULL"); return; }

        Debug.Log($"[SelectionBox] Checking {list.Count} units");

        foreach (Unit unit in list)
        {
            if (unit == null) continue;
            Vector3 sp = myCam.WorldToScreenPoint(unit.transform.position);
            Debug.Log($"[SelectionBox] Unit {unit.UnitName} screenPos={sp}, inRect={currentRect.Contains(new Vector2(sp.x, sp.y))}");
            if (sp.z < 0) continue;
            if (currentRect.Contains(new Vector2(sp.x, sp.y)))
                SelectionManager.Instance?.AddUnitToSelection(unit);
        }

        SelectionManager.Instance?.RefreshInfoPanelPublic();
    }

    // ── Reset ─────────────────────────────────────────────────────────────
    void CancelDrag()
    {
        isDragging    = false;
        startOnUI     = false;
        startPosition = Vector2.zero;
        endPosition   = Vector2.zero;
        selectionBox  = new Rect();
        if (boxVisual != null) boxVisual.gameObject.SetActive(false);
        StartCoroutine(ResetDragFlag());
    }
}
