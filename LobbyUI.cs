using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("Lobby Panel")]
    [SerializeField] private GameObject lobbyPanel;

    [Header("Player List")]
    [SerializeField] private Transform  playerListContainer;
    [SerializeField] private GameObject playerRowPrefab;

    [Header("Local Player Controls")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button         readyButton;
    [SerializeField] private TMP_Text       readyButtonText;

    [Header("Color Picker Panel")]
    [SerializeField] private GameObject colorPickerPanel;
    [SerializeField] private Button[]   colorButtons;

    [Header("Host Controls")]
    [SerializeField] private GameObject hostControls;
    [SerializeField] private Button     startButton;
    [SerializeField] private TMP_Text   startButtonText;

    [Header("Team Colors")]
    [SerializeField] private Color team1Color = new Color(0.2f, 0.4f, 1f);
    [SerializeField] private Color team2Color = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color team3Color = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color team4Color = new Color(0.8f, 0.8f, 0.2f);

    private List<LobbyPlayer> players    = new List<LobbyPlayer>();
    private List<GameObject>  playerRows = new List<GameObject>();
    private LobbyPlayer       localPlayer;
    private bool              colorPickerOpen = false;

    private void Awake()
    {
        Debug.Log($"[LobbyUI] Awake on {gameObject.name}, lobbyPanel={lobbyPanel}");

        if (Instance != null && Instance != this)
        {
            if (Instance.lobbyPanel == null && lobbyPanel != null)
                Instance = this;
            else { Destroy(this); return; }
        }
        else Instance = this;

        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (colorPickerPanel != null) colorPickerPanel.SetActive(false);
    }

    private void Start()
    {
        // Wire color buttons
        for (int i = 0; i < colorButtons.Length; i++)
        {
            int idx = i;
            colorButtons[i]?.onClick.AddListener(() => OnColorPicked(idx));
            if (i < LobbyPlayer.AvailableColors.Length)
            {
                var cb = colorButtons[i].colors;
                cb.normalColor    = LobbyPlayer.AvailableColors[i];
                cb.highlightedColor = LobbyPlayer.AvailableColors[i] * 1.2f;
                colorButtons[i].colors = cb;
                // Clear button text
                var txt = colorButtons[i].GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = "";
            }
        }

        readyButton?.onClick.AddListener(OnReadyClicked);
        startButton?.onClick.AddListener(OnStartClicked);

        // Name input — send to server when edited
        nameInputField?.onEndEdit.AddListener(OnNameEdited);
    }

    // ── Show / Hide ───────────────────────────────────────────────────

    public void ShowLobby()
    {
        if (lobbyPanel == null)
        { Debug.LogError("[LobbyUI] lobbyPanel not assigned!"); return; }

        MainMenuUI.Instance?.HideAllPanels();
        lobbyPanel.SetActive(true);
        colorPickerPanel?.SetActive(false);

        bool isHost = Mirror.NetworkServer.active;
        if (hostControls != null) hostControls.SetActive(isHost);
    }

    public void HideLobby() => lobbyPanel?.SetActive(false);

    // ── Player registration ───────────────────────────────────────────

    public void RegisterPlayer(LobbyPlayer player)
    {
        if (!players.Contains(player)) players.Add(player);
        if (player.isLocalPlayer)
        {
            localPlayer = player;
            if (nameInputField != null)
                nameInputField.text = player.playerName;
        }
        RefreshPlayerList();
    }

    public void UnregisterPlayer(LobbyPlayer player)
    {
        players.Remove(player);
        if (localPlayer == player) localPlayer = null;
        RefreshPlayerList();
    }

    // ── Refresh player rows ───────────────────────────────────────────

    public void RefreshPlayerList()
    {
        foreach (var row in playerRows) if (row) Destroy(row);
        playerRows.Clear();

        foreach (LobbyPlayer p in players)
        {
            if (p == null || playerRowPrefab == null || playerListContainer == null) continue;

            GameObject row = Instantiate(playerRowPrefab, playerListContainer);
            playerRows.Add(row);

            // Name
            var nameText = row.transform.Find("NameText")?.GetComponent<TMP_Text>();
            if (nameText) nameText.text = p.playerName;

            // Color swatch
            var colorImg = row.transform.Find("ColorImage")?.GetComponent<Image>();
            if (colorImg) colorImg.color = p.playerColor;

            // Team badge
            var teamText = row.transform.Find("TeamText")?.GetComponent<TMP_Text>();
            if (teamText)
            {
                teamText.text = $"Team {p.teamIndex + 1}";
                teamText.color = GetTeamColor(p.teamIndex);
            }

            // Ready indicator
            var readyText = row.transform.Find("ReadyText")?.GetComponent<TMP_Text>();
            if (readyText)
            {
                readyText.text  = p.isReady ? "✓" : "...";
                readyText.color = p.isReady ? Color.green : Color.gray;
            }

            // Team change buttons (only for local player's row)
            var teamUpBtn   = row.transform.Find("TeamUpBtn")?.GetComponent<Button>();
            var teamDownBtn = row.transform.Find("TeamDownBtn")?.GetComponent<Button>();
            if (teamUpBtn != null)
            {
                teamUpBtn.gameObject.SetActive(p.isLocalPlayer);
                LobbyPlayer captured = p;
                teamUpBtn.onClick.AddListener(() =>
                    captured.CmdSetTeam((captured.teamIndex + 1) % 4));
            }
            if (teamDownBtn != null)
            {
                teamDownBtn.gameObject.SetActive(p.isLocalPlayer);
                LobbyPlayer captured = p;
                teamDownBtn.onClick.AddListener(() =>
                    captured.CmdSetTeam((captured.teamIndex + 3) % 4));
            }

            // Color picker button (only for local player's row)
            var colorBtn = row.transform.Find("ColorBtn")?.GetComponent<Button>();
            if (colorBtn != null)
            {
                colorBtn.gameObject.SetActive(p.isLocalPlayer);
                colorBtn.onClick.AddListener(ToggleColorPicker);
            }

            // Kick button (host only, not self)
            var kickBtn = row.transform.Find("KickButton")?.GetComponent<Button>();
            if (kickBtn != null)
            {
                bool canKick = Mirror.NetworkServer.active && !p.isLocalPlayer;
                kickBtn.gameObject.SetActive(canKick);
                if (canKick)
                {
                    LobbyPlayer captured = p;
                    kickBtn.onClick.AddListener(() => OnKickClicked(captured));
                }
            }
        }

        UpdateStartButton();
    }

    private Color GetTeamColor(int index)
    {
        switch (index)
        {
            case 0: return team1Color;
            case 1: return team2Color;
            case 2: return team3Color;
            case 3: return team4Color;
            default: return Color.white;
        }
    }

    private void UpdateStartButton()
    {
        if (startButton == null) return;
        bool allReady = players.Count >= 2;
        foreach (LobbyPlayer p in players)
            if (p != null && !p.isReady) { allReady = false; break; }
        startButton.interactable = allReady;
        if (startButtonText != null)
            startButtonText.text = allReady ? "▶  Start Game" : "Waiting for players...";
    }

    // ── Color Picker ─────────────────────────────────────────────────

    public void ToggleColorPicker()
    {
        if (colorPickerPanel == null) return;
        colorPickerOpen = !colorPickerOpen;
        colorPickerPanel.SetActive(colorPickerOpen);
    }

    private void OnColorPicked(int index)
    {
        if (localPlayer == null || index >= LobbyPlayer.AvailableColors.Length) return;
        localPlayer.CmdSetColor(LobbyPlayer.AvailableColors[index]);
        colorPickerPanel?.SetActive(false);
        colorPickerOpen = false;
    }

    // ── Other handlers ────────────────────────────────────────────────

    private void OnNameEdited(string newName)
    {
        if (localPlayer == null) return;
        string trimmed = newName.Trim();
        if (trimmed.Length == 0) trimmed = $"Player {localPlayer.playerIndex + 1}";
        localPlayer.CmdSetName(trimmed);
    }

    private void OnReadyClicked()
    {
        if (localPlayer == null) return;
        bool newReady = !localPlayer.isReady;
        localPlayer.CmdSetReady(newReady);
        if (readyButtonText != null)
            readyButtonText.text = newReady ? "✓  Ready" : "Not Ready";
    }

    private void OnStartClicked() => RTSNetworkManager.Instance?.StartMatch();

    private void OnKickClicked(LobbyPlayer player) =>
        RTSNetworkManager.Instance?.KickPlayer(player);
}