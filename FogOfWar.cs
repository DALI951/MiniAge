using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FogOfWar — grid-based fog of war rendered as a texture on a UI RawImage
/// that covers the entire screen.
///
/// Three states per cell:
///   0 = Unexplored   → fully black
///   1 = Explored     → dark gray overlay (you've been here but can't see it now)
///   2 = Visible      → clear (a unit is nearby right now)
///
/// Setup in Unity:
///   1. Attach this script to GameManager.
///   2. Create a UI RawImage in your Canvas that covers the FULL screen.
///      Set its color to white. Name it "FogOverlay".
///   3. Drag FogOverlay into the fogOverlay field.
///   4. Set mapHalfSize to match your MapBoundary / SpawnAreaManager.
///   5. Each unit that should reveal fog: attach FogRevealer.cs to it.
/// </summary>
public class FogOfWar : MonoBehaviour
{
    public static FogOfWar Instance { get; private set; }

    [Header("References")]
    [Tooltip("A full-screen RawImage in the Canvas. Set its color to white.")]
    [SerializeField] private RawImage fogOverlay;

    [Header("Map Settings — match SpawnAreaManager")]
    [SerializeField] private float mapHalfSize = 23f;

    [Header("Grid Resolution")]
    [Tooltip("Higher = sharper fog edges but slower. 128 is a good default.")]
    [SerializeField] private int resolution = 128;

    [Header("Colors")]
    [SerializeField] private Color unexploredColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color exploredColor   = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color visibleColor    = new Color(0f, 0f, 0f, 0f);

    // ── Runtime ───────────────────────────────────────────────────────────
    private Texture2D fogTexture;
    private byte[]    cellState;     // 0=unseen, 1=explored, 2=visible
    private Color[]   pixels;
    private bool      dirty = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Create fog texture
        fogTexture         = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        fogTexture.filterMode = FilterMode.Bilinear;
        cellState          = new byte[resolution * resolution];
        pixels             = new Color[resolution * resolution];

        // Start fully black
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = unexploredColor;

        fogTexture.SetPixels(pixels);
        fogTexture.Apply();

        if (fogOverlay != null)
            fogOverlay.texture = fogTexture;
    }

    // ── Called by FogRevealer every frame ─────────────────────────────────

    /// <summary>
    /// Reveal a circle of cells around worldPos.
    /// radiusWorld is the sight range in world units.
    /// </summary>
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
                    cellState[idx] = 2; // visible
                    dirty = true;
                }
            }
        }
    }

    // ── Called once per frame (LateUpdate) to rebuild texture if needed ───

    private void LateUpdate()
    {
        // Step 1: downgrade all "visible" cells to "explored"
        // (they'll be re-upgraded by revealers this frame)
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

        // Note: FogRevealers call Reveal() in their Update (before LateUpdate)
        // so visible cells get re-marked 2 before we rebuild the texture here.
        // The order is: Update (revealers reveal) → LateUpdate (we rebuild).

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

    // ── Helpers ───────────────────────────────────────────────────────────

    private int WorldToCell(float worldCoord)
    {
        // Map [-halfSize, +halfSize] → [0, resolution]
        float t = (worldCoord + mapHalfSize) / (mapHalfSize * 2f);
        return Mathf.Clamp(Mathf.FloorToInt(t * resolution), 0, resolution - 1);
    }

    /// <summary>Returns true if a world position is currently visible.</summary>
    public bool IsVisible(Vector3 worldPos)
    {
        int cx  = WorldToCell(worldPos.x);
        int cy  = WorldToCell(worldPos.z);
        int idx = cy * resolution + cx;
        return cellState[idx] == 2;
    }
}
