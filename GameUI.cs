using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameUI — building spawn panel + live training status.
/// Training UI always updates as long as a building is tracked,
/// even when the panel is closed (so it's fresh on re-open).
/// </summary>
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject buildingPanel;
    [SerializeField] private Transform  buttonContainer;
    [SerializeField] private GameObject spawnButtonPrefab;
    [SerializeField] private TMP_Text   buildingNameText;
    [SerializeField] private Button     closeButton;

    [Header("Training Progress")]
    [SerializeField] private Image    trainingProgressFill;   // Image (filled)
    [SerializeField] private TMP_Text trainingStatusText;     // "Training: Infantry x3  67%"
    [SerializeField] private TMP_Text trainingQueueText;      // "Next: Cavalry | Infantry x2"
    [SerializeField] private GameObject trainingAlwaysPanel;
    
    [Header("Training Panel - Slot 2")]
    [SerializeField] private Image      trainingProgressFill2;
    [SerializeField] private TMP_Text   trainingStatusText2;
    [SerializeField] private TMP_Text   trainingQueueText2;
    [SerializeField] private GameObject trainingAlwaysPanel2;

    private Building             activeBuilding;
    private Building             slot1Building;
    private Building             activeBuildingSlot2;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    // ────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        closeButton?.onClick.AddListener(HideBuildingUI);
        buildingPanel?.SetActive(false);
    }

    private void Update()
    {
        // Slot 1 panel — always shows HomeSite training
        Building display1 = slot1Building != null && slot1Building.IsTraining ? slot1Building : null;
        if (display1 != null) RefreshTrainingUIFor(display1);
        else                  ClearTrainingUI();

        // Slot 2 panel — always shows Barracks training
        Building display2 = activeBuildingSlot2 != null && activeBuildingSlot2.IsTraining ? activeBuildingSlot2 : null;
        if (display2 != null) RefreshTrainingUIForSlot2(display2);
        else                  ClearTrainingUISlot2();
    }

    // ── Public API ───────────────────────────────────────────────────────

    public void ShowBuildingUI(Building building)
    {
        UnitInfoUI.Instance?.Hide();
        ResourceInfoUI.Instance?.Hide();
        if (building == null) return;

        activeBuilding = building;

        if (buildingNameText != null)
            buildingNameText.text = building.BuildingName;

        RebuildSpawnButtons(building);

        buildingPanel?.SetActive(true);
        RefreshTrainingUIFor(building);
    }

    public void HideBuildingUI()
    {
        buildingPanel?.SetActive(false);
        activeBuilding = null;
        ClearButtons();
    }

    // ── Training UI ──────────────────────────────────────────────────────
    
    private void RefreshTrainingUIFor(Building b)
    {
        if (trainingAlwaysPanel != null) trainingAlwaysPanel.SetActive(true);

        float  pct   = b.TrainingProgress;
        string label = b.CurrentTrainingLabel;

        if (trainingProgressFill) trainingProgressFill.fillAmount = pct;
        if (trainingStatusText)
            trainingStatusText.text = string.IsNullOrEmpty(label)
                ? "" : $"{b.BuildingName}: {label}  {(pct * 100f):F0}%";

        string[] queue = b.GetQueueLabels();
        if (trainingQueueText)
            trainingQueueText.text = queue.Length == 0 ? "" : "Next: " + string.Join(" | ", queue);
    }

    private void ClearTrainingUI()
    {
        if (trainingAlwaysPanel != null) trainingAlwaysPanel.SetActive(false);
        if (trainingProgressFill) trainingProgressFill.fillAmount = 0f;
        if (trainingStatusText)   trainingStatusText.text = "";
        if (trainingQueueText)    trainingQueueText.text  = "";
    }

    public void RegisterBuilding(Building b)
    {
        if (b is HomeSite)   slot1Building       = b;
        else if (b is Barracks) activeBuildingSlot2 = b;
    }

    private void RefreshTrainingUIForSlot2(Building b)
    {
        if (trainingAlwaysPanel2 != null) trainingAlwaysPanel2.SetActive(true);
        float  pct   = b.TrainingProgress;
        string label = b.CurrentTrainingLabel;
        if (trainingProgressFill2) trainingProgressFill2.fillAmount = pct;
        if (trainingStatusText2)
            trainingStatusText2.text = string.IsNullOrEmpty(label)
                ? "" : $"{b.BuildingName}: {label}  {(pct * 100f):F0}%";
        string[] queue = b.GetQueueLabels();
        if (trainingQueueText2)
            trainingQueueText2.text = queue.Length == 0 ? "" : "Next: " + string.Join(" | ", queue);
    }

    private void ClearTrainingUISlot2()
    {
        if (trainingAlwaysPanel2 != null) trainingAlwaysPanel2.SetActive(false);
        if (trainingProgressFill2) trainingProgressFill2.fillAmount = 0f;
        if (trainingStatusText2)   trainingStatusText2.text = "";
        if (trainingQueueText2)    trainingQueueText2.text  = "";
    }

    // ── Spawn buttons ────────────────────────────────────────────────────

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
            if (txt != null) txt.text = $"Train\n{label}";

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
        if (activeBuilding == null) { Debug.LogWarning("[GameUI] No active building."); return; }
        activeBuilding.SpawnUnit(index);
    }
}