using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows build menu when a Villager is selected.
/// Attach to GameManager. Wire in Inspector.
/// </summary>
public class BuildMenuUI : MonoBehaviour
{
    public static BuildMenuUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject buildPanel;
    [SerializeField] private Button     closeButton;

    [Header("Building Entries")]
    [SerializeField] private BuildEntry[] buildings;

    [System.Serializable]
    public class BuildEntry
    {
        public string      buildingName;
        public GameObject  buildingPrefab;
        public GameObject  constructionSitePrefab;
        public Material    ghostMaterial;
        public float       buildTime    = 10f;
        public int         costFood     = 0;
        public int         costWood     = 50;
        public int         costGold     = 0;
        public Sprite      icon;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        closeButton?.onClick.AddListener(Hide);
        buildPanel?.SetActive(false);
    }

    private void Start()
    {
        // Auto-create buttons for each building entry
        // (you can also wire buttons manually in Inspector)
    }

    public void Show()
    {
        buildPanel?.SetActive(true);
    }

    public void Hide()
    {
        buildPanel?.SetActive(false);
        BuildingPlacer.Instance?.CancelPlacement();
    }

    /// <summary>Called by build buttons in the Inspector.</summary>
    public void OnBuildButtonClicked(int index)
    {
        if (index < 0 || index >= buildings.Length) return;
        BuildEntry entry = buildings[index];

        // Check resources
        if (!ResourceManager.Instance.TrySpend(
            entry.costFood, entry.costWood, entry.costGold))
        {
            Debug.Log("[BuildMenu] Not enough resources.");
            return;
        }

        Hide();
        BuildingPlacer.Instance?.StartPlacing(
            entry.buildingPrefab,
            entry.constructionSitePrefab,
            entry.buildTime,
            entry.ghostMaterial);
    }
}