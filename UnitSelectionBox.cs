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
    [SerializeField] private RectTransform bottomBar;

    Vector2 startPosition;      // screen-space start of drag
    Vector2 endPosition;
    bool    isDragging       = false;
    bool    hasDragOrigin    = false; // replaces checking startPosition == Vector2.zero (ambiguous at screen origin)
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

        if (isDown && !isDragging && !hasDragOrigin && !IsMouseOverBottomPanel())
        {
            hasDragOrigin = true;
            startPosition = Input.mousePosition;
            endPosition   = startPosition;
            startOnUI     = EventSystem.current != null &&
                            EventSystem.current.IsPointerOverGameObject();
        }

        if (isDown && !startOnUI && !IsMouseOverBottomPanel())
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

        var list = UnitSelectionManager.Instance?.allUnitsList;

        if (list == null)
        { Debug.LogError("[SelectionBox] UnitSelectionManager list is NULL"); return; }

        foreach (Unit unit in list)
        {
            if (unit == null) continue;
            Vector3 sp = myCam.WorldToScreenPoint(unit.transform.position);
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
        hasDragOrigin = false;
        startOnUI     = false;
        startPosition = Vector2.zero;
        endPosition   = Vector2.zero;
        if (boxVisual != null) boxVisual.gameObject.SetActive(false);
        StartCoroutine(ResetDragFlag());
    }
    private bool IsMouseOverBottomPanel()
    {
        if (bottomBar == null) return false;
        Vector3[] corners = new Vector3[4];
        bottomBar.GetWorldCorners(corners);
        // corners[0] = bottom-left, corners[1] = top-left in world space
        // Convert to screen space
        float panelScreenTop = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
        return Input.mousePosition.y < panelScreenTop;
    }
}
