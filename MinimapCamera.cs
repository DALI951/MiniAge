using UnityEngine;

/// <summary>
/// MinimapCamera — orthographic top-down camera showing the full map.
///
/// Fix Bug 1: position and rotation are locked in LateUpdate every frame,
/// preventing any accidental drift. orthographicSize is set in Start and
/// re-validated in LateUpdate so it can't be overwritten by other systems.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MinimapCamera : MonoBehaviour
{
    [Header("Must match MapBoundary / SpawnAreaManager Half Size")]
    [SerializeField] private float mapHalfSize = 23f;
    [SerializeField] private float padding      = 4f;

    [Header("Height above scene")]
    [SerializeField] private float height = 300f;

    private Camera cam;
    private float  targetSize;

    private void Awake()
    {
        cam        = GetComponent<Camera>();
        targetSize = mapHalfSize + padding;
        Apply();
    }

    private void Start()  => Apply();

    // Re-apply every late update so nothing can accidentally move/resize this camera
    private void LateUpdate() => Apply();

    private void Apply()
    {
        cam.orthographic     = true;
        cam.orthographicSize = targetSize;
        transform.position   = new Vector3(0f, height, 0f);
        transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
    }

#if UNITY_EDITOR
    // Show the camera's view area as a green square in Scene view
    private void OnDrawGizmos()
    {
        float s = mapHalfSize + padding;
        Gizmos.color = Color.green;
        Vector3 c    = new Vector3(0, 0, 0);
        Gizmos.DrawWireCube(c, new Vector3(s * 2f, 0.1f, s * 2f));
    }
#endif
}
