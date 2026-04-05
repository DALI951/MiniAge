using UnityEngine;

/// <summary>
/// RTSCamera — WASD / edge-pan + scroll zoom.
/// Feature 3: camera position is clamped to MapBoundary every frame.
/// </summary>
public class RTSCamera : MonoBehaviour
{
    [Header("Pan")]
    [SerializeField] private float panSpeed        = 20f;
    [SerializeField] private float edgePanThreshold = 10f;
    [SerializeField] private bool  useEdgePan      = true;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minHeight = 5f;
    [SerializeField] private float maxHeight = 50f;
    private void Start()
    {
        float fraction = 200f / Screen.height;
        Camera.main.rect = new Rect(0, fraction, 1, 1 - fraction);
        // Find player 0's spawn point and center the camera there
        SpawnAreaManager area = FindObjectOfType<SpawnAreaManager>();
        if (area != null)
        {
            Vector3 spawnPos = area.GetSpawnPosition(
                PlayerColorManager.LocalPlayerIndex);
            transform.position = new Vector3(spawnPos.x, 20f, spawnPos.z - 10f);
        }
    }
    private void Update()
    {
        HandlePan();
        HandleZoom();
        ClampToMap();
    }

    private void HandlePan()
    {
        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    move += Vector3.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  move += Vector3.back;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  move += Vector3.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += Vector3.right;

        if (useEdgePan)
        {
            Vector2 m = Input.mousePosition;
            if (m.x < edgePanThreshold)                 move += Vector3.left;
            if (m.x > Screen.width  - edgePanThreshold) move += Vector3.right;
            if (m.y < edgePanThreshold)                 move += Vector3.back;
            if (m.y > Screen.height - edgePanThreshold) move += Vector3.forward;
        }

        transform.Translate(move.normalized * panSpeed * Time.deltaTime, Space.World);
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;

        Vector3 pos = transform.position;
        pos.y -= scroll * zoomSpeed * 10f;
        pos.y  = Mathf.Clamp(pos.y, minHeight, maxHeight);
        transform.position = pos;
    }

    private void ClampToMap()
    {
        if (MapBoundary.Instance == null) return;

        float limit = MapBoundary.Instance.CameraLimit;
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -limit, limit);
        pos.z = Mathf.Clamp(pos.z, -limit, limit);
        transform.position = pos;
    }
}
