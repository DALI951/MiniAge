using UnityEngine;

[DefaultExecutionOrder(-50)]
public class FogOfWar : MonoBehaviour
{
    public static FogOfWar Instance { get; private set; }

    [Header("World-Space Rendering")]
    [SerializeField] private MeshRenderer fogPlaneRenderer;

    [Header("Map Settings")]
    [SerializeField] private float mapHalfSize = 23f;

    [Header("Grid Resolution")]
    [SerializeField] private int resolution = 128;

    [Header("Colors")]
    [SerializeField] private Color unexploredColor = new Color(0.02f, 0.02f, 0.05f, 1f);
    [SerializeField] private Color exploredColor   = new Color(0.15f, 0.12f, 0.08f, 0.50f);
    [SerializeField] private Color visibleColor    = new Color(0f, 0f, 0f, 0f);

    // Runtime
    private Texture2D fogTexture;
    private byte[]    cellState;
    private Color[]   pixels;
    private bool      dirty = true;
    private Material  fogMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (MapBoundary.Instance != null)
            mapHalfSize = MapBoundary.Instance.HalfSize;

        if (fogPlaneRenderer == null)
        {
            Debug.LogError("[FogOfWar] Fog Plane Renderer NOT assigned!");
            enabled = false;
            return;
        }

        fogMaterial = fogPlaneRenderer.material;
        fogMaterial.SetFloat("_MapHalfSize", mapHalfSize);

        fogTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        fogTexture.filterMode = FilterMode.Bilinear;
        fogTexture.wrapMode   = TextureWrapMode.Clamp;
        cellState = new byte[resolution * resolution];
        pixels    = new Color[resolution * resolution];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = unexploredColor;

        fogTexture.SetPixels(pixels);
        fogTexture.Apply();
        fogMaterial.SetTexture("_FogTex", fogTexture);
    }

    private void Start()
    {
        // CRITICAL FIX: Much smaller starting reveal
        // Was: max(20, mapHalfSize * 0.12) → 60 for 1000x1000
        // Now: mapHalfSize * 0.04, clamped to 15 max
        // For 1000x1000: 500 * 0.04 = 20 units
        // For 100x100: 50 * 0.04 = 2 units → clamped to 15

        float revealRadius = Mathf.Clamp(mapHalfSize * 0.04f, 8f, 20f);

        Vector3 revealCenter = Vector3.zero;
        SpawnAreaManager area = FindObjectOfType<SpawnAreaManager>();
        if (area != null)
            revealCenter = area.GetSpawnPosition(PlayerColorManager.LocalPlayerIndex);

        Debug.Log($"[FogOfWar] Starting reveal radius: {revealRadius:F1} at {revealCenter}");
        Reveal(revealCenter, revealRadius);
    }

    public void Reveal(Vector3 worldPos, float radiusWorld)
    {
        int cx = WorldToCell(worldPos.x);
        int cy = WorldToCell(worldPos.z);
        int cr = Mathf.CeilToInt(radiusWorld / (mapHalfSize * 2f) * resolution);

        for (int dy = -cr; dy <= cr; dy++)
        {
            for (int dx = -cr; dx <= cr; dx++)
            {
                if (dx * dx + dy * dy > cr * cr) continue;
                int px = cx + dx;
                int py = cy + dy;
                if (px < 0 || px >= resolution || py < 0 || py >= resolution) continue;

                int idx = py * resolution + px;
                if (cellState[idx] < 2)
                {
                    cellState[idx] = 2;
                    dirty = true;
                }
            }
        }
    }

    private void LateUpdate()
    {
        bool needsDowngrade = false;
        for (int i = 0; i < cellState.Length; i++)
        {
            if (cellState[i] == 2)
            {
                cellState[i] = 1;
                needsDowngrade = true;
            }
        }
        if (needsDowngrade) dirty = true;
        if (!dirty) return;
        dirty = false;

        for (int i = 0; i < cellState.Length; i++)
        {
            switch (cellState[i])
            {
                case 0: pixels[i] = unexploredColor; break;
                case 1: pixels[i] = exploredColor;   break;
                case 2: pixels[i] = visibleColor;    break;
            }
        }

        fogTexture.SetPixels(pixels);
        fogTexture.Apply(false);
    }

    private int WorldToCell(float worldCoord)
    {
        float t = (worldCoord + mapHalfSize) / (mapHalfSize * 2f);
        return Mathf.Clamp(Mathf.FloorToInt(t * resolution), 0, resolution - 1);
    }

    public bool IsVisible(Vector3 worldPos)
    {
        int cx  = WorldToCell(worldPos.x);
        int cy  = WorldToCell(worldPos.z);
        int idx = cy * resolution + cx;
        return cellState[idx] == 2;
    }

    public bool IsExplored(Vector3 worldPos)
    {
        int cx  = WorldToCell(worldPos.x);
        int cy  = WorldToCell(worldPos.z);
        int idx = cy * resolution + cx;
        return cellState[idx] >= 1;
    }
}