using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameUI v11 — building spawn panel.
/// Wire the close button's OnClick to ClosePanel() in the Inspector,
/// OR assign it to the closeButton field and it auto-wires.
/// </summary>
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject buildingPanel;
    [SerializeField] private Transform  buttonContainer;
    [SerializeField] private GameObject spawnButtonPrefab;
    [SerializeField] private TMP_Text   buildingNameText;
    [SerializeField] private Button     closeButton;       // X button on the panel

    private Building             activeBuilding;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (closeButton != null)
            closeButton.onClick.AddListener(HideBuildingUI);

        // Start hidden — safe null check
        if (buildingPanel != null)
            buildingPanel.SetActive(false);
    }

    // ─── Public API ──────────────────────────────────────────────────────

    public void ShowBuildingUI(Building building)
    {
        UnitInfoUI.Instance?.Hide();
        ResourceInfoUI.Instance?.Hide();
        if (building == null) return;
        activeBuilding = building;

        if (buildingNameText != null)
            buildingNameText.text = building.BuildingName;

        RebuildSpawnButtons(building);

        if (buildingPanel != null)
            buildingPanel.SetActive(true);
    }

    public void HideBuildingUI()
    {
        if (buildingPanel != null)
            buildingPanel.SetActive(false);
        activeBuilding = null;
        ClearButtons();
    }

    // Alias so it can be called from button OnClick in Inspector
    public void ClosePanel() => HideBuildingUI();

    // ─── Private ─────────────────────────────────────────────────────────

    private void RebuildSpawnButtons(Building building)
    {
        ClearButtons();
        if (spawnButtonPrefab == null || buttonContainer == null) return;

        List<GameObject> prefabs = building.SpawnablePrefabs;
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == null) continue;
            int    capturedIndex = i;
            string label         = prefabs[i].name;

            GameObject btnObj = Instantiate(spawnButtonPrefab, buttonContainer);
            spawnedButtons.Add(btnObj);

            TMP_Text txt = btnObj.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = $"Spawn\n{label}";

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnSpawnButtonClicked(capturedIndex));
        }
    }

    private void ClearButtons()
    {
        foreach (GameObject btn in spawnedButtons)
            if (btn != null) Destroy(btn);
        spawnedButtons.Clear();
    }

    private void OnSpawnButtonClicked(int index)
    {
        if (activeBuilding == null)
        {
            Debug.LogWarning("[GameUI] No active building.");
            return;
        }
        activeBuilding.SpawnUnit(index);
    }
}
