using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MinimapViewport v11.
/// Fixed: pivot/anchor issue that caused the indicator rectangle to appear offset.
/// The viewportIndicator RectTransform must have its pivot and anchor set to (0,0).
/// </summary>
[RequireComponent(typeof(Camera))]
public class MinimapViewport : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RawImage       minimapDisplay;
    [SerializeField] private RectTransform  viewportIndicator;
    [SerializeField] private Camera         mainCam;

    [Header("Settings")]
    [SerializeField] private float groundY = 0f;

    private Camera minimapCam;

    private void Awake()
    {
        minimapCam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (minimapCam        == null) return;
        if (mainCam           == null) return;
        if (minimapDisplay    == null) return;
        if (viewportIndicator == null) return;

        UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        // Project screen corners onto the ground plane
        Vector3[] corners =
        {
            new Vector3(0,            0,             1),
            new Vector3(Screen.width, 0,             1),
            new Vector3(Screen.width, Screen.height, 1),
            new Vector3(0,            Screen.height, 1),
        };

        Vector2 vpMin = new Vector2(float.MaxValue,  float.MaxValue);
        Vector2 vpMax = new Vector2(float.MinValue, float.MinValue);

        foreach (Vector3 c in corners)
        {
            Ray   ray = mainCam.ScreenPointToRay(c);
            float t;

            if (Mathf.Abs(ray.direction.y) < 0.001f) t = 1000f;
            else                                      t = (groundY - ray.origin.y) / ray.direction.y;
            if (t < 0) t = 1000f;

            Vector3 world = ray.origin + ray.direction * t;
            Vector3 vp    = minimapCam.WorldToViewportPoint(world);
            vpMin = Vector2.Min(vpMin, new Vector2(vp.x, vp.y));
            vpMax = Vector2.Max(vpMax, new Vector2(vp.x, vp.y));
        }

        vpMin = Vector2.Max(vpMin, Vector2.zero);
        vpMax = Vector2.Min(vpMax, Vector2.one);

        // Map viewport 0..1 to pixel size of the RawImage
        Rect dr = minimapDisplay.rectTransform.rect;

        float x = vpMin.x * dr.width;
        float y = vpMin.y * dr.height;
        float w = (vpMax.x - vpMin.x) * dr.width;
        float h = (vpMax.y - vpMin.y) * dr.height;

        // viewportIndicator must have anchor=(0,0) and pivot=(0,0) for this to be correct
        viewportIndicator.anchoredPosition = new Vector2(x, y);
        viewportIndicator.sizeDelta        = new Vector2(w, h);
    }
}
