using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows building info in the center panel.
/// Displays name, health bar, and spawn point button.
/// Attach to GameManager.
/// </summary>
public class BuildingInfoUI : MonoBehaviour
{
    public static BuildingInfoUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button     closeButton;

    [Header("Info Fields")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image    healthBarFill;

    [Header("Spawn Point")]
    [SerializeField] private Button   setSpawnPointButton;
    [SerializeField] private TMP_Text spawnPointStatusText;

    // Construction progress (for sites)
    [Header("Construction")]
    [SerializeField] private GameObject constructionGroup;
    [SerializeField] private TMP_Text   progressText;
    [SerializeField] private Image      progressBarFill;

    private Building         trackedBuilding;
    private ConstructionSite trackedSite;
    private bool             settingSpawnPoint = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        closeButton?.onClick.AddListener(Hide);
        setSpawnPointButton?.onClick.AddListener(OnSetSpawnPointClicked);
        panel?.SetActive(false);
    }

    private void Update()
    {
        if (trackedBuilding != null) RefreshBuilding();
        if (trackedSite     != null) RefreshSite();

        // Click ground to set spawn point
        if (settingSpawnPoint && Input.GetMouseButtonDown(0))
        {
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    trackedBuilding?.SetSpawnPoint(hit.point);
                    MoveFlag.Instance?.ShowSpawnFlag(hit.point);
                    settingSpawnPoint = false;
                    if (spawnPointStatusText != null)
                        spawnPointStatusText.text = "Spawn point set ✓";
                }
            }
        }
    }

    public void ShowBuilding(Building building)
    {
        trackedBuilding = building;
        trackedSite     = null;

        UnitInfoUI.Instance?.Hide();
        ResourceInfoUI.Instance?.Hide();

        bool isEnemy = building.OwnerPlayerId != PlayerColorManager.LocalPlayerIndex;
        if (nameText) nameText.text = building.BuildingName;
        if (constructionGroup) constructionGroup.SetActive(false);
        if (setSpawnPointButton) setSpawnPointButton.gameObject.SetActive(!isEnemy);
        if (spawnPointStatusText)
        {
            spawnPointStatusText.gameObject.SetActive(!isEnemy);
            if (!isEnemy) spawnPointStatusText.text = "Click to set spawn point";
        }

        RefreshBuilding();
        panel?.SetActive(true);
    }

    public void ShowConstructionSite(ConstructionSite site)
    {
        trackedSite     = site;
        trackedBuilding = null;

        UnitInfoUI.Instance?.Hide();
        ResourceInfoUI.Instance?.Hide();

        if (nameText) nameText.text = "Under Construction...";
        if (constructionGroup) constructionGroup.SetActive(true);
        if (setSpawnPointButton) setSpawnPointButton.gameObject.SetActive(false);

        panel?.SetActive(true);
    }

    public void Hide()
    {
        trackedBuilding  = null;
        trackedSite      = null;
        settingSpawnPoint = false;
        panel?.SetActive(false);
        MoveFlag.Instance?.ClearRallyFlag();
    }

    private void RefreshBuilding()
    {
        if (trackedBuilding == null) return;
        int cur = trackedBuilding.CurrentBuildingHealth;
        int max = trackedBuilding.MaxBuildingHealth;
        if (healthText)    healthText.text         = $"HP: {cur} / {max}";
        if (healthBarFill) healthBarFill.fillAmount = max > 0 ? Mathf.Clamp01((float)cur / max) : 1f;
    }

    private void RefreshSite()
    {
        if (trackedSite == null) { Hide(); return; }
        float ratio = trackedSite.BuildTime > 0
            ? trackedSite.Progress / trackedSite.BuildTime : 0f;
        if (progressText)
            progressText.text = $"Building... {(ratio * 100f):F0}%";
        if (progressBarFill)
            progressBarFill.fillAmount = Mathf.Clamp01(ratio);
    }

    private void OnSetSpawnPointClicked()
    {
        settingSpawnPoint = true;
        if (spawnPointStatusText)
            spawnPointStatusText.text = "Click on the map to set spawn point...";
    }
}